using Azure;
using Azure.Data.Tables;

namespace Vantage.Approvals.Core.Storage;

/// <summary>
/// Storage shape of a decision. Partitioned by review id so a round's decision history is one
/// partition scan; the RowKey is a descending timestamp prefix, which makes "latest" the first row
/// returned instead of a client-side sort over the partition.
/// </summary>
internal sealed class ReviewDecisionTableEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;

    public string RowKey { get; set; } = string.Empty;

    public DateTimeOffset? Timestamp { get; set; }

    public ETag ETag { get; set; }

    public Guid DecisionId { get; set; }

    public string Outcome { get; set; } = string.Empty;

    public string DecidedByEmail { get; set; } = string.Empty;

    public DateTimeOffset DecidedUtc { get; set; }

    public string Comment { get; set; } = string.Empty;

    /// <summary>Comma-separated asset ids; empty when the decision covers the whole round.</summary>
    public string AssetIds { get; set; } = string.Empty;
}
