# Data Model: Feature 007 — Lyrics Engine

## Overview
Este documento especifica as estruturas de dados, modelos de domínio, entidades de persistência e enumerações necessárias para suportar a resolução canônica de 6 etapas, proveniência rastreável, letras sincronizadas/estáticas, compensação de offset e sinalização de faixas instrumentais no Resonance Player.

---

## 1. Domain Models (`Resonance.Core.Models.Lyrics`)

### 1.1 `LyricsDocument`
Representa o documento unificado de letras resolvido pelo motor para uma música em reprodução.

```csharp
namespace Resonance.Core.Models.Lyrics;

public class LyricsDocument
{
    public LyricsDocument(
        IEnumerable<LyricLine> lines,
        string? rawUnsyncedLyrics = null,
        LyricsType type = LyricsType.None,
        LyricsProvenance provenance = LyricsProvenance.None,
        string? sourcePath = null,
        TimeSpan timeOffset = default,
        bool isInstrumental = false)
    {
        Lines = lines.OrderBy(l => l.StartTime).ToList().AsReadOnly();
        RawUnsyncedLyrics = rawUnsyncedLyrics;
        Type = type;
        Provenance = provenance;
        SourcePath = sourcePath;
        TimeOffset = timeOffset;
        IsInstrumental = isInstrumental;
    }

    /// <summary>Lista imutável e ordenada de estrofes/versos com timestamps.</summary>
    public IReadOnlyList<LyricLine> Lines { get; }

    /// <summary>Texto completo para letras não-sincronizadas ou brutas.</summary>
    public string? RawUnsyncedLyrics { get; }

    /// <summary>Classificação da letra (Sincronizada, Texto Puro, Instrumental, Nenhuma).</summary>
    public LyricsType Type { get; }

    /// <summary>Origem exata da resolução canônica.</summary>
    public LyricsProvenance Provenance { get; }

    /// <summary>Caminho em disco do arquivo fonte ou cache (se aplicável).</summary>
    public string? SourcePath { get; }

    /// <summary>Offset de calibração temporal aplicado às linhas.</summary>
    public TimeSpan TimeOffset { get; }

    /// <summary>Indica se a faixa foi classificada como instrumental.</summary>
    public bool IsInstrumental { get; }

    /// <summary>Verdadeiro se não houver linhas sincronizadas nem texto puro.</summary>
    public bool IsEmpty => !Lines.Any() && string.IsNullOrWhiteSpace(RawUnsyncedLyrics);

    /// <summary>Instância estática para estado vazio/sem letra.</summary>
    public static LyricsDocument Empty { get; } = new(
        Enumerable.Empty<LyricLine>(),
        null,
        LyricsType.None,
        LyricsProvenance.None);

    /// <summary>Instância estática para faixa instrumental identificada.</summary>
    public static LyricsDocument CreateInstrumental(LyricsProvenance provenance = LyricsProvenance.LocalCache) => new(
        Enumerable.Empty<LyricLine>(),
        null,
        LyricsType.Instrumental,
        provenance,
        null,
        TimeSpan.Zero,
        true);
}
```

### 1.2 `LyricsProvenance` (Enum)
Classificação da procedência da letra para exibição de selo na interface e auditoria de cache.

```csharp
namespace Resonance.Core.Models.Lyrics;

public enum LyricsProvenance
{
    None = 0,
    EmbeddedSynced = 1,  // ID3v2 SYLT / Vorbis sync dentro do áudio
    EmbeddedPlain = 2,   // ID3v2 USLT / Vorbis LYRICS dentro do áudio
    LocalFileLrc = 3,    // Arquivo .lrc na mesma pasta (Nome ou Artista - Título)
    LocalFileTxt = 4,    // Arquivo .txt na mesma pasta (Nome ou Artista - Título)
    LocalCache = 5,      // Cache interno da aplicação em %LocalAppData%
    RemoteLrcLib = 6,    // Provedor LRCLIB
    RemoteNetEase = 7    // Provedor NetEase Cloud Music
}
```

### 1.3 `LyricsType` (Enum)
Modalidade da letra para orientar a renderização visual e comportamentos de seek/rolagem.

```csharp
namespace Resonance.Core.Models.Lyrics;

public enum LyricsType
{
    None = 0,
    Synced = 1,        // Sincronizada: suporta highlight em tempo real e clique para seek
    Plain = 2,         // Texto Puro: rolagem manual livre, sem seek por clique
    Instrumental = 3   // Faixa Instrumental: sem letra, exibe estado vazio musical
}
```

### 1.4 `LyricLine` (Existente, Preservado)
```csharp
namespace Resonance.Core.Models.Lyrics;

public class LyricLine
{
    public LyricLine() { }
    public LyricLine(TimeSpan startTime, string text)
    {
        StartTime = startTime;
        Text = text;
    }

    public TimeSpan StartTime { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? RomanizedText { get; set; }
    public bool HasRomanizedText => !string.IsNullOrWhiteSpace(RomanizedText);
}
```

---

## 2. Persistence Model Extensions (`Resonance.Core.Models.Song`)

Atualização da entidade `Song` persistida no SQLite via Entity Framework Core:

```csharp
// Novas propriedades na entidade Song:

/// <summary>
///     Indica se a faixa foi identificada como instrumental (por tag ou provedor remoto).
/// </summary>
public bool? IsInstrumental { get; set; }

/// <summary>
///     Offset manual de calibração em milissegundos configurado pelo usuário para esta faixa.
///     Valores positivos adiantam o texto; negativos atrasam o texto.
/// </summary>
public int? LyricsOffsetMs { get; set; }
```

### Migration EF Core
- **Nome da Migration**: `AddLyricsInstrumentalAndOffsetToSong`
- **Tabela afetada**: `Songs`
- **Colunas adicionadas**:
  - `IsInstrumental` (`INTEGER`, nullable)
  - `LyricsOffsetMs` (`INTEGER`, nullable)

---

## 3. UI Presentation Model (`Resonance.WinUI.ViewModels.LyricsPageViewModel`)

Propriedades adicionadas ao ViewModel para consumo direto na `LyricsPage.xaml`:

| Propriedade | Tipo | Descrição |
|:---|:---|:---|
| `ProvenanceLabel` | `string` | Rótulo amigável (ex.: `"Letra Embutida (Sincronizada)"`, `"Arquivo .lrc"`, `"Fonte: LRCLIB"`) |
| `IsSynced` | `bool` | `true` se a letra atual for do tipo `Synced` |
| `IsPlain` | `bool` | `true` se for texto puro (aciona badge `[Não Sincronizada]`) |
| `IsInstrumental` | `bool` | `true` se for instrumental (exibe mensagem musical dedicada) |
| `CurrentOffsetMs` | `int` | Offset ativo da música em milissegundos (exibe `"Offset: +200 ms"`) |
| `CanExportLrc` | `bool` | Habilitado quando a letra atual não for originada de `.lrc` local |
| `AdjustOffsetCommand` | `IRelayCommand<int>` | Adiciona ou subtrai milissegundos (-500, -100, +100, +500) |
| `ResetOffsetCommand` | `IRelayCommand` | Zera o offset para 0 ms |
| `ExportSidecarLrcCommand`| `IAsyncRelayCommand` | Grava `<NomeDoAudio>.lrc` na pasta da faixa |
