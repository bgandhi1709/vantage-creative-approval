using System.ComponentModel.DataAnnotations;

namespace Vantage.Approvals.Api.Configuration;

/// <summary>
/// Blob storage for the immutable snapshots written on every state change.
/// </summary>
public sealed class SnapshotOptions
{
    public const string SectionName = "Snapshots";

    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    [Required]
    public string ContainerName { get; set; } = "review-snapshots";
}

/// <summary>Outbound review notifications.</summary>
public sealed class NotificationOptions
{
    public const string SectionName = "Notifications";

    [Required]
    public string SmtpHost { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int SmtpPort { get; set; } = 1025;

    [Required]
    [EmailAddress]
    public string FromAddress { get; set; } = string.Empty;

    [Required]
    public string FromName { get; set; } = "Vantage Works";
}

/// <summary>Bearer tokens for the producer console.</summary>
public sealed class ProducerTokenOptions
{
    public const string SectionName = "ProducerToken";

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    [Required]
    [MinLength(32)]
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Only addresses in this domain may hold a producer token.</summary>
    [Required]
    public string AllowedEmailDomain { get; set; } = string.Empty;

    [Range(1, 24 * 60)]
    public int LifetimeMinutes { get; set; } = 60;
}

/// <summary>Signed cookie a client reviewer carries after opening their emailed link.</summary>
public sealed class ReviewerSessionOptions
{
    public const string SectionName = "ReviewerSession";

    [Required]
    [MinLength(32)]
    public string SigningKey { get; set; } = string.Empty;

    [Range(1, 30)]
    public int LifetimeDays { get; set; } = 7;

    [Required]
    public string CookieName { get; set; } = "vw_review";
}

/// <summary>The upstream campaign system this service advances stages in.</summary>
public sealed class CampaignSystemOptions
{
    public const string SectionName = "CampaignSystem";

    [Required]
    [Url]
    public string BaseUrl { get; set; } = string.Empty;

    [Required]
    public string ApiKey { get; set; } = string.Empty;

    [Range(1, 120)]
    public int TimeoutSeconds { get; set; } = 30;
}
