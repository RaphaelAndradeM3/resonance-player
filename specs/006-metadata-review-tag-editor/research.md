# Research & Architectural Decisions: 006 — Metadata Review, Tag Editor & File Update

**Branch**: `006-metadata-review-tag-editor`  
**Date**: 2026-09-22  
**Status**: Completed  
**Spec Reference**: [spec.md](./spec.md)

---

## 1. Tag Writing Engine (Escritor Físico de Tags)

### Contexto
O Resonance suporta múltiplos formatos de áudio (MP3, FLAC, M4A/AAC, OGG, OPUS, WAV, AIF, WMA). Para atualizar tags físicas sem introduzir dependências redundantes nem quebrar padrões de áudio, é necessário definir a biblioteca e camada de abstração de escrita.

### Decisão
Reutilizar a biblioteca **ATL.NET** (`ATL.Track`), já integrada na Solution através do `AtlMetadataService.cs`, criando um serviço dedicado e desacoplado: `ITagWriterService` / `SafeTagWriterService` em `Resonance.Core`.

### Racional
1. **Conformidade Constitucional (Princípio I)**: "Existing Code Is the Source of Truth" — ATL já é a dependência central de metadados do projeto. Adicionar TagLib# ou TagWriter externo criaria dependência paralela desnecessária.
2. **Amplo Suporte de Formatos**: ATL.NET suporta escrita nativa em ID3v2.3, ID3v2.4, Vorbis Comments (FLAC/OGG), MP4 Atoms (M4A/AAC), APE tags e WMA, preservando o stream PCM e blocos de áudio intactos.
3. **Imagens Embutidas**: Suporte robusto a `track.EmbeddedPictures.Add(PictureInfo.fromBinaryData(...))` e remoção de imagens anteriores.

### Alternativas Consideradas
- **TagLib#**: Muito popular em .NET, mas exigiria adicionar novo pacote NuGet, aumentando o footprint do binário e duplicando lógica de I/O de áudio já resolvida pelo ATL.NET.
- **Escrita via FFmpeg**: Ineficiente para edição exclusiva de tags (exige remuxing de streams inteiros e gera I/O desnecessário).

---

## 2. Estratégia de Gravação Atômica e Proteção Contra Corrupção

### Contexto
A Regra de Ouro e o critério **SC-001** exigem 100% de preservação da integridade do arquivo. Em caso de interrupção abrupta (queda de energia, exceção de I/O, disco cheio), o arquivo original de áudio jamais pode ser corrompido ou truncado.

### Decisão
Implementar um pipeline atômico de escrita em 4 etapas no `SafeTagWriterService`:
1. **Cópia de Trabalho Temporária**: Copiar o arquivo original para um arquivo temporário no **mesmo diretório** (`<nome>.tmp.<guid>`). Manter no mesmo volume garante que a substituição final seja uma operação atômica de ponteiro de diretório no sistema de arquivos NTFS/FAT32.
2. **Escrita de Tags no Arquivo Temporário**: Abrir o temporário com ATL, aplicar o `TagWritePlan`, gravar as tags (`track.Save()`) e descarregar todos os streams.
3. **Validação Pós-Gravação**: Reler o arquivo temporário com ATL para certificar que o cabeçalho é legível, os metadados foram persistidos e a duração do áudio (`DurationMs > 0`) permanece consistente com o original.
4. **Substituição Segura**: Substituir o original utilizando `File.Replace(tempPath, originalPath, backupPath, ignoreMetadataStoreErrors: true)` com exclusão do backup logo após o sucesso. Em caso de falha em qualquer etapa anterior, o temporário é excluído e o original permanece intacto.

### Alternativas Consideradas
- **Escrita direta in-place**: Rejeitada terminantemente. Uma falha de energia durante `Save()` corromperia o cabeçalho do arquivo original.
- **Temporário no `%TEMP%` do sistema**: Rejeitada. Se `%TEMP%` residir em outro disco (ex.: C:\ vs D:\Músicas), a substituição final exige cópia completa e não é atômica, abrindo janela de inconsistência.

---

## 3. Coordenação de Lock com o Player de Áudio

### Contexto
No Windows, o engine de reprodução (LibVLC via `LibVlcAudioPlayerService`) mantém um handle de arquivo aberto para leitura enquanto a faixa está em reprodução. Tentar substituir ou renomear o arquivo original enquanto ele toca resulta em `IOException: The process cannot access the file because it is being used by another process`.

### Decisão
Implementar coordenação inteligente via `IMusicPlaybackService`:
1. O serviço de gravação verifica se o arquivo alvo é a faixa atualmente carregada no player (`_playbackService.CurrentTrack?.FilePath == targetPath`).
2. Se estiver tocando ou carregada:
   - Armazena o estado atual (`wasPlaying = _playbackService.IsPlaying`, `currentPosition = _playbackService.CurrentPosition`).
   - Solicita descarregamento temporário ou pause controlado com liberação de mídia.
   - Aplica a substituição atômica do arquivo.
   - Restaura a reprodução suavemente na mesma posição (`_playbackService.SeekAsync(currentPosition)` e `PlayAsync()` se estava tocando).
3. Se o arquivo estiver sob lock exclusivo de outro processo do SO (ex.: outro player aberto ou antivírus), captura a exceção com segurança, cancela a operação, limpa o temporário e avisa o usuário com mensagem amigável sem corromper o original.

---

## 4. Tratamento do Atributo "Somente Leitura" (Read-Only)

### Contexto
Muitas bibliotecas locais de música contêm arquivos extraídos de CD-ROMs ou backups antigos marcados com a flag `FileAttributes.ReadOnly`. No .NET, `File.Replace` e `File.Move` disparam `UnauthorizedAccessException` se o arquivo de destino for Somente-Leitura.

### Decisão
1. Antes de iniciar a substituição atômica, o sistema inspeciona se `File.GetAttributes(originalPath).HasFlag(FileAttributes.ReadOnly)`.
2. Se positivo, o sistema remove a flag programaticamente durante a gravação autorizada (`File.SetAttributes(originalPath, attributes & ~FileAttributes.ReadOnly)`).
3. Conclui a substituição atômica.
4. Caso ocorra erro de permissão real de segurança (ACL NTFS / falta de permissão de escrita na pasta), a exceção é interceptada e convertida em uma mensagem compreensível na UI ("Permissão negada pelo sistema operacional").

---

## 5. Arquitetura de Entidades e Diff (`TagDiffRecord` & `TagWritePlan`)

### Contexto
A especificação exige visualização diferencial campo a campo antes da escrita e suporte tanto à aplicação de propostas online (`EnrichmentProposal` da Feature 005) quanto à edição manual direta.

### Decisão
- **`TagDiffRecord`**:
  - `FieldKey` (string canonical, ex.: "Title", "Artist", "Album", "Year", "TrackNumber", "Genre", "Picture")
  - `DisplayName` (string amigável, ex.: "Título", "Artista")
  - `OriginalValue` (string?)
  - `ProposedValue` (string?)
  - `Status` (Enum: `Unchanged`, `Updated`, `NewValue`, `Conflict`)
  - `IsSelected` (bool observável para binding em checkboxes)
  - `IsPictureField` (bool para diferenciar capas de texto)
- **`TagWritePlan`**:
  - `FilePath` (string)
  - `SongId` (Guid?)
  - `FieldChanges` (IReadOnlyList de `TagDiffRecord` onde `IsSelected == true`)
  - `NewPictureData` (byte[]? ou stream da imagem a embutir)
  - `RemoveExistingPicture` (bool)
- **`ITagDiffService`**:
  - Converte `EnrichmentProposal` em `TagWritePlan`.
  - Compara estado de formulário manual com o arquivo físico e gera `TagWritePlan`.

---

## 6. Interface de Usuário WinUI (`TagEditorDialog`)

### Contexto
O usuário deve poder:
1. Revisar propostas do MusicBrainz geradas na Feature 005.
2. Editar tags manualmente quando desejar corrigir pequenos erros.
3. Ter confirmação explícita antes de qualquer escrita física.

### Decisão
Criar o `TagEditorDialog` em `src/Resonance.WinUI/Dialogs/`:
- Baseado em `ContentDialog` do WinUI 3, utilizando o `XamlRoot` da janela principal.
- Apresenta:
  - Header com título da faixa e botão de ação clara ("Gravar Tags" e "Cancelar").
  - Coluna/painel de campos editáveis (Título, Artistas, Artista do Álbum, Álbum, Ano, Faixa, Total de Faixas, Disco, Total de Discos, Gênero, Comentário).
  - Quando aberto com uma proposta online ativa, exibe badges de status ("Novo", "Atualizado", "Conflito") e checkboxes por campo para aceitar ou rejeitar individualmente a sugestão.
  - Seletor e preview de capa de álbum (Capa atual vs Capa proposta em 500px).
  - Barra de status de gravação com indicador de progresso e tratamento visual de erros.
- Integração imediata com o `TrackInspectorViewModel.AdvanceToTagReviewAsync` existente.

---

## 7. Sincronização Imediata com a Persistência Local (SQLite)

### Contexto
O critério **SC-002** estipula que a persistência local reflita as novas tags em menos de 50ms após a confirmação.

### Decisão
Logo após a substituição atômica bem-sucedida no disco:
1. Invocar `_metadataService.ExtractMetadataAsync(filePath)` para reler os metadados consolidados.
2. Atualizar a entidade `Song` correspondente através de `_libraryWriter.UpdateSongAsync(song)` e sincronizar tags desnormalizadas.
3. Disparar notificação na biblioteca para que listas de reprodução, álbum e Track Inspector atualizem imediatamente na tela sem necessidade de re-escaneamento completo.
