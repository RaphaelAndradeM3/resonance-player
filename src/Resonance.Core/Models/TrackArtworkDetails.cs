namespace Resonance.Core.Models;

/// <summary>
///     Identifies the origin of the cover artwork.
/// </summary>
public enum ArtworkSource
{
    None,
    Embedded,
    AdjacentFolder,
    RemoteCache
}

/// <summary>
///     Encapsulates artwork specifications and origin for a track.
/// </summary>
public class TrackArtworkDetails
{
    public string? CoverArtUri { get; set; }
    public string? MimeType { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public long? FileSizeBytes { get; set; }
    public ArtworkSource Source { get; set; } = ArtworkSource.None;
    public string DimensionsFormatted => Width.HasValue && Height.HasValue ? $"{Width} x {Height}" : "N/A";
}
