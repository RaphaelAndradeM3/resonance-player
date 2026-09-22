# Tasks: 005 — Online Metadata Enrichment

**Feature Branch**: `005-online-metadata-enrichment`  
**Date**: 2026-09-22  
**Status**: Ready for Implementation  
**Spec Reference**: [spec.md](./spec.md)  
**Implementation Plan**: [plan.md](./plan.md)  
**Data Model**: [data-model.md](./data-model.md)  
**Contracts**: [contracts/](./contracts/)  
**Quickstart Guide**: [quickstart.md](./quickstart.md)  
**Quality Checklist**: [checklists/quality.md](./checklists/quality.md)  

---

## Constitution Execution Guardrails

- **Princípio I (Fonte da Verdade)**: Reutilização e expansão estrita de `MusicBrainzService`, `IProviderPipelineProvider`, `IAppInfoService`, `TrackExternalIds`, `TrackAudioTags`, `Song` e `TrackInspectorViewModel`. Proibida a criação de pipelines ou modelos paralelos.
- **Princípio II (Fatias Verticais)**: A implementação está organizada estritamente em **3 Fatias Verticais ponta a ponta**. Cada fatia repete o bloco META IMUTÁVEL e encerra com validação local de compilação e testes.
- **Princípio III (Validação da Solution)**: A fatia final valida a solution inteira com `dotnet restore`, `dotnet build --configuration Release -p:Platform=x64` e `dotnet test --no-build`.
- **Princípio IV (Local-First & Privacidade)**: Toda consulta externa é opcional e falha graciosamente em modo offline. O playback e navegação local continuam 100% funcionais sem internet. Nenhum dado de áudio é transmitido.
- **Princípio V (Conformidade com Provedor)**: Limite de taxa rigoroso de 1 requisição por segundo no MusicBrainz com cabeçalho oficial `User-Agent: Resonance/1.0 (+https://github.com/RaphaelAndradeM3/resonance-player)`.
- **Princípio VII (Fronteiras de Metadados)**: A gravação física de tags em arquivos de áudio em disco é ESTRITAMENTE PROIBIDA nesta feature (0 bytes gravados em arquivos de mídia). A gravação física é escopo exclusivo da Feature 006 com fluxo de Review, Diff e Confirmação.

---

## Slice 1: MusicBrainz Recording Lookup, Cover Art Archive & Cache

```markdown
## META IMUTÁVEL
Problem: O usuário precisa obter metadados canônicos completos (gravação, álbum, ano, faixas, disco, ISRC, gênero e arte de capa oficial) para uma música sem sobrecarregar a infraestrutura pública externa.
Definition of Success: O cliente MusicBrainzService consulta a gravação no MusicBrainz Web Service v2 utilizando rate limit de 1 req/s via IProviderPipelineProvider, aplica a heurística canônica de seleção de release oficial mais antigo (FR-009), resolve a capa oficial 500px via Cover Art Archive (FR-010), e persiste o resultado em cache local JSON em disco (FR-011) respondendo subsequentemente em < 15ms.
```

- [ ] T001 [P] [Slice1] Create `MusicBrainzLookupDtos.cs` in `src/Resonance.Core/Http/MusicBrainz/MusicBrainzLookupDtos.cs` with JSON deserialization classes: `MusicBrainzRecordingLookupResponse`, `MusicBrainzArtistCredit`, `MusicBrainzArtistRef`, `MusicBrainzReleaseLookupDto`, `MusicBrainzReleaseGroupDto`, `MusicBrainzMediaDto`, `MusicBrainzTrackDto`, `MusicBrainzLabelInfoDto`, `MusicBrainzTagDto`, `MusicBrainzRecordingSearchResponse`, and `MusicBrainzRecordingSearchResultDto`.
- [ ] T002 [P] [Slice1] Create `MusicBrainzRecordingDetail.cs` in `src/Resonance.Core/Models/MusicBrainzRecordingDetail.cs` with properties: `RecordingId` (string), `Title` (string), `Artist` (string), `ArtistId` (string?), `ReleaseId` (string?), `Album` (string?), `AlbumArtist` (string?), `Year` (int?), `ReleaseDate` (string?), `TrackNumber` (int?), `TotalTracks` (int?), `DiscNumber` (int?), `TotalDiscs` (int?), `Label` (string?), `Isrc` (string?), `Genres` (IReadOnlyList<string>), and `Score` (int).
- [ ] T003 [P] [Slice1] Expand `IMusicBrainzService.cs` in `src/Resonance.Core/Services/Abstractions/IMusicBrainzService.cs` declaring `Task<MusicBrainzRecordingDetail?> GetRecordingMetadataAsync(string recordingMbid, string? preferredAlbum = null, CancellationToken cancellationToken = default)`, `Task<MusicBrainzRecordingDetail?> SearchRecordingAsync(string artist, string title, string? preferredAlbum = null, CancellationToken cancellationToken = default)`, and `Task<string?> GetCoverArtUrlAsync(string releaseMbid, CancellationToken cancellationToken = default)`.
- [ ] T004 [P] [Slice1] Create unit tests in `tests/Resonance.Core.Tests/Services/MusicBrainzServiceTests.cs` using `TestHttpMessageHandler` simulating: successful recording lookup by MBID, canonical release selection heuristic (status Official, type Album, oldest date or local album match per FR-009), textual fallback search (artist + title), Cover Art Archive URL resolution (front-500 with front-250 fallback per FR-010), User-Agent header validation, 1 req/s rate limit respect, and disk JSON cache hit/expiration per FR-011.
- [ ] T005 [Slice1] Implement `GetRecordingMetadataAsync`, `SearchRecordingAsync`, and `GetCoverArtUrlAsync` in `src/Resonance.Core/Services/Implementations/MusicBrainzService.cs` utilizing `IProviderPipelineProvider` (`ServiceProviderIds.MusicBrainz`), applying canonical release selection heuristic (FR-009) and Cover Art Archive URL resolution with front-500 / front-250 fallback (FR-010).
- [ ] T006 [Slice1] Implement disk JSON caching in `MusicBrainzService.cs` storing cached envelopes under `IAppInfoService.CachePath/metadata/{hash}.json` with 7 days TTL (FR-011) to achieve < 15ms resolution on repeated queries without SQLite migrations.
- [ ] T007 [Slice1] Gate Slice 1: Validate build and test execution of MusicBrainz provider and cache layer:
  ```powershell
  dotnet test tests/Resonance.Core.Tests --filter FullyQualifiedName~MusicBrainzServiceTests
  ```

---

## Slice 2: Metadata Merge Engine & Provenance Model

```markdown
## META IMUTÁVEL
Problem: O usuário precisa comparar os metadados locais de uma música com as sugestões online campo a campo, com transparência total sobre a origem de cada informação e sem risco de apagar anotações locais.
Definition of Success: O serviço MetadataEnrichmentService compara a tag local com a resposta do MusicBrainz, gera um EnrichmentProposal completo onde cada campo possui seu status (Unchanged, Updated, NewValue, Conflict), proveniência (MetadataProvenance), seleção interativa inicial (IsSelected) e fusão semântica de gênero com deduplicação (FR-013), com zero gravação em disco.
```

- [ ] T008 [P] [Slice2] Create `MetadataProvenance.cs` in `src/Resonance.Core/Models/MetadataProvenance.cs` with enum values: `LocalTag = 0`, `MusicBrainz = 1`, `CoverArtArchive = 2`, and `UserOverride = 3`.
- [ ] T009 [P] [Slice2] Create `FieldProposalStatus.cs` in `src/Resonance.Core/Models/FieldProposalStatus.cs` with enum values: `Unchanged = 0`, `NewValue = 1`, `Updated = 2`, and `Conflict = 3`.
- [ ] T010 [P] [Slice2] Create `FieldProposal.cs` in `src/Resonance.Core/Models/FieldProposal.cs` with properties: `FieldName`, `FieldKey`, `CurrentValue`, `ProposedValue`, `Status`, `Provenance`, and `IsSelected` (bool, default true for NewValue/Updated, false for Unchanged/Conflict per FR-012).
- [ ] T011 [P] [Slice2] Create `EnrichmentProposal.cs` in `src/Resonance.Core/Models/EnrichmentProposal.cs` with properties: `SongId`, `FilePath`, `RecordingMbid`, `ReleaseMbid`, `Proposals` (IReadOnlyList<FieldProposal>), `OriginalCoverPath`, `ProposedCoverUrl`, `ProposedCoverThumbnailUrl`, `ConfidenceScore`, `CreatedAt`, and helper computed properties `SelectedCount` and `HasActionableChanges`.
- [ ] T012 [P] [Slice2] Create `IMetadataEnrichmentService.cs` in `src/Resonance.Core/Services/Abstractions/IMetadataEnrichmentService.cs` declaring `EnrichmentProposal CreateProposal(string filePath, TrackAudioTags localTags, MusicBrainzRecordingDetail remoteData, string? coverArtUrl = null, Guid? songId = null, string? originalCoverPath = null)` and `string MergeGenres(string? localGenre, IReadOnlyList<string>? remoteGenres)`.
- [ ] T013 [P] [Slice2] Create unit tests in `tests/Resonance.Core.Tests/Services/MetadataEnrichmentServiceTests.cs` verifying field comparisons (Title, Artist, Album, AlbumArtist, Year, TrackNumber, TotalTracks, DiscNumber, TotalDiscs, Label, Isrc, CoverArt), classification of `Unchanged`, `NewValue`, `Updated`, `Conflict`, default `IsSelected` states (FR-012), semantic genre merge with deduplication (FR-013), and preservation of local unmapped fields (comments, BPM).
- [ ] T014 [Slice2] Implement `MetadataEnrichmentService.cs` in `src/Resonance.Core/Services/Implementations/MetadataEnrichmentService.cs` implementing string normalization, field-by-field diff, default selection rules (FR-012), genre deduplication and concatenation with `;` (FR-013), and proposal aggregation.
- [ ] T015 [Slice2] Register `IMetadataEnrichmentService` singleton in `src/Resonance.WinUI/App.xaml.cs`.
- [ ] T016 [Slice2] Gate Slice 2: Validate build and test execution of metadata merge engine and provenance model:
  ```powershell
  dotnet test tests/Resonance.Core.Tests --filter FullyQualifiedName~MetadataEnrichmentServiceTests
  ```

---

## Slice 3: Track Inspector UI, Interactive Proposal & Regression

```markdown
## META IMUTÁVEL
Problem: O usuário precisa visualizar e interagir com a proposta de enriquecimento dentro do Track Inspector, selecionando quais campos deseja incorporar antes de encaminhar para a revisão/edição de tags, funcionando graciosamente offline.
Definition of Success: O Track Inspector disponibiliza card expansível de "Enriquecimento de Metadados Online", dispara a busca sob demanda com indicador de carregamento, renderiza a grade comparativa com checkboxes interativos por campo, exibe badges visuais de status e proveniência, mostra a capa oficial sugerida lado a lado com a capa local, e permite acionar "Avançar para Revisão de Tags" em memória, com zero erros na compilação Release e 100% dos testes passando.
```

- [ ] T017 [P] [Slice3] Expand `ITrackInspectorViewModel.cs` and `TrackInspectorViewModel.cs` in `src/Resonance.WinUI/ViewModels/` with observable properties: `EnrichmentProposal`, `IsEnriching`, `EnrichmentStatusText`, `HasEnrichmentProposal`, `ProposedCoverImageUri`, and commands `EnrichOnlineMetadataCommand` and `ClearEnrichmentProposalCommand`.
- [ ] T018 [Slice3] Implement enrichment workflow in `TrackInspectorViewModel.cs`: check if `CurrentData.ExternalIds.MusicBrainzTrackId` exists to call `GetRecordingMetadataAsync`; otherwise fallback to `SearchRecordingAsync` with `Tags.Artist` and `Tags.Title`; call `GetCoverArtUrlAsync`; execute `IMetadataEnrichmentService.CreateProposal`; populate `EnrichmentProposal` and handle offline/timeout errors gracefully.
- [ ] T019 [Slice3] Update `TrackInspectorControl.xaml` in `src/Resonance.WinUI/Controls/TrackInspectorControl.xaml` adding the "Enriquecimento de Metadados Online" Expander card with button "Buscar Metadados Online" / "Recarregar", indeterminate `ProgressBar`, comparison table with columns (Check, Campo, Valor Atual, Sugestão Online, Origem, Status), status badges (Novo: verde, Atualizado: amarelo, Conflito: laranja, Inalterado: neutro), provenance badges (`MusicBrainz`, `CoverArtArchive`), cover art preview (Local vs Sugerida), and button "Avançar para Revisão de Tags (Feature 006)".
- [ ] T020 [Slice3] Add "Buscar Metadados Online" menu item in song list context menus in `src/Resonance.WinUI/ViewModels/SongListViewModelBase.cs` and related views (`LibraryPage.xaml`, `AlbumViewPage.xaml`, `PlaylistSongViewPage.xaml`) linking to the inspector enrichment flow.
- [ ] T021 [P] [Slice3] Create ViewModel and integration tests in `tests/Resonance.Core.Tests/ViewModels/TrackInspectorViewModelTests.cs` validating `EnrichOnlineMetadataCommand` execution, fallback flow, interactive `IsSelected` toggling, and offline error presentation.
- [ ] T022 [P] [Slice3] Verify documentation integrity and cross-references across `spec.md`, `plan.md`, `data-model.md`, `contracts/`, and `quickstart.md`.
- [ ] T023 [Slice3] Execute quickstart validation scenarios described in `specs/005-online-metadata-enrichment/quickstart.md`.
- [ ] T024 [Slice3] Final Solution Gate: Validate whole-solution build and test gates in Release mode:
  ```powershell
  dotnet restore Resonance.slnx
  dotnet build Resonance.slnx --configuration Release -p:Platform=x64
  dotnet test Resonance.slnx --configuration Release --no-build
  ```

---

## Dependencies & Execution Order

### Vertical Slice Dependencies

- **Slice 1 (MusicBrainz Lookup, Cover Art Archive & Cache)**: Inicia imediatamente. Produz DTOs, métodos do `MusicBrainzService`, heurística de seleção de release, resolução de capa, cache JSON em disco e testes unitários.
- **Slice 2 (Metadata Merge Engine & Provenance Model)**: Depende dos modelos de dados e do `MusicBrainzRecordingDetail` da Slice 1. Produz motor de mesclagem puro, enums de proveniência/status, regras de gênero e testes de diff.
- **Slice 3 (Track Inspector UI & Regressão)**: Depende das Slices 1 e 2. Conecta os serviços ao `TrackInspectorViewModel`, `TrackInspectorControl.xaml`, menus de contexto da biblioteca e valida a solution inteira em Release x64 com 100% de aprovação.

### Parallel Opportunities

- Todas as tarefas marcadas com `[P]` atuam em arquivos independentes e podem ser implementadas em paralelo:
  - Slice 1: T001, T002, T003, T004
  - Slice 2: T008, T009, T010, T011, T012, T013
  - Slice 3: T017, T021, T022
