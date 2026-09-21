namespace Resonance.Core.Models;

/// <summary>
///     Encapsulates external database identifiers associated with a track.
/// </summary>
public class TrackExternalIds
{
    public string? AcoustId { get; set; }
    public string? MusicBrainzTrackId { get; set; }
    public string? MusicBrainzReleaseId { get; set; }
    public string? MusicBrainzArtistId { get; set; }

    public bool HasAnyExternalId => !string.IsNullOrEmpty(AcoustId) ||
                                     !string.IsNullOrEmpty(MusicBrainzTrackId) ||
                                     !string.IsNullOrEmpty(MusicBrainzReleaseId) ||
                                     !string.IsNullOrEmpty(MusicBrainzArtistId);
}
