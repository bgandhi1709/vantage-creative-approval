using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using Vantage.Approvals.Core.Models;
using Vantage.Approvals.Core.Platform;
using Vantage.Approvals.Core.Repositories;

namespace Vantage.Approvals.Core.Storage.Repositories;

internal sealed class ReviewTableRepository(TableServiceClient serviceClient) : IReviewRepository
{
    private readonly TableClient table = serviceClient.GetTableClient(TableNames.Reviews);

    public async Task<CreativeReview?> GetAsync(
        Guid reviewId,
        CancellationToken cancellationToken = default
    )
    {
        var client = await this.table.EnsureExistsAsync(cancellationToken).ConfigureAwait(false);
        var key = TableKeys.From(reviewId);

        var response = await client
            .GetEntityIfExistsAsync<ReviewTableEntity>(key, key, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return response.HasValue ? ToModel(response.Value!) : null;
    }

    public async Task<IReadOnlyList<CreativeReview>> GetManyAsync(
        IReadOnlyCollection<Guid> reviewIds,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(reviewIds);

        if (reviewIds.Count == 0)
        {
            return [];
        }

        var client = await this.table.EnsureExistsAsync(cancellationToken).ConfigureAwait(false);

        // Fan-out point reads rather than one filter query: each row lives in its own partition,
        // so a filter would be a table scan while these are N single-partition lookups.
        var reads = reviewIds
            .Distinct()
            .Select(async id =>
            {
                var key = TableKeys.From(id);
                var response = await client
                    .GetEntityIfExistsAsync<ReviewTableEntity>(
                        key,
                        key,
                        cancellationToken: cancellationToken
                    )
                    .ConfigureAwait(false);
                return response.HasValue ? response.Value : null;
            });

        var entities = await Task.WhenAll(reads).ConfigureAwait(false);

        return [.. entities.Where(entity => entity is not null).Select(entity => ToModel(entity!))];
    }

    public async Task CreateAsync(
        CreativeReview review,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(review);

        var client = await this.table.EnsureExistsAsync(cancellationToken).ConfigureAwait(false);
        await client.AddEntityAsync(ToEntity(review), cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(
        CreativeReview review,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(review);

        var client = await this.table.EnsureExistsAsync(cancellationToken).ConfigureAwait(false);
        await client
            .UpsertEntityAsync(ToEntity(review), TableUpdateMode.Replace, cancellationToken)
            .ConfigureAwait(false);
    }

    private static ReviewTableEntity ToEntity(CreativeReview review)
    {
        var key = TableKeys.From(review.ReviewId);

        return new ReviewTableEntity
        {
            PartitionKey = key,
            RowKey = key,
            CampaignId = review.CampaignId,
            ClientCode = review.ClientCode,
            CampaignName = review.CampaignName,
            Version = review.Version,
            Status = review.Status.ToString(),
            Locale = review.Locale,
            ReviewerEmail = review.ReviewerEmail,
            ReviewerName = review.ReviewerName,
            SentDate = review.SentDate?.ToUniversalTime(),
            CreatedUtc = review.CreatedUtc.ToUniversalTime(),
            LastModifiedUtc = review.LastModifiedUtc.ToUniversalTime(),
            LastModifiedBy = review.LastModifiedBy,
            IsActive = review.IsActive,
            AssetsJson = JsonSerializer.Serialize(review.Assets),
        };
    }

    private static CreativeReview ToModel(ReviewTableEntity entity) =>
        new()
        {
            ReviewId = Guid.Parse(entity.RowKey),
            CampaignId = entity.CampaignId,
            ClientCode = entity.ClientCode,
            CampaignName = entity.CampaignName,
            Version = entity.Version,
            Status = Enum.TryParse<ReviewStatus>(entity.Status, out var status)
                ? status
                : ReviewStatus.Draft,
            Locale = entity.Locale,
            ReviewerEmail = entity.ReviewerEmail,
            ReviewerName = entity.ReviewerName,
            SentDate = entity.SentDate,
            CreatedUtc = entity.CreatedUtc,
            LastModifiedUtc = entity.LastModifiedUtc,
            LastModifiedBy = entity.LastModifiedBy,
            IsActive = entity.IsActive,
            Assets =
                JsonSerializer.Deserialize<List<CreativeAsset>>(entity.AssetsJson) ?? [],
        };
}
