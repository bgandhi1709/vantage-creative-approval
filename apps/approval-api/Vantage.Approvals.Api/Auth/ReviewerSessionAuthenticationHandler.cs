using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Vantage.Approvals.Api.Configuration;

namespace Vantage.Approvals.Api.Auth;

/// <summary>
/// Authenticates a client reviewer from the signed session cookie.
/// </summary>
/// <remarks>
/// Reviewers arrive from an emailed link and have no account, so there is nothing to sign in to.
/// The link exchanges itself for this cookie once, and every later call carries it. Versioned
/// controllers accept this scheme alongside bearer tokens, which is how one endpoint serves both
/// the client and the producer console.
/// </remarks>
public sealed class ReviewerSessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> schemeOptions,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    ISessionVerifier verifier,
    IOptions<ReviewerSessionOptions> sessionOptions
) : AuthenticationHandler<AuthenticationSchemeOptions>(schemeOptions, loggerFactory, encoder)
{
    public const string SchemeName = "ReviewerSession";

    /// <summary>Claim carrying the review the cookie was issued for.</summary>
    public const string ReviewIdClaimType = "vantage:review-id";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!this.Request.Cookies.TryGetValue(sessionOptions.Value.CookieName, out var cookie))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = verifier.Verify(cookie);

        if (identity is null)
        {
            return Task.FromResult(AuthenticateResult.Fail("The reviewer session is not valid."));
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, identity.Email),
            new(ClaimTypes.Email, identity.Email),
            new(ClaimTypes.Name, identity.Name),
            new(ReviewIdClaimType, identity.ReviewId.ToString("D")),
        };

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return Task.FromResult(
            AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName))
        );
    }
}
