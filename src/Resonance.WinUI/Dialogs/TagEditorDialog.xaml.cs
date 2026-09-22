using System;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Resonance.WinUI.Helpers;
using Resonance.WinUI.ViewModels;

namespace Resonance.WinUI.Dialogs;

/// <summary>
///     Modal dialog for reviewing metadata enrichment proposals and manually editing audio file tags.
/// </summary>
public sealed partial class TagEditorDialog : ContentDialog
{
    public TagEditorDialog(TagEditorViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = ViewModel;

        InitializeComponent();

        DialogThemeHelper.ApplyThemeOverrides(this);

        ViewModel.RequestClose += OnViewModelRequestClose;
    }

    public TagEditorViewModel ViewModel { get; }

    private void OnViewModelRequestClose(bool saved)
    {
        if (!saved)
        {
            Hide();
        }
    }

    private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            if (ViewModel.IsSaving)
            {
                args.Cancel = true;
                return;
            }

            await ViewModel.SaveAsync();

            // If saving was not successful, prevent dialog from closing so the user can review the error
            if (!ViewModel.SaveSucceeded)
            {
                args.Cancel = true;
            }
        }
        catch
        {
            args.Cancel = true;
        }
        finally
        {
            deferral.Complete();
        }
    }

    private void OnCloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        ViewModel.Cancel();
    }

    private string GetSubheaderText(bool isReviewMode, int selectedCount, int totalDiffCount)
    {
        if (isReviewMode)
        {
            return $"Revisão de Metadados Online ({selectedCount} de {totalDiffCount} selecionados)";
        }
        return "Modo de Edição Manual Direta";
    }

    private bool? GetAreAllSelected(int selectedCount, int totalDiffCount)
    {
        if (totalDiffCount == 0) return false;
        if (selectedCount == totalDiffCount) return true;
        if (selectedCount > 0) return null;
        return false;
    }

    private string GetCounterText(int selectedCount, int totalDiffCount)
    {
        return $"{selectedCount} de {totalDiffCount} selecionados";
    }

    private void OnReviewModeClick(object sender, RoutedEventArgs e)
    {
        ViewModel.IsReviewMode = true;
    }

    private void OnManualModeClick(object sender, RoutedEventArgs e)
    {
        ViewModel.IsReviewMode = false;
    }

    private Brush GetReviewButtonBackground(bool isReviewMode)
    {
        return isReviewMode
            ? (Application.Current.Resources.TryGetValue("CardBackgroundFillColorDefaultBrush", out var b) && b is Brush brush ? brush : new SolidColorBrush(Colors.DimGray))
            : new SolidColorBrush(Colors.Transparent);
    }

    private Brush GetManualButtonBackground(bool isReviewMode)
    {
        return !isReviewMode
            ? (Application.Current.Resources.TryGetValue("CardBackgroundFillColorDefaultBrush", out var b) && b is Brush brush ? brush : new SolidColorBrush(Colors.DimGray))
            : new SolidColorBrush(Colors.Transparent);
    }

    private Visibility GetStatusVisibility(string? statusMessage, bool isSaving)
    {
        return (isSaving || !string.IsNullOrWhiteSpace(statusMessage))
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private Brush GetStatusBrush(bool hasStatusError)
    {
        if (hasStatusError)
        {
            return Application.Current.Resources.TryGetValue("SystemFillColorCriticalBrush", out var brush) && brush is Brush b
                ? b
                : new SolidColorBrush(Colors.Red);
        }

        return Application.Current.Resources.TryGetValue("AccentTextFillColorPrimaryBrush", out var accent) && accent is Brush ab
            ? ab
            : new SolidColorBrush(Colors.DodgerBlue);
    }
}
