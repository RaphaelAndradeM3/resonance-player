using Resonance.Core.Models;
using Resonance.Core.Models.Lyrics;

namespace Resonance.Core.Services.Abstractions;

/// <summary>
///     Service for loading, parsing, and interacting with lyric files,
///     supporting canonical 6-stage resolution, provenance tracking, and offset calibration.
/// </summary>
public interface ILrcService
{
    /// <summary>
    ///     Executes the canonical 6-stage lyrics resolution for the specified song:
    ///     1. Embedded Synced -> 2. Embedded Plain -> 3. Sidecar .lrc -> 4. Sidecar .txt -> 5. Local Cache -> 6. Remote Providers.
    /// </summary>
    /// <param name="song">The song object with metadata and file paths.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A populated LyricsDocument with lines, provenance and metadata, or null if no lyrics are found.</returns>
    Task<LyricsDocument?> ResolveLyricsAsync(Song song, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Explicitly exports lyrics to a sidecar (.lrc) file in the audio file's directory.
    /// </summary>
    /// <param name="song">The target song.</param>
    /// <param name="lrcContent">The formatted LRC content to write.</param>
    /// <returns>True if exported successfully, false otherwise.</returns>
    Task<bool> ExportSidecarLrcAsync(Song song, string lrcContent);

    /// <summary>
    ///     Updates and persists the manual lyrics timing calibration offset for the song.
    /// </summary>
    /// <param name="song">The target song.</param>
    /// <param name="offsetMs">Offset in milliseconds (+ advances lyrics, - delays lyrics).</param>
    Task SetLyricsOffsetAsync(Song song, int offsetMs);

    /// <summary>
    ///     Asynchronously loads and parses an LRC file from the path specified in the Song object.
    /// </summary>
    /// <param name="song">The song object containing the LrcFilePath.</param>
    /// <returns>A ParsedLrc object containing the timed lyrics, or null if parsing fails or the file doesn't exist.</returns>
    Task<ParsedLrc?> GetLyricsAsync(Song song, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Asynchronously loads and parses an LRC file from a direct file path.
    /// </summary>
    /// <param name="lrcFilePath">The full path to the .lrc file.</param>
    /// <returns>A ParsedLrc object containing the timed lyrics, or null if parsing fails or the file doesn't exist.</returns>
    Task<ParsedLrc?> GetLyricsAsync(string lrcFilePath);

    /// <summary>Saves a lyrics override in Resonance's cache.</summary>
    Task SaveLyricsAsync(Song song, string lrcContent);

    /// <summary>Removes cached lyrics without deleting external sidecars.</summary>
    Task<bool> RemoveCachedLyricsAsync(Song song);

    /// <summary>Returns whether Resonance manages the song's lyrics file.</summary>
    bool HasCachedLyrics(Song song);

    /// <summary>
    ///     Parses a raw LRC string into a ParsedLrc object, which may contain synced or unsynced lyrics.
    /// </summary>
    /// <param name="lrcContent">The raw LRC string content.</param>
    /// <returns>A ParsedLrc object containing the parsed lyrics.</returns>
    ParsedLrc ParseLyrics(string? lrcContent);

    /// <summary>
    ///     Gets the current lyric line based on the playback time.
    /// </summary>
    /// <param name="parsedLrc">The parsed LRC data.</param>
    /// <param name="currentTime">The current playback time of the song.</param>
    /// <returns>The active LyricLine for the given time, or null if no line is active.</returns>
    LyricLine? GetCurrentLine(ParsedLrc parsedLrc, TimeSpan currentTime);

    /// <summary>
    ///     Gets the current lyric line using a search hint for optimal sequential performance.
    /// </summary>
    /// <param name="parsedLrc">The parsed LRC data.</param>
    /// <param name="currentTime">The current playback time.</param>
    /// <param name="searchStartIndex">A reference to the last known line index. This will be updated by the method.</param>
    /// <returns>The active LyricLine.</returns>
    LyricLine? GetCurrentLine(ParsedLrc parsedLrc, TimeSpan currentTime, ref int searchStartIndex);
}
