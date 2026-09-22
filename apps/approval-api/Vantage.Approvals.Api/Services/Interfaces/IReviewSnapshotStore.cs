namespace Vantage.Approvals.Api.Services.Interfaces;

/// <summary>
/// Immutable JSON snapshots of what the service sent and what it was told, written to blob storage.
/// </summary>
/// <remarks>
/// Snapshots are the audit trail. Table rows move forward; a snapshot records the exact payload at
/// a moment, so a dispute about what a client was shown is answered by a file, not a reconstruction.
/// </remarks>
public interface IReviewSnapshotStore
{
    /// <summary>Writes <paramref name="payload"/> as JSON under {clientCode}/{reviewId}/v{version}/{name}.json.</summary>
    Task WriteAsync<T>(
        string clientCode,
        Guid reviewId,
        int version,
        string name,
        T payload,
        CancellationToken cancellationToken = default
    );

    Task<string?> ReadAsync(
        string clientCode,
        Guid reviewId,
        int version,
        string name,
        CancellationToken cancellationToken = default
    );
}
