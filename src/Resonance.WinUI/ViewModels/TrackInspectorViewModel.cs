using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Resonance.Core.Models;
using Resonance.Core.Services.Abstractions;
using Resonance.WinUI.Services.Abstractions;
using Windows.ApplicationModel.DataTransfer;

namespace Resonance.WinUI.ViewModels;

/// <summary>
///     ViewModel for the Track Inspector side panel, providing technical stream details,
///     rich tags, artwork lightbox state, external IDs, and playback synchronization.
/// </summary>
public partial class TrackInspectorViewModel : ObservableObject, ITrackInspectorViewModel, IDisposable
{
    private readonly IMetadataService _metadataService;
    private readonly IMusicPlaybackService _playbackService;
    private readonly IDispatcherService _dispatcherService;
    private readonly IUIService _uiService;
    private readonly IFileSystemService _fileSystem;
    private readonly ILogger<TrackInspectorViewModel> _logger;

    private IReadOnlyList<Song> _selectedSongs = Array.Empty<Song>();
    private int _currentSongIndex;
    private CancellationTokenSource? _loadingCts;
    private bool _disposed;

    public TrackInspectorViewModel(
        IMetadataService metadataService,
        IMusicPlaybackService playbackService,
        IDispatcherService dispatcherService,
        IUIService uiService,
        IFileSystemService fileSystem,
        ILogger<TrackInspectorViewModel> logger)
    {
        _metadataService = metadataService ?? throw new ArgumentNullException(nameof(metadataService));
        _playbackService = playbackService ?? throw new ArgumentNullException(nameof(playbackService));
        _dispatcherService = dispatcherService ?? throw new ArgumentNullException(nameof(dispatcherService));
        _uiService = uiService ?? throw new ArgumentNullException(nameof(uiService));
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

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

        IsLoading = true;
        ErrorMessage = null;
        IsOpen = true;

        try
        {
            var data = await _metadataService.GetTrackInspectorViewDataAsync(song, token).ConfigureAwait(false);

            data.CurrentTrackIndex = _currentSongIndex + 1;
            data.TotalSelectedTracks = _selectedSongs.Count;

            _dispatcherService.TryEnqueue(() =>
            {
                CurrentData = data;
                HasMultipleTracks = _selectedSongs.Count > 1;
                PaginationText = $"{data.CurrentTrackIndex} de {data.TotalSelectedTracks}";
                IsLoading = false;
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
