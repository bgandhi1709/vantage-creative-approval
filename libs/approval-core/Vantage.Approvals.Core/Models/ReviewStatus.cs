namespace Vantage.Approvals.Core.Models;

/// <summary>
/// Lifecycle of one creative review round.
/// </summary>
/// <remarks>
/// Values are appended, never reordered: the status is persisted by name in Table Storage and by
/// name in the blob snapshots, and old snapshots outlive any renumbering we might be tempted to do.
/// </remarks>
public enum ReviewStatus
{
    Draft = 0,
    AwaitingDecision = 1,
    ChangesRequested = 2,
    Approved = 3,
    Cancelled = 4,
}
