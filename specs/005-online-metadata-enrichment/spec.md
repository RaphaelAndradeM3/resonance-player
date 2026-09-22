# FEATURE SPEC: 005 — Online Metadata Enrichment

**Feature Branch**: `005-online-metadata-enrichment`  
**Created**: 2026-09-21  
**Status**: Draft  
**Input**: User description: "FEATURE 005 — Online Metadata Enrichment"

---

## 1. META IMUTÁVEL (Global Goal)

> **Problema de Negócio:** Depois de identificar uma música (por exemplo, via AcoustID da Feature 004 ou pela seleção de uma faixa na biblioteca), o usuário frequentemente encontra campos de tags incompletos, inconsistentes ou ausentes (como ano exato de lançamento, número de disco/faixa, gravadora, ISRC, gênero canônico ou arte de capa oficial). O usuário precisa consultar fontes abertas confiáveis na internet para complementar essas informações, mas sem perder suas notas manuais e sem permitir que serviços externos sobrescrevam silenciosamente seus arquivos locais.
>
> **Definição de Sucesso:** A aplicação consulta metadados online estruturados (utilizando o `MusicBrainz Recording ID` já associado à faixa ou pesquisando por Artista + Título quando ausente), obtém informações canônicas de gravação e lançamento (incluindo arte de capa via Cover Art Archive quando disponível), armazena as respostas em cache local persistente respeitando rate limits rigorosos (1 req/s para MusicBrainz), e apresenta uma proposta de enriquecimento (`EnrichmentProposal`) detalhada campo a campo com rastreabilidade explícita da procedência (provenance) e indicador de alteração/conflito, sem jamais gravar nada fisicamente no arquivo de áudio.
>
> **Regra de Ouro:** Dados remotos NUNCA substituem silenciosamente as tags locais nem gravam em arquivos físicos no disco. Toda proposta é estritamente informativa e auditável nesta feature; a gravação física de tags é responsabilidade exclusiva da Feature 006 (Metadata Review & Tag Editor).

---

## Clarifications

### Session 2026-09-21

- Q: Como o sistema deve selecionar o lançamento (álbum) canônico quando uma gravação no MusicBrainz estiver vinculada a múltiplos lançamentos diferentes? (FR-009) → A: Heurística canônica: priorizar lançamentos do tipo primário "Album" e status "Official", selecionando a data de lançamento mais antiga para preservar o ano original e arte da primeira tiragem, priorizando correspondência de título se o álbum local já estiver preenchido.
- Q: Qual resolução de imagem do Cover Art Archive deve ser obtida como padrão para a capa oficial sugerida na proposta de enriquecimento? (FR-010) → A: Resolução padrão front-500 (500x500 pixels), oferecendo nitidez ideal em telas modernas e excelente equilíbrio de desempenho e consumo de dados.
- Q: Onde e como o cache persistente de respostas do MusicBrainz e metadados online deve ser armazenado localmente? (FR-011) → A: Cache baseado em arquivos JSON em disco no diretório da aplicação (IAppInfoService.CachePath/metadata/), desacoplado do banco de dados da biblioteca, com invalidação automática por data de expiração (7 dias).
- Q: Como o usuário deve interagir com os campos sugeridos na proposta de enriquecimento dentro do Track Inspector? (FR-012) → A: Tabela interativa com checkbox individual por campo (selecionados por padrão), permitindo ao usuário escolher quais sugestões deseja manter na proposta antes de enviá-la para a revisão/edição de tags.
- Q: Como o motor de mesclagem deve tratar o campo de Gênero quando a música local já possui um gênero e o MusicBrainz retorna tags de gêneros comunitários adicionais? (FR-013) → A: Fusão inteligente com deduplicação: manter o gênero local existente e propor adicionar as tags de maior relevância do MusicBrainz separadas por ponto-e-vírgula (;).

---

## 2. CONTRATOS & LIMITES DA ARQUITETURA

* **Projetos Afetados na Solution (`Resonance.slnx`):**
  - `Resonance.Core`: Expansão do cliente `IMusicBrainzService` para lookup de gravações e lançamentos (`GetRecordingAsync`, `SearchRecordingsAsync`), integração de metadados do Cover Art Archive, serviço de cache resiliente em memória/banco, motor de mesclagem (`IMetadataEnrichmentService`), e modelos de dados para proposta de enriquecimento (`EnrichmentProposal`, `FieldProposal`, `MetadataProvenance`).
  - `Resonance.WinUI`: Extensão do `TrackInspectorViewModel` e do `TrackInspectorControl.xaml` com o card retrátil "Enriquecimento de Metadados Online", tabela comparativa de campos atuais vs sugeridos, badges visuais de proveniência (ex.: `Local`, `MusicBrainz`, `Cover Art Archive`), indicação de status (Novo, Atualizado, Inalterado, Conflito) e botão de transição para revisão.
  - `Resonance.Core.Tests`: Suíte de testes unitários e de integração mockada cobrindo desserialização de payloads do MusicBrainz, respeito a rate limit, política de cache, cenários de merge campo a campo e resiliência offline.
* **Tipos/Serviços Existentes que DEVEM ser reutilizados:**
  - `IMusicBrainzService` / `MusicBrainzService`: Reutilizar e expandir o serviço já registrado em DI, que utiliza `IProviderPipelineProvider` com rate limit de 1 req/s, circuit breaker e cabeçalho `User-Agent: Resonance/1.0 (+https://github.com/RaphaelAndradeM3/resonance-player)`.
  - `IProviderPipelineProvider`: Execução com fallback, isolamento de falhas e circuit breaker por provedor (`ServiceProviderIds.MusicBrainz`, `ServiceProviderIds.ImageDownload`).
  - `TrackExternalIds`: Utilização do `MusicBrainzTrackId` (Recording ID) associado na Feature 004 para busca direta sem ambiguidade.
  - `TrackInspectorViewModel` / `TrackInspectorControl`: Painel retrátil do Track Inspector implementado na Feature 003 e estendido na Feature 004.
  - `SongFileMetadata` / `TrackAudioTags` / `Song`: Estruturas existentes contendo os dados locais atuais para alimentar a comparação de merge.
  - `ISettingsService`: Verificação da política de privacidade e ativação de provedores online pelo usuário.
* **Provedores Suportados e Regras de Integração:**
  - **MusicBrainz (Primário)**: Endpoint `https://musicbrainz.org/ws/2/recording/{mbid}?inc=releases+artists+media+isrcs+tags&fmt=json` e fallback `https://musicbrainz.org/ws/2/recording?query=...`. Respeitar rigorosamente 1 req/s e identificação de User-Agent.
  - **Cover Art Archive (Complementar)**: Resolução de URL de capa frontal oficial via `https://coverartarchive.org/release/{releaseMbid}/front-500` sem intermediários proprietários.
* **Convenções Obrigatórias:**
  - **Zero gravação em disco**: Nenhum byte de tag física é alterado no arquivo de áudio. A persistência nesta etapa limita-se ao cache de respostas online e propostas temporárias na memória ou SQLite.
  - **Preservação de Proveniência**: Cada campo da proposta DEVE indicar sua fonte original (`LocalTag`, `MusicBrainz`, `CoverArtArchive`).
  - **Resiliência Offline**: Se a máquina estiver sem internet ou o MusicBrainz responder com erro/timeout, a aplicação deve falhar silenciosamente com banner discreto, mantendo a reprodução de áudio e navegação 100% funcionais.
  - **Cache Inteligente**: Consultas ao mesmo `MusicBrainz Recording ID` ou chave de busca repetidas não devem refazer chamadas HTTP redundantes dentro do período de expiração (padrão 7 dias).

---

## 3. FATIAS VERTICAIS DE IMPLEMENTAÇÃO (Max 3 Slices)

### Slice 1: MusicBrainz Recording Lookup, Cover Art Archive & Cache
- **META IMUTÁVEL repetida:**
  - *Problema:* Consultar metadados canônicos completos a partir de um identificador MusicBrainz ou pesquisa de texto, obtendo detalhes de álbum, ano, faixas e URL de capa sem sobrecarregar a API pública.
  - *Definição de Sucesso:* `IMusicBrainzService` obtém os dados canônicos da gravação e lançamento associado via MusicBrainz Web Service v2 (incluindo títulos, artistas, ano, número de faixa, número de disco, gravadora, ISRC e tags de gênero) e resolve a capa oficial via Cover Art Archive, armazenando o resultado em cache local e respeitando a taxa de 1 req/s.
- **Escopo ponta a ponta:**
  - Estender `IMusicBrainzService` e `MusicBrainzService` com `GetRecordingMetadataAsync(string recordingId, CancellationToken ct)` e `SearchRecordingsAsync(string query, CancellationToken ct)`.
  - Criar DTOs de desserialização JSON para o payload completo do MusicBrainz (`MusicBrainzRecordingDetail`, `MusicBrainzRelease`, `MusicBrainzMedia`, `MusicBrainzTag`).
  - Implementar resolução de arte de capa via Cover Art Archive (`https://coverartarchive.org/release/{releaseMbid}/front-500`).
  - Implementar camada de cache (`IMetadataCacheService` ou cache integrado) com expiração configurável e chave baseada em ID/Query.
- **Reutilização obrigatória:** `IProviderPipelineProvider`, `IHttpClientFactory`, `ServiceProviderIds.MusicBrainz`.
- **Teste obrigatório:** Testes unitários com payloads JSON simulados do MusicBrainz (gravação simples, lançamento multi-disco, tags de gênero), verificação de User-Agent, throttle de 1 req/s e validação de cache hit.
- **Validação Local:** `dotnet build Resonance.slnx --configuration Release -p:Platform=x64` + execução dos testes da camada de provedor.

### Slice 2: Metadata Merge Engine & Provenance Model
- **META IMUTÁVEL repetida:**
  - *Problema:* Comparar os metadados locais de uma faixa com as informações recebidas do provedor online, identificando campos ausentes, campos idênticos e divergências/conflitos sem apagar anotações do usuário.
  - *Definição de Sucesso:* Um motor de mesclagem puro (`IMetadataEnrichmentService`) recebe a metadata local atual e a metadata online obtida, compara campo a campo e gera um objeto imutável `EnrichmentProposal` com status claro (`Unchanged`, `Updated`, `NewValue`, `Conflict`) e rótulo de procedência (`Provenance`) para cada item.
- **Escopo ponta a ponta:**
  - Definir modelos em `Resonance.Core.Models`:
    - `MetadataProvenance` (enum: `LocalTag`, `MusicBrainz`, `CoverArtArchive`, `UserOverride`).
    - `FieldProposalStatus` (enum: `Unchanged`, `Updated`, `NewValue`, `Conflict`).
    - `FieldProposal<T>` / `FieldProposal` (nome do campo, valor atual, valor proposto, status, proveniência).
    - `EnrichmentProposal` (conjunto de propostas para Título, Artista, Álbum, Artista do Álbum, Ano, Gênero, Faixa, Total de Faixas, Disco, Total de Discos, Gravadora, ISRC, CoverArtUrl).
  - Implementar `MetadataEnrichmentService` com regras de normalização de strings (comparação insensível a maiúsculas/minúsculas, corte de espaços em branco, tratamento de múltiplos gêneros).
  - Permitir cálculo de confiança global da proposta.
- **Reutilização obrigatória:** `TrackAudioTags`, `Song`, `TrackExternalIds`.
- **Teste obrigatório:** Testes unitários de regras de mesclagem cobrindo: faixa sem tags (todos `NewValue`), faixa já perfeita (todos `Unchanged`), divergência de grafia/ano (`Conflict`/`Updated`), preservação de campos locais não fornecidos pelo provedor (ex.: comentários pessoais mantidos).
- **Validação Local:** `dotnet build Resonance.slnx --configuration Release -p:Platform=x64` + execução dos testes de merge.

### Slice 3: UI de Proposta de Enriquecimento, Track Inspector & Regressão
- **META IMUTÁVEL repetida:**
  - *Problema:* Apresentar a proposta de enriquecimento de forma clara e legível no Track Inspector, permitindo ao usuário comparar lado a lado o dado atual e a sugestão remota com suas fontes, funcionando perfeitamente em modo offline.
  - *Definição de Sucesso:* O usuário visualiza o card "Enriquecimento Online" no Track Inspector; ao acionar a busca, uma tabela de comparação exibe cada campo com badges coloridos de proveniência e status; botões permitem recarregar ou encaminhar os dados para revisão futura; erros de rede são exibidos de maneira não-bloqueante.
- **Escopo ponta a ponta:**
  - Atualizar `ITrackInspectorViewModel` e `TrackInspectorViewModel` com comandos assíncronos:
    - `EnrichOnlineMetadataCommand` (dispara busca usando `MusicBrainzTrackId` existente ou busca textual por artista/título).
    - Propriedades reativas para `EnrichmentProposal`, `IsEnriching`, `EnrichmentError`, `HasEnrichmentProposal`.
  - Atualizar `TrackInspectorControl.xaml` com o card expansível "Enriquecimento de Metadados Online":
    - Indicador de progresso e status.
    - Grade/Lista comparativa elegante: Campo | Atual (Local) | Proposto (MusicBrainz) | Status Badge.
    - Exibição de preview da capa oficial sugerida (Cover Art Archive) lado a lado com a capa local.
    - Informação explícita de que a gravação em arquivo requer a confirmação na Feature 006.
  - Menu de contexto na biblioteca/álbum/playlist: atalho "Buscar Metadados Online".
- **Reutilização obrigatória:** `TrackInspectorControl.xaml`, `TrackInspectorViewModel.cs`, `SongListViewModelBase.cs`.
- **Teste obrigatório:** Testes de fluxo do ViewModel simulando: carregamento com sucesso, exibição de badges de proveniência, falha de rede/timeout com notificação suave e ausência de bloqueio na thread de playback.
- **Validação Final:** Compilação da solution completa sem avisos e 100% dos testes passando em modo Release x64.

---

## 4. GATES DE VALIDAÇÃO (.NET Toolchain)

```powershell
dotnet restore Resonance.slnx
dotnet build Resonance.slnx --configuration Release -p:Platform=x64
dotnet test Resonance.slnx --configuration Release --no-build
```

Nenhuma tarefa pode ser marcada como concluída se:
1. Dados de provedores online forem gravados diretamente nos arquivos de áudio em disco sem o fluxo de revisão e confirmação da Feature 006.
2. A aplicação realizar requisições ao MusicBrainz fora do rate limit de 1 req/s ou sem o cabeçalho User-Agent oficial.
3. A ausência de internet interromper ou degradar o playback de músicas locais.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Obtenção Canônica de Metadados via MusicBrainz (Priority: P1)

Como usuário com faixas que possuem identificadores MusicBrainz (adquiridos pelo AcoustID ou busca prévia), quero consultar os metadados canônicos do álbum e da faixa para padronizar numeração, ano de lançamento original, gravadora e arte de capa de alta resolução.

**Why this priority**: É o valor central da funcionalidade: transformar dados escassos em metadados ricos e oficiais a partir de uma base de conhecimento aberta e curada globalmente.

**Independent Test**: Invocar o enriquecimento para uma música com `MusicBrainzTrackId` válido; verificar se a API retorna com precisão título, artista, álbum, ano, número de faixa e link do Cover Art Archive, armazenando o payload no cache local.

**Acceptance Scenarios**:
1. **Given** uma faixa com `MusicBrainzTrackId` preenchido, **When** o usuário aciona "Buscar Metadados Online", **Then** os dados canônicos da gravação e do lançamento principal são recuperados e deserializados corretamente.
2. **Given** uma faixa sem identificador prévio mas com tags mínimas (Artista e Título), **When** o enriquecimento é solicitado, **Then** o sistema realiza busca textual estruturada no MusicBrainz e utiliza o melhor resultado com score confiável (≥ 80%).
3. **Given** uma resposta bem-sucedida do MusicBrainz, **When** processada, **Then** o resultado é armazenado em cache para que chamadas repetidas na mesma faixa ocorram em tempo sub-milissegundo sem tráfego de rede.

---

### User Story 2 - Comparação Campo a Campo e Preservação de Proveniência (Priority: P2)

Como colecionador cuidadoso, quero inspecionar exatamente o que cada campo sugere alterar, sabendo a origem de cada informação (se veio da tag local, do MusicBrainz ou do Cover Art Archive), para ter total transparência antes de qualquer decisão.

**Why this priority**: Garante conformidade com o Princípio VII da Constituição (Explicit Boundaries) e elimina o medo do usuário de ter suas músicas alteradas de forma arbitrária ou misteriosa.

**Independent Test**: Fornecer uma faixa contendo comentários pessoais e título com grafia diferente do MusicBrainz; executar o merge e validar que o objeto `EnrichmentProposal` classifica cada campo com status (`Conflict`, `Updated`, `NewValue`, `Unchanged`) e mantém a origem explícita.

**Acceptance Scenarios**:
1. **Given** um campo existente localmente com valor idêntico ao sugerido, **When** a proposta é gerada, **Then** o status é marcado como `Unchanged`.
2. **Given** um campo ausente localmente (ex.: Ano ou ISRC) que foi fornecido pelo MusicBrainz, **When** a proposta é gerada, **Then** o status é marcado como `NewValue` com proveniência `MusicBrainz`.
3. **Given** um campo local com valor diferente da base online (ex.: título com grafia alternativa), **When** a proposta é gerada, **Then** o status é marcado como `Conflict` ou `Updated`, exibindo ambos os valores lado a lado.
4. **Given** um campo local não suportado pelo provedor (ex.: Comentários do Usuário), **When** a proposta é gerada, **Then** o valor local é integralmente preservado.

---

### User Story 3 - Resiliência a Falhas de Rede e Respeito a Rate Limits (Priority: P3)

Como usuário que escuta música em viagens ou conexões móveis instáveis, quero que as tentativas de enriquecimento online não travem a aplicação, não interrompam o som e não insistam agressivamente caso o servidor esteja lento ou fora do ar.

**Why this priority**: Sustenta o Princípio IV (Local-First): a experiência primordial do player local é soberana e nunca pode ser refém de APIs remotas.

**Independent Test**: Simular indisponibilidade total de rede (ou erro HTTP 503/429); disparar enriquecimento e certificar que a interface exibe feedback claro e não-intrusivo, sem estalos no áudio e sem exceções não-tratadas.

**Acceptance Scenarios**:
1. **Given** ausência de conexão com a internet, **When** o usuário clica em "Buscar Metadados Online", **Then** o sistema apresenta status informativo ("Serviço temporariamente indisponível") sem congelar a interface.
2. **Given** disparo de requisições consecutivas para faixas diferentes, **When** enfileiradas, **Then** o limitador de vazão garante o espaçamento mínimo de 1 segundo entre chamadas à API do MusicBrainz.
3. **Given** resposta de erro 429 (Too Many Requests) ou circuito aberto do provedor, **When** detectada, **Then** o pipeline ativa recuo exponencial sem gerar enxame de requisições.

---

### Edge Cases

- **Múltiplos Lançamentos para a Mesma Gravação**: Resolução determinística priorizando álbum oficial de estúdio com data mais antiga, ou correspondência exata de título se o álbum local já estiver preenchido.
- **Lançamentos Multi-Disco (Box Sets)**: Tratamento correto de numeração de disco (ex.: Disco 2 de 3, Faixa 4) mapeada a partir da mídia do lançamento no MusicBrainz.
- **Gravações com Múltiplos Artistas (Feat. / Colaborações)**: Desserialização limpa de artist credits complexos (ex.: "Artist A feat. Artist B").
- **Caracteres Unicode e Acentuação**: Preservação estrita de caracteres não-latinos (japonês, cirílico, acentos em português) sem corrupção de encoding UTF-8.
- **Capas Inexistentes no Cover Art Archive**: Caso o lançamento não possua imagem frontal no arquivo público, a proposta deve indicar ausência de capa remota mantendo a imagem local inalterada sem disparar erros.

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: O sistema DEVE consultar a API do MusicBrainz Web Service v2 utilizando o identificador `MusicBrainzTrackId` (Recording ID) associado à faixa ou, quando ausente, via busca estruturada por Artista e Título.
- **FR-002**: O sistema DEVE aplicar cabeçalho `User-Agent` canônico e respeitar rigorosamente a taxa limite de 1 requisição por segundo no domínio `musicbrainz.org`.
- **FR-003**: O sistema DEVE buscar a arte de capa frontal oficial no Cover Art Archive (`https://coverartarchive.org/release/{mbid}/front-500`) de forma não-bloqueante.
- **FR-004**: O sistema DEVE persistir as respostas das consultas em cache local (com expiração padrão de 7 dias) para evitar requisições redundantes à infraestrutura pública.
- **FR-005**: O sistema DEVE comparar campo a campo as informações locais com as sugestões online e gerar um modelo estruturado de proposta de enriquecimento (`EnrichmentProposal`).
- **FR-006**: O sistema DEVE associar a cada campo sugerido seu status de divergência (`Unchanged`, `Updated`, `NewValue`, `Conflict`) e sua proveniência de origem (`LocalTag`, `MusicBrainz`, `CoverArtArchive`).
- **FR-007**: O sistema DEVE exibir a proposta de enriquecimento no Track Inspector com visualização tabular clara, badges informativos e pré-visualização da capa.
- **FR-008**: O sistema NÃO DEVE gravar nenhuma tag ou arquivo fisicamente no disco durante esta feature, mantendo a responsabilidade de escrita confinada à Feature 006.
- **FR-009**: O sistema DEVE aplicar heurística canônica de seleção de lançamento para gravações com múltiplos lançamentos no MusicBrainz: priorizar status "Official" e tipo "Album" com a data de lançamento mais antiga, ou correspondência de título se o álbum local já estiver preenchido, evitando coletâneas/álbuns ao vivo a menos que explicitamente correspondentes.
- **FR-010**: O sistema DEVE resolver a URL de arte de capa do Cover Art Archive utilizando a resolução padrão "front-500" (500x500 pixels) como equilíbrio entre fidelidade visual e uso de banda, aplicando fallback para "front-250" se a imagem de 500px não estiver disponível.
- **FR-011**: O sistema DEVE persistir o cache de metadados em arquivos JSON indexados por hash no diretório de cache da aplicação (`IAppInfoService.CachePath/metadata/`), preservando o banco de dados SQLite leve e sem necessidade de migrações estruturais adicionais.
- **FR-012**: O sistema DEVE fornecer seleção individual via checkbox para cada campo sugerido na tabela do Track Inspector (com valor padrão marcado para novos/atualizados e desmarcado para conflitos críticos), permitindo ao usuário ajustar a proposta em memória antes de encaminhar à Feature 006.
- **FR-013**: O sistema DEVE aplicar fusão com deduplicação no campo de Gênero: preservar o valor local preenchido e propor complementação com as tags mais relevantes do MusicBrainz separadas por ponto-e-vírgula (';'), classificando o status como "Updated" quando novos termos forem sugeridos.

### Key Entities

- **EnrichmentProposal**: Modelo agregado contendo a lista de `FieldProposal` para todos os campos musicais analisados, além da referência da faixa e carimbo de data/hora.
- **FieldProposal**: Representação de um campo individual contendo Nome, Valor Atual, Valor Sugerido, Status, Proveniência e flag reativa `IsSelected`.
- **MetadataProvenance**: Enumeração identificando a origem do metadado (`LocalTag`, `MusicBrainz`, `CoverArtArchive`, `UserOverride`).
- **FieldProposalStatus**: Enumeração identificando o resultado da comparação (`Unchanged`, `Updated`, `NewValue`, `Conflict`).
- **MusicBrainzRecordingDetail**: Modelo com os atributos consolidados extraídos do XML/JSON da API do MusicBrainz.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% das requisições disparadas ao MusicBrainz cumprem a política de taxa limite (máximo 1 req/s) e cabeçalho de identificação.
- **SC-002**: Respostas de consultas já presentes em cache são resolvidas e exibidas na interface em menos de 15 milissegundos.
- **SC-003**: 100% dos campos gerados na proposta de enriquecimento expõem explicitamente sua fonte de proveniência (`MetadataProvenance`).
- **SC-004**: Zero modificações em arquivos de áudio físicos em disco (0 bytes alterados em arquivos de mídia).

---

## Assumptions

- O usuário possui permissão de rede para acessar `musicbrainz.org` e `coverartarchive.org` (ou opera em modo offline gracioso).
- A identificação acústica prévia da Feature 004 ou tags mínimas existentes fornecem subsídio suficiente para localização da gravação no MusicBrainz.
- A aplicação da proposta e a modificação de arquivos de áudio físicos em disco serão efetuadas exclusivamente através do fluxo de confirmação e diff da Feature 006.
