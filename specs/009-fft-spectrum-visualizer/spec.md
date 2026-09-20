# FEATURE SPEC: 009 — FFT / Spectrum / Waveform Visualizer

**Feature Branch**: `009-fft-spectrum-visualizer`  
**Created**: 2026-09-20  
**Status**: Draft  
**Input**: Baseado em `FEATURES_SUGERIDAS_SPEC_KIT.md` (Feature 009)

---

## 1. META IMUTÁVEL (Global Goal)

> **Problema de Negócio:** O usuário quer visualizar em tempo real o conteúdo espectral da música tocando, sem prejudicar reprodução, responsividade ou consumo quando o recurso estiver desligado.
>
> **Definição de Sucesso:** O player expõe spectrum em tempo real com barras/linha, testes FFT determinísticos e visualização desligável sem impacto perceptível no playback.
>
> **Regra de Ouro:** Não substituir o playback engine apenas para obter FFT sem antes provar necessidade técnica.

---

## 2. CONTRATOS & LIMITES DA ARQUITETURA

* **Projetos Afetados na Solution (.sln):**
  - Módulo de captura de amostras PCM/áudio, módulo de análise de transformada rápida de Fourier (FFT), e controle gráfico de renderização de espectro.
* **Tipos/Serviços Existentes que DEVEM ser reutilizados:**
  - Callbacks de áudio/PCM do motor de reprodução existente (ex.: LibVLC audio callbacks).
  - Loop de renderização da UI (Composition / WinUI Canvas / DispatcherQueueTimer).
* **Convenções Obrigatórias:**
  - **FFT Desligada = Pipeline Desligado:** Se o visualizador estiver minimizado, oculto ou desativado, nenhum processamento de áudio ou cálculo matemático de FFT deve ser executado (consumo zero de CPU extra).
  - Separação estrita entre o analisador de áudio (Audio Analyzer - processamento matemático) e o renderizador gráfico (Spectrum Renderer).
  - Taxa de quadros limitada e controlada (ex.: 30 ou 60 FPS fixos) para garantir fluidez sem sobrecarregar a GPU/CPU.
  - Algoritmo de suavização com decaimento suave (falloff/decay) e picos flutuantes (peak hold) clássicos.
  - Não substituir o motor de playback existente sem justificativa aprovada.

---

## 3. FATIAS VERTICAIS DE IMPLEMENTAÇÃO (Max 3 Slices)

### Slice 1: Sample Capture & FFT Core
- **Meta Imutável repetida:** Capturar amostras de áudio PCM em tempo real e transformá-las em magnitudes de frequência discretas via FFT com testes matemáticos determinísticos.
- **Escopo ponta a ponta:** Conectar callback de áudio PCM ao buffer circular; implementar rotina de janelamento (Hann/Hamming) e algoritmo FFT gerando magnitudes agrupadas em bandas de frequência (logarítmicas ou lineares).
- **Reutilização obrigatória:** Motor de áudio existente e buffers de áudio.
- **Teste obrigatório:** Testes unitários com ondas senoidais puras sintetizadas (ex: 440Hz, 1kHz) verificando a identificação exata dos picos de frequência na FFT.
- **Validação Local:** `dotnet build` + testes da suite de FFT matemática.

### Slice 2: Spectrum Renderer (Barras, Linha e Animação)
- **Meta Imutável repetida:** Desenhar na tela o espectro animado em tempo real com barras ou linha contínua, com interpolação e decaimento natural.
- **Escopo ponta a ponta:** Construir controle WinUI dedicado de renderização (Canvas/Win2D ou Shapes otimizadas); implementar física de decaimento (gravity/decay), suavização temporal e modos visuais (barras com picos ou linha de onda).
- **Reutilização obrigatória:** Estilos e paleta de cores do player.
- **Teste obrigatório:** Testes de renderização com simulação de dados de espectro garantindo ausência de alocação excessiva de memória por quadro.
- **Validação Local:** `dotnet build` + testes de componente gráfico.

### Slice 3: Otimização de Performance, Modo Econômico e Regressão
- **Meta Imutável repetida:** Assegurar que o visualizador não prejudique a continuidade do áudio nem consuma recursos excessivos, desligando completamente o pipeline quando inativo.
- **Escopo ponta a ponta:** Adicionar interruptor liga/desliga, detecção de visibilidade da janela (ocultar janela = pausar timer), limitação de FPS configurável (30 vs 60 FPS) e teste de estabilidade durante playback contínuo.
- **Reutilização obrigatória:** Configurações do usuário e controle de visibilidade da UI.
- **Teste obrigatório:** Medição de uso de CPU com visualizador ativado vs desativado, e garantia de ausência de dropouts no áudio.
- **Validação Final:** Solution inteira compilando e testes passando.

---

## 4. GATES DE VALIDAÇÃO (.NET Toolchain)

```powershell
dotnet restore
dotnet build --configuration Release --warnaserror
dotnet test --configuration Release --no-build
```

Nenhuma tarefa pode ser marcada como concluída se o cálculo de FFT causar engasgos no playback de áudio ou consumir CPU de forma contínua com a janela oculta.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Visualização de Espectro Vibrante em Tempo Real (Priority: P1)
Como fã da estética clássica de players de música (estilo Winamp), quero ver barras de frequência dançando em sincronia precisa com as batidas da música.

**Why this priority**: É um dos principais apelos visuais e nostálgicos da proposta do Resonance.

**Independent Test**: Tocar música com batida forte; verificar se as barras de graves reagem instantaneamente aos bumbos e as barras de agudos aos pratos da bateria.

**Acceptance Scenarios**:
1. **Given** música tocando e visualizador aberto, **When** as frequências sonoras variam, **Then** as barras do espectro refletem as magnitudes instantaneamente sem defasagem perceptível.
2. **Given** as barras em movimento, **When** um pico ocorre, **Then** o marcador de pico flutua brevemente e decai suavemente.

---

### User Story 2 - Eficiência Energética e Desligamento Total Inativo (Priority: P2)
Como usuário usando notebook na bateria, quero poder desligar o visualizador ou minimizar o player sabendo que a CPU não ficará ocupada calculando gráficos que ninguém está vendo.

**Why this priority**: Eficiência energética, respeito à máquina do usuário e robustez do produto.

**Independent Test**: Minimizar o player com visualizador ativado; monitorar com profiler/contador para comprovar consumo 0% de CPU para FFT enquanto minimizado.

**Acceptance Scenarios**:
1. **Given** o visualizador ativado, **When** a janela do player é minimizada ou o usuário muda para outra aba, **Then** o loop de cálculo e renderização é pausado imediatamente.
2. **Given** o visualizador desativado nas configurações, **When** a música toca, **Then** os callbacks de PCM não executam operações de FFT.

---

### User Story 3 - Alternância de Modos de Exibição (Barras vs Linha) (Priority: P3)
Como usuário, quero poder escolher se prefiro ver o espectro como barras verticais discretas ou como uma linha suave de onda espectral.

**Why this priority**: Flexibilidade de personalização da experiência estética.

**Independent Test**: Clicar no visualizador para alternar entre "Barras de Espectro" e "Curva Contínua"; conferir a transição visual imediata.

**Acceptance Scenarios**:
1. **Given** o visualizador na tela, **When** o usuário clica ou seleciona o modo "Linha", **Then** a renderização se transforma em uma onda suave.
2. **Given** o modo selecionado, **When** o app for reiniciado, **Then** a preferência do usuário é mantida.

---

### Edge Cases
- Áudio em silêncio ou pausa (as barras devem cair para zero suavemente, sem travar no último estado).
- Áudio mono vs áudio estéreo (combinar canais ou oferecer modo estéreo espelhado sem quebrar o array).
- Resolução de tela e DPI scaling altos (4K/High-DPI sem borramento ou perda de desempenho).

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: O sistema DEVE capturar dados de áudio PCM brutos sem interferir na reprodução sonora.
- **FR-002**: O sistema DEVE computar a Transformada Rápida de Fourier (FFT) em blocos de pelo menos 512 ou 1024 amostras.
- **FR-003**: O sistema DEVE aplicar função de janelamento para minimizar vazamento espectral.
- **FR-004**: O sistema DEVE agrupar as frequências em pelo menos 16 a 32 barras de espectro ponderadas logaritmicamente.
- **FR-005**: O sistema DEVE suportar física de decaimento (decay) e retenção de picos (peak hold).
- **FR-006**: O sistema DEVE suspender imediatamente o processamento de FFT quando o visualizador estiver invisível ou desativado.

### Key Entities

- **SpectrumFrame**: Array de magnitudes normalizadas (0.0 a 1.0) por banda de frequência e valores de pico correspondentes.
- **VisualizerConfig**: Modo de visualização (Barras/Linha), taxa de atualização alvo (30/60 FPS) e estado ligado/desligado.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Latência entre o evento sonoro audível e a reação visual inferior a 35ms.
- **SC-002**: Taxa de quadros estável em 60 FPS (ou 30 FPS no modo econômico) sem jitter.
- **SC-003**: Uso adicional de CPU inferior a 3% em processadores modernos durante a execução do visualizador, e exatamente 0% adicional quando minimizado.

---

## Assumptions

- O motor de reprodução do Nagi permite interceptação ou leitura de buffers PCM sem bloquear o fluxo de decodificação.
- Renderização utiliza aceleração gráfica padrão do WinUI 3 (Win2D ou Composition).
