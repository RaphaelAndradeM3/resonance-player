using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Resonance.Core.Models;
using Resonance.Core.Models.Lyrics;
using Resonance.Core.Services.Abstractions;
using Resonance.WinUI.Helpers;
using Resonance.WinUI.Services.Abstractions;

namespace Resonance.WinUI.ViewModels;

/// <summary>
///     Manages the state and logic for the LyricsPage. It interfaces with playback
///     services to get song information and lyrics, and provides properties for data
///     binding to the view.
/// </summary>
public partial class LyricsPageViewModel : ObservableObject, IDisposable
{
    private static readonly TimeSpan OptimisticUpdateGracePeriod = TimeSpan.FromSeconds(2);
    private readonly IDispatcherService _dispatcherService;
    private readonly ILibraryReader _libraryReader;
    private readonly ILyricRomanizationService _lyricRomanizationService;
    private readonly ILogger<LyricsPageViewModel> _logger;
    private readonly ILrcService _lrcService;
    private readonly IMusicPlaybackService _playbackService;
    private readonly IRomanizationPackManager _romanizationPackManager;
    private readonly ISettingsService _settingsService;
    private static readonly TimeSpan _seekTimeOffset = TimeSpan.FromSeconds(0.2);
    private readonly object _lyricsFetchLock = new();

    private CancellationTokenSource? _lyricsFetchCts;
    private bool _isDisposed;
    private int _lrcSearchHint;

    private LyricLine? _optimisticallySetLine;
    private DateTime _optimisticSetTimestamp;
    private ParsedLrc? _parsedLrc;
    private Song? _currentSong;

    public LyricsPageViewModel(
        IMusicPlaybackService playbackService,
        IDispatcherService dispatcherService,
        ILrcService lrcService,
        ILibraryReader libraryReader,
        ILyricRomanizationService lyricRomanizationService,
        ISettingsService settingsService,
        IRomanizationPackManager romanizationPackManager,
        ILogger<LyricsPageViewModel> logger)
    {
        _playbackService = playbackService;
        _dispatcherService = dispatcherService;
        _lrcService = lrcService;
        _libraryReader = libraryReader;
        _lyricRomanizationService = lyricRomanizationService;
        _settingsService = settingsService;
        _romanizationPackManager = romanizationPackManager;
        _logger = logger;

        _playbackService.TrackChanged += OnPlaybackServiceTrackChanged;
        _playbackService.PositionChanged += OnPlaybackServicePositionChanged;
        _playbackService.DurationChanged += OnPlaybackServiceDurationChanged;
        _playbackService.PlaybackStateChanged += OnPlaybackServicePlaybackStateChanged;
        _settingsService.LyricsRomanizationEnabledChanged += OnLyricsRomanizationEnabledChanged;
        _romanizationPackManager.PacksChanged += OnRomanizationPacksChanged;

        _ = UpdateForTrack(_playbackService.CurrentTrack);
        IsPlaying = _playbackService.IsPlaying;
    }

    [ObservableProperty] public partial string SongTitle { get; set; } = Resonance.WinUI.Resources.Strings.Lyrics_NoSongSelected;

    /// <summary>
    ///     True when synchronized (timed) lyrics are available and being displayed.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNoLyricsMessage))]
    [NotifyPropertyChangedFor(nameof(ShowTimedLyrics))]
    public partial bool HasLyrics { get; set; }

    /// <summary>
    ///     True when only unsynchronized (plain text) lyrics are available.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNoLyricsMessage))]
    [NotifyPropertyChangedFor(nameof(ShowTimedLyrics))]
    public partial bool HasUnsyncedLyrics { get; set; }

    [ObservableProperty] public partial LyricLine? CurrentLine { get; set; }

    [ObservableProperty] public partial TimeSpan SongDuration { get; set; }

    [ObservableProperty] public partial TimeSpan CurrentPosition { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNoLyricsMessage))]
    public partial bool IsPlaying { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNoLyricsMessage))]
    public partial bool IsLoading { get; set; }

    [ObservableProperty] public partial bool CanEditLyrics { get; set; }
    [ObservableProperty] public partial bool CanRemoveLyrics { get; set; }
    [ObservableProperty] public partial string EditableLyrics { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProvenanceLabel))]
    public partial string ProvenanceLabel { get; set; } = string.Empty;

    public bool HasProvenanceLabel => !string.IsNullOrWhiteSpace(ProvenanceLabel);

    [ObservableProperty] public partial bool IsSynced { get; set; }
    [ObservableProperty] public partial bool IsPlain { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNoLyricsMessage))]
    public partial bool IsInstrumental { get; set; }

    [ObservableProperty] public partial int CurrentOffsetMs { get; set; }
    [ObservableProperty] public partial string FormattedOffset { get; set; } = "Offset: 0 ms";
    [ObservableProperty] public partial bool CanExportLrc { get; set; }

    public Guid? CurrentSongId => _currentSong?.Id;

    /// <summary>
    ///     True when the timed lyrics ListView should be displayed.
    /// </summary>
    public bool ShowTimedLyrics => HasLyrics;

    /// <summary>
    ///     True when the "No lyrics found" message should be displayed.
    ///     This is only when we are not loading, not instrumental, and have no lyrics of either type.
    /// </summary>
    public bool ShowNoLyricsMessage => !HasLyrics && !HasUnsyncedLyrics && !IsInstrumental && !IsLoading;

    public ObservableRangeCollection<LyricLine> LyricLines { get; } = new();

    /// <summary>
    ///     Collection of unsynchronized lyric lines for display in a ListView-like format.
    /// </summary>
    public ObservableRangeCollection<LyricLine> UnsyncedLyricLines { get; } = new();

    public void Dispose()
    {
        if (_isDisposed) return;

        _playbackService.TrackChanged -= OnPlaybackServiceTrackChanged;
        _playbackService.PositionChanged -= OnPlaybackServicePositionChanged;
        _playbackService.DurationChanged -= OnPlaybackServiceDurationChanged;
        _playbackService.PlaybackStateChanged -= OnPlaybackServicePlaybackStateChanged;
        _settingsService.LyricsRomanizationEnabledChanged -= OnLyricsRomanizationEnabledChanged;
        _romanizationPackManager.PacksChanged -= OnRomanizationPacksChanged;

        // Cancel any pending lyrics fetch operation
        lock (_lyricsFetchLock)
        {
            _lyricsFetchCts?.Cancel();
            _lyricsFetchCts?.Dispose();
            _lyricsFetchCts = null;
        }

        _isDisposed = true;
        GC.SuppressFinalize(this);
    }

    [RelayCommand]
    public async Task SeekToLineAsync(LyricLine? line)
    {
        if (line is null || !IsSynced) return;

        var targetTime = line.StartTime - TimeSpan.FromMilliseconds(CurrentOffsetMs) - _seekTimeOffset;
        if (targetTime < TimeSpan.Zero) targetTime = TimeSpan.Zero;

        _logger.LogDebug("Seeking lyrics to line with start time {StartTime} (offset: {Offset}ms)", line.StartTime, CurrentOffsetMs);
        _optimisticallySetLine = line;
        _optimisticSetTimestamp = DateTime.UtcNow;
        UpdateCurrentLineFromPosition(targetTime);

        await _playbackService.SeekAsync(targetTime);
    }

    [RelayCommand]
    public async Task AdjustOffsetAsync(object? parameter)
    {
        var delta = 0;
        if (parameter is int intVal) delta = intVal;
        else if (parameter is string strVal && int.TryParse(strVal, out var parsed)) delta = parsed;

        if (delta == 0 || _currentSong == null) return;

        CurrentOffsetMs += delta;
        FormattedOffset = CurrentOffsetMs >= 0 ? $"Offset: +{CurrentOffsetMs} ms" : $"Offset: {CurrentOffsetMs} ms";

        await _lrcService.SetLyricsOffsetAsync(_currentSong, CurrentOffsetMs).ConfigureAwait(false);
        UpdateCurrentLineFromPosition(_playbackService.CurrentPosition);
    }

    [RelayCommand]
    public async Task ResetOffsetAsync()
    {
        if (_currentSong == null || CurrentOffsetMs == 0) return;

        CurrentOffsetMs = 0;
        FormattedOffset = "Offset: 0 ms";

        await _lrcService.SetLyricsOffsetAsync(_currentSong, 0).ConfigureAwait(false);
        UpdateCurrentLineFromPosition(_playbackService.CurrentPosition);
    }

    [RelayCommand]
    public async Task ExportSidecarLrcAsync()
    {
        if (_currentSong == null || string.IsNullOrWhiteSpace(_currentSong.FilePath)) return;

        string? contentToExport = null;
        if (_parsedLrc != null && !_parsedLrc.IsEmpty)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var line in _parsedLrc.Lines)
            {
                sb.AppendLine($"[{line.StartTime:mm\\:ss\\.ff}]{line.Text}");
            }
            contentToExport = sb.ToString();
        }
        else if (!string.IsNullOrWhiteSpace(EditableLyrics))
        {
            contentToExport = EditableLyrics;
        }

        if (string.IsNullOrWhiteSpace(contentToExport)) return;

        var success = await _lrcService.ExportSidecarLrcAsync(_currentSong, contentToExport).ConfigureAwait(false);
        if (success)
        {
            _dispatcherService.TryEnqueue(() =>
            {
                CanExportLrc = false;
                ProvenanceLabel = "Arquivo .lrc (Exportado)";
            });
        }
    }

    public async Task SaveLyricsAsync(Guid songId, string lyrics)
    {
        var song = _currentSong?.Id == songId
            ? _currentSong
            : await _libraryReader.GetSongWithFullDataAsync(songId).ConfigureAwait(false);
        if (song is null || string.IsNullOrWhiteSpace(lyrics)) return;

        await _lrcService.SaveLyricsAsync(song, lyrics).ConfigureAwait(false);
        if (_currentSong?.Id == song.Id) await UpdateForTrack(song).ConfigureAwait(false);
    }

    public async Task RemoveLyricsAsync(Guid songId)
    {
        var song = _currentSong?.Id == songId
            ? _currentSong
            : await _libraryReader.GetSongWithFullDataAsync(songId).ConfigureAwait(false);
        if (song is null || !await _lrcService.RemoveCachedLyricsAsync(song).ConfigureAwait(false)) return;

        if (_currentSong?.Id == song.Id) await UpdateForTrack(song).ConfigureAwait(false);
    }

    private void OnPlaybackServicePositionChanged()
    {
        UpdateCurrentLineFromPosition(_playbackService.CurrentPosition);
    }

    private void UpdateCurrentLineFromPosition(TimeSpan position)
    {
        if (_parsedLrc is null || !HasLyrics || !IsSynced) return;

        var effectivePosition = position + TimeSpan.FromMilliseconds(CurrentOffsetMs);
        if (effectivePosition < TimeSpan.Zero) effectivePosition = TimeSpan.Zero;

        _dispatcherService.TryEnqueue(() =>
        {
            if (_isDisposed || _parsedLrc is null) return;
            CurrentPosition = position;
            var newCurrentLine = _lrcService.GetCurrentLine(_parsedLrc, effectivePosition, ref _lrcSearchHint);

            if (_optimisticallySetLine != null)
            {
                if (DateTime.UtcNow - _optimisticSetTimestamp < OptimisticUpdateGracePeriod)
                {
                    // If the "natural" current line is already the one we optimistically set,
                    // we can clear the optimistic flag early as the seek has successfully caught up.
                    if (ReferenceEquals(newCurrentLine, _optimisticallySetLine))
                    {
                        _optimisticallySetLine = null;
                    }
                    else
                    {
                        newCurrentLine = _optimisticallySetLine;
                    }
                }
                else
                {
                    _optimisticallySetLine = null;
                }
            }

            if (!ReferenceEquals(newCurrentLine, CurrentLine))
            {
                CurrentLine = newCurrentLine;
            }
        });
    }

    private async Task UpdateForTrack(Song? song)
    {
        _currentSong = song;

        // Cancel any previous lyrics fetch operation to prevent stale data when skipping songs
        CancellationToken cancellationToken;
        lock (_lyricsFetchLock)
        {
            _lyricsFetchCts?.Cancel();
            _lyricsFetchCts?.Dispose();
            _lyricsFetchCts = new CancellationTokenSource();
            cancellationToken = _lyricsFetchCts.Token;
        }

        // Reset UI state immediately on dispatcher
        _dispatcherService.TryEnqueue(() =>
        {
            if (_isDisposed) return;
            LyricLines.Clear();
            UnsyncedLyricLines.Clear();
            CurrentLine = null;
            _parsedLrc = null;
            _lrcSearchHint = 0;
            _optimisticallySetLine = null;
            SongDuration = TimeSpan.Zero;
            CurrentPosition = TimeSpan.Zero;
            HasLyrics = false;
            HasUnsyncedLyrics = false;
            IsSynced = false;
            IsPlain = false;
            IsInstrumental = false;
            ProvenanceLabel = string.Empty;
            CanExportLrc = false;
            CanEditLyrics = false;
            CanRemoveLyrics = song is not null && _lrcService.HasCachedLyrics(song);
            EditableLyrics = string.Empty;

            if (song is null)
            {
                _logger.LogDebug("Clearing lyrics view as playback stopped");
                SongTitle = Resonance.WinUI.Resources.Strings.Lyrics_NoSongSelected;
                IsLoading = false;
                CurrentOffsetMs = 0;
                FormattedOffset = "Offset: 0 ms";
            }
            else
            {
                SongTitle = !string.IsNullOrWhiteSpace(song.ArtistName)
                    ? string.Format(Resonance.WinUI.Resources.Strings.Lyrics_SongTitleFormat, song.Title, song.ArtistName)
                    : song.Title;
                SongDuration = _playbackService.Duration;
                IsLoading = true;
                CurrentOffsetMs = song.LyricsOffsetMs ?? 0;
                FormattedOffset = CurrentOffsetMs >= 0 ? $"Offset: +{CurrentOffsetMs} ms" : $"Offset: {CurrentOffsetMs} ms";
            }
        });

        if (song is null) return;

        _logger.LogDebug("Updating lyrics for track '{SongTitle}' ({SongId})", song.Title, song.Id);

        // Start prefetch for next track immediately (runs in parallel with current fetch)
        _ = PrefetchNextTrackLyrics();

        try
        {
            var fullSong = await _libraryReader.GetSongWithFullDataAsync(song.Id).ConfigureAwait(false) ?? song;
            if (cancellationToken.IsCancellationRequested) return;

            CurrentOffsetMs = fullSong.LyricsOffsetMs ?? 0;
            _dispatcherService.TryEnqueue(() =>
            {
                if (cancellationToken.IsCancellationRequested || _isDisposed) return;
                FormattedOffset = CurrentOffsetMs >= 0 ? $"Offset: +{CurrentOffsetMs} ms" : $"Offset: {CurrentOffsetMs} ms";
            });

            var doc = await _lrcService.ResolveLyricsAsync(fullSong, cancellationToken).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested) return;

            if (doc is null || doc.IsEmpty || doc.Type == LyricsType.None)
            {
                _logger.LogDebug("No lyrics found for track '{SongTitle}'", fullSong.Title);
                _dispatcherService.TryEnqueue(() =>
                {
                    if (cancellationToken.IsCancellationRequested || _isDisposed) return;
                    CanEditLyrics = true;
                    IsLoading = false;
                });
                return;
            }

            var provenanceStr = GetProvenanceLabel(doc.Provenance);

            if (doc.Type == LyricsType.Instrumental || doc.IsInstrumental)
            {
                _logger.LogDebug("Track '{SongTitle}' is instrumental", fullSong.Title);
                _dispatcherService.TryEnqueue(() =>
                {
                    if (cancellationToken.IsCancellationRequested || _isDisposed) return;
                    IsInstrumental = true;
                    ProvenanceLabel = !string.IsNullOrEmpty(provenanceStr) ? provenanceStr : "Instrumental";
                    IsLoading = false;
                    CanEditLyrics = true;
                });
                return;
            }

            if (doc.Type == LyricsType.Synced && doc.Lines.Count > 0)
            {
                _logger.LogDebug("Resolved synced lyrics ({Provenance}) for track '{SongTitle}'", doc.Provenance, fullSong.Title);
                var parsed = new ParsedLrc(doc.Lines, doc.RawUnsyncedLyrics);
                var displayLrc = await ApplyRomanizationAsync(parsed, cancellationToken).ConfigureAwait(false);

                var sb = new System.Text.StringBuilder();
                foreach (var line in doc.Lines)
                {
                    sb.AppendLine($"[{line.StartTime:mm\\:ss\\.ff}]{line.Text}");
                }
                var rawEditable = sb.ToString();

                _dispatcherService.TryEnqueue(() =>
                {
                    if (cancellationToken.IsCancellationRequested || _isDisposed) return;
                    _parsedLrc = displayLrc;
                    EditableLyrics = rawEditable;
                    CanEditLyrics = true;
                    CanRemoveLyrics = _lrcService.HasCachedLyrics(fullSong);
                    CanExportLrc = doc.Provenance != LyricsProvenance.LocalFileLrc;
                    ProvenanceLabel = provenanceStr;
                    IsSynced = true;
                    LyricLines.AddRange(displayLrc.Lines);
                    HasLyrics = true;
                    IsLoading = false;
                    UpdateCurrentLineFromPosition(_playbackService.CurrentPosition);
                });
                return;
            }

            if (doc.Type == LyricsType.Plain && !string.IsNullOrWhiteSpace(doc.RawUnsyncedLyrics))
            {
                _logger.LogDebug("Resolved plain lyrics ({Provenance}) for track '{SongTitle}'", doc.Provenance, fullSong.Title);
                var plainLines = await ApplyRomanizationAsync(
                    ParseUnsyncedLyricsToLines(doc.RawUnsyncedLyrics),
                    cancellationToken).ConfigureAwait(false);
                _dispatcherService.TryEnqueue(() =>
                {
                    if (cancellationToken.IsCancellationRequested || _isDisposed) return;
                    UnsyncedLyricLines.AddRange(plainLines);
                    EditableLyrics = doc.RawUnsyncedLyrics;
                    CanEditLyrics = true;
                    CanRemoveLyrics = _lrcService.HasCachedLyrics(fullSong);
                    CanExportLrc = false;
                    ProvenanceLabel = provenanceStr;
                    IsPlain = true;
                    HasUnsyncedLyrics = true;
                    IsLoading = false;
                });
                return;
            }

            _logger.LogDebug("No lyrics found for track '{SongTitle}'", fullSong.Title);
            _dispatcherService.TryEnqueue(() =>
            {
                if (cancellationToken.IsCancellationRequested || _isDisposed) return;
                CanEditLyrics = true;
                IsLoading = false;
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Lyrics fetch for '{SongTitle}' was cancelled", song.Title);
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch lyrics for track '{SongTitle}'", song.Title);
            _dispatcherService.TryEnqueue(() =>
            {
                CanEditLyrics = true;
                IsLoading = false;
            });
        }
    }

    private static string GetProvenanceLabel(LyricsProvenance provenance) => provenance switch
    {
        LyricsProvenance.LocalFileLrc => "Arquivo .lrc",
        LyricsProvenance.LocalFileTxt => "Arquivo .txt",
        LyricsProvenance.EmbeddedSynced => "Tag Embutida",
        LyricsProvenance.EmbeddedPlain => "Tag Embutida",
        LyricsProvenance.LocalCache => "Cache Local",
        LyricsProvenance.RemoteLrcLib => "LRCLIB",
        LyricsProvenance.RemoteNetEase => "NetEase",
        _ => string.Empty
    };

    private async Task<ParsedLrc> ApplyRomanizationAsync(ParsedLrc parsedLrc, CancellationToken cancellationToken)
    {
        var lines = await _lyricRomanizationService.ApplyRomanizationAsync(parsedLrc.Lines, cancellationToken).ConfigureAwait(false);
        return new ParsedLrc(lines, parsedLrc.RawUnsyncedLyrics);
    }

    private Task<IReadOnlyList<LyricLine>> ApplyRomanizationAsync(IEnumerable<string> lines, CancellationToken cancellationToken)
    {
        return _lyricRomanizationService.ApplyRomanizationAsync(lines, cancellationToken);
    }

    /// <summary>
    ///     Parses unsynchronized lyrics text into individual lines for display,
    ///     normalizing line breaks and preserving verse structure with blank lines.
    /// </summary>
    private static List<string> ParseUnsyncedLyricsToLines(string rawLyrics)
    {
        // Strip language prefixes like "eng||" or "jpn||" at the start of the text
        rawLyrics = LanguagePrefixRegex().Replace(rawLyrics, string.Empty);

        // Normalize different line ending styles to \n
        var normalized = rawLyrics
            .Replace("\r\n", "\n")
            .Replace("\r", "\n");

        // Collapse multiple consecutive blank lines into double line breaks
        // This preserves verse separation while cleaning up excessive spacing
        normalized = MultipleNewlines().Replace(normalized, "\n\n");

        // Split into lines and trim each line
        return normalized
            .Split('\n')
            .Select(line => line.Trim())
            .ToList();
    }

    private void OnPlaybackServiceTrackChanged()
    {
        _ = UpdateForTrack(_playbackService.CurrentTrack);
    }

    private void OnLyricsRomanizationEnabledChanged(bool isEnabled)
    {
        _ = UpdateForTrack(_playbackService.CurrentTrack);
    }

    private void OnRomanizationPacksChanged()
    {
        _ = UpdateForTrack(_playbackService.CurrentTrack);
    }

    private void OnPlaybackServiceDurationChanged()
    {
        _dispatcherService.TryEnqueue(() =>
        {
            if (_isDisposed) return;
            SongDuration = _playbackService.Duration;
        });
    }

    private void OnPlaybackServicePlaybackStateChanged()
    {
        _dispatcherService.TryEnqueue(() =>
        {
            if (_isDisposed) return;
            IsPlaying = _playbackService.IsPlaying;
        });
    }

    /// <summary>
    ///     Pre-fetches lyrics for the next track in the queue to reduce perceived latency.
    ///     This is fire-and-forget - failures are silently logged and don't affect current playback.
    /// </summary>
    private async Task PrefetchNextTrackLyrics()
    {
        CancellationTokenSource? prefetchCts = null;
        try
        {
            var nextSong = await GetNextSongInQueueAsync().ConfigureAwait(false);
            if (nextSong is null) return;

            // Skip if already network-checked
            if (nextSong.LyricsLastCheckedUtc != null) return;

            // Skip if a local .lrc sidecar exists — it loads from disk when the track plays
            if (!string.IsNullOrWhiteSpace(nextSong.LrcFilePath)) return;

            // Skip if embedded lyrics exist — they load from the DB when the track plays
            var fullNextSong = await _libraryReader.GetSongWithFullDataAsync(nextSong.Id).ConfigureAwait(false);
            if (fullNextSong is not null && !string.IsNullOrWhiteSpace(fullNextSong.Lyrics)) return;

            // No local lyrics — worth prefetching from network.
            // Wait 1 second to reduce peak concurrent HTTP request count.
            CancellationToken earlyCancelToken;
            lock (_lyricsFetchLock)
            {
                if (_lyricsFetchCts == null || _isDisposed) return;
                earlyCancelToken = _lyricsFetchCts.Token;
            }
            await Task.Delay(TimeSpan.FromSeconds(1), earlyCancelToken).ConfigureAwait(false);

            lock (_lyricsFetchLock)
            {
                if (_lyricsFetchCts == null || _isDisposed) return;
                prefetchCts = CancellationTokenSource.CreateLinkedTokenSource(_lyricsFetchCts.Token);
            }

            if (prefetchCts.Token.IsCancellationRequested) return;
            prefetchCts.CancelAfter(TimeSpan.FromSeconds(15));

            await _lrcService.GetLyricsAsync(nextSong, prefetchCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected when the pre-fetch times out or is cancelled
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Pre-fetch failed for next track (non-critical)");
        }
        finally
        {
            prefetchCts?.Dispose();
        }
    }

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex MultipleNewlines();

    [GeneratedRegex(@"^[a-z]{2,3}\|\|", RegexOptions.IgnoreCase)]
    private static partial Regex LanguagePrefixRegex();

    private async Task<Song?> GetNextSongInQueueAsync()
    {
        Guid nextId = Guid.Empty;
        if (_playbackService.IsShuffleEnabled)
        {
            var nextIndex = _playbackService.CurrentShuffledIndex + 1;
            if (nextIndex < _playbackService.ShuffledQueue.Count)
                nextId = _playbackService.ShuffledQueue[nextIndex];
        }
        else
        {
            var nextQueueIndex = _playbackService.CurrentQueueIndex + 1;
            if (nextQueueIndex < _playbackService.PlaybackQueue.Count)
                nextId = _playbackService.PlaybackQueue[nextQueueIndex];
        }

        if (nextId == Guid.Empty) return null;

        // Fetch basic metadata for the next song (single lookup is more efficient than dictionary)
        return await _libraryReader.GetSongByIdAsync(nextId).ConfigureAwait(false);
    }
}
