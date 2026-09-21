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

    public string DimensionsSummary
    {
        get
        {
            if (Width.HasValue && Height.HasValue)
            {
                var sizePart = FileSizeBytes.HasValue ? $" ({FileSizeBytes.Value / 1024.0:F1} KB)" : "";
                return $"{Width} x {Height} px{sizePart}";
            }
            return "Dimensões Indisponíveis";
        }
    }

    public string SourceSummary => Source switch
    {
        ArtworkSource.Embedded => "Capa Embutida no Arquivo",
        ArtworkSource.AdjacentFolder => "Imagem na Pasta Local",
        ArtworkSource.RemoteCache => "Cache da Biblioteca",
        _ => "Sem Imagem de Capa"
    };
}
