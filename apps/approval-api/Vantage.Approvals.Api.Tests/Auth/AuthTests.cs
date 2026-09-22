using Microsoft.Extensions.Options;
using Vantage.Approvals.Api.Auth;
using Vantage.Approvals.Api.Configuration;

namespace Vantage.Approvals.Api.Tests.Auth;

public sealed class SignedCookieSessionVerifierTests
{
    private readonly SignedCookieSessionVerifier verifier = new(
        Options.Create(
            new ReviewerSessionOptions
            {
                SigningKey = "a-local-test-signing-key-long-enough",
                LifetimeDays = 7,
                CookieName = "vw_review",
            }
        )
    );

    [Fact]
    public void Verify_RoundTripsTheIdentityItIssued()
    {
        var identity = NewIdentity();

        var restored = this.verifier.Verify(this.verifier.Issue(identity));

        Assert.NotNull(restored);
        Assert.Equal(identity.Email, restored.Email);
        Assert.Equal(identity.ReviewId, restored.ReviewId);
    }

    [Fact]
    public void Verify_WhenThePayloadIsEditedAfterSigning_Rejects()
    {
        var cookie = this.verifier.Issue(NewIdentity());
        var tampered = 'x' + cookie[1..];

        Assert.Null(this.verifier.Verify(tampered));
    }

    [Fact]
    public void Verify_WhenTheSignatureIsFromADifferentKey_Rejects()
    {
        var otherVerifier = new SignedCookieSessionVerifier(
            Options.Create(
                new ReviewerSessionOptions
                {
                    SigningKey = "a-different-key-that-is-long-enough!!",
                    LifetimeDays = 7,
                    CookieName = "vw_review",
                }
            )
        );

        var cookie = otherVerifier.Issue(NewIdentity());

        Assert.Null(this.verifier.Verify(cookie));
    }

    [Fact]
    public void Verify_WhenTheSessionHasExpired_Rejects()
    {
        var expiring = new SignedCookieSessionVerifier(
            Options.Create(
                new ReviewerSessionOptions
                {
                    SigningKey = "a-local-test-signing-key-long-enough",
                    // The options validator forbids zero, but a cookie minted before a lifetime
                    // change must still expire on its own stored timestamp.
                    LifetimeDays = 1,
                    CookieName = "vw_review",
                }
            )
        );

        var cookie = expiring.Issue(NewIdentity());

        Assert.NotNull(expiring.Verify(cookie));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-cookie")]
    [InlineData("too.many.parts.here")]
    public void Verify_WithMalformedInput_ReturnsNullRatherThanThrowing(string cookie) =>
        Assert.Null(this.verifier.Verify(cookie));

    private static ReviewerIdentity NewIdentity() =>
        new()
        {
            Email = "client@northwind.test",
            Name = "Client Reviewer",
            ReviewId = Guid.NewGuid(),
        };
}

public sealed class ProducerTokenFactoryTests
{
    private readonly ProducerTokenFactory factory = new(
        Options.Create(
            new ProducerTokenOptions
            {
                Issuer = "vantage-approvals-api",
                Audience = "vantage-producer-console",
                SigningKey = "a-local-test-producer-key-long-enough",
                AllowedEmailDomain = "vantage.test",
                LifetimeMinutes = 60,
            }
        )
    );

    [Fact]
    public void TryIssue_ForAnAddressInTheAllowedDomain_MintsAToken()
    {
        var token = this.factory.TryIssue("producer@vantage.test");

        Assert.NotNull(token);
        Assert.Equal(3, token.Split('.').Length);
    }

    [Fact]
    public void TryIssue_IgnoresTheCaseOfTheDomain()
    {
        Assert.NotNull(this.factory.TryIssue("Producer@VANTAGE.test"));
    }

    [Theory]
    [InlineData("someone@example.com")]
    [InlineData("someone@notvantage.test")]
    [InlineData("")]
    public void TryIssue_ForAnAddressOutsideTheAllowedDomain_ReturnsNull(string email) =>
        Assert.Null(this.factory.TryIssue(email));

    [Fact]
    public void TryIssue_DoesNotMatchADomainThatMerelyEndsWithTheAllowedOne()
    {
        // "evil-vantage.test" ends with "vantage.test" as a substring; the '@' in the comparison
        // is what stops it being accepted.
        Assert.Null(this.factory.TryIssue("someone@evil-vantage.test"));
    }
}
