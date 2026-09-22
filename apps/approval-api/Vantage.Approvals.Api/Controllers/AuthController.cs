using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Vantage.Approvals.Api.Auth;
using Vantage.Approvals.Api.Configuration;
using Vantage.Approvals.Api.Exceptions;
using Vantage.Approvals.Api.Services.Interfaces;

namespace Vantage.Approvals.Api.Controllers;

public sealed class ExchangeLinkBody
{
    [Required]
    public Guid ReviewId { get; set; }
}

public sealed class ProducerSignInBody
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}

/// <summary>
/// Issues the two credentials this service accepts.
/// </summary>
/// <remarks>
/// Both flows are local by design so the stack runs with no identity vendor. A real deployment
/// replaces <see cref="ISessionVerifier"/> and this controller's producer endpoint with an
/// identity provider; the authentication handlers and every controller above them stay as they are.
/// </remarks>
[ApiController]
[Route("auth")]
[AllowAnonymous]
public sealed class AuthController(
    ISessionVerifier verifier,
    IProducerTokenFactory tokens,
    ICreativeReviewService reviews,
    IOptions<ReviewerSessionOptions> sessionOptions
) : ControllerBase
{
    /// <summary>
    /// Exchanges an emailed review link for the reviewer's session cookie. The cookie is scoped to
    /// the one round it was issued for.
    /// </summary>
    [HttpPost("reviewer-session")]
    public async Task<IActionResult> ExchangeLink(
        [FromBody] ExchangeLinkBody body,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(body);

        var review =
            await reviews.GetAsync(body.ReviewId, cancellationToken).ConfigureAwait(false)
            ?? throw new ReviewNotFoundException(body.ReviewId);

        var cookie = verifier.Issue(
            new ReviewerIdentity
            {
                Email = review.ReviewerEmail,
                Name = review.ReviewerName,
                ReviewId = review.ReviewId,
            }
        );

        this.Response.Cookies.Append(
            sessionOptions.Value.CookieName,
            cookie,
            new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = this.Request.IsHttps,
                Expires = DateTimeOffset.UtcNow.AddDays(sessionOptions.Value.LifetimeDays),
                Path = "/",
            }
        );

        return this.NoContent();
    }

    [HttpPost("reviewer-session/logout")]
    public IActionResult Logout()
    {
        this.Response.Cookies.Delete(sessionOptions.Value.CookieName);
        return this.NoContent();
    }

    /// <summary>Mints a producer token for an address inside the allowed domain.</summary>
    [HttpPost("producer-token")]
    public ActionResult<object> IssueProducerToken([FromBody] ProducerSignInBody body)
    {
        ArgumentNullException.ThrowIfNull(body);

        var token = tokens.TryIssue(body.Email);

        return token is null
            ? this.Forbid()
            : this.Ok(new { accessToken = token, tokenType = "Bearer" });
    }
}
