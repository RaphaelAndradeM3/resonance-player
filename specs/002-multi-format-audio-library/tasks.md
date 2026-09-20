# Tasks: Feature 002 — Multi-Format Audio Library

**Branch**: `002-multi-format-audio-library` | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)  

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Inicialização do ambiente da feature e criação de utilitários de teste para arquivos multi-formato sintéticos.

- [x] T001 Validar integridade da solution no branch `002-multi-format-audio-library` executando compilação em `Resonance.sln`
- [x] T002 [P] Implementar fixture auxiliar para criação de arquivos sintéticos multi-formato e arquivos truncados/vazios em `tests/Resonance.Core.Tests/Utils/AudioFormatTestFixture.cs`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Alinhar a autoridade única de extensões canônicas e implementar o registro central de formatos que bloqueia as User Stories.

**⚠️ CRITICAL**: Nenhuma User Story pode avançar sem a autoridade única de extensões atualizada e o helper de formatos disponível.

- [x] T003 [P] Atualizar a autoridade canônica `FileExtensions.MusicFileExtensions` em `src/Resonance.Core/Constants/FileExtensions.cs` incluindo a extensão DSD `.dff` conforme especificação
- [x] T004 [P] Implementar o helper estático `AudioFormatRegistry` e enum `AudioCodecCategory` em `src/Resonance.Core/Helpers/AudioFormatRegistry.cs` para consulta O(1) de formatos, categorias e nomes de exibição
- [x] T005 [P] Escrever testes unitários para a lista canônica e para o helper de formatos em `tests/Resonance.Core.Tests/FileExtensionsTests.cs` e `tests/Resonance.Core.Tests/AudioFormatRegistryTests.cs`

**Checkpoint**: Fundação canônica pronta e testada de forma isolada.

---

## Phase 3: User Story 1 - Fonte Única de Formatos e Matriz de Capacidades (Priority: P1) 🎯 MVP [Slice 1]

```markdown
## META IMUTÁVEL

Problem:
A biblioteca deve lidar corretamente com todos os formatos de áudio realmente suportados pelo Nagi/LibVLC e não apenas MP3, evitando divergência entre o que o scanner aceita e o que o player consegue tocar.

Definition of Success:
MP3, FLAC, WAV e demais formatos suportados pela baseline são descobertos, identificados e reproduzidos conforme a capability real da aplicação; arquivos incompatíveis são reportados de forma explícita.

Regra de Ouro:
Não manter listas duplicadas de extensões se a codebase já possui fonte de capability.
```

**Goal**: Garantir que todos os formatos de áudio da baseline (MP3, FLAC, WAV, AAC, M4A, OGG, Opus, WMA, AIFF, APE, WavPack e DSD) sejam reconhecidos pelo scanner e pela engine de extração de metadados sem omissões ou listas divergentes.

**Independent Test**: Execução dos testes automatizados de `FormatCapabilityTests` validando a matriz de formatos contra `FileExtensions.MusicFileExtensions`.

### Tests for User Story 1
- [ ] T006 [P] [US1] Escrever testes em `tests/Resonance.Core.Tests/FormatCapabilityTests.cs` validando a matriz de capacidade para todos os formatos mandatados da baseline

### Implementation for User Story 1
- [ ] T007 [US1] Atualizar `Package.appxmanifest` em `src/Resonance.WinUI/Package.appxmanifest` para registrar a extensão de arquivo `.dff` em conformidade com `FileExtensions`
- [ ] T008 [US1] Executar e validar aprovação de testes via `dotnet test tests/Resonance.Core.Tests/Resonance.Core.Tests.csproj --filter "FullyQualifiedName~FormatCapability|FullyQualifiedName~FileExtensions" --no-build`

**Checkpoint**: Fonte única e matriz de formatos plenamente validadas de forma independente.

---

## Phase 4: User Story 2 - Varredura Resiliente e Isolamento de Arquivos Corrompidos (Priority: P2) [Slice 2]

```markdown
## META IMUTÁVEL

Problem:
A biblioteca deve lidar corretamente com todos os formatos de áudio realmente suportados pelo Nagi/LibVLC e não apenas MP3, evitando divergência entre o que o scanner aceita e o que o player consegue tocar.

Definition of Success:
MP3, FLAC, WAV e demais formatos suportados pela baseline são descobertos, identificados e reproduzidos conforme a capability real da aplicação; arquivos incompatíveis são reportados de forma explícita.

Regra de Ouro:
Não manter listas duplicadas de extensões se a codebase já possui fonte de capability.
```

**Goal**: Identificar precocemente arquivos de 0 bytes e com cabeçalhos corrompidos no `AtlMetadataService`, marcando `ExtractionFailed = true` com códigos específicos (`EmptyFile`, `CorruptFile`, `UnsupportedFormat`) e garantindo que o `LibraryService` não insira arquivos defeituosos no banco SQLite nem aborte o scan.

**Independent Test**: Execução dos testes em `FormatResilienceTests` comprovando que o scanner reporta falha graciosa e continua processando os arquivos válidos.

### Tests for User Story 2
- [ ] T009 [P] [US2] Escrever testes unitários em `tests/Resonance.Core.Tests/FormatResilienceTests.cs` simulando arquivos vazios (0 bytes), cabeçalhos truncados e extensões falsas

### Implementation for User Story 2
- [ ] T010 [US2] Atualizar `AtlMetadataService.cs` em `src/Resonance.Core/Services/Implementations/AtlMetadataService.cs` com validação antecipada de arquivos 0 bytes (`EmptyFile`) e isolamento estrito de falhas de leitura do formato (`AudioFormat.Readable == false`)
- [ ] T011 [US2] Assegurar em `LibraryService.cs` em `src/Resonance.Core/Services/Implementations/LibraryService.cs` que arquivos com falha de extração permanente não sejam salvos no SQLite e alimentem o resumo de auditoria
- [ ] T012 [US2] Executar e validar aprovação dos testes de resiliência via `dotnet test tests/Resonance.Core.Tests/Resonance.Core.Tests.csproj --filter "FullyQualifiedName~FormatResilience" --no-build`

**Checkpoint**: Varredura resiliente, isolamento de arquivos vazios/corrompidos e proteção do banco de dados SQLite validados.

---

## Phase 5: User Story 3 - Reprodução Homogênea no Player e Transparência na UI (Priority: P3) [Slice 3]

```markdown
## META IMUTÁVEL

Problem:
A biblioteca deve lidar corretamente com todos os formatos de áudio realmente suportados pelo Nagi/LibVLC e não apenas MP3, evitando divergência entre o que o scanner aceita e o que o player consegue tocar.

Definition of Success:
MP3, FLAC, WAV e demais formatos suportados pela baseline são descobertos, identificados e reproduzidos conforme a capability real da aplicação; arquivos incompatíveis são reportados de forma explícita.

Regra de Ouro:
Não manter listas duplicadas de extensões se a codebase já possui fonte de capability.
```

**Goal**: Mapear hints de formato e demuxers nativos do LibVLC para 100% das extensões registradas e garantir tratamento não fatal em caso de falha de decodificação.

**Independent Test**: Execução dos testes de paridade em `LibVlcFormatMappingTests` cobrindo todas as extensões do `MusicFileExtensions` contra `LibVlcAudioPlayerService.GetAvFormatHint` e `UsesNativeDemuxer`.

### Tests for User Story 3
- [ ] T013 [P] [US3] Escrever testes em `tests/Resonance.Core.Tests/LibVlcFormatMappingTests.cs` validando que toda extensão em `MusicFileExtensions` possui hint de formato adequado ou demuxer nativo mapeado

### Implementation for User Story 3
- [ ] T014 [US3] Atualizar `GetAvFormatHint` em `src/Resonance.WinUI/Services/Implementations/LibVlcAudioPlayerService.cs` mapeando `.dff` para demuxer DSD e verificando paridade com `MusicFileExtensions`
- [ ] T015 [US3] Executar e validar testes de paridade de reprodução via `dotnet test tests/Resonance.Core.Tests/Resonance.Core.Tests.csproj --filter "FullyQualifiedName~LibVlcFormatMapping" --no-build`

**Checkpoint**: Paridade total de reprodução comprovada entre Scanner, Tags e Player LibVLC.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Verificação de consistência, testes de regressão de ponta a ponta e gate constitucional.

- [ ] T016 [P] Atualizar links cruzados entre `spec.md`, `plan.md`, `research.md`, `data-model.md`, `quickstart.md` e contratos em `specs/002-multi-format-audio-library/`
- [ ] T017 Executar Whole-Solution Validation completa via `dotnet build Resonance.sln -p:Platform=x64` e `dotnet test tests/Resonance.Core.Tests/Resonance.Core.Tests.csproj` comprovando 0 erros e 100% de testes aprovados
- [ ] T018 Executar auditoria de qualidade do checklist `specs/002-multi-format-audio-library/checklists/quality-gate.md` garantindo conformidade constitucional

---

## Dependencies & Execution Order

### Phase Dependencies
- **Setup (Phase 1)**: Sem dependências — inicia imediatamente.
- **Foundational (Phase 2)**: Depende da Phase 1 — BLOQUEIA as User Stories.
- **User Story 1 (P1 - MVP)**: Inicia após Foundational; foca na matriz de formatos e autoridade canônica.
- **User Story 2 (P2)**: Inicia após US1; foca na resiliência a arquivos corrompidos e de 0 bytes.
- **User Story 3 (P3)**: Inicia após US2; foca no alinhamento do reprodutor LibVLC e paridade de hints.
- **Polish (Phase 6)**: Executada após o término das três user stories.

### User Story Dependencies
- **User Story 1 (P1)**: Independente de US2 e US3.
- **User Story 2 (P2)**: Consome as extensões canônicas validadas na US1.
- **User Story 3 (P3)**: Consome a integridade de arquivos e o registro canônico da US1 e US2.

### Parallel Opportunities
- Tarefas marcadas com `[P]` (`T002`, `T003`, `T004`, `T005`, `T006`, `T009`, `T013`, `T016`) podem ser implementadas em paralelo por operarem em arquivos independentes sem dependências mútuas.

---

## Implementation Strategy

### MVP First (User Story 1 Only)
1. Completar Setup e Foundational (`T001` a `T005`).
2. Implementar User Story 1 (`T006` a `T008`): Extensão `.dff`, registro em AppxManifest e testes de matriz de formatos.
3. **STOP and VALIDATE**: Executar testes unitários de extensões e garantir autoridade canônica.

### Incremental Delivery (Slices 2 e 3)
1. Concluir User Story 2 (`T009` a `T012`): Detecção de arquivos vazios/corrompidos no `AtlMetadataService` e proteção do SQLite no `LibraryService`.
2. Concluir User Story 3 (`T013` a `T015`): Alinhamento de hints e demuxers no `LibVlcAudioPlayerService` com testes de paridade.
3. Concluir Polish & Quality Gate (`T016` a `T018`): Whole-Solution Validation e checklist.
