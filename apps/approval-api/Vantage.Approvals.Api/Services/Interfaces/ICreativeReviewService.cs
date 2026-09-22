using Vantage.Approvals.Core.Models;

namespace Vantage.Approvals.Api.Services.Interfaces;

public sealed class CreateReviewRequest
{
    public required Guid CampaignId { get; init; }

    public required string CampaignName { get; init; }

    public required string ReviewerEmail { get; init; }

    public required string ReviewerName { get; init; }

    public required string Locale { get; init; }

    public IReadOnlyList<CreativeAsset> Assets { get; init; } = [];
}

public sealed class SubmitReviewRequest
{
    public required Guid ReviewId { get; init; }

    public required string ClientCode { get; init; }

    /// <summary>Absolute base URL of the portal, derived from the incoming request.</summary>
    public required string PortalBaseUrl { get; init; }

    public required string SubmittedBy { get; init; }
}

public sealed class RecordDecisionRequest
{
    public required Guid ReviewId { get; init; }

    public required string ClientCode { get; init; }

    public required DecisionOutcome Outcome { get; init; }

    public required string DecidedByEmail { get; init; }

    public string Comment { get; init; } = string.Empty;

    public IReadOnlyList<Guid> AssetIds { get; init; } = [];
}

public interface ICreativeReviewService
{
    Task<CreativeReview> CreateDraftAsync(
        string clientCode,
        CreateReviewRequest request,
        string createdBy,
        CancellationToken cancellationToken = default
    );

    Task<CreativeReview> SubmitAsync(
        SubmitReviewRequest request,
        CancellationToken cancellationToken = default
    );

    Task<CreativeReview> RecordDecisionAsync(
        RecordDecisionRequest request,
        CancellationToken cancellationToken = default
    );

    Task<CreativeReview?> GetAsync(Guid reviewId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CreativeReview>> GetOutstandingForClientAsync(
        string clientCode,
        CancellationToken cancellationToken = default
    );
}
