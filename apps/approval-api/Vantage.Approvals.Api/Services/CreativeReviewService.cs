using System.Globalization;
using Vantage.Approvals.Api.Clients;
using Vantage.Approvals.Api.Exceptions;
using Vantage.Approvals.Api.Logging;
using Vantage.Approvals.Api.Services.Interfaces;
using Vantage.Approvals.Core.Models;
using Vantage.Approvals.Core.Repositories;

namespace Vantage.Approvals.Api.Services;

internal sealed class CreativeReviewService(
    IReviewRepository reviews,
    IReviewAssignmentRepository assignments,
    IReviewDecisionRepository decisions,
    IReviewSnapshotStore snapshots,
    INotificationSender notifications,
    IStageTransitionService stages,
    TimeProvider clock,
    ILogger<CreativeReviewService> logger
) : ICreativeReviewService
{
    public async Task<CreativeReview> CreateDraftAsync(
        string clientCode,
        CreateReviewRequest request,
        string createdBy,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        var now = clock.GetUtcNow();
        var existing = await assignments
            .GetLatestByCampaignAsync([request.CampaignId], cancellationToken)
            .ConfigureAwait(false);

        var nextVersion = existing.TryGetValue(request.CampaignId, out var latest)
            ? latest.Version + 1
            : 1;

        var review = new CreativeReview
        {
            ReviewId = Guid.NewGuid(),
            CampaignId = request.CampaignId,
            ClientCode = clientCode,
            CampaignName = request.CampaignName,
            Version = nextVersion,
            Status = ReviewStatus.Draft,
            Locale = request.Locale,
            ReviewerEmail = request.ReviewerEmail,
            ReviewerName = request.ReviewerName,
            CreatedUtc = now,
            LastModifiedUtc = now,
            LastModifiedBy = createdBy,
            IsActive = true,
            Assets = [.. request.Assets],
        };

        await reviews.CreateAsync(review, cancellationToken).ConfigureAwait(false);

        await assignments
            .UpsertAsync(
                new ReviewAssignment
                {
                    AssignmentId = Guid.NewGuid(),
                    ReviewId = review.ReviewId,
                    CampaignId = review.CampaignId,
                    ClientCode = clientCode,
                    Version = review.Version,
                    IsActive = true,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        Log.DraftCreated(logger, review.Version, review.CampaignId, review.ReviewId);

        return review;
    }

    /// <summary>
    /// Sends a round to its reviewer.
    /// </summary>
    /// <remarks>
    /// The order of the six steps below is a contract, not a preference:
    /// <list type="number">
    ///   <item>read the round — nothing is written before the state is known to be legal;</item>
    ///   <item>snapshot the notification payload, <em>before</em> sending it, so a message that
    ///   went out is always backed by a stored copy and never the other way round;</item>
    ///   <item>update the row — the round is now officially sent, and a crash after this point
    ///   leaves a sent round with a snapshot, which is recoverable;</item>
    ///   <item>snapshot the round as sent, the artefact a dispute is answered with;</item>
    ///   <item>send the notification — the first externally visible effect, deliberately after
    ///   everything that can still be rolled forward by a retry;</item>
    ///   <item>advance the campaign stage, per campaign, idempotently.</item>
    /// </list>
    /// No step is wrapped in a swallow-and-continue handler. A failure propagates, the caller sees
    /// it, and the operation is retried — every step above is safe to repeat.
    /// </remarks>
    public async Task<CreativeReview> SubmitAsync(
        SubmitReviewRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. Read.
        var review =
            await reviews.GetAsync(request.ReviewId, cancellationToken).ConfigureAwait(false)
            ?? throw new ReviewNotFoundException(request.ReviewId);

        if (review.Status != ReviewStatus.Draft && review.Status != ReviewStatus.ChangesRequested)
        {
            throw new ReviewStateException(
                review.ReviewId,
                $"A round in {review.Status} cannot be sent for decision."
            );
        }

        var now = clock.GetUtcNow();
        var reviewUrl = BuildReviewUrl(request.PortalBaseUrl, review);

        var notification = new ReviewNotification
        {
            ToAddress = review.ReviewerEmail,
            ToName = review.ReviewerName,
            Subject = $"{review.CampaignName}: round {review.Version} is ready for your review",
            Body =
                $"{review.ReviewerName}, round {review.Version} of {review.CampaignName} is ready. "
                + "Open the link below to approve it or request changes.",
            ReviewUrl = reviewUrl,
        };

        // 2. Snapshot what is about to be sent.
        await snapshots
            .WriteAsync(
                review.ClientCode,
                review.ReviewId,
                review.Version,
                "notification",
                notification,
                cancellationToken
            )
            .ConfigureAwait(false);

        // 3. Update the row.
        review.Status = ReviewStatus.AwaitingDecision;
        review.SentDate = now;
        review.LastModifiedUtc = now;
        review.LastModifiedBy = request.SubmittedBy;
        await reviews.UpdateAsync(review, cancellationToken).ConfigureAwait(false);

        // 4. Snapshot the round as sent.
        await snapshots
            .WriteAsync(
                review.ClientCode,
                review.ReviewId,
                review.Version,
                "review",
                review,
                cancellationToken
            )
            .ConfigureAwait(false);

        // 5. Send.
        await notifications.SendAsync(notification, cancellationToken).ConfigureAwait(false);

        // 6. Advance the stage for every campaign on the round. One campaign today, a list by
        //    contract: indexing into the first element is the bug this shape exists to prevent.
        await stages
            .AdvanceAsync(
                [review.CampaignId],
                CampaignStage.AwaitingClientDecision,
                cancellationToken
            )
            .ConfigureAwait(false);

        return review;
    }

    /// <summary>
    /// Records the reviewer's answer. Same ordering rule as <see cref="SubmitAsync"/>: the
    /// decision is snapshotted before the row moves, and the upstream stage is advanced last.
    /// </summary>
    public async Task<CreativeReview> RecordDecisionAsync(
        RecordDecisionRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        var review =
            await reviews.GetAsync(request.ReviewId, cancellationToken).ConfigureAwait(false)
            ?? throw new ReviewNotFoundException(request.ReviewId);

        if (review.Status != ReviewStatus.AwaitingDecision)
        {
            throw new ReviewStateException(
                review.ReviewId,
                $"A round in {review.Status} is not awaiting a decision."
            );
        }

        var now = clock.GetUtcNow();
        var decision = new ReviewDecision
        {
            DecisionId = Guid.NewGuid(),
            ReviewId = review.ReviewId,
            Outcome = request.Outcome,
            DecidedByEmail = request.DecidedByEmail,
            DecidedUtc = now,
            Comment = request.Comment,
            AssetIds = [.. request.AssetIds],
        };

        // Snapshot first: the client's answer is evidence, and it is written before anything
        // derived from it moves.
        await snapshots
            .WriteAsync(
                review.ClientCode,
                review.ReviewId,
                review.Version,
                "decision",
                decision,
                cancellationToken
            )
            .ConfigureAwait(false);

        await decisions.CreateAsync(decision, cancellationToken).ConfigureAwait(false);

        review.Status =
            request.Outcome == DecisionOutcome.Approved
                ? ReviewStatus.Approved
                : ReviewStatus.ChangesRequested;
        review.LastModifiedUtc = now;
        review.LastModifiedBy = request.DecidedByEmail;
        review.IsActive = request.Outcome != DecisionOutcome.Approved;
        await reviews.UpdateAsync(review, cancellationToken).ConfigureAwait(false);

        // The assignment index is what the console's work list reads, so an approved round has to
        // be closed there too. Leaving it active is invisible in the review row and shows up as a
        // finished campaign that never leaves the work list.
        await this.DeactivateAssignmentAsync(review, cancellationToken).ConfigureAwait(false);

        var targetStage =
            request.Outcome == DecisionOutcome.Approved
                ? CampaignStage.Approved
                : CampaignStage.DecisionReceived;

        await stages
            .AdvanceAsync([review.CampaignId], targetStage, cancellationToken)
            .ConfigureAwait(false);

        return review;
    }

    public Task<CreativeReview?> GetAsync(
        Guid reviewId,
        CancellationToken cancellationToken = default
    ) => reviews.GetAsync(reviewId, cancellationToken);

    public async Task<IReadOnlyList<CreativeReview>> GetOutstandingForClientAsync(
        string clientCode,
        CancellationToken cancellationToken = default
    )
    {
        var outstanding = await assignments
            .GetActiveForClientAsync(clientCode, cancellationToken)
            .ConfigureAwait(false);

        if (outstanding.Count == 0)
        {
            return [];
        }

        return await reviews
            .GetManyAsync([.. outstanding.Select(item => item.ReviewId)], cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task DeactivateAssignmentAsync(
        CreativeReview review,
        CancellationToken cancellationToken
    )
    {
        if (review.IsActive)
        {
            return;
        }

        var latest = await assignments
            .GetLatestByCampaignAsync([review.CampaignId], cancellationToken)
            .ConfigureAwait(false);

        if (!latest.TryGetValue(review.CampaignId, out var assignment))
        {
            return;
        }

        assignment.IsActive = false;
        await assignments.UpsertAsync(assignment, cancellationToken).ConfigureAwait(false);
    }

    private static string BuildReviewUrl(string portalBaseUrl, CreativeReview review) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{portalBaseUrl.TrimEnd('/')}/{review.Locale}/{review.ClientCode}/review/{review.ReviewId:D}"
        );
}
