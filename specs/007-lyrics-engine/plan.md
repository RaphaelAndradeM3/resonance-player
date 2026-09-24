# Implementation Plan: 007 — Lyrics Engine

**Branch**: `007-lyrics-engine` | **Date**: 2026-09-23 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/007-lyrics-engine/spec.md` e Clarifications Session 2026-09-23

---

## Summary

A **Feature 007** estabelece o mecanismo robusto, previsível e local-first de resolução, cache e sincronização de letras no Resonance Player. O sistema unifica fontes embutidas (tags físicas ID3v2/Vorbis via ATL.NET), arquivos locais adjacentes (*sidecars* `.lrc` e `.txt` com suporte a nome base e `<Artista> - <Título>`), cache interno isolado em `%LocalAppData%` e provedores remotos opcionais (LRCLIB e NetEase), obedecendo rigorosamente à ordem canônica de 6 etapas. O recurso suporta letras sincronizadas com rolagem suave e salto no áudio (seek por clique), letras de texto puro (com rolagem manual e indicador de não-sincronizada), detecção e memorização de faixas instrumentais no SQLite (eliminando requisições redundantes de rede), controles em tempo real para calibração de offset (+/- 100ms/500ms) persistidos por faixa e ação manual para exportação de `.lrc` na pasta da música.

---

## Technical Context

**Language/Version**: C# 13 / .NET 10.0  
**Primary Dependencies**:
- Windows App SDK / WinUI 3 (1.7+)
- ATL.NET (`ATL.Track`, `LyricsInfo`) para extração de letras sincronizadas (SYLT) e texto puro (USLT) embutidas em containers de áudio
- ModernLrc / parser interno de timestamps LRC
- CommunityToolkit.Mvvm para comandos e bindings reativos
- Microsoft.EntityFrameworkCore / SQLite (`MusicDbContext`) para persistência de `IsInstrumental`, `LyricsOffsetMs` e `LyricsLastCheckedUtc`  
**Storage**:
- Sistema de arquivos local (arquivos de mídia e sidecars `.lrc`/`.txt`)
- Cache interno de letras isolado em `%LocalAppData%\Resonance\Cache\Lrc\`
- Banco de dados SQLite local (`MusicDbContext`)  
**Testing**: xUnit com FluentAssertions e `NSubstitute` em `tests/Resonance.Core.Tests`  
**Target Platform**: Windows 10/11 (x64) Desktop unpackaged  
**Project Type**: WinUI 3 Desktop App + Class Library  
**Performance Goals**:
- Leitura e renderização de arquivo `.lrc` local ou tag embutida em menos de 50ms (SC-001)
- Atualização do destaque da linha ativa durante playback com precisão mínima de 100ms (FR-003)
- Rolagem suave com taxa de quadros fluida sem sobressaltos visuais (SC-002)  
**Constraints**:
- **Local-First & Soberania**: Zero requisições de rede executadas se o arquivo local contiver letra (SC-003)
- **Não-Destrutivo**: Provedores remotos nunca gravam automaticamente na pasta de mídia do usuário sem ação explícita (Princípio Constitucional VII)
- **Modo Offline Resiliente**: Cache interno permanente e tratamento explícito de faixas instrumentais sem polling repetido (FR-005, FR-009)
- **Máximo de 3 Fatias Verticais**: Implementação ponta a ponta sem fragmentação em microtarefas (Princípio Constitucional II)  
**Scale/Scope**: Todas as faixas reproduzíveis na biblioteca local.

---

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Princípio Constitucional | Status | Justificativa / Verificação |
|:---|:---:|:---|
| **I. Existing Code Is the Source of Truth** | **PASS** | Evolve `ILrcService`, `LrcService`, `AtlMetadataService`, `LrcLibService`, `LyricsPageViewModel` e `LyricsPage.xaml`. Reutiliza ATL.NET existente. Zero criação de bibliotecas ou serviços paralelos de letras. |
| **II. Vertical Slices, Not Microtasks** | **PASS** | Estruturado rigorosamente em 3 fatias verticais ponta a ponta: Slice 1 (Resolução Local e Parser); Slice 2 (Provedores Remotos, Cache e Exportação); Slice 3 (UI Sincronizada, Offset e Regressão). |
| **III. Whole-Solution Validation** | **PASS** | Validação mandatória: `dotnet restore Resonance.slnx`, `dotnet build Resonance.slnx --configuration Release -p:Platform=x64 --warnaserror`, `dotnet test Resonance.slnx --configuration Release -p:Platform=x64 --no-build`. |
| **IV. Local-First and Privacy-First** | **PASS** | Letras locais (embutidas ou sidecar) funcionam 100% offline. Consultas online são estritamente opcionais, desativáveis por configuração, e só enviam metadados mínimos (título, artista, álbum, duração). |
| **V. Licensing and Provider Compliance** | **PASS** | Preserva a licença GPLv3; respeita termos e rate limits do LRCLIB; cache mantido unicamente para visualização local do usuário sem redistribuição. |
| **VI. Large Library Resilience** | **PASS** | Resolução não-bloqueante executada de forma assíncrona; tolerância a arquivos `.lrc` malformados; memorização de checagens para não sobrecarregar rede ou I/O. |
| **VII. Explicit Boundaries** | **PASS** | Provedor online grava exclusivamente no cache interno; escrita na pasta do áudio ocorre apenas sob clique explícito no botão "Exportar como .lrc". UI desacoplada do acesso direto a disco/rede. |

---

## Project Structure

### Documentation (this feature)

```text
specs/007-lyrics-engine/
├── spec.md              # Especificação com clarificações de escopo integradas
├── plan.md              # Este plano de implementação
├── research.md          # Decisões de arquitetura (resolução canônica, ATL, cache, offset)
├── data-model.md        # Modelos de domínio (LyricsDocument, Provenance, Enums, Song schema)
├── quickstart.md        # Guia de validação automatizada e cenários manuais
├── contracts/           # Contratos de interfaces e extensões
│   ├── ILrcService.cs.md
│   └── ILibraryWriterLyrics.cs.md
└── checklists/          # Checklist de qualidade
    └── requirements.md
```

### Source Code (repository layout)

```text
src/
├── Resonance.Core/
│   ├── Data/
│   │   └── Migrations/
│   │       └── [Timestamp]_AddLyricsInstrumentalAndOffsetToSong.cs  # Migration EF Core
│   ├── Models/
│   │   ├── Song.cs                                                 # Campos IsInstrumental e LyricsOffsetMs
│   │   └── Lyrics/
│   │       ├── LyricsDocument.cs                                   # Modelo unificado de documento de letras
│   │       ├── LyricsProvenance.cs                                 # Enumeração da procedência da letra
│   │       ├── LyricsType.cs                                       # Enumeração da modalidade da letra
│   │       ├── LyricLine.cs                                        # Verso/estrofe com timestamp (preservado)
│   │       └── ParsedLrc.cs                                        # Container legado preservado para retrocompatibilidade
│   └── Services/
│       ├── Abstractions/
│       │   ├── ILrcService.cs                                      # Contrato expandido da engine de letras
│       │   └── ILibraryWriter.cs                                   # Métodos para persistir offset e instrumental
│       └── Implementations/
│           ├── LrcService.cs                                       # Implementação do motor canônico de 6 etapas
│           ├── LrcLibService.cs                                    # Tratamento de flag instrumental do LRCLIB
│           └── LibraryWriter.cs                                    # Persistência de offset e flag instrumental
│
├── Resonance.WinUI/
│   ├── Pages/
│   │   ├── LyricsPage.xaml                                         # UI com selo de proveniência, controles de offset e exportação
│   │   └── LyricsPage.xaml.cs                                      # Code-behind e eventos visuais
│   └── ViewModels/
│       └── LyricsPageViewModel.cs                                  # ViewModel com suporte a proveniência, offset, texto puro e exportação
│
└── tests/
    └── Resonance.Core.Tests/
        ├── LrcServiceTests.cs                                      # Testes da resolução canônica e precedência
        └── LrcLibServiceTests.cs                                   # Testes de fallback, instrumental e mock HTTP
```

---

## Vertical Slices de Implementação

### Slice 1: Local Lyrics Resolution & Parser
- **META IMUTÁVEL**:
  > **Problema de Negócio:** Resolver e parsear letras locais (embutidas via ATL e sidecars `.lrc`/`.txt`) seguindo estritamente a precedência canônica local (Embedded Synced $\to$ Embedded Plain $\to$ Sidecar `.lrc` $\to$ Sidecar `.txt`), sem realizar chamadas externas quando houver dado local.
  > **Definição de Sucesso:** A engine identifica e parseia corretamente letras embutidas e sidecars com casamento por nome base e fallback `<Artista> - <Título>`, indicando proveniência e tipo sem bloquear a reprodução.
  > **Regra de Ouro:** Não inventar bibliotecas externas; utilizar ATL.NET existente.
- **Escopo ponta a ponta**:
  - Implementar `LyricsDocument`, `LyricsProvenance` e `LyricsType`.
  - Expandir `LrcService.cs` para extrair letras embutidas via `ATL.Track` (`SynchronizedLyrics` e `UnsynchronizedLyrics`).
  - Implementar resolução de arquivos locais adjacentes na pasta do áudio (prioridade: `<NomeDoAudio>.lrc/.txt`, fallback: `<Artista> - <Título>.lrc/.txt`, case-insensitive).
  - Preservar métodos legados de `ILrcService` para total retrocompatibilidade.
- **Reutilização obrigatória**: `ATL.Track`, `IFileSystemService`, `LrcService`.
- **Teste obrigatório**: Testes unitários em `LrcServiceTests.cs` cobrindo matriz completa de precedência local, timestamps normais e com horas, e arquivos `.txt` puros.
- **Validação Local**: `dotnet test tests/Resonance.Core.Tests/Resonance.Core.Tests.csproj --filter "FullyQualifiedName~LrcService"`.

### Slice 2: Remote Provider, Cache Layer & Explicit Export
- **META IMUTÁVEL**:
  > **Problema de Negócio:** Consultar provedores online opcionais somente quando não houver letra local, armazenar respostas em cache isolado em `%LocalAppData%`, memorizar faixas instrumentais no banco e permitir exportar `.lrc` sob demanda.
  > **Definição de Sucesso:** Faixas sem letra consultam LRCLIB/NetEase de forma assíncrona; cache armazena letras com hash de identidade; faixas instrumentais registram flag no banco; ação de exportação grava `.lrc` na pasta do áudio.
  > **Regra de Ouro:** Não escrever silenciosamente na pasta de mídia do usuário.
- **Escopo ponta a ponta**:
  - Adicionar `IsInstrumental` e `LyricsOffsetMs` em `Song` e gerar migration EF Core no `MusicDbContext`.
  - Implementar métodos de escrita em `LibraryWriter` (`UpdateSongLyricsOffsetAsync`, `UpdateSongInstrumentalAsync`).
  - Atualizar `LrcLibService` para capturar `Instrumental == true` e retornar essa informação.
  - Implementar `ExportSidecarLrcAsync` em `LrcService` para salvar `.lrc` na pasta do áudio sob demanda.
  - Registrar checagem no banco (`LyricsLastCheckedUtc`) para faixas sem letra ou instrumentais.
- **Reutilização obrigatória**: `IPathConfiguration`, `IOnlineLyricsService`, `ILibraryWriter`, `MusicDbContext`.
- **Teste obrigatório**: Testes com respostas mockadas do LRCLIB (synced, plain, instrumental), persistência de cache e gravação de sidecar exportado.
- **Validação Local**: `dotnet test tests/Resonance.Core.Tests/Resonance.Core.Tests.csproj --filter "FullyQualifiedName~LrcLibService|FullyQualifiedName~LrcService"`.

### Slice 3: Synchronized Lyrics UI, Offset Controls & Full Regression
- **META IMUTÁVEL**:
  > **Problema de Negócio:** Renderizar as letras na tela com destaque sincronizado da linha ativa, badge de proveniência visível, modo texto puro com seek desativado, mensagem musical para instrumental, controles táteis de offset (+/- 100ms/500ms) e botão de exportação.
  > **Definição de Sucesso:** A página de letras (`LyricsPage`) exibe em tempo real o texto correto com rolagem suave, permite ajustar o offset no momento da reprodução memorizando a calibração, e exibe estados vazios e de texto puro com clareza.
  > **Regra de Ouro:** Não recriar a página do zero nem duplicar serviços de playback.
- **Escopo ponta a ponta**:
  - Atualizar `LyricsPageViewModel` integrando a chamada a `ResolveLyricsAsync`, propriedades de proveniência (`ProvenanceLabel`), comandos de offset (`AdjustOffsetCommand`, `ResetOffsetCommand`), comando de exportação (`ExportSidecarLrcCommand`) e binding para faixa instrumental.
  - Atualizar `LyricsPage.xaml` com o selo de proveniência no cabeçalho, indicador `[Não Sincronizada]`, estado vazio para instrumental (`♫ Faixa Instrumental`), botões de ajuste de offset e botão de exportação.
  - Ajustar seek por clique para respeitar o offset calibrado e desabilitar seek em texto puro.
  - Validação de compilação da solution completa e execução da suite de testes.
- **Reutilização obrigatória**: `LyricsPage.xaml`, `LyricsPageViewModel`, `IMusicPlaybackService`.
- **Teste obrigatório**: Testes de regressão da solution completa compilando com zero warnings.
- **Validação Final**: `dotnet build Resonance.slnx --configuration Release -p:Platform=x64 --warnaserror` e `dotnet test Resonance.slnx --configuration Release -p:Platform=x64 --no-build`.

---

## 4. Gates de Validação (.NET Toolchain)

Para considerar qualquer slice ou a feature concluída, os comandos abaixo devem executar sem falhas:

```powershell
dotnet restore Resonance.slnx
dotnet build Resonance.slnx --configuration Release -p:Platform=x64 --warnaserror
dotnet test Resonance.slnx --configuration Release -p:Platform=x64 --no-build
```
