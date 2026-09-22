using Azure;
using Azure.Data.Tables;

namespace Vantage.Approvals.Core.Storage;

internal sealed class ReviewNoteTableEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;

    public string RowKey { get; set; } = string.Empty;

    public DateTimeOffset? Timestamp { get; set; }

    public ETag ETag { get; set; }

    public Guid NoteId { get; set; }

    public string AuthorEmail { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public DateTimeOffset CreatedUtc { get; set; }

    public bool IsInternal { get; set; }
}
