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

    public string ArtistsFormatted => Artists.Count > 0 ? string.Join(", ", Artists) : "—";
    public string GenresFormatted => Genres.Count > 0 ? string.Join(", ", Genres) : "—";

    public string TrackAndDiscSummary
    {
        get
        {
            var trackStr = TrackNumber.HasValue
                ? (TrackCount.HasValue ? $"{TrackNumber}/{TrackCount}" : $"{TrackNumber}")
                : "—";
            var discStr = DiscNumber.HasValue
                ? (DiscCount.HasValue ? $"{DiscNumber}/{DiscCount}" : $"{DiscNumber}")
                : null;
            return discStr != null ? $"Faixa {trackStr} (Disco {discStr})" : $"Faixa {trackStr}";
        }
    }

    public string YearSummary => Year.HasValue && Year.Value > 0 ? Year.Value.ToString() : "—";

    public string ReplayGainTrackGainFormatted => ReplayGainTrackGain.HasValue ? $"{ReplayGainTrackGain.Value:+0.00;-0.00;0.00} dB" : "—";
    public string ReplayGainTrackPeakFormatted => ReplayGainTrackPeak.HasValue ? $"{ReplayGainTrackPeak.Value:F6}" : "—";
    public string ReplayGainAlbumGainFormatted => ReplayGainAlbumGain.HasValue ? $"{ReplayGainAlbumGain.Value:+0.00;-0.00;0.00} dB" : "—";
    public string ReplayGainAlbumPeakFormatted => ReplayGainAlbumPeak.HasValue ? $"{ReplayGainAlbumPeak.Value:F6}" : "—";
}
