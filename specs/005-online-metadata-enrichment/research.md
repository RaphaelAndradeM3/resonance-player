# Research & Architectural Decisions: Feature 005 — Online Metadata Enrichment

**Feature**: `005-online-metadata-enrichment`  
**Date**: 2026-09-22  
**Spec**: [spec.md](./spec.md)

---

## 1. MusicBrainz Web Service v2 Integration

### Context
A aplicação precisa consultar metadados detalhados de gravações e lançamentos (álbum, ano original, número da faixa, número do disco, gravadora, código ISRC e tags de gênero). O Resonance já possui um serviço base `MusicBrainzService` registrado no container de DI que pesquisa artistas por nome.

### Decisions
1. **Extensão do `IMusicBrainzService` e `MusicBrainzService`**:
   - Adicionar método `GetRecordingMetadataAsync(string recordingMbid, CancellationToken ct)`.
   - Adicionar método `SearchRecordingsAsync(string artist, string title, CancellationToken ct)` como fallback quando a música não tiver `MusicBrainzTrackId`.
2. **Endpoint e Parâmetros**:
   - Consulta direta: `https://musicbrainz.org/ws/2/recording/{mbid}?inc=releases+artists+media+isrcs+tags&fmt=json`
   - Busca textual: `https://musicbrainz.org/ws/2/recording?query=recording:{title}+AND+artist:{artist}&limit=5&fmt=json`
3. **Respeito a Rate Limits e User-Agent**:
   - Manter o cabeçalho obrigatório `User-Agent: Resonance/1.0 (+https://github.com/RaphaelAndradeM3/resonance-player)`.
   - Utilizar o `IProviderPipelineProvider` existente configurado para `ServiceProviderIds.MusicBrainz` com token bucket limitador de 1 requisição por segundo e circuit breaker.
4. **Heurística Canônica de Seleção de Lançamento (FR-009)**:
   - Uma gravação pode estar presente em dezenas de lançamentos (álbum original, coletâneas, edições especiais, singles, ao vivo).
   - *Algoritmo*:
     1. Filtrar lançamentos com `status == "Official"` e `primary-type == "Album"`.
     2. Se o metadado local já contiver um nome de álbum (`TrackAudioTags.Album`), priorizar o lançamento cujo título coincida exatamente (case-insensitive).
     3. Caso contrário (ou se nenhuma coincidência for encontrada), selecionar o lançamento com a data mais antiga (`date`), garantindo a preservação do ano original de gravação e da primeira tiragem física.
     4. Se não houver lançamentos do tipo "Album", aceitar "EP" ou "Single", evitando "Compilation" a menos que seja a única opção existente.

### Alternatives Considered
- *Biblioteca terceira (ex.: MetaBrainz.MusicBrainz)*: Rejeitada. Adicionaria dependência externa desnecessária quando o `HttpClient` e o `System.Text.Json` da solution já atendem a especificação com zero acoplamento.
- *Carregar todos os lançamentos via chamadas adicionais*: Rejeitada. O parâmetro `inc=releases+media` já retorna a lista resumida de lançamentos e suas mídias na mesma chamada, evitando estourar o limite de 1 req/s.

---

## 2. Resolução de Capa Oficial via Cover Art Archive

### Context
O Cover Art Archive (CAA) é um serviço conjunto do Internet Archive e da MetaBrainz Foundation que armazena imagens de alta resolução para lançamentos do MusicBrainz.

### Decisions
1. **Resolução Padrão (FR-010)**:
   - Utilizar a URL direta `https://coverartarchive.org/release/{releaseMbid}/front-500` (imagem de 500x500 pixels).
   - Se o servidor responder 404 (sem imagem de 500px), aplicar fallback para `https://coverartarchive.org/release/{releaseMbid}/front-250`.
2. **Isolamento de Requisição**:
   - A obtenção da capa ocorre de forma assíncrona desacoplada da busca textual de metadados, utilizando `ServiceProviderIds.ImageDownload`.
   - Se a capa não existir no arquivo público, a proposta simplesmente indica que a arte remota não está disponível, mantendo a arte local inalterada sem falhas.

### Alternatives Considered
- *Download da imagem original (Full Res)*: Rejeitada. Imagens originais podem ter mais de 3000x3000px e pesar 5MB a 15MB, degradando a performance de exibição no Track Inspector.
- *Consulta ao endpoint JSON `/release/{mbid}` do CAA antes do download*: Rejeitada como padrão. A URL `/front-500` redireciona diretamente para a imagem no Internet Archive via HTTP 307, economizando um roundtrip de rede.

---

## 3. Persistência do Cache Local de Metadados

### Context
Conforme FR-004 e FR-011, consultas ao MusicBrainz e respostas do provedor devem ser cacheadas localmente para evitar tráfego repetitivo e economizar os recursos da API pública comunitária.

### Decisions
1. **Localização do Cache**:
   - Armazenar em arquivos JSON individuais no diretório de cache do sistema: `%LocalAppData%/Resonance/cache/metadata/` (via `IAppInfoService.CachePath`).
   - Nome do arquivo: hash SHA256 da URL ou do identificador (`mbid_{recordingMbid}.json`).
2. **Estrutura do Item de Cache**:
   - Objeto encapsulador contendo `CachedAt` (UTC), `ExpiresAt` (UTC, padrão 7 dias) e o payload `MusicBrainzRecordingDetail`.
3. **Limpeza e Invalidação**:
   - Se o arquivo de cache existir e `DateTime.UtcNow < ExpiresAt`, retornar imediatamente sem chamada HTTP (< 10ms).
   - Se expirado ou se o usuário acionar "Recarregar", deletar o arquivo e refazer a consulta remota.
   - O desacoplamento do SQLite evita crescimento da base de dados local (`resonance.db`) e dispensa migrações de banco.

### Alternatives Considered
- *Tabela no SQLite via EF Core*: Rejeitada. Cache de rede é temporário e descartável; colocar blobs JSON no banco de dados principal de biblioteca aumentaria o tamanho do banco e exigiria novas migrações e rotinas de compactação (VACUUM).
- *Cache volátil somente em memória (RAM)*: Rejeitada. Faria o usuário refazer consultas ao reabrir o aplicativo no dia seguinte.

---

## 4. Motor de Mesclagem & Modelo de Proveniência (Merge Engine)

### Context
A aplicação precisa comparar metadados locais com os dados do provedor online, detectando divergências campo a campo e permitindo ao usuário auditar cada informação antes de qualquer decisão.

### Decisions
1. **Arquitetura do Motor de Mesclagem (`IMetadataEnrichmentService`)**:
   - Serviço puramente computacional em `Resonance.Core.Services.Implementations`.
   - Entrada: `SongFileMetadata` (ou `Song`) + `MusicBrainzRecordingDetail` + `CoverArtUrl`.
   - Saída: Objeto imutável `EnrichmentProposal`.
2. **Status dos Campos (`FieldProposalStatus`)**:
   - `Unchanged`: O valor local e o valor sugerido são semanticamente idênticos (após `Trim()` e comparação case-insensitive).
   - `NewValue`: O campo local estava vazio/nulo e o MusicBrainz forneceu um valor.
   - `Updated`: O campo local possuía um valor e o MusicBrainz sugere uma correção de grafia ou atualização natural.
   - `Conflict`: Divergência significativa (ex.: títulos totalmente diferentes, anos conflitantes).
3. **Seleção Interativa (FR-012)**:
   - Cada `FieldProposal` possui a propriedade `IsSelected` (booleana observável).
   - Por padrão, campos com status `NewValue` e `Updated` vêm com `IsSelected = true`; campos com `Unchanged` e `Conflict` vêm com `IsSelected = false`.
4. **Regra Específica de Gênero (FR-013)**:
   - Se o arquivo local tiver "Rock" e o MusicBrainz retornar as tags mais votadas ["Progressive Rock", "Art Rock"]:
   - Proposta gerada: "Rock; Progressive Rock; Art Rock" com status `Updated` e proveniência `MusicBrainz`.
5. **Rastreabilidade de Proveniência (`MetadataProvenance`)**:
   - Valores: `LocalTag`, `MusicBrainz`, `CoverArtArchive`, `UserOverride`.

---

## 5. Track Inspector UI & Apresentação Visual

### Context
A interface deve apresentar os resultados de forma elegante, moderna e alinhada ao design system existente do WinUI 3 no Resonance.

### Decisions
1. **Card Retrátil no Track Inspector**:
   - Inserir um novo card `Expander` denominado "Enriquecimento de Metadados Online" no `TrackInspectorControl.xaml`.
   - Se a faixa já possuir `MusicBrainzTrackId` (reconhecida pelo AcoustID na Feature 004), o botão principal exibe "Buscar Metadados Canônicos".
   - Se a faixa não possuir ID, exibe "Buscar por Artista e Título".
2. **Grid Comparativa de Proposta**:
   - Tabela organizada com:
     - Checkbox (`IsSelected`)
     - Nome do Campo (Título, Artista, Álbum, Ano, Gênero, Faixa, Disco, Gravadora, ISRC)
     - Valor Atual (Local)
     - Valor Sugerido (Online)
     - Badge de Status (Verde para Novo, Amarelo para Atualizado, Roxo para Inalterado, Laranja para Conflito)
     - Badge de Proveniência (`MusicBrainz` / `CoverArtArchive`)
3. **Pré-visualização da Capa**:
   - Exibir miniatura da capa atual lado a lado com a capa sugerida do Cover Art Archive.
4. **Ação de Encaminhamento**:
   - Botão "Avançar para Revisão de Tags" que prepara a proposta em memória para o fluxo seguro de gravação física da Feature 006.
