using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Resonance.Core.Data;
using Resonance.Core.Models;
using Resonance.Core.Services.Abstractions;
using Resonance.WinUI.Services.Abstractions;
using Windows.ApplicationModel.DataTransfer;

namespace Resonance.WinUI.ViewModels;

/// <summary>
///     ViewModel for the Track Inspector side panel, providing technical stream details,
///     rich tags, artwork lightbox state, external IDs, acoustic fingerprinting, and recognition.
/// </summary>
public partial class TrackInspectorViewModel : ObservableObject, ITrackInspectorViewModel, IDisposable
{
    private readonly IMetadataService _metadataService;
    private readonly IMusicPlaybackService _playbackService;
    private readonly IDispatcherService _dispatcherService;
    private readonly IUIService _uiService;
    private readonly IFileSystemService _fileSystem;
    private readonly IFingerprintService _fingerprintService;
    private readonly IAcoustIdService _acoustIdService;
    private readonly IDbContextFactory<MusicDbContext> _dbContextFactory;
    private readonly ILogger<TrackInspectorViewModel> _logger;
    private readonly IMusicBrainzService _musicBrainzService;
    private readonly IMetadataEnrichmentService _enrichmentService;

    private IReadOnlyList<Song> _selectedSongs = Array.Empty<Song>();
    private int _currentSongIndex;
    private Song? _currentSong;
    private CancellationTokenSource? _loadingCts;
    private bool _disposed;

    public TrackInspectorViewModel(
        IMetadataService metadataService,
        IMusicPlaybackService playbackService,
        IDispatcherService dispatcherService,
        IUIService uiService,
        IFileSystemService fileSystem,
        IFingerprintService fingerprintService,
        IAcoustIdService acoustIdService,
        IDbContextFactory<MusicDbContext> dbContextFactory,
        ILogger<TrackInspectorViewModel> logger,
        IMusicBrainzService musicBrainzService,
        IMetadataEnrichmentService enrichmentService)
    {
        _metadataService = metadataService ?? throw new ArgumentNullException(nameof(metadataService));
        _playbackService = playbackService ?? throw new ArgumentNullException(nameof(playbackService));
        _dispatcherService = dispatcherService ?? throw new ArgumentNullException(nameof(dispatcherService));
        _uiService = uiService ?? throw new ArgumentNullException(nameof(uiService));
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _fingerprintService = fingerprintService ?? throw new ArgumentNullException(nameof(fingerprintService));
        _acoustIdService = acoustIdService ?? throw new ArgumentNullException(nameof(acoustIdService));
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _musicBrainzService = musicBrainzService ?? throw new ArgumentNullException(nameof(musicBrainzService));
        _enrichmentService = enrichmentService ?? throw new ArgumentNullException(nameof(enrichmentService));

        _playbackService.TrackChanged += OnPlaybackTrackChanged;
    }

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial TrackInspectorViewData? CurrentData { get; set; }

    [ObservableProperty]
    public partial bool FollowPlayback { get; set; }

    [ObservableProperty]
    public partial bool IsLightBoxOpen { get; set; }

    [ObservableProperty]
    public partial string PaginationText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasMultipleTracks { get; set; }

    [ObservableProperty]
    public partial bool IsRecognizing { get; set; }

    [ObservableProperty]
    public partial string RecognitionStatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial IReadOnlyList<RecognitionCandidate> RecognitionCandidates { get; set; } = Array.Empty<RecognitionCandidate>();

    [ObservableProperty]
    public partial string? FingerprintHash { get; set; }

    [ObservableProperty]
    public partial bool IsAlreadyIdentified { get; set; }

    [ObservableProperty]
    public partial bool IsEnriching { get; set; }

    [ObservableProperty]
    public partial string EnrichmentStatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial EnrichmentProposal? CurrentProposal { get; set; }

    [ObservableProperty]
    public partial bool HasEnrichmentProposal { get; set; }

    [ObservableProperty]
    public partial string EnrichmentButtonText { get; set; } = "Buscar Metadados Canônicos";

    partial void OnIsOpenChanged(bool value)
    {
        if (!value)
        {
            IsLightBoxOpen = false;
        }
        else if (CurrentData == null && _playbackService.CurrentTrack != null)
        {
            _ = InspectSongAsync(_playbackService.CurrentTrack);
        }
    }

    partial void OnFollowPlaybackChanged(bool value)
    {
        if (value && IsOpen && _playbackService.CurrentTrack != null)
        {
            _ = InspectSongAsync(_playbackService.CurrentTrack);
        }
    }

    private void OnPlaybackTrackChanged()
    {
        if (!FollowPlayback || !IsOpen || _playbackService.CurrentTrack == null)
            return;

        _dispatcherService.TryEnqueue(() =>
        {
            _ = InspectSongAsync(_playbackService.CurrentTrack);
        });
    }

    [RelayCommand]
    public async Task ToggleInspectorAsync()
    {
        if (IsOpen)
        {
            IsOpen = false;
        }
        else
        {
            IsOpen = true;
            if (CurrentData == null && _playbackService.CurrentTrack != null)
            {
                await InspectSongAsync(_playbackService.CurrentTrack);
            }
        }
    }

    [RelayCommand]
    public Task CloseInspectorAsync()
    {
        IsOpen = false;
        IsLightBoxOpen = false;
        return Task.CompletedTask;
    }

    [RelayCommand]
    public async Task InspectSongAsync(Song? song)
    {
        if (song == null) return;
        _selectedSongs = [song];
        _currentSongIndex = 0;
        await LoadSongDataAsync(song);
    }

    [RelayCommand]
    public async Task InspectMultipleSongsAsync(IReadOnlyList<Song>? songs)
    {
        if (songs == null || songs.Count == 0) return;
        _selectedSongs = songs;
        _currentSongIndex = 0;
        await LoadSongDataAsync(_selectedSongs[_currentSongIndex]);
    }

    [RelayCommand]
    public async Task NextSelectedTrackAsync()
    {
        if (_selectedSongs.Count <= 1 || _currentSongIndex >= _selectedSongs.Count - 1)
            return;

        _currentSongIndex++;
        await LoadSongDataAsync(_selectedSongs[_currentSongIndex]);
    }

    [RelayCommand]
    public async Task PreviousSelectedTrackAsync()
    {
        if (_selectedSongs.Count <= 1 || _currentSongIndex <= 0)
            return;

        _currentSongIndex--;
        await LoadSongDataAsync(_selectedSongs[_currentSongIndex]);
    }

    private async Task LoadSongDataAsync(Song song)
    {
        _loadingCts?.Cancel();
        _loadingCts = new CancellationTokenSource();
        var token = _loadingCts.Token;

        _currentSong = song;
        IsLoading = true;
        ErrorMessage = null;
        IsOpen = true;

        try
        {
            var data = await _metadataService.GetTrackInspectorViewDataAsync(song, token).ConfigureAwait(false);

            data.CurrentTrackIndex = _currentSongIndex + 1;
            data.TotalSelectedTracks = _selectedSongs.Count;

            bool identified = !string.IsNullOrEmpty(data.ExternalIds.AcoustId) ||
                              !string.IsNullOrEmpty(data.ExternalIds.MusicBrainzTrackId) ||
                              !string.IsNullOrEmpty(song.AcoustId) ||
                              !string.IsNullOrEmpty(song.MusicBrainzTrackId);

            _dispatcherService.TryEnqueue(() =>
            {
                CurrentData = data;
                HasMultipleTracks = _selectedSongs.Count > 1;
                PaginationText = $"{data.CurrentTrackIndex} de {data.TotalSelectedTracks}";
                IsAlreadyIdentified = identified;
                FingerprintHash = song.AcousticFingerprint;
                RecognitionCandidates = Array.Empty<RecognitionCandidate>();
                RecognitionStatusText = string.Empty;
                IsRecognizing = false;
                CurrentProposal = null;
                HasEnrichmentProposal = false;
                EnrichmentStatusText = string.Empty;
                IsEnriching = false;
                EnrichmentButtonText = !string.IsNullOrEmpty(data.ExternalIds.MusicBrainzTrackId ?? song.MusicBrainzTrackId)
                    ? "Buscar Metadados Canônicos"
                    : "Buscar por Artista e Título";
                IsLoading = false;

                if (FollowPlayback && IsOpen)
                {
                    _ = FetchMetadataAsync();
                }
            });
        }
        catch (OperationCanceledException)
        {
            // Cancelled
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load track inspector data for song {SongId}", song.Id);
            _dispatcherService.TryEnqueue(() =>
            {
                ErrorMessage = "Não foi possível carregar os detalhes da faixa.";
                IsLoading = false;
            });
        }
    }

    [RelayCommand]
    public async Task IdentifyTrackAsync()
    {
        if (IsRecognizing || _currentSong == null)
            return;

        await PerformTrackRecognitionAsync(forceRecomputeFingerprint: false);
    }

    [RelayCommand]
    public async Task ReidentifyTrackAsync()
    {
        if (IsRecognizing || _currentSong == null)
            return;

        await PerformTrackRecognitionAsync(forceRecomputeFingerprint: true);
    }

    private async Task PerformTrackRecognitionAsync(bool forceRecomputeFingerprint)
    {
        if (_currentSong == null || string.IsNullOrWhiteSpace(_currentSong.FilePath))
        {
            RecognitionStatusText = "Nenhuma faixa selecionada ou caminho inválido.";
            return;
        }

        IsRecognizing = true;
        RecognitionCandidates = Array.Empty<RecognitionCandidate>();
        RecognitionStatusText = "Iniciando análise acústica...";

        try
        {
            AcousticFingerprint fingerprint;

            if (!forceRecomputeFingerprint && !string.IsNullOrWhiteSpace(_currentSong.AcousticFingerprint))
            {
                var duration = (int)Math.Round(_currentSong.Duration.TotalSeconds);
                if (duration <= 0) duration = 120;
                fingerprint = new AcousticFingerprint(_currentSong.AcousticFingerprint, duration);
                FingerprintHash = fingerprint.Hash;
                RecognitionStatusText = "Impressão acústica local recuperada. Consultando AcoustID...";
            }
            else
            {
                RecognitionStatusText = "Calculando impressão acústica local (Chromaprint)...";
                var fp = await _fingerprintService.GenerateFingerprintAsync(_currentSong.FilePath).ConfigureAwait(false);

                if (fp == null || !fp.Value.IsValid)
                {
                    _dispatcherService.TryEnqueue(() =>
                    {
                        RecognitionStatusText = "Falha ao gerar fingerprint de áudio (arquivo inacessível ou formato incompatível).";
                        IsRecognizing = false;
                    });
                    return;
                }

                fingerprint = fp.Value;

                try
                {
                    await using var db = await _dbContextFactory.CreateDbContextAsync().ConfigureAwait(false);
                    var dbSong = await db.Songs.FindAsync(_currentSong.Id).ConfigureAwait(false);
                    if (dbSong != null)
                    {
                        dbSong.AcousticFingerprint = fingerprint.Hash;
                        await db.SaveChangesAsync().ConfigureAwait(false);
                    }
                    _currentSong.AcousticFingerprint = fingerprint.Hash;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Não foi possível salvar AcousticFingerprint para a música {SongId}", _currentSong.Id);
                }

                _dispatcherService.TryEnqueue(() =>
                {
                    FingerprintHash = fingerprint.Hash;
                    RecognitionStatusText = "Impressão calculada. Consultando AcoustID & MusicBrainz...";
                });
            }

            var result = await _acoustIdService.LookupAsync(fingerprint).ConfigureAwait(false);

            _dispatcherService.TryEnqueue(() =>
            {
                IsRecognizing = false;
                switch (result.Status)
                {
                    case RecognitionStatus.Success:
                        RecognitionCandidates = result.Candidates;
                        RecognitionStatusText = $"{result.Candidates.Count} correspondência(s) encontrada(s).";
                        break;
                    case RecognitionStatus.NoMatchFound:
                        RecognitionCandidates = Array.Empty<RecognitionCandidate>();
                        RecognitionStatusText = "Nenhuma correspondência encontrada no AcoustID para esta gravação.";
                        break;
                    case RecognitionStatus.AudioTooShort:
                        RecognitionCandidates = Array.Empty<RecognitionCandidate>();
                        RecognitionStatusText = "Áudio muito curto para identificação confiável (mínimo de 10s).";
                        break;
                    case RecognitionStatus.RateLimited:
                        RecognitionCandidates = Array.Empty<RecognitionCandidate>();
                        RecognitionStatusText = "Limite de requisições AcoustID atingido. Aguarde alguns instantes.";
                        break;
                    case RecognitionStatus.OfflineOrDisabled:
                        RecognitionCandidates = Array.Empty<RecognitionCandidate>();
                        RecognitionStatusText = "Serviço AcoustID desativado nas configurações do player.";
                        break;
                    default:
                        RecognitionCandidates = Array.Empty<RecognitionCandidate>();
                        RecognitionStatusText = result.ErrorMessage ?? "Erro ao consultar o serviço AcoustID.";
                        break;
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante o reconhecimento acústico da faixa {SongId}", _currentSong.Id);
            _dispatcherService.TryEnqueue(() =>
            {
                IsRecognizing = false;
                RecognitionStatusText = "Ocorreu uma falha inesperada durante a identificação.";
            });
        }
    }

    [RelayCommand]
    public async Task SelectCandidateAsync(RecognitionCandidate? candidate)
    {
        if (candidate == null || _currentSong == null)
            return;

        try
        {
            // 1. Atualizar banco de dados SQLite (Princípio VII: somente BD/memória, sem tocar no arquivo em disco)
            await using var db = await _dbContextFactory.CreateDbContextAsync().ConfigureAwait(false);
            var dbSong = await db.Songs.FindAsync(_currentSong.Id).ConfigureAwait(false);
            if (dbSong != null)
            {
                dbSong.AcoustId = candidate.AcoustId;
                dbSong.MusicBrainzTrackId = candidate.MusicBrainzTrackId;
                if (!string.IsNullOrEmpty(candidate.MusicBrainzReleaseId))
                {
                    dbSong.MusicBrainzReleaseId = candidate.MusicBrainzReleaseId;
                }
                await db.SaveChangesAsync().ConfigureAwait(false);
            }

            _currentSong.AcoustId = candidate.AcoustId;
            _currentSong.MusicBrainzTrackId = candidate.MusicBrainzTrackId;
            if (!string.IsNullOrEmpty(candidate.MusicBrainzReleaseId))
            {
                _currentSong.MusicBrainzReleaseId = candidate.MusicBrainzReleaseId;
            }

            // 2. Atualizar dados em memória no TrackInspectorViewData
            _dispatcherService.TryEnqueue(() =>
            {
                if (CurrentData != null)
                {
                    CurrentData.ExternalIds.AcoustId = candidate.AcoustId;
                    CurrentData.ExternalIds.MusicBrainzTrackId = candidate.MusicBrainzTrackId;
                    CurrentData.ExternalIds.MusicBrainzReleaseId = candidate.MusicBrainzReleaseId;
                    CurrentData.ExternalIds.MusicBrainzArtistId = candidate.MusicBrainzArtistId;

                    // Sugestões de tags sem modificar arquivos físicos no disco (Princípio VII)
                    if (!string.IsNullOrWhiteSpace(candidate.Title))
                    {
                        CurrentData.Tags.Title = candidate.Title;
                    }
                    if (!string.IsNullOrWhiteSpace(candidate.Artist))
                    {
                        CurrentData.Tags.Artists = new List<string> { candidate.Artist };
                    }
                    if (!string.IsNullOrWhiteSpace(candidate.Album))
                    {
                        CurrentData.Tags.Album = candidate.Album;
                    }
                    if (candidate.Year.HasValue && candidate.Year.Value > 0)
                    {
                        CurrentData.Tags.Year = candidate.Year;
                    }

                    OnPropertyChanged(nameof(CurrentData));
                }

                IsAlreadyIdentified = true;
                RecognitionCandidates = Array.Empty<RecognitionCandidate>();
                RecognitionStatusText = "Faixa vinculada com sucesso ao AcoustID & MusicBrainz!";
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao vincular candidato {CandidateTrackId} à música {SongId}", candidate.MusicBrainzTrackId, _currentSong.Id);
            _dispatcherService.TryEnqueue(() =>
            {
                RecognitionStatusText = "Não foi possível vincular os identificadores à faixa.";
            });
        }
    }

    [RelayCommand]
    public void DiscardCandidates()
    {
        RecognitionCandidates = Array.Empty<RecognitionCandidate>();
        RecognitionStatusText = string.Empty;
    }

    [RelayCommand]
    public void CopyToClipboard(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        try
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(text);
            Clipboard.SetContent(dataPackage);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to copy text to clipboard: {Text}", text);
        }
    }

    [RelayCommand]
    public async Task ExportArtworkAsync()
    {
        if (CurrentData?.Artwork.CoverArtUri == null)
            return;

        var sourceUri = CurrentData.Artwork.CoverArtUri;
        var localPath = sourceUri.StartsWith("file:///", StringComparison.OrdinalIgnoreCase)
            ? Uri.UnescapeDataString(new Uri(sourceUri).LocalPath)
            : sourceUri;

        if (!_fileSystem.FileExists(localPath))
        {
            await _uiService.ShowMessageDialogAsync("Exportar Capa", "Arquivo de capa não encontrado no disco.");
            return;
        }

        var targetFolder = await _uiService.PickSingleFolderAsync();
        if (string.IsNullOrEmpty(targetFolder))
            return;

        try
        {
            var ext = _fileSystem.GetExtension(localPath);
            if (string.IsNullOrEmpty(ext)) ext = ".jpg";

            var fileName = $"cover_{CurrentData.Tags.Title}_{DateTime.UtcNow.Ticks}{ext}";
            foreach (var invalidChar in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(invalidChar, '_');
            }

            var destPath = Path.Combine(targetFolder, fileName);
            _fileSystem.CopyFile(localPath, destPath, overwrite: true);
            await _uiService.ShowMessageDialogAsync("Exportar Capa", $"Capa exportada com sucesso para:\n{destPath}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export artwork to folder {Folder}", targetFolder);
            await _uiService.ShowMessageDialogAsync("Exportar Capa", "Falha ao salvar a imagem na pasta selecionada.");
        }
    }

    #region Online Metadata Enrichment (Feature 005)

    [RelayCommand]
    public async Task FetchMetadataAsync()
    {
        if (IsEnriching || CurrentData == null || _currentSong == null)
            return;

        IsEnriching = true;
        EnrichmentStatusText = "Consultando MusicBrainz...";

        try
        {
            var mbid = CurrentData.ExternalIds.MusicBrainzTrackId ?? _currentSong.MusicBrainzTrackId;
            MusicBrainzRecordingDetail? detail = null;

            if (!string.IsNullOrWhiteSpace(mbid))
            {
                EnrichmentStatusText = "Buscando metadados canônicos por MBID...";
                detail = await _musicBrainzService.GetRecordingMetadataAsync(
                    mbid,
                    preferredAlbum: CurrentData.Tags.Album).ConfigureAwait(false);
            }

            if (detail == null)
            {
                var artist = CurrentData.Tags.ArtistsFormatted != "—" ? CurrentData.Tags.ArtistsFormatted : _currentSong.ArtistName;
                var title = !string.IsNullOrWhiteSpace(CurrentData.Tags.Title) ? CurrentData.Tags.Title : _currentSong.Title;

                if (!string.IsNullOrWhiteSpace(title))
                {
                    EnrichmentStatusText = $"Buscando gravação para '{title}'...";
                    detail = await _musicBrainzService.SearchRecordingAsync(
                        artist ?? string.Empty,
                        title,
                        preferredAlbum: CurrentData.Tags.Album).ConfigureAwait(false);
                }
            }

            // Fallback: If not found via text, query AcoustID via acoustic fingerprint
            if (detail == null)
            {
                var hash = FingerprintHash ?? _currentSong.AcousticFingerprint;
                var duration = (int)_currentSong.Duration.TotalSeconds;
                if (!string.IsNullOrEmpty(hash) && duration >= 10)
                {
                    _dispatcherService.TryEnqueue(() => EnrichmentStatusText = "Consultando AcoustID por áudio...");
                    var acoustResult = await _acoustIdService.LookupAsync(new AcousticFingerprint(hash, duration)).ConfigureAwait(false);
                    if (acoustResult.Status == RecognitionStatus.Success && acoustResult.Candidates.Count > 0)
                    {
                        var topCandidate = acoustResult.Candidates[0];
                        if (!string.IsNullOrEmpty(topCandidate.MusicBrainzTrackId))
                        {
                            _dispatcherService.TryEnqueue(() => EnrichmentStatusText = "Identificado via AcoustID! Carregando metadados...");
                            detail = await _musicBrainzService.GetRecordingMetadataAsync(
                                topCandidate.MusicBrainzTrackId,
                                preferredAlbum: CurrentData.Tags.Album).ConfigureAwait(false);
                        }
                    }
                }
            }

            if (detail == null)
            {
                _dispatcherService.TryEnqueue(() =>
                {
                    EnrichmentStatusText = "Nenhuma gravação correspondente encontrada no MusicBrainz.";
                    IsEnriching = false;
                });
                return;
            }

            string? coverUrl = null;
            if (!string.IsNullOrWhiteSpace(detail.ReleaseId))
            {
                EnrichmentStatusText = "Verificando capa oficial no Cover Art Archive...";
                coverUrl = await _musicBrainzService.GetCoverArtUrlAsync(detail.ReleaseId).ConfigureAwait(false);
            }

            EnrichmentStatusText = "Gerando proposta de enriquecimento...";
            var proposal = _enrichmentService.CreateProposal(
                _currentSong.FilePath,
                CurrentData.Tags,
                detail,
                coverArtUrl: coverUrl,
                songId: _currentSong.Id,
                originalCoverPath: CurrentData.Artwork.CoverArtUri ?? _currentSong.AlbumArtUriFromTrack);

            _dispatcherService.TryEnqueue(() =>
            {
                CurrentProposal = proposal;
                HasEnrichmentProposal = true;
                EnrichmentStatusText = $"Metadados encontrados! {proposal.SelectedCount} alterações sugeridas.";
                IsEnriching = false;
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enrich metadata for {FilePath}", _currentSong.FilePath);
            _dispatcherService.TryEnqueue(() =>
            {
                EnrichmentStatusText = "Falha ao conectar com o serviço online. Verifique sua conexão.";
                IsEnriching = false;
            });
        }
    }

    [RelayCommand]
    public async Task AdvanceToTagReviewAsync()
    {
        if (CurrentProposal == null)
            return;

        var count = CurrentProposal.SelectedCount;
        await _uiService.ShowMessageDialogAsync(
            "Revisão de Tags",
            $"Proposta com {count} alterações pronta para a etapa de revisão e gravação de tags (Feature 006).");
    }

    [RelayCommand]
    public void ClearProposal()
    {
        CurrentProposal = null;
        HasEnrichmentProposal = false;
        EnrichmentStatusText = string.Empty;
    }

    #endregion

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _playbackService.TrackChanged -= OnPlaybackTrackChanged;
        _loadingCts?.Cancel();
        _loadingCts?.Dispose();
        GC.SuppressFinalize(this);
    }
}
