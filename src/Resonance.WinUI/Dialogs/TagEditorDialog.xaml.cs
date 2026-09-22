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
        InitializeComponent();
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = ViewModel;

        DialogThemeHelper.ApplyThemeOverrides(this);

        ViewModel.RequestClose += OnViewModelRequestClose;
    }

    public TagEditorViewModel ViewModel { get; }

    private void OnViewModelRequestClose(bool saved)
    {
        Hide();
    }

    private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // Cancel the immediate closing of the dialog so asynchronous save and validation can occur
        args.Cancel = true;

        if (ViewModel.IsSaving)
            return;

        await ViewModel.SaveAsync();
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

    private bool GetAreAllSelected(int selectedCount, int totalDiffCount)
    {
        return totalDiffCount > 0 && selectedCount == totalDiffCount;
    }

    private string GetCounterText(int selectedCount, int totalDiffCount)
    {
        return $"{selectedCount} de {totalDiffCount} selecionados";
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
