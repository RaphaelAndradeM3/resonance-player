# Tasks: 007 — Lyrics Engine

**Feature Branch**: `007-lyrics-engine`  
**Date**: 2026-09-23  
**Status**: Ready for Implementation  
**Spec Reference**: [spec.md](./spec.md)  
**Implementation Plan**: [plan.md](./plan.md)  
**Data Model**: [data-model.md](./data-model.md)  
**Contracts**: [contracts/](./contracts/)  
**Quickstart Guide**: [quickstart.md](./quickstart.md)  
**Quality Checklist**: [checklists/quality.md](./checklists/quality.md)  

---

## Constitution Execution Guardrails

- **Princípio I (Fonte da Verdade)**: Reutilização estrita da biblioteca ATL.NET (`ATL.Track`, `LyricsInfo`), `ILrcService`, `LrcService`, `IOnlineLyricsService`, `LyricsPageViewModel` e `LyricsPage.xaml`. Proibida a introdução de bibliotecas paralelas ou pipelines redundantes de letras.
- **Princípio II (Fatias Verticais)**: A implementação está organizada estritamente em **3 Fatias Verticais ponta a ponta**. Cada fatia repete o bloco META IMUTÁVEL e encerra com validação local de compilação e testes.
- **Princípio III (Validação da Solution)**: A fatia final valida a solution inteira com `dotnet restore Resonance.slnx`, `dotnet build Resonance.slnx --configuration Release -p:Platform=x64 --warnaserror` e `dotnet test Resonance.slnx --configuration Release -p:Platform=x64 --no-build`.
- **Princípio IV (Local-First & Privacidade)**: O playback e resolução de letras locais funcionam 100% offline. Zero chamadas de rede são feitas se houver letra local (embutida ou sidecar). Provedores remotos são opcionais e respeitam rate limits e cache isolado.
- **Princípio VII (Fronteiras Explícitas)**: Provedores remotos gravam exclusivamente no cache interno da aplicação (`%LocalAppData%`). Gravação de arquivo sidecar na pasta da música ocorre unicamente sob comando explícito do usuário ("Exportar como .lrc").

---

## Slice 1: Local Lyrics Resolution & Parser (Core Engine — US2)

```markdown
## META IMUTÁVEL
Problem: Resolver e parsear letras locais (embutidas via ATL e sidecars .lrc/.txt) seguindo estritamente a precedência canônica local (Embedded Synced -> Embedded Plain -> Sidecar .lrc -> Sidecar .txt), sem realizar chamadas externas quando houver dado local.
Definition of Success: A engine identifica e parseia corretamente letras embutidas e sidecars com casamento por nome base (<NomeDoAudio>.lrc/.txt) e fallback secundário (<Artista> - <Título>.lrc/.txt), indicando proveniência e tipo sem bloquear a reprodução, com 100% dos testes unitários de parsing e precedência passando.
```

- [X] T001 [P] [US2] Create `LyricsProvenance.cs` in `src/Resonance.Core/Models/Lyrics/LyricsProvenance.cs` with enum values: `None`, `EmbeddedSynced`, `EmbeddedPlain`, `LocalFileLrc`, `LocalFileTxt`, `LocalCache`, `RemoteLrcLib`, `RemoteNetEase`.
- [X] T002 [P] [US2] Create `LyricsType.cs` in `src/Resonance.Core/Models/Lyrics/LyricsType.cs` with enum values: `None`, `Synced`, `Plain`, `Instrumental`.
- [X] T003 [P] [US2] Create `LyricsDocument.cs` in `src/Resonance.Core/Models/Lyrics/LyricsDocument.cs` with properties: `Lines` (IReadOnlyList<LyricLine>), `RawUnsyncedLyrics` (string?), `Type` (LyricsType), `Provenance` (LyricsProvenance), `SourcePath` (string?), `TimeOffset` (TimeSpan), `IsInstrumental` (bool), `IsEmpty` (bool), and static factories `Empty` and `CreateInstrumental`.
- [X] T004 [P] [US2] Update `ILrcService.cs` in `src/Resonance.Core/Services/Abstractions/ILrcService.cs` declaring `Task<LyricsDocument?> ResolveLyricsAsync(Song song, CancellationToken cancellationToken = default)`, `Task<bool> ExportSidecarLrcAsync(Song song, string lrcContent)`, and `Task SetLyricsOffsetAsync(Song song, int offsetMs)` while preserving all legacy methods.
- [X] T005 [P] [US2] Create unit tests in `tests/Resonance.Core.Tests/Services/LrcServiceLocalResolutionTests.cs` verifying the local resolution precedence: embedded synced vs embedded plain vs sidecar `.lrc` vs sidecar `.txt`, matching by exact `<AudioBaseName>.lrc/.txt`, fallback to `<Artist> - <Title>.lrc/.txt`, and zero network requests when local lyrics exist.
- [X] T006 [US2] Implement embedded lyrics extraction via `ATL.Track` in `src/Resonance.Core/Services/Implementations/LrcService.cs` reading `SynchronizedLyrics` (converting to `LyricLine`s with millisecond timestamps) and `UnsynchronizedLyrics`.
- [X] T007 [US2] Implement local sidecar file resolution in `src/Resonance.Core/Services/Implementations/LrcService.cs` checking exact audio file name (`<NomeDoAudio>.lrc/.txt`) and secondary fallback (`<Artista> - <Título>.lrc/.txt`) in the audio file directory (case-insensitive).
- [X] T008 [US2] Implement `ResolveLyricsAsync` stages 1 through 4 in `src/Resonance.Core/Services/Implementations/LrcService.cs` returning a populated `LyricsDocument` with appropriate `LyricsProvenance` and `LyricsType`.
- [X] T009 [US2] Gate Slice 1: Validate build and test execution of local lyrics resolution engine:
  ```powershell
  dotnet test tests/Resonance.Core.Tests/Resonance.Core.Tests.csproj --filter "FullyQualifiedName~LrcServiceLocalResolutionTests"
  ```

---

## Slice 2: Remote Provider, Cache Layer & Explicit Export (Infra & Cache — US3)

```markdown
## META IMUTÁVEL
Problem: Consultar provedores online opcionais somente quando não houver letra local, armazenar respostas em cache isolado em %LocalAppData%, memorizar faixas instrumentais no banco e permitir exportar .lrc sob demanda.
Definition of Success: Faixas sem letra consultam LRCLIB/NetEase de forma assíncrona; cache armazena letras com hash de identidade; faixas instrumentais registram flag no banco; ação de exportação grava .lrc na pasta do áudio sem poluir arquivos sem autorização do usuário.
```

- [ ] T010 [P] [US3] Add `IsInstrumental` (`bool?`) and `LyricsOffsetMs` (`int?`) properties to `Song.cs` in `src/Resonance.Core/Models/Song.cs`.
- [ ] T011 [P] [US3] Create EF Core migration `AddLyricsInstrumentalAndOffsetToSong` in `src/Resonance.Core/Data/Migrations/` adding `IsInstrumental` and `LyricsOffsetMs` columns to the `Songs` table and updating `MusicDbContextModelSnapshot.cs`.
- [ ] T012 [P] [US3] Update `ILibraryWriter.cs` in `src/Resonance.Core/Services/Abstractions/ILibraryWriter.cs` declaring `Task UpdateSongLyricsOffsetAsync(Guid songId, int? offsetMs)` and `Task UpdateSongInstrumentalAsync(Guid songId, bool isInstrumental)`.
- [ ] T013 [US3] Implement `UpdateSongLyricsOffsetAsync` and `UpdateSongInstrumentalAsync` in `src/Resonance.Core/Services/Implementations/LibraryService.cs` updating the SQLite database records.
- [ ] T014 [US3] Update `LrcLibService.cs` in `src/Resonance.Core/Services/Implementations/LrcLibService.cs` to capture `instrumental: true` from LRCLIB JSON responses and return typed indication of instrumental/plain lyrics.
- [ ] T015 [US3] Implement stage 5 (internal cache in `%LocalAppData%`) and stage 6 (remote providers with `instrumental: true` handling and persistence of `LyricsLastCheckedUtc` and `IsInstrumental` in SQLite) in `src/Resonance.Core/Services/Implementations/LrcService.cs`.
- [ ] T016 [US3] Implement `ExportSidecarLrcAsync` in `src/Resonance.Core/Services/Implementations/LrcService.cs` formatting and writing `<NomeDoAudio>.lrc` directly to the song's directory via `IFileSystemService`.
- [ ] T017 [P] [US3] Create integration tests in `tests/Resonance.Core.Tests/Services/LrcServiceRemoteAndExportTests.cs` testing LRCLIB responses (synced, plain, instrumental), cache persistence in `%LocalAppData%`, avoidance of redundant lookups, and sidecar export.
- [ ] T018 [US3] Gate Slice 2: Validate build and test execution of remote provider, cache and export engine:
  ```powershell
  dotnet test tests/Resonance.Core.Tests/Resonance.Core.Tests.csproj --filter "FullyQualifiedName~LrcServiceRemoteAndExportTests|FullyQualifiedName~LrcLibServiceTests"
  ```

---

## Slice 3: Synchronized Lyrics UI, Offset Controls & Full Regression (UI & Playback — US1)

```markdown
## META IMUTÁVEL
Problem: Renderizar as letras na tela com destaque sincronizado da linha ativa, badge de proveniência visível, modo texto puro com seek desativado, mensagem musical para instrumental, controles táteis de offset (+/- 100ms/500ms) e botão de exportação.
Definition of Success: A página de letras (LyricsPage) exibe em tempo real o texto correto com rolagem suave, permite ajustar o offset no momento da reprodução memorizando a calibração, e exibe estados vazios e de texto puro com clareza, com a solution inteira compilando e 100% dos testes passando.
```

- [ ] T019 [P] [US1] Update `LyricsPageViewModel.cs` in `src/Resonance.WinUI/ViewModels/LyricsPageViewModel.cs` adding observable properties: `ProvenanceLabel` (string), `IsSynced` (bool), `IsPlain` (bool), `IsInstrumental` (bool), `CurrentOffsetMs` (int), `CanExportLrc` (bool), and commands `AdjustOffsetCommand`, `ResetOffsetCommand`, and `ExportSidecarLrcCommand`.
- [ ] T020 [US1] Update `UpdateForTrack` in `src/Resonance.WinUI/ViewModels/LyricsPageViewModel.cs` to call `ResolveLyricsAsync`, apply saved `LyricsOffsetMs` from `Song`, and populate synced vs unsynced line collections with provenance badges.
- [ ] T021 [US1] Implement live offset calibration logic in `src/Resonance.WinUI/ViewModels/LyricsPageViewModel.cs` shifting active playback time during highlight calculation and persisting calibrated offset via `ILrcService.SetLyricsOffsetAsync`.
- [ ] T022 [US1] Implement `ExportSidecarLrcCommand` in `src/Resonance.WinUI/ViewModels/LyricsPageViewModel.cs` invoking `ILrcService.ExportSidecarLrcAsync` and triggering visual confirmation.
- [ ] T023 [P] [US1] Update `LyricsPage.xaml` in `src/Resonance.WinUI/Pages/LyricsPage.xaml` adding provenance badge in header, `[Não Sincronizada]` indicator on plain view, instrumental empty state panel (`♫ Faixa Instrumental`), offset adjustment controls (`-500ms`, `-100ms`, `+100ms`, `+500ms`, `Zerar`), and "Exportar como .lrc" button.
- [ ] T024 [US1] Update `LyricsPage.xaml.cs` in `src/Resonance.WinUI/Pages/LyricsPage.xaml.cs` ensuring smooth auto-scrolling respects offset calibration and plain text ListView disables seek taps.
- [ ] T025 [US1] Whole-Solution Regression Gate: Validate entire solution build, packaging, analyzers, and all tests passing:
  ```powershell
  dotnet build Resonance.slnx --configuration Release -p:Platform=x64 --warnaserror
  dotnet test Resonance.slnx --configuration Release -p:Platform=x64 --no-build
  ```

---

## Dependencies & Execution Order

### Slice Dependencies

- **Slice 1 (Local Resolution & Parser)**: Foundational — sem dependência de rede, estabelece `LyricsDocument`, `LyricsProvenance` e extração local ATL / sidecar.
- **Slice 2 (Remote Provider, Cache & Export)**: Depende dos modelos da Slice 1. Adiciona migration de banco, integração remota LRCLIB e exportação.
- **Slice 3 (Synchronized Lyrics UI & Offset)**: Depende dos serviços da Slice 1 e Slice 2. Integra ViewModel, XAML, controles táteis e validação final da solution.

### Parallel Opportunities [P]

- `T001`, `T002`, `T003`, `T004`, `T005` na Slice 1 podem ser iniciadas em paralelo (arquivos distintos).
- `T010`, `T011`, `T012`, `T017` na Slice 2 podem ser iniciadas em paralelo.
- `T019` e `T023` na Slice 3 podem ser iniciadas em paralelo (ViewModel vs XAML).

---

## Format & Quality Validation

- [x] Todas as tarefas iniciam com `- [ ] T###`
- [x] Todas as tarefas de fatias de usuário incluem o rótulo da história (`[US1]`, `[US2]`, `[US3]`)
- [x] Todas as tarefas especificam caminhos absolutos ou relativos exatos de arquivos
- [x] Cada fatia vertical repete o cabeçalho imutável `## META IMUTÁVEL` conforme a Constituição
- [x] O número total de fatias é rigorosamente 3 (Princípio Constitucional II)
- [x] A última fatia inclui o gate de validação mandatória de toda a solution (Princípio Constitucional III)
