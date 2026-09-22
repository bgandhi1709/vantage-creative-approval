namespace Vantage.Approvals.Core.Models;

/// <summary>
/// A comment on a review round. Internal notes are producer-only and never leave the console;
/// the flag is enforced in the service layer, not in the reader.
/// </summary>
public sealed class ReviewNote
{
    public Guid NoteId { get; set; }

    public Guid ReviewId { get; set; }

    public string AuthorEmail { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public DateTimeOffset CreatedUtc { get; set; }

    public bool IsInternal { get; set; }
}
