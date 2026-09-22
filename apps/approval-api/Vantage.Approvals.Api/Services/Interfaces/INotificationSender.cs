namespace Vantage.Approvals.Api.Services.Interfaces;

/// <summary>The message a reviewer receives when a round is sent to them.</summary>
public sealed class ReviewNotification
{
    public required string ToAddress { get; init; }

    public required string ToName { get; init; }

    public required string Subject { get; init; }

    public required string Body { get; init; }

    /// <summary>Absolute URL of the review page, derived from the incoming request.</summary>
    public required string ReviewUrl { get; init; }
}

public interface INotificationSender
{
    Task SendAsync(ReviewNotification notification, CancellationToken cancellationToken = default);
}
