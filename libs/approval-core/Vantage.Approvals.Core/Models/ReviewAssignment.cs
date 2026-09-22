namespace Vantage.Approvals.Core.Models;

/// <summary>
/// Links a review round to the campaign and client it belongs to.
/// This is the aggregate the two denormalized index tables are built from: "latest round for a
/// campaign" and "everything outstanding for a client" are both partition scans of an index,
/// never a scan of the review table.
/// </summary>
public sealed class ReviewAssignment
{
    public Guid AssignmentId { get; set; }

    public Guid ReviewId { get; set; }

    public Guid CampaignId { get; set; }

    public string ClientCode { get; set; } = string.Empty;

    public int Version { get; set; }

    public DateTimeOffset? SentDate { get; set; }

    public bool IsActive { get; set; } = true;
}
