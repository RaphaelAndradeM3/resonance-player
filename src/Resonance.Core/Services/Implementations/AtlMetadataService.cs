using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ATL;
using Microsoft.Extensions.Logging;
using Resonance.Core.Constants;
using Resonance.Core.Helpers;
using Resonance.Core.Models;
using Resonance.Core.Services.Abstractions;

namespace Resonance.Core.Services.Implementations;

/// <summary>
///     Extracts music file metadata using the ATL.NET library.
/// </summary>
public class AtlMetadataService : IMetadataService, IDisposable
{
    private const char AtlMultiValueSeparator = '\u001F';
    private const char CombinedValueSeparator = ';';
    private readonly IFileSystemService _fileSystem;
    private readonly IImageProcessor _imageProcessor;
    private readonly ILogger<AtlMetadataService> _logger;
    private readonly IPathConfiguration _pathConfig;
    private readonly ISettingsService _settingsService;

    // Cache for artist split characters to avoid repeated async calls during batch scanning
    private readonly object _splitCharactersLock = new();
    private string? _cachedSplitCharacters;
    private readonly object _genreSplitCharactersLock = new();
    private string? _cachedGenreSplitCharacters;
    private bool _disposed;

    static AtlMetadataService()
    {
        ATL.Settings.DisplayValueSeparator = AtlMultiValueSeparator;
    }

    public AtlMetadataService(IImageProcessor imageProcessor, IFileSystemService fileSystem,
        IPathConfiguration pathConfig, ILogger<AtlMetadataService> logger, ISettingsService settingsService)
    {
        _imageProcessor = imageProcessor ?? throw new ArgumentNullException(nameof(imageProcessor));
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _pathConfig = pathConfig ?? throw new ArgumentNullException(nameof(pathConfig));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

        // Subscribe to settings changes to invalidate cache
        _settingsService.ArtistSplitCharactersChanged += OnArtistSplitCharactersChanged;
        _settingsService.GenreSplitCharactersChanged += OnGenreSplitCharactersChanged;
    }

    /// <inheritdoc />
    public async Task<SongFileMetadata> ExtractMetadataAsync(string filePath, string? baseFolderPath = null,
        bool includeMediaAssets = true)
    {
        var metadata = new SongFileMetadata { FilePath = filePath };

        try
        {
            var fileInfo = _fileSystem.GetFileInfo(filePath);
            metadata.FileCreatedDate = fileInfo.CreationTimeUtc;
            metadata.FileModifiedDate = fileInfo.LastWriteTimeUtc;
            metadata.Title = ArtistNameHelper.NormalizeStringCore(_fileSystem.GetFileNameWithoutExtension(filePath)) ?? _fileSystem.GetFileNameWithoutExtension(filePath);

            if (fileInfo.Exists && fileInfo.Length == 0)
            {
                metadata.ExtractionFailed = true;
                metadata.ErrorMessage = "EmptyFile";
                return metadata;
            }

            // ATL parsing is synchronous, so enforce the timeout while awaiting it.
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var track = await Task.Run(() => new Track(filePath)).WaitAsync(cts.Token).ConfigureAwait(false);

            // Check if the file is valid - ATL is lenient so we need multiple checks
            // Check 1: AudioFormat.Readable flag
            // Check 2: Format name is "Unknown" (indicates ATL couldn't identify the format)
            // Check 3: Duration is 0 and no audio data detected
            var isUnknownFormat = track.AudioFormat.Name?.Equals("Unknown", StringComparison.OrdinalIgnoreCase) == true ||
                                  track.AudioFormat.ID == -1;

            if (!track.AudioFormat.Readable || isUnknownFormat)
            {
                metadata.ExtractionFailed = true;
                metadata.ErrorMessage = isUnknownFormat ? "UnsupportedFormat" : "CorruptFile";
                return metadata;
            }

            var splitCharacters = await GetCachedSplitCharactersAsync().ConfigureAwait(false);
            var genreSplitCharacters = await GetCachedGenreSplitCharactersAsync().ConfigureAwait(false);
            PopulateMetadataFromTrack(metadata, track, splitCharacters, genreSplitCharacters);

            if (includeMediaAssets)
            {
                // Get cached or extract new synchronized lyrics.
                using var lrcCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                try
                {
                    metadata.LrcFilePath = await GetLrcPathAsync(filePath, fileInfo.LastWriteTimeUtc,
                            metadata.Artists?.FirstOrDefault() ?? Artist.UnknownArtistName, metadata.Album,
                            metadata.Title, track)
                        .WaitAsync(lrcCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("LRC extraction timed out for file: {FilePath}", filePath);
                }

                if (string.IsNullOrWhiteSpace(metadata.LrcFilePath))
                    metadata.LrcFilePath = FindLrcFilePath(filePath);

                using var albumArtCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                try
                {
                    await ProcessAlbumArtAsync(metadata, track, baseFolderPath).WaitAsync(albumArtCts.Token)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Album art extraction timed out for file: {FilePath}", filePath);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Metadata extraction timed out for file: {FilePath}", filePath);
            metadata.ExtractionFailed = true;
            metadata.ErrorMessage = "ExtractionTimeout";
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Metadata extraction timed out for file: {FilePath}", filePath);
            metadata.ExtractionFailed = true;
            metadata.ErrorMessage = "ExtractionTimeout";
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "File access error during metadata extraction for '{FilePath}'.", filePath);
            metadata.ExtractionFailed = true;
            metadata.ErrorMessage = "FileAccessError";
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied during metadata extraction for '{FilePath}'.", filePath);
            metadata.ExtractionFailed = true;
            metadata.ErrorMessage = "FileAccessError";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred during metadata extraction for '{FilePath}'.",
                filePath);
            metadata.ExtractionFailed = true;
            metadata.ErrorMessage = "CorruptFile";
        }

        return metadata;
    }

    /// <inheritdoc />
    public async Task<TrackInspectorViewData> GetTrackInspectorViewDataAsync(Song song, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(song);

        TrackInspectorViewData viewData;
        if (!string.IsNullOrWhiteSpace(song.FilePath) && _fileSystem.FileExists(song.FilePath))
        {
            viewData = await GetTrackInspectorViewDataAsync(song.FilePath, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            viewData = CreateInaccessibleViewDataFromSong(song);
        }

        if (viewData.Technical.IsAccessible)
        {
            viewData.ProvenanceLabel = "Biblioteca Local & Arquivo";
        }

        if (string.IsNullOrEmpty(viewData.Artwork.CoverArtUri) && !string.IsNullOrEmpty(song.AlbumArtUriFromTrack))
        {
            viewData.Artwork.CoverArtUri = song.AlbumArtUriFromTrack;
            viewData.Artwork.Source = ArtworkSource.RemoteCache;
        }
        else if (string.IsNullOrEmpty(viewData.Artwork.CoverArtUri) && !string.IsNullOrEmpty(song.Album?.CoverArtUri))
        {
            viewData.Artwork.CoverArtUri = song.Album.CoverArtUri;
            viewData.Artwork.Source = ArtworkSource.RemoteCache;
        }

        if (string.IsNullOrEmpty(viewData.ExternalIds.AcoustId) && !string.IsNullOrEmpty(song.AcoustId))
        {
            viewData.ExternalIds.AcoustId = song.AcoustId;
        }
        if (string.IsNullOrEmpty(viewData.ExternalIds.MusicBrainzTrackId) && !string.IsNullOrEmpty(song.MusicBrainzTrackId))
        {
            viewData.ExternalIds.MusicBrainzTrackId = song.MusicBrainzTrackId;
        }
        if (string.IsNullOrEmpty(viewData.ExternalIds.MusicBrainzReleaseId) && !string.IsNullOrEmpty(song.MusicBrainzReleaseId))
        {
            viewData.ExternalIds.MusicBrainzReleaseId = song.MusicBrainzReleaseId;
        }

        return viewData;
    }

    /// <inheritdoc />
    public async Task<TrackInspectorViewData> GetTrackInspectorViewDataAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

        FileInfo? fileInfo = null;
        try
        {
            fileInfo = _fileSystem.GetFileInfo(filePath);
            if (fileInfo == null || !fileInfo.Exists)
            {
                return new TrackInspectorViewData
                {
                    Technical = new TrackTechnicalDetails
                    {
                        FilePath = filePath,
                        IsAccessible = false
                    },
                    Tags = new TrackTagDetails
                    {
                        Title = _fileSystem.GetFileNameWithoutExtension(filePath)
                    },
                    ProvenanceLabel = "Arquivo Inacessível"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Removable media or file offline/inaccessible: '{FilePath}'.", filePath);
            return new TrackInspectorViewData
            {
                Technical = new TrackTechnicalDetails
                {
                    FilePath = filePath,
                    IsAccessible = false
                },
                Tags = new TrackTagDetails
                {
                    Title = _fileSystem.GetFileNameWithoutExtension(filePath)
                },
                ProvenanceLabel = "Arquivo Inacessível"
            };
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(30));

        Track track;
        try
        {
            track = await Task.Run(() => new Track(filePath), cts.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read track metadata for inspector: '{FilePath}'.", filePath);
            return new TrackInspectorViewData
            {
                Technical = new TrackTechnicalDetails
                {
                    FilePath = filePath,
                    FileSizeBytes = fileInfo?.Length ?? 0,
                    FileSizeFormatted = fileInfo != null ? FormatFileSize(fileInfo.Length) : "0 B",
                    FileCreatedDate = fileInfo?.CreationTimeUtc,
                    FileModifiedDate = fileInfo?.LastWriteTimeUtc,
                    IsAccessible = false
                },
                Tags = new TrackTagDetails
                {
                    Title = _fileSystem.GetFileNameWithoutExtension(filePath)
                },
                ProvenanceLabel = "Erro de Leitura"
            };
        }

        var splitCharacters = await GetCachedSplitCharactersAsync().ConfigureAwait(false);
        var genreSplitCharacters = await GetCachedGenreSplitCharactersAsync().ConfigureAwait(false);

        var technical = ExtractTechnicalDetails(filePath, fileInfo!, track);
        var tags = ExtractTagDetails(filePath, track, splitCharacters, genreSplitCharacters);
        var artwork = await ExtractArtworkDetailsAsync(filePath, track, cts.Token).ConfigureAwait(false);
        var externalIds = ExtractExternalIds(track);

        return new TrackInspectorViewData
        {
            Technical = technical,
            Tags = tags,
            Artwork = artwork,
            ExternalIds = externalIds,
            ProvenanceLabel = "Arquivo Local"
        };
    }

    private TrackInspectorViewData CreateInaccessibleViewDataFromSong(Song song)
    {
        var artists = song.SongArtists?.Select(sa => sa.Artist?.Name).Where(n => !string.IsNullOrEmpty(n)).Select(n => n!).ToList() ?? [];
        if (artists.Count == 0 && !string.IsNullOrWhiteSpace(song.Composer))
        {
            artists.Add(song.Composer);
        }

        var ext = _fileSystem.GetExtension(song.FilePath);
        var container = AudioFormatRegistry.GetDisplayName(ext);

        return new TrackInspectorViewData
        {
            Technical = new TrackTechnicalDetails
            {
                FilePath = song.FilePath ?? string.Empty,
                Duration = song.Duration,
                ContainerFormat = container,
                AudioCodec = ext.TrimStart('.').ToUpperInvariant(),
                BitrateKbps = song.Bitrate,
                BitrateMode = "Desconhecido",
                SampleRateHz = song.SampleRate,
                Channels = song.Channels,
                ChannelsDescription = GetChannelsDescription(song.Channels),
                FileCreatedDate = song.FileCreatedDate,
                FileModifiedDate = song.FileModifiedDate,
                IsAccessible = false
            },
            Tags = new TrackTagDetails
            {
                Title = !string.IsNullOrWhiteSpace(song.Title) ? song.Title : _fileSystem.GetFileNameWithoutExtension(song.FilePath ?? string.Empty),
                Artists = artists,
                Album = song.Album?.Title,
                TrackNumber = song.TrackNumber,
                TrackCount = song.TrackCount,
                DiscNumber = song.DiscNumber,
                DiscCount = song.DiscCount,
                Year = song.Year,
                Composer = song.Composer,
                Conductor = song.Conductor,
                Grouping = song.Grouping,
                Copyright = song.Copyright,
                Comment = song.Comment,
                Bpm = song.Bpm,
                ReplayGainTrackGain = song.ReplayGainTrackGain,
                ReplayGainTrackPeak = song.ReplayGainTrackPeak,
                HasLyrics = !string.IsNullOrWhiteSpace(song.Lyrics),
                HasSynchronizedLyrics = !string.IsNullOrWhiteSpace(song.LrcFilePath),
                LyricsPreview = !string.IsNullOrWhiteSpace(song.Lyrics) ? GetLyricsPreview(song.Lyrics) : null
            },
            Artwork = new TrackArtworkDetails
            {
                CoverArtUri = song.AlbumArtUriFromTrack ?? song.Album?.CoverArtUri,
                Source = !string.IsNullOrEmpty(song.AlbumArtUriFromTrack ?? song.Album?.CoverArtUri)
                    ? ArtworkSource.RemoteCache
                    : ArtworkSource.None
            },
            ExternalIds = new TrackExternalIds
            {
                AcoustId = song.AcoustId,
                MusicBrainzTrackId = song.MusicBrainzTrackId,
                MusicBrainzReleaseId = song.MusicBrainzReleaseId
            },
            ProvenanceLabel = "Biblioteca Local (Arquivo Inacessível)"
        };
    }

    private TrackTechnicalDetails ExtractTechnicalDetails(string filePath, FileInfo fileInfo, Track track)
    {
        var ext = _fileSystem.GetExtension(filePath);
        var container = !string.IsNullOrWhiteSpace(track.AudioFormat?.Name) && !track.AudioFormat.Name.Equals("Unknown", StringComparison.OrdinalIgnoreCase)
            ? track.AudioFormat.Name
            : AudioFormatRegistry.GetDisplayName(ext);

        var codec = !string.IsNullOrWhiteSpace(track.AudioFormat?.ShortName)
            ? track.AudioFormat.ShortName
            : (!string.IsNullOrWhiteSpace(track.AudioFormat?.Name) ? track.AudioFormat.Name : ext.TrimStart('.').ToUpperInvariant());

        var isLossless = AudioFormatRegistry.IsLossless(ext);
        int? bitDepth = (isLossless && track.BitDepth > 0) ? track.BitDepth : null;

        return new TrackTechnicalDetails
        {
            FilePath = filePath,
            FileSizeBytes = fileInfo.Length,
            FileSizeFormatted = FormatFileSize(fileInfo.Length),
            Duration = TimeSpan.FromSeconds(track.Duration),
            ContainerFormat = container,
            AudioCodec = codec,
            BitrateKbps = track.Bitrate > 0 ? track.Bitrate : null,
            BitrateMode = DetermineBitrateMode(track),
            SampleRateHz = track.SampleRate > 0 ? (int)track.SampleRate : null,
            BitDepth = bitDepth,
            Channels = track.ChannelsArrangement?.NbChannels > 0 ? track.ChannelsArrangement.NbChannels : null,
            ChannelsDescription = GetChannelsDescription(track.ChannelsArrangement?.NbChannels),
            FileCreatedDate = fileInfo.CreationTimeUtc,
            FileModifiedDate = fileInfo.LastWriteTimeUtc,
            IsAccessible = true
        };
    }

    private TrackTagDetails ExtractTagDetails(string filePath, Track track, string splitCharacters, string genreSplitCharacters)
    {
        var rawArtist = SanitizeString(track.Artist) ?? Artist.UnknownArtistName;
        var rawAlbumArtist = SanitizeString(track.AlbumArtist) ?? rawArtist;

        var artists = SplitMetadataList(rawArtist, splitCharacters);
        var albumArtists = SplitMetadataList(rawAlbumArtist, splitCharacters);

        var album = SanitizeString(track.Album);
        var fileName = _fileSystem.GetFileNameWithoutExtension(filePath);
        var title = SanitizeString(track.Title) ?? fileName;

        var genres = SplitMetadataList(SanitizeString(track.Genre), genreSplitCharacters);

        string? grouping = null;
        if (track.AdditionalFields.TryGetValue("GRP1", out var g) ||
            track.AdditionalFields.TryGetValue("CONTENTGROUP", out g) ||
            track.AdditionalFields.TryGetValue("GROUPING", out g) ||
            track.AdditionalFields.TryGetValue("TIT1", out g))
        {
            grouping = SanitizeString(g);
        }

        double? trackGain = null;
        var gainKey = track.AdditionalFields.Keys
            .FirstOrDefault(k => k.Equals("REPLAYGAIN_TRACK_GAIN", StringComparison.OrdinalIgnoreCase));
        if (gainKey != null && track.AdditionalFields.TryGetValue(gainKey, out var gainStr))
            trackGain = ParseReplayGainValue(gainStr);

        double? trackPeak = null;
        var peakKey = track.AdditionalFields.Keys
            .FirstOrDefault(k => k.Equals("REPLAYGAIN_TRACK_PEAK", StringComparison.OrdinalIgnoreCase));
        if (peakKey != null && track.AdditionalFields.TryGetValue(peakKey, out var peakStr))
            trackPeak = ParseReplayGainValue(peakStr);

        double? albumGain = null;
        var albumGainKey = track.AdditionalFields.Keys
            .FirstOrDefault(k => k.Equals("REPLAYGAIN_ALBUM_GAIN", StringComparison.OrdinalIgnoreCase));
        if (albumGainKey != null && track.AdditionalFields.TryGetValue(albumGainKey, out var albumGainStr))
            albumGain = ParseReplayGainValue(albumGainStr);

        double? albumPeak = null;
        var albumPeakKey = track.AdditionalFields.Keys
            .FirstOrDefault(k => k.Equals("REPLAYGAIN_ALBUM_PEAK", StringComparison.OrdinalIgnoreCase));
        if (albumPeakKey != null && track.AdditionalFields.TryGetValue(albumPeakKey, out var albumPeakStr))
            albumPeak = ParseReplayGainValue(albumPeakStr);

        var lyricsInfo = track.Lyrics?.FirstOrDefault();
        var hasLyrics = !string.IsNullOrWhiteSpace(lyricsInfo?.UnsynchronizedLyrics);
        var hasSync = lyricsInfo?.SynchronizedLyrics?.Count > 0;
        var lyricsPreview = hasLyrics ? GetLyricsPreview(lyricsInfo!.UnsynchronizedLyrics) : null;

        string? isrc = null;
        if (track.AdditionalFields.TryGetValue("ISRC", out var isrcStr))
        {
            isrc = SanitizeString(isrcStr);
        }

        return new TrackTagDetails
        {
            Title = title,
            Artists = artists,
            Album = album,
            AlbumArtists = albumArtists,
            TrackNumber = track.TrackNumber > 0 ? track.TrackNumber : null,
            TrackCount = track.TrackTotal > 0 ? track.TrackTotal : null,
            DiscNumber = track.DiscNumber > 0 ? track.DiscNumber : null,
            DiscCount = track.DiscTotal > 0 ? track.DiscTotal : null,
            Year = track.Year > 0 ? track.Year : null,
            Genres = genres,
            Composer = SanitizeString(track.Composer),
            Conductor = SanitizeString(track.Conductor),
            Grouping = grouping,
            Copyright = SanitizeString(track.Copyright),
            Comment = SanitizeString(track.Comment),
            Isrc = isrc,
            Bpm = track.BPM > 0 ? track.BPM : null,
            ReplayGainTrackGain = trackGain,
            ReplayGainTrackPeak = trackPeak,
            ReplayGainAlbumGain = albumGain,
            ReplayGainAlbumPeak = albumPeak,
            HasLyrics = hasLyrics,
            HasSynchronizedLyrics = hasSync,
            LyricsPreview = lyricsPreview
        };
    }

    private async Task<TrackArtworkDetails> ExtractArtworkDetailsAsync(string filePath, Track track, CancellationToken cancellationToken)
    {
        var dirCover = FindCoverArtInDirectoryHierarchy(filePath, null);
        if (!string.IsNullOrEmpty(dirCover) && _fileSystem.FileExists(dirCover))
        {
            try
            {
                var bytes = await _fileSystem.ReadAllBytesAsync(dirCover).ConfigureAwait(false);
                if (bytes.Length > 0)
                {
                    var (width, height, mime) = InspectImageMetadata(bytes);
                    return new TrackArtworkDetails
                    {
                        CoverArtUri = dirCover,
                        Source = ArtworkSource.AdjacentFolder,
                        FileSizeBytes = bytes.Length,
                        Width = width,
                        Height = height,
                        MimeType = mime
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to read adjacent cover art at '{DirCover}'.", dirCover);
            }
        }

        const int MaxCoverSizeAllowed = 20 * 1024 * 1024; // 20 MB safety threshold

        var pic = track.EmbeddedPictures?.FirstOrDefault();
        if (pic?.PictureData is { Length: > 0 } picData)
        {
            if (picData.Length > MaxCoverSizeAllowed)
            {
                _logger.LogWarning("Embedded cover art in '{FilePath}' exceeds 20MB ({Size} bytes). Skipping full decode.", filePath, picData.Length);
                return new TrackArtworkDetails
                {
                    Source = ArtworkSource.Embedded,
                    FileSizeBytes = picData.Length,
                    MimeType = pic.MimeType
                };
            }

            var (width, height, mime) = InspectImageMetadata(picData);
            string? uri = null;
            try
            {
                var (savedUri, _, _) = await _imageProcessor.SaveCoverArtAndExtractColorsAsync(picData).ConfigureAwait(false);
                uri = savedUri;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to cache embedded cover art for inspector.");
            }

            return new TrackArtworkDetails
            {
                CoverArtUri = uri,
                Source = ArtworkSource.Embedded,
                FileSizeBytes = picData.Length,
                Width = width,
                Height = height,
                MimeType = mime ?? pic.MimeType
            };
        }

        return new TrackArtworkDetails
        {
            Source = ArtworkSource.None
        };
    }

    private static (int? width, int? height, string? mime) InspectImageMetadata(byte[] bytes)
    {
        try
        {
            var info = SixLabors.ImageSharp.Image.Identify(bytes);
            if (info != null)
            {
                return (info.Width, info.Height, info.Metadata?.DecodedImageFormat?.DefaultMimeType);
            }
        }
        catch
        {
            // Ignore format parsing issues
        }
        return (null, null, null);
    }

    private static TrackExternalIds ExtractExternalIds(Track track)
    {
        string? acoustId = null;
        if (track.AdditionalFields.TryGetValue("ACOUSTID_ID", out var aid))
            acoustId = ArtistNameHelper.NormalizeStringCore(aid);

        string? mbTrackId = null;
        if (track.AdditionalFields.TryGetValue("MUSICBRAINZ_TRACKID", out var tid))
            mbTrackId = ArtistNameHelper.NormalizeStringCore(tid);

        string? mbReleaseId = null;
        if (track.AdditionalFields.TryGetValue("MUSICBRAINZ_RELEASEID", out var rid) ||
            track.AdditionalFields.TryGetValue("MUSICBRAINZ_ALBUMID", out rid))
            mbReleaseId = ArtistNameHelper.NormalizeStringCore(rid);

        string? mbArtistId = null;
        if (track.AdditionalFields.TryGetValue("MUSICBRAINZ_ARTISTID", out var arid))
            mbArtistId = ArtistNameHelper.NormalizeStringCore(arid);

        return new TrackExternalIds
        {
            AcoustId = acoustId,
            MusicBrainzTrackId = mbTrackId,
            MusicBrainzReleaseId = mbReleaseId,
            MusicBrainzArtistId = mbArtistId
        };
    }

    private static string DetermineBitrateMode(Track track)
    {
        return track.IsVBR ? "VBR" : "CBR";
    }

    private static string GetChannelsDescription(int? channels) => channels switch
    {
        1 => "Mono (1 canal)",
        2 => "Estéreo (2 canais)",
        3 => "2.1 Surround (3 canais)",
        4 => "Quadrafônico (4 canais)",
        5 => "5.0 Surround (5 canais)",
        6 => "5.1 Surround (6 canais)",
        7 => "6.1 Surround (7 canais)",
        8 => "7.1 Surround (8 canais)",
        _ when channels > 0 => $"{channels} canais",
        _ => "Desconhecido"
    };

    private static string FormatFileSize(long bytes)
    {
        if (bytes <= 0) return "0 B";
        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024) >= 1 && counter < suffixes.Length - 1)
        {
            number /= 1024;
            counter++;
        }
        return $"{number:n1} {suffixes[counter]}";
    }

    private static string? GetLyricsPreview(string lyrics, int maxLines = 4)
    {
        if (string.IsNullOrWhiteSpace(lyrics)) return null;
        var lines = lyrics.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                          .Select(l => l.Trim())
                          .Where(l => !string.IsNullOrEmpty(l))
                          .Take(maxLines)
                          .ToList();
        return lines.Count > 0 ? string.Join(Environment.NewLine, lines) : null;
    }

    /// <summary>
    ///     Populates the metadata object from the ATL track, providing sane defaults for missing values.
    /// </summary>
    private void PopulateMetadataFromTrack(SongFileMetadata metadata, Track track, string splitCharacters, string genreSplitCharacters)
    {
        var rawArtist = SanitizeString(track.Artist) ?? Artist.UnknownArtistName;
        var rawAlbumArtist = SanitizeString(track.AlbumArtist) ?? rawArtist;

        var artists = SplitMetadataList(rawArtist, splitCharacters);
        var albumArtists = SplitMetadataList(rawAlbumArtist, splitCharacters);

        var album = SanitizeString(track.Album) ?? Album.UnknownAlbumName;


        metadata.Title = SanitizeString(track.Title) ?? metadata.Title;
        metadata.Artists = artists;
        metadata.Album = album;
        metadata.AlbumArtists = albumArtists;
        metadata.Duration = TimeSpan.FromSeconds(track.Duration);
        metadata.Year = track.Year > 0 ? track.Year : null;
        metadata.TrackNumber = track.TrackNumber > 0 ? track.TrackNumber : null;
        metadata.TrackCount = track.TrackTotal > 0 ? track.TrackTotal : null;
        metadata.DiscNumber = track.DiscNumber > 0 ? track.DiscNumber : null;
        metadata.DiscCount = track.DiscTotal > 0 ? track.DiscTotal : null;
        metadata.Bpm = track.BPM > 0 ? track.BPM : null;
        metadata.SampleRate = track.SampleRate > 0 ? (int)track.SampleRate : null;
        metadata.Bitrate = track.Bitrate > 0 ? track.Bitrate : null;
        metadata.Channels = track.ChannelsArrangement?.NbChannels > 0 ? track.ChannelsArrangement.NbChannels : null;

        // Get unsynchronized lyrics from the first lyrics entry if available
        var lyricsInfo = track.Lyrics?.FirstOrDefault();
        if (lyricsInfo != null)
        {
            metadata.Lyrics = SanitizeString(lyricsInfo.UnsynchronizedLyrics);
        }

        metadata.Composer = SanitizeString(track.Composer);
        metadata.Copyright = SanitizeString(track.Copyright);
        metadata.Comment = SanitizeString(track.Comment);
        metadata.Conductor = SanitizeString(track.Conductor);

        // ATL uses AdditionalFields for MusicBrainz IDs
        if (track.AdditionalFields.TryGetValue("MUSICBRAINZ_TRACKID", out var mbTrackId))
            metadata.MusicBrainzTrackId = SanitizeString(mbTrackId);
        if (track.AdditionalFields.TryGetValue("MUSICBRAINZ_RELEASEID", out var mbReleaseId) ||
            track.AdditionalFields.TryGetValue("MUSICBRAINZ_ALBUMID", out mbReleaseId))
            metadata.MusicBrainzReleaseId = SanitizeString(mbReleaseId);

        // Parse genre (ATL returns a single string, may need to split)
        var genreString = SanitizeString(track.Genre);
        metadata.Genres = SplitMetadataList(genreString, genreSplitCharacters);

        // ATL doesn't have a direct Grouping property, check additional fields
        if (track.AdditionalFields.TryGetValue("GRP1", out var grouping) ||
            track.AdditionalFields.TryGetValue("CONTENTGROUP", out grouping) ||
            track.AdditionalFields.TryGetValue("GROUPING", out grouping) ||
            track.AdditionalFields.TryGetValue("TIT1", out grouping))
            metadata.Grouping = SanitizeString(grouping);

        // Extract ReplayGain tags (case-insensitive lookup)
        var gainKey = track.AdditionalFields.Keys
            .FirstOrDefault(k => k.Equals("REPLAYGAIN_TRACK_GAIN", StringComparison.OrdinalIgnoreCase));
        if (gainKey != null && track.AdditionalFields.TryGetValue(gainKey, out var gainStr))
            metadata.ReplayGainTrackGain = ParseReplayGainValue(gainStr);

        var peakKey = track.AdditionalFields.Keys
            .FirstOrDefault(k => k.Equals("REPLAYGAIN_TRACK_PEAK", StringComparison.OrdinalIgnoreCase));
        if (peakKey != null && track.AdditionalFields.TryGetValue(peakKey, out var peakStr))
            metadata.ReplayGainTrackPeak = ParseReplayGainValue(peakStr);
    }

    private List<string> SplitMetadataList(string? input, string splitCharacters)
    {
        if (string.IsNullOrWhiteSpace(input)) return [];

        // If no split characters are provided, normalize and return the whole string
        if (string.IsNullOrEmpty(splitCharacters))
        {
            var normalized = ArtistNameHelper.NormalizeStringCore(
                input.Replace(AtlMultiValueSeparator, CombinedValueSeparator));
            return normalized != null ? [normalized] : [];
        }

        return input
            .Split([.. splitCharacters, AtlMultiValueSeparator], StringSplitOptions.RemoveEmptyEntries)
            .Select(s => ArtistNameHelper.NormalizeStringCore(s))
            .Where(s => !string.IsNullOrEmpty(s))
            .Select(s => s!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    ///     Extracts album art from the track and saves it to the cache. Each song gets its own
    ///     cached cover art file, keyed by the song's file path. If no embedded art is found,
    ///     falls back to searching directory hierarchy for common cover art files.
    /// </summary>
    private async Task ProcessAlbumArtAsync(SongFileMetadata metadata, Track track, string? baseFolderPath)
    {
        // Navidrome priority: directory files first (cover.*, folder.*, front.*), then embedded, then external.
        // Search directory hierarchy first.
        await ProcessCoverArtFromDirectoryAsync(metadata, baseFolderPath).ConfigureAwait(false);
        if (metadata.CoverArtUri != null) return;

        // No directory art found — fall back to embedded image.
        var picture = track.EmbeddedPictures?.FirstOrDefault();
        if (picture?.PictureData is { Length: > 0 } pictureData)
        {
            try
            {
                var (coverArtUri, lightSwatchId, darkSwatchId) =
                    await _imageProcessor.SaveCoverArtAndExtractColorsAsync(pictureData).ConfigureAwait(false);

                metadata.CoverArtUri = coverArtUri;
                metadata.LightSwatchId = lightSwatchId;
                metadata.DarkSwatchId = darkSwatchId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process embedded album art for '{FilePath}'.", metadata.FilePath);
            }
        }
    }

    /// <summary>
    ///     Searches for cover art files in the directory hierarchy, starting from the song's
    ///     directory and walking up to the base folder path.
    /// </summary>
    private async Task ProcessCoverArtFromDirectoryAsync(SongFileMetadata metadata, string? baseFolderPath)
    {
        try
        {
            var coverArtPath = FindCoverArtInDirectoryHierarchy(metadata.FilePath, baseFolderPath);
            if (string.IsNullOrEmpty(coverArtPath)) return;

            // Read the image file and process it
            var imageBytes = await _fileSystem.ReadAllBytesAsync(coverArtPath).ConfigureAwait(false);
            if (imageBytes.Length == 0) return;

            var (coverArtUri, lightSwatchId, darkSwatchId) =
                await _imageProcessor.SaveCoverArtAndExtractColorsAsync(imageBytes).ConfigureAwait(false);

            metadata.CoverArtUri = coverArtUri;
            metadata.LightSwatchId = lightSwatchId;
            metadata.DarkSwatchId = darkSwatchId;

            _logger.LogDebug("Found cover art from directory for '{FilePath}': {CoverArtPath}",
                metadata.FilePath, coverArtPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to process directory cover art for '{FilePath}'.", metadata.FilePath);
        }
    }

    /// <summary>
    ///     Searches for a cover art file in the directory hierarchy, starting from the song's
    ///     directory and walking up to the base folder path. Looks for common cover art file
    ///     names (cover, folder, album, front) with supported image extensions.
    /// </summary>
    /// <param name="songFilePath">The path to the song file.</param>
    /// <param name="baseFolderPath">The root folder path to stop searching at.</param>
    /// <returns>The full path to the first matching cover art file, or null if none found.</returns>
    private string? FindCoverArtInDirectoryHierarchy(string songFilePath, string? baseFolderPath)
    {
        try
        {
            var currentDirectory = _fileSystem.GetDirectoryName(songFilePath);
            if (string.IsNullOrEmpty(currentDirectory)) return null;

            // Normalize the base folder path for comparison
            var normalizedBasePath = baseFolderPath?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            while (!string.IsNullOrEmpty(currentDirectory))
            {
                // Search for cover art in the current directory
                var coverArtPath = FindCoverArtInDirectory(currentDirectory);
                if (coverArtPath != null) return coverArtPath;

                // Check if we've reached or passed the base folder
                var normalizedCurrent = currentDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (!string.IsNullOrEmpty(normalizedBasePath) &&
                    normalizedCurrent.Equals(normalizedBasePath, StringComparison.OrdinalIgnoreCase))
                {
                    // We've searched the base folder, stop here
                    break;
                }

                // Move up to the parent directory
                var parentDirectory = _fileSystem.GetDirectoryName(currentDirectory);

                // Safety check: if parent equals current, we're at the root
                if (string.IsNullOrEmpty(parentDirectory) ||
                    parentDirectory.Equals(currentDirectory, StringComparison.OrdinalIgnoreCase))
                    break;

                currentDirectory = parentDirectory;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while searching for cover art in directory hierarchy for '{SongFilePath}'.",
                songFilePath);
            return null;
        }
    }

    /// <summary>
    ///     Searches for a cover art file in a specific directory by enumerating files
    ///     and matching against known cover art file names (case-insensitive).
    /// </summary>
    private string? FindCoverArtInDirectory(string directory)
    {
        try
        {
            // Enumerate files once and find matches - more efficient than checking each combination
            var files = _fileSystem.GetFiles(directory, "*.*");

            // First pass: find all matching cover art files
            string? bestMatch = null;
            var bestPriority = int.MaxValue;

            foreach (var filePath in files)
            {
                if (_fileSystem.IsHiddenOrSystemFile(filePath))
                    continue;

                var extension = _fileSystem.GetExtension(filePath);
                if (!FileExtensions.ImageFileExtensions.Contains(extension))
                    continue;

                var fileNameWithoutExt = _fileSystem.GetFileNameWithoutExtension(filePath);
                if (!FileExtensions.CoverArtFileNames.Contains(fileNameWithoutExt))
                    continue;

                // Determine priority based on position in the priority list
                var priority = GetCoverArtPriority(fileNameWithoutExt);
                if (priority < bestPriority)
                {
                    bestPriority = priority;
                    bestMatch = filePath;
                }
            }

            return bestMatch;
        }
        catch (Exception ex)
        {
            // If we can't enumerate the directory, log and return null
            _logger.LogDebug(ex, "Failed to enumerate cover art in directory '{Directory}'.", directory);
            return null;
        }
    }

    /// <summary>
    ///     Gets the priority of a cover art file name (lower = higher priority).
    ///     Delegates to <see cref="FileExtensions.GetCoverArtPriority"/> as the single source of truth.
    /// </summary>
    private static int GetCoverArtPriority(string fileNameWithoutExt) =>
        FileExtensions.GetCoverArtPriority(fileNameWithoutExt);

    /// <summary>
    ///     Gets the path to the LRC file, prioritizing a valid cache entry before
    ///     attempting to extract embedded lyrics from the audio file.
    /// </summary>
    private async Task<string?> GetLrcPathAsync(string audioFilePath, DateTime audioFileLastWriteTime, string? artist, string? album, string? title, Track track)
    {
        var overrideFileName = FileNameHelper.GenerateLrcOverrideFileName(audioFilePath);
        var overridePath = _fileSystem.Combine(_pathConfig.LrcCachePath, overrideFileName);
        if (_fileSystem.FileExists(overridePath)) return overridePath;

        var cacheFileName = FileNameHelper.GenerateLrcCacheFileName(audioFilePath, artist, album, title);
        var cachedLrcPath = _fileSystem.Combine(_pathConfig.LrcCachePath, cacheFileName);

        // Check for a valid cache entry. It's valid if it exists and is newer than the audio file.
        if (_fileSystem.FileExists(cachedLrcPath))
        {
            var cacheLastWriteTime = _fileSystem.GetLastWriteTimeUtc(cachedLrcPath);
            if (cacheLastWriteTime >= audioFileLastWriteTime) return cachedLrcPath;
        }

        try
        {
            return await ExtractAndCacheEmbeddedLrcAsync(track, cachedLrcPath).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract or cache embedded LRC for '{AudioFilePath}'.", audioFilePath);
            return null;
        }
    }

    /// <summary>
    ///     Extracts embedded synchronized lyrics from ATL track, converts them to LRC format, and saves them to the cache.
    /// </summary>
    private async Task<string?> ExtractAndCacheEmbeddedLrcAsync(Track track, string cachedLrcPath)
    {
        var lyricsInfo = track.Lyrics?.FirstOrDefault();
        if (lyricsInfo == null) return null;

        var syncLyrics = lyricsInfo.SynchronizedLyrics;
        if (syncLyrics == null || syncLyrics.Count == 0) return null;

        var lrcContentBuilder = new StringBuilder();

        foreach (var phrase in syncLyrics.OrderBy(p => p.TimestampStart))
        {
            var text = ArtistNameHelper.NormalizeStringCore(phrase.Text);
            if (string.IsNullOrWhiteSpace(text)) continue;

            var time = TimeSpan.FromMilliseconds(phrase.TimestampStart);
            lrcContentBuilder.AppendLine($"[{time:mm\\:ss\\.fff}]{text}");
        }

        var lrcContent = lrcContentBuilder.ToString();
        if (string.IsNullOrWhiteSpace(lrcContent)) return null;

        // Ensure the LRC cache directory exists before writing
        var cacheDirectory = _fileSystem.GetDirectoryName(cachedLrcPath);
        if (!string.IsNullOrEmpty(cacheDirectory) && !_fileSystem.DirectoryExists(cacheDirectory))
        {
            _fileSystem.CreateDirectory(cacheDirectory);
        }

        await _fileSystem.WriteAllTextAsync(cachedLrcPath, lrcContent).ConfigureAwait(false);
        return cachedLrcPath;
    }

    /// <summary>
    ///     Searches for an external .lrc file in the same directory as the audio file, matching by filename.
    /// </summary>
    private string? FindLrcFilePath(string audioFilePath)
    {
        try
        {
            var directory = _fileSystem.GetDirectoryName(audioFilePath);
            if (string.IsNullOrEmpty(directory)) return null;

            var audioFileNameWithoutExt = _fileSystem.GetFileNameWithoutExtension(audioFilePath);
            var lrcFiles = _fileSystem.GetFiles(directory, "*.lrc");
            var match = lrcFiles.FirstOrDefault(lrcPath =>
                _fileSystem.GetFileNameWithoutExtension(lrcPath)
                    .Equals(audioFileNameWithoutExt, StringComparison.OrdinalIgnoreCase));

            if (match != null) return match;

            var txtFiles = _fileSystem.GetFiles(directory, "*.txt");
            return txtFiles.FirstOrDefault(txtPath =>
                _fileSystem.GetFileNameWithoutExtension(txtPath)
                    .Equals(audioFileNameWithoutExt, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while searching for external LRC file for '{AudioFilePath}'.",
                audioFilePath);
            return null;
        }
    }

    /// <summary>
    ///     Trims a string and returns null if the result is empty or whitespace, ensuring consistent null/empty handling.
    /// </summary>
    private string? SanitizeString(string? input)
    {
        return ArtistNameHelper.NormalizeStringCore(input);
    }

    /// <summary>
    ///     Parses a ReplayGain value string (e.g., "-6.54 dB" or "0.98") to a nullable double.
    ///     Uses regular expressions to extract the numeric part, making it robust against
    ///     various formats and trailing metadata.
    /// </summary>
    private static double? ParseReplayGainValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        // Matches a number optionally preceded by + or -
        var match = Regex.Match(value.Trim(), @"^[-+]?[0-9]*\.?[0-9]+");
        if (match.Success && double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        return null;
    }

    /// <summary>
    ///     Gets the artist split characters from cache, or loads them if not cached.
    ///     Thread-safe lazy initialization pattern.
    /// </summary>
    private async Task<string> GetCachedSplitCharactersAsync()
    {
        // Fast path: read from cache without locking if already initialized
        string? cached;
        lock (_splitCharactersLock)
        {
            cached = _cachedSplitCharacters;
        }

        if (cached != null)
            return cached;

        // Slow path: load from settings service
        var splitCharacters = await _settingsService.GetArtistSplitCharactersAsync().ConfigureAwait(false);

        lock (_splitCharactersLock)
        {
            _cachedSplitCharacters = splitCharacters;
        }

        return splitCharacters;
    }

    /// <summary>
    ///     Invalidates the cached split characters when the setting changes.
    /// </summary>
    private void OnArtistSplitCharactersChanged()
    {
        lock (_splitCharactersLock)
        {
            _cachedSplitCharacters = null;
        }

        _logger.LogDebug("Artist split characters cache invalidated due to settings change.");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _settingsService.ArtistSplitCharactersChanged -= OnArtistSplitCharactersChanged;
        _settingsService.GenreSplitCharactersChanged -= OnGenreSplitCharactersChanged;
        GC.SuppressFinalize(this);
    }

    private async Task<string> GetCachedGenreSplitCharactersAsync()
    {
        string? cached;
        lock (_genreSplitCharactersLock)
        {
            cached = _cachedGenreSplitCharacters;
        }

        if (cached != null) return cached;

        var splitCharacters = await _settingsService.GetGenreSplitCharactersAsync().ConfigureAwait(false);

        lock (_genreSplitCharactersLock)
        {
            _cachedGenreSplitCharacters = splitCharacters;
        }

        return splitCharacters;
    }

    private void OnGenreSplitCharactersChanged()
    {
        lock (_genreSplitCharactersLock)
        {
            _cachedGenreSplitCharacters = null;
        }

        _logger.LogDebug("Genre split characters cache invalidated due to settings change.");
    }
}
