# Tasks: Feature 000 — Baseline / Audit do Fork Nagi

**Branch**: `000-nagi-baseline-audit` | **Spec**: [spec.md](file:///h:/tmp/RSA/Loterias/JogosMaster/GitHub/resonance-player/specs/000-nagi-baseline-audit/spec.md) | **Plan**: [plan.md](file:///h:/tmp/RSA/Loterias/JogosMaster/GitHub/resonance-player/specs/000-nagi-baseline-audit/plan.md)

---

## Phase 1: Setup (Shared Infrastructure & Upstream Reference)

**Purpose**: Fixação formal do repositório upstream do Nagi, configuração do ambiente e alinhamento de infraestrutura.

- [x] T001 Fixar referência do repositório upstream oficial em `specs/000-nagi-baseline-audit/research.md` com Tag `2.3.0` (commit `88b9790e`) e HEAD `e242b8b0`
- [x] T002 [P] Configurar diretório de trabalho e contratos de toolchain .NET em `specs/000-nagi-baseline-audit/contracts/toolchain-contract.md`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Verificação das ferramentas centrais e pré-requisitos antes da execução das fatias de auditoria.

**CRITICAL**: Nenhuma tarefa de auditoria ou caracterização pode ser concluída sem a validação do runtime e SDK instalados.

- [x] T003 Validar SDK .NET 10.0 e cargas de trabalho x64 do WinUI 3 via `dotnet --info` conforme `specs/000-nagi-baseline-audit/quickstart.md`
- [x] T004 [P] Documentar parâmetros mandatórios de compilação x64 e tratamento de licença SixLabors ImageSharp em `specs/000-nagi-baseline-audit/contracts/toolchain-contract.md`

---

## Phase 3: User Story 1 - Validação do Ambiente de Build e Testes (Priority: P1) [Slice 1]

```markdown
## META IMUTÁVEL

Problem:
Antes de modificar o Nagi, precisamos saber exatamente o que a versão escolhida já implementa,
como a solution está organizada e quais lacunas são reais. Sem isso, agentes podem duplicar
recursos, criar arquitetura paralela ou quebrar comportamentos existentes.

Definition of Success:
Existe uma baseline reproduzível com tag/SHA fixados, solution compilando, testes executados e
documentação técnica verificável sobre scanner, formatos, metadata, equalizador, lyrics,
playback, DI, persistência e testes existentes.

Regra de Ouro:
Não implementar feature nova nesta etapa. Não refatorar código de produto.
```

**Goal**: Comprovar e documentar o estado de compilação e teste dos 4 projetos da solution (`Nagi.Core`, `Nagi.WinUI`, `NagiAppFunctions`, `Nagi.Core.Tests`) sem alterar código de produto.

**Independent Test**: Execução dos comandos oficiais `dotnet restore Nagi.sln -p:Platform=x64`, `dotnet build Nagi.sln --configuration Release -p:Platform=x64 --no-restore` e execução da suíte de testes.

- [x] T005 [US1] Executar restauração completa dos pacotes NuGet da solution via `dotnet restore Nagi.sln -p:Platform=x64`
- [x] T006 [US1] Executar compilação da solution no modo Release direcionado a x64 em `Nagi.sln`
- [x] T007 [US1] Executar a suíte de testes automatizados (`tests/Nagi.Core.Tests/Nagi.Core.Tests.csproj`) com `$env:DOTNET_CLI_UI_LANGUAGE = "en"` e registrar a aprovação de 100% (845/845 testes) em `specs/000-nagi-baseline-audit/research.md`
- [x] T008 [US1] Formalizar a matriz de saída de compilação e testes em `specs/000-nagi-baseline-audit/contracts/toolchain-contract.md`

**Checkpoint**: Ambiente e solution 100% auditados e comprovados como estáveis.

---

## Phase 4: User Story 2 - Mapeamento Arquitetural de Componentes Existentes (Priority: P2) [Slice 2]

```markdown
## META IMUTÁVEL

Problem:
Antes de modificar o Nagi, precisamos saber exatamente o que a versão escolhida já implementa,
como a solution está organizada e quais lacunas são reais. Sem isso, agentes podem duplicar
recursos, criar arquitetura paralela ou quebrar comportamentos existentes.

Definition of Success:
Existe uma baseline reproduzível com tag/SHA fixados, solution compilando, testes executados e
documentação técnica verificável sobre scanner, formatos, metadata, equalizador, lyrics,
playback, DI, persistência e testes existentes.

Regra de Ouro:
Não implementar feature nova nesta etapa. Não refatorar código de produto.
```

**Goal**: Mapear detalhadamente todos os serviços herdados, grafo de DI e persistência para impor a regra de ouro de não duplicação arquitetural.

**Independent Test**: Inspeção cruzada do catálogo gerado em `audit-matrix-contract.md` contra o grafo de injeção de dependências em `App.xaml.cs`.

- [x] T009 [P] [US2] Catalogar o motor de reprodução LibVLC 4.0 (`IAudioPlayer` e `IMusicPlaybackService`) em `src/Nagi.WinUI/Services/Implementations/LibVlcAudioPlayerService.cs` e `src/Nagi.Core/Services/Implementations/MusicPlaybackService.cs`
- [x] T010 [P] [US2] Catalogar o serviço de scanner de biblioteca (`ILibraryScanner`, `ILibraryReader`, `ILibraryWriter`) e persistência SQLite (`MusicDbContext`) em `src/Nagi.Core/Services/Implementations/LibraryService.cs` e `src/Nagi.Core/Data/MusicDbContext.cs`
- [x] T011 [P] [US2] Catalogar o motor de metadados ATL (`IMetadataService`) e serviço de letras (`ILrcService`, `IOnlineLyricsService`) em `src/Nagi.Core/Services/Implementations/AtlMetadataService.cs` e `src/Nagi.Core/Services/Implementations/LrcService.cs`
- [x] T012 [US2] Mapear o container de Injeção de Dependência de `src/Nagi.WinUI/App.xaml.cs` e consolidar as regras de reutilização obrigatória em `specs/000-nagi-baseline-audit/contracts/audit-matrix-contract.md`

**Checkpoint**: Toda a arquitetura do Nagi mapeada; agentes proibidos de recriar serviços concorrentes.

---

## Phase 5: User Story 3 - Relatório de Gaps contra o PRD e Testes de Caracterização (Priority: P3) [Slice 3]

```markdown
## META IMUTÁVEL

Problem:
Antes de modificar o Nagi, precisamos saber exatamente o que a versão escolhida já implementa,
como a solution está organizada e quais lacunas são reais. Sem isso, agentes podem duplicar
recursos, criar arquitetura paralela ou quebrar comportamentos existentes.

Definition of Success:
Existe uma baseline reproduzível com tag/SHA fixados, solution compilando, testes executados e
documentação técnica verificável sobre scanner, formatos, metadata, equalizador, lyrics,
playback, DI, persistência e testes existentes.

Regra de Ouro:
Não implementar feature nova nesta etapa. Não refatorar código de produto.
```

**Goal**: Confrontar os requisitos funcionais do `PRD.md` contra a baseline auditada, categorizando o que é existente, parcial ou ausente, e vinculando cada lacuna à sua respectiva feature (001 a 010).

**Independent Test**: Verificação da matriz de gaps contra as 10 features funcionais e execução do roteiro de quickstart.

- [ ] T013 [US3] Mapear gaps de grandes bibliotecas recursivas (`FR-LIB-001` a `008`) vinculando à Feature 001 em `specs/000-nagi-baseline-audit/contracts/gap-report-contract.md`
- [ ] T014 [P] [US3] Mapear gaps de formatos, inspector, fingerprint, metadados, letras, equalizador e FFT (`FR-FMT`, `FR-TRK`, `FR-FNG`, `FR-META`, `FR-TAG`, `FR-LYR`, `FR-EQ`, `FR-FFT`, `FR-UI`) vinculando às Features 002 a 010 em `specs/000-nagi-baseline-audit/contracts/gap-report-contract.md`
- [ ] T015 [US3] Consolidar o roteiro executável de validação ponta a ponta e testes de caracterização em `specs/000-nagi-baseline-audit/quickstart.md`
- [ ] T016 [US3] Executar o gate final de whole-solution validation comprovando conformidade constitucional em `specs/000-nagi-baseline-audit/plan.md`

**Checkpoint**: Matriz de gaps completa e roadmap das Features 001 a 010 blindado contra suposições incorretas.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Verificação de consistência e alinhamento documental final.

- [ ] T017 [P] Atualizar links de navegação cruzada entre `plan.md`, `spec.md`, `data-model.md`, `quickstart.md` e os contratos em `specs/000-nagi-baseline-audit/`
- [ ] T018 Realizar auditoria de conformidade com a Constituição em `.specify/memory/constitution.md` garantindo observância aos 7 princípios fundamentais

---

## Dependencies & Execution Order

### Phase Dependencies
- **Setup (Phase 1)**: Sem dependências — inicia imediatamente.
- **Foundational (Phase 2)**: Depende da Phase 1 — BLOQUEIA as User Stories.
- **User Stories (Phases 3, 4, 5)**:
  - **User Story 1 (P1 - Slice 1)**: Validação de build e testes.
  - **User Story 2 (P2 - Slice 2)**: Mapeamento de serviços e DI (depende da compilação de US1).
  - **User Story 3 (P3 - Slice 3)**: Matriz de gaps e alinhamento com PRD (depende de US1 e US2).
- **Polish (Phase 6)**: Executada após o término das três user stories.

### Parallel Opportunities
- Tarefas `T002`, `T004`, `T009`, `T010`, `T011`, `T014` e `T017` são marcadas com `[P]` e podem ser executadas ou analisadas em paralelo por não apresentarem conflitos de arquivos ou escrita.

---

## Implementation Strategy

### MVP First (User Story 1 - Slice 1)
1. Completar Setup e Foundational (T001 a T004).
2. Executar e validar User Story 1 (T005 a T008): restore, build Release x64 e execução dos testes da baseline.
3. Certificar o estado do código herdado antes de qualquer avanço.

### Incremental Delivery (Slices 2 e 3)
1. Concluir Slice 2 (T009 a T012): Mapeamento formal da arquitetura e injeção de dependência.
2. Concluir Slice 3 (T013 a T016): Mapeamento de gaps contra o PRD e formalização das premissas para a Feature 001.
3. Finalizar com Polish (T017 e T018).
