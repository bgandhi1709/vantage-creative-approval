using System.Globalization;
using Azure.Data.Tables;
using Vantage.Approvals.Core.Models;
using Vantage.Approvals.Core.Platform;
using Vantage.Approvals.Core.Repositories;

namespace Vantage.Approvals.Core.Storage.Repositories;

internal sealed class ReviewDecisionTableRepository(TableServiceClient serviceClient)
    : IReviewDecisionRepository
{
    private readonly TableClient table = serviceClient.GetTableClient(TableNames.Decisions);

    public async Task<ReviewDecision?> GetLatestForReviewAsync(
        Guid reviewId,
        CancellationToken cancellationToken = default
    )
    {
        var history = await this.GetHistoryForReviewAsync(reviewId, cancellationToken)
            .ConfigureAwait(false);
        return history.Count == 0 ? null : history[0];
    }

    public async Task<IReadOnlyList<ReviewDecision>> GetHistoryForReviewAsync(
        Guid reviewId,
        CancellationToken cancellationToken = default
    )
    {
        var client = await this.table.EnsureExistsAsync(cancellationToken).ConfigureAwait(false);
        var partition = TableKeys.From(reviewId);

        var results = new List<ReviewDecision>();

        // Single-partition scan, already ordered newest-first by the descending RowKey.
        var query = client.QueryAsync<ReviewDecisionTableEntity>(
            entity => entity.PartitionKey == partition,
            cancellationToken: cancellationToken
        );

        await foreach (var entity in query.ConfigureAwait(false))
        {
            results.Add(ToModel(reviewId, entity));
        }

        return results;
    }

    public async Task CreateAsync(
        ReviewDecision decision,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(decision);

        var client = await this.table.EnsureExistsAsync(cancellationToken).ConfigureAwait(false);
        await client.AddEntityAsync(ToEntity(decision), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Descending-timestamp RowKey: Table Storage sorts RowKeys ascending, so subtracting the
    /// timestamp from <see cref="DateTime.MaxValue"/> puts the newest decision first and makes
    /// "latest" the first row of the partition rather than a sort over all of it. The decision id
    /// is appended so two decisions in the same tick cannot collide.
    /// </summary>
    private static string BuildRowKey(DateTimeOffset decidedUtc, Guid decisionId)
    {
        var inverted = DateTime.MaxValue.Ticks - decidedUtc.UtcDateTime.Ticks;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{inverted:D19}-{TableKeys.From(decisionId)}"
        );
    }

    private static ReviewDecisionTableEntity ToEntity(ReviewDecision decision) =>
        new()
        {
            PartitionKey = TableKeys.From(decision.ReviewId),
            RowKey = BuildRowKey(decision.DecidedUtc, decision.DecisionId),
            DecisionId = decision.DecisionId,
            Outcome = decision.Outcome.ToString(),
            DecidedByEmail = decision.DecidedByEmail,
            DecidedUtc = decision.DecidedUtc.ToUniversalTime(),
            Comment = decision.Comment,
            AssetIds = string.Join(',', decision.AssetIds.Select(TableKeys.From)),
        };

    private static ReviewDecision ToModel(Guid reviewId, ReviewDecisionTableEntity entity) =>
        new()
        {
            DecisionId = entity.DecisionId,
            ReviewId = reviewId,
            Outcome = Enum.TryParse<DecisionOutcome>(entity.Outcome, out var outcome)
                ? outcome
                : DecisionOutcome.ChangesRequested,
            DecidedByEmail = entity.DecidedByEmail,
            DecidedUtc = entity.DecidedUtc,
            Comment = entity.Comment,
            AssetIds =
            [
                .. entity
                    .AssetIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(Guid.Parse),
            ],
        };
}
