# Phase 1: Data Model & Schema Changes — 004 Audio Fingerprint & Music Recognition

**Feature**: [spec.md](./spec.md)  
**Date**: 2026-09-21  
**Status**: Completed  

---

## 1. Domain Entities & Value Objects

### 1.1 `AcousticFingerprint` (Value Object / Record)
Representa o resultado imutável da análise acústica local de um arquivo de áudio gerado pelo Chromaprint.

```csharp
namespace Resonance.Core.Models;

/// <summary>
///     Encapsula a impressão digital acústica calculada localmente pelo Chromaprint.
/// </summary>
/// <param name="Hash">String compactada em Base64 gerada pelo algoritmo.</param>
/// <param name="DurationSeconds">Duração da gravação em segundos inteiros.</param>
/// <param name="Algorithm">Versão do algoritmo Chromaprint (padrão = 1).</param>
public readonly record struct AcousticFingerprint(
    string Hash,
    int DurationSeconds,
    int Algorithm = 1)
{
    public bool IsValid => !string.IsNullOrWhiteSpace(Hash) && DurationSeconds > 0;
}
```

---

### 1.2 `RecognitionCandidate` (Model)
Representa um candidato a correspondência de áudio retornado pelo AcoustID e associado a registros do MusicBrainz.

```csharp
namespace Resonance.Core.Models;

/// <summary>
///     Representa uma gravação sugerida pelo serviço de reconhecimento acústico.
/// </summary>
public class RecognitionCandidate
{
    public string? AcoustId { get; init; }
    public string MusicBrainzTrackId { get; init; } = string.Empty;
    public string? MusicBrainzReleaseId { get; init; }
    public string? MusicBrainzArtistId { get; init; }

    public string Title { get; init; } = string.Empty;
    public string Artist { get; init; } = string.Empty;
    public string? Album { get; init; }
    public int? Year { get; init; }

    /// <summary>
    ///     Pontuação de similaridade retornada pelo AcoustID (0.0 a 1.0).
    /// </summary>
    public double ConfidenceScore { get; init; }

    /// <summary>
    ///     Pontuação normalizada em percentual (0 a 100).
    /// </summary>
    public int ConfidencePercentage => (int)Math.Round(ConfidenceScore * 100);

    /// <summary>
    ///     Indica se o candidato atende ao critério de alta relevância (>= 80%).
    /// </summary>
    public bool IsHighConfidence => ConfidenceScore >= 0.80;
}
```

---

### 1.3 `RecognitionResult` (Model)
Encapsula o resultado de ponta a ponta da consulta acústica.

```csharp
namespace Resonance.Core.Models;

public enum RecognitionStatus
{
    Success,
    NoMatchFound,
    OfflineOrDisabled,
    RateLimited,
    NetworkError,
    AudioTooShort,
    AnalysisFailed
}

/// <summary>
///     Resultado consolidado de uma operação de identificação acústica.
/// </summary>
public class RecognitionResult
{
    public RecognitionStatus Status { get; init; }
    public IReadOnlyList<RecognitionCandidate> Candidates { get; init; } = Array.Empty<RecognitionCandidate>();
    public AcousticFingerprint? Fingerprint { get; init; }
    public string? ErrorMessage { get; init; }

    public bool HasCandidates => Candidates.Count > 0;
}
```

---

## 2. Extensões no Modelo de Persistência (`Song` & EF Core)

### 2.1 Propriedades Adicionadas em `Resonance.Core.Models.Song`

```csharp
// Em src/Resonance.Core/Models/Song.cs:

/// <summary>
///     Identificador único da gravação no banco comunitário do AcoustID.
/// </summary>
[MaxLength(100)]
public string? AcoustId { get; set; }

/// <summary>
///     Impressão digital acústica calculada pelo Chromaprint em Base64 compactado.
/// </summary>
public string? AcousticFingerprint { get; set; }
```

### 2.2 Migration EF Core
Arquivo: `src/Resonance.Core/Data/Migrations/20260921_AddAcousticFingerprintAndAcoustIdToSong.cs`
- Adiciona coluna `AcousticFingerprint` (TEXT, nullable) na tabela `Songs`.
- Adiciona coluna `AcoustId` (TEXT, nullable, MaxLength 100) na tabela `Songs`.
- Cria índice opcional `IX_Songs_AcoustId` para buscas de catálogo e duplicatas.

---

## 3. Máquina de Estados da UI no Track Inspector

```text
[ Idle ] 
   │
   ├─► Usuário clica em "Identificar Música via Áudio"
   │
   ▼
[ ExtractingFingerprint ] (FFmpeg executando localmente, gerando Chromaprint)
   │
   ├─► Erro de decodificação / arquivo ilegível ──► [ Error (AnalysisFailed) ]
   │
   ▼
[ QueryingAcoustId ] (Consulta HTTP com Polly rate limiter e circuit breaker)
   │
   ├─► Sem internet ou Provedor Desativado ─────► [ OfflineOrDisabled ]
   ├─► Erro 429 ou Timeout ─────────────────────► [ Error (RetryAvailable) ]
   ├─► 0 resultados na base ────────────────────► [ NoMatchFound ]
   │
   ▼
[ ResultsAvailable ] (Exibe até 5 candidatos com score >= 40%)
   │
   ├─► Usuário clica em "Descartar" ───────────► [ Idle ]
   ├─► Usuário clica em "Vincular Candidato" ───► [ LinkedSuccess ]
   │
   ▼
[ LinkedSuccess ] (Atualiza TrackExternalIds, preenche preview e salva Song)
```

---

## 4. Regras de Validação de Dados

1. **Threshold de Relevância**: Candidatos com `ConfidenceScore < 0.40` são descartados antes da exibição.
2. **Limite de Exibição**: No máximo 5 candidatos são mantidos na coleção observável da UI, ordenados de forma decrescente por `ConfidenceScore`.
3. **Imutabilidade do Arquivo Físico**: Nenhuma tag do arquivo de áudio no sistema de arquivos é escrita ou alterada nesta etapa.
4. **Resiliência a Nulos**: Campos opcionais como `Album`, `Year`, `ReleaseGroupId` e `AcoustId` são tratados de forma segura com valores de fallback amigáveis caso ausentes no retorno do AcoustID.
