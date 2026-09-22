using Vantage.Approvals.Core.Models;

namespace Vantage.Approvals.Core.Repositories;

public interface IReviewDecisionRepository
{
    Task<ReviewDecision?> GetLatestForReviewAsync(
        Guid reviewId,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<ReviewDecision>> GetHistoryForReviewAsync(
        Guid reviewId,
        CancellationToken cancellationToken = default
    );

    Task CreateAsync(ReviewDecision decision, CancellationToken cancellationToken = default);
}
