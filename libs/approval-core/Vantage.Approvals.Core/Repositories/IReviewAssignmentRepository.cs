using Vantage.Approvals.Core.Models;

namespace Vantage.Approvals.Core.Repositories;

/// <summary>
/// Owns the assignment aggregate and the two denormalized indexes derived from it.
/// Writers call <see cref="UpsertAsync"/>; the implementation is responsible for keeping both
/// indexes consistent with the row, because nothing above this interface knows they exist.
/// </summary>
public interface IReviewAssignmentRepository
{
    Task<ReviewAssignment?> GetAsync(
        Guid assignmentId,
        CancellationToken cancellationToken = default
    );

    /// <summary>Latest active round per campaign, answered from the by-campaign index.</summary>
    Task<IReadOnlyDictionary<Guid, ReviewAssignment>> GetLatestByCampaignAsync(
        IReadOnlyCollection<Guid> campaignIds,
        CancellationToken cancellationToken = default
    );

    /// <summary>Everything currently out for decision with one client, from the by-client index.</summary>
    Task<IReadOnlyList<ReviewAssignment>> GetActiveForClientAsync(
        string clientCode,
        CancellationToken cancellationToken = default
    );

    Task UpsertAsync(ReviewAssignment assignment, CancellationToken cancellationToken = default);
}
