namespace Vantage.Approvals.Core.Models;

/// <summary>
/// A single round of client sign-off on one campaign's creative.
/// A new round is a new <see cref="CreativeReview"/> with an incremented <see cref="Version"/>;
/// rounds are never edited in place once sent.
/// </summary>
public sealed class CreativeReview
{
    public Guid ReviewId { get; set; }

    public Guid CampaignId { get; set; }

    /// <summary>Short client identifier that appears in every portal route.</summary>
    public string ClientCode { get; set; } = string.Empty;

    public string CampaignName { get; set; } = string.Empty;

    public int Version { get; set; }

    public ReviewStatus Status { get; set; } = ReviewStatus.Draft;

    /// <summary>Locale the review page renders in, resolved from the campaign, not the browser.</summary>
    public string Locale { get; set; } = "en-US";

    public string ReviewerEmail { get; set; } = string.Empty;

    public string ReviewerName { get; set; } = string.Empty;

    /// <summary>Set when the review notification is sent; null while the round is still a draft.</summary>
    public DateTimeOffset? SentDate { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset LastModifiedUtc { get; set; }

    public string LastModifiedBy { get; set; } = string.Empty;

    /// <summary>False once a newer round supersedes this one.</summary>
    public bool IsActive { get; set; } = true;

    public List<CreativeAsset> Assets { get; set; } = [];
}
