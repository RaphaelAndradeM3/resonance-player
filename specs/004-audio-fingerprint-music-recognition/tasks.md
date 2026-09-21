# Tasks: 004 — Audio Fingerprint & Music Recognition

**Feature Branch**: `004-audio-fingerprint-music-recognition`  
**Date**: 2026-09-21  
**Status**: Ready for Implementation  
**Spec Reference**: [spec.md](./spec.md)  
**Implementation Plan**: [plan.md](./plan.md)  
**Data Model**: [data-model.md](./data-model.md)  
**Contracts**: [contracts/](./contracts/)  
**Quickstart Guide**: [quickstart.md](./quickstart.md)  

---

## Constitution Execution Guardrails

- **Princípio I (Fonte da Verdade)**: Reutilização do FFmpeg existente, `IProviderPipelineProvider`, `TrackExternalIds`, `TrackInspectorViewModel`, `SettingsService` e `MusicDbContext`.
- **Princípio II (Fatias Verticais)**: A implementação está dividida estritamente em **3 Fatias Verticais ponta a ponta**. Cada fatia repete o bloco META IMUTÁVEL e encerra com validação de build/testes.
- **Princípio III (Validação da Solution)**: A fatia final valida a solution inteira com `dotnet restore`, `dotnet build --configuration Release` e `dotnet test --no-build`.
- **Princípio IV (Local-First & Privacidade)**: O áudio é decodificado e o Chromaprint é calculado 100% localmente. Apenas a string do fingerprint e a duração são transmitidas ao AcoustID.
- **Princípio VII (Limites de Metadados)**: A gravação física de tags em arquivos de áudio é proibida (escopo da Feature 006). Apenas identificadores e metadados de sessão são vinculados.

---

## Slice 1: Local Fingerprint Engine (FFmpeg & Chromaprint Pipeline)

```markdown
## META IMUTÁVEL
Problem: Arquivos de áudio na biblioteca frequentemente estão sem tags ou com nomes genéricos. O usuário precisa extrair sua impressão digital acústica de forma rápida, determinística e 100% local.
Definition of Success: A aplicação executa o muxer chromaprint nativo do FFmpeg localmente, gera o hash Base64 e duração da faixa em menos de 1,5s, persiste o valor no SQLite local (Song.AcousticFingerprint) para evitar redecodificação futura, e opera sem enviar nenhum dado sonoro pela rede.
```

- [X] T001 [P] [Slice1] Create `AcousticFingerprint.cs` in `src/Resonance.Core/Models/AcousticFingerprint.cs` with properties: `Hash` (string Base64), `DurationSeconds` (int), `Algorithm` (int, default 1), and `IsValid` (bool).
- [X] T002 [Slice1] Add `AcousticFingerprint` (string?, null) and `AcoustId` (string?, MaxLength 100) properties in `src/Resonance.Core/Models/Song.cs`.
- [X] T003 [Slice1] Create EF Core migration `20260921_AddAcousticFingerprintAndAcoustIdToSong.cs` and update model snapshot in `src/Resonance.Core/Data/Migrations/` adding columns `AcousticFingerprint` and `AcoustId` to table `Songs`.
- [X] T004 [P] [Slice1] Create `IFingerprintService.cs` in `src/Resonance.Core/Services/Abstractions/IFingerprintService.cs` declaring `Task<AcousticFingerprint?> GenerateFingerprintAsync(string filePath, CancellationToken cancellationToken = default)`.
- [X] T005 [P] [Slice1] Create unit tests in `tests/Resonance.Core.Tests/Services/FingerprintServiceTests.cs` verifying FFmpeg chromaprint extraction with synthetic audio, silent audio handling, short audio handling (< 10s), and cancellation handling.
- [X] T006 [Slice1] Implement `FFmpegFingerprintService.cs` in `src/Resonance.Core/Services/Implementations/FFmpegFingerprintService.cs` executing `ffmpeg -v error -nostdin -i <file> -t 120 -f chromaprint -fp_format base64 pipe:1` asynchronously with process cancellation, duration parsing, and detection of short audio (< 10s).
- [X] T007 [Slice1] Register `IFingerprintService` singleton in `src/Resonance.WinUI/App.xaml.cs`.
- [X] T008 [Slice1] Gate Slice 1: Validate build and test execution of the local fingerprint engine:
  ```powershell
  dotnet test tests/Resonance.Core.Tests --filter FullyQualifiedName~FingerprintServiceTests
  ```

---

## Slice 2: AcoustID Provider, Rate Limiting & Recognition Service

```markdown
## META IMUTÁVEL
Problem: O usuário precisa consultar o catálogo comunitário do AcoustID de forma segura e confiável, respeitando termos de uso, limites de requisições e privacidade.
Definition of Success: O cliente AcoustIdService executa requisições na API v2 com rate limiting estrito de 3 req/s via IProviderPipelineProvider, suporte a chave padrão e chave personalizada do usuário em Configurações, cache de sessão em memória, retornando até 5 candidatos com score >= 40% (destaque para >= 80%) associados a IDs do MusicBrainz.
```

- [X] T009 [P] [Slice2] Create `RecognitionCandidate.cs` in `src/Resonance.Core/Models/RecognitionCandidate.cs` with properties: `AcoustId` (string?), `MusicBrainzTrackId` (string), `MusicBrainzReleaseId` (string?), `MusicBrainzArtistId` (string?), `Title` (string), `Artist` (string), `Album` (string?), `Year` (int?), `ConfidenceScore` (double, 0.0 to 1.0), `ConfidencePercentage` (int, 0 to 100), and `IsHighConfidence` (bool, score >= 0.80).
- [X] T010 [P] [Slice2] Create `RecognitionResult.cs` in `src/Resonance.Core/Models/RecognitionResult.cs` with enum `RecognitionStatus` (`Success`, `NoMatchFound`, `OfflineOrDisabled`, `RateLimited`, `NetworkError`, `AudioTooShort`, `AnalysisFailed`) and properties: `Status`, `Candidates`, `Fingerprint`, `ErrorMessage`, and `HasCandidates`.
- [X] T011 [P] [Slice2] Create `AcoustIdDtos.cs` in `src/Resonance.Core/Http/AcoustId/AcoustIdDtos.cs` with JSON deserialization classes: `AcoustIdLookupResponse`, `AcoustIdLookupResult`, `AcoustIdRecording`, `AcoustIdArtist`, `AcoustIdReleaseGroup`, and `AcoustIdError`.
- [X] T012 [P] [Slice2] Add `ServiceProviderIds.AcoustId = "acoustid"` constant in `src/Resonance.Core/Models/ServiceProviderIds.cs`.
- [X] T013 [P] [Slice2] Create `IAcoustIdService.cs` in `src/Resonance.Core/Services/Abstractions/IAcoustIdService.cs` declaring `Task<RecognitionResult> LookupAsync(AcousticFingerprint fingerprint, CancellationToken cancellationToken = default)` and `Task<bool> IsEnabledAsync()`.
- [X] T014 [Slice2] Configure `ServiceProviderIds.AcoustId` policy in `src/Resonance.WinUI/App.xaml.cs` inside `AddProviderPipelines` with `PermitsPerWindow = 3`, `Window = 1s`, `MaxConcurrent = 2`, `MaxRetries = 3`, `BaseRetryDelay = 2s`, and `MaxRetryDelay = 10s`.
- [X] T015 [Slice2] Register AcoustID provider in `SettingsService.cs` (in `DefaultProviders` as `ServiceCategory.Metadata`) and add configuration options in `SettingsViewModel.cs` and `SettingsPage.xaml` allowing the user to enable/disable AcoustID and enter an optional custom API key.
- [X] T016 [P] [Slice2] Create unit tests in `tests/Resonance.Core.Tests/Services/AcoustIdServiceTests.cs` using `TestHttpMessageHandler` simulating successful lookups, candidate ranking, cutoff of scores < 40%, evaluation of `IsHighConfidence` (>= 80%), rate limit 429 retry backoff, disabled provider response, in-memory cache hits, and request inspection verifying zero raw audio bytes are sent.
- [X] T017 [Slice2] Implement `AcoustIdService.cs` in `src/Resonance.Core/Services/Implementations/AcoustIdService.cs` utilizing `IProviderPipelineProvider`, `IHttpClientFactory`, `IApiKeyService`, and `ISettingsService`, querying `https://api.acoustid.org/v2/lookup`, filtering candidates with `ConfidenceScore >= 0.40`, ordering by score descending (max 5 items), and caching responses in-memory by `(Hash, DurationSeconds)`.
- [X] T018 [Slice2] Register `IAcoustIdService` singleton in `src/Resonance.WinUI/App.xaml.cs`.
- [X] T019 [Slice2] Gate Slice 2: Validate build and test execution of AcoustID provider and resilience layer:
  ```powershell
  dotnet test tests/Resonance.Core.Tests --filter FullyQualifiedName~AcoustIdServiceTests
  ```

---

## Slice 3: Track Inspector UI, Candidate Selection & Integration

```markdown
## META IMUTÁVEL
Problem: O usuário precisa disparar o reconhecimento de áudio no Track Inspector ou na biblioteca, acompanhar a análise visualmente, inspecionar os candidatos com seus scores de confiança e vincular os identificadores à faixa sem alterar o arquivo em disco.
Definition of Success: O Track Inspector exibe card de "Fingerprint & Reconhecimento Acústico" com indicador de progresso, status da faixa, lista de candidatos com percentuais de confiança, ação de vincular identificadores (MusicBrainz e AcoustID) à faixa e persistir no banco, além de opção no menu de contexto das listas de músicas.
```

- [ ] T020 [P] [Slice3] Expand `src/Resonance.WinUI/ViewModels/TrackInspectorViewModel.cs` adding observable properties (`IsRecognizing`, `RecognitionStatusText`, `RecognitionCandidates`, `FingerprintHash`, `IsAlreadyIdentified`) and commands (`IdentifyTrackCommand`, `ReidentifyTrackCommand`, `SelectCandidateCommand`, `DiscardCandidatesCommand`).
- [ ] T021 [Slice3] Implement identification workflow in `TrackInspectorViewModel.cs`: check if `Song.AcousticFingerprint` already exists in database before invoking FFmpeg; execute `IFingerprintService` if null and persist `Song.AcousticFingerprint` immediately via `MusicDbContext`; then invoke `IAcoustIdService` and populate `RecognitionCandidates`.
- [ ] T022 [Slice3] Implement `SelectCandidateCommand` and `DiscardCandidatesCommand` in `TrackInspectorViewModel.cs`: associate `MusicBrainzTrackId` and `AcoustId` to `CurrentData.ExternalIds`, update `Song.AcoustId` in `MusicDbContext`, and update suggested tags in `CurrentData.Tags` without modifying physical files on disk.
- [ ] T023 [Slice3] Update `src/Resonance.WinUI/Controls/TrackInspectorControl.xaml` adding the "Fingerprint & Reconhecimento Acústico" Expander card with track status badge ("Já Identificada" / "Não Identificada"), copy hash button, progress bar (`ProgressBar IsIndeterminate="True"`), candidate list with confidence badges (purple highlight for >= 80%), and "Vincular" buttons.
- [ ] T024 [Slice3] Add "Identificar Música via Áudio" menu flyout item in library song list context menus in `src/Resonance.WinUI/Views/SongsView.xaml` linking to `IdentifyTrackCommand`.
- [ ] T025 [P] [Slice3] Verify documentation integrity and cross-references across `spec.md`, `plan.md`, `data-model.md`, `contracts/`, and `quickstart.md`.
- [ ] T026 [Slice3] Execute quickstart validation scenarios described in `specs/004-audio-fingerprint-music-recognition/quickstart.md`.
- [ ] T027 [Slice3] Final Solution Gate: Validate whole-solution build and test gates:
  ```powershell
  dotnet restore Resonance.slnx
  dotnet build Resonance.slnx --configuration Release
  dotnet test Resonance.slnx --configuration Release --no-build
  ```

---

## Dependencies & Execution Order

### Vertical Slice Dependencies

- **Slice 1 (Local Fingerprint Engine)**: Inicia imediatamente. Produz modelo `AcousticFingerprint`, migração do banco SQLite, implementação do `FFmpegFingerprintService` e testes unitários locais.
- **Slice 2 (AcoustID Provider & Resiliência)**: Depende do modelo `AcousticFingerprint` da Slice 1. Produz cliente HTTP, DTOs, rate limiter, cache, settings de provedor e testes com mocks.
- **Slice 3 (Track Inspector UI & Integração)**: Depende das Slices 1 e 2. Integra os serviços ao `TrackInspectorViewModel`, `TrackInspectorControl.xaml` e menus da biblioteca, encerrando com os gates globais de compilação e testes da solution.

### Parallel Opportunities

- Todas as tarefas marcadas com `[P]` operam em arquivos isolados e podem ser desenvolvidas em paralelo:
  - Slice 1: T001, T004, T005
  - Slice 2: T009, T010, T011, T012, T013, T016
  - Slice 3: T020, T025
