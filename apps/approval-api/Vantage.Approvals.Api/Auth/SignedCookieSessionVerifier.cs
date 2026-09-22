using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Vantage.Approvals.Api.Configuration;

namespace Vantage.Approvals.Api.Auth;

/// <summary>
/// HMAC-signed session cookie. Payload and signature are both base64url, joined by a dot.
/// </summary>
internal sealed class SignedCookieSessionVerifier(IOptions<ReviewerSessionOptions> options)
    : ISessionVerifier
{
    private sealed class Payload
    {
        public string Email { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public Guid ReviewId { get; set; }

        public long ExpiresUnix { get; set; }
    }

    public string Issue(ReviewerIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);

        var payload = new Payload
        {
            Email = identity.Email,
            Name = identity.Name,
            ReviewId = identity.ReviewId,
            ExpiresUnix = DateTimeOffset
                .UtcNow.AddDays(options.Value.LifetimeDays)
                .ToUnixTimeSeconds(),
        };

        var body = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload));
        return string.Create(CultureInfo.InvariantCulture, $"{body}.{this.Sign(body)}");
    }

    public ReviewerIdentity? Verify(string cookieValue)
    {
        if (string.IsNullOrWhiteSpace(cookieValue))
        {
            return null;
        }

        var parts = cookieValue.Split('.');

        if (parts.Length != 2)
        {
            return null;
        }

        // Fixed-time comparison: a byte-by-byte string equality on a signature leaks where the
        // first mismatch is, which is enough to forge one given enough attempts.
        var expected = Encoding.UTF8.GetBytes(this.Sign(parts[0]));
        var actual = Encoding.UTF8.GetBytes(parts[1]);

        if (!CryptographicOperations.FixedTimeEquals(expected, actual))
        {
            return null;
        }

        Payload? payload;

        try
        {
            payload = JsonSerializer.Deserialize<Payload>(Base64UrlDecode(parts[0]));
        }
        catch (JsonException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }

        if (payload is null || payload.ExpiresUnix < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        {
            return null;
        }

        return new ReviewerIdentity
        {
            Email = payload.Email,
            Name = payload.Name,
            ReviewId = payload.ReviewId,
        };
    }

    private string Sign(string body)
    {
        var key = Encoding.UTF8.GetBytes(options.Value.SigningKey);
        var signature = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(body));
        return Base64UrlEncode(signature);
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - (padded.Length % 4)) % 4);
        return Convert.FromBase64String(padded);
    }
}
