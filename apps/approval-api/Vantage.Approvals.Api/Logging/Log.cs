using Vantage.Approvals.Api.Clients;

namespace Vantage.Approvals.Api.Logging;

/// <summary>
/// Source-generated log messages. Every log call in the service goes through here, so message
/// templates are declared once, allocate nothing when the level is disabled, and the whole set of
/// events this service can emit can be read in one file.
/// </summary>
internal static partial class Log
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Created draft round {Version} for campaign {CampaignId} as review {ReviewId}"
    )]
    internal static partial void DraftCreated(
        ILogger logger,
        int version,
        Guid campaignId,
        Guid reviewId
    );

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Sent review notification to {Recipient} for {ReviewUrl}"
    )]
    internal static partial void NotificationSent(ILogger logger, string recipient, string reviewUrl);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Information,
        Message = "Campaign {CampaignId} is already at {Stage}; nothing to advance"
    )]
    internal static partial void StageAlreadyOpen(
        ILogger logger,
        Guid campaignId,
        CampaignStage stage
    );

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Warning,
        Message = "Campaign {CampaignId} is not at {Predecessor}; skipping the advance to {Stage}"
    )]
    internal static partial void PredecessorStageMissing(
        ILogger logger,
        Guid campaignId,
        CampaignStage predecessor,
        CampaignStage stage
    );

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Information,
        Message = "Campaign {CampaignId} advanced to {Stage}"
    )]
    internal static partial void StageAdvanced(ILogger logger, Guid campaignId, CampaignStage stage);

    [LoggerMessage(
        EventId = 1100,
        Level = LogLevel.Warning,
        Message = "Review {ReviewId} was requested and does not exist"
    )]
    internal static partial void ReviewNotFound(ILogger logger, Guid reviewId);

    [LoggerMessage(
        EventId = 1101,
        Level = LogLevel.Warning,
        Message = "Review {ReviewId} rejected a transition: {Reason}"
    )]
    internal static partial void ReviewStateRejected(ILogger logger, Guid reviewId, string reason);

    [LoggerMessage(
        EventId = 1102,
        Level = LogLevel.Error,
        Message = "The campaign system failed for {CampaignId} with status {StatusCode}"
    )]
    internal static partial void CampaignSystemFailed(
        ILogger logger,
        Exception exception,
        Guid campaignId,
        int statusCode
    );
}
