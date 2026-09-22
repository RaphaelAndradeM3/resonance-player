using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Resonance.Core.Models;
using Resonance.Core.Services.Abstractions;

namespace Resonance.Core.ViewModels;

/// <summary>
///     ViewModel for the unified Tag Editor Dialog, supporting both online proposal diff review
///     and manual direct tag editing with atomic write coordination.
/// </summary>
public partial class TagEditorViewModel : ObservableObject
{
    private readonly ITagDiffService _tagDiffService;
    private readonly ITagWriterService _tagWriterService;
    private readonly IFilePickerService _filePickerService;
    private readonly IHttpClientFactory? _httpClientFactory;
    private readonly ILogger<TagEditorViewModel> _logger;

    public TagEditorViewModel(
        ITagDiffService tagDiffService,
        ITagWriterService tagWriterService,
        IFilePickerService filePickerService,
        ILogger<TagEditorViewModel> logger,
        IHttpClientFactory? httpClientFactory = null)
    {
        _tagDiffService = tagDiffService ?? throw new ArgumentNullException(nameof(tagDiffService));
        _tagWriterService = tagWriterService ?? throw new ArgumentNullException(nameof(tagWriterService));
        _filePickerService = filePickerService ?? throw new ArgumentNullException(nameof(filePickerService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClientFactory = httpClientFactory;

        DiffRecords = new ObservableCollection<TagDiffRecord>();
        EditableTags = new EditableTagModel();
    }

    /// <summary>
    ///     Event triggered when the dialog should close. Parameter indicates whether changes were saved.
    /// </summary>
    public event Action<bool>? RequestClose;

    [ObservableProperty] public partial Song? CurrentSong { get; set; }
    [ObservableProperty] public partial string FilePath { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IsReviewMode { get; set; }
    [ObservableProperty] public partial bool IsSaving { get; set; }
    [ObservableProperty] public partial string? StatusMessage { get; set; }
    [ObservableProperty] public partial bool HasStatusError { get; set; }
    [ObservableProperty] public partial bool SaveSucceeded { get; set; }

    [ObservableProperty] public partial string? CurrentCoverUri { get; set; }
    [ObservableProperty] public partial string? ProposedCoverUri { get; set; }
    [ObservableProperty] public partial byte[]? NewPictureBytes { get; set; }
    [ObservableProperty] public partial string? PictureMimeType { get; set; }
    [ObservableProperty] public partial bool RemovePicture { get; set; }
    [ObservableProperty] public partial bool IncludeCoverArt { get; set; } = true;

    [ObservableProperty] public partial TrackAudioTags? OriginalTags { get; set; }
    [ObservableProperty] public partial EditableTagModel EditableTags { get; set; }
    [ObservableProperty] public partial ObservableCollection<TagDiffRecord> DiffRecords { get; set; }

    public int SelectedCount => DiffRecords.Count(r => r.IsSelected);
    public int TotalDiffCount => DiffRecords.Count(r => r.HasChanged);

    public bool HasProposedCover => !string.IsNullOrWhiteSpace(ProposedCoverUri);
    public bool HasCurrentCover => !string.IsNullOrWhiteSpace(CurrentCoverUri);

    /// <summary>
    ///     Initializes the ViewModel from an online enrichment proposal for diff review.
    /// </summary>
    public void InitializeFromProposal(
        EnrichmentProposal proposal,
        Song? song,
        TrackAudioTags? currentTags)
    {
        ArgumentNullException.ThrowIfNull(proposal);

        CurrentSong = song;
        FilePath = proposal.FilePath;
        IsReviewMode = true;
        OriginalTags = currentTags ?? new TrackAudioTags();
        EditableTags = EditableTagModel.FromTrackAudioTags(OriginalTags);

        CurrentCoverUri = proposal.OriginalCoverPath;
        ProposedCoverUri = proposal.ProposedCoverUrl;
        IncludeCoverArt = !string.IsNullOrWhiteSpace(proposal.ProposedCoverUrl);
        RemovePicture = false;
        NewPictureBytes = null;

        DiffRecords.Clear();
        var diffs = _tagDiffService.GenerateDiff(FilePath, OriginalTags, proposal);
        foreach (var diff in diffs)
        {
            diff.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(TagDiffRecord.IsSelected))
                {
                    OnPropertyChanged(nameof(SelectedCount));
                }
            };
            DiffRecords.Add(diff);
        }

        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(TotalDiffCount));
        OnPropertyChanged(nameof(HasProposedCover));
        OnPropertyChanged(nameof(HasCurrentCover));
    }

    /// <summary>
    ///     Initializes the ViewModel for direct manual tag editing.
    /// </summary>
    public void InitializeForManualEdit(
        Song song,
        TrackAudioTags currentTags)
    {
        ArgumentNullException.ThrowIfNull(song);
        ArgumentNullException.ThrowIfNull(currentTags);

        CurrentSong = song;
        FilePath = song.FilePath;
        IsReviewMode = false;
        OriginalTags = currentTags;
        EditableTags = EditableTagModel.FromTrackAudioTags(currentTags);

        CurrentCoverUri = song.AlbumArtUriFromTrack;
        ProposedCoverUri = null;
        IncludeCoverArt = false;
        RemovePicture = false;
        NewPictureBytes = null;

        DiffRecords.Clear();

        OnPropertyChanged(nameof(HasProposedCover));
        OnPropertyChanged(nameof(HasCurrentCover));
    }

    [RelayCommand]
    public void ToggleSelectAll()
    {
        if (DiffRecords.Count == 0) return;

        bool allSelected = DiffRecords.Where(d => d.HasChanged).All(d => d.IsSelected);
        bool targetState = !allSelected;

        foreach (var record in DiffRecords)
        {
            if (record.HasChanged)
            {
                record.IsSelected = targetState;
            }
        }

        OnPropertyChanged(nameof(SelectedCount));
    }

    [RelayCommand]
    public async Task PickCustomCoverAsync()
    {
        var picked = await _filePickerService.PickSingleFileAsync(new[] { ".jpg", ".jpeg", ".png", ".webp" });
        if (string.IsNullOrWhiteSpace(picked) || !File.Exists(picked))
            return;

        try
        {
            var bytes = await File.ReadAllBytesAsync(picked);
            var ext = Path.GetExtension(picked).ToLowerInvariant();
            var mime = ext switch
            {
                ".png" => "image/png",
                ".webp" => "image/webp",
                _ => "image/jpeg"
            };

            NewPictureBytes = bytes;
            PictureMimeType = mime;
            ProposedCoverUri = picked;
            RemovePicture = false;
            IncludeCoverArt = true;

            OnPropertyChanged(nameof(HasProposedCover));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao carregar imagem de capa customizada de '{Path}'.", picked);
            StatusMessage = "Erro ao ler o arquivo de imagem selecionado.";
            HasStatusError = true;
        }
    }

    [RelayCommand]
    public void RemoveCover()
    {
        RemovePicture = true;
        NewPictureBytes = null;
        ProposedCoverUri = null;
        IncludeCoverArt = false;

        OnPropertyChanged(nameof(HasProposedCover));
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        if (IsSaving) return;

        IsSaving = true;
        HasStatusError = false;
        StatusMessage = "Gravando tags de forma atômica no arquivo...";

        try
        {
            if (IncludeCoverArt && !RemovePicture && NewPictureBytes == null &&
                !string.IsNullOrWhiteSpace(ProposedCoverUri) &&
                (ProposedCoverUri.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                 ProposedCoverUri.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            {
                StatusMessage = "Baixando imagem de capa em alta resolução...";
                await DownloadProposedCoverArtAsync();
            }

            TagWritePlan plan;

            if (IsReviewMode)
            {
                plan = _tagDiffService.CreateWritePlan(
                    FilePath,
                    DiffRecords,
                    IncludeCoverArt ? NewPictureBytes : null,
                    IncludeCoverArt ? PictureMimeType : null,
                    RemovePicture,
                    CurrentSong?.Id);
            }
            else
            {
                var manualDiffs = _tagDiffService.GenerateDiff(
                    FilePath,
                    OriginalTags ?? new TrackAudioTags(),
                    EditableTags);

                plan = _tagDiffService.CreateWritePlan(
                    FilePath,
                    manualDiffs,
                    IncludeCoverArt ? NewPictureBytes : null,
                    IncludeCoverArt ? PictureMimeType : null,
                    RemovePicture,
                    CurrentSong?.Id);
            }

            StatusMessage = "Gravando arquivo e validando integridade...";
            var result = await _tagWriterService.ApplyWritePlanAsync(plan);

            if (result.Success)
            {
                SaveSucceeded = true;
                StatusMessage = $"Sucesso: {result.FieldsUpdatedCount} tags gravadas ({result.ElapsedTime.TotalMilliseconds:F0}ms).";
                _logger.LogInformation("Gravação concluída para '{FilePath}'. Fechando diálogo.", FilePath);
                RequestClose?.Invoke(true);
            }
            else
            {
                HasStatusError = true;
                StatusMessage = result.ErrorMessage ?? "Falha ao gravar as alterações no arquivo.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exceção não tratada ao salvar tags para '{FilePath}'.", FilePath);
            HasStatusError = true;
            StatusMessage = ex.Message;
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    public void Cancel()
    {
        RequestClose?.Invoke(false);
    }

    private async Task DownloadProposedCoverArtAsync()
    {
        if (string.IsNullOrWhiteSpace(ProposedCoverUri)) return;

        try
        {
            using var client = _httpClientFactory?.CreateClient() ?? new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(15);
            var bytes = await client.GetByteArrayAsync(ProposedCoverUri);
            if (bytes != null && bytes.Length > 0)
            {
                NewPictureBytes = bytes;
                PictureMimeType = "image/jpeg";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao baixar imagem de capa de '{Uri}'. Prosseguindo sem capa.", ProposedCoverUri);
        }
    }
}
