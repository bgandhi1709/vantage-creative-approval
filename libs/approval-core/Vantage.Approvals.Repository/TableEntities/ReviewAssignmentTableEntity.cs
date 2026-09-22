using Azure;
using Azure.Data.Tables;

namespace Vantage.Approvals.Core.Storage;

/// <summary>Canonical assignment row, keyed by assignment id.</summary>
internal sealed class ReviewAssignmentTableEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;

    public string RowKey { get; set; } = string.Empty;

    public DateTimeOffset? Timestamp { get; set; }

    public ETag ETag { get; set; }

    public Guid AssignmentId { get; set; }

    public Guid ReviewId { get; set; }

    public Guid CampaignId { get; set; }

    public string ClientCode { get; set; } = string.Empty;

    public int Version { get; set; }

    public DateTimeOffset? SentDate { get; set; }

    public bool IsActive { get; set; }
}

/// <summary>
/// Index row: one partition per campaign, RowKey ordered so the newest version sorts first.
/// Carries copies of Version, SentDate and IsActive so "latest round for these campaigns" is
/// answered entirely from this table.
/// </summary>
internal sealed class AssignmentByCampaignEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;

    public string RowKey { get; set; } = string.Empty;

    public DateTimeOffset? Timestamp { get; set; }

    public ETag ETag { get; set; }

    public Guid AssignmentId { get; set; }

    public Guid ReviewId { get; set; }

    public Guid CampaignId { get; set; }

    public string ClientCode { get; set; } = string.Empty;

    public int Version { get; set; }

    public DateTimeOffset? SentDate { get; set; }

    public bool IsActive { get; set; }
}

/// <summary>Index row: one partition per client code, for the producer console's work list.</summary>
internal sealed class AssignmentByClientEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;

    public string RowKey { get; set; } = string.Empty;

    public DateTimeOffset? Timestamp { get; set; }

    public ETag ETag { get; set; }

    public Guid AssignmentId { get; set; }

    public Guid ReviewId { get; set; }

    public Guid CampaignId { get; set; }

    public string ClientCode { get; set; } = string.Empty;

    public int Version { get; set; }

    public DateTimeOffset? SentDate { get; set; }

    public bool IsActive { get; set; }
}
