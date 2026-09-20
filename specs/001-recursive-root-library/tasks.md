# Tasks: Feature 001 — Recursive Root Library Hardening

**Branch**: `001-recursive-root-library` | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Inicialização do ambiente da feature e utilitários compartilhados de teste.

- [x] T001 Validar integridade da solution no branch `001-recursive-root-library` executando compilação em `Resonance.sln`
- [x] T002 [P] Implementar fixture auxiliar para criação de árvores sintéticas de pastas e arquivos temporários em `tests/Resonance.Core.Tests/Utils/SyntheticDirectoryFixture.cs`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Componentes essenciais de validação de sobreposição que bloqueiam a lógica de raízes das User Stories.

**⚠️ CRITICAL**: Nenhuma User Story pode avançar sem a validação do contrato de sobreposição de raízes.

- [x] T003 [P] Implementar validador de sobreposição de diretórios `RootOverlapValidator` e enum `RootOverlapAction` em `src/Resonance.Core/Helpers/RootOverlapValidator.cs` conforme contrato `CTR-ROOT-002`
- [x] T004 [P] Implementar testes unitários para `RootOverlapValidator` cobrindo subpastas, pastas ancestrais e caminhos idênticos em `tests/Resonance.Core.Tests/RootOverlapValidatorTests.cs`

**Checkpoint**: Fundação de validação de caminhos pronta e testada.

---

## Phase 3: User Story 1 - Varredura Recursiva Profunda e Resiliente (Priority: P1) 🎯 MVP [Slice 1]

```markdown
## META IMUTÁVEL

Problem:
O usuário deve apontar uma ou várias pastas raiz e ter todas as músicas válidas das subpastas indexadas automaticamente, sem cadastrar pasta por pasta e sem perder músicas por profundidade de diretório.

Definition of Success:
Uma árvore com múltiplos níveis, raízes sobrepostas, arquivos inválidos e caminhos problemáticos é processada sem duplicatas, sem travar a UI e sem abortar o scan inteiro.

Regra de Ouro:
Melhorar o scanner e persistência existentes do Nagi. Não criar uma segunda biblioteca paralela.
```

**Goal**: Descobrir todos os arquivos de áudio válidos em qualquer nível de subpastas a partir de raízes fornecidas, sem travar diante de caminhos inválidos ou ciclos em junções de diretório (NTFS Junctions) e links simbólicos.

**Independent Test**: Execução dos testes automatizados de `SafeFileEnumerator` em uma árvore sintética com 10 níveis de profundidade, links cíclicos propositais e arquivos com permissão negada.

### Tests for User Story 1
- [x] T005 [P] [US1] Criar testes unitários em `tests/Resonance.Core.Tests/SafeFileEnumeratorTests.cs` simulando reparse points cíclicos, caminhos longos (>260 caracteres) e arquivos corrompidos
 
### Implementation for User Story 1
- [x] T006 [US1] Atualizar `SafeFileEnumerator.cs` em `src/Resonance.Core/Helpers/SafeFileEnumerator.cs` para resolver destinos físicos canônicos de junções/symlinks (`ResolveLinkTarget`) rastreando nós visitados para impedir loops infinitos
- [x] T007 [US1] Implementar isolamento e captura segura de exceções de I/O por arquivo (ex: `UnauthorizedAccessException`, caminhos inválidos) em `src/Resonance.Core/Helpers/SafeFileEnumerator.cs`
- [x] T008 [US1] Executar e validar 100% de aprovação dos testes de enumeração segura via `dotnet test tests/Resonance.Core.Tests/Resonance.Core.Tests.csproj --filter "FullyQualifiedName~SafeFileEnumerator" --no-build`

**Checkpoint**: Travessia recursiva protegida contra ciclos e erros de I/O totalmente funcional de forma independente.

---

## Phase 4: User Story 2 - Prevenção de Duplicatas e Varredura Incremental (Priority: P2) [Slice 2]

```markdown
## META IMUTÁVEL

Problem:
O usuário deve apontar uma ou várias pastas raiz e ter todas as músicas válidas das subpastas indexadas automaticamente, sem cadastrar pasta por pasta e sem perder músicas por profundidade de diretório.

Definition of Success:
Uma árvore com múltiplos níveis, raízes sobrepostas, arquivos inválidos e caminhos problemáticos é processada sem duplicatas, sem travar a UI e sem abortar o scan inteiro.

Regra de Ouro:
Melhorar o scanner e persistência existentes do Nagi. Não criar uma segunda biblioteca paralela.
```

**Goal**: Indexar faixas descobertas de forma idempotente, com sincronização incremental, remoção (*hard delete*) de arquivos ausentes em raízes acessíveis, preservação do catálogo em raízes offline/desconectadas e cancelamento consistente em menos de 1 segundo.

**Independent Test**: Execução dos testes de integração em `LibraryServiceTests` validando expurgo de faixas ausentes, proteção de raízes offline e resposta de cancelamento < 1s.

### Tests for User Story 2
- [x] T009 [P] [US2] Escrever testes de integração em `tests/Resonance.Core.Tests/LibraryServiceTests.cs` para expurgo de faixas ausentes sob raiz acessível, preservação de faixas sob raiz offline e cancelamento cooperativo
 
### Implementation for User Story 2
- [x] T010 [US2] Atualizar `LibraryService.cs` em `src/Resonance.Core/Services/Implementations/LibraryService.cs` para validar acessibilidade de cada pasta raiz antes do scan, pulando raízes desconectadas com aviso e preservando seus registros
- [x] T011 [US2] Implementar sincronização incremental com expurgo (*hard delete*) de faixas ausentes no disco em `src/Resonance.Core/Services/Implementations/LibraryService.cs` apenas quando a raiz correspondente estiver acessível
- [x] T012 [US2] Implementar checagem de `CancellationToken` cooperativo a cada lote de arquivos persistidos no SQLite em `src/Resonance.Core/Services/Implementations/LibraryService.cs` garantindo cancelamento em < 1s
- [x] T013 [US2] Executar e validar aprovação de testes em `tests/Resonance.Core.Tests/LibraryServiceTests.cs` via `dotnet test tests/Resonance.Core.Tests/Resonance.Core.Tests.csproj --filter "FullyQualifiedName~LibraryService" --no-build`

**Checkpoint**: Sincronização incremental, hard delete seguro, tolerância a discos desconectados e cancelamento cooperativo validados.

---

## Phase 5: User Story 3 - Feedback em Tempo Real e Cancelamento na UI (Priority: P3) [Slice 3]

```markdown
## META IMUTÁVEL

Problem:
O usuário deve apontar uma ou várias pastas raiz e ter todas as músicas válidas das subpastas indexadas automaticamente, sem cadastrar pasta por pasta e sem perder músicas por profundidade de diretório.

Definition of Success:
Uma árvore com múltiplos níveis, raízes sobrepostas, arquivos inválidos e caminhos problemáticos é processada sem duplicatas, sem travar a UI e sem abortar o scan inteiro.

Regra de Ouro:
Melhorar o scanner e persistência existentes do Nagi. Não criar uma segunda biblioteca paralela.
```

**Goal**: Apresentar progresso contínuo na UI sem travamentos (60 fps), controles de cancelamento responsivos, consolidação visual de raízes sobrepostas e gatilhos de varredura sob demanda e na inicialização.

**Independent Test**: Testes no `SettingsViewModel` simulando adição de raízes sobrepostas com alerta visual e emissão de telemetria `IProgress<ScanProgress>` sem congelamento da thread de UI.

### Tests for User Story 3
- [x] T014 [P] [US3] Escrever testes unitários em `tests/Resonance.Core.Tests/RootOverlapValidatorTests.cs` cobrindo cenários complexos de consolidação de ancestrais e rejeição de subpastas consumidos pelo SettingsViewModel

### Implementation for User Story 3
- [x] T015 [US3] Integrar `RootOverlapValidator` em `src/Resonance.WinUI/ViewModels/SettingsViewModel.cs` ao adicionar novas pastas, emitindo notificação amigável na UI se a pasta já estiver coberta
- [x] T016 [US3] Atualizar `SettingsPage.xaml` em `src/Resonance.WinUI/Pages/SettingsPage.xaml` para exibir notificação informativa (*InfoBar*) de pasta sobreposta e botão de cancelamento ativo durante a varredura
- [x] T017 [US3] Conectar relato de progresso com limitação (*throttling* de 100ms) no `SettingsViewModel.cs` em `src/Resonance.WinUI/ViewModels/SettingsViewModel.cs` e adicionar toggle opcional de scan na inicialização

**Checkpoint**: Todas as 3 User Stories integradas e testadas de ponta a ponta na UI e Core.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Verificação de consistência, testes de regressão de ponta a ponta e gate constitucional.

- [x] T018 [P] Atualizar links cruzados entre `spec.md`, `plan.md`, `research.md`, `data-model.md`, `quickstart.md` e contratos em `specs/001-recursive-root-library/`
- [x] T019 Executar Whole-Solution Validation completa via `dotnet build Resonance.sln -p:Platform=x64` e `dotnet test Resonance.sln` comprovando 0 erros e 100% de testes aprovados
- [x] T020 Executar auditoria de qualidade do checklist `specs/001-recursive-root-library/checklists/quality-gate.md` garantindo conformidade constitucional

---

## Dependencies & Execution Order

### Phase Dependencies
- **Setup (Phase 1)**: Sem dependências — inicia imediatamente.
- **Foundational (Phase 2)**: Depende da Phase 1 — BLOQUEIA as User Stories.
- **User Story 1 (P1 - MVP)**: Inicia após Foundational; foca exclusivamente na travessia e proteção de ciclos.
- **User Story 2 (P2)**: Inicia após US1; foca na indexação incremental, hard delete e tolerância a discos desconectados.
- **User Story 3 (P3)**: Inicia após US2; integra UI, progresso em tempo real e prevenção visual de sobreposição.
- **Polish (Phase 6)**: Executada após o término das três user stories.

### User Story Dependencies
- **User Story 1 (P1)**: Independente de US2 e US3.
- **User Story 2 (P2)**: Consome a saída de arquivos do `SafeFileEnumerator` da US1.
- **User Story 3 (P3)**: Consome o pipeline de varredura e cancelamento da US2 e o validador da Phase 2.

### Parallel Opportunities
- Tarefas marcadas com `[P]` (`T002`, `T003`, `T004`, `T005`, `T009`, `T014`, `T018`) podem ser implementadas em paralelo por operarem em arquivos independentes sem dependências mútuas.

---

## Implementation Strategy

### MVP First (User Story 1 Only)
1. Completar Setup e Foundational (`T001` a `T004`).
2. Implementar User Story 1 (`T005` a `T008`): Resolução de reparse points cíclicos no `SafeFileEnumerator`.
3. **STOP and VALIDATE**: Executar testes unitários de travessia e garantir descoberta correta de arquivos.

### Incremental Delivery (Slices 2 e 3)
1. Concluir Slice 2 (`T009` a `T013`): Incremental scan, hard delete de arquivos ausentes e cancelamento < 1s.
2. Concluir Slice 3 (`T014` a `T017`): Experiência de UI, consolidação de raízes no Settings e telemetria fluida.
3. Executar Polish (`T018` a `T020`) com whole-solution validation e checklist quality gate.
