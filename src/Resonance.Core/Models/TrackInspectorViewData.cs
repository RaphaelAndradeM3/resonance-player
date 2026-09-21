namespace Resonance.Core.Models;

/// <summary>
///     Consolidated presentation model for the Track Inspector panel.
/// </summary>
public class TrackInspectorViewData
{
    public TrackTechnicalDetails Technical { get; set; } = new();
    public TrackTagDetails Tags { get; set; } = new();
    public TrackArtworkDetails Artwork { get; set; } = new();
    public TrackExternalIds ExternalIds { get; set; } = new();

    /// <summary>
    ///     Visual label indicating the origin of the metadata.
    /// </summary>
    public string ProvenanceLabel { get; set; } = "Arquivo Local";

    // Multi-track pagination state
    public int CurrentTrackIndex { get; set; } = 1;
    public int TotalSelectedTracks { get; set; } = 1;
    public bool IsMultiTrackSelection => TotalSelectedTracks > 1;
    public bool HasPreviousTrack => CurrentTrackIndex > 1;
    public bool HasNextTrack => CurrentTrackIndex < TotalSelectedTracks;
}
