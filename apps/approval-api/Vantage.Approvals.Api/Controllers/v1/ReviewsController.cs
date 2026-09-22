using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vantage.Approvals.Api.Auth;
using Vantage.Approvals.Api.Contracts;
using Vantage.Approvals.Api.Exceptions;
using Vantage.Approvals.Api.Services.Interfaces;
using Vantage.Approvals.Core.Models;

namespace Vantage.Approvals.Api.Controllers.V1;

/// <summary>
/// Review rounds for one client. Every route carries the client code: the portal is multi-client,
/// and a request that cannot name its client has no business reaching a service method.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("v1/{clientCode}/reviews")]
[Authorize(
    AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme
        + ","
        + ReviewerSessionAuthenticationHandler.SchemeName
)]
public sealed class ReviewsController(ICreativeReviewService reviews) : ControllerBase
{
    /// <summary>Rounds currently out for decision with this client.</summary>
    [HttpGet]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult<IReadOnlyList<ReviewResponse>>> GetOutstanding(
        string clientCode,
        CancellationToken cancellationToken
    )
    {
        var outstanding = await reviews
            .GetOutstandingForClientAsync(clientCode, cancellationToken)
            .ConfigureAwait(false);

        return this.Ok(outstanding.Select(ReviewResponse.From).ToList());
    }

    [HttpGet("{reviewId:guid}")]
    public async Task<ActionResult<ReviewResponse>> Get(
        string clientCode,
        Guid reviewId,
        CancellationToken cancellationToken
    )
    {
        var review =
            await reviews.GetAsync(reviewId, cancellationToken).ConfigureAwait(false)
            ?? throw new ReviewNotFoundException(reviewId);

        // A reviewer's cookie is scoped to one round; it must not read another client's work.
        if (!string.Equals(review.ClientCode, clientCode, StringComparison.OrdinalIgnoreCase))
        {
            return this.NotFound();
        }

        return this.Ok(ReviewResponse.From(review));
    }

    /// <summary>Creates the next round for a campaign as a draft. Producers only.</summary>
    [HttpPost]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult<ReviewResponse>> CreateDraft(
        string clientCode,
        [FromBody] CreateReviewBody body,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(body);

        var review = await reviews
            .CreateDraftAsync(
                clientCode,
                new CreateReviewRequest
                {
                    CampaignId = body.CampaignId,
                    CampaignName = body.CampaignName,
                    ReviewerEmail = body.ReviewerEmail,
                    ReviewerName = body.ReviewerName,
                    Locale = body.Locale,
                    Assets =
                    [
                        .. body.Assets.Select(asset => new CreativeAsset
                        {
                            AssetId = Guid.NewGuid(),
                            Name = asset.Name,
                            Format = asset.Format,
                            PreviewPath = asset.PreviewPath,
                        }),
                    ],
                },
                this.CallerName(),
                cancellationToken
            )
            .ConfigureAwait(false);

        return this.CreatedAtAction(
            nameof(this.Get),
            new { clientCode, reviewId = review.ReviewId },
            ReviewResponse.From(review)
        );
    }

    /// <summary>Sends a round to its reviewer. Producers only.</summary>
    [HttpPost("{reviewId:guid}/submit")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult<ReviewResponse>> Submit(
        string clientCode,
        Guid reviewId,
        CancellationToken cancellationToken
    )
    {
        var review = await reviews
            .SubmitAsync(
                new SubmitReviewRequest
                {
                    ReviewId = reviewId,
                    ClientCode = clientCode,
                    // Derived from the request, not configured: one deployment serves several
                    // hostnames, and the link in the email has to match the one the client used.
                    PortalBaseUrl = $"{this.Request.Scheme}://{this.Request.Host}",
                    SubmittedBy = this.CallerName(),
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return this.Ok(ReviewResponse.From(review));
    }

    /// <summary>Records the client's answer. Reached with the reviewer's session cookie.</summary>
    [HttpPost("{reviewId:guid}/decision")]
    [Authorize(AuthenticationSchemes = ReviewerSessionAuthenticationHandler.SchemeName)]
    public async Task<ActionResult<ReviewResponse>> RecordDecision(
        string clientCode,
        Guid reviewId,
        [FromBody] DecisionBody body,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(body);

        // The cookie is issued per round. Accepting a decision for a different round would let one
        // valid link answer for every review a client has open.
        var cookieReviewId = this.User.FindFirst(
            ReviewerSessionAuthenticationHandler.ReviewIdClaimType
        )?.Value;

        if (!Guid.TryParse(cookieReviewId, out var scopedReviewId) || scopedReviewId != reviewId)
        {
            return this.Forbid();
        }

        var review = await reviews
            .RecordDecisionAsync(
                new RecordDecisionRequest
                {
                    ReviewId = reviewId,
                    ClientCode = clientCode,
                    Outcome = body.Outcome,
                    DecidedByEmail = this.CallerName(),
                    Comment = body.Comment,
                    AssetIds = body.AssetIds,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return this.Ok(ReviewResponse.From(review));
    }

    private string CallerName() =>
        this.User.Identity?.Name ?? this.User.FindFirst("email")?.Value ?? "unknown";
}
