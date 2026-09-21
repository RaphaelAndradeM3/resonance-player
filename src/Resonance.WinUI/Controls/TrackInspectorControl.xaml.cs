using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Resonance.WinUI.Dialogs;
using Resonance.WinUI.ViewModels;

namespace Resonance.WinUI.Controls;

/// <summary>
///     Control representing the Track Inspector side panel.
/// </summary>
public sealed partial class TrackInspectorControl : UserControl
{
    public TrackInspectorControl()
    {
        InitializeComponent();

        if (App.Services != null)
        {
            ViewModel = App.Services.GetRequiredService<TrackInspectorViewModel>();
            DataContext = ViewModel;
        }
    }

    public TrackInspectorViewModel? ViewModel { get; }

    private void OnArtworkThumbnailClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.CurrentData?.Artwork.CoverArtUri == null)
            return;

        // Open LightBox
        ViewModel.IsLightBoxOpen = true;
        ShowArtworkLightBox();
    }

    private async void ShowArtworkLightBox()
    {
        if (ViewModel?.CurrentData == null) return;

        try
        {
            var dialog = new ArtworkLightBoxDialog(ViewModel)
            {
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
        catch
        {
            // Dialog display cancelled or unattached
        }
        finally
        {
            if (ViewModel != null)
            {
                ViewModel.IsLightBoxOpen = false;
            }
        }
    }

    private void OnThumbnailOverlayPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (ThumbnailOverlay != null)
        {
            ThumbnailOverlay.Opacity = 1;
        }
    }

    private void OnThumbnailOverlayPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (ThumbnailOverlay != null)
        {
            ThumbnailOverlay.Opacity = 0;
        }
    }
}
