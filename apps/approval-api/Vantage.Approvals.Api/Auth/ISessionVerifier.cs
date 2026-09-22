namespace Vantage.Approvals.Api.Auth;

/// <summary>Identity carried by a reviewer's session cookie.</summary>
public sealed class ReviewerIdentity
{
    public required string Email { get; init; }

    public required string Name { get; init; }

    public required Guid ReviewId { get; init; }
}

/// <summary>
/// Verifies a reviewer's session cookie.
/// </summary>
/// <remarks>
/// This is the seam where a real identity provider plugs in. The shipped implementation signs and
/// verifies the cookie locally, so the demo runs with no external account; a provider adapter
/// would implement this interface and nothing above it would change.
/// </remarks>
public interface ISessionVerifier
{
    string Issue(ReviewerIdentity identity);

    ReviewerIdentity? Verify(string cookieValue);
}
