using System.Globalization;
using Azure.Data.Tables;
using Vantage.Approvals.Core.Models;
using Vantage.Approvals.Core.Platform;
using Vantage.Approvals.Core.Repositories;

namespace Vantage.Approvals.Core.Storage.Repositories;

internal sealed class ReviewNoteTableRepository(TableServiceClient serviceClient)
    : IReviewNoteRepository
{
    private readonly TableClient table = serviceClient.GetTableClient(TableNames.Notes);

    public async Task<IReadOnlyList<ReviewNote>> GetForReviewAsync(
        Guid reviewId,
        bool includeInternal,
        CancellationToken cancellationToken = default
    )
    {
        var client = await this.table.EnsureExistsAsync(cancellationToken).ConfigureAwait(false);
        var partition = TableKeys.From(reviewId);

        var notes = new List<ReviewNote>();

        // The internal flag is filtered server-side so an internal note never leaves storage on a
        // client-facing request, even if a caller forgets to filter afterwards.
        var query = includeInternal
            ? client.QueryAsync<ReviewNoteTableEntity>(
                entity => entity.PartitionKey == partition,
                cancellationToken: cancellationToken
            )
            : client.QueryAsync<ReviewNoteTableEntity>(
                entity => entity.PartitionKey == partition && entity.IsInternal == false,
                cancellationToken: cancellationToken
            );

        await foreach (var entity in query.ConfigureAwait(false))
        {
            notes.Add(ToModel(reviewId, entity));
        }

        return notes;
    }

    public async Task CreateAsync(ReviewNote note, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(note);

        var client = await this.table.EnsureExistsAsync(cancellationToken).ConfigureAwait(false);
        await client.AddEntityAsync(ToEntity(note), cancellationToken).ConfigureAwait(false);
    }

    private static ReviewNoteTableEntity ToEntity(ReviewNote note) =>
        new()
        {
            PartitionKey = TableKeys.From(note.ReviewId),
            RowKey = string.Create(
                CultureInfo.InvariantCulture,
                $"{note.CreatedUtc.UtcDateTime.Ticks:D19}-{TableKeys.From(note.NoteId)}"
            ),
            NoteId = note.NoteId,
            AuthorEmail = note.AuthorEmail,
            Body = note.Body,
            CreatedUtc = note.CreatedUtc.ToUniversalTime(),
            IsInternal = note.IsInternal,
        };

    private static ReviewNote ToModel(Guid reviewId, ReviewNoteTableEntity entity) =>
        new()
        {
            NoteId = entity.NoteId,
            ReviewId = reviewId,
            AuthorEmail = entity.AuthorEmail,
            Body = entity.Body,
            CreatedUtc = entity.CreatedUtc,
            IsInternal = entity.IsInternal,
        };
}
