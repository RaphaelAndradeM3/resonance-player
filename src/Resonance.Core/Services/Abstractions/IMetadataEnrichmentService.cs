using Resonance.Core.Models;

namespace Resonance.Core.Services.Abstractions;

/// <summary>
///     Service responsible for comparing local song metadata with remote online suggestions
///     and generating a comprehensive, non-destructive <see cref="EnrichmentProposal"/>.
/// </summary>
public interface IMetadataEnrichmentService
{
    /// <summary>
    ///     Compares local track audio tags with resolved MusicBrainz recording details and generates
    ///     an <see cref="EnrichmentProposal"/> with field-by-field provenance and status.
    /// </summary>
    /// <param name="filePath">Physical path of the audio file.</param>
    /// <param name="localTags">Current local tags read from file or database.</param>
    /// <param name="remoteData">Canonical metadata retrieved from MusicBrainz.</param>
    /// <param name="coverArtUrl">Resolved front cover URL from Cover Art Archive, if any.</param>
    /// <param name="songId">Optional Song ID if the track is indexed in library.</param>
    /// <param name="originalCoverPath">Path to existing local cover art, if any.</param>
    /// <returns>An immutable <see cref="EnrichmentProposal"/> ready for review and UI presentation.</returns>
    EnrichmentProposal CreateProposal(
        string filePath,
        TrackAudioTags localTags,
        MusicBrainzRecordingDetail remoteData,
        string? coverArtUrl = null,
        Guid? songId = null,
        string? originalCoverPath = null);

    /// <summary>
    ///     Compares local track tag details with resolved MusicBrainz recording details and generates
    ///     an <see cref="EnrichmentProposal"/> with field-by-field provenance and status.
    /// </summary>
    EnrichmentProposal CreateProposal(
        string filePath,
        TrackTagDetails localTagDetails,
        MusicBrainzRecordingDetail remoteData,
        string? coverArtUrl = null,
        Guid? songId = null,
        string? originalCoverPath = null);

    /// <summary>
    ///     Merges local genre with remote tags using semantic deduplication per FR-013.
    /// </summary>
    /// <param name="localGenre">Current local genre string.</param>
    /// <param name="remoteGenres">List of genre tags from MusicBrainz.</param>
    /// <returns>Deduplicated, semicolon-separated genre string.</returns>
    string MergeGenres(string? localGenre, IReadOnlyList<string>? remoteGenres);
}
