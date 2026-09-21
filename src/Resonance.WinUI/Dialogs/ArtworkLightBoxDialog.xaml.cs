using System;
using Microsoft.UI.Xaml.Controls;
using Resonance.WinUI.ViewModels;

namespace Resonance.WinUI.Dialogs;

/// <summary>
///     Modal dialog providing a full-resolution LightBox view of the track's album cover
///     along with physical dimensions, format, and quick export capability.
/// </summary>
public sealed partial class ArtworkLightBoxDialog : ContentDialog
{
    public ArtworkLightBoxDialog(TrackInspectorViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = ViewModel;
    }

    public TrackInspectorViewModel ViewModel { get; }

    private void OnExportButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (ViewModel.ExportArtworkCommand.CanExecute(null))
        {
            ViewModel.ExportArtworkCommand.Execute(null);
        }
    }
}
