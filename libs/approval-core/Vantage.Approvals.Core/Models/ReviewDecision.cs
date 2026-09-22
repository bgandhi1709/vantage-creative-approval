namespace Vantage.Approvals.Core.Models;

public enum DecisionOutcome
{
    Approved = 0,
    ChangesRequested = 1,
}

/// <summary>
/// The client's answer to one review round. Stored separately from the review so a decision is
/// append-only evidence: the review row moves on, the decision that caused it stays as written.
/// </summary>
public sealed class ReviewDecision
{
    public Guid DecisionId { get; set; }

    public Guid ReviewId { get; set; }

    public DecisionOutcome Outcome { get; set; }

    public string DecidedByEmail { get; set; } = string.Empty;

    public DateTimeOffset DecidedUtc { get; set; }

    /// <summary>Free text the client typed when requesting changes; empty on a clean approval.</summary>
    public string Comment { get; set; } = string.Empty;

    /// <summary>Assets the client called out by id. Empty means the decision applies to the whole round.</summary>
    public List<Guid> AssetIds { get; set; } = [];
}
