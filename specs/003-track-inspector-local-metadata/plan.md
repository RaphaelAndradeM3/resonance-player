# Implementation Plan: 003 — Track Inspector & Local Metadata

**Branch**: `003-track-inspector-local-metadata` | **Date**: 2026-09-20 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/003-track-inspector-local-metadata/spec.md`

---

## Summary

O **Track Inspector** fornece ao ouvinte e audiófilo uma experiência transparente e detalhada de inspeção de propriedades técnicas de áudio (codec, container, sample rate, bit depth, canais, bitrate CBR/VBR, tamanho em disco) e metadados musicais (título, artistas, álbum, numeração, ano, gênero, compositor, ReplayGain, letras e capa em alta resolução).

A abordagem técnica consiste em:
1. Evoluir o serviço existente `AtlMetadataService` em `Resonance.Core` para extrair nativamente todos os 23 atributos através da biblioteca ATL.NET já integrada, sem adicionar novos parsers ou dependências externas.
2. Criar os modelos `TrackTechnicalDetails`, `TrackTagDetails`, `TrackArtworkDetails`, `TrackExternalIds` e `TrackInspectorViewData`.
3. Implementar o `TrackInspectorViewModel` e o controle `TrackInspectorControl` em `Resonance.WinUI`.
4. Integrar o painel retrátil diretamente na margem direita de `MainPage.xaml`, acoplado ao `NavigationView` ao lado de `ContentFrame`, com suporte ao atalho global `Alt+Enter`, menu de contexto, navegação sequencial em seleções múltiplas e visualizador com exportação de arte de capa (*LightBox*).

---

## Technical Context

**Language/Version**: C# 13 / .NET 10.0  
**Primary Dependencies**:
- Windows App SDK / WinUI 3 (1.7+)
- ATL.NET (`z4kn4fein.atl.core`) para extração profunda de áudio e tags
- CommunityToolkit.Mvvm para comandos e propriedades observáveis
- SixLabors.ImageSharp para processamento seguro de imagens em background
- Microsoft.Extensions.DependencyInjection para injeção de dependências  
**Storage**: SQLite (`MusicDbContext`) para leitura de dados persistidos e leitura direta do sistema de arquivos via `IFileSystemService`  
**Testing**: xUnit com FluentAssertions em `tests/Resonance.Core.Tests`  
**Target Platform**: Windows 10/11 (x64) Desktop  
**Project Type**: WinUI 3 Desktop App + Class Library  
**Performance Goals**:
- Abertura e renderização do painel em menos de 150ms para arquivos locais
- Zero travamento da thread de UI (>16ms bloqueando o renderizador) durante a decodificação de metadados e capas pesadas  
**Constraints**:
- **Local-First**: Operação 100% offline para arquivos locais
- **Somente Leitura**: Sem gravação destrutiva de tags nesta etapa (escopo reservado à Feature 006)
- **Não Duplicar Abstrações**: Reutilizar `AtlMetadataService` e `IMetadataService` existentes  
**Scale/Scope**: Catálogos com milhares de músicas; suporte a arquivos individuais com capas embutidas de até 25MB sem sobrecarregar a memória.

---

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Princípio Constitucional | Status | Justificativa / Verificação |
|---|---|---|
| **I. Existing Code Is the Source of Truth** | **PASS** | Mapeamento completo dos projetos existentes (`Resonance.Core`, `Resonance.WinUI`, `Resonance.Core.Tests`). Reutilização estrita de `IMetadataService` e `AtlMetadataService`. Zero criação de serviços ou parsers concorrentes. |
| **II. Vertical Slices, Not Microtasks** | **PASS** | Estruturado exatamente em 3 fatias verticais ponta a ponta (Slice 1: Read Model & Core DTOs; Slice 2: UI Panel & ViewModel; Slice 3: Integration, Navigation & Regression). |
| **III. Whole-Solution Validation** | **PASS** | Gates obrigatórios: `dotnet restore`, `dotnet build Resonance.slnx --configuration Release`, `dotnet test Resonance.slnx`. Nenhuma tarefa será concluída sem compilação total limpa da solution. |
| **IV. Local-First and Privacy-First** | **PASS** | Todos os metadados técnicos e tags são lidos estritamente do disco local offline. Zero tráfego de rede ou upload de áudio. |
| **V. Licensing and Provider Compliance** | **PASS** | Nenhuma biblioteca nova adicionada. ATL.NET é licenciada sob MIT compatível com o projeto. |
| **VI. Large Library Resilience** | **PASS** | Extração assíncrona com cancelamento via `CancellationToken` e decodificação protegida contra imagens corrompidas ou gigantes. |
| **VII. Explicit Boundaries** | **PASS** | Isolamento claro entre extração de metadados (`Resonance.Core`), orquestração observável (`ViewModels`) e apresentação WinUI (`Controls`). Gravação de tags estritamente proibida nesta etapa. |

---

## Implementation Slices (Max 3 Vertical Slices)

### Slice 1: Technical & Tag Read Model (Core)

```markdown
## META IMUTÁVEL
Problem: O usuário precisa visualizar propriedades técnicas reais e tags completas do arquivo de áudio.
Definition of Success: O leitor de metadados extrai com precisão todos os 23 parâmetros (codec, container, CBR/VBR, sample rate, bit depth, canais, ReplayGain, ISRC e imagens) sem dependências externas adicionais.
```

- **Escopo ponta a ponta**:
  - Criar DTOs `TrackTechnicalDetails`, `TrackTagDetails`, `TrackArtworkDetails`, `TrackExternalIds` e `TrackInspectorViewData` em `Resonance.Core.Models`.
  - Estender `IMetadataService` com `GetTrackInspectorViewDataAsync`.
  - Implementar no `AtlMetadataService` a leitura e normalização de `BitrateType`, `BitDepth`, contêineres, ReplayGain completo e dados de imagem embutida.
  - Criar testes unitários em `tests/Resonance.Core.Tests/Services/AtlMetadataServiceTests.cs` cobrindo arquivos reais e sintéticos de diversos formatos.
- **Validação Local**: `dotnet test tests/Resonance.Core.Tests --filter FullyQualifiedName~AtlMetadataService`.

---

### Slice 2: Track Inspector UI & ViewModel (WinUI)

```markdown
## META IMUTÁVEL
Problem: O usuário precisa de uma interface ergonômica, elegante e responsiva para inspecionar os metadados.
Definition of Success: Painel retrátil funcional com apresentação organizada por abas/seções, carregamento assíncrono não bloqueante e modal LightBox para visualização/exportação de capas.
```

- **Escopo ponta a ponta**:
  - Criar `TrackInspectorViewModel` com comandos assíncronos (`ToggleInspectorCommand`, `InspectSongCommand`, `InspectMultipleSongsCommand`, `ExportArtworkCommand`).
  - Desenvolver o `UserControl` `TrackInspectorControl.xaml` com seções colapsáveis para:
    - Cabeçalho: Título, Artistas, Capa (com badge de proveniência e botão de zoom)
    - Especificações Técnicas: Codec, formato, taxa de amostragem, profundidade, canais, bitrate CBR/VBR e tamanho
    - Tags Editoriais: Álbum, numerações, ano, gêneros, compositor, ISRC, ReplayGain
    - Letras e IDs Externos (AcoustID / MusicBrainz)
  - Implementar o diálogo modal `ArtworkLightBoxDialog` para exibição em tamanho real e ação de salvar no disco.
  - Registrar os novos componentes no container de injeção de dependências em `App.xaml.cs`.
- **Validação Local**: `dotnet build Resonance.slnx` + testes de ViewModel.

---

### Slice 3: Integration, Navigation & Regression (End-to-End)

```markdown
## META IMUTÁVEL
Problem: O usuário precisa acessar o Inspector de forma rápida a partir de qualquer ponto da aplicação (Now Playing, Biblioteca, Álbuns) sem atrito.
Definition of Success: O Inspector abre via atalho `Alt+Enter`, menu de contexto de música e botão do player; acompanha dinamicamente a faixa em reprodução e pagina seleções múltiplas.
```

- **Escopo ponta a ponta**:
  - Integrar `TrackInspectorControl` à coluna direita de `MainPage.xaml`, acoplado ao layout principal com animação suave de abertura/fechamento.
  - Configurar `KeyboardAccelerator` global para `Alt+Enter` no `MainPageRoot`.
  - Adicionar item de menu de contexto "Inspecionar Faixa / Propriedades" em listas de músicas da biblioteca.
  - Conectar sincronização automática com a faixa ativa de `IMusicPlaybackService` quando a opção "Seguir reprodução" estiver marcada.
  - Implementar controles de paginação no cabeçalho do Inspector (`< Anterior` / `Próxima >` com contador `1 de N`) quando múltiplas faixas estiverem selecionadas.
  - Executar bateria completa de testes de regressão na solução inteira.
- **Validação Final**: `dotnet test Resonance.slnx --configuration Release`.

---

## Project Structure

### Documentation (this feature)

```text
specs/003-track-inspector-local-metadata/
├── spec.md              # Especificação refinada e validada
├── checklists/
│   └── requirements.md  # Checklist de qualidade (16/16 aprovado)
├── plan.md              # Este plano de implementação
├── research.md          # Pesquisa técnica e decisões de arquitetura
├── data-model.md        # Modelo de entidades e DTOs
├── quickstart.md        # Guia de validação ponta a ponta
├── contracts/
│   └── track-inspector-contract.md # Contratos de serviço, ViewModel e UI
└── tasks.md             # Tarefas executáveis (gerado na próxima fase por /speckit-tasks)
```

### Source Code (repository layout)

```text
src/Resonance.Core/
├── Models/
│   ├── TrackTechnicalDetails.cs
│   ├── TrackTagDetails.cs
│   ├── TrackArtworkDetails.cs
│   ├── TrackExternalIds.cs
│   └── TrackInspectorViewData.cs
└── Services/
    ├── Abstractions/
    │   └── IMetadataService.cs
    └── Implementations/
        └── AtlMetadataService.cs

src/Resonance.WinUI/
├── Controls/
│   ├── TrackInspectorControl.xaml
│   └── TrackInspectorControl.xaml.cs
├── ViewModels/
│   └── TrackInspectorViewModel.cs
├── Dialogs/
│   ├── ArtworkLightBoxDialog.xaml
│   └── ArtworkLightBoxDialog.xaml.cs
├── MainPage.xaml
├── MainPage.xaml.cs
└── App.xaml.cs

tests/Resonance.Core.Tests/
└── Services/
    └── AtlMetadataServiceTests.cs
```

**Structure Decision**: A implementação segue estritamente a divisão em camadas existente no repositório: regras de extração de dados e modelos em `Resonance.Core`, componentes visuais WinUI 3 e ViewModels em `Resonance.WinUI`, e testes automatizados em `Resonance.Core.Tests`.

---

## Complexity Tracking

> **Nenhuma violação constitucional detectada.** Não foram criadas bibliotecas paralelas, camadas desnecessárias ou arquiteturas concorrentes. Todas as adições evoluem o código existente.
