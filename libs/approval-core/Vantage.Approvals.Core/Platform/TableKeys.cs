namespace Vantage.Approvals.Core.Platform;

/// <summary>
/// The single formatter for identifiers used as Table Storage keys.
/// </summary>
/// <remarks>
/// Table Storage compares PartitionKey and RowKey as case-sensitive strings, unlike a relational
/// uniqueidentifier. A GUID formatted "D" by one writer and "N" by another — or upper-cased by a
/// third — produces rows that look identical in a browser and never match on read. Every key goes
/// through here so the representation is decided in one place.
/// </remarks>
public static class TableKeys
{
    /// <summary>Lower-case dashed form, for example "8e2b...-...-...-...-...".</summary>
    public static string From(Guid id) => id.ToString("D").ToLowerInvariant();

    /// <summary>Normalizes a caller-supplied code (client code, locale) used as a partition key.</summary>
    public static string FromCode(string code) =>
        string.IsNullOrWhiteSpace(code)
            ? throw new ArgumentException("A key code cannot be empty.", nameof(code))
            : code.Trim().ToLowerInvariant();
}
