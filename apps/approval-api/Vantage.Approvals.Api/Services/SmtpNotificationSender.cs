using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;
using Vantage.Approvals.Api.Configuration;
using Vantage.Approvals.Api.Logging;
using Vantage.Approvals.Api.Services.Interfaces;

namespace Vantage.Approvals.Api.Services;

/// <summary>
/// Plain SMTP. The local stack points this at a mail catcher, so the demo shows a real message
/// without a delivery vendor; swapping in a provider SDK is a change to this class only.
/// </summary>
internal sealed class SmtpNotificationSender(
    IOptions<NotificationOptions> options,
    ILogger<SmtpNotificationSender> logger
) : INotificationSender
{
    public async Task SendAsync(
        ReviewNotification notification,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(notification);

        var settings = options.Value;

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
        message.To.Add(new MailboxAddress(notification.ToName, notification.ToAddress));
        message.Subject = notification.Subject;
        message.Body = new TextPart("plain")
        {
            Text = $"{notification.Body}{Environment.NewLine}{Environment.NewLine}{notification.ReviewUrl}",
        };

        using var client = new SmtpClient();
        await client
            .ConnectAsync(
                settings.SmtpHost,
                settings.SmtpPort,
                MailKit.Security.SecureSocketOptions.Auto,
                cancellationToken
            )
            .ConfigureAwait(false);
        await client.SendAsync(message, cancellationToken).ConfigureAwait(false);
        await client.DisconnectAsync(quit: true, cancellationToken).ConfigureAwait(false);

        Log.NotificationSent(logger, notification.ToAddress, notification.ReviewUrl);
    }
}
