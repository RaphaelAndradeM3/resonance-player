# Data Model: Feature 005 — Online Metadata Enrichment

**Feature**: `005-online-metadata-enrichment`  
**Date**: 2026-09-22  
**Spec**: [spec.md](./spec.md)

---

## 1. Domain Entities & Enums

### 1.1 `MetadataProvenance` (Enum)

Representa a fonte de dados de um metadado musical individual.

```csharp
namespace Resonance.Core.Models;

/// <summary>
///     Identifies the originating source of a metadata property.
/// </summary>
public enum MetadataProvenance
{
    /// <summary>Extracted directly from the local file's embedded audio tags.</summary>
    LocalTag = 0,

    /// <summary>Retrieved from the MusicBrainz open encyclopedia.</summary>
    MusicBrainz = 1,

    /// <summary>Retrieved from the Cover Art Archive community database.</summary>
    CoverArtArchive = 2,

    /// <summary>Manually supplied or overridden by the user.</summary>
    UserOverride = 3
}
```

---

### 1.2 `FieldProposalStatus` (Enum)

Classifica o relacionamento semântico entre o valor atualmente presente na faixa local e a recomendação externa.

```csharp
namespace Resonance.Core.Models;

/// <summary>
///     Represents the comparison outcome between the local tag and the proposed remote value.
/// </summary>
public enum FieldProposalStatus
{
    /// <summary>The local value and the remote value are semantically identical.</summary>
    Unchanged = 0,

    /// <summary>The local value was empty/null and the provider supplied a new value.</summary>
    NewValue = 1,

    /// <summary>The remote value proposes an update or casing/formatting refinement to the existing local value.</summary>
    Updated = 2,

    /// <summary>A significant divergence exists between the local value and the remote value (e.g., completely different album/title).</summary>
    Conflict = 3
}
```

---

### 1.3 `FieldProposal` (Class / Model)

Descreve a proposta de alteração de um campo de metadado específico, com suporte a seleção reativa pelo usuário.

```csharp
namespace Resonance.Core.Models;

/// <summary>
///     Encapsulates a proposed change for an individual metadata field.
/// </summary>
public class FieldProposal
{
    /// <summary>Gets the display name of the field (e.g. "Título", "Artista", "Álbum", "Ano").</summary>
    public string FieldName { get; init; } = string.Empty;

    /// <summary>Gets the canonical internal key of the field (e.g. "Title", "Artist", "Year").</summary>
    public string FieldKey { get; init; } = string.Empty;

    /// <summary>Gets the current local value in the file/library, or null if unset.</summary>
    public string? CurrentValue { get; init; }

    /// <summary>Gets the value suggested by the remote provider, or null if unavailable.</summary>
    public string? ProposedValue { get; init; }

    /// <summary>Gets the comparison status for this field.</summary>
    public FieldProposalStatus Status { get; init; }

    /// <summary>Gets the origin of the proposed value.</summary>
    public MetadataProvenance Provenance { get; init; }

    /// <summary>Gets or sets whether this proposal is selected by the user to be carried over to staging/review.</summary>
    public bool IsSelected { get; set; }
}
```

---

### 1.4 `EnrichmentProposal` (Class / Aggregate)

Representa a proposta completa de enriquecimento consolidada para uma faixa.

```csharp
namespace Resonance.Core.Models;

/// <summary>
///     Represents a complete, consolidated metadata enrichment proposal for a track.
/// </summary>
public class EnrichmentProposal
{
    /// <summary>Gets the unique identifier of the song in the local library, if indexed.</summary>
    public Guid? SongId { get; init; }

    /// <summary>Gets the absolute physical file path of the audio file.</summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>Gets the MusicBrainz Recording ID associated with this proposal.</summary>
    public string RecordingMbid { get; init; } = string.Empty;

    /// <summary>Gets the MusicBrainz Release ID (Album) associated with this proposal.</summary>
    public string? ReleaseMbid { get; init; }

    /// <summary>Gets the list of individual field proposals.</summary>
    public IReadOnlyList<FieldProposal> Proposals { get; init; } = Array.Empty<FieldProposal>();

    /// <summary>Gets the path to the local cover art file, if any.</summary>
    public string? OriginalCoverPath { get; init; }

    /// <summary>Gets the resolved Cover Art Archive URL for the official front cover (500px).</summary>
    public string? ProposedCoverUrl { get; init; }

    /// <summary>Gets the thumbnail URL for the official front cover (250px).</summary>
    public string? ProposedCoverThumbnailUrl { get; init; }

    /// <summary>Gets the overall confidence score (0.0 to 1.0) of the match.</summary>
    public double ConfidenceScore { get; init; }

    /// <summary>Gets the timestamp when this proposal was generated.</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Gets the number of fields currently selected by the user.</summary>
    public int SelectedCount => Proposals.Count(p => p.IsSelected);

    /// <summary>Gets whether there are any actionable changes (NewValue, Updated or Conflict).</summary>
    public bool HasActionableChanges => Proposals.Any(p => p.Status != FieldProposalStatus.Unchanged);
}
```

---

### 1.5 `MusicBrainzRecordingDetail` (Model)

Dados consolidados extraídos do Web Service do MusicBrainz após aplicação da heurística canônica de release.

```csharp
namespace Resonance.Core.Models;

/// <summary>
///     Consolidated metadata extracted from MusicBrainz Web Service for a recording.
/// </summary>
public class MusicBrainzRecordingDetail
{
    public string RecordingId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Artist { get; init; } = string.Empty;
    public string? ArtistId { get; init; }
    public string? ReleaseId { get; init; }
    public string? Album { get; init; }
    public string? AlbumArtist { get; init; }
    public int? Year { get; init; }
    public string? ReleaseDate { get; init; }
    public int? TrackNumber { get; init; }
    public int? TotalTracks { get; init; }
    public int? DiscNumber { get; init; }
    public int? TotalDiscs { get; init; }
    public string? Label { get; init; }
    public string? Isrc { get; init; }
    public IReadOnlyList<string> Genres { get; init; } = Array.Empty<string>();
    public int Score { get; init; }
}
```

---

## 2. Cache Data Model

### 2.1 `CachedMetadataEntry<T>` (JSON Envelope)

Estrutura salva nos arquivos JSON sob `%LocalAppData%/Resonance/cache/metadata/{key}.json`.

```json
{
  "key": "mbid_e2b46781-a832-4720-9dc2-7c77f0a9058f",
  "cachedAt": "2026-09-22T03:35:00Z",
  "expiresAt": "2026-09-29T03:35:00Z",
  "data": {
    "recordingId": "e2b46781-a832-4720-9dc2-7c77f0a9058f",
    "title": "Bohemian Rhapsody",
    "artist": "Queen",
    "artistId": "0383dadf-2a4e-4d10-a4f8-e9e809d381f8",
    "releaseId": "23c21dc3-9993-41e7-96a1-944a1e944747",
    "album": "A Night at the Opera",
    "albumArtist": "Queen",
    "year": 1975,
    "releaseDate": "1975-11-21",
    "trackNumber": 11,
    "totalTracks": 12,
    "discNumber": 1,
    "totalDiscs": 1,
    "label": "EMI",
    "isrc": "GBAYE7500045",
    "genres": ["Rock", "Progressive Rock", "Opera"],
    "score": 100
  }
}
```

---

## 3. State Transitions & Lifecycle

```text
[Faixa Inspecionada no Track Inspector]
         │
         ▼
[Usuário Clica em "Buscar Metadados Online"]
         │
         ├──► Se MusicBrainzTrackId existe ──► Consulta direta via MBID
         └──► Se ausente ────────────────────► Busca textual (Artista + Título)
         │
         ▼
[Consulta ao Cache Local em Disco]
         │
         ├──► Cache Hit (< 15ms) ────────────► Recupera JSON sem chamada HTTP
         └──► Cache Miss ────────────────────► Consulta API MusicBrainz (1 req/s)
                                                      │
                                                      ▼
                                              Salva resposta no cache em disco
         │
         ▼
[Resolução de Capa via Cover Art Archive]
         │
         ▼
[Execução do Motor de Mesclagem (IMetadataEnrichmentService)]
         │
         ├──► Compara cada campo (Local vs Remoto)
         ├──► Calcula Status (Unchanged, Updated, NewValue, Conflict)
         ├──► Aplica Regra de Gênero (Fusão com deduplicação)
         ├──► Atribui IsSelected inicial (True p/ Novos e Atualizados; False p/ Inalterados e Conflitos)
         └──► Gera EnrichmentProposal
         │
         ▼
[Renderização no Track Inspector]
         │
         ├──► Usuário marca/desmarca campos via checkboxes
         ├──► Visualiza capa oficial sugerida (500x500)
         │
         ▼
[Ação "Avançar para Revisão de Tags"] ──► Repassa proposta selecionada em memória para a Feature 006
```
