namespace Resonance.Core.Models;

/// <summary>
///     Encapsulates the outcome, telemetry, and diagnostics of an atomic tag write operation.
/// </summary>
public class TagWriteResult
{
    /// <summary>Gets whether the tag write and file replacement completed successfully.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the absolute physical file path of the processed audio file.</summary>
    public required string FilePath { get; init; }

    /// <summary>Gets the number of tag fields physically written or updated in the file.</summary>
    public int FieldsUpdatedCount { get; init; }

    /// <summary>Gets whether the audio playback stream was temporarily paused and resumed.</summary>
    public bool WasPlaybackInterrupted { get; init; }

    /// <summary>Gets the error message if the operation failed, or null if successful.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the total elapsed time taken by the write, validation, and replacement pipeline.</summary>
    public TimeSpan ElapsedTime { get; init; }

    /// <summary>Creates a failed outcome result.</summary>
    public static TagWriteResult Failed(string filePath, string errorMessage, TimeSpan elapsed) => new()
    {
        Success = false,
        FilePath = filePath,
        ErrorMessage = errorMessage,
        ElapsedTime = elapsed
    };

    /// <summary>Creates a successful outcome result.</summary>
    public static TagWriteResult Succeeded(string filePath, int fieldsCount, bool wasPlaybackInterrupted, TimeSpan elapsed) => new()
    {
        Success = true,
        FilePath = filePath,
        FieldsUpdatedCount = fieldsCount,
        WasPlaybackInterrupted = wasPlaybackInterrupted,
        ElapsedTime = elapsed
    };
}
