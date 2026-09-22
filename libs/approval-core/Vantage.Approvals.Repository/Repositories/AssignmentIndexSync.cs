using System.Globalization;
using Azure.Data.Tables;
using Vantage.Approvals.Core.Models;
using Vantage.Approvals.Core.Platform;

namespace Vantage.Approvals.Core.Storage.Repositories;

/// <summary>
/// Keeps the two denormalized assignment indexes in step with the canonical row.
/// </summary>
/// <remarks>
/// Table Storage has no server-side joins and no secondary indexes, so the queries the product
/// actually asks — "latest round for these campaigns", "what is outstanding for this client" —
/// are served by writing the answer at write time into a partition shaped like the question.
/// The cost is this class: every write to an assignment must pass through it, or an index will
/// quietly disagree with the row it describes. That is the trade, and it is why the write path
/// is the only place indexes are touched.
/// </remarks>
internal sealed class AssignmentIndexSync(TableServiceClient serviceClient)
{
    private readonly TableClient byCampaign = serviceClient.GetTableClient(
        TableNames.AssignmentsByCampaign
    );
    private readonly TableClient byClient = serviceClient.GetTableClient(
        TableNames.AssignmentsByClient
    );

    internal async Task SyncAsync(ReviewAssignment assignment, CancellationToken cancellationToken)
    {
        var campaignTable = await this
            .byCampaign.EnsureExistsAsync(cancellationToken)
            .ConfigureAwait(false);
        var clientTable = await this
            .byClient.EnsureExistsAsync(cancellationToken)
            .ConfigureAwait(false);

        var campaignRow = new AssignmentByCampaignEntity
        {
            PartitionKey = TableKeys.From(assignment.CampaignId),
            // Descending version: the newest round is the first row of the partition.
            RowKey = string.Create(CultureInfo.InvariantCulture, $"{int.MaxValue - assignment.Version:D10}"),
            AssignmentId = assignment.AssignmentId,
            ReviewId = assignment.ReviewId,
            CampaignId = assignment.CampaignId,
            ClientCode = assignment.ClientCode,
            Version = assignment.Version,
            SentDate = assignment.SentDate?.ToUniversalTime(),
            IsActive = assignment.IsActive,
        };

        var clientRow = new AssignmentByClientEntity
        {
            PartitionKey = TableKeys.FromCode(assignment.ClientCode),
            RowKey = TableKeys.From(assignment.AssignmentId),
            AssignmentId = assignment.AssignmentId,
            ReviewId = assignment.ReviewId,
            CampaignId = assignment.CampaignId,
            ClientCode = assignment.ClientCode,
            Version = assignment.Version,
            SentDate = assignment.SentDate?.ToUniversalTime(),
            IsActive = assignment.IsActive,
        };

        await Task.WhenAll(
                campaignTable.UpsertEntityAsync(
                    campaignRow,
                    TableUpdateMode.Replace,
                    cancellationToken
                ),
                clientTable.UpsertEntityAsync(clientRow, TableUpdateMode.Replace, cancellationToken)
            )
            .ConfigureAwait(false);
    }
}
