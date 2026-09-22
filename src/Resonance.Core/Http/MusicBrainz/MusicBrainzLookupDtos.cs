using System.Text.Json.Serialization;

namespace Resonance.Core.Http.MusicBrainz;

/// <summary>
///     Response payload for a direct recording lookup by MBID: /ws/2/recording/{mbid}?inc=releases+artists+media+isrcs+tags&fmt=json
/// </summary>
public sealed class MusicBrainzRecordingLookupResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("length")]
    public int? LengthMs { get; set; }

    [JsonPropertyName("artist-credit")]
    public List<MusicBrainzArtistCreditDto>? ArtistCredits { get; set; }

    [JsonPropertyName("releases")]
    public List<MusicBrainzReleaseLookupDto>? Releases { get; set; }

    [JsonPropertyName("isrcs")]
    public List<string>? Isrcs { get; set; }

    [JsonPropertyName("tags")]
    public List<MusicBrainzTagDto>? Tags { get; set; }
}

public sealed class MusicBrainzArtistCreditDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("joinphrase")]
    public string? JoinPhrase { get; set; }

    [JsonPropertyName("artist")]
    public MusicBrainzArtistRefDto? Artist { get; set; }
}

public sealed class MusicBrainzArtistRefDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public sealed class MusicBrainzReleaseLookupDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("date")]
    public string? Date { get; set; }

    [JsonPropertyName("country")]
    public string? Country { get; set; }

    [JsonPropertyName("release-group")]
    public MusicBrainzReleaseGroupDto? ReleaseGroup { get; set; }

    [JsonPropertyName("media")]
    public List<MusicBrainzMediaDto>? Media { get; set; }

    [JsonPropertyName("label-info")]
    public List<MusicBrainzLabelInfoDto>? LabelInfo { get; set; }

    [JsonPropertyName("artist-credit")]
    public List<MusicBrainzArtistCreditDto>? ArtistCredits { get; set; }
}

public sealed class MusicBrainzReleaseGroupDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("primary-type")]
    public string? PrimaryType { get; set; }

    [JsonPropertyName("secondary-types")]
    public List<string>? SecondaryTypes { get; set; }
}

public sealed class MusicBrainzMediaDto
{
    [JsonPropertyName("position")]
    public int Position { get; set; }

    [JsonPropertyName("track-count")]
    public int TrackCount { get; set; }

    [JsonPropertyName("track-offset")]
    public int? TrackOffset { get; set; }

    [JsonPropertyName("tracks")]
    public List<MusicBrainzTrackDto>? Tracks { get; set; }
}

public sealed class MusicBrainzTrackDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("position")]
    public int Position { get; set; }

    [JsonPropertyName("number")]
    public string? Number { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }
}

public sealed class MusicBrainzLabelInfoDto
{
    [JsonPropertyName("label")]
    public MusicBrainzLabelRefDto? Label { get; set; }
}

public sealed class MusicBrainzLabelRefDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public sealed class MusicBrainzTagDto
{
    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

/// <summary>
///     Response payload for recording search: /ws/2/recording?query=...&fmt=json
/// </summary>
public sealed class MusicBrainzRecordingSearchResponse
{
    [JsonPropertyName("recordings")]
    public List<MusicBrainzRecordingSearchResultDto>? Recordings { get; set; }
}

public sealed class MusicBrainzRecordingSearchResultDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("score")]
    public int Score { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("artist-credit")]
    public List<MusicBrainzArtistCreditDto>? ArtistCredits { get; set; }

    [JsonPropertyName("releases")]
    public List<MusicBrainzReleaseLookupDto>? Releases { get; set; }

    [JsonPropertyName("tags")]
    public List<MusicBrainzTagDto>? Tags { get; set; }

    [JsonPropertyName("isrcs")]
    public List<string>? Isrcs { get; set; }
}
