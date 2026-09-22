using Azure;
using Azure.Data.Tables;

namespace Vantage.Approvals.Core.Storage;

/// <summary>
/// Storage shape of a review round. PartitionKey and RowKey are both the review id: rounds are
/// always fetched by id, so a one-row partition gives a point read and no table scan ever happens.
/// Assets are stored as a JSON string because Table Storage has no nested types and the asset list
/// is only ever read with its round.
/// </summary>
internal sealed class ReviewTableEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;

    public string RowKey { get; set; } = string.Empty;

    public DateTimeOffset? Timestamp { get; set; }

    public ETag ETag { get; set; }

    public Guid CampaignId { get; set; }

    public string ClientCode { get; set; } = string.Empty;

    public string CampaignName { get; set; } = string.Empty;

    public int Version { get; set; }

    /// <summary>Persisted by name, not ordinal — see the remarks on the status enum.</summary>
    public string Status { get; set; } = string.Empty;

    public string Locale { get; set; } = string.Empty;

    public string ReviewerEmail { get; set; } = string.Empty;

    public string ReviewerName { get; set; } = string.Empty;

    public DateTimeOffset? SentDate { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset LastModifiedUtc { get; set; }

    public string LastModifiedBy { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public string AssetsJson { get; set; } = "[]";
}
