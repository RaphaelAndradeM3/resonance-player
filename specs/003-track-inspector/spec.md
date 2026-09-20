# FEATURE SPEC: 003 — Track Inspector & Local Metadata

**Feature Branch**: `003-track-inspector`  
**Created**: 2026-09-20  
**Status**: Draft  
**Input**: Baseado em `FEATURES_SUGERIDAS_SPEC_KIT.md` (Feature 003)

---

## 1. META IMUTÁVEL (Global Goal)

> **Problema de Negócio:** O usuário precisa visualizar com clareza tanto as tags musicais quanto os detalhes técnicos reais do arquivo que está tocando ou foi indexado.
>
> **Definição de Sucesso:** A aplicação mostra metadata local, propriedades técnicas, IDs disponíveis e origem dos dados para qualquer faixa suportada.
>
> **Regra de Ouro:** Reutilizar o mecanismo atual de leitura de metadata antes de adicionar novo parser.

---

## 2. CONTRATOS & LIMITES DA ARQUITETURA

* **Projetos Afetados na Solution (.sln):**
  - Módulos de extração de metadados, modelos de dados de track, e componentes de UI do Track Inspector.
* **Tipos/Serviços Existentes que DEVEM ser reutilizados:**
  - Leitor de tags existente (ex.: TagLib# ou leitor de metadados do Nagi).
  - Modelos de faixa (`Track`, `MediaFile`) e repositórios existentes.
  - Infraestrutura de exibição de capas de álbum (artwork caching).
* **Campos Desejados de Metadados e Propriedades Técnicas:**
  - **Tags Musicais:** title, artist, album artist, album, track/disc number, year/date, genre, comment, ISRC, ReplayGain (track/album gain e peak), artwork, lyrics presence.
  - **Propriedades Técnicas do Arquivo:** path, size (bytes/MB), duration, container format, audio codec, bitrate (kbps), bitrate mode (CBR/VBR), sample rate (Hz/kHz), bit depth (16/24/32-bit), channels (Mono/Stereo/5.1).
  - **Identificadores Externos:** AcoustID, MusicBrainz Recording ID, Release ID, Artist ID (quando disponíveis).
* **Convenções Obrigatórias:**
  - Reutilizar a leitura de tags existente sem introduzir bibliotecas conflitantes.
  - Exibir explicitamente a procedência dos dados (se vieram de tags locais ou de enriquecimento externo).
  - Carregamento assíncrono para não travar a UI ao inspecionar arquivos pesados ou remotos.

---

## 3. FATIAS VERTICAIS DE IMPLEMENTAÇÃO (Max 3 Slices)

### Slice 1: Technical & Tag Read Model
- **Meta Imutável repetida:** Extrair dados de tags e propriedades técnicas completas usando a infraestrutura existente, normalizando valores ausentes e erros.
- **Escopo ponta a ponta:** Desenvolver modelo de leitura consolidado que preenche propriedades musicais e técnicas reais para todos os formatos suportados, com tratamento de valores nulos.
- **Reutilização obrigatória:** Serviços de leitura de metadados existentes.
- **Teste obrigatório:** Testes unitários com arquivos reais/sintéticos verificando a extração precisa de bitrate, sample rate, canais, codec e tags.
- **Validação Local:** `dotnet build` + testes da suíte de metadata.

### Slice 2: Inspector UI & Proveniência dos Dados
- **Meta Imutável repetida:** Apresentar os dados de forma elegante, organizada e compreensível, destacando a origem (provenance) dos campos.
- **Escopo ponta a ponta:** Construir tela/painel de Track Inspector (diálogo ou painel lateral) com abas ou seções claras ("Informações Musicais", "Detalhes Técnicos", "Capa & Letras"), incluindo tratamento de loading e erro.
- **Reutilização obrigatória:** Controles WinUI e estilos de tipografia do Resonance.
- **Teste obrigatório:** Testes de ViewModel garantindo formatação correta de durações, taxas de bits, frequências e tratamento de capa ausente.
- **Validação Local:** `dotnet build` + testes de ViewModel.

### Slice 3: Integração com Now Playing / Library e Regressão
- **Meta Imutável repetida:** Permitir acesso ao Inspector a partir de qualquer ponto da aplicação (Now Playing, lista de faixas, álbuns) de forma rápida e responsiva.
- **Escopo ponta a ponta:** Adicionar pontos de entrada (menu de contexto "Inspecionar Faixa / Propriedades", atalho de teclado `Alt+Enter` ou botão na barra de reprodução) e garantir atualização automática se a faixa ativa mudar.
- **Reutilização obrigatória:** Menus de contexto e comandos existentes da UI.
- **Teste obrigatório:** Teste de integração ponta a ponta acionando o Inspector pela UI em faixas tocando e da biblioteca.
- **Validação Final:** Solution inteira compilando e testes passando.

---

## 4. GATES DE VALIDAÇÃO (.NET Toolchain)

```powershell
dotnet restore
dotnet build --configuration Release --warnaserror
dotnet test --configuration Release --no-build
```

Nenhuma tarefa pode ser marcada como concluída se o Inspector travar a UI ao abrir arquivos grandes ou se campos técnicos forem exibidos com dados fictícios.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Consulta a Detalhes Técnicos de Alta Fidelidade (Priority: P1)
Como ouvinte que se importa com a qualidade do áudio, quero abrir os detalhes da música em reprodução para saber se estou ouvindo um arquivo lossless (ex: FLAC 24-bit 96kHz) ou lossy (ex: MP3 320kbps).

**Why this priority**: É a funcionalidade central do Track Inspector para os usuários foco do Resonance.

**Independent Test**: Tocar um arquivo FLAC 24-bit e abrir o Inspector; conferir se taxa de amostragem, profundidade de bits e codec são exibidos com precisão.

**Acceptance Scenarios**:
1. **Given** uma faixa em reprodução, **When** o usuário abre o Inspector, **Then** as propriedades técnicas reais (codec, bitrate, sample rate, bit depth, canais) são exibidas.
2. **Given** um arquivo com bitrate variável (VBR), **When** inspecionado, **Then** o modo de bitrate é indicado apropriadamente.

---

### User Story 2 - Visualização Completa de Tags Musicais e Capa (Priority: P2)
Como organizador de música, quero conferir todas as tags salvas no arquivo, incluindo número de disco, compositor, gênero e capa em alta resolução.

**Why this priority**: Essencial para verificar se a organização dos álbuns e metadados locais está correta.

**Independent Test**: Abrir o Inspector em faixa contendo todas as tags e capa embutida; verificar a correta renderização de todos os campos e visualização da arte.

**Acceptance Scenarios**:
1. **Given** uma faixa com metadados preenchidos, **When** o Inspector é aberto, **Then** título, artista, álbum, ano, faixa/disco, gênero e comentários são listados sem truncamentos indevidos.
2. **Given** uma faixa com capa embutida, **When** o Inspector é aberto, **Then** a imagem da capa é exibida com suas dimensões e formato.

---

### User Story 3 - Acesso Rápido a Partir da Biblioteca e da Barra Now Playing (Priority: P3)
Como usuário navegando na biblioteca ou ouvindo música, quero clicar com o botão direito em qualquer faixa ou usar um atalho para abrir o Inspector imediatamente.

**Why this priority**: Garante agilidade e ergonomia de uso.

**Independent Test**: Clicar com o botão direito numa faixa da lista e selecionar "Propriedades da Faixa"; verificar abertura instantânea do painel.

**Acceptance Scenarios**:
1. **Given** qualquer item na lista de músicas, **When** o menu de contexto "Propriedades" for acionado, **Then** o Inspector daquela faixa específica é exibido.
2. **Given** o Inspector aberto para a faixa atual do Now Playing, **When** a próxima faixa começar a tocar, **Then** o Inspector atualiza os dados automaticamente se configurado para seguir a reprodução.

---

### Edge Cases
- Arquivos sem nenhuma tag escrita (deve exibir nomes derivados do arquivo de forma clara e neutra).
- Arquivos com capas gigantescas (ex: 20MB embutidas) que poderiam travar o carregamento da janela (implementar decodificação assíncrona otimizada).
- Arquivos em unidades removíveis desconectadas logo após a solicitação de inspeção.

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: O sistema DEVE extrair e exibir metadados musicais completos para qualquer faixa suportada.
- **FR-002**: O sistema DEVE extrair e exibir propriedades técnicas de áudio (codec, container, bitrate, sample rate, bit depth, channels).
- **FR-003**: O sistema DEVE exibir a arte da capa embutida ou de arquivo local adjacente (cover.jpg/folder.jpg).
- **FR-004**: O sistema DEVE indicar a procedência de cada metadado (Tag Local vs Provedor Remoto).
- **FR-005**: O sistema DEVE permitir abertura do Inspector via menu de contexto, botão dedicado e atalho `Alt+Enter`.
- **FR-006**: O sistema DEVE carregar os detalhes de forma não bloqueante em background.

### Key Entities

- **TrackTechnicalDetails**: Informações de áudio em baixo nível (codec, sample rate, bit depth, channels, bitrate, duration, container).
- **TrackMetadataView**: Visão consolidada de apresentação unindo tags musicais, atributos técnicos e proveniência de dados.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: O painel do Inspector abre e renderiza dados em menos de 150ms para arquivos locais.
- **SC-002**: 100% dos 23 campos descritos nos Contratos da Arquitetura são suportados pelo modelo de dados.
- **SC-003**: Zero travamentos na UI durante a leitura de propriedades técnicas em arquivos pesados.

---

## Assumptions

- O leitor de tags existente já provê acesso básico à maioria das tags e streams de áudio.
- O Inspector nesta fase é estritamente para visualização (a edição de tags é escopo da Feature 006).
