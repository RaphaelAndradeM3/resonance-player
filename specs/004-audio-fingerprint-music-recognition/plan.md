# Implementation Plan: 004 — Audio Fingerprint & Music Recognition

**Branch**: `004-audio-fingerprint-music-recognition` | **Date**: 2026-09-21 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/004-audio-fingerprint-music-recognition/spec.md`

---

## Summary

A **Feature 004** fornece ao Resonance Player a capacidade de identificar gravações musicais pelo seu som real, gerando a impressão digital acústica (Chromaprint) de forma 100% local e consultando a base comunitária do AcoustID de forma segura e resiliente.

A abordagem técnica consiste em:
1. Utilizar o muxer nativo `chromaprint` do **FFmpeg** já integrado ao player (`ffmpeg -v error -i <file> -t 120 -f chromaprint -fp_format base64 pipe:1`), obtendo o fingerprint Base64 em ~20ms de forma 100% local sem dependências de DLLs extras e sem enviar áudio para a nuvem.
2. Persistir o hash Chromaprint localmente no banco de dados SQLite (`Song.AcousticFingerprint` e `Song.AcoustId`) através de migration EF Core para evitar reprocessamentos.
3. Desenvolver o serviço cliente `AcoustIdService` acoplado à infraestrutura de resiliência `IProviderPipelineProvider` (`ServiceProviderIds.AcoustId`), garantindo rate limiting estrito de 3 requisições por segundo, circuit breaker, retry com backoff exponencial e cache de sessão.
4. Expandir o **Track Inspector** (`TrackInspectorControl.xaml` e `TrackInspectorViewModel`) com um card dedicado de "Fingerprint & Reconhecimento Acústico", exibindo progresso em tempo real, lista de até 5 candidatos com score ≥ 40% (destacando correspondências ≥ 80%) e ação para vincular identificadores à faixa em memória/banco de dados, sem alterar tags físicas em disco.

---

## Technical Context

**Language/Version**: C# 13 / .NET 10.0  
**Primary Dependencies**:
- Windows App SDK / WinUI 3 (1.7+)
- FFmpeg (com muxer `chromaprint` habilitado e suporte nativo a múltiplos formatos de áudio)
- Polly & Polly.RateLimiting via `IProviderPipelineProvider` para controle estrito de tráfego
- CommunityToolkit.Mvvm para comandos e propriedades observáveis do ViewModel
- Entity Framework Core / SQLite (`MusicDbContext`) para persistência de dados  
**Storage**: SQLite (`MusicDbContext`) para armazenamento do hash `AcousticFingerprint` e `AcoustId` na entidade `Song`  
**Testing**: xUnit com FluentAssertions e `NSubstitute` em `tests/Resonance.Core.Tests`  
**Target Platform**: Windows 10/11 (x64) Desktop unpackaged  
**Project Type**: WinUI 3 Desktop App + Class Library  
**Performance Goals**:
- Cálculo do Chromaprint local em menos de 1,5s para qualquer faixa de até 10 minutos
- Consulta e renderização de candidatos em menos de 1 segundo sob conexões de banda larga padrão
- Zero travamentos da thread de UI (>16ms bloqueando o renderizador)  
**Constraints**:
- **Local-First & Privacy-First**: 100% do cálculo é local; ZERO bytes de amostras de áudio bruto deixam o computador
- **Respeito a Rate Limits**: Máximo rigoroso de 3 req/s na API pública do AcoustID
- **Somente Leitura em Disco**: Candidatos são sugestões; gravação de tags em arquivos físicos é de escopo exclusivo da Feature 006  
**Scale/Scope**: Coleções de milhares de faixas; catálogo do AcoustID/MusicBrainz com dezenas de milhões de gravações registradas.

---

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Princípio Constitucional | Status | Justificativa / Verificação |
|:---|:---:|:---|
| **I. Existing Code Is the Source of Truth** | **PASS** | Reutilização estrita de `FFmpegService`, `IPcmExtractor`, `IProviderPipelineProvider`, `TrackExternalIds`, `TrackInspectorViewModel` e `ISettingsService`. Zero criação de serviços paralelos. |
| **II. Vertical Slices, Not Microtasks** | **PASS** | Estruturado exatamente em 3 fatias verticais ponta a ponta (Slice 1: Fingerprint Local; Slice 2: Cliente AcoustID & Rate Limiting; Slice 3: UI, Candidatos e Track Inspector). |
| **III. Whole-Solution Validation** | **PASS** | Gates obrigatórios: `dotnet restore Resonance.slnx`, `dotnet build Resonance.slnx --configuration Release`, `dotnet test Resonance.slnx`. Nenhuma tarefa será concluída sem validação total limpa. |
| **IV. Local-First and Privacy-First** | **PASS** | O cálculo do Chromaprint ocorre 100% localmente. Apenas a string do fingerprint e a duração são transmitidas ao AcoustID. Provedor pode ser desativado nas Configurações. |
| **V. Licensing and Provider Compliance** | **PASS** | FFmpeg com LGPL/GPLv3 em conformidade com o projeto; API AcoustID respeitada com rate limit de 3 req/s e cabeçalho User-Agent identificando o Resonance. |
| **VI. Large Library Resilience** | **PASS** | Execução assíncrona desacoplada da UI com cancelamento via `CancellationToken` e persistência do hash em banco para eliminar reanálise computacional. |
| **VII. Explicit Boundaries** | **PASS** | Separação entre decodificação sonora (`Core`/`FFmpeg`), comunicação HTTP (`AcoustIdService`), dados (`MusicDbContext`) e apresentação (`TrackInspectorViewModel`). Gravação de tags físicas proibida. |

---

## Implementation Slices (Max 3 Vertical Slices)

### Slice 1: Local Fingerprint Engine (FFmpeg & Chromaprint Pipeline)

```markdown
## META IMUTÁVEL
Problem: O usuário possui arquivos sem tags ou com nomes genéricos e precisa extrair sua impressão digital acústica de forma rápida e 100% local.
Definition of Success: A aplicação executa o FFmpeg chromaprint muxer localmente, gera o hash Base64 e duração da faixa em menos de 1,5s, e persiste o valor no SQLite local sem enviar nenhum byte sonoro pela rede.
```

- **Escopo ponta a ponta**:
  - Criar `AcousticFingerprint` record em `Resonance.Core.Models`.
  - Criar interface `IFingerprintService` em `Resonance.Core.Services.Abstractions`.
  - Implementar `FFmpegFingerprintService` utilizando processo FFmpeg com flags `-v error -i <file> -t 120 -f chromaprint -fp_format base64 pipe:1`.
  - Estender `Song.cs` com as propriedades `AcousticFingerprint` e `AcoustId`.
  - Criar migration do EF Core `AddAcousticFingerprintAndAcoustIdToSong`.
  - Implementar testes unitários em `tests/Resonance.Core.Tests/Services/FingerprintServiceTests.cs` cobrindo áudio sintético, arquivos curtos, silêncio e cancelamento.
- **Validação Local**: `dotnet test tests/Resonance.Core.Tests --filter FullyQualifiedName~FingerprintServiceTests`.

---

### Slice 2: AcoustID Provider, Rate Limiting & Recognition Service

```markdown
## META IMUTÁVEL
Problem: O usuário precisa consultar o catálogo comunitário do AcoustID de forma segura e confiável, respeitando termos de uso e rate limits da API.
Definition of Success: O cliente AcoustIdService executa requisições na API v2 com token bucket de 3 req/s, circuit breaker e retry com backoff exponencial via IProviderPipelineProvider, mapeando respostas para candidatos estruturados com score de confiança e MusicBrainz IDs.
```

- **Escopo ponta a ponta**:
  - Adicionar constante `ServiceProviderIds.AcoustId = "acoustid"` em `Resonance.Core.Models.ServiceProviderIds`.
  - Definir a política do provedor AcoustID no pipeline HTTP de `App.xaml.cs` (3 RPS, 2 conexões simultâneas, retries com backoff).
  - Criar DTOs de deserialização da API AcoustID v2 em `Resonance.Core.Http.AcoustId.AcoustIdDtos`.
  - Criar interface `IAcoustIdService` em `Resonance.Core.Services.Abstractions`.
  - Registrar provedor AcoustID em `SettingsService.cs` e adicionar configuração em `SettingsViewModel.cs` / `SettingsPage.xaml` para ativação/desativação e campo de chave de API pessoal.
  - Implementar `AcoustIdService` em `Resonance.Core.Services.Implementations` utilizando `IProviderPipelineProvider`, `IHttpClientFactory`, `IApiKeyService` e `ISettingsService`.
  - Filtrar candidatos com pontuação mínima de 40%, limitando a até 5 itens e destacando os que possuem score ≥ 80%.
  - Implementar testes com mocks HTTP em `tests/Resonance.Core.Tests/Services/AcoustIdServiceTests.cs` cobrindo sucesso, múltiplos candidatos, rate limit 429 com backoff, modo offline e ausência de resultados.
- **Validação Local**: `dotnet test tests/Resonance.Core.Tests --filter FullyQualifiedName~AcoustIdServiceTests`.

---

### Slice 3: Track Inspector UI, Candidate Selection & Integration

```markdown
## META IMUTÁVEL
Problem: O usuário precisa disparar o reconhecimento de áudio no Track Inspector ou na biblioteca, acompanhar a análise visualmente e escolher com clareza o candidato correspondente.
Definition of Success: O Track Inspector exibe card dedicado de "Fingerprint & Reconhecimento Acústico" com indicador de progresso, status da faixa, lista de candidatos com percentuais de confiança e ação para vincular os IDs à faixa, acompanhado de menu de contexto nas listas de músicas.
```

- **Escopo ponta a ponta**:
  - Expandir `TrackInspectorViewModel` com:
    - Comandos: `IdentifyTrackCommand`, `SelectCandidateCommand`, `DiscardCandidatesCommand`, `ReidentifyTrackCommand`.
    - Propriedades observáveis: `IsRecognizing`, `RecognitionStatusText`, `RecognitionCandidates`, `FingerprintHash`, `IsAlreadyIdentified`.
  - Atualizar `TrackInspectorControl.xaml` inserindo o card colapsável de Fingerprint & Reconhecimento com:
    - Status visual ("Já Identificada" ou "Não Identificada").
    - Botão primário "Identificar Música via Áudio" / "Re-identificar".
    - `ProgressBar` de carregamento indeterminado.
    - `ItemsRepeater` / `ListView` estilizado exibindo candidatos (Título, Artista, Álbum, Confiança % com badge em cor roxa para ≥ 80% e botão "Vincular").
  - Adicionar ação "Identificar Música via Áudio" no menu de contexto de faixas da biblioteca.
  - Registrar os novos serviços no container de injeção de dependências em `App.xaml.cs`.
  - Executar verificação manual e validação da solution completa.
- **Validação Final**: `dotnet restore Resonance.slnx` + `dotnet build Resonance.slnx --configuration Release` + `dotnet test Resonance.slnx --configuration Release --no-build`.

---

## Project Structure

```text
specs/004-audio-fingerprint-music-recognition/
├── plan.md              # Este plano de implementação
├── research.md          # Fase 0: Decisões técnicas e trade-offs
├── data-model.md        # Fase 1: Entidades, DTOs e migrations
├── quickstart.md        # Fase 1: Guia prático de teste e validação
├── contracts/           # Fase 1: Contratos de serviço e DTOs de API
│   ├── IFingerprintService.cs.md
│   ├── IAcoustIdService.cs.md
│   └── AcoustIdApiContracts.md
├── checklists/
│   └── requirements.md  # Checklist de qualidade da especificação
└── tasks.md             # Fase 2: Quebra de tarefas acionáveis (/speckit-tasks)

src/
├── Resonance.Core/
│   ├── Data/
│   │   └── Migrations/  # Migration EF Core adicionando AcousticFingerprint e AcoustId
│   ├── Http/
│   │   └── AcoustId/    # DTOs da API v2 do AcoustID
│   ├── Models/          # AcousticFingerprint, RecognitionCandidate, RecognitionResult
│   └── Services/
│       ├── Abstractions/    # IFingerprintService, IAcoustIdService
│       └── Implementations/ # AcoustIdService, FFmpegFingerprintService
│
├── Resonance.WinUI/
│   ├── Controls/        # TrackInspectorControl.xaml (Card de Fingerprint e Candidatos)
│   └── ViewModels/      # TrackInspectorViewModel (Comandos assíncronos e estados)
│
tests/
└── Resonance.Core.Tests/
    └── Services/        # FingerprintServiceTests.cs, AcoustIdServiceTests.cs
```

---

## Complexity Tracking

> Nenhuma violação aos princípios constitucionais. O design respeita integralmente a arquitetura existente, sem adicionar novas bibliotecas de terceiros e mantendo rigorosamente 3 fatias verticais.
