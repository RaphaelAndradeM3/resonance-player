# Implementation Plan: 006 — Metadata Review, Tag Editor & File Update

**Branch**: `006-metadata-review-tag-editor` | **Date**: 2026-09-22 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/006-metadata-review-tag-editor/spec.md`

---

## Summary

A **Feature 006** implementa o fluxo seguro, transparente e controlado de revisão e gravação de metadados no Resonance Player. Atuando diretamente após o enriquecimento online (Feature 005) ou mediante edição manual pelo usuário, o sistema compara os metadados campo a campo (`TagDiffRecord`), permite ligar/desligar alterações individualmente e só aplica qualquer alteração mediante confirmação explícita. A gravação física utiliza o **ATL.NET** em um pipeline atômico de quatro estágios (cópia de trabalho `.tmp`, escrita, validação de integridade de áudio e substituição segura via `File.Replace`), garantindo zero risco de corrupção ou perda de dados em MP3, FLAC, M4A e outros containers. O recurso ainda coordena a liberação de handle caso a música esteja em execução no player, trata arquivos com atributo Somente-Leitura (*Read-Only*) e sincroniza instantaneamente a biblioteca local no SQLite (< 50ms).

---

## Technical Context

**Language/Version**: C# 13 / .NET 10.0  
**Primary Dependencies**:
- Windows App SDK / WinUI 3 (1.7+)
- ATL.NET (`ATL.Track`) para leitura e escrita nativa de tags físicas e imagens embutidas
- CommunityToolkit.Mvvm para bindings reativos de formulário e checkboxes
- Microsoft.EntityFrameworkCore / SQLite (`MusicDbContext`) para persistência imediata  
**Storage**:
- Arquivos de áudio físico no sistema de arquivos local (MP3, FLAC, M4A, OGG, OPUS, WAV)
- Banco de dados SQLite local da biblioteca (`MusicDbContext`)  
**Testing**: xUnit com FluentAssertions e `NSubstitute` em `tests/Resonance.Core.Tests`  
**Target Platform**: Windows 10/11 (x64) Desktop unpackaged  
**Project Type**: WinUI 3 Desktop App + Class Library  
**Performance Goals**:
- Cálculo e renderização do diff em menos de 10ms
- Gravação atômica e verificação de áudio em menos de 100ms para arquivos típicos (<50MB)
- Sincronização do catálogo SQLite em menos de 50ms (SC-002)  
**Constraints**:
- **Regra de Ouro**: Nenhuma gravação automática em disco por resultado de API (SC-003)
- **Zero Corrupção**: 100% de integridade garantida via gravação em arquivo temporário `.tmp` e substituição atômica (SC-001)
- **Escopo Estritamente Single-Track**: Edição faixa a faixa; lote postergado para feature dedicada (FR-007)
- **Coordenação de Playback**: Liberação temporária de stream se a faixa estiver tocando, sem erro de *Sharing Violation* (FR-009)  
**Scale/Scope**: Todas as faixas individuais reproduzíveis na biblioteca local.

---

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Princípio Constitucional | Status | Justificativa / Verificação |
|:---|:---:|:---|
| **I. Existing Code Is the Source of Truth** | **PASS** | Reutiliza ATL.NET (`ATL.Track`), `AtlMetadataService`, `ILibraryWriter` e os modelos `FieldProposalStatus` e `MetadataProvenance` da Feature 005. Zero bibliotecas de áudio redundantes. |
| **II. Vertical Slices, Not Microtasks** | **PASS** | Estruturado rigorosamente em no máximo 3 fatias verticais ponta a ponta (Slice 1: Diff & Plan; Slice 2: Safe Tag Write Engine; Slice 3: WinUI Tag Editor Dialog). |
| **III. Whole-Solution Validation** | **PASS** | Validação mandatória: `dotnet restore Resonance.slnx`, `dotnet build Resonance.slnx --configuration Release -p:Platform=x64 --warnaserror`, `dotnet test Resonance.slnx --configuration Release --no-build`. |
| **IV. Local-First and Privacy-First** | **PASS** | 100% da edição, diff e gravação ocorrem localmente na máquina do usuário sem envio de arquivos de áudio para servidores externos. |
| **V. Licensing and Provider Compliance** | **PASS** | Preserva a licença GPLv3 do projeto; utiliza ATL.NET já homologado; zero inclusão de binários ou componentes proprietários. |
| **VI. Large Library Resilience** | **PASS** | Gravação isolada por faixa com cancelamento seguro; preservação integral do arquivo original em caso de falta de espaço em disco ou erro de permissão. |
| **VII. Explicit Boundaries** | **PASS** | Segue rigorosamente o fluxo constitucional: `Review -> Diff -> Explicit confirmation -> Apply`. UI não faz I/O direto; serviços mantêm responsabilidades distintas. |

---

## Project Structure

### Documentation (this feature)

```text
specs/006-metadata-review-tag-editor/
├── spec.md              # Especificação com clarificações de escopo integradas
├── plan.md              # Este plano de implementação
├── research.md          # Decisões de arquitetura (ATL, atomicidade, locks)
├── data-model.md        # Entidades (TagDiffRecord, TagWritePlan, TagWriteResult)
├── quickstart.md        # Guia de validação automatizada e cenários manuais
├── contracts/           # Contratos de interfaces e DTOs
│   ├── ITagDiffService.cs.md
│   ├── ITagWriterService.cs.md
│   └── TagReviewDtos.md
└── checklists/          # Checklist de qualidade
    └── requirements.md
```

### Source Code (repository layout)

```text
src/
├── Resonance.Core/
│   ├── Models/
│   │   ├── TagDiffRecord.cs              # Registro de comparação campo a campo com seleção
│   │   ├── TagWritePlan.cs               # Plano imutável validado de gravação de tags
│   │   ├── TagWriteResult.cs             # Resultado e telemetria da substituição atômica
│   │   └── EditableTagModel.cs           # Modelo de formulário para edição manual
│   └── Services/
│       ├── Abstractions/
│       │   ├── ITagDiffService.cs        # Contrato para geração de diff e montagem de plano
│       │   └── ITagWriterService.cs      # Contrato de gravação atômica segura e verificação
│       └── Implementations/
│           ├── TagDiffService.cs         # Implementação de diff de texto, números e capa
│           └── SafeTagWriterService.cs   # Implementação com ATL.Track, .tmp, lock handling e SQLite sync
│
├── Resonance.WinUI/
│   ├── Dialogs/
│   │   ├── TagEditorDialog.xaml          # Diálogo modal WinUI unificado (ContentDialog)
│   │   └── TagEditorDialog.xaml.cs       # Code-behind e inicialização de XamlRoot
│   └── ViewModels/
│       ├── TagEditorViewModel.cs         # ViewModel reativo do diálogo (manual + diff online)
│       └── TrackInspectorViewModel.cs    # Atualização de AdvanceToTagReviewAsync para abrir o diálogo
│
tests/
└── Resonance.Core.Tests/
    └── Services/
        ├── TagDiffServiceTests.cs        # Testes de matriz de diff e seleção de campos
        └── SafeTagWriterServiceTests.cs  # Testes de round-trip, atomicidade, read-only e rollback
```

---

## Vertical Implementation Slices

### Slice 1: Metadata Diff Engine & Write Plan (Core)
- **Meta Imutável:**
  > Problema: O usuário precisa comparar o estado atual das tags físicas com sugestões externas ou edições manuais e ligar/desligar cada campo individualmente.  
  > Definição de Sucesso: O mecanismo calcula o diff de texto, números e imagem de capa, gerando um `TagWritePlan` seletivo que respeita as escolhas do usuário.  
  > Regra de Ouro: Nenhuma escrita sem autorização explícita; zero código redundante de diff.
- **Escopo Ponta a Ponta:**
  - Criar `TagDiffRecord`, `TagWritePlan`, `TagWriteResult` e `EditableTagModel` em `Resonance.Core.Models`.
  - Criar interface `ITagDiffService` e implementação `TagDiffService`.
  - Implementar suporte a conversão direta a partir de `EnrichmentProposal` (Feature 005) e a partir de tags manuais.
  - Testes unitários com matriz completa de diff (valores idênticos, modificados, nulos, novas capas).
- **Critério de Saída**: Testes de diff compilando e passando em `Resonance.Core.Tests`.

---

### Slice 2: Safe Tag Write Engine & Persistence Sync (Core & Infra)
- **Meta Imutável:**
  > Problema: Gravar alterações no arquivo físico sem risco de corrupção em caso de queda de energia ou lock de processo, e manter a biblioteca sincronizada.  
  > Definição de Sucesso: Gravação atômica via arquivo temporário no mesmo diretório, validação pós-escrita com ATL, substituição segura (`File.Replace`), suporte a arquivo Read-Only, pausa suave se em playback e atualização imediata do SQLite em < 50ms.  
  > Regra de Ouro: Nenhuma escrita in-place destrutiva; arquivo original sempre preservado se houver falha.
- **Escopo Ponta a Ponta:**
  - Criar interface `ITagWriterService` e implementação `SafeTagWriterService`.
  - Implementar pipeline atômico de escrita: `.tmp.<guid>` -> `track.Save()` -> `ValidateAudioFileIntegrityAsync()` -> desativação temporária de `ReadOnly` -> `File.Replace()`.
  - Implementar coordenação com `IMusicPlaybackService` para descarregar/pausar o stream se o arquivo estiver tocando e restaurar na mesma posição.
  - Implementar sincronização imediata pós-escrita: reler com `_metadataService.ExtractMetadataAsync` e chamar `_libraryWriter.UpdateSongAsync`.
  - Registrar serviços no container de DI em `App.xaml.cs`.
  - Testes de integração em `SafeTagWriterServiceTests`: gravação round-trip em arquivos reais de teste (MP3, FLAC), simulação de erro de I/O com verificação de rollback, e teste de arquivo Read-Only.
- **Critério de Saída**: Testes de gravação atômica passando sem falhas de integridade.

---

### Slice 3: WinUI Tag Editor Dialog & Regression (UI & Integração Final)
- **Meta Imutável:**
  > Problema: O usuário precisa de uma interface rica, fluida e clara para revisar o diff de propostas online ou editar campos manualmente antes de salvar.  
  > Definição de Sucesso: `TagEditorDialog` em WinUI exibe formulário completo com visão comparativa Antes vs Sugerido quando originado da Feature 005, checkboxes de seleção campo a campo, preview de capa, botão "Gravar Alterações" com confirmação, conectado ao botão "Revisar e Gravar" do Track Inspector.  
  > Regra de Ouro: Respeitar a estética moderna do Resonance e diretrizes de thread UI (DispatcherQueue).
- **Escopo Ponta a Ponta:**
  - Criar `TagEditorViewModel` com comandos para alternar campos, selecionar/desmarcar todos, e invocar o `ITagWriterService`.
  - Criar `TagEditorDialog.xaml` e `.xaml.cs` como `ContentDialog` com tema e estilos padrão do Resonance.
  - Atualizar `TrackInspectorViewModel.AdvanceToTagReviewAsync` para abrir o `TagEditorDialog` passando a proposta atual.
  - Adicionar opção de menu de contexto nas listas de faixas ("Editar Tags") para acionar o modo manual.
  - Executar os gates de validação completos da solução (`restore`, `build --warnaserror`, `test`).
- **Critério de Saída**: Solution inteira compilando com Release x64 e todos os testes passando.

---

## Complexity Tracking

> *Nenhuma violação constitucional identificada. Todas as regras e limites foram rigorosamente cumpridos.*

| Violação | Por que é necessária | Alternativa mais simples rejeitada porque |
|:---|:---|:---|
| Nenhuma | N/A | N/A |
