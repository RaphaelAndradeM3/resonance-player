namespace Resonance.Core.Models;

/// <summary>
///     Consolidated canonical metadata extracted from MusicBrainz Web Service for a recording.
/// </summary>
public class MusicBrainzRecordingDetail
{
    /// <summary>MusicBrainz Recording ID (UUID).</summary>
    public string RecordingId { get; init; } = string.Empty;

    /// <summary>Title of the recording.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Primary artist name (with joinphrases resolved).</summary>
    public string Artist { get; init; } = string.Empty;

    /// <summary>MusicBrainz Artist ID (UUID), if available.</summary>
    public string? ArtistId { get; init; }

    /// <summary>MusicBrainz Release ID (UUID) for the selected canonical release.</summary>
    public string? ReleaseId { get; init; }

    /// <summary>Canonical album / release title.</summary>
    public string? Album { get; init; }

    /// <summary>Album artist name, if different from recording artist.</summary>
    public string? AlbumArtist { get; init; }

    /// <summary>Original release year parsed from release date.</summary>
    public int? Year { get; init; }

    /// <summary>Full release date string (e.g. "1975-11-21" or "1975").</summary>
    public string? ReleaseDate { get; init; }

    /// <summary>Track number on the specific disc/medium.</summary>
    public int? TrackNumber { get; init; }

    /// <summary>Total track count on the specific disc/medium.</summary>
    public int? TotalTracks { get; init; }

    /// <summary>Disc / medium position index (1-based).</summary>
    public int? DiscNumber { get; init; }

    /// <summary>Total discs / media count in the release.</summary>
    public int? TotalDiscs { get; init; }

    /// <summary>Record label name, if available.</summary>
    public string? Label { get; init; }

    /// <summary>International Standard Recording Code (ISRC), if available.</summary>
    public string? Isrc { get; init; }

    /// <summary>List of community genre tags ordered by vote count.</summary>
    public IReadOnlyList<string> Genres { get; init; } = Array.Empty<string>();

    /// <summary>Search relevance score (1-100), default 100 for direct MBID lookups.</summary>
    public int Score { get; init; } = 100;
}
