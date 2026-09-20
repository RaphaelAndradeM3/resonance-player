# FEATURE SPEC: 000 — Baseline / Audit do Fork Nagi

**Feature Branch**: `000-nagi-baseline-audit`  
**Created**: 2026-09-20  
**Status**: Draft  
**Input**: Baseado em `FEATURES_SUGERIDAS_SPEC_KIT.md` (Feature 000)

---

## 1. META IMUTÁVEL (Global Goal)

> **Problema de Negócio:** Antes de modificar o Nagi, precisamos saber exatamente o que a versão escolhida já implementa, como a solution está organizada e quais lacunas são reais. Sem isso, agentes podem duplicar recursos, criar arquitetura paralela ou quebrar comportamentos existentes.
>
> **Definição de Sucesso:** Existe uma baseline reproduzível com tag/SHA fixados, solution compilando, testes executados e documentação técnica verificável sobre scanner, formatos, metadata, equalizador, lyrics, playback, DI, persistência e testes existentes.
>
> **Regra de Ouro:** Não implementar feature nova nesta etapa. Não refatorar código de produto.

---

## Clarifications

### Session 2026-09-20
- Q: Como a especificação da baseline deve tratar a cultura/localização na execução da suíte de testes existente para cumprir o critério de 100% de aprovação? (SC-002) → A: Exigir execução da toolchain com cultura neutra/inglês (`DOTNET_CLI_UI_LANGUAGE=en` / `en-US`), alcançando 100% de aprovação (845/845) sem alterar código de produto na baseline.

---

## 2. CONTRATOS & LIMITES DA ARQUITETURA

* **Projetos Afetados na Solution (.sln):**
  - Mapeamento de todos os projetos da solution real do Nagi (`Nagi.sln` / projetos WinUI 3, Core, Services, UI, etc.).
* **Tipos/Serviços Existentes que DEVEM ser reutilizados e auditados:**
  - Scanner de biblioteca e persistência de tracks/pastas.
  - Pipeline de reprodução de áudio (LibVLC / MediaEngine).
  - Equalizador de áudio e gerenciador de DSP.
  - Leitor de metadata local e cache remoto.
  - Mecanismo de letras (lyrics local e providers).
  - Container de Injeção de Dependência (DI) e ciclo de vida.
  - Suíte de testes automatizados existente.
* **Convenções Obrigatórias:**
  - Reutilizar a arquitetura real já existente no Nagi.
  - Não criar services, repositories, DTOs, ViewModels, factories, adapters ou pipelines paralelos.
  - Fixar a tag/SHA base do Nagi como referência imutável.
  - Registrar formalmente os comandos oficiais de build e test da toolchain .NET.
  - Registrar gaps reais contra `PRD.md` e `IDEIA.md`.

---

## 3. FATIAS VERTICAIS DE IMPLEMENTAÇÃO (Max 3 Slices)

### Slice 1: Build/Test Baseline
- **Meta Imutável repetida:** Comprovar o estado real do repositório Nagi usado como base, garantindo build e testes funcionais sem adicionar novas features.
- **Escopo ponta a ponta:** Clonar/fixar upstream no tag/SHA definido, executar restore/build/test na toolchain oficial, registrar comandos definitivos e documentar warnings/falhas existentes.
- **Reutilização obrigatória:** Toolchain .NET e scripts de build existentes no repositório.
- **Teste obrigatório:** Execução limpa dos testes existentes da baseline.
- **Validação Local:** `dotnet restore` + `dotnet build` + `dotnet test`.

### Slice 2: Architecture & Capability Audit
- **Meta Imutável repetida:** Mapear rigorosamente a arquitetura real do Nagi para evitar duplicação ou abstrações concorrentes.
- **Escopo ponta a ponta:** Mapear componentes de scanner, playback, EQ, metadata, lyrics, persistence e DI; documentar os tipos concretos existentes que devem ser reutilizados pelas features subsequentes.
- **Reutilização obrigatória:** Solution real e todos os namespaces/serviços já implementados.
- **Teste obrigatório:** Matriz de auditoria de tipos/serviços e verificação de integridade da DI.
- **Validação Local:** Compilação da solution com relatório arquitetural gerado.

### Slice 3: Characterization & Gap Report
- **Meta Imutável repetida:** Consolidar relatório verificável do que já existe e das lacunas reais contra o PRD, protegendo o projeto contra regressões.
- **Escopo ponta a ponta:** Adicionar testes de caracterização (characterization tests) para comportamentos críticos identificados, gerar relatório de gaps contra o PRD e atualizar o alinhamento das features futuras.
- **Reutilização obrigatória:** Framework de testes existente no Nagi.
- **Teste obrigatório:** Suíte completa de testes de regressão/caracterização executada com sucesso.
- **Validação Final:** Solution inteira compilando e suíte de testes passando.

---

## 4. GATES DE VALIDAÇÃO (.NET Toolchain)

```powershell
$env:DOTNET_CLI_UI_LANGUAGE = "en"
dotnet restore Nagi.sln -p:Platform=x64
dotnet build Nagi.sln --configuration Release -p:Platform=x64 --warnaserror
dotnet test tests/Nagi.Core.Tests/Nagi.Core.Tests.csproj --configuration Release --no-build
```

Nenhuma tarefa pode ser marcada como concluída se a solution não compilar, DI estiver inconsistente ou testes falharem.

---

## 5. AGENT GUARDRAILS

1. Leia antes de alterar: `.specify/memory/constitution.md`, `IDEIA.md`, `PRD.md`.
2. Não invente arquitetura nem crie código paralelo.
3. Não faça refatoração de produto ou código novo nesta baseline.
4. Máximo padrão de 3 fatias verticais.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Validação do Ambiente de Build e Testes (Priority: P1)
Como desenvolvedor do projeto Resonance, quero compilar e testar a versão base do Nagi com a toolchain .NET para ter certeza de que o fork é reprodutível e estável antes de qualquer alteração.

**Why this priority**: É o pré-requisito mandatório para garantir que nenhuma alteração seja feita sobre uma base quebrada.

**Independent Test**: Execução de `dotnet build` e `dotnet test` a partir do repositório base fixado, confirmando build e testes sem regressão.

**Acceptance Scenarios**:
1. **Given** o repositório base clonado no SHA fixado, **When** os comandos oficiais de build forem executados, **Then** todos os projetos da solution compilam com sucesso.
2. **Given** a compilação bem-sucedida, **When** os testes automatizados da baseline forem disparados, **Then** os resultados refletem o estado auditado sem falhas não documentadas.

---

### User Story 2 - Mapeamento Arquitetural de Componentes Existentes (Priority: P2)
Como arquiteto/desenvolvedor, quero um catálogo detalhado de serviços, DI, persistência e playback existentes no Nagi para saber quais classes e interfaces reutilizar nas próximas features.

**Why this priority**: Evita a criação de services, repositories ou pipelines de áudio paralelos.

**Independent Test**: Inspeção do inventário arquitetural confrontado com a codebase real e grafo de DI.

**Acceptance Scenarios**:
1. **Given** a codebase do Nagi, **When** a auditoria de DI e persistência for executada, **Then** todas as classes de serviço, interfaces e ciclo de vida de componentes são catalogados.
2. **Given** o catálogo gerado, **When** um agente for implementar uma nova feature, **Then** os tipos obrigatórios para reutilização estão mapeados.

---

### User Story 3 - Relatório de Gaps contra o PRD e Testes de Caracterização (Priority: P3)
Como stakeholder do projeto, quero um relatório claro comparando o que o Nagi já oferece com o que o PRD do Resonance exige, amparado por testes de caracterização para as áreas críticas.

**Why this priority**: Permite que as features 001 a 010 sejam planejadas e executadas sem suposições incorretas sobre a base.

**Independent Test**: Execução dos testes de caracterização criados e conferência do gap report contra os requisitos do PRD.

**Acceptance Scenarios**:
1. **Given** as funcionalidades do Nagi, **When** comparadas aos requisitos do PRD, **Then** cada lacuna é categorizada e mapeada para sua respectiva feature (001 a 010).
2. **Given** componentes críticos existentes (ex.: playback, scanner), **When** testes de caracterização são executados, **Then** o comportamento esperado é verificado e blindado contra regressões.

---

### Edge Cases
- Falhas pré-existentes ou avisos de compilação no upstream do Nagi devem ser documentados como baseline, e não mascarados.
- Diferenças de ambiente ou dependências nativas (como runtimes WinUI 3 ou LibVLC) devem ser explicitamente registradas nos pré-requisitos da solution.
- Execução de testes em máquinas com cultura diferente de inglês (como `pt-BR`) traduz strings de fallback (ex.: "Desconhecido Artista"), o que causa divergência com asserções que esperam o literal em inglês; portanto, os comandos de validação devem definir `$env:DOTNET_CLI_UI_LANGUAGE = 'en'` ou executar em cultura neutra.
- O pacote `SixLabors.ImageSharp` 4.1.1 requer compilação em `Debug` para execução local irrestrita ou injeção da variável de licença de código aberto `SixLaborsLicenseKey` no modo `Release`.

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: O sistema de documentação da baseline DEVE registrar o commit SHA exato e a URL de origem do upstream do Nagi.
- **FR-002**: O processo de auditoria DEVE documentar formalmente os comandos exatos de restore, build e test que funcionam na solution.
- **FR-003**: A especificação da baseline DEVE inventariar os serviços de DI, o provedor de banco de dados/persistência e a biblioteca de reprodução de áudio.
- **FR-004**: O relatório de gaps DEVE mapear cada requisito do `PRD.md` para a respectiva feature do catálogo (001 a 010) ou marcar como já existente na baseline.
- **FR-005**: Testes de caracterização DEVEM ser adicionados apenas para capturar e proteger comportamentos existentes contra quebras futuras.

### Key Entities

- **Baseline Audit Record**: Registro contendo SHA, data, versão de SDK .NET, lista de projetos compilados e status dos testes existentes.
- **Architecture Inventory**: Mapeamento dos serviços existentes (DI, Scanner, Playback, Equalizer, Metadata, Lyrics, Database).
- **Gap Analysis Matrix**: Matriz comparativa entre requisitos do PRD e capacidades comprovadas na baseline.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% dos projetos da solution compilam com sucesso no modo Release sem novos erros.
- **SC-002**: Suíte de testes existente executa com taxa de aprovação de 100% dos testes suportados (845 testes) com cultura neutra/inglês (`DOTNET_CLI_UI_LANGUAGE=en`).
- **SC-003**: 100% das capabilities do catálogo (001 a 010) possuem status mapeado (existente, parcial ou ausente na baseline).
- **SC-004**: Tempo de build e execução de testes registrado e reprodutível na máquina local.

---

## Assumptions

- A baseline usa .NET 10.0 com WinUI 3 conforme a versão 2.3.0 do Nagi original.
- Nenhuma funcionalidade de usuário final ou refatoração será introduzida nesta feature.
- O código da codebase real do Nagi é a única fonte da verdade arquitetural.
