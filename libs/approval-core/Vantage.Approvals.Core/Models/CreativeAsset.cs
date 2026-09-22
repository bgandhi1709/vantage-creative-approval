namespace Vantage.Approvals.Core.Models;

/// <summary>
/// One asset inside a review round — a banner, a film cut, a print panel.
/// Nested with its owning aggregate because nothing outside a review reads an asset on its own.
/// </summary>
public sealed class CreativeAsset
{
    public Guid AssetId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Free-form format label, for example "1080x1920" or "30s cutdown".</summary>
    public string Format { get; set; } = string.Empty;

    /// <summary>Relative path of the rendered preview inside the snapshot container.</summary>
    public string PreviewPath { get; set; } = string.Empty;
}
