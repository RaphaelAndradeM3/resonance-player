using Resonance.Core.Models;

namespace Resonance.Core.Services.Abstractions;

/// <summary>
///     Defines a contract for extracting metadata from a music file.
/// </summary>
public interface IMetadataService
{
    /// <summary>
    ///     Asynchronously extracts all relevant metadata from a single music file.
    /// </summary>
    /// <param name="filePath">The absolute path to the music file.</param>
    /// <param name="baseFolderPath">
    ///     Optional. The root folder path for the library scan. When provided, the service will search
    ///     for cover art files in the directory hierarchy up to (and including) this folder if no
    ///     embedded art is found.
    /// </param>
    /// <param name="includeMediaAssets">Whether to process cover art and synchronized lyrics.</param>
    /// <returns>A <see cref="SongFileMetadata" /> object containing the extracted data and file properties.</returns>
    Task<SongFileMetadata> ExtractMetadataAsync(string filePath, string? baseFolderPath = null,
        bool includeMediaAssets = true);

    /// <summary>
    ///     Asynchronously constructs the consolidated inspection view for an audio file.
    /// </summary>
    /// <param name="filePath">Absolute physical file path of the audio file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Consolidated view model for the Track Inspector.</returns>
    Task<TrackInspectorViewData> GetTrackInspectorViewDataAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Asynchronously constructs the consolidated inspection view from an existing indexed Song.
    /// </summary>
    /// <param name="song">The indexed song entity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Consolidated view model for the Track Inspector.</returns>
    Task<TrackInspectorViewData> GetTrackInspectorViewDataAsync(Song song, CancellationToken cancellationToken = default);
}
