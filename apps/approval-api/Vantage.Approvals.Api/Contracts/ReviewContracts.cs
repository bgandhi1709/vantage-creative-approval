using System.ComponentModel.DataAnnotations;
using Vantage.Approvals.Core.Models;

namespace Vantage.Approvals.Api.Contracts;

public sealed class CreateReviewBody
{
    [Required]
    public Guid CampaignId { get; set; }

    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string CampaignName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string ReviewerEmail { get; set; } = string.Empty;

    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string ReviewerName { get; set; } = string.Empty;

    /// <summary>BCP-47 tag. The review page renders in this locale regardless of the browser.</summary>
    [Required]
    [RegularExpression("^[a-z]{2}-[A-Z]{2}$")]
    public string Locale { get; set; } = "en-US";

    public List<CreativeAssetBody> Assets { get; set; } = [];
}

public sealed class CreativeAssetBody
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(100)]
    public string Format { get; set; } = string.Empty;

    [StringLength(500)]
    public string PreviewPath { get; set; } = string.Empty;
}

public sealed class DecisionBody
{
    [Required]
    public DecisionOutcome Outcome { get; set; }

    [StringLength(2000)]
    public string Comment { get; set; } = string.Empty;

    public List<Guid> AssetIds { get; set; } = [];
}

public sealed class NoteBody
{
    [Required]
    [StringLength(2000, MinimumLength = 1)]
    public string Body { get; set; } = string.Empty;

    public bool IsInternal { get; set; }
}

public sealed class ReviewResponse
{
    public Guid ReviewId { get; set; }

    public Guid CampaignId { get; set; }

    public string ClientCode { get; set; } = string.Empty;

    public string CampaignName { get; set; } = string.Empty;

    public int Version { get; set; }

    public string Status { get; set; } = string.Empty;

    public string Locale { get; set; } = string.Empty;

    public string ReviewerName { get; set; } = string.Empty;

    public DateTimeOffset? SentDate { get; set; }

    public DateTimeOffset LastModifiedUtc { get; set; }

    public List<CreativeAsset> Assets { get; set; } = [];

    public static ReviewResponse From(CreativeReview review)
    {
        ArgumentNullException.ThrowIfNull(review);

        return new ReviewResponse
        {
            ReviewId = review.ReviewId,
            CampaignId = review.CampaignId,
            ClientCode = review.ClientCode,
            CampaignName = review.CampaignName,
            Version = review.Version,
            Status = review.Status.ToString(),
            Locale = review.Locale,
            ReviewerName = review.ReviewerName,
            SentDate = review.SentDate,
            LastModifiedUtc = review.LastModifiedUtc,
            Assets = review.Assets,
        };
    }
}
