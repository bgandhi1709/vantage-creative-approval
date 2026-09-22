using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Vantage.Approvals.Api.Configuration;

namespace Vantage.Approvals.Api.Auth;

public interface IProducerTokenFactory
{
    /// <summary>Mints a producer token, or returns null when the address is outside the allowed domain.</summary>
    string? TryIssue(string email);
}

internal sealed class ProducerTokenFactory(IOptions<ProducerTokenOptions> options)
    : IProducerTokenFactory
{
    public string? TryIssue(string email)
    {
        var settings = options.Value;

        // Domain gate: the console is internal, so the token is only ever minted for staff
        // addresses. A real deployment moves this check into the identity provider; keeping the
        // rule visible here is what makes the seam obvious.
        if (
            string.IsNullOrWhiteSpace(email)
            || !email.EndsWith('@' + settings.AllowedEmailDomain, StringComparison.OrdinalIgnoreCase)
        )
        {
            return null;
        }

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
            SecurityAlgorithms.HmacSha256
        );

        var token = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, email),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(ClaimTypes.Name, email),
            ],
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(settings.LifetimeMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
