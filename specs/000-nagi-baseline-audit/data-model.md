# Data Model: Feature 000 — Baseline / Audit do Fork Nagi

**Feature**: `000-nagi-baseline-audit`  
**Date**: 2026-09-20  
**Status**: Completed  

---

## 1. Overview & Scope

Este documento define os modelos de dados e entidades conceituais utilizados para registrar a auditoria arquitetural, a baseline de compilação e a matriz de gaps entre o fork do Nagi e os requisitos do Resonance (`PRD.md`). Como a Feature 000 é estritamente de documentação e auditoria técnica da baseline, essas entidades representam os registros formais gerados durante a inspeção do repositório.

---

## 2. Entities & Schemas

### 2.1 BaselineAuditRecord
Registra a proveniência exata do código-fonte herdado, o ambiente de compilação e o resultado das validações automáticas.

| Campo | Tipo | Obrigatório | Descrição / Exemplo |
| :--- | :--- | :--- | :--- |
| `UpstreamRepoUrl` | `string` | Sim | URL oficial do upstream: `https://github.com/Anthonyy232/Nagi` |
| `BaselineTag` | `string` | Sim | Tag oficial de referência: `2.3.0` |
| `CommitSha` | `string` | Sim | SHA completo do commit auditado: `88b9790e7fd176841dc055a51b61f07b5011f849` (HEAD: `e242b8b0ae406144aa94b3b40a8e7815d8143cb4`) |
| `AuditDate` | `DateTimeOffset`| Sim | Data da auditoria: `2026-09-20` |
| `DotNetSdkVersion`| `string` | Sim | Versão do SDK instalada: `10.0.401` (`global.json`: `10.0.300` com rollForward) |
| `TargetPlatform` | `string` | Sim | Arquitetura de compilação da UI: `x64` |
| `ProjectsAudited` | `List<string>` | Sim | Lista de projetos na solution (.sln) |
| `BuildStatus` | `Enum` | Sim | `Succeeded`, `Failed`, `Warning` |
| `TestsExecuted` | `int` | Sim | Total de testes executados na suíte baseline |
| `TestsPassed` | `int` | Sim | Total de testes aprovados |
| `LicenseVerified`| `string` | Sim | `GPL-3.0-only` com headers preservados |

---

### 2.2 ArchitectureInventoryItem
Cataloga cada serviço e subsistema existente no Nagi para impor as regras da Constituição (não duplicar nem criar arquitetura concorrente).

| Campo | Tipo | Obrigatório | Descrição / Exemplo |
| :--- | :--- | :--- | :--- |
| `CapabilityArea` | `Enum` | Sim | `Playback`, `Scanner`, `Metadata`, `Lyrics`, `Equalizer`, `Persistence`, `UI`, `Telemetry` |
| `PrimaryInterface`| `string` | Sim | Nome totalmente qualificado da interface (ex.: `Nagi.Core.Services.Abstractions.IAudioPlayer`) |
| `ConcreteClass` | `string` | Sim | Classe de implementação padrão (ex.: `Nagi.WinUI.Services.Implementations.LibVlcAudioPlayerService`) |
| `Assembly` | `string` | Sim | `Nagi.Core` ou `Nagi.WinUI` |
| `Lifetime` | `Enum` | Sim | `Singleton`, `Scoped`, `Transient` |
| `ReuseObligation` | `Enum` | Sim | `Mandatory` (proibido criar classe paralela), `Extensible` (permitido estender), `Internal` |
| `RelatedFeature` | `string` | Sim | Feature do roadmap que reutilizará o componente (ex.: `001`, `002`, `008`) |

---

### 2.3 GapAnalysisRecord
Mapeia a correspondência entre cada requisito funcional do `PRD.md` e a capacidade comprovada na baseline do Nagi.

| Campo | Tipo | Obrigatório | Descrição / Exemplo |
| :--- | :--- | :--- | :--- |
| `PrdRequirementId`| `string`| Sim | Identificador no PRD (ex.: `FR-LIB-001`, `FR-EQ-001`) |
| `RequirementName` | `string`| Sim | Título da necessidade funcional |
| `BaselineStatus` | `Enum` | Sim | `FullyImplemented`, `PartiallyImplemented`, `AbsentInBaseline` |
| `TargetFeatureId` | `string`| Sim | Feature do catálogo Spec Kit atribuída (`001` a `010`) |
| `ExistingComponent`| `string`| Não | Tipo ou serviço que já atende parcialmente o requisito |
| `GapDescription` | `string`| Sim | Detalhamento do que falta para cumprir o requisito integralmente |

---

## 3. Relationships & Cardinality

```text
+-----------------------+           1:N           +----------------------------+
|  BaselineAuditRecord  | ----------------------> | ArchitectureInventoryItem  |
+-----------------------+                         +----------------------------+
            |
            | 1:N
            v
+-----------------------+
|   GapAnalysisRecord   |
+-----------------------+
```

1. **BaselineAuditRecord** referencia uma coleção de **ArchitectureInventoryItems**, estabelecendo a verdade arquitetural da solução.
2. Cada **GapAnalysisRecord** se conecta a um ou mais **ArchitectureInventoryItems** para indicar se o gap deve ser resolvido estendendo um serviço existente ou se depende de integração externa autorizada.

---

## 4. State Transitions: Audit Lifecycle

```text
[Initialized] 
       │
       ▼
[Upstream Cloned / Pinned] 
       │
       ▼
[Toolchain Verified (dotnet restore / build -p:Platform=x64)]
       │
       ▼
[Test Suite Executed (dotnet test Nagi.Core.Tests)]
       │
       ▼
[Architecture & DI Cataloged]
       │
       ▼
[PRD Gap Matrix Finalized] ──> [Baseline Certified]
```

---

## 5. Validation Rules

- **VAL-001**: O `CommitSha` deve corresponder a um commit válido e publicamente auditável do repositório upstream do Nagi.
- **VAL-002**: Toda entidade em `ArchitectureInventoryItem` com `ReuseObligation = Mandatory` proíbe expressamente a criação de novos tipos com responsabilidades sobrepostas nas Features 001 a 010.
- **VAL-003**: Todo requisito funcional de `FR-LIB-001` até `FR-UI-010` do PRD deve possuir exatamente 1 entrada correspondente em `GapAnalysisRecord`.
- **VAL-004**: O `BuildStatus` da baseline deve ser obrigatoriamente `Succeeded` para que qualquer feature subsequente (001+) possa iniciar implementação.
