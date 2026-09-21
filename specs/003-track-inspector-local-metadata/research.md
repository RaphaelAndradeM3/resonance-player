# Research: 003 — Track Inspector & Local Metadata

**Feature Branch**: `003-track-inspector-local-metadata`  
**Date**: 2026-09-20  
**Status**: Completed  
**Spec Reference**: [spec.md](./spec.md)

---

## 1. Technical Decisions & Research Findings

### Decision 1: Extração e Mapeamento de Propriedades Técnicas via ATL.NET

- **Decision**: Estender o `SongFileMetadata` e os métodos de leitura do `AtlMetadataService` existente para capturar os 23 atributos técnicos e tags mapeados na especificação, aproveitando as propriedades nativas de `ATL.Track`.
- **Rationale**:
  - `ATL.Track` já inspeciona internamente cabeçalhos e fluxos para todos os formatos da biblioteca (MP3, FLAC, WAV, AAC, M4A, OGG, Opus, WMA, AIFF, APE, WavPack, DSD, Musepack).
  - Propriedades técnicas nativas disponíveis em `ATL.Track`:
    - `track.AudioFormat.Name`: Container format (ex.: "FLAC", "MPEG Audio", "Ogg", "MPEG-4").
    - `track.AudioFormat.ShortName`: Codec de áudio conciso.
    - `track.Bitrate`: Taxa de bits média em kbps.
    - `track.BitrateType`: Enumeração indicando `CBR` (Constant), `VBR` (Variable) ou `ABR` (Average).
    - `track.BitDepth`: Profundidade de bits (ex.: 16, 24, 32 bits, ou 0/null para lossy perceptual).
    - `track.SampleRate`: Frequência de amostragem em Hz.
    - `track.ChannelsArrangement.NbChannels`: Número de canais (1=Mono, 2=Stereo, 6=5.1, etc.).
    - `track.Duration`: Duração precisa em segundos (com fração).
    - `track.AdditionalFields`: Dicionário contendo tags adicionais como `ISRC`, `REPLAYGAIN_TRACK_GAIN`, `REPLAYGAIN_TRACK_PEAK`, `REPLAYGAIN_ALBUM_GAIN`, `REPLAYGAIN_ALBUM_PEAK`, `MUSICBRAINZ_TRACKID`, `MUSICBRAINZ_RELEASEID`, `MUSICBRAINZ_ARTISTID`, `ACOUSTID_ID`.
    - `track.EmbeddedPictures`: Lista de imagens embutidas contendo dados binários (`PictureData`), MIME type, tipo de imagem (Front, Back, etc.), dimensões (`Width`, `Height`).
  - O princípio I da Constituição ("Existing Code Is the Source of Truth") veta a criação de novos parsers paralelos quando o `AtlMetadataService` já possui a infraestrutura completa.
- **Alternatives Considered**:
  - *Adicionar biblioteca TagLib#*: Rejeitado. Introduziria dependência binária redundante e conflitante com ATL.NET.
  - *Executar FFprobe/FFmpeg via processo*: Rejeitado. O overhead de processamento é proibitivo para abertura em <150ms e viola a premissa de uso de bibliotecas gerenciadas locais.

---

### Decision 2: Arquitetura de UI — Painel Lateral Retrátil (Collapsible Docked Panel)

- **Decision**: Implementar o Track Inspector como um `UserControl` (`TrackInspectorControl`) posicionado em uma coluna dedicada à direita da grade principal de `MainPage.xaml`, controlado por estado observável no `TrackInspectorViewModel` e animado suavemente via `VisualStateManager` / `Storyboard`.
- **Rationale**:
  - Alinhado com a clarified answer na sessão de esclarecimento: o usuário pode inspecionar os detalhes de uma faixa sem perder o contexto da lista de reprodução, sem cobrir a biblioteca e sem bloquear a barra de reprodução (*Now Playing*).
  - A largura do painel (ex.: 360px) é compacta, elegante e fluida em monitores padrão e widescreen. Em janelas menores (layout responsivo adaptativo), o painel pode sobrepor temporariamente ou reduzir a margem do conteúdo principal.
  - O controle pode ser reutilizado ou aberto a partir de múltiplos disparadores: atalho de teclado `Alt+Enter`, menu de contexto de música ("Inspecionar Faixa / Propriedades") e botão dedicado na barra do player.
- **Alternatives Considered**:
  - *ContentDialog Modal*: Rejeitado durante o esclarecimento com o usuário porque impedia a interação com a fila de reprodução e pausava a navegação na biblioteca.
  - *Janela Separada (AppWindow/Desktop Window)*: Desnecessariamente complexa para inspeções rápidas, exigindo coordenação de ciclo de vida e gerenciamento de múltiplos HWNDs.

---

### Decision 3: Decodificação de Capas em Alta Resolução e Proteção Contra Bloqueio de UI

- **Decision**: Carregamento assíncrono das imagens em segundo plano usando `IImageProcessor` / `ImageSharpProcessor` com geração de miniatura com limite de amostragem para a UI e carregamento sob demanda da imagem original em caso de ampliação (*LightBox*).
- **Rationale**:
  - Arquivos de áudio audiófilos (especialmente FLAC Hi-Res) frequentemente contêm capas embutidas de 10MB a 25MB em resoluções de 3000x3000px ou superiores.
  - Decodificar essas imagens na thread principal de UI bloquearia o renderizador por centenas de milissegundos, violando o critério `SC-003` (<16ms na thread de UI).
  - O fluxo seguro executa a extração dos bytes e metadados de imagem (`Width`, `Height`, MIME type) em `Task.Run` e cria o `BitmapImage` com `DecodePixelWidth` ajustado (ex.: 400px) para a miniatura do painel, mantendo a imagem original disponível apenas para exportação ou exibição no modal de zoom (*LightBox*).
- **Alternatives Considered**:
  - *Carregar a imagem completa diretamente no Image control do XAML*: Rejeitado. Causa consumo excessivo de memória RAM e stutter perceptível ao rolar a lista de músicas.

---

### Decision 4: Suporte a Atalho de Teclado `Alt+Enter` e Teclas de Navegação

- **Decision**: Registrar `KeyboardAccelerator` para `Key="Enter"` com `Modifiers="Menu"` (Alt) no escopo de `MainPageRoot` e nas páginas de listagem de músicas, vinculado a `ToggleInspectorCommand` do `TrackInspectorViewModel`.
- **Rationale**:
  - `Alt+Enter` é o atalho universal do Windows (File Explorer e reprodutores clássicos) para abrir a janela de propriedades de um arquivo selecionado.
  - Ao inspecionar uma seleção de múltiplas faixas, as teclas de seta (`Left` e `Right`) ou os atalhos de paginação permitem navegar sequencialmente entre as faixas selecionadas conforme definido no `FR-011`.
- **Alternatives Considered**:
  - *Atalho `Ctrl+I`*: Menos intuitivo no ambiente Windows tradicional, embora possa ser adicionado como atalho secundário.

---

### Decision 5: Proveniência de Metadados e IDs Externos

- **Decision**: Modelo de dados explícito com badges informativos de proveniência (`Source: LocalFileTag`, `Source: AdjacentFolder`, `Source: RemoteProvider`) e campos de identificadores com botão de cópia rápida para o clipboard (`IClipboardService` / `Windows.ApplicationModel.DataTransfer.DataPackage`).
- **Rationale**:
  - Atende aos princípios IV (Local-First) e VII (Explicit Boundaries) da Constituição.
  - Evita qualquer confusão sobre se um dado foi lido do arquivo físico ou derivado de indexação externa.
