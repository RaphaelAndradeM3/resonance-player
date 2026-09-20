# Implementation Plan: Feature 000 — Baseline / Audit do Fork Nagi

**Branch**: `000-nagi-baseline-audit` | **Date**: 2026-09-20 | **Spec**: [specs/000-nagi-baseline-audit/spec.md](file:///h:/tmp/RSA/Loterias/JogosMaster/GitHub/resonance-player/specs/000-nagi-baseline-audit/spec.md)

**Input**: Feature specification from `/specs/000-nagi-baseline-audit/spec.md`

---

## Summary

Esta feature estabelece a baseline formal e imutável do fork do Nagi para o projeto Resonance. Antes de efetuar qualquer alteração no código de produto, auditamos o estado real da codebase herdada, fixamos o tag e commit SHA upstream, comprovamos a toolchain .NET oficial (`.NET 10.0`, WinUI 3, LibVLCSharp, ATL, SQLite/EF Core), catalogamos a injeção de dependência e os serviços existentes para prevenir a criação de abstrações concorrentes ou duplicadas, e mapeamos todas as lacunas técnicas reais contra o `PRD.md`.

---

## Technical Context

**Language/Version**: C# 13 / preview, .NET 10.0 (SDK 10.0.401 instalado localmente; `global.json` especificando versão base `10.0.300` com `rollForward: latestFeature`).

**Primary Dependencies**:
- WinUI 3 / Windows App SDK 2.4.0 (BuildTools 10.0.28000.2705)
- LibVLCSharp 4.0.0-alpha / VideoLAN.LibVLC.Windows 4.0.0-alpha
- ATL Core (`z440.atl.core`) 7.16.0 para metadados de áudio
- CommunityToolkit.Mvvm 8.4.2
- ModernLrc 1.2.0 para parser de letras sincronizadas
- SixLabors.ImageSharp 4.1.1 (suporte a processamento de capas)
- Polly 8.7.0 (resiliência HTTP e rate limiting)
- Serilog 10.0.0 (logging com sinks de arquivo e debug)

**Storage**: SQLite local via Entity Framework Core 10.0.11 (`Nagi.Core.Data.MusicDbContext`) com pool de conexões e migrations gerenciadas.

**Testing**: `Microsoft.Testing.Platform` (MTP) com `xunit.v3` 4.0.0, `FluentAssertions` 8.10.0, `NSubstitute` 6.2.0 e `coverlet.MTP` 10.0.1.

**Target Platform**: Windows 10 (Build 17763+) e Windows 11 (arquiteturas `win-x64` e `win-arm64`). O flag `-p:Platform=x64` é mandatório para compilação da UI.

**Project Type**: Aplicação Desktop WinUI 3 empacotada/desempacotada + Bibliotecas de Classe .NET 10 + Azure Functions.

**Performance Goals**: Inicialização a frio < 1.5s; varredura de biblioteca sem bloqueio de thread de UI; pipeline de áudio estável com consumo reduzido de CPU.

**Constraints**:
- Operação 100% offline para reprodução, biblioteca, EQ e letras locais (Princípio IV).
- Não criar abstrações, serviços, repositórios ou pipelines de áudio paralelos (Princípio I).
- Preservar integralmente a licença GPLv3 do Nagi (Princípio V).

**Scale/Scope**: Solution composta por 4 projetos (`Nagi.WinUI`, `Nagi.Core`, `NagiAppFunctions`, `Nagi.Core.Tests`), ~40 suítes de teste de unidade e integração.

---

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Princípio Constitucional | Status | Avaliação na Baseline |
| :--- | :--- | :--- |
| **I. Existing Code Is the Source of Truth** | **PASS** | Repositório upstream clonado e auditado (Tag 2.3.0 / HEAD `e242b8b0`). Todos os tipos existentes mapeados em `audit-matrix-contract.md`. |
| **II. Vertical Slices, Not Horizontal Microtasks** | **PASS** | Feature estruturada em exatamente 3 fatias verticais ponta a ponta com repetição da META IMUTÁVEL. |
| **III. Whole-Solution Validation** | **PASS** | Comandos oficiais de `restore`, `build -p:Platform=x64` e `test` documentados formalmente em `toolchain-contract.md`. |
| **IV. Local-First and Privacy-First** | **PASS** | Confirmado que o core do Nagi funciona 100% offline e não exige telemetria ou envio de arquivos para a nuvem. |
| **V. Licensing and Provider Compliance** | **PASS** | Licença GPLv3 confirmada. Impacto de licença do SixLabors ImageSharp 4.1.1 investigado e documentado em `research.md`. |
| **VI. Large Library Resilience** | **PASS** | Identificados os pontos de extensão em `LibraryService` e `SafeFileEnumerator` para a Feature 001. |
| **VII. Explicit Audio and Metadata Boundaries** | **PASS** | Isolamento claro comprovado entre `LibVlcAudioPlayerService` (áudio), `AtlMetadataService` (tags), `LrcService` (letras) e `MusicDbContext` (dados). |

---

## Project Structure

### Documentation (this feature)

```text
specs/000-nagi-baseline-audit/
├── plan.md                          # Este plano de implementação
├── research.md                      # Phase 0: Investigação técnica e decisões arquiteturais
├── data-model.md                    # Phase 1: Entidades de auditoria e esquemas
├── quickstart.md                    # Phase 1: Guia executável de validação
├── checklists/
│   └── requirements.md              # Checklist de requisitos da spec
└── contracts/
    ├── toolchain-contract.md        # Contrato de comandos oficiais de build/test
    ├── audit-matrix-contract.md     # Matriz de inventário arquitetural e regras de reuso
    └── gap-report-contract.md       # Matriz de alinhamento PRD x Features 001-010
```

### Source Code (repository root)

A estrutura real da solution é composta por 4 projetos:

```text
Nagi.sln
├── src/
│   ├── Nagi.Core/                   # Biblioteca central de lógica de domínio, serviços e modelos
│   │   ├── Constants/
│   │   ├── Data/                    # SQLite / EF Core (MusicDbContext, Migrations)
│   │   ├── Helpers/
│   │   ├── Http/
│   │   ├── Models/                  # Modelos de domínio (Song, Album, Artist, Folder, Equalizer)
│   │   └── Services/                # Abstrações e implementações de Scanner, Tags, Letras
│   └── Nagi.WinUI/                  # Interface do usuário em WinUI 3 / Windows App SDK
│       ├── Controls/
│       ├── Navigation/
│       ├── Pages/
│       ├── Services/                # LibVlcAudioPlayerService, UIService, SettingsService
│       ├── ViewModels/
│       └── Views/
├── tests/
│   └── Nagi.Core.Tests/             # Suíte de testes automatizados com xUnit v3 e MTP
└── api/
    └── NagiAppFunctions/            # Funções auxiliares Azure (opcionais / desacopladas)
```

**Structure Decision**: Preservar rigorosamente a estrutura modular de projetos existente. Nenhuma nova camada ou projeto será criado além dos já previstos no design da baseline.

---

## Complexity Tracking

*Nenhuma violação ou exceção aos princípios constitucionais foi identificada.*

| Violação | Justificativa | Alternativa Rejeitada |
| :--- | :--- | :--- |
| *Nenhuma* | N/A | N/A |

---

## Vertical Implementation Slices

### Slice 1: Build/Test Baseline
- **Meta Imutável:**
  > **Problema:** Antes de modificar o Nagi, precisamos saber exatamente o que a versão escolhida já implementa, como a solution está organizada e quais lacunas são reais.  
  > **Definição de Sucesso:** Existe uma baseline reproduzível com tag/SHA fixados, solution compilando, testes executados e documentação técnica verificável sobre scanner, formatos, metadata, equalizador, lyrics, playback, DI, persistência e testes existentes.  
  > **Regra de Ouro:** Não implementar feature nova nesta etapa. Não refatorar código de produto.
- **Escopo ponta a ponta:** Fixação de upstream em tag `2.3.0` / commit `88b9790e` (HEAD `e242b8b0`), execução de `dotnet restore Nagi.sln -p:Platform=x64`, `dotnet build Nagi.sln -p:Platform=x64` e execução da suíte de testes.
- **Artefatos:** `contracts/toolchain-contract.md`, `research.md`.
- **Validação Local:** `dotnet restore Nagi.sln -p:Platform=x64` e compilação limpa.

### Slice 2: Architecture & Capability Audit
- **Meta Imutável:** *(mesma acima)*
- **Escopo ponta a ponta:** Mapeamento minucioso do grafo de DI em `App.xaml.cs`, dos serviços em `Nagi.Core.Services` e `Nagi.WinUI.Services`, e da persistência em `MusicDbContext`. Estabelecimento da regra de reutilização mandatória para impedir arquitetura concorrente.
- **Artefatos:** `contracts/audit-matrix-contract.md`, `data-model.md`.
- **Validação Local:** Inspeção estrutural e verificação cruzada com a codebase real.

### Slice 3: Characterization & Gap Report
- **Meta Imutável:** *(mesma acima)*
- **Escopo ponta a ponta:** Comparação exaustiva de cada requisito de negócio do `PRD.md` com as capabilities comprovadas no Nagi, vinculando cada gap diretamente a uma das Features 001 a 010 do catálogo Spec Kit.
- **Artefatos:** `contracts/gap-report-contract.md`, `quickstart.md`.
- **Validação Final:** Solution 100% mapeada, comandos de teste auditados e alinhamento aprovado para início da Feature 001.
