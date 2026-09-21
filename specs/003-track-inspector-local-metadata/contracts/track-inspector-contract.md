# Contract: Track Inspector & Local Metadata

**Feature Branch**: `003-track-inspector-local-metadata`  
**Date**: 2026-09-20  
**Status**: Completed  
**Spec Reference**: [spec.md](../spec.md)

---

## 1. Service Contract: IMetadataService Extension

A interface de extração de metadados (`IMetadataService`) é estendida para fornecer o modelo consolidado do Inspector sem acoplar a interface a detalhes de persistência ou bibliotecas de terceiros:

```csharp
namespace Resonance.Core.Services.Abstractions;

public interface IMetadataService
{
    // Método existente preservado
    Task<SongFileMetadata> ExtractMetadataAsync(string filePath, string? baseFolderPath = null,
        bool includeMediaAssets = true);

    /// <summary>
    ///     Constrói a visão consolidada de inspeção técnica e tags completas para um arquivo de áudio.
    /// </summary>
    /// <param name="filePath">Caminho físico absoluto do arquivo de áudio.</param>
    /// <param name="cancellationToken">Token de cancelamento da operação assíncrona.</param>
    /// <returns>Modelo de dados unificado do Track Inspector.</returns>
    Task<TrackInspectorViewData> GetTrackInspectorViewDataAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Constrói a visão consolidada de inspeção a partir de uma entidade Song já indexada.
    /// </summary>
    Task<TrackInspectorViewData> GetTrackInspectorViewDataAsync(Song song, CancellationToken cancellationToken = default);
}
```

---

## 2. ViewModel Contract: TrackInspectorViewModel

O ViewModel atua como orquestrador do estado do painel lateral, paginação em seleções múltiplas e sincronização com o reprodutor de áudio:

```csharp
namespace Resonance.WinUI.ViewModels;

public interface ITrackInspectorViewModel
{
    // Estado de exibição
    bool IsOpen { get; set; }
    bool IsLoading { get; }
    string? ErrorMessage { get; }

    // Dados ativos
    TrackInspectorViewData? CurrentData { get; }
    bool FollowPlayback { get; set; }

    // Estado do LightBox da capa
    bool IsLightBoxOpen { get; set; }

    // Comandos de controle
    IAsyncRelayCommand ToggleInspectorCommand { get; }
    IAsyncRelayCommand CloseInspectorCommand { get; }
    IAsyncRelayCommand<Song> InspectSongCommand { get; }
    IAsyncRelayCommand<IReadOnlyList<Song>> InspectMultipleSongsCommand { get; }
    IAsyncRelayCommand NextSelectedTrackCommand { get; }
    IAsyncRelayCommand PreviousSelectedTrackCommand { get; }
    IAsyncRelayCommand ExportArtworkCommand { get; }
    IRelayCommand<string> CopyToClipboardCommand { get; }
}
```

---

## 3. UI Layout & Visual States Contract

### Main Page Layout Integration

O painel retrátil é integrado ao `NavigationView` de `MainPage.xaml` através de um Grid de duas colunas:

```xml
<!-- Layout do Conteúdo Principal com Painel Acoplado -->
<Grid x:Name="ContentAreaGrid">
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="*" />
        <ColumnDefinition x:Name="InspectorColumnDefinition" Width="Auto" />
    </Grid.ColumnDefinitions>

    <!-- Conteúdo das Páginas (Biblioteca, Álbuns, Artistas) -->
    <Frame x:Name="ContentFrame" Grid.Column="0" Padding="12,0,12,0" />

    <!-- Painel Lateral Retrátil do Track Inspector -->
    <localControls:TrackInspectorControl
        x:Name="TrackInspectorPanel"
        Grid.Column="1"
        Width="360"
        Visibility="{x:Bind TrackInspectorVm.IsOpen, Mode=OneWay, Converter={StaticResource BooleanToVisibilityConverter}}" />
</Grid>
```

### Visual States do Painel

- **InspectorClosed**:
  - `InspectorColumnDefinition.Width = 0`
  - `TrackInspectorPanel.Visibility = Collapsed`
- **InspectorOpen**:
  - `InspectorColumnDefinition.Width = 360`
  - `TrackInspectorPanel.Visibility = Visible`
  - Animação de deslizamento/fade via `Storyboard`.

### Teclas de Atalho (Keyboard Accelerators)

- `Alt + Enter`: Alterna abertura e fechamento do Inspector para a faixa selecionada ou para a música em reprodução ativa.
- `Escape`: Fecha o Inspector ou o visualizador LightBox caso esteja em foco.
- `Alt + LeftArrow` / `Alt + RightArrow`: Navega entre faixas selecionadas quando em modo multi-seleção.
