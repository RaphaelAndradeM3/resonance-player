# FEATURE SPEC: 004 — Audio Fingerprint & Music Recognition

**Feature Branch**: `004-audio-fingerprint-music-recognition`  
**Created**: 2026-09-21  
**Status**: Draft  
**Input**: User description: "FEATURE 004 — Audio Fingerprint & Music Recognition"

---

## 1. META IMUTÁVEL (Global Goal)

> **Problema de Negócio:** Arquivos de áudio na biblioteca do usuário frequentemente estão sem tags, com nomes genéricos (ex.: `track_01.mp3`, `audio_gravacao.wav`) ou com metadados completamente corrompidos/incorretos. O usuário precisa de uma forma confiável de identificar a gravação pelo seu conteúdo sonoro real, e não por suposições de texto.
>
> **Definição de Sucesso:** A aplicação extrai o fingerprint acústico (Chromaprint) de forma 100% local, consulta o serviço AcoustID quando habilitado na configuração, recebe os candidatos correspondentes associados a identificadores do MusicBrainz com índices de confiança explícitos, e permite ao usuário inspecionar, selecionar ou descartar os resultados sem jamais transmitir o arquivo de áudio bruto para a rede.
>
> **Regra de Ouro:** O resultado do reconhecimento acústico é estritamente um conjunto de candidatos/sugestões e NUNCA uma alteração automática ou silenciosa das tags físicas do arquivo em disco.

---

## Clarifications

### Session 2026-09-21

- Q: Como a chave de API do AcoustID deve ser gerenciada pelo sistema? (FR-007) → A: Chave de aplicação oficial do Resonance embutida por padrão para funcionamento imediato ("out of the box"), com campo em Configurações > Provedores permitindo que o usuário insira sua própria API Key pessoal do AcoustID se desejar.
- Q: Onde e como o usuário interage visualmente com o processo de identificação acústica e seus resultados? (FR-008) → A: Integrado diretamente no painel retrátil do Track Inspector (com card "Fingerprint & Reconhecimento Acústico", botão de ação, indicador de progresso e lista de candidatos) e acionável também pelo menu de contexto da biblioteca ("Identificar Música via Áudio").
- Q: Qual deve ser o efeito imediato quando o usuário seleciona um candidato correspondente retornado pelo AcoustID? (FR-009) → A: Associar os identificadores externos (`MusicBrainz Recording ID`, `AcoustID`) à faixa e exibir os metadados sugeridos no Track Inspector para visualização e comparação imediata, sem modificação física em disco (gravação de tags em arquivo reservada à Feature 006).
- Q: Qual deve ser o piso mínimo de pontuação de confiança e a quantidade máxima de candidatos apresentados no resultado do reconhecimento acústico? (FR-005) → A: Exibir até 5 candidatos ordenados decrescentemente por relevância com corte mínimo de 40% de confiança, aplicando destaque visual aos resultados de alta precisão (≥ 80%).
- Q: O hash Chromaprint gerado localmente deve ser persistido no banco de dados da biblioteca para reaproveitamento futuro? (FR-001) → A: Persistir o hash Chromaprint no banco de dados local da biblioteca (campo `Song.AcousticFingerprint`), permitindo reutilização instantânea em futuras consultas, re-inspeções e detecção de duplicatas sem re-decodificar o arquivo de áudio.
- Q: Como o sistema deve se comportar quando o usuário solicitar o reconhecimento de uma faixa que já possui identificadores (AcoustId ou MusicBrainzTrackId) previamente associados? (FR-006) → A: Exibir no Track Inspector o status "Faixa Já Identificada" com os identificadores atuais e fornecer botão explícito "Re-identificar via Áudio" para permitir nova consulta sob demanda, sem sobrescrever nada automaticamente.

---

## 2. CONTRATOS & LIMITES DA ARQUITETURA

* **Projetos Afetados na Solution (`Resonance.slnx`):**
  - `Resonance.Core`: Abstrações e implementações de cálculo de Chromaprint, cliente HTTP resiliente para AcoustID com pipeline de resiliência/rate-limit, modelos de fingerprint e candidatos de reconhecimento, e enriquecimento de identificadores externos em `TrackExternalIds`.
  - `Resonance.WinUI`: Componentes de apresentação visual integrados ao Track Inspector e menus de contexto da biblioteca, exibindo barra de progresso, estado da consulta, lista ordenada de candidatos com score de confiança e ações de vinculação/descarte.
* **Tipos/Serviços Existentes que DEVEM ser reutilizados:**
  - `FFmpeg`: Utilização do binário FFmpeg integrado ao player com muxer nativo `chromaprint` (`ffmpeg -v error -nostdin -i <file> -t 120 -f chromaprint -fp_format base64 pipe:1`) para decodificar e gerar o fingerprint Base64 em processo local ultrarrápido sem dependências de DLLs nativas extras.
  - `IProviderPipelineProvider`: Infraestrutura central de HTTP para aplicar rate limiting, retry com backoff exponencial e circuit breaker para chamadas ao provedor AcoustID.
  - `TrackExternalIds`: Modelo de dados de identificadores externos criado na Feature 003 (`AcoustId`, `MusicBrainzTrackId`, `MusicBrainzReleaseId`, `MusicBrainzArtistId`).
  - `TrackInspectorViewModel` / `TrackInspectorPanel`: Painel retrátil lateral da Feature 003 para acomodar a visualização do fingerprint local e dos candidatos de reconhecimento.
  - `ISettingsService` / `ServiceProviderSetting`: Gestão de ativação do serviço e armazenamento seguro de credenciais/chaves de API.
* **Convenções Obrigatórias:**
  - O cálculo da impressão digital Chromaprint deve ser realizado **100% localmente no dispositivo**.
  - O arquivo de áudio físico ou seus bytes de amostras PCM **JAMAIS** devem ser transmitidos via rede; apenas a string do fingerprint calculada e a duração em segundos são enviadas ao AcoustID.
  - O provedor AcoustID deve ser opcional, podendo ser ativado/desativado pelo usuário a qualquer momento nas configurações do aplicativo.
  - Respeitar rigorosamente os limites de taxa da API pública do AcoustID (máximo de 3 requisições por segundo por cliente) utilizando token bucket ou pipeline rate limiter.
  - Toda sugestão deve expor de forma clara a pontuação de confiança (confidence score percentual) e o `MusicBrainz Recording ID`.
  - Nenhuma alteração pode ser gravada de forma destrutiva no arquivo físico sem aprovação explícita (escopo da Feature 006 com workflow de Review, Diff e Confirmação).

---

## 3. FATIAS VERTICAIS DE IMPLEMENTAÇÃO (Max 3 Slices)

### Slice 1: Fingerprint Local (Chromaprint Engine & Pipeline FFmpeg)
- **META IMUTÁVEL repetida:**
  - *Problema:* Extrair uma impressão digital acústica confiável e determinística a partir de qualquer arquivo de áudio suportado sem enviar nenhum dado sonoro pela rede.
  - *Definição de Sucesso:* A aplicação decodifica os primeiros ~120s via FFmpeg local com muxer chromaprint, gera a string Base64 compatível com AcoustID, calcula a duração exata em segundos e persiste o valor no SQLite local (`Song.AcousticFingerprint`) sem enviar nenhum dado sonoro para a rede.
- **Escopo ponta a ponta:** Integrar `FFmpegFingerprintService` executando o muxer chromaprint do FFmpeg; criar `IFingerprintService`; estender entidade `Song` e migration EF Core para `AcousticFingerprint` e `AcoustId`; reaproveitar hash se já existente em banco.
- **Reutilização obrigatória:** Infraestrutura e binários FFmpeg existentes em `Resonance.WinUI`/`Resonance.Core`.
- **Teste obrigatório:** Testes unitários com faixas e amostras sonoras sintéticas gerando fingerprints consistentes e determinísticos.
- **Validação Local:** `dotnet build Resonance.slnx` + execução da suíte de testes de fingerprint.

### Slice 2: AcoustID Provider, Resiliência e Rate Limiting
- **META IMUTÁVEL repetida:**
  - *Problema:* Consultar o serviço AcoustID de forma segura, respeitando termos de uso, rate limits e políticas de privacidade sem falhar sob instabilidade de rede.
  - *Definição de Sucesso:* O serviço cliente do AcoustID consulta a API com User-Agent apropriado, rate limiting rigoroso (3 req/s), cache local em memória/disco, mapeando respostas para candidatos estruturados com pontuação de confiança e MusicBrainz IDs.
- **Escopo ponta a ponta:** Implementar `IAcoustIdService` com `IProviderPipelineProvider`, DTOs de resposta do AcoustID, cálculo de score de confiança (0-100%) e integração com `ServiceProviderIds.AcoustId`.
- **Reutilização obrigatória:** `IProviderPipelineProvider` e `HttpClientFactory`.
- **Teste obrigatório:** Testes de integração com mocks HTTP simulando: sucesso com múltiplos lançamentos, resposta vazia (gravação desconhecida), erro 429 (rate limit com backoff) e timeout de rede.
- **Validação Local:** `dotnet build Resonance.slnx` + testes da camada de rede e resiliência.

### Slice 3: UI, Seleção de Candidatos e Vinculação no Track Inspector
- **META IMUTÁVEL repetida:**
  - *Problema:* Permitir ao usuário acionar a identificação acústica para qualquer faixa, acompanhar o progresso em tempo real, examinar os candidatos sugeridos e vincular os metadados desejados de forma transparente.
  - *Definição de Sucesso:* A UI do Track Inspector e menus de contexto disponibilizam ação para "Identificar Música via Áudio", exibem estado de carregamento não-bloqueante, listam os candidatos classificados por confiança e permitem vincular os IDs à faixa ou descartar.
- **Escopo ponta a ponta:** Expandir `TrackInspectorViewModel` e painel XAML com seção de Fingerprint & Reconhecimento; vincular comandos assíncronos e feedback visual responsivo.
- **Reutilização obrigatória:** `TrackInspectorPanel` e `TrackInspectorViewModel` da Feature 003.
- **Teste obrigatório:** Teste de ponta a ponta de acionamento do fluxo, exibição dos candidatos e persistência dos identificadores em `TrackExternalIds`.
- **Validação Final:** Compilação completa da solution `Resonance.slnx` e passagem de todos os testes unitários.

---

## 4. GATES DE VALIDAÇÃO (.NET Toolchain)

```powershell
dotnet restore Resonance.slnx
dotnet build Resonance.slnx --configuration Release
dotnet test Resonance.slnx --configuration Release --no-build
```

Nenhuma tarefa pode ser considerada concluída se dados de áudio brutos forem transmitidos via rede ou se resultados de API modificarem automaticamente arquivos em disco.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Reconhecimento Acústico de Músicas Sem Tags (Priority: P1)

Como usuário com faixas em formato desconhecido ou arquivos nomeados genericamente como "track_01.mp3" sem artista ou título nas tags, quero clicar em "Identificar Música via Áudio" e descobrir em poucos segundos o nome da música, artista e álbum através do som da gravação.

**Why this priority**: É a proposta de valor essencial da Feature 004: resgatar a identidade de gravações locais sem demandar esforço manual de digitação ou pesquisa externa.

**Independent Test**: Executar o fluxo de reconhecimento para um arquivo de áudio sem tags locais; validar que o sistema extrai o fingerprint Chromaprint localmente, consulta o AcoustID e exibe os candidatos correspondentes com taxa de confiança superior a 80%.

**Acceptance Scenarios**:

1. **Given** um arquivo de áudio sem metadados locais na biblioteca, **When** o usuário solicita o reconhecimento acústico, **Then** o sistema decodifica os primeiros segundos do arquivo localmente, calcula o Chromaprint e realiza a consulta à API AcoustID.
2. **Given** uma resposta bem-sucedida do provedor AcoustID, **When** os resultados são carregados, **Then** a interface apresenta a lista de candidatos contendo Título, Artista, Álbum, ano, MusicBrainz Recording ID e percentual de confiança.
3. **Given** um arquivo cujo áudio não possui correspondência cadastrada na base do AcoustID, **When** a busca termina, **Then** o sistema exibe mensagem amigável informando que nenhuma correspondência acústica foi encontrada, sem gerar exceções não tratadas.

---

### User Story 2 - Respeito à Privacidade e Garantia Local-First (Priority: P2)

Como usuário preocupado com privacidade de dados e consumo de banda de internet, quero ter garantia absoluta de que meus arquivos de áudio jamais são transmitidos para a nuvem e ter o controle de desativar consultas externas quando desejar.

**Why this priority**: Cumpre os princípios inegociáveis da Constituição do Resonance (Princípio IV: Local-First e Privacy-First), mantendo transparência com o usuário.

**Independent Test**: Inspecionar as requisições de rede emitidas durante o processo de identificação acústica para atestar que apenas a string de hash do Chromaprint e a duração em segundos são trafegadas.

**Acceptance Scenarios**:

1. **Given** uma requisição de identificação acústica disparada, **When** os dados transmitidos são inspecionados, **Then** nenhum byte de amostra PCM ou payload de áudio do arquivo é transmitido para fora da máquina local.
2. **Given** o player configurado com "Consultas Online" desativadas nas configurações ou computador sem conexão à internet, **When** o usuário tenta identificar uma faixa, **Then** o sistema calcula o fingerprint localmente, exibe o hash e duração no Track Inspector, e indica de forma clara que a consulta externa está desabilitada/indisponível.

---

### User Story 3 - Escolha Consciente Entre Múltiplos Candidatos (Priority: P3)

Como colecionador de música organizando álbuns que possuem múltiplas edições (versões de estúdio, remasterizações, edições de luxo ou versões ao vivo), quero visualizar todos os candidatos encontrados ordenados por relevância e poder escolher explicitamente a gravação correta.

**Why this priority**: Evita falsos positivos e garante que o usuário mantenha o controle editorial final sobre sua coleção de músicas.

**Independent Test**: Submeter ao reconhecimento uma faixa amplamente lançada em múltiplos álbuns; verificar se a interface lista os candidatos agrupados ou ordenados por confiança, permitindo selecionar individualmente um deles ou rejeitar todos.

**Acceptance Scenarios**:

1. **Given** múltiplos lançamentos associados ao mesmo fingerprint acústico na base do AcoustID, **When** o resultado for exibido, **Then** a interface lista cada candidato com seus respectivos detalhes (Título, Artista, Álbum, MusicBrainz ID e % de Confiança).
2. **Given** a lista de candidatos apresentada, **When** o usuário escolhe a opção "Vincular Este Candidato", **Then** os identificadores externos (`MusicBrainzTrackId`, `AcoustId`) são atribuídos à faixa e os metadados sugeridos ficam visíveis para auditoria no Track Inspector.
3. **Given** a lista de candidatos apresentada, **When** o usuário opta por "Descartar", **Then** a lista de sugestões é limpa sem afetar os dados existentes da faixa.

---

### User Story 4 - Integração Fluida no Track Inspector e Menus da Biblioteca (Priority: P4)

Como usuário ouvindo música ou navegando pela lista de faixas, quero acessar o recurso de identificação acústica diretamente pelo Track Inspector (painel lateral retrátil) ou através do clique com botão direito em qualquer faixa na biblioteca.

**Why this priority**: Oferece coerência de interface, complementando a experiência introduzida na Feature 003 sem necessidade de abrir janelas modais separadas.

**Independent Test**: Abrir o Track Inspector para uma faixa em reprodução e clicar no botão "Identificar Música via Áudio"; verificar exibição do progresso de análise sonora e atualização suave da interface.

**Acceptance Scenarios**:

1. **Given** o Track Inspector aberto para qualquer faixa selecionada, **When** o usuário clica em "Identificar Música via Áudio", **Then** uma animação de progresso indica a extração local do áudio seguida da busca no AcoustID sem bloquear a rolagem ou reprodução do player.
2. **Given** qualquer faixa na lista de músicas da biblioteca, **When** o usuário clica com o botão direito e escolhe "Identificar Música via Áudio", **Then** o Track Inspector é aberto já iniciando o processo de reconhecimento da faixa selecionada.

---

### Edge Cases

- **Gravações ultracurtas (menos de 10 segundos)**: Músicas ou vinhetas muito curtas podem gerar fingerprints com menos entropia. O sistema deve analisar a duração mínima e retornar status `AudioTooShort`, informando amigavelmente que o áudio é curto demais para identificação confiável.
- **Arquivos com áudio em silêncio ou ruído contínuo**: Faixas que contêm apenas silêncio ou ruído estático produzem fingerprints degenerados; o sistema deve detectar e retornar `AnalysisFailed` informando que a faixa não possui sinal musical suficiente.
- **Servidor do AcoustID retornando HTTP 429 (Too Many Requests)**: O pipeline de resiliência deve aguardar com backoff exponencial antes de tentar novamente, sem travar a interface e sem estourar o limite de conexões.
- **Queda de conexão no meio da requisição**: Tratar timeouts e falhas de DNS de forma silenciosa e informativa, permitindo ao usuário tentar novamente com um clique no botão "Repetir".
- **Áudio decodificado em taxa diferente de 44.1kHz/48kHz**: O muxer nativo do FFmpeg normaliza a reamostragem internamente de maneira transparente para o algoritmo Chromaprint.

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: O sistema DEVE decodificar localmente os primeiros 120 segundos de qualquer arquivo de áudio suportado via muxer nativo `chromaprint` do FFmpeg, calcular o fingerprint acústico (Chromaprint Base64) e duração em segundos correspondentes, consultar e reutilizar o hash previamente gravado no banco de dados local (`Song.AcousticFingerprint`) para evitar reprocessamento sonoro desnecessário, e persistir imediatamente o hash gerado no SQLite da biblioteca.
- **FR-002**: O sistema NUNCA DEVE enviar dados sonoros brutos, amostras PCM ou arquivos de áudio completos para qualquer servidor externo via rede.
- **FR-003**: O sistema DEVE consultar a API pública do AcoustID enviando apenas a string do fingerprint Chromaprint, a duração em segundos e a chave de aplicação (API Key), recuperando os metadados de gravações e lançamentos associados.
- **FR-004**: O sistema DEVE aplicar rate limiting estrito nas requisições ao AcoustID (máximo de 3 requisições por segundo) através do `IProviderPipelineProvider`.
- **FR-005**: O sistema DEVE calcular e exibir um índice de confiança percentual (0% a 100%) para cada candidato retornado pelo AcoustID, filtrando e apresentando até 5 candidatos com pontuação mínima de 40%, ordenados decrescentemente e com destaque visual para correspondências de alta precisão (≥ 80%).
- **FR-006**: O sistema DEVE extrair e estruturar os identificadores externos correspondentes (`AcoustId`, `MusicBrainzTrackId`, `MusicBrainzReleaseId`, `MusicBrainzArtistId`) para cada candidato retornado. Caso a faixa já possua identificadores previamente associados, o sistema DEVE exibir o status "Já Identificada" e disponibilizar ação explícita de "Re-identificar via Áudio" sob demanda sem disparar consultas automáticas desnecessárias.
- **FR-007**: O sistema DEVE fornecer uma chave de aplicação do Resonance embutida por padrão para consultas ao AcoustID, disponibilizando adicionalmente um campo em Configurações > Provedores para o usuário configurar sua própria chave de API pessoal.
- **FR-008**: O sistema DEVE disponibilizar o fluxo de identificação acústica integrado diretamente no painel retrátil do Track Inspector (com card dedicado, botão de ação, indicador de progresso e lista ordenada de candidatos) e através do menu de contexto de faixas na biblioteca.
- **FR-009**: Ao selecionar um candidato correspondente, o sistema DEVE associar os identificadores externos (`MusicBrainz Recording ID`, `AcoustID`) à faixa e preencher os metadados sugeridos no Track Inspector para conferência e comparação visual, sem realizar alterações diretas ou destrutivas no arquivo físico em disco.
- **FR-010**: O sistema NÃO DEVE realizar modificações físicas silenciosas ou gravações automáticas em arquivos de áudio em disco (escopo exclusivo da Feature 006 com workflow de Review, Diff e Confirmação).
- **FR-011**: O sistema DEVE permitir ao usuário ativar ou desativar o provedor AcoustID a qualquer momento nas Configurações da aplicação.
- **FR-012**: O sistema DEVE armazenar em cache em memória durante a sessão ativa os resultados de consultas do AcoustID, indexados por `(Hash, DurationSeconds)`, para evitar requisições idênticas repetidas na rede.

### Key Entities

- **AcousticFingerprint**: Hash acústico compactado gerado pelo algoritmo Chromaprint e duração correspondente em segundos da gravação, persistido no campo `Song.AcousticFingerprint` no banco de dados SQLite da biblioteca.
- **RecognitionCandidate**: Candidato sugerido pelo AcoustID contendo título da gravação, nome do artista, álbum/lançamento, ano, `MusicBrainzRecordingId`, `AcoustId` e pontuação de confiança (0-100%).
- **RecognitionResult**: Resultado completo da consulta acústica contendo status da operação (sucesso, não encontrado, sem conexão, erro de rate limit), lista ordenada de candidatos e metadados de proveniência.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A extração local do Chromaprint e duração é concluída em menos de 1,5 segundo para qualquer arquivo de áudio de até 10 minutos em hardware padrão.
- **SC-002**: 100% de garantia de privacidade constatada em auditoria de rede (zero bytes de áudio bruto transmitidos externamente).
- **SC-003**: 0% de ocorrência de bloqueios permanentes por HTTP 429 na API do AcoustID devido à atuação preventiva do rate limiter configurado a 3 req/s com backoff exponencial.
- **SC-004**: O usuário consegue acionar o reconhecimento e visualizar a lista de candidatos com no máximo 2 cliques a partir do Track Inspector ou menu de contexto.
- **SC-005**: 100% dos candidatos exibem de forma inequívoca o título, artista e percentual de confiança antes de qualquer ação de vinculação.

---

## Assumptions

- O Resonance utilizará um algoritmo/biblioteca Chromaprint nativo ou port gerenciado de alta eficiência compatível com o ecossistema .NET 10.
- As consultas ao AcoustID dependem de conexão com a internet; quando offline, a aplicação informa a impossibilidade de consulta sem travar.
- A gravação física definitiva de tags em disco (ID3v2, Vorbis Comments, MP4 tags) faz parte da Feature 006 (Metadata Review & Tag Editor); a Feature 004 foca no reconhecimento acústico, obtenção de candidatos e vinculação de IDs e metadados de sessão.
- Gravações musicais comerciais cadastradas na base de dados colaborativa do MusicBrainz/AcoustID fornecerão candidatos de alta confiança.
