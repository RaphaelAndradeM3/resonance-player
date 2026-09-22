namespace Resonance.Core.Models;

/// <summary>
///     Encapsulates track audio tags for metadata enrichment comparison.
/// </summary>
public class TrackAudioTags
{
    public string Title { get; set; } = string.Empty;
    public string? Artist { get; set; }
    public string? Album { get; set; }
    public string? AlbumArtist { get; set; }
    public int? Year { get; set; }
    public int? TrackNumber { get; set; }
    public int? TotalTracks { get; set; }
    public int? DiscNumber { get; set; }
    public int? TotalDiscs { get; set; }
    public string? Genre { get; set; }
    public string? Label { get; set; }
    public string? Isrc { get; set; }
    public string? Comment { get; set; }
    public double? Bpm { get; set; }

    /// <summary>
    ///     Creates a <see cref="TrackAudioTags"/> from existing <see cref="TrackTagDetails"/>.
    /// </summary>
    public static TrackAudioTags FromTrackTagDetails(TrackTagDetails details)
    {
        return new TrackAudioTags
        {
            Title = details.Title,
            Artist = details.ArtistsFormatted != "—" ? details.ArtistsFormatted : null,
            Album = details.Album,
            AlbumArtist = details.AlbumArtists.Count > 0 ? string.Join(", ", details.AlbumArtists) : null,
            Year = details.Year,
            TrackNumber = details.TrackNumber,
            TotalTracks = details.TrackCount,
            DiscNumber = details.DiscNumber,
            TotalDiscs = details.DiscCount,
            Genre = details.GenresFormatted != "—" ? details.GenresFormatted : null,
            Isrc = details.Isrc,
            Comment = details.Comment,
            Bpm = details.Bpm
        };
    }
}
