using System.Linq;
using ATL;
using Microsoft.Extensions.Logging;
using ModernLrc;
using ModernLrc.Model;
using Resonance.Core.Data;
using Resonance.Core.Helpers;
using Resonance.Core.Models;
using Resonance.Core.Models.Lyrics;
using Resonance.Core.Services.Abstractions;

namespace Resonance.Core.Services.Implementations;

/// <summary>
///     Service for loading, parsing, and interacting with .lrc lyric files,
///     optimized for high-performance playback synchronization.
/// </summary>
public class LrcService : ILrcService, IDisposable
{
    private readonly IFileSystemService _fileSystemService;
    private readonly ILogger<LrcService> _logger;
    private readonly IOnlineLyricsService _onlineLyricsService;
    private readonly INetEaseLyricsService _netEaseLyricsService;
    private readonly ISettingsService _settingsService;
    private readonly IPathConfiguration _pathConfig;
    private readonly ILibraryWriter _libraryWriter;

    private readonly object _ctsLock = new();
    private CancellationTokenSource _settingsCts = new();
    private bool _disposed;

    public LrcService(
        IFileSystemService fileSystemService,
        IOnlineLyricsService onlineLyricsService,
        INetEaseLyricsService netEaseLyricsService,
        ISettingsService settingsService,
        IPathConfiguration pathConfig,
        ILibraryWriter libraryWriter,
        ILogger<LrcService> logger)
    {
        _fileSystemService = fileSystemService;
        _onlineLyricsService = onlineLyricsService;
        _netEaseLyricsService = netEaseLyricsService;
        _settingsService = settingsService;
        _pathConfig = pathConfig;
        _libraryWriter = libraryWriter;
        _logger = logger;
        _settingsService.FetchOnlineLyricsEnabledChanged += OnFetchOnlineLyricsEnabledChanged;
    }

    /// <inheritdoc />
    public async Task<LyricsDocument?> ResolveLyricsAsync(Song song, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(song);

        if (cancellationToken.IsCancellationRequested)
            return null;

        // Stage 1 & 2: Embedded Lyrics via ATL
        if (!string.IsNullOrWhiteSpace(song.FilePath))
        {
            var (syncedLyrics, unsyncedLyrics) = ExtractEmbeddedLyrics(song.FilePath);

            // Stage 1: Embedded Synced
            if (syncedLyrics != null && syncedLyrics.Count > 0)
            {
                var lines = new List<LyricLine>();
                foreach (var phase in syncedLyrics.OrderBy(p => p.TimestampStart))
                {
                    var text = ArtistNameHelper.NormalizeStringCore(phase.Text);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        lines.Add(new LyricLine(TimeSpan.FromMilliseconds(phase.TimestampStart), text));
                    }
                }

                if (lines.Count > 0)
                {
                    _logger.LogDebug("Resolved embedded synced lyrics for song {SongId} ({Count} lines)", song.Id, lines.Count);
                    return new LyricsDocument(
                        lines,
                        unsyncedLyrics,
                        LyricsType.Synced,
                        LyricsProvenance.EmbeddedSynced,
                        song.FilePath,
                        TimeSpan.Zero);
                }
            }

            // Stage 2: Embedded Plain
            if (!string.IsNullOrWhiteSpace(unsyncedLyrics))
            {
                _logger.LogDebug("Resolved embedded plain lyrics for song {SongId}", song.Id);
                return new LyricsDocument(
                    Enumerable.Empty<LyricLine>(),
                    unsyncedLyrics.Trim(),
                    LyricsType.Plain,
                    LyricsProvenance.EmbeddedPlain,
                    song.FilePath,
                    TimeSpan.Zero);
            }

            // Stage 3: Sidecar .lrc
            var lrcSidecar = FindLocalSidecarPath(song.FilePath, ".lrc", song.PrimaryArtistName, song.Title);
            if (!string.IsNullOrWhiteSpace(lrcSidecar) && _fileSystemService.FileExists(lrcSidecar))
            {
                try
                {
                    var content = await _fileSystemService.ReadAllTextAsync(lrcSidecar).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        var parsed = ParseLyrics(content);
                        if (!parsed.IsEmpty)
                        {
                            _logger.LogDebug("Resolved sidecar .lrc for song {SongId} from {Path}", song.Id, lrcSidecar);
                            return new LyricsDocument(
                                parsed.Lines,
                                parsed.RawUnsyncedLyrics,
                                LyricsType.Synced,
                                LyricsProvenance.LocalFileLrc,
                                lrcSidecar,
                                TimeSpan.Zero);
                        }

                        if (!string.IsNullOrWhiteSpace(parsed.RawUnsyncedLyrics))
                        {
                            _logger.LogDebug("Resolved sidecar .lrc as plain text for song {SongId} from {Path}", song.Id, lrcSidecar);
                            return new LyricsDocument(
                                Enumerable.Empty<LyricLine>(),
                                parsed.RawUnsyncedLyrics.Trim(),
                                LyricsType.Plain,
                                LyricsProvenance.LocalFileLrc,
                                lrcSidecar,
                                TimeSpan.Zero);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read sidecar .lrc at {Path} for song {SongId}", lrcSidecar, song.Id);
                }
            }

            // Stage 4: Sidecar .txt
            var txtSidecar = FindLocalSidecarPath(song.FilePath, ".txt", song.PrimaryArtistName, song.Title);
            if (!string.IsNullOrWhiteSpace(txtSidecar) && _fileSystemService.FileExists(txtSidecar))
            {
                try
                {
                    var txtContent = await _fileSystemService.ReadAllTextAsync(txtSidecar).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(txtContent))
                    {
                        _logger.LogDebug("Resolved sidecar .txt for song {SongId} from {Path}", song.Id, txtSidecar);
                        return new LyricsDocument(
                            Enumerable.Empty<LyricLine>(),
                            txtContent.Trim(),
                            LyricsType.Plain,
                            LyricsProvenance.LocalFileTxt,
                            txtSidecar,
                            TimeSpan.Zero);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read sidecar .txt at {Path} for song {SongId}", txtSidecar, song.Id);
                }
            }
        }

        // Stages 5 and 6 (Cache & Remote Providers) are added in Slice 2
        return null;
    }

    /// <inheritdoc />
    public async Task<bool> ExportSidecarLrcAsync(Song song, string lrcContent)
    {
        ArgumentNullException.ThrowIfNull(song);
        if (string.IsNullOrWhiteSpace(song.FilePath) || string.IsNullOrWhiteSpace(lrcContent)) return false;

        try
        {
            var dir = _fileSystemService.GetDirectoryName(song.FilePath);
            var baseName = _fileSystemService.GetFileNameWithoutExtension(song.FilePath);
            if (string.IsNullOrWhiteSpace(dir) || string.IsNullOrWhiteSpace(baseName)) return false;

            var targetPath = _fileSystemService.Combine(dir, $"{baseName}.lrc");
            await _fileSystemService.WriteAllTextAsync(targetPath, lrcContent).ConfigureAwait(false);
            _logger.LogInformation("Exported sidecar LRC for song {SongId} to {Path}", song.Id, targetPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export sidecar LRC for song {SongId} to {FilePath}", song.Id, song.FilePath);
            return false;
        }
    }

    /// <inheritdoc />
    public Task SetLyricsOffsetAsync(Song song, int offsetMs)
    {
        ArgumentNullException.ThrowIfNull(song);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<ParsedLrc?> GetLyricsAsync(Song song, CancellationToken cancellationToken = default)
    {
        var forceOnlineRefresh = false;

        // 1. Try the local path from the Song object. Cache files created before identity hashes
        // are not safe to reuse: two songs may already point at the same metadata-derived file.
        if (!string.IsNullOrWhiteSpace(song.LrcFilePath))
        {
            var isLegacyCachePath = IsLegacyCachePath(song);
            if (!isLegacyCachePath && _fileSystemService.FileExists(song.LrcFilePath))
                return await GetLyricsAsync(song.LrcFilePath).ConfigureAwait(false);

            forceOnlineRefresh = true;
            _logger.LogInformation(
                isLegacyCachePath
                    ? "Ignoring legacy collision-prone lyrics cache path while refreshing song {SongId}."
                    : "Ignoring missing lyrics path while refreshing song {SongId}.",
                song.Id);
        }

        // 2. Try online fallback if enabled AND never checked before
        if ((!forceOnlineRefresh && song.LyricsLastCheckedUtc != null)
            || !await _settingsService.GetFetchOnlineLyricsEnabledAsync().ConfigureAwait(false))
            return null;

        CancellationToken settingsToken;
        lock (_ctsLock)
        {
            if (_disposed) return null;
            settingsToken = _settingsCts.Token;
        }

        // Use a linked token source to handle both caller cancellation AND settings toggle
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, settingsToken);
        var token = linkedCts.Token;

        // Check for cancellation before making any online calls
        if (token.IsCancellationRequested)
            return null;

        var anyProviderSuccess = false;
        var enabledProviders = await _settingsService.GetEnabledServiceProvidersAsync(ServiceCategory.Lyrics).ConfigureAwait(false);

        if (enabledProviders.Count > 0)
        {
            // Start all enabled provider tasks in parallel for speed
            var tasks = new Dictionary<string, Task<string?>>();
            foreach (var provider in enabledProviders)
            {
                try
                {
                    tasks[provider.Id] = provider.Id switch
                    {
                        ServiceProviderIds.LrcLib => _onlineLyricsService.GetLyricsAsync(
                            song.Title, song.PrimaryArtistName, song.Album?.Title, song.Duration, token),
                        ServiceProviderIds.NetEase => _netEaseLyricsService.SearchLyricsAsync(
                            song.Title, song.PrimaryArtistName, token),
                        _ => LogUnknownProviderAndReturnNull(provider.Id)
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to initiate lyrics fetch for provider {Provider}", provider.DisplayName);
                }
            }

            // Evaluate results in priority order (but all tasks already running in parallel)
            string? lrcContent = null;
            foreach (var provider in enabledProviders)
            {
                if (!tasks.TryGetValue(provider.Id, out var task)) continue;

                try
                {
                    var result = await task.ConfigureAwait(false);
                    anyProviderSuccess = true;
                    if (!string.IsNullOrWhiteSpace(result))
                    {
                        lrcContent = result;
                        break;
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected when token is cancelled
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Lyrics provider {Provider} failed for '{Title}'.", provider.DisplayName, song.Title);
                }
            }

            // Fire-and-forget pattern: Observe remaining tasks to prevent UnobservedTaskException.
            // We use ContinueWith with a static lambda to avoid closure allocations. The logger is
            // passed via state parameter. NotOnRanToCompletion ensures we only handle faults/cancellations.
            foreach (var kvp in tasks)
            {
                if (kvp.Value.IsCompleted) continue;
                _ = kvp.Value.ContinueWith(
                    static (t, state) =>
                    {
                        if (t.IsFaulted)
                        {
                            var logger = (ILogger<LrcService>)state!;
                            logger.LogDebug(t.Exception?.InnerException, "Lyrics task faulted (ignored, already resolved)");
                        }
                    },
                    _logger,
                    TaskContinuationOptions.NotOnRanToCompletion);
            }

            if (!string.IsNullOrWhiteSpace(lrcContent))
            {
                // Cache the lyrics for future use
                await CacheLyricsAsync(song, lrcContent).ConfigureAwait(false);

                // Parse the downloaded lyrics string
                return ParseLyrics(lrcContent);
            }
        }

        // Only mark as checked if providers were attempted AND operation wasn't cancelled.
        // This allows retry if providers are enabled later or if the fetch was cancelled.
        if (enabledProviders.Count > 0 && !token.IsCancellationRequested && anyProviderSuccess)
        {
            // A provider completed successfully but returned no lyrics. Remove the invalid local
            // reference now; successful lyric results already replace it in CacheLyricsAsync.
            if (forceOnlineRefresh && !string.IsNullOrWhiteSpace(song.LrcFilePath))
            {
                song.LrcFilePath = null;
                await _libraryWriter.UpdateSongLrcPathAsync(song.Id, null).ConfigureAwait(false);
            }

            await _libraryWriter.UpdateSongLyricsLastCheckedAsync(song.Id).ConfigureAwait(false);
            song.LyricsLastCheckedUtc = DateTime.UtcNow;
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<ParsedLrc?> GetLyricsAsync(string lrcFilePath)
    {
        if (string.IsNullOrWhiteSpace(lrcFilePath) || !_fileSystemService.FileExists(lrcFilePath)) return null;

        try
        {
            var fileContent = await _fileSystemService.ReadAllTextAsync(lrcFilePath).ConfigureAwait(false);
            return ParseLyrics(fileContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse LRC file {LrcFilePath}", lrcFilePath);
            return null;
        }
    }

    /// <inheritdoc />
    public Task SaveLyricsAsync(Song song, string lrcContent)
    {
        ArgumentNullException.ThrowIfNull(song);
        ArgumentException.ThrowIfNullOrWhiteSpace(lrcContent);
        return WriteLyricsCacheAsync(song, lrcContent, true);
    }

    /// <inheritdoc />
    public async Task<bool> RemoveCachedLyricsAsync(Song song)
    {
        ArgumentNullException.ThrowIfNull(song);
        if (!HasCachedLyrics(song)) return false;

        var cachedPath = song.LrcFilePath!;
        var sidecarPath = FindExternalLyricsPath(song.FilePath);
        if (_fileSystemService.FileExists(cachedPath)) _fileSystemService.DeleteFile(cachedPath);

        song.LrcFilePath = sidecarPath;
        song.LyricsLastCheckedUtc = DateTime.UtcNow;
        await _libraryWriter.UpdateSongLrcPathAsync(song.Id, sidecarPath).ConfigureAwait(false);
        await _libraryWriter.UpdateSongLyricsLastCheckedAsync(song.Id).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    public bool HasCachedLyrics(Song song)
    {
        ArgumentNullException.ThrowIfNull(song);
        return IsCachePath(song.LrcFilePath);
    }

    private async Task CacheLyricsAsync(Song song, string lrcContent)
    {
        try
        {
            await WriteLyricsCacheAsync(song, lrcContent, false).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cache lyrics for song {SongId}", song.Id);
        }
    }

    private async Task WriteLyricsCacheAsync(Song song, string lrcContent, bool isOverride)
    {
        var cacheIdentity = string.IsNullOrWhiteSpace(song.FilePath) ? song.Id.ToString("N") : song.FilePath;
        var cacheFileName = isOverride
            ? FileNameHelper.GenerateLrcOverrideFileName(cacheIdentity)
            : FileNameHelper.GenerateLrcCacheFileName(
                cacheIdentity, song.PrimaryArtistName, song.Album?.Title, song.Title);
        var cachedLrcPath = _fileSystemService.Combine(_pathConfig.LrcCachePath, cacheFileName);
        var previousPath = song.LrcFilePath;

        await _fileSystemService.WriteAllTextAsync(cachedLrcPath, lrcContent).ConfigureAwait(false);
        _logger.LogDebug("Cached lyrics for song {SongId} to {Path}", song.Id, cachedLrcPath);

        if (previousPath == cachedLrcPath) return;
        await _libraryWriter.UpdateSongLrcPathAsync(song.Id, cachedLrcPath).ConfigureAwait(false);
        song.LrcFilePath = cachedLrcPath;

        if (previousPath is not null && IsCachePath(previousPath) && _fileSystemService.FileExists(previousPath))
            _fileSystemService.DeleteFile(previousPath);
    }

    private bool IsLegacyCachePath(Song song)
    {
        if (string.IsNullOrWhiteSpace(song.LrcFilePath)
            || string.IsNullOrWhiteSpace(_pathConfig.LrcCachePath))
            return false;

        if (!IsCachePath(song.LrcFilePath)) return false;

        var cacheIdentity = string.IsNullOrWhiteSpace(song.FilePath) ? song.Id.ToString("N") : song.FilePath;
        var candidate = PathCanonicalizer.Normalize(song.LrcFilePath);
        return !FileNameHelper.MatchesLrcCacheIdentity(candidate, cacheIdentity);
    }

    private bool IsCachePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(_pathConfig.LrcCachePath)) return false;

        var cacheRoot = PathCanonicalizer.Normalize(_pathConfig.LrcCachePath).TrimEnd('\\') + "\\";
        var candidate = PathCanonicalizer.Normalize(path);
        return candidate.StartsWith(cacheRoot, StringComparison.OrdinalIgnoreCase);
    }

    protected internal virtual (IList<LyricsInfo.LyricsPhrase>? SyncedLyrics, string? UnsyncedLyrics) ExtractEmbeddedLyrics(string audioFilePath)
    {
        if (string.IsNullOrWhiteSpace(audioFilePath) || !_fileSystemService.FileExists(audioFilePath))
            return (null, null);

        try
        {
            var track = new Track(audioFilePath);
            var lyricsInfo = track.Lyrics?.FirstOrDefault();
            return (lyricsInfo?.SynchronizedLyrics, lyricsInfo?.UnsynchronizedLyrics);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract embedded lyrics from {AudioFilePath}.", audioFilePath);
            return (null, null);
        }
    }

    private string? FindLocalSidecarPath(string audioFilePath, string extension, string? artist, string? title)
    {
        if (string.IsNullOrWhiteSpace(audioFilePath)) return null;

        try
        {
            var directory = _fileSystemService.GetDirectoryName(audioFilePath);
            if (string.IsNullOrWhiteSpace(directory)) return null;

            var audioName = _fileSystemService.GetFileNameWithoutExtension(audioFilePath);

            // 1. Direct check for exact base name first
            var exactCandidate = _fileSystemService.Combine(directory, $"{audioName}{extension}");
            if (_fileSystemService.FileExists(exactCandidate)) return exactCandidate;

            var searchPattern = $"*{extension}";
            var candidateFiles = _fileSystemService.GetFiles(directory, searchPattern);
            if (candidateFiles != null && candidateFiles.Length > 0)
            {
                var exactMatch = candidateFiles.FirstOrDefault(p =>
                    _fileSystemService.GetFileNameWithoutExtension(p).Equals(audioName, StringComparison.OrdinalIgnoreCase));
                if (exactMatch != null) return exactMatch;

                if (!string.IsNullOrWhiteSpace(artist) && !string.IsNullOrWhiteSpace(title))
                {
                    var artistTitlePattern = $"{artist.Trim()} - {title.Trim()}";
                    var artistTitleMatch = candidateFiles.FirstOrDefault(p =>
                        _fileSystemService.GetFileNameWithoutExtension(p).Equals(artistTitlePattern, StringComparison.OrdinalIgnoreCase));
                    if (artistTitleMatch != null) return artistTitleMatch;
                }
            }

            // 2. Fallback direct file check
            if (!string.IsNullOrWhiteSpace(artist) && !string.IsNullOrWhiteSpace(title))
            {
                var fallbackCandidate = _fileSystemService.Combine(directory, $"{artist.Trim()} - {title.Trim()}{extension}");
                if (_fileSystemService.FileExists(fallbackCandidate)) return fallbackCandidate;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to find local sidecar {Extension} for {AudioFilePath}.", extension, audioFilePath);
            return null;
        }
    }

    private string? FindExternalLyricsPath(string audioFilePath)
    {
        return FindLocalSidecarPath(audioFilePath, ".lrc", null, null)
               ?? FindLocalSidecarPath(audioFilePath, ".txt", null, null);
    }

    private Task<string?> LogUnknownProviderAndReturnNull(string providerId)
    {
        _logger.LogWarning("Unknown lyrics provider ID: {ProviderId}. This provider will be skipped.", providerId);
        return Task.FromResult<string?>(null);
    }

    public ParsedLrc ParseLyrics(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return new ParsedLrc(Enumerable.Empty<LyricLine>());

        try
        {
            var document = LrcParser.Parse(content).Document;
            var lyricLines = document.Lines.Select(line => new LyricLine(document.GetEffectiveTime(line.Timestamp), line.GetText()));
            return new ParsedLrc(lyricLines, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing LRC content");
            return new ParsedLrc(Enumerable.Empty<LyricLine>(), content);
        }
    }

    /// <inheritdoc />
    public LyricLine? GetCurrentLine(ParsedLrc parsedLrc, TimeSpan currentTime)
    {
        if (parsedLrc is null || parsedLrc.IsEmpty) return null;

        var index = FindBestMatchIndex(parsedLrc.Lines, currentTime);
        return index != -1 ? parsedLrc.Lines[index] : null;
    }

    /// <inheritdoc />
    public LyricLine? GetCurrentLine(ParsedLrc parsedLrc, TimeSpan currentTime, ref int searchStartIndex)
    {
        if (parsedLrc is null || parsedLrc.IsEmpty) return null;

        var lines = parsedLrc.Lines;
        var lineCount = lines.Count;

        // Check if the current or next line is the correct one, which is the most common case during playback.
        if (searchStartIndex >= 0 && searchStartIndex < lineCount)
        {
            var currentLine = lines[searchStartIndex];
            var nextLineStartTime = searchStartIndex + 1 < lineCount
                ? lines[searchStartIndex + 1].StartTime
                : TimeSpan.MaxValue;

            if (currentLine.StartTime <= currentTime && currentTime < nextLineStartTime) return currentLine;
        }

        // If the hint was wrong (e.g., due to seeking), perform a full binary search.
        var bestMatchIndex = FindBestMatchIndex(lines, currentTime);
        searchStartIndex = bestMatchIndex != -1 ? bestMatchIndex : 0;

        return bestMatchIndex != -1 ? lines[bestMatchIndex] : null;
    }

    /// <summary>
    ///     Performs a binary search to find the index of the lyric line that should be active at the given time.
    /// </summary>
    private int FindBestMatchIndex(IReadOnlyList<LyricLine> lines, TimeSpan currentTime)
    {
        var low = 0;
        var high = lines.Count - 1;
        var latestMatchIndex = -1;

        while (low <= high)
        {
            var mid = low + (high - low) / 2;
            if (lines[mid].StartTime <= currentTime)
            {
                latestMatchIndex = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return latestMatchIndex;
    }

    private void OnFetchOnlineLyricsEnabledChanged(bool isEnabled)
    {
        lock (_ctsLock)
        {
            if (_disposed) return;

            if (!isEnabled)
            {
                _logger.LogInformation("Fetch online lyrics disabled. Cancelling ongoing fetches.");
                if (!_settingsCts.IsCancellationRequested)
                {
                    _settingsCts.Cancel();
                }
            }
            else
            {
                if (_settingsCts.IsCancellationRequested)
                {
                    var old = _settingsCts;
                    _settingsCts = new CancellationTokenSource();
                    old.Dispose();
                }
            }
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            _settingsService.FetchOnlineLyricsEnabledChanged -= OnFetchOnlineLyricsEnabledChanged;

            lock (_ctsLock)
            {
                _settingsCts.Cancel();
                _settingsCts.Dispose();
                _disposed = true;
            }
        }
        else
        {
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
