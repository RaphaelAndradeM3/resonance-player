using Resonance.Core.Models;

namespace Resonance.Core.Services.Abstractions;

/// <summary>
///     Defines the contract for the atomic, safe tag write engine,
///     file lock coordination, and catalog synchronization.
/// </summary>
public interface ITagWriterService
{
    /// <summary>
    ///     Safely applies a validated <see cref="TagWritePlan"/> using an atomic temporary file,
    ///     verifies audio container integrity, handles file attributes and playback locks,
    ///     replaces the original file, and updates the SQLite library catalog.
    /// </summary>
    /// <param name="plan">The write plan containing approved field changes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="TagWriteResult"/> containing telemetry and success/failure status.</returns>
    Task<TagWriteResult> ApplyWritePlanAsync(
        TagWritePlan plan,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Verifies the integrity of an audio file using ATL.NET to guarantee the container is intact,
    ///     headers are readable, and audio duration is positive.
    /// </summary>
    /// <param name="filePath">Absolute physical file path of the audio file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the file is valid and readable; otherwise false.</returns>
    Task<bool> ValidateAudioFileIntegrityAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}
