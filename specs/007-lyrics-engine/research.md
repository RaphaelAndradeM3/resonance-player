# Research: Feature 007 — Lyrics Engine

## Context & Objectives
A **Feature 007** estabelece o mecanismo robusto e local-first de resolução, cache e sincronização de letras no Resonance Player. O sistema deve unificar fontes embutidas (tags físicas), arquivos locais adjacentes (*sidecars* `.lrc` e `.txt`), cache persistente da aplicação e provedores remotos opcionais (LRCLIB e NetEase), garantindo suporte a letras sincronizadas (com destaque e seek por clique), letras estáticas, faixas instrumentais e calibração fina de offset temporal.

---

## Decisions & Design Rationales

### 1. Canonical 6-Step Resolution Chain
- **Decision**: O motor de resolução (`ILrcService` / `LrcService`) executará estritamente a ordem de 6 etapas:
  1. **Embedded Synced**: Letras sincronizadas embutidas no arquivo de áudio via ATL (`LyricsInfo.SynchronizedLyrics`).
  2. **Embedded Plain**: Letras estáticas de texto puro embutidas no arquivo via ATL (`LyricsInfo.UnsynchronizedLyrics`).
  3. **Sidecar `.lrc`**: Arquivo `.lrc` na mesma pasta do áudio (prioridade: `<NomeDoAudio>.lrc`, fallback: `<Artista> - <Título>.lrc`, case-insensitive).
  4. **Sidecar `.txt`**: Arquivo `.txt` na mesma pasta do áudio (prioridade: `<NomeDoAudio>.txt`, fallback: `<Artista> - <Título>.txt`, case-insensitive).
  5. **Local Cache**: Letra previamente baixada ou salva em `%LocalAppData%` no diretório de cache gerenciado pelo Resonance.
  6. **Remote Providers**: Provedores online habilitados (LRCLIB, NetEase), se o usuário tiver busca online ativada e a faixa ainda não tiver sido checada (`LyricsLastCheckedUtc == null`).
- **Rationale**: Cumpre o Princípio Constitucional IV (Local-First & Privacy-First). Se qualquer fonte local (1 a 4) responder, zero requisições HTTP são disparadas.
- **Alternatives Considered**:
  - *Buscar online concorrentemente com arquivos locais*: Rejeitado porque gasta banda desnecessariamente e viola o princípio de soberania dos dados do usuário.
  - *Checar apenas arquivos `.lrc` ignorando tags embutidas* (comportamento herdado do Nagi): Rejeitado porque arquivos com tags ID3 SYLT/USLT ou Vorbis LYRICS seriam ignorados forçando downloads remotos redundantes.

### 2. ATL.NET Extraction of Embedded Lyrics
- **Decision**: Utilizar `ATL.Track.Lyrics` diretamente na extração de letras locais em `LrcService` / `AtlMetadataService`.
  - Para letras sincronizadas embutidas, converter as fases de tempo (`LyricsPhase.TimestampStart` e `Text`) para o modelo unificado de `LyricLine`s ordenadas.
  - Para letras estáticas embutidas, ler `LyricsInfo.UnsynchronizedLyrics` como texto puro formatado.
- **Rationale**: ATL.NET já é a biblioteca canônica do projeto para inspeção e gravação de tags (utilizada nas Features 002, 003 e 006). Nenhuma biblioteca externa adicional é necessária.
- **Alternatives Considered**:
  - *TagLib#*: Rejeitado por violar o Princípio Constitucional I (proibição de bibliotecas paralelas redundantes).
  - *Regex manual sobre bytes brutos*: Rejeitado devido à fragilidade em containers complexos (FLAC, MP4/M4A, OGG).

### 3. Local Cache Isolation vs Explicit Sidecar Export
- **Decision**:
  - Downloads de provedores remotos (LRCLIB/NetEase) são gravados estritamente no diretório de cache interno (`%LocalAppData%\Resonance\Cache\Lrc\`) indexados pelo hash de identidade do áudio, sem tocar na pasta do arquivo de música.
  - A interface oferece um botão/ação explícita de "Exportar como .lrc" que salva uma cópia limpa em `<NomeDoAudio>.lrc` no mesmo diretório da mídia.
- **Rationale**: A Constituição proíbe expressamente escrita silenciosa no sistema de arquivos do usuário. O cache isolado garante que letras baixadas continuem disponíveis offline, enquanto o comando de exportação atende aos usuários que desejam manter bibliotecas portáveis com arquivos sidecar.
- **Alternatives Considered**:
  - *Gravar automaticamente `.lrc` na pasta da música*: Rejeitado por violação direta da Constituição ("Remote metadata MUST NOT silently overwrite local files").
  - *Guardar letras apenas no SQLite*: Rejeitado porque letras longas aumentariam excessivamente o banco e dificultariam inspeção e limpeza de cache pelo usuário.

### 4. Database Schema: Instrumental Flag and Offset Persistence
- **Decision**:
  - Adicionar campos à entidade `Song`:
    - `IsInstrumental` (`bool?`): Indica se a faixa é instrumental (confirmada por resposta `instrumental: true` do LRCLIB ou detecção manual).
    - `LyricsOffsetMs` (`int?`): Compensação temporal em milissegundos aplicada aos timestamps da letra para aquela faixa específica.
  - Criar migration EF Core `AddLyricsInstrumentalAndOffsetToSong`.
  - Atualizar `ILibraryWriter` com `UpdateSongLyricsOffsetAsync` e `UpdateSongInstrumentalAsync`.
- **Rationale**: Elimina chamadas repetidas a cada reprodução de faixas instrumentais ou clássicas e memoriza a calibração de sincronização feita pelo usuário para que a música toque perfeitamente sincronizada em sessões futuras.
- **Alternatives Considered**:
  - *Guardar offset apenas no arquivo `.lrc` com tag `[offset:+500]`*: Rejeitado porque não funciona para letras embutidas no arquivo, nem para arquivos em mídia somente leitura.

### 5. UI Architecture in WinUI LyricsPage
- **Decision**: Efetuar evolução in-place em `LyricsPage.xaml` e `LyricsPageViewModel.cs`:
  - **Badge de Proveniência**: Indicador textual/pílula no cabeçalho mostrando a fonte atual (`Embutida (Sincronizada)`, `Embutida (Texto)`, `Arquivo .lrc`, `Arquivo .txt`, `Cache Local`, `LRCLIB`).
  - **Visualização de Texto Puro**: Quando a letra for estática, renderizar bloco formatado com rolagem manual livre, ocultar seek ao clicar e exibir badge `[Não Sincronizada]`.
  - **Estado Vazio Contextualizado**: Diferenciar "♫ Faixa Instrumental" de "Nenhuma letra disponível", evitando confusão com erros de rede.
  - **Controles de Offset**: Botões rápidos (`-500ms`, `-100ms`, `+100ms`, `+500ms`, `Zerar`) integrados à interface, alterando dinamicamente o cálculo de linha ativa e persistindo no banco.
  - **Botão de Exportação**: Botão discreto com ícone de download/export para salvar `.lrc` na pasta do áudio.
- **Rationale**: Reutiliza a infraestrutura de animações fluidas baseadas em Composition API já presentes no Nagi, sem recriar páginas paralelas ou duplicar ViewModels.
