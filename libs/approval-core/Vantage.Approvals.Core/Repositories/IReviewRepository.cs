using Vantage.Approvals.Core.Models;

namespace Vantage.Approvals.Core.Repositories;

/// <summary>
/// Reads and writes review rounds. Every method is named for the question it answers, so the
/// storage layer can satisfy each one with a point read or a partition scan and never has to
/// expose a query surface the caller could misuse.
/// </summary>
public interface IReviewRepository
{
    Task<CreativeReview?> GetAsync(Guid reviewId, CancellationToken cancellationToken = default);

    /// <summary>Fan-out point read. Ids that do not exist are skipped, not faulted.</summary>
    Task<IReadOnlyList<CreativeReview>> GetManyAsync(
        IReadOnlyCollection<Guid> reviewIds,
        CancellationToken cancellationToken = default
    );

    Task CreateAsync(CreativeReview review, CancellationToken cancellationToken = default);

    Task UpdateAsync(CreativeReview review, CancellationToken cancellationToken = default);
}
