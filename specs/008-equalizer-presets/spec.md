# FEATURE SPEC: 008 — Equalizer & Preset Manager

**Feature Branch**: `008-equalizer-presets`  
**Created**: 2026-09-20  
**Status**: Draft  
**Input**: Baseado em `FEATURES_SUGERIDAS_SPEC_KIT.md` (Feature 008)

---

## 1. META IMUTÁVEL (Global Goal)

> **Problema de Negócio:** O equalizador existente precisa oferecer uma experiência de uso comparável aos players clássicos, com presets claros e personalização persistente.
>
> **Definição de Sucesso:** O usuário escolhe, cria, salva, aplica e gerencia presets sem reiniciar perceptivelmente a música e sem causar clipping silencioso.
>
> **Regra de Ouro:** Evoluir o equalizador existente. Não criar segundo pipeline DSP.

---

## 2. CONTRATOS & LIMITES DA ARQUITETURA

* **Projetos Afetados na Solution (.sln):**
  - Módulo DSP/Equalizador de áudio, serviços de persistência de configurações e interface de equalização.
* **Tipos/Serviços Existentes que DEVEM ser reutilizados:**
  - Instância e pipeline do equalizador existente (ex.: LibVLC Equalizer com 10 bandas de frequência).
  - Repositório de configurações/preferências do usuário.
* **Bandas e Presets Canônicos:**
  - 10 bandas de frequência (ex.: 31Hz, 63Hz, 125Hz, 250Hz, 500Hz, 1kHz, 2kHz, 4kHz, 8kHz, 16kHz) e controle de Preamp (-20dB a +20dB).
  - **Presets Embutidos Originais:** Flat, Classical, Club, Dance, Full Bass, Full Bass & Treble, Full Treble, Laptop, Large Hall, Live, Party, Pop, Reggae, Rock, Ska, Soft, Soft Rock, Techno, Vocal.
* **Convenções Obrigatórias:**
  - Aplicação dos ganhos em tempo real (live apply) **sem** pausar, reiniciar ou engasgar a música.
  - Proteção contra distorção harmônica e saturação digital (Clipping Guard / Limiter suave ou compensação de Preamp).
  - Persistência imediata de presets personalizados do usuário e do último estado ativo do equalizador.
  - Não recriar pipeline DSP; operar sobre o equalizador já integrado ao motor de reprodução.

---

## 3. FATIAS VERTICAIS DE IMPLEMENTAÇÃO (Max 3 Slices)

### Slice 1: Preset Model & Persistence Layer
- **Meta Imutável repetida:** Fornecer modelo completo para presets de fábrica (built-in) e personalizados do usuário, com operações de salvar, renomear, duplicar e excluir.
- **Escopo ponta a ponta:** Implementar catálogo de presets padrão em código/JSON, repositório de persistência de presets customizados e serialização do estado atual (ganhos por banda, preamp, ligado/desligado).
- **Reutilização obrigatória:** Sistema de persistência e settings existente.
- **Teste obrigatório:** Testes unitários para criação, edição, deleção de presets e garantia de imutabilidade dos presets de fábrica.
- **Validação Local:** `dotnet build` + testes da suite de equalizador/presets.

### Slice 2: Live EQ UI & Sliders em Tempo Real
- **Meta Imutável repetida:** Permitir ao usuário ajustar bandas e chavear presets através de uma UI gráfica responsiva com aplicação instantânea de áudio sem interrupção perceptível.
- **Escopo ponta a ponta:** Construir interface do Equalizador com 10 faders verticais, controle de Preamp, toggle liga/desliga, botão "Zerar/Flat" e menu dropdown de presets conectado ao engine de áudio.
- **Reutilização obrigatória:** Equalizador do player e controles WinUI.
- **Teste obrigatório:** Testes de ViewModel garantindo que a alteração de fader despacha o novo ganho para o motor de áudio em tempo real.
- **Validação Local:** `dotnet build` + testes de integração UI/DSP.

### Slice 3: Clipping Guard & Validação de Regressão
- **Meta Imutável repetida:** Evitar distorção e estalos quando ganhos altos forem aplicados e garantir restauração do último estado ao reabrir o app.
- **Escopo ponta a ponta:** Implementar cálculo de teto dinâmico (auto-preamp ou clipping guard) para reduzir automaticamente o preamp quando bandas são impulsionadas, e teste de regressão de persistência entre sessões do player.
- **Reutilização obrigatória:** Pipeline de áudio e configurações.
- **Teste obrigatório:** Testes de cálculo de atenuação de clipping com sinais senoidais próximos a 0 dBFS.
- **Validação Final:** Solution inteira compilando e testes passando.

---

## 4. GATES DE VALIDAÇÃO (.NET Toolchain)

```powershell
dotnet restore
dotnet build --configuration Release --warnaserror
dotnet test --configuration Release --no-build
```

Nenhuma tarefa pode ser marcada como concluída se a alteração de equalização reiniciar o stream de áudio ou causar chiados e estalos na saída.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Aplicação Instantânea de Presets Clássicos (Priority: P1)
Como ouvinte de rock, quero selecionar o preset "Rock" no equalizador e sentir a mudança imediata no som sem nenhum corte ou reinício na música.

**Why this priority**: É a essência da experiência clássica de equalização em players de música.

**Independent Test**: Tocar música, selecionar alternadamente "Full Bass", "Rock" e "Vocal"; confirmar que o áudio permanece contínuo e a curva sonora muda em tempo real.

**Acceptance Scenarios**:
1. **Given** música em reprodução com EQ ativado, **When** o usuário escolhe um preset, **Then** os ganhos de todas as bandas são atualizados instantaneamente.
2. **Given** a troca de presets, **When** o novo perfil é carregado, **Then** não ocorre pausa nem estalo de áudio.

---

### User Story 2 - Criação e Salvamento de Presets Personalizados (Priority: P2)
Como usuário que calibrou suas próprias frequências para um fone de ouvido específico, quero salvar esse ajuste com o nome "Meu Fone" e poder reutilizá-lo sempre.

**Why this priority**: Permite que o usuário personalize sua experiência acústica e mantenha seus ajustes protegidos.

**Independent Test**: Modificar 3 sliders, clicar em "Salvar Preset Como", digitar o nome, reiniciar o aplicativo e verificar se o preset customizado continua disponível.

**Acceptance Scenarios**:
1. **Given** valores personalizados nos sliders, **When** o usuário aciona "Salvar Como", **Then** o preset é adicionado à lista de presets do usuário.
2. **Given** um preset do usuário selecionado, **When** o usuário desejar excluí-lo ou renomeá-lo, **Then** a operação é executada com sucesso.

---

### User Story 3 - Prevenção Automática de Distorção (Clipping Guard) (Priority: P3)
Como usuário que elevou os graves ao máximo (+12dB), quero que o sistema ajuste o pré-amplificador para evitar distorção digital estridente nos alto-falantes.

**Why this priority**: Protege os ouvidos do usuário e a integridade do som reproduzido.

**Independent Test**: Subir todas as bandas para o ganho máximo (+12dB); verificar se o clipping guard atenua o sinal mantendo a saída limpa e sem corte de onda.

**Acceptance Scenarios**:
1. **Given** ganhos acumulados que excedem a faixa dinâmica de 0 dBFS, **When** o clipping guard estiver ativado, **Then** o sistema atenua o ganho global proporcionalmente.
2. **Given** o retorno a valores neutros (Flat), **When** restaurado, **Then** a atenuação do clipping guard é liberada.

---

### Edge Cases
- Alternar liga/desliga do equalizador rapidamente durante a reprodução.
- Presets corrompidos no arquivo de configurações (devem cair de volta para "Flat" sem quebrar o app).
- Tentativa de exclusão de presets de fábrica embutidos (deve ser bloqueada na UI e na regra de negócio).

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: O sistema DEVE disponibilizar equalizador gráfico de 10 bandas com faixa de ajuste de pelo menos -12dB a +12dB.
- **FR-002**: O sistema DEVE disponibilizar controle de ganho geral (Preamp).
- **FR-003**: O sistema DEVE incluir a lista canônica de pelo menos 19 presets pré-definidos de fábrica.
- **FR-004**: O sistema DEVE permitir a criação, salvamento, renomeação e exclusão de presets do usuário.
- **FR-005**: O sistema DEVE aplicar as mudanças de equalização sem reiniciar a reprodução da faixa.
- **FR-006**: O sistema DEVE incorporar proteção contra clipping (clipping guard) para prevenir distorções digitais.
- **FR-007**: O sistema DEVE restaurar o estado exato e o preset ativo ao reiniciar o aplicativo.

### Key Entities

- **EqualizerPreset**: Nome, identificador, flag se é de fábrica ou do usuário, valor do preamp e array de 10 ganhos em decibéis.
- **EqualizerState**: Status ativo/inativo, preset selecionado, ganhos correntes e modo de proteção contra clipping.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Tempo de aplicação do novo perfil sonoro inferior a 50ms.
- **SC-002**: Zero reinicializações ou engasgos audíveis durante ajustes de sliders.
- **SC-003**: 100% de persistência dos presets do usuário entre sessões consecutivas do aplicativo.

---

## Assumptions

- O motor de reprodução do Nagi (LibVLC) expõe a API de equalizador nativo de 10 bandas.
- Os presets de fábrica são somente-leitura e não podem ser apagados pelo usuário.
