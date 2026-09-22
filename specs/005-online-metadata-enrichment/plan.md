# Implementation Plan: 005 — Online Metadata Enrichment

**Branch**: `005-online-metadata-enrichment` | **Date**: 2026-09-22 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/005-online-metadata-enrichment/spec.md`

---

## Summary

A **Feature 005** capacita o Resonance Player a enriquecer e padronizar os metadados das faixas a partir de bases abertas mundiais (MusicBrainz e Cover Art Archive). Utilizando o `MusicBrainzTrackId` obtido na Feature 004 (ou busca estruturada por Artista + Título como fallback), o sistema consulta gravações e lançamentos canônicos, armazena os dados em cache local persistente em disco (JSON desacoplado do SQLite) respeitando o limite rigoroso de 1 req/s, resolve a capa frontal em 500px, e gera uma proposta estruturada campo a campo (`EnrichmentProposal`) com rastreamento explícito de proveniência (`LocalTag`, `MusicBrainz`, `CoverArtArchive`) e interatividade por checkboxes no Track Inspector, sem gravar nenhum byte físico no arquivo de áudio (responsabilidade reservada à Feature 006).

---

## Technical Context

**Language/Version**: C# 13 / .NET 10.0  
**Primary Dependencies**:
- Windows App SDK / WinUI 3 (1.7+)
- `Microsoft.Extensions.Http` / `IHttpClientFactory`
- Polly & Polly.RateLimiting via `IProviderPipelineProvider` (`ServiceProviderIds.MusicBrainz`, `ServiceProviderIds.ImageDownload`)
- CommunityToolkit.Mvvm para comandos e propriedades observáveis do ViewModel
- `System.Text.Json` para serialização de cache e DTOs  
**Storage**:
- Cache em arquivos JSON compactos em disco sob `%LocalAppData%/Resonance/cache/metadata/` (`IAppInfoService.CachePath`) com expiração de 7 dias
- Zero migrações no banco SQLite da biblioteca (`MusicDbContext`)  
**Testing**: xUnit com FluentAssertions e `NSubstitute` em `tests/Resonance.Core.Tests`  
**Target Platform**: Windows 10/11 (x64) Desktop unpackaged  
**Project Type**: WinUI 3 Desktop App + Class Library  
**Performance Goals**:
- Resolução de dados cacheados em menos de 15ms
- Zero bloqueios na thread de reprodução de áudio e UI (>16ms)
- Download e redimensionamento suave da capa de 500px  
**Constraints**:
- **Local-First & Privacy-First**: Toda consulta remota é opcional e falha graciosamente em modo offline sem estalos ou erros de playback
- **Rate Limit Estrito**: Máximo de 1 requisição por segundo no MusicBrainz com cabeçalho `User-Agent: Resonance/1.0 (+https://github.com/RaphaelAndradeM3/resonance-player)`
- **Somente Leitura em Disco**: Propostas são estritamente mantidas em memória ou cache de visualização; zero gravação destrutiva em arquivos de áudio  
**Scale/Scope**: Catálogo global do MusicBrainz (~40 milhões de gravações e ~3 milhões de lançamentos).

---

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Princípio Constitucional | Status | Justificativa / Verificação |
|:---|:---:|:---|
| **I. Existing Code Is the Source of Truth** | **PASS** | Reutiliza e expande `MusicBrainzService`, `IProviderPipelineProvider`, `IAppInfoService`, `TrackExternalIds`, `TrackAudioTags`, `Song` e `TrackInspectorViewModel`. Zero criação de serviços paralelos. |
| **II. Vertical Slices, Not Microtasks** | **PASS** | Estruturado rigorosamente em no máximo 3 fatias verticais ponta a ponta (Slice 1: MusicBrainz, CAA & Cache; Slice 2: Merge Engine & Provenance; Slice 3: Track Inspector UI & Regressão). |
| **III. Whole-Solution Validation** | **PASS** | Gates obrigatórios: `dotnet restore Resonance.slnx`, `dotnet build Resonance.slnx --configuration Release -p:Platform=x64`, `dotnet test Resonance.slnx --configuration Release --no-build`. |
| **IV. Local-First and Privacy-First** | **PASS** | Consulta online é opcional; o player funciona 100% offline. Falhas de rede ou timeout não degradam a reprodução nem a navegação local. Nenhum áudio é enviado. |
| **V. Licensing and Provider Compliance** | **PASS** | Conformidade estrita com as diretrizes de API do MusicBrainz (1 req/s, User-Agent explícito) e Cover Art Archive (acesso a dados públicos abertos sob CC0/CC-BY). |
| **VI. Large Library Resilience** | **PASS** | Consultas assíncronas desacopladas da UI, com cancelamento via `CancellationToken` e cache local persistente em disco para eliminar tráfego redundante de rede. |
| **VII. Explicit Boundaries** | **PASS** | Separação rigorosa entre consulta externa (`MusicBrainzService`), comparação lógica pura (`MetadataEnrichmentService`), cache de disco e apresentação (`TrackInspectorViewModel`). Gravação física em disco totalmente proibida (Feature 006). |

---

## Project Structure

### Documentation (this feature)

```text
specs/005-online-metadata-enrichment/
├── spec.md              # Feature specification com clarificações integradas
├── plan.md              # Este plano de implementação
├── research.md          # Decisões arquiteturais consolidadas
├── data-model.md        # Entidades, enums e ciclo de vida
├── quickstart.md        # Guia de validação automatizada e comandos
├── contracts/           # Contratos de interfaces e DTOs
│   ├── IMusicBrainzService.cs.md
│   ├── IMetadataEnrichmentService.cs.md
│   └── EnrichmentDtos.md
├── checklists/          # Checklists de qualidade
│   └── requirements.md
└── tasks.md             # Tarefas de implementação (geradas pelo /speckit-tasks)
```

### Source Code (repository layout)

```text
src/
├── Resonance.Core/
│   ├── Http/
│   │   └── MusicBrainz/
│   │       └── MusicBrainzLookupDtos.cs       # DTOs de desserialização da API Web Service v2
│   ├── Models/
│   │   ├── MetadataProvenance.cs              # Enum de proveniência (LocalTag, MusicBrainz, etc.)
│   │   ├── FieldProposalStatus.cs             # Enum de status (Unchanged, Updated, NewValue, Conflict)
│   │   ├── FieldProposal.cs                   # Modelo de campo individual com IsSelected
│   │   ├── EnrichmentProposal.cs              # Modelo consolidado da proposta de enriquecimento
│   │   └── MusicBrainzRecordingDetail.cs      # Modelo canônico extraído do MusicBrainz
│   └── Services/
│       ├── Abstractions/
│       │   ├── IMusicBrainzService.cs         # Expansão com GetRecordingMetadataAsync e Cover Art
│       │   └── IMetadataEnrichmentService.cs  # Motor de comparação e fusão de metadados
│       └── Implementations/
│           ├── MusicBrainzService.cs          # Implementação com rate limit, release selection e cache em disco
│           └── MetadataEnrichmentService.cs   # Implementação do motor de mesclagem e regras de gênero
│
├── Resonance.WinUI/
│   ├── Controls/
│   │   └── TrackInspectorControl.xaml         # Card expansível "Enriquecimento de Metadados Online"
│   └── ViewModels/
│       ├── ITrackInspectorViewModel.cs        # Expansão do contrato do ViewModel
│       └── TrackInspectorViewModel.cs         # Comandos de enriquecimento, bind de proposta e checkboxes
│
tests/
└── Resonance.Core.Tests/
    └── Services/
        ├── MusicBrainzServiceTests.cs         # Testes de cliente MusicBrainz, CAA, rate limit e cache
        └── MetadataEnrichmentServiceTests.cs  # Testes de merge, provenances, status e regra de gênero
```

---

## Implementation Slices (Max 3 Vertical Slices)

### Slice 1: MusicBrainz Recording Lookup, Cover Art Archive & Cache

```markdown
## META IMUTÁVEL
Problem: O usuário precisa obter metadados canônicos completos (gravação, álbum, ano, faixas, disco, ISRC, gênero e arte de capa oficial) para uma música sem sobrecarregar a infraestrutura pública externa.
Definition of Success: O cliente MusicBrainzService consulta a gravação no MusicBrainz Web Service v2 utilizando rate limit de 1 req/s via IProviderPipelineProvider, aplica a heurística canônica de seleção de release oficial mais antigo (FR-009), resolve a capa oficial 500px via Cover Art Archive (FR-010), e persiste o resultado em cache local JSON em disco (FR-011) respondendo subsequentemente em < 15ms.
```

- **Escopo ponta a ponta**:
  - Criar DTOs em `src/Resonance.Core/Http/MusicBrainz/MusicBrainzLookupDtos.cs`.
  - Criar modelo `MusicBrainzRecordingDetail` em `src/Resonance.Core/Models/`.
  - Estender `IMusicBrainzService.cs` com `GetRecordingMetadataAsync`, `SearchRecordingAsync` e `GetCoverArtUrlAsync`.
  - Implementar métodos em `MusicBrainzService.cs` integrando `IProviderPipelineProvider` (`ServiceProviderIds.MusicBrainz`), heurística de seleção de release (status Official, tipo Album, data mais antiga ou match de álbum local) e resolução de capa CAA (`front-500` com fallback para `front-250`).
  - Implementar persistência de cache JSON em disco em `IAppInfoService.CachePath/metadata/` com TTL de 7 dias.
  - Criar testes unitários e de integração mockados em `tests/Resonance.Core.Tests/Services/MusicBrainzServiceTests.cs` cobrindo sucesso com MBID, múltiplos releases, fallback textual, User-Agent, rate limit e hit de cache.
- **Validação Local**: `dotnet test tests/Resonance.Core.Tests --filter FullyQualifiedName~MusicBrainzServiceTests`.

---

### Slice 2: Metadata Merge Engine & Provenance Model

```markdown
## META IMUTÁVEL
Problem: O usuário precisa comparar os metadados locais de uma música com as sugestões online campo a campo, com transparência total sobre a origem de cada informação e sem risco de apagar anotações locais.
Definition of Success: O serviço MetadataEnrichmentService compara a tag local com a resposta do MusicBrainz, gera um EnrichmentProposal completo onde cada campo possui seu status (Unchanged, Updated, NewValue, Conflict), proveniência (MetadataProvenance), seleção interativa inicial (IsSelected) e fusão semântica de gênero com deduplicação (FR-013), com zero gravação em disco.
```

- **Escopo ponta a ponta**:
  - Criar enums `MetadataProvenance` e `FieldProposalStatus` em `src/Resonance.Core/Models/`.
  - Criar modelos `FieldProposal` e `EnrichmentProposal` em `src/Resonance.Core/Models/`.
  - Criar interface `IMetadataEnrichmentService` em `src/Resonance.Core/Services/Abstractions/`.
  - Implementar `MetadataEnrichmentService` em `src/Resonance.Core/Services/Implementations/`:
    - Normalização de strings e comparação campo a campo (Título, Artista, Álbum, Artista do Álbum, Ano, Número da Faixa, Total de Faixas, Disco, Total de Discos, Gravadora, ISRC, Arte de Capa).
    - Regra de seleção padrão: `IsSelected = true` para `NewValue` e `Updated`; `IsSelected = false` para `Unchanged` e `Conflict`.
    - Regra de Gênero (FR-013): preservar gênero local e concatenar com tags do MusicBrainz separadas por `;` com deduplicação semântica.
    - Preservação estrita de campos locais não mapeados pelo MusicBrainz (comentários, BPM).
  - Criar testes em `tests/Resonance.Core.Tests/Services/MetadataEnrichmentServiceTests.cs` validando todos os status de campos, regra de gênero, faixas sem tags e conflitos de grafia.
- **Validação Local**: `dotnet test tests/Resonance.Core.Tests --filter FullyQualifiedName~MetadataEnrichmentServiceTests`.

---

### Slice 3: Track Inspector UI, Interactive Proposal & Regression

```markdown
## META IMUTÁVEL
Problem: O usuário precisa visualizar e interagir com a proposta de enriquecimento dentro do Track Inspector, selecionando quais campos deseja incorporar antes de encaminhar para a revisão/edição de tags, funcionando graciosamente offline.
Definition of Success: O Track Inspector disponibiliza card expansível de "Enriquecimento de Metadados Online", dispara a busca sob demanda com indicador de carregamento, renderiza a grade comparativa com checkboxes interativos por campo, exibe badges visuais de status e proveniência, mostra a capa oficial sugerida lado a lado com a capa local, e permite acionar "Avançar para Revisão de Tags" em memória, com zero erros na compilação Release e 100% dos testes passando.
```

- **Escopo ponta a ponta**:
  - Expandir `ITrackInspectorViewModel` e `TrackInspectorViewModel` com:
    - Comandos: `EnrichOnlineMetadataCommand`, `ClearEnrichmentProposalCommand`.
    - Propriedades observáveis: `EnrichmentProposal`, `IsEnriching`, `EnrichmentStatusText`, `HasEnrichmentProposal`, `ProposedCoverImage`.
  - Atualizar `TrackInspectorControl.xaml` inserindo o card expansível:
    - Botão "Buscar Metadados Online" / "Recarregar".
    - `ProgressBar` de carregamento não-bloqueante.
    - Tabela/ListView com cabeçalho (Selecionar, Campo, Atual, Sugerido, Origem, Status).
    - Checkbox vinculado a `FieldProposal.IsSelected`.
    - Badges com estilo e cores da aplicação para status (`Novo`, `Atualizado`, `Conflito`, `Inalterado`) e proveniência (`MusicBrainz`, `CoverArtArchive`).
    - Preview de Capa Local vs Capa Sugerida.
    - Botão "Avançar para Revisão de Tags" (prepara a proposta selecionada para a Feature 006).
  - Adicionar atalho "Buscar Metadados Online" no menu de contexto das listas (`SongListViewModelBase`, `LibraryPage.xaml`, etc.).
  - Implementar testes de fluxo do ViewModel e testes de integração end-to-end.
  - Validação completa com os gates da solução (`dotnet build Resonance.slnx -p:Platform=x64` e `dotnet test Resonance.slnx`).
- **Validação Local**: `dotnet build Resonance.slnx --configuration Release -p:Platform=x64` e `dotnet test Resonance.slnx --configuration Release --no-build`.

---

## .NET Toolchain Validation Gates

Para aprovação de cada fatia vertical e validação final da feature:

```powershell
dotnet restore Resonance.slnx
dotnet build Resonance.slnx --configuration Release -p:Platform=x64
dotnet test Resonance.slnx --configuration Release --no-build
```

Nenhum código pode ser integrado se:
1. Houver qualquer tentativa de gravação física em arquivo de mídia no disco (Princípio VII).
2. O limite de 1 requisição por segundo do MusicBrainz for violado (Princípio V).
3. A ausência de internet quebrar a reprodução ou travar a interface do usuário (Princípio IV).
