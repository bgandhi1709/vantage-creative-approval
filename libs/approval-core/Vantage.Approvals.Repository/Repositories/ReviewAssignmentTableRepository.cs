using Azure.Data.Tables;
using Vantage.Approvals.Core.Models;
using Vantage.Approvals.Core.Platform;
using Vantage.Approvals.Core.Repositories;

namespace Vantage.Approvals.Core.Storage.Repositories;

internal sealed class ReviewAssignmentTableRepository(
    TableServiceClient serviceClient,
    AssignmentIndexSync indexSync
) : IReviewAssignmentRepository
{
    private readonly TableClient table = serviceClient.GetTableClient(TableNames.Assignments);
    private readonly TableClient byCampaign = serviceClient.GetTableClient(
        TableNames.AssignmentsByCampaign
    );
    private readonly TableClient byClient = serviceClient.GetTableClient(
        TableNames.AssignmentsByClient
    );

    public async Task<ReviewAssignment?> GetAsync(
        Guid assignmentId,
        CancellationToken cancellationToken = default
    )
    {
        var client = await this.table.EnsureExistsAsync(cancellationToken).ConfigureAwait(false);
        var key = TableKeys.From(assignmentId);

        var response = await client
            .GetEntityIfExistsAsync<ReviewAssignmentTableEntity>(
                key,
                key,
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);

        return response.HasValue ? ToModel(response.Value!) : null;
    }

    public async Task<IReadOnlyDictionary<Guid, ReviewAssignment>> GetLatestByCampaignAsync(
        IReadOnlyCollection<Guid> campaignIds,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(campaignIds);

        if (campaignIds.Count == 0)
        {
            return new Dictionary<Guid, ReviewAssignment>();
        }

        var client = await this
            .byCampaign.EnsureExistsAsync(cancellationToken)
            .ConfigureAwait(false);

        // One single-partition query per campaign, each taking only the first row: the index is
        // written newest-first, so "latest" costs one row read regardless of how many rounds exist.
        var lookups = campaignIds
            .Distinct()
            .Select(async campaignId =>
            {
                var partition = TableKeys.From(campaignId);
                var query = client.QueryAsync<AssignmentByCampaignEntity>(
                    entity => entity.PartitionKey == partition,
                    maxPerPage: 1,
                    cancellationToken: cancellationToken
                );

                await foreach (var entity in query.ConfigureAwait(false))
                {
                    return entity;
                }

                return null;
            });

        var rows = await Task.WhenAll(lookups).ConfigureAwait(false);

        return rows.Where(row => row is not null)
            .Select(row => row!)
            .ToDictionary(row => row.CampaignId, ToModel);
    }

    public async Task<IReadOnlyList<ReviewAssignment>> GetActiveForClientAsync(
        string clientCode,
        CancellationToken cancellationToken = default
    )
    {
        var client = await this.byClient.EnsureExistsAsync(cancellationToken).ConfigureAwait(false);
        var partition = TableKeys.FromCode(clientCode);

        var assignments = new List<ReviewAssignment>();

        var query = client.QueryAsync<AssignmentByClientEntity>(
            entity => entity.PartitionKey == partition && entity.IsActive,
            cancellationToken: cancellationToken
        );

        await foreach (var entity in query.ConfigureAwait(false))
        {
            assignments.Add(ToModel(entity));
        }

        return assignments;
    }

    public async Task UpsertAsync(
        ReviewAssignment assignment,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(assignment);

        var client = await this.table.EnsureExistsAsync(cancellationToken).ConfigureAwait(false);
        var key = TableKeys.From(assignment.AssignmentId);

        var entity = new ReviewAssignmentTableEntity
        {
            PartitionKey = key,
            RowKey = key,
            AssignmentId = assignment.AssignmentId,
            ReviewId = assignment.ReviewId,
            CampaignId = assignment.CampaignId,
            ClientCode = assignment.ClientCode,
            Version = assignment.Version,
            SentDate = assignment.SentDate?.ToUniversalTime(),
            IsActive = assignment.IsActive,
        };

        // Row first, then indexes. A failure between the two leaves an index stale rather than
        // pointing at a row that does not exist, and the next write repairs it.
        await client
            .UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken)
            .ConfigureAwait(false);
        await indexSync.SyncAsync(assignment, cancellationToken).ConfigureAwait(false);
    }

    private static ReviewAssignment ToModel(ReviewAssignmentTableEntity entity) =>
        new()
        {
            AssignmentId = entity.AssignmentId,
            ReviewId = entity.ReviewId,
            CampaignId = entity.CampaignId,
            ClientCode = entity.ClientCode,
            Version = entity.Version,
            SentDate = entity.SentDate,
            IsActive = entity.IsActive,
        };

    private static ReviewAssignment ToModel(AssignmentByCampaignEntity entity) =>
        new()
        {
            AssignmentId = entity.AssignmentId,
            ReviewId = entity.ReviewId,
            CampaignId = entity.CampaignId,
            ClientCode = entity.ClientCode,
            Version = entity.Version,
            SentDate = entity.SentDate,
            IsActive = entity.IsActive,
        };

    private static ReviewAssignment ToModel(AssignmentByClientEntity entity) =>
        new()
        {
            AssignmentId = entity.AssignmentId,
            ReviewId = entity.ReviewId,
            CampaignId = entity.CampaignId,
            ClientCode = entity.ClientCode,
            Version = entity.Version,
            SentDate = entity.SentDate,
            IsActive = entity.IsActive,
        };
}
