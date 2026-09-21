namespace Resonance.Core.Models;

/// <summary>
///     Encapsulates musical metadata and editorial tags of a track.
/// </summary>
public class TrackTagDetails
{
    public string Title { get; set; } = string.Empty;
    public List<string> Artists { get; set; } = new();
    public string? Album { get; set; }
    public List<string> AlbumArtists { get; set; } = new();
    public int? TrackNumber { get; set; }
    public int? TrackCount { get; set; }
    public int? DiscNumber { get; set; }
    public int? DiscCount { get; set; }
    public int? Year { get; set; }
    public List<string> Genres { get; set; } = new();
    public string? Composer { get; set; }
    public string? Conductor { get; set; }
    public string? Grouping { get; set; }
    public string? Copyright { get; set; }
    public string? Comment { get; set; }
    public string? Isrc { get; set; }
    public double? Bpm { get; set; }

    // ReplayGain
    public double? ReplayGainTrackGain { get; set; }
    public double? ReplayGainTrackPeak { get; set; }
    public double? ReplayGainAlbumGain { get; set; }
    public double? ReplayGainAlbumPeak { get; set; }

    // Lyrics
    public bool HasLyrics { get; set; }
    public bool HasSynchronizedLyrics { get; set; }
    public string? LyricsPreview { get; set; }
}
