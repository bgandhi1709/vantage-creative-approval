using Vantage.Approvals.Core.Models;
using Vantage.Approvals.Core.Storage.Repositories;

namespace Vantage.Approvals.Api.Tests.TableStorage;

[Collection(AzuriteTableCollection.Name)]
public sealed class ReviewDecisionTableRepositoryTests(AzuriteTableFixture fixture)
{
    private ReviewDecisionTableRepository CreateRepository() => new(fixture.CreateClient());

    [Fact]
    public async Task GetLatestForReviewAsync_ReturnsTheMostRecentDecision()
    {
        // The RowKey is an inverted timestamp, so "latest" must come back first without the
        // caller sorting anything. Written out of order on purpose.
        var repository = this.CreateRepository();
        var reviewId = Guid.NewGuid();

        await repository.CreateAsync(
            NewDecision(reviewId, DecisionOutcome.ChangesRequested, DateTimeOffset.UtcNow.AddHours(-2))
        );
        var newest = NewDecision(
            reviewId,
            DecisionOutcome.Approved,
            DateTimeOffset.UtcNow.AddMinutes(-1)
        );
        await repository.CreateAsync(newest);
        await repository.CreateAsync(
            NewDecision(reviewId, DecisionOutcome.ChangesRequested, DateTimeOffset.UtcNow.AddHours(-5))
        );

        var latest = await repository.GetLatestForReviewAsync(reviewId);

        Assert.NotNull(latest);
        Assert.Equal(newest.DecisionId, latest.DecisionId);
        Assert.Equal(DecisionOutcome.Approved, latest.Outcome);
    }

    [Fact]
    public async Task GetHistoryForReviewAsync_ReturnsEveryDecisionNewestFirst()
    {
        var repository = this.CreateRepository();
        var reviewId = Guid.NewGuid();
        var older = NewDecision(
            reviewId,
            DecisionOutcome.ChangesRequested,
            DateTimeOffset.UtcNow.AddHours(-3)
        );
        var newer = NewDecision(reviewId, DecisionOutcome.Approved, DateTimeOffset.UtcNow);

        await repository.CreateAsync(older);
        await repository.CreateAsync(newer);

        var history = await repository.GetHistoryForReviewAsync(reviewId);

        Assert.Equal(2, history.Count);
        Assert.Equal(newer.DecisionId, history[0].DecisionId);
        Assert.Equal(older.DecisionId, history[1].DecisionId);
    }

    [Fact]
    public async Task GetHistoryForReviewAsync_IsScopedToOneReview()
    {
        var repository = this.CreateRepository();
        var mine = Guid.NewGuid();
        var other = Guid.NewGuid();

        await repository.CreateAsync(NewDecision(mine, DecisionOutcome.Approved, DateTimeOffset.UtcNow));
        await repository.CreateAsync(
            NewDecision(other, DecisionOutcome.Approved, DateTimeOffset.UtcNow)
        );

        var history = await repository.GetHistoryForReviewAsync(mine);

        Assert.Single(history);
        Assert.Equal(mine, history[0].ReviewId);
    }

    [Fact]
    public async Task CreateAsync_RoundTripsTheCalledOutAssetIds()
    {
        var repository = this.CreateRepository();
        var reviewId = Guid.NewGuid();
        var decision = NewDecision(
            reviewId,
            DecisionOutcome.ChangesRequested,
            DateTimeOffset.UtcNow
        );
        decision.AssetIds = [Guid.NewGuid(), Guid.NewGuid()];
        decision.Comment = "Second frame needs the legal line.";

        await repository.CreateAsync(decision);
        var stored = await repository.GetLatestForReviewAsync(reviewId);

        Assert.NotNull(stored);
        Assert.Equal(decision.AssetIds, stored.AssetIds);
        Assert.Equal(decision.Comment, stored.Comment);
    }

    [Fact]
    public async Task GetLatestForReviewAsync_WhenNothingWasDecided_ReturnsNull()
    {
        var repository = this.CreateRepository();

        var latest = await repository.GetLatestForReviewAsync(Guid.NewGuid());

        Assert.Null(latest);
    }

    private static ReviewDecision NewDecision(
        Guid reviewId,
        DecisionOutcome outcome,
        DateTimeOffset decidedUtc
    ) =>
        new()
        {
            DecisionId = Guid.NewGuid(),
            ReviewId = reviewId,
            Outcome = outcome,
            DecidedByEmail = "client@northwind.test",
            DecidedUtc = decidedUtc,
        };
}
