# Implementation Plan: 001 — Recursive Root Library Hardening

**Branch**: `001-recursive-root-library` | **Date**: 2026-09-20 | **Spec**: [spec.md](spec.md)

---

## Summary

Esta feature entrega o endurecimento (*hardening*) definitivo da descoberta recursiva de bibliotecas de áudio locais no **Resonance**.

O objetivo técnico central é permitir que o usuário aponte uma ou mais pastas raiz e tenha 100% das faixas de áudio válidas indexadas automaticamente através de qualquer nível de subpastas, com proteção contra loops infinitos em *NTFS Junctions* e *Symbolic Links*, consolidação inteligente de raízes sobrepostas, sincronização incremental com expurgo (*hard delete*) de arquivos ausentes em raízes acessíveis, e preservação integral de coleções quando volumes externos (USB/rede) estiverem desconectados.

A implementação reutiliza estritamente os componentes existentes (`SafeFileEnumerator`, `LibraryService`, `Folder`, `Song`, `ScanProgress`), dividida em exatamente 3 fatias verticais.

---

## Technical Context

* **Language/Version**: C# 13 / .NET 10.0 (preview features ativadas em `Directory.Build.props`)
* **Primary Dependencies**: WinUI 3 (Windows App SDK 1.7+), Entity Framework Core SQLite (`Microsoft.EntityFrameworkCore.Sqlite`), ATL.NET (`z4kn4fein.atl.core` para extração de metadados), LibVLC 4.0 (`VideoLAN.LibVLC.Windows`), Microsoft.Extensions.DependencyInjection
* **Storage**: SQLite local criptografado/estruturado via EF Core (`resonance.db`), operando no padrão `AsSplitQuery` com transações em lote
* **Testing**: xUnit, FluentAssertions, NSubstitute (`tests/Resonance.Core.Tests/`)
* **Target Platform**: Windows 10 (10.0.17763.0+) e Windows 11 (x64 nativo)
* **Project Type**: Desktop Application (WinUI 3 com biblioteca de classes Core)
* **Performance Goals**: Enumeração de árvore com 10.000 faixas em 10 níveis de profundidade sem congelar a UI; cancelamento de scan responsivo em menos de 1000ms; reprodução contínua de áudio a 60 fps durante o scan
* **Constraints**: Bounded concurrency (`Math.Clamp(Environment.ProcessorCount / 2, 2, 4)` workers de I/O de tags); sem operações de I/O síncronas na thread de UI do WinUI
* **Scale/Scope**: Coleções musicais de 1 a 100.000+ faixas distribuídas em diretórios complexos com reparse points

---

## Constitution Check

*GATE: Avaliado antes da geração do design e reavaliado após a modelagem.*

| Princípio Constitucional | Conformidade | Racional e Evidência |
|---|:---:|---|
| **I. Existing Code Is Truth** | **PASS** | Não são criados novos serviços ou bancos paralelos. O `SafeFileEnumerator` e o `LibraryService` existentes são evoluídos diretamente. As entidades `Folder` e `Song` continuam sendo o modelo oficial. |
| **II. Vertical Slices (Max 3)** | **PASS** | A feature é estritamente decomposta em 3 fatias verticais end-to-end (Slice 1: Traversal & Ciclos; Slice 2: Indexação & Incremental; Slice 3: UI & Progresso). |
| **III. Whole-Solution Validation** | **PASS** | O plano exige a compilação completa da solution (`Resonance.sln`) em Release x64 e aprovação de 100% da suíte de testes (845+ testes) em cada checkpoint. |
| **IV. Local First & Privacy** | **PASS** | Toda a varredura, detecção de caminhos e persistência opera 100% offline no disco local e banco SQLite. |
| **V. Licensing & Toolchain** | **PASS** | Respeita o toolchain mandatório x64 no .NET 10 e não adiciona dependências externas restritivas. |
| **VI. Large Library Resilience** | **PASS** | Diretamente alinhado ao princípio VI: bounded concurrency, suporte a MAX_PATH, detecção de loops em reparse points e isolamento contra corrupção. |
| **VII. Explicit Boundaries** | **PASS** | O escopo se restringe à descoberta e indexação física recursiva; formatos exóticos pertencem à Feature 002 e inspector à Feature 003. |

---

## Project Structure

### Documentation (this feature)

```text
specs/001-recursive-root-library/
├── spec.md              # Especificação funcional com clarificações formalizadas
├── plan.md              # Este plano de implementação
├── research.md          # Decisões de arquitetura, trade-offs e racional técnico
├── data-model.md        # Mapeamento de entidades Folder/Song e ciclo de vida
├── quickstart.md        # Roteiro executável de testes e validação ponta a ponta
├── tasks.md             # Tarefas de implementação decompostas por fases
├── checklists/          # Checklists de requisitos e quality-gate
│   ├── requirements.md
│   └── quality-gate.md
└── contracts/           # Contratos formais da engine e de gestão de raízes
    ├── scanner-engine-contract.md
    └── root-management-contract.md
```

### Source Code (repository root)

```text
src/
├── Resonance.Core/
│   ├── Helpers/
│   │   ├── SafeFileEnumerator.cs          # [MODIFY] Adicionar resolução de reparse points e tracking de ciclos
│   │   └── RootOverlapValidator.cs        # [NEW] Validador de sobreposição de raízes (CTR-ROOT-002)
│   ├── Services/
│   │   ├── Abstractions/
│   │   │   └── ILibraryScanner.cs         # [MAINTAIN] Contrato existente respeitado
│   │   └── Implementations/
│   │       └── LibraryService.cs          # [MODIFY] Hard delete de faixas ausentes, pulo seguro de raízes offline
│   └── Models/
│       ├── Folder.cs                      # [MAINTAIN] Entidade de persistência de raiz
│       └── Song.cs                        # [MAINTAIN] Entidade de persistência de faixa
│
├── Resonance.WinUI/
│   ├── ViewModels/
│   │   └── SettingsViewModel.cs           # [MODIFY] Validação de sobreposição ao adicionar/remover pastas raiz
│   └── Views/
│       └── SettingsPage.xaml              # [MODIFY] Mensagens visuais de sobreposição e progresso desacoplado
│
tests/
└── Resonance.Core.Tests/
    ├── SafeFileEnumeratorTests.cs         # [MODIFY] Adicionar testes de junções/symlinks e ciclos
    ├── RootOverlapValidatorTests.cs       # [NEW] Testes de validação de raízes sobrepostas
    └── LibraryServiceTests.cs             # [MODIFY] Testes de sincronização incremental, raízes offline e cancelamento
```

**Structure Decision**: A solution mantém a arquitetura existente de 2 projetos de produto (`Resonance.Core` e `Resonance.WinUI`) e 1 de teste (`Resonance.Core.Tests`), evoluindo os módulos responsáveis sem adicionar projetos desnecessários.

---

## Fatias Verticais de Implementação

### Slice 1 — Recursive Traversal Resiliente (Priority: P1)
* **Meta Imutável Repetida**: Descobrir todos os arquivos de áudio válidos em qualquer nível de subpastas a partir de raízes fornecidas, sem travar diante de caminhos inválidos ou ciclos.
* **Componentes Afetados**: [SafeFileEnumerator.cs](file:///h:/tmp/RSA/Loterias/JogosMaster/GitHub/resonance-player/src/Resonance.Core/Helpers/SafeFileEnumerator.cs), [SafeFileEnumeratorTests.cs](file:///h:/tmp/RSA/Loterias/JogosMaster/GitHub/resonance-player/tests/Resonance.Core.Tests/SafeFileEnumeratorTests.cs).
* **Entrega**: Resolução de caminhos canônicos de *NTFS Junctions* e *Symlinks* com conjunto de visitados; testes com árvores sintéticas de 10 níveis e loops propositais.

### Slice 2 — Indexação, Persistência e Varredura Incremental (Priority: P2)
* **Meta Imutável Repetida**: Indexar faixas descobertas de forma idempotente, sem duplicatas entre raízes sobrepostas, com suporte a atualização incremental e cancelamento consistente.
* **Componentes Afetados**: [RootOverlapValidator.cs](file:///h:/tmp/RSA/Loterias/JogosMaster/GitHub/resonance-player/src/Resonance.Core/Helpers/RootOverlapValidator.cs), [LibraryService.cs](file:///h:/tmp/RSA/Loterias/JogosMaster/GitHub/resonance-player/src/Resonance.Core/Services/Implementations/LibraryService.cs), testes de integração de persistência.
* **Entrega**: Lógica de consolidação de raízes; remoção (*hard delete*) de faixas ausentes em raízes acessíveis; preservação de faixas em raízes offline; cancelamento < 1s via `CancellationToken`.

### Slice 3 — UI, Progresso e Regressão (Priority: P3)
* **Meta Imutável Repetida**: Apresentar progresso contínuo na UI sem travamentos e disponibilizar controles de cancelamento e relatório de erros não-bloqueantes.
* **Componentes Afetados**: [SettingsViewModel.cs](file:///h:/tmp/RSA/Loterias/JogosMaster/GitHub/resonance-player/src/Resonance.WinUI/ViewModels/SettingsViewModel.cs), [SettingsPage.xaml](file:///h:/tmp/RSA/Loterias/JogosMaster/GitHub/resonance-player/src/Resonance.WinUI/Pages/SettingsPage.xaml), gatilhos de scan (manual sob demanda e ao alterar raízes).
* **Entrega**: Conexão fluida da UI com `IProgress<ScanProgress>`, feedback visual amigável ao tentar adicionar subpastas sobrepostas e gate de whole-solution validation.

---

## Complexity Tracking

> **Nenhuma violação constitucional registrada.** Todos os 7 princípios foram respeitados integralmente sem necessidade de exceções.
