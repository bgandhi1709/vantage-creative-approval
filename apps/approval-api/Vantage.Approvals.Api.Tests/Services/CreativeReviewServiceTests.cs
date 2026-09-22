using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Vantage.Approvals.Api.Clients;
using Vantage.Approvals.Api.Exceptions;
using Vantage.Approvals.Api.Services;
using Vantage.Approvals.Api.Services.Interfaces;
using Vantage.Approvals.Core.Models;
using Vantage.Approvals.Core.Repositories;

namespace Vantage.Approvals.Api.Tests.Services;

public sealed class CreativeReviewServiceTests
{
    private readonly Mock<IReviewRepository> reviews = new();
    private readonly Mock<IReviewAssignmentRepository> assignments = new();
    private readonly Mock<IReviewDecisionRepository> decisions = new();
    private readonly Mock<IReviewSnapshotStore> snapshots = new();
    private readonly Mock<INotificationSender> notifications = new();
    private readonly Mock<IStageTransitionService> stages = new();

    /// <summary>Every side effect appends its name here, so a test can assert the order.</summary>
    private readonly List<string> effects = [];

    private readonly CreativeReviewService service;

    public CreativeReviewServiceTests()
    {
        this.snapshots
            .Setup(store =>
                store.WriteAsync(
                    It.IsAny<string>(),
                    It.IsAny<Guid>(),
                    It.IsAny<int>(),
                    It.IsAny<string>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (string _, Guid _, int _, string name, object _, CancellationToken _) =>
                    this.effects.Add($"snapshot:{name}")
            )
            .Returns(Task.CompletedTask);

        // Default: no earlier round for any campaign. Tests that care override this.
        this.assignments
            .Setup(repository =>
                repository.GetLatestByCampaignAsync(
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new Dictionary<Guid, ReviewAssignment>());

        this.reviews
            .Setup(repository =>
                repository.UpdateAsync(It.IsAny<CreativeReview>(), It.IsAny<CancellationToken>())
            )
            .Callback(() => this.effects.Add("row"))
            .Returns(Task.CompletedTask);

        this.notifications
            .Setup(sender =>
                sender.SendAsync(It.IsAny<ReviewNotification>(), It.IsAny<CancellationToken>())
            )
            .Callback(() => this.effects.Add("notify"))
            .Returns(Task.CompletedTask);

        this.stages
            .Setup(transition =>
                transition.AdvanceAsync(
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<CampaignStage>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(() => this.effects.Add("stage"))
            .Returns(Task.CompletedTask);

        this.decisions
            .Setup(repository =>
                repository.CreateAsync(It.IsAny<ReviewDecision>(), It.IsAny<CancellationToken>())
            )
            .Callback(() => this.effects.Add("decision-row"))
            .Returns(Task.CompletedTask);

        this.service = new CreativeReviewService(
            this.reviews.Object,
            this.assignments.Object,
            this.decisions.Object,
            this.snapshots.Object,
            this.notifications.Object,
            this.stages.Object,
            TimeProvider.System,
            NullLogger<CreativeReviewService>.Instance
        );
    }

    [Fact]
    public async Task SubmitAsync_RunsItsSideEffectsInTheContractedOrder()
    {
        // This ordering is the contract the whole submit path is built around: the message is on
        // disk before it is sent, and the upstream stage moves last. If this test is ever changed
        // to match new code, the code is what needs re-reading.
        var review = NewReview(ReviewStatus.Draft);
        this.GivenReviewExists(review);

        await this.service.SubmitAsync(NewSubmitRequest(review));

        Assert.Equal(
            ["snapshot:notification", "row", "snapshot:review", "notify", "stage"],
            this.effects
        );
    }

    [Fact]
    public async Task SubmitAsync_SetsTheRoundAwaitingDecisionAndStampsSentDate()
    {
        var review = NewReview(ReviewStatus.Draft);
        this.GivenReviewExists(review);

        var submitted = await this.service.SubmitAsync(NewSubmitRequest(review));

        Assert.Equal(ReviewStatus.AwaitingDecision, submitted.Status);
        Assert.NotNull(submitted.SentDate);
        Assert.Equal("producer@vantage.test", submitted.LastModifiedBy);
    }

    [Fact]
    public async Task SubmitAsync_BuildsTheReviewLinkFromTheRequestHost()
    {
        var review = NewReview(ReviewStatus.Draft);
        this.GivenReviewExists(review);
        ReviewNotification? sent = null;

        this.notifications
            .Setup(sender =>
                sender.SendAsync(It.IsAny<ReviewNotification>(), It.IsAny<CancellationToken>())
            )
            .Callback((ReviewNotification notification, CancellationToken _) => sent = notification)
            .Returns(Task.CompletedTask);

        await this.service.SubmitAsync(NewSubmitRequest(review));

        Assert.NotNull(sent);
        Assert.Equal(
            $"https://reviews.vantage.test/en-US/northwind/review/{review.ReviewId:D}",
            sent.ReviewUrl
        );
    }

    [Fact]
    public async Task SubmitAsync_WhenTheRoundWasAlreadySent_RejectsWithoutAnySideEffect()
    {
        var review = NewReview(ReviewStatus.AwaitingDecision);
        this.GivenReviewExists(review);

        await Assert.ThrowsAsync<ReviewStateException>(
            () => this.service.SubmitAsync(NewSubmitRequest(review))
        );

        Assert.Empty(this.effects);
    }

    [Fact]
    public async Task SubmitAsync_WhenTheRoundDoesNotExist_ThrowsNotFound()
    {
        this.reviews
            .Setup(repository =>
                repository.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((CreativeReview?)null);

        await Assert.ThrowsAsync<ReviewNotFoundException>(
            () =>
                this.service.SubmitAsync(
                    new SubmitReviewRequest
                    {
                        ReviewId = Guid.NewGuid(),
                        ClientCode = "northwind",
                        PortalBaseUrl = "https://reviews.vantage.test",
                        SubmittedBy = "producer@vantage.test",
                    }
                )
        );
    }

    [Fact]
    public async Task SubmitAsync_WhenTheNotificationFails_LeavesTheSnapshotsAndRowWritten()
    {
        // The send is deliberately after the writes: a failed send is retried against a round that
        // is already recorded as sent, which is safe, rather than a sent message with no record.
        var review = NewReview(ReviewStatus.Draft);
        this.GivenReviewExists(review);

        this.notifications
            .Setup(sender =>
                sender.SendAsync(It.IsAny<ReviewNotification>(), It.IsAny<CancellationToken>())
            )
            .ThrowsAsync(new InvalidOperationException("smtp is down"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => this.service.SubmitAsync(NewSubmitRequest(review))
        );

        Assert.Equal(["snapshot:notification", "row", "snapshot:review"], this.effects);
        this.stages.Verify(
            transition =>
                transition.AdvanceAsync(
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<CampaignStage>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task RecordDecisionAsync_OnApproval_SnapshotsFirstThenAdvancesToApproved()
    {
        var review = NewReview(ReviewStatus.AwaitingDecision);
        this.GivenReviewExists(review);

        var updated = await this.service.RecordDecisionAsync(
            new RecordDecisionRequest
            {
                ReviewId = review.ReviewId,
                ClientCode = "northwind",
                Outcome = DecisionOutcome.Approved,
                DecidedByEmail = "client@northwind.test",
            }
        );

        Assert.Equal(["snapshot:decision", "decision-row", "row", "stage"], this.effects);
        Assert.Equal(ReviewStatus.Approved, updated.Status);
        Assert.False(updated.IsActive);
        this.stages.Verify(
            transition =>
                transition.AdvanceAsync(
                    It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(review.CampaignId)),
                    CampaignStage.Approved,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task RecordDecisionAsync_OnApproval_ClosesTheAssignmentIndexToo()
    {
        // The console's work list reads the assignment index, not the review row. An approved round
        // that stays active there is a campaign that never leaves the list.
        var review = NewReview(ReviewStatus.AwaitingDecision);
        this.GivenReviewExists(review);
        this.assignments
            .Setup(repository =>
                repository.GetLatestByCampaignAsync(
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new Dictionary<Guid, ReviewAssignment>
                {
                    [review.CampaignId] = new ReviewAssignment
                    {
                        AssignmentId = Guid.NewGuid(),
                        ReviewId = review.ReviewId,
                        CampaignId = review.CampaignId,
                        ClientCode = review.ClientCode,
                        Version = review.Version,
                        IsActive = true,
                    },
                }
            );

        await this.service.RecordDecisionAsync(
            new RecordDecisionRequest
            {
                ReviewId = review.ReviewId,
                ClientCode = "northwind",
                Outcome = DecisionOutcome.Approved,
                DecidedByEmail = "client@northwind.test",
            }
        );

        this.assignments.Verify(
            repository =>
                repository.UpsertAsync(
                    It.Is<ReviewAssignment>(assignment => !assignment.IsActive),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task RecordDecisionAsync_OnChangesRequested_LeavesTheAssignmentOpen()
    {
        var review = NewReview(ReviewStatus.AwaitingDecision);
        this.GivenReviewExists(review);

        await this.service.RecordDecisionAsync(
            new RecordDecisionRequest
            {
                ReviewId = review.ReviewId,
                ClientCode = "northwind",
                Outcome = DecisionOutcome.ChangesRequested,
                DecidedByEmail = "client@northwind.test",
                Comment = "One more pass.",
            }
        );

        this.assignments.Verify(
            repository =>
                repository.UpsertAsync(
                    It.IsAny<ReviewAssignment>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task RecordDecisionAsync_OnChangesRequested_KeepsTheRoundActive()
    {
        var review = NewReview(ReviewStatus.AwaitingDecision);
        this.GivenReviewExists(review);

        var updated = await this.service.RecordDecisionAsync(
            new RecordDecisionRequest
            {
                ReviewId = review.ReviewId,
                ClientCode = "northwind",
                Outcome = DecisionOutcome.ChangesRequested,
                DecidedByEmail = "client@northwind.test",
                Comment = "Swap the end frame.",
            }
        );

        Assert.Equal(ReviewStatus.ChangesRequested, updated.Status);
        Assert.True(updated.IsActive);
        this.stages.Verify(
            transition =>
                transition.AdvanceAsync(
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    CampaignStage.DecisionReceived,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task RecordDecisionAsync_WhenTheRoundIsNotAwaitingADecision_Rejects()
    {
        var review = NewReview(ReviewStatus.Approved);
        this.GivenReviewExists(review);

        await Assert.ThrowsAsync<ReviewStateException>(
            () =>
                this.service.RecordDecisionAsync(
                    new RecordDecisionRequest
                    {
                        ReviewId = review.ReviewId,
                        ClientCode = "northwind",
                        Outcome = DecisionOutcome.Approved,
                        DecidedByEmail = "client@northwind.test",
                    }
                )
        );

        Assert.Empty(this.effects);
    }

    [Fact]
    public async Task CreateDraftAsync_StartsAtVersionOneForANewCampaign()
    {
        this.assignments
            .Setup(repository =>
                repository.GetLatestByCampaignAsync(
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new Dictionary<Guid, ReviewAssignment>());

        var draft = await this.service.CreateDraftAsync(
            "northwind",
            NewCreateRequest(),
            "producer@vantage.test"
        );

        Assert.Equal(1, draft.Version);
        Assert.Equal(ReviewStatus.Draft, draft.Status);
    }

    [Fact]
    public async Task CreateDraftAsync_IncrementsTheVersionOfAnExistingCampaign()
    {
        var campaignId = Guid.NewGuid();
        this.assignments
            .Setup(repository =>
                repository.GetLatestByCampaignAsync(
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new Dictionary<Guid, ReviewAssignment>
                {
                    [campaignId] = new ReviewAssignment { CampaignId = campaignId, Version = 4 },
                }
            );

        var request = NewCreateRequest();
        var draft = await this.service.CreateDraftAsync(
            "northwind",
            new CreateReviewRequest
            {
                CampaignId = campaignId,
                CampaignName = request.CampaignName,
                ReviewerEmail = request.ReviewerEmail,
                ReviewerName = request.ReviewerName,
                Locale = request.Locale,
            },
            "producer@vantage.test"
        );

        Assert.Equal(5, draft.Version);
    }

    [Fact]
    public async Task GetOutstandingForClientAsync_WithNothingOutstanding_DoesNotReadReviews()
    {
        this.assignments
            .Setup(repository =>
                repository.GetActiveForClientAsync("northwind", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync([]);

        var outstanding = await this.service.GetOutstandingForClientAsync("northwind");

        Assert.Empty(outstanding);
        this.reviews.Verify(
            repository =>
                repository.GetManyAsync(
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    private void GivenReviewExists(CreativeReview review) =>
        this.reviews
            .Setup(repository =>
                repository.GetAsync(review.ReviewId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(review);

    private static SubmitReviewRequest NewSubmitRequest(CreativeReview review) =>
        new()
        {
            ReviewId = review.ReviewId,
            ClientCode = review.ClientCode,
            PortalBaseUrl = "https://reviews.vantage.test",
            SubmittedBy = "producer@vantage.test",
        };

    private static CreateReviewRequest NewCreateRequest() =>
        new()
        {
            CampaignId = Guid.NewGuid(),
            CampaignName = "Spring launch",
            ReviewerEmail = "client@northwind.test",
            ReviewerName = "Client Reviewer",
            Locale = "en-US",
        };

    private static CreativeReview NewReview(ReviewStatus status) =>
        new()
        {
            ReviewId = Guid.NewGuid(),
            CampaignId = Guid.NewGuid(),
            ClientCode = "northwind",
            CampaignName = "Spring launch",
            Version = 1,
            Status = status,
            Locale = "en-US",
            ReviewerEmail = "client@northwind.test",
            ReviewerName = "Client Reviewer",
            CreatedUtc = DateTimeOffset.UtcNow,
            LastModifiedUtc = DateTimeOffset.UtcNow,
            IsActive = true,
        };
}
