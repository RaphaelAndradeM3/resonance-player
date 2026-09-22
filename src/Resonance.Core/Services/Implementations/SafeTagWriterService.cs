using System.Diagnostics;
using ATL;
using Microsoft.Extensions.Logging;
using Resonance.Core.Helpers;
using Resonance.Core.Models;
using Resonance.Core.Services.Abstractions;

namespace Resonance.Core.Services.Implementations;

/// <summary>
///     Provides atomic, corruption-proof tag writing, file lock coordination,
///     and library persistence synchronization.
/// </summary>
public class SafeTagWriterService : ITagWriterService
{
    private readonly IFileSystemService _fileSystem;
    private readonly IMetadataService _metadataService;
    private readonly ILibraryWriter _libraryWriter;
    private readonly ILibraryReader? _libraryReader;
    private readonly IMusicPlaybackService? _playbackService;
    private readonly ILogger<SafeTagWriterService> _logger;

    public SafeTagWriterService(
        IFileSystemService fileSystem,
        IMetadataService metadataService,
        ILibraryWriter libraryWriter,
        ILogger<SafeTagWriterService> logger,
        ILibraryReader? libraryReader = null,
        IMusicPlaybackService? playbackService = null)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _metadataService = metadataService ?? throw new ArgumentNullException(nameof(metadataService));
        _libraryWriter = libraryWriter ?? throw new ArgumentNullException(nameof(libraryWriter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _libraryReader = libraryReader;
        _playbackService = playbackService;
    }

    /// <inheritdoc />
    public async Task<TagWriteResult> ApplyWritePlanAsync(
        TagWritePlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var stopwatch = Stopwatch.StartNew();
        var originalPath = plan.FilePath;

        if (string.IsNullOrWhiteSpace(originalPath) || !_fileSystem.FileExists(originalPath))
        {
            return TagWriteResult.Failed(originalPath, "O arquivo de áudio original não foi encontrado no disco.", stopwatch.Elapsed);
        }

        var dir = _fileSystem.GetDirectoryName(originalPath) ?? Path.GetDirectoryName(originalPath) ?? string.Empty;
        var tempPath = Path.Combine(dir, $"{_fileSystem.GetFileName(originalPath)}.tmp.{Guid.NewGuid():N}");
        var backupPath = Path.Combine(dir, $"{_fileSystem.GetFileName(originalPath)}.bak.{Guid.NewGuid():N}");

        bool wasPlaybackInterrupted = false;
        TimeSpan savedPosition = TimeSpan.Zero;
        bool wasPlaying = false;

        try
        {
            // 1. Working copy in the same directory (volume)
            _fileSystem.CopyFile(originalPath, tempPath, overwrite: true);

            // Ensure temporary copy is writable even if original was marked ReadOnly
            var tempAttrs = File.GetAttributes(tempPath);
            if (tempAttrs.HasFlag(FileAttributes.ReadOnly))
            {
                File.SetAttributes(tempPath, tempAttrs & ~FileAttributes.ReadOnly);
            }

            // 2. Open temporary copy with ATL and apply approved field changes
            var track = new Track(tempPath);
            int fieldsWritten = 0;

            foreach (var change in plan.SelectedChanges)
            {
                if (ApplyField(track, change.FieldKey, change.ProposedValue))
                {
                    fieldsWritten++;
                }
            }

            // Apply cover art changes if specified
            if (plan.RemovePicture)
            {
                track.EmbeddedPictures.Clear();
                fieldsWritten++;
            }
            else if (plan.NewPictureBytes != null && plan.NewPictureBytes.Length > 0)
            {
                track.EmbeddedPictures.Clear();
                var picInfo = PictureInfo.fromBinaryData(plan.NewPictureBytes);
                picInfo.PicType = PictureInfo.PIC_TYPE.Front;
                track.EmbeddedPictures.Add(picInfo);
                fieldsWritten++;
            }

            // Save changes to the temporary copy
            bool saved = track.Save();
            if (!saved)
            {
                throw new InvalidOperationException("ATL.Track.Save() falhou ao salvar as tags no arquivo temporário.");
            }

            // 3. Post-write integrity validation
            bool isValid = await ValidateAudioFileIntegrityAsync(tempPath, cancellationToken).ConfigureAwait(false);
            if (!isValid)
            {
                throw new InvalidOperationException("A validação de integridade do arquivo resultante falhou. O arquivo original foi preservado.");
            }

            // 4. Coordinate file lock if currently playing in Resonance
            if (_playbackService != null &&
                string.Equals(_playbackService.CurrentTrack?.FilePath, originalPath, StringComparison.OrdinalIgnoreCase))
            {
                wasPlaying = _playbackService.IsPlaying;
                savedPosition = _playbackService.CurrentPosition;
                wasPlaybackInterrupted = true;
                _logger.LogInformation("Faixa '{FilePath}' em reprodução ativa. Pausando temporariamente para escrita atômica...", originalPath);
                if (_playbackService.IsPlaying)
                {
                    await _playbackService.PlayPauseAsync().ConfigureAwait(false);
                    await Task.Delay(50, cancellationToken).ConfigureAwait(false);
                }
            }

            // 5. Check and temporarily remove Read-Only flag if set on original file
            var fileAttributes = File.GetAttributes(originalPath);
            if (fileAttributes.HasFlag(FileAttributes.ReadOnly))
            {
                _logger.LogInformation("Arquivo '{FilePath}' marcado como Somente-Leitura. Removendo flag temporariamente...", originalPath);
                File.SetAttributes(originalPath, fileAttributes & ~FileAttributes.ReadOnly);
            }

            // 6. Atomic replacement
            try
            {
                File.Replace(tempPath, originalPath, backupPath);
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }
            }
            catch (PlatformNotSupportedException)
            {
                File.Move(tempPath, originalPath, overwrite: true);
            }

            // 7. Resume playback if was interrupted
            if (wasPlaybackInterrupted && _playbackService != null)
            {
                try
                {
                    if (savedPosition > TimeSpan.Zero)
                    {
                        await _playbackService.SeekAsync(savedPosition).ConfigureAwait(false);
                    }
                    if (wasPlaying && !_playbackService.IsPlaying)
                    {
                        await _playbackService.PlayPauseAsync().ConfigureAwait(false);
                    }
                }
                catch (Exception pbEx)
                {
                    _logger.LogWarning(pbEx, "Falha ao restaurar playback da faixa '{FilePath}'.", originalPath);
                }
            }

            // 8. Synchronize SQLite persistence
            await SynchronizeDatabaseAsync(plan, originalPath, cancellationToken).ConfigureAwait(false);

            stopwatch.Stop();
            _logger.LogInformation("Gravação atômica concluída com sucesso para '{FilePath}' ({FieldsCount} campos em {ElapsedMs}ms).",
                originalPath, fieldsWritten, stopwatch.ElapsedMilliseconds);

            return TagWriteResult.Succeeded(originalPath, fieldsWritten, wasPlaybackInterrupted, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Erro durante a gravação segura de tags para '{FilePath}'. Realizando rollback...", originalPath);

            // Clean up temporary file
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
                // Best-effort cleanup
            }

            // Restore backup if it was left behind
            try
            {
                if (File.Exists(backupPath) && !File.Exists(originalPath))
                {
                    File.Move(backupPath, originalPath, overwrite: true);
                }
            }
            catch
            {
                // Best-effort restore
            }

            // Resume playback if it was paused
            if (wasPlaybackInterrupted && _playbackService != null && wasPlaying && !_playbackService.IsPlaying)
            {
                try
                {
                    await _playbackService.PlayPauseAsync().ConfigureAwait(false);
                }
                catch
                {
                    // Ignore
                }
            }

            return TagWriteResult.Failed(originalPath, ex.Message, stopwatch.Elapsed);
        }
    }

    /// <inheritdoc />
    public Task<bool> ValidateAudioFileIntegrityAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        if (!File.Exists(filePath))
        {
            return Task.FromResult(false);
        }

        try
        {
            var track = new Track(filePath);
            var isUnknownFormat = track.AudioFormat.Name?.Equals("Unknown", StringComparison.OrdinalIgnoreCase) == true ||
                                  track.AudioFormat.ID == -1;

            bool isValid = track.AudioFormat.Readable && !isUnknownFormat && track.DurationMs > 0;
            return Task.FromResult(isValid);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha na validação de integridade de áudio para '{FilePath}'.", filePath);
            return Task.FromResult(false);
        }
    }

    private static bool ApplyField(Track track, string fieldKey, string? value)
    {
        var trimmed = string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        switch (fieldKey)
        {
            case "Title":
                track.Title = trimmed ?? string.Empty;
                return true;

            case "Artist":
                track.Artist = trimmed ?? string.Empty;
                return true;

            case "Album":
                track.Album = trimmed ?? string.Empty;
                return true;

            case "AlbumArtist":
                track.AlbumArtist = trimmed ?? string.Empty;
                return true;

            case "Year":
                track.Year = int.TryParse(trimmed, out var y) ? y : 0;
                return true;

            case "TrackNumber":
                track.TrackNumber = int.TryParse(trimmed, out var t) ? t : 0;
                return true;

            case "TrackTotal":
                track.TrackTotal = int.TryParse(trimmed, out var tt) ? tt : 0;
                return true;

            case "DiscNumber":
                track.DiscNumber = int.TryParse(trimmed, out var d) ? d : 0;
                return true;

            case "DiscTotal":
                track.DiscTotal = int.TryParse(trimmed, out var dt) ? dt : 0;
                return true;

            case "Genre":
                track.Genre = trimmed ?? string.Empty;
                return true;

            case "Comment":
                track.Comment = trimmed ?? string.Empty;
                return true;

            default:
                return false;
        }
    }

    private async Task SynchronizeDatabaseAsync(TagWritePlan plan, string filePath, CancellationToken cancellationToken)
    {
        try
        {
            var refreshedMetadata = await _metadataService.ExtractMetadataAsync(filePath, includeMediaAssets: true).ConfigureAwait(false);

            Song? song = null;
            if (plan.SongId.HasValue && _libraryReader != null)
            {
                song = await _libraryReader.GetSongByIdAsync(plan.SongId.Value).ConfigureAwait(false);
            }
            else if (_libraryReader != null)
            {
                song = await _libraryReader.GetSongByFilePathAsync(filePath).ConfigureAwait(false);
            }

            if (song != null)
            {
                if (!string.IsNullOrWhiteSpace(refreshedMetadata.Title))
                    song.Title = refreshedMetadata.Title;

                if (refreshedMetadata.Artists.Count > 0)
                {
                    song.ArtistName = string.Join(" & ", refreshedMetadata.Artists);
                    song.PrimaryArtistName = refreshedMetadata.Artists[0];
                }

                if (song.Album != null && !string.IsNullOrWhiteSpace(refreshedMetadata.Album))
                {
                    song.Album.Title = refreshedMetadata.Album;
                }

                if (refreshedMetadata.Year.HasValue)
                    song.Year = refreshedMetadata.Year.Value;

                if (refreshedMetadata.TrackNumber.HasValue)
                    song.TrackNumber = refreshedMetadata.TrackNumber.Value;

                if (refreshedMetadata.DiscNumber.HasValue)
                    song.DiscNumber = refreshedMetadata.DiscNumber.Value;

                if (song.SongArtists != null && song.SongArtists.Count > 0)
                {
                    song.SyncDenormalizedFields();
                }
                else
                {
                    song.SortTitle = SortKeyHelper.Normalize(song.Title);
                    song.PrimaryArtistSortName = SortKeyHelper.Normalize(song.PrimaryArtistName);
                }

                await _libraryWriter.UpdateSongAsync(song).ConfigureAwait(false);
                _logger.LogInformation("Metadados sincronizados no banco de dados para Song ID: {SongId}", song.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha na sincronização com o banco de dados após a gravação física de '{FilePath}'.", filePath);
        }
    }
}
