# Data Model: 003 — Track Inspector & Local Metadata

**Feature Branch**: `003-track-inspector-local-metadata`  
**Date**: 2026-09-20  
**Status**: Completed  
**Spec Reference**: [spec.md](./spec.md)

---

## 1. Core Domain Entities & DTOs

O modelo de dados do Track Inspector organiza-se em camadas conceituais bem definidas, separando dados físicos do arquivo de áudio, tags editoriais, metadados de arte/capa e identificadores externos.

### Entity: TrackTechnicalDetails

Representa os atributos de baixo nível e parâmetros físicos de codificação do arquivo de áudio.

```csharp
namespace Resonance.Core.Models;

public class TrackTechnicalDetails
{
    public string FilePath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string FileSizeFormatted { get; set; } = string.Empty; // ex.: "43.2 MB"
    public TimeSpan Duration { get; set; }
    public string ContainerFormat { get; set; } = string.Empty;  // ex.: "FLAC", "MPEG Audio", "Ogg"
    public string AudioCodec { get; set; } = string.Empty;       // ex.: "FLAC", "MP3", "AAC", "Opus"
    public int? BitrateKbps { get; set; }                        // ex.: 320, 1045
    public string BitrateMode { get; set; } = string.Empty;      // "CBR", "VBR", "ABR", "Desconhecido"
    public int? SampleRateHz { get; set; }                       // ex.: 44100, 96000
    public int? BitDepth { get; set; }                           // ex.: 16, 24, 32 (null para lossy)
    public int? Channels { get; set; }                           // ex.: 1, 2, 6
    public string ChannelsDescription { get; set; } = string.Empty; // ex.: "Estéreo (2 canais)"
    public DateTime? FileCreatedDate { get; set; }
    public DateTime? FileModifiedDate { get; set; }
}
```

### Entity: TrackTagDetails

Representa os metadados musicais e editoriais extraídos das tags locais embutidas no arquivo.

```csharp
namespace Resonance.Core.Models;

public class TrackTagDetails
{
    public string Title { get; set; } = string.Empty;
    public List<string> Artists { get; set; } = new();
    public string? Album { get; set; }
    public List<string> AlbumArtists { get; set; } = new();
    public int? TrackNumber { get; set; }
    public int? TrackCount { get; set; }
    public int? DiscNumber { get; set; }
    public int? DiscCount { get; set; }
    public int? Year { get; set; }
    public List<string> Genres { get; set; } = new();
    public string? Composer { get; set; }
    public string? Conductor { get; set; }
    public string? Grouping { get; set; }
    public string? Copyright { get; set; }
    public string? Comment { get; set; }
    public string? Isrc { get; set; }
    public double? Bpm { get; set; }

    // ReplayGain
    public double? ReplayGainTrackGain { get; set; }
    public double? ReplayGainTrackPeak { get; set; }
    public double? ReplayGainAlbumGain { get; set; }
    public double? ReplayGainAlbumPeak { get; set; }

    // Letras
    public bool HasLyrics { get; set; }
    public bool HasSynchronizedLyrics { get; set; }
    public string? LyricsPreview { get; set; }
}
```

### Entity: TrackArtworkDetails

Representa a imagem de capa embutida no arquivo ou detectada na pasta local.

```csharp
namespace Resonance.Core.Models;

public enum ArtworkSource
{
    None,
    Embedded,
    AdjacentFolder,
    RemoteCache
}

public class TrackArtworkDetails
{
    public string? CoverArtUri { get; set; }
    public string? MimeType { get; set; }        // ex.: "image/jpeg", "image/png"
    public int? Width { get; set; }
    public int? Height { get; set; }
    public long? FileSizeBytes { get; set; }
    public ArtworkSource Source { get; set; } = ArtworkSource.None;
    public string DimensionsFormatted => Width.HasValue && Height.HasValue ? $"{Width} x {Height}" : "N/A";
}
```

### Entity: TrackExternalIds

Representa códigos e identificadores de integração externa vinculados à faixa.

```csharp
namespace Resonance.Core.Models;

public class TrackExternalIds
{
    public string? AcoustId { get; set; }
    public string? MusicBrainzTrackId { get; set; }
    public string? MusicBrainzReleaseId { get; set; }
    public string? MusicBrainzArtistId { get; set; }

    public bool HasAnyExternalId => !string.IsNullOrEmpty(AcoustId) ||
                                     !string.IsNullOrEmpty(MusicBrainzTrackId) ||
                                     !string.IsNullOrEmpty(MusicBrainzReleaseId) ||
                                     !string.IsNullOrEmpty(MusicBrainzArtistId);
}
```

### Presentation DTO: TrackInspectorViewData

Consolida todas as informações necessárias para renderização no painel lateral retrátil e suporta paginação em multi-seleção.

```csharp
namespace Resonance.Core.Models;

public class TrackInspectorViewData
{
    public TrackTechnicalDetails Technical { get; set; } = new();
    public TrackTagDetails Tags { get; set; } = new();
    public TrackArtworkDetails Artwork { get; set; } = new();
    public TrackExternalIds ExternalIds { get; set; } = new();

    // Proveniência dos dados
    public string ProvenanceLabel { get; set; } = "Arquivo Local";

    // Paginação para seleção múltipla
    public int CurrentTrackIndex { get; set; } = 1;
    public int TotalSelectedTracks { get; set; } = 1;
    public bool IsMultiTrackSelection => TotalSelectedTracks > 1;
    public bool HasPreviousTrack => CurrentTrackIndex > 1;
    public bool HasNextTrack => CurrentTrackIndex < TotalSelectedTracks;
}
```

---

## 2. Validações e Regras de Negócio

1. **Campos Obrigatórios de Exibição**:
   - `FilePath` e `Title` nunca devem ser nulos ou em branco. Se a tag `Title` estiver ausente, o nome do arquivo sem extensão deve ser adotado como título padrão de fallback.
2. **Normalização de Taxa de Bits e Frequência**:
   - `SampleRateHz` exibido como kHz com uma casa decimal (ex.: `44100` -> `44.1 kHz`, `96000` -> `96.0 kHz`).
   - `BitrateKbps` formatado com unidade explícita (ex.: `320 kbps`). Modo `VBR` explicitamente indicado caso `BitrateMode == "VBR"`.
3. **Tratamento de Profundidade de Bits**:
   - Para formatos lossy (MP3, AAC, Ogg, Opus) em que profundidade de bits inteira não se aplica nativamente ao contêiner, o campo deve exibir `N/A (Lossy)` ou ficar omitido elegantemente, evitando exibir `0 bits`.
4. **Resiliência a Erros de Leitura**:
   - Se o arquivo físico for deletado, mover-se ou estiver em mídia removível inacessível, `TechnicalDetails` registra `IsAccessible = false` e a interface exibe aviso amigável sem lançar exceções.
