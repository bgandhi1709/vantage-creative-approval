using Vantage.Approvals.Core.Models;

namespace Vantage.Approvals.Core.Repositories;

public interface IReviewNoteRepository
{
    Task<IReadOnlyList<ReviewNote>> GetForReviewAsync(
        Guid reviewId,
        bool includeInternal,
        CancellationToken cancellationToken = default
    );

    Task CreateAsync(ReviewNote note, CancellationToken cancellationToken = default);
}
