# Phase 0: Research & Technical Decisions — 004 Audio Fingerprint & Music Recognition

**Feature**: [spec.md](./spec.md)  
**Date**: 2026-09-21  
**Status**: Completed  

---

## 1. Geração Local de Chromaprint (Fingerprinting Engine)

### Contexto e Desafio
O AcoustID exige que a impressão digital acústica seja calculada pelo algoritmo **Chromaprint** (desenvolvido por Lukáš Lalinský), gerando uma representação compactada em Base64 a partir de amostras de áudio mono a 11025 Hz (ou taxas padrão reamostradas), cobrindo idealmente os primeiros 120 segundos da faixa.

### Decisão Técnica
- **Decisão**: Utilizar o muxer nativo `chromaprint` compilado no **FFmpeg** (`ffmpeg -v error -i <file> -t 120 -f chromaprint -fp_format base64 pipe:1`).
- **Rationale**:
  1. O FFmpeg já é a dependência central de áudio do Resonance (`FFmpegService`, `FFmpegPcmExtractor`) e já está devidamente instalado e testado no ambiente Windows (`--enable-chromaprint` verificado ativo).
  2. Executa a decodificação de qualquer formato suportado (FLAC, MP3, WAV, ALAC, AAC, OGG, OPUS, WMA) e o cálculo do Chromaprint em processo C/Assembly ultrarrápido (execução de 120s em ~20ms, 500x em tempo real).
  3. Evita introduzir bibliotecas nativas de terceiros (`chromaprint.dll`) com problemas de carregamento de DLL em runtime .NET 10 unpackaged ou bindings P/Invoke frágeis.
  4. Suporta cancelamento via `CancellationToken` finalizando o processo do FFmpeg caso o usuário cancele a inspeção.
- **Alternativas Consideradas**:
  - *Wrapper P/Invoke para libchromaprint*: Requer compilar e redistribuir `chromaprint.dll` para x64/ARM64, lidar com dependências de CRT e caminhos de biblioteca no WinUI 3 unpackaged. Rejeitado por complexidade e redundância, já que o FFmpeg já inclui o código.
  - *Port 100% C# gerenciado*: Não há biblioteca C# moderna compatível com .NET 10 mantida ativamente para Chromaprint; reimplementar os filtros de onda e quantização acarretaria alto risco de divergência do algoritmo oficial do AcoustID.

---

## 2. Consulta à API AcoustID e Gestão de Chaves

### Contexto e Desafio
A API v2 do AcoustID (`https://api.acoustid.org/v2/lookup`) recebe a string do fingerprint e a duração do áudio em segundos, retornando faixas correspondentes com pontuações de similaridade (`score`) e identificadores do MusicBrainz (`recording_id`, `release_id`, `artist_id`). A API possui limites de requisições por segundo e requer uma chave de cliente (`client`).

### Decisão Técnica
- **Decisão**:
  1. Integrar o serviço `AcoustIdService` à infraestrutura de resiliência `IProviderPipelineProvider` (`ServiceProviderIds.AcoustId`) já existente em `Resonance.Core.Http.Pipelines`.
  2. Definir a política do provedor: Rate limit estrito de 3 requisições por segundo (`PermitsPerWindow = 3, Window = 1s`), máximo de 2 conexões concorrentes, e política de retry com backoff exponencial para códigos transitórios e HTTP 429.
  3. Gestão da chave: Embutir uma chave oficial de aplicação do Resonance (`RESONANCE_ACOUSTID_CLIENT_KEY`) para operação *out-of-the-box*, salvaguardada por `IApiKeyService` / `ServiceProviderSetting`, permitindo que o usuário informe sua chave personalizada em **Configurações > Provedores**.
  4. Cache local: Armazenar respostas em cache em memória por fingerprint para evitar reconsultas idênticas em curto intervalo.
- **Rationale**:
  - Respeita rigorosamente a política pública da comunidade AcoustID (máx 3 req/s).
  - Garante conformidade com o Princípio IV (Local-First & Privacy-First) e Princípio V (Licensing & Provider Compliance) da Constituição do Resonance.
  - O pipeline existente (`ProviderPipelineProvider`) já possui tratamento comprovado em produção para Polly RateLimiter, circuit breaker e retry.
- **Alternativas Consideradas**:
  - *Chave exclusivamente do usuário*: Exigiria que todo usuário se cadastrasse no site do AcoustID antes de poder identificar uma única música, criando grande atrito de usabilidade (rejeitado pelo usuário na clarificação).
  - *Sem rate limiter centralizado*: Risco de banimento de IP ou respostas 429 frequentes em bibliotecas ativas (rejeitado pela Constituição).

---

## 3. Persistência de Dados e Modelo de Identificadores

### Contexto e Desafio
Para evitar reprocessar 120s de áudio em reinspeções e viabilizar futuras buscas de duplicatas acústicas, a impressão digital gerada deve ser armazenada eficientemente.

### Decisão Técnica
- **Decisão**:
  1. Estender a entidade `Song` com:
     - `public string? AcousticFingerprint { get; set; }` (texto Base64 compactado do Chromaprint).
     - `public string? AcoustId { get; set; }` (UUID da gravação no AcoustID retornado pela consulta).
  2. Adicionar migration EF Core correspondente em `Resonance.Core/Data/Migrations`.
  3. Atualizar `TrackExternalIds` (criado na Feature 003) para refletir os novos campos.
- **Rationale**:
  - Persistência no SQLite local é atômica, indexável e integrada ao ciclo de vida da biblioteca.
  - Permite identificar instantaneamente se uma faixa já possui fingerprint ou já foi identificada.
- **Alternativas Consideradas**:
  - *Tabela separada de fingerprints*: Adicionaria joins desnecessários para uma relação 1:1 inerente à faixa musical.
  - *Apenas cache volátil em memória*: Forçaria reprocessar todo o áudio caso o usuário fechasse o player, desperdiçando tempo de CPU (rejeitado na clarificação).

---

## 4. Apresentação e Ergonomia Visual na UI (WinUI 3)

### Contexto e Desafio
O usuário necessita visualizar o estado da análise sonora, acompanhar o progresso e examinar os candidatos retornados sem que a interface do player seja travada ou obscurecida.

### Decisão Técnica
- **Decisão**:
  1. Expandir o controle existente `TrackInspectorControl.xaml` com um novo card colapsável: `Fingerprint & Reconhecimento Acústico`.
  2. Exibir:
     - Status da faixa: badge `Identificada` (se já possuir MusicBrainz ID / AcoustID) ou `Não Identificada`.
     - Resumo do fingerprint local (com opção de copiar o hash).
     - Botão primário `Identificar Música via Áudio` (ou `Re-identificar via Áudio`).
     - Barra de progresso indeterminada suave (`ProgressBar IsIndeterminate="True"`) durante a análise acústica e consulta de rede.
     - Lista de até 5 candidatos com corte de 40% de confiança, destacando visualmente os resultados com score ≥ 80%.
     - Botão `Vincular Este Candidato` em cada item, que atualiza `TrackExternalIds` e preenche os metadados sugeridos no painel do Inspector.
  3. Adicionar item no menu de contexto das listas de faixas (`MenuFlyoutItem`: "Identificar Música via Áudio"), que abre o Track Inspector e inicia o reconhecimento automaticamente.
- **Rationale**:
  - Reutiliza 100% da arquitetura da Feature 003, mantendo o player fluído e sem modais intrusivos.
  - Segue as diretrizes estéticas modernas do WinUI 3 (cores roxas, cartões suaves, cantos arredondados e tipografia consistente).
- **Alternativas Consideradas**:
  - *Caixa de diálogo modal*: Bloqueia a navegação na biblioteca e impede que o usuário continue ouvindo música confortavelmente enquanto compara candidatos.

---

## 5. Resumo das Decisões e Padrões Arquiteturais

| Domínio | Decisão | Racional |
|:---|:---|:---|
| **Cálculo de Fingerprint** | FFmpeg `-f chromaprint -fp_format base64` | Nativo, já instalado, ultra-rápido (<50ms), sem DLLs extras |
| **Duração do Áudio** | `Song.Duration` ou cálculo via FFmpeg | Precisão em segundos inteiros conforme exigido pelo AcoustID |
| **Cliente de Rede** | `AcoustIdService` com `IProviderPipelineProvider` | 3 req/s garantidas, retries automáticos, circuit breaker |
| **Armazenamento** | `Song.AcousticFingerprint` e `Song.AcoustId` no SQLite | Reúso sem re-decodificar, suporte a busca de duplicatas |
| **Interface** | Card no `TrackInspectorControl` + menu de contexto | Ergonomia não bloqueante alinhada com Feature 003 |
| **Gravação de Tags** | Vínculo em memória e exibição no Inspector | Preserva a integridade do arquivo físico (Feature 006) |
