# FEATURE SPEC: 002 — Multi-Format Audio Library

**Feature Branch**: `002-multi-format-audio-library`  
**Created**: 2026-09-20  
**Status**: Draft  
**Input**: Baseado em `FEATURES_SUGERIDAS_SPEC_KIT.md` (Feature 002)

---

## 1. META IMUTÁVEL (Global Goal)

> **Problema de Negócio:** A biblioteca deve lidar corretamente com todos os formatos de áudio realmente suportados pelo Nagi/LibVLC e não apenas MP3, evitando divergência entre o que o scanner aceita e o que o player consegue tocar.
>
> **Definição de Sucesso:** MP3, FLAC, WAV e demais formatos suportados pela baseline são descobertos, identificados e reproduzidos conforme a capability real da aplicação; arquivos incompatíveis são reportados de forma explícita.
>
> **Regra de Ouro:** Não manter listas duplicadas de extensões se a codebase já possui fonte de capability.

---

## User Scenarios & Testing

### User Story 1 - Fonte Única de Formatos e Matriz de Capacidades (Priority: P1)

Como usuário com uma coleção de músicas diversificada (MP3, FLAC, WAV, AAC, M4A, OGG, Opus, WMA, AIFF, APE, WavPack e DSD), quero que todas as minhas faixas suportadas sejam descobertas e cadastradas na biblioteca com seus formatos técnicos reconhecidos, sem que extensões fiquem de fora por omissão ou divergência entre scanner e player.

**Why this priority**: É a fundação arquitetural da feature. Elimina divergências entre o scanner de arquivos e o pipeline de áudio, garantindo que toda extensão permitida pelo scanner tenha suporte garantido de extração de metadados e demuxing no player.

**Independent Test**: Testes automatizados validando que cada extensão contida na fonte da verdade possui mapeamento válido no leitor de metadados e no serviço de reprodução, sem listas duplicadas espalhadas pela solução.

**Acceptance Scenarios**:
1. **Given** uma pasta contendo faixas nos formatos MP3, FLAC, WAV, AAC, M4A, OGG, Opus, WMA, AIFF, APE, WV e DSF/DFF, **When** o scanner analisa a pasta, **Then** todos os arquivos são identificados como áudio suportado e incluídos na fila de indexação.
2. **Given** um arquivo com extensão desconhecida ou não suportada (ex: `.txt`, `.exe`, `.pdf`), **When** o scanner percorre o diretório, **Then** o arquivo é ignorado silenciosamente sem gerar erros.

---

### User Story 2 - Varredura Resiliente e Tratamento de Arquivos Incompatíveis/Corrompidos (Priority: P2)

Como usuário, quero que o scanner lide graciosamente com arquivos corrompidos, faixas com cabeçalhos inválidos ou arquivos vazios, registrando o problema de forma clara sem abortar a indexação do restante da coleção.

**Why this priority**: Coleções grandes frequentemente possuem faixas danificadas, downloads incompletos ou formatos exóticos. O scanner deve ser imune a travamentos causados por arquivos corrompidos.

**Independent Test**: Testes de extração com arquivos sintéticos corrompidos (0 bytes, headers truncados, extensões falsas) comprovando que o serviço marca `ExtractionFailed` e prossegue com os arquivos válidos.

**Acceptance Scenarios**:
1. **Given** um arquivo de áudio com extensão válida (`.flac`) mas com conteúdo corrompido ou truncado, **When** a extração de metadados é executada, **Then** o sistema marca a falha no metadado sem lançar exceções não tratadas e preserva a estabilidade da biblioteca.
2. **Given** um arquivo vazio (0 bytes) com extensão de áudio, **When** a varredura o encontra, **Then** o arquivo é identificado como inválido/corrompido e descartado da lista de músicas ativas.

---

### User Story 3 - Reprodução Homogênea e Transparência na UI (Priority: P3)

Como usuário, quero poder reproduzir qualquer faixa suportada da biblioteca e ver na interface as informações técnicas reais do formato (container, bitrate, sample rate, canais), com feedback claro se uma faixa específica falhar na reprodução.

**Why this priority**: Fecha o ciclo de ponta a ponta: o usuário precisa ouvir o que foi indexado e entender o formato de áudio em reprodução.

**Independent Test**: Testes de reprodução com `LibVlcAudioPlayerService` cobrindo o envio dos hints corretos de formato e demuxers nativos para cada extensão suportada.

**Acceptance Scenarios**:
1. **Given** uma faixa em formato Lossless/Hi-Res (ex: FLAC 96kHz/24bit, DSD `.dsf` ou Opus), **When** o usuário clica em reproduzir, **Then** o player inicializa o demuxer correto no LibVLC e reproduz o áudio sem falhas.
2. **Given** uma faixa que não pode ser decodificada pelo player em tempo de execução, **When** o usuário tenta reproduzi-la, **Then** o player emite um evento de erro claro na interface sem travar a navegação.

---

### Edge Cases

- Arquivo de vídeo com faixa de áudio (ex: `.mp4`, `.m4v`, `.webm`): Como o player e scanner devem tratar contêineres de vídeo suportados que contêm áudio?
- Arquivos DSD (`.dsf` e `.dff`): Suporte a taxas de amostragem extremamente altas e leitura de tags ID3v2 específicas do formato DSF.
- Contêineres Musepack (`.mpc`, `.mpp`): Seleção dinâmica de demuxer SV7 vs SV8 via inspeção de conteúdo.
- Arquivos com extensões trocadas (ex: um MP3 renomeado para `.wav`): O leitor de tags e o LibVLC devem identificar o fluxo real pelo cabeçalho mágico (*magic bytes*).

---

## Requirements

### Functional Requirements

- **FR-001**: O sistema DEVE manter uma fonte única e canônica de extensões de áudio suportadas em `Resonance.Core.Constants.FileExtensions.MusicFileExtensions`. Nenhuma outra classe deve manter listas paralelas ou divergentes de extensões de áudio.
- **FR-002**: O sistema DEVE oferecer suporte integral de catálogo e reprodução para os formatos listados na baseline:
  - **MPEG Audio**: `.mp3`
  - **Lossless Standards**: `.flac`, `.wav`, `.aiff`
  - **Advanced Audio Coding**: `.aac`, `.m4a`, `.m4b`, `.mp4`
  - **Ogg / Xiph**: `.ogg`, `.oga`, `.opus`, `.webm`
  - **Windows Media**: `.wma`, `.asf`
  - **Specialized Lossless**: `.ape` (Monkey's Audio), `.wv` (WavPack)
  - **Direct Stream Digital (DSD)**: `.dsf`, `.dff`
  - **Musepack**: `.mpc`, `.mpp`
- **FR-003**: O serviço de extração de metadados (`AtlMetadataService`) DEVE preencher com precisão os dados técnicos da faixa (`Duration`, `Bitrate`, `SampleRate`, `Channels`) independentemente do formato subjacente.
- **FR-004**: O reprodutor de áudio (`LibVlcAudioPlayerService`) DEVE mapear os parâmetros adequados de demuxer (`UsesNativeDemuxer` e `GetAvFormatHint`) para todas as extensões registradas, assegurando que nenhum formato suportado pelo scanner falhe no player por falta de hint.
- **FR-005**: O leitor de metadados DEVE isolar erros de parsing em arquivos corrompidos ou não reconhecidos, preenchendo `ExtractionFailed = true` e `ErrorMessage` descritivo, impedindo a interrupção do scanner.
- **FR-006**: O scanner e o repositório DEVEM persistir os metadados técnicos de formato no banco SQLite para que possam ser exibidos nas telas da biblioteca e no painel de reprodução.

---

### Key Entities

- **Song**: Entidade de persistência do Resonance que armazena os metadados musicais e atributos técnicos do arquivo de áudio (`FilePath`, `Duration`, `Bitrate`, `SampleRate`, `Channels`, `FileCreatedDate`, `FileModifiedDate`, etc.).
- **SongFileMetadata**: DTO de trânsito emitido pelo `AtlMetadataService` contendo metadados extraídos e informações diagnósticas de sucesso/falha da leitura do formato.
- **AudioFormatCapability**: Matriz conceitual relacionando extensão, codec, leitor de metadados (ATL), hint de reprodução (LibVLC) e compatibilidade com tags.

---

## Success Criteria

### Measurable Outcomes

- **SC-001**: 100% dos formatos mandatados (MP3, FLAC, WAV, AAC, M4A, OGG, Opus, WMA, AIFF, APE, WavPack, DSD) possuem paridade comprovada por testes unitários e de integração entre Scanner, MetadataService e AudioPlayerService.
- **SC-002**: Zero duplicatas ou listas hardcoded desconectadas de extensões de arquivo no código-fonte da solução.
- **SC-003**: 100% de tolerância a falhas na presença de arquivos de áudio corrompidos ou de tamanho zero durante o scan, com zero falhas não tratadas.
- **SC-004**: A extração de metadados multi-formato mantém tempo médio inferior a 50ms por arquivo em armazenamento local.

---

## Assumptions

- O player utiliza LibVLC 4.0 (`VideoLAN.LibVLC.Windows`) como backend nativo, que já possui decodificadores internos para a vasta maioria dos codecs de áudio comuns.
- A extração de metadados continuará utilizando a biblioteca ATL.NET (`z4kn4fein.atl.core`), que fornece suporte nativo a dezenas de formatos e contêineres de áudio.
- Arquivos de áudio DSD (`.dsf` e `.dff`) exigem hardware ou decodificação PCM via LibVLC, que é nativamente transparente na versão 4.0.
- Não serão introduzidas dependências externas binárias fora das já existentes no projeto.
