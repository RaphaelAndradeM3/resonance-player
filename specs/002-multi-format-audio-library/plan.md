# Implementation Plan: 002 — Multi-Format Audio Library

**Branch**: `002-multi-format-audio-library` | **Date**: 2026-09-20 | **Spec**: [spec.md](spec.md)  
**Input**: Feature specification from `specs/002-multi-format-audio-library/spec.md`

---

## Summary

Esta feature consolida e assegura o suporte robusto a múltiplos formatos de áudio no **Resonance**, garantindo consistência total entre o scanner de arquivos, a extração de metadados técnicos (ATL.NET) e a decodificação/reprodução em tempo real (LibVLC).

O objetivo técnico é eliminar qualquer discrepância de formatos, manter `FileExtensions.MusicFileExtensions` como autoridade única (incluindo DSD `.dsf` e `.dff`), isolar com precisão arquivos corrompidos ou de tamanho zero (0 bytes), e garantir o mapeamento adequado de demuxers nativos e hints de formato no player.

---

## Technical Context

* **Language/Version**: C# 13 / .NET 10.0 (preview features ativadas em `Directory.Build.props`)
* **Primary Dependencies**: WinUI 3 (Windows App SDK 1.7+), ATL.NET (`z4kn4fein.atl.core`), LibVLC 4.0 (`VideoLAN.LibVLC.Windows`), Entity Framework Core SQLite
* **Storage**: SQLite local (`resonance.db`), reutilizando a entidade existente `Song`
* **Testing**: xUnit, FluentAssertions, NSubstitute (`tests/Resonance.Core.Tests/`)
* **Target Platform**: Windows 10 (10.0.17763.0+) e Windows 11 (x64 nativo)
* **Project Type**: Desktop Application (WinUI 3 com biblioteca de classes Core)
* **Performance Goals**: Mapeamento e classificação O(1) de extensões de formato; extração de metadados < 50ms por arquivo local; 0 travamentos em arquivos corrompidos
* **Constraints**: Proibição de listas paralelas de extensões; sem dependências de rede para reprodução ou inspeção de formatos locais; preservação da integridade da solução x64
* **Scale/Scope**: Coleções de áudio com múltiplos formatos heterogêneos (MP3, FLAC, WAV, AAC, M4A, OGG, Opus, WMA, AIFF, APE, WavPack, DSD, Musepack)

---

## Constitution Check

*GATE: Avaliado antes da geração do design e reavaliado após a modelagem.*

| Princípio Constitucional | Conformidade | Racional e Evidência |
|---|:---:|---|
| **I. Existing Code Is Truth** | **PASS** | Reutiliza estritamente os componentes existentes (`FileExtensions`, `AtlMetadataService`, `LibraryService`, `Song`, `LibVlcAudioPlayerService`). Não cria novos serviços de áudio ou tabelas paralelas. |
| **II. Vertical Slices (Max 3)** | **PASS** | A feature é dividida em exatamente 3 fatias verticais end-to-end (Slice 1: Unificação da Fonte Canônica; Slice 2: Resiliência e Isolamento de Corrupção; Slice 3: Mapeamento de Reprodução e Paridade). |
| **III. Whole-Solution Validation** | **PASS** | Exige compilação completa da solution (`Resonance.sln`) em Release x64 e aprovação de 100% da suíte de testes (880+ testes) a cada checkpoint. |
| **IV. Local First & Privacy** | **PASS** | Operações de inspeção de formato, demuxing e decodificação ocorrem 100% offline no dispositivo local. |
| **V. Licensing & Toolchain** | **PASS** | .NET 10 x64 nativo; nenhuma dependência externa não-licenciada ou binário de terceiro novo adicionado. |
| **VI. Large Library Resilience** | **PASS** | Arquivos vazios ou corrompidos são identificados precocemente sem alocação desnecessária e sem travar a thread de varredura. |
| **VII. Explicit Boundaries** | **PASS** | Escopo focado estritamente na capacidade multi-formato da biblioteca; inspeção detalhada de tags pertence à Feature 003 e fingerprint acústico à Feature 004. |

---

## Project Structure

### Documentation (this feature)

```text
specs/002-multi-format-audio-library/
├── spec.md              # Especificação funcional com clarificações formalizadas
├── plan.md              # Este plano de implementação
├── research.md          # Decisões de arquitetura, trade-offs e matriz de formatos
├── data-model.md        # Mapeamento da entidade Song, DTO SongFileMetadata e ciclo de vida
├── quickstart.md        # Roteiro executável de testes e validação ponta a ponta
├── checklists/          # Checklists de qualidade
│   └── requirements.md
└── contracts/           # Contratos formais de capability e tratamento de erros
    ├── format-capability-contract.md
    └── format-error-handling-contract.md
```

### Source Code (repository root)

```text
src/
├── Resonance.Core/
│   ├── Constants/
│   │   └── FileExtensions.cs               # Fonte canônica única (adicionando .dff e alinhando formatos)
│   ├── Helpers/
│   │   └── AudioFormatRegistry.cs          # Helper de consulta e categorização de formatos
│   ├── Services/
│   │   └── Implementations/
│   │       ├── AtlMetadataService.cs       # Detecção precoce de arquivos vazios/corrompidos e suporte multi-formato
│   │       └── LibraryService.cs           # Sincronização e expurgo resiliente
└── Resonance.WinUI/
    └── Services/
        └── Implementations/
            └── LibVlcAudioPlayerService.cs # Mapeamento completo de hints e demuxers para todos os formatos

tests/
└── Resonance.Core.Tests/
    ├── FileExtensionsTests.cs              # Testes da lista única de extensões
    ├── AudioFormatRegistryTests.cs         # Testes de categorização e paridade
    ├── AtlMetadataServiceTests.cs          # Testes de extração multi-formato e resiliência a corrupção
    └── LibVlcFormatMappingTests.cs         # Testes de paridade de hints/demuxers
```

---

## Vertical Implementation Slices

### Slice 1: Capability Inventory & Unified Format Registry
* **Meta Imutável Repetida**:
  * *Problema*: A biblioteca deve lidar corretamente com todos os formatos de áudio realmente suportados pelo Nagi/LibVLC e não apenas MP3, evitando divergência entre o que o scanner aceita e o que o player consegue tocar.
  * *Definição de Sucesso*: MP3, FLAC, WAV e demais formatos suportados pela baseline são descobertos, identificados e reproduzidos conforme a capability real da aplicação; arquivos incompatíveis são reportados de forma explícita.
  * *Regra de Ouro*: Não manter listas duplicadas de extensões se a codebase já possui fonte de capability.
* **Escopo**:
  * Atualizar `FileExtensions.MusicFileExtensions` incluindo `.dff` e verificando todos os formatos da baseline.
  * Criar `AudioFormatRegistry` em `Resonance.Core.Helpers` para expor categorização (`IsLossless`, `DisplayName`, `Category`).
  * Atualizar `FileExtensionsTests` validando todos os formatos mandatados.
* **Validação**: Compilação x64 e testes unitários de extensão aprovados.

---

### Slice 2: Resilient Scanning, Identification & Corrupt File Isolation
* **Meta Imutável Repetida**:
  * *Problema*: A biblioteca deve lidar corretamente com todos os formatos de áudio realmente suportados pelo Nagi/LibVLC e não apenas MP3, evitando divergência entre o que o scanner aceita e o que o player consegue tocar.
  * *Definição de Sucesso*: MP3, FLAC, WAV e demais formatos suportados pela baseline são descobertos, identificados e reproduzidos conforme a capability real da aplicação; arquivos incompatíveis são reportados de forma explícita.
  * *Regra de Ouro*: Não manter listas duplicadas de extensões se a codebase já possui fonte de capability.
* **Escopo**:
  * No `AtlMetadataService`: adicionar verificação imediata para arquivos com tamanho 0 bytes (`"EmptyFile"`).
  * No `AtlMetadataService`: garantir captura segura e marcação `ExtractionFailed` para formatos corrompidos ou desconhecidos.
  * No `LibraryService`: certificar que arquivos com falha não são persistidos no banco de dados e são contabilizados no log.
  * Criar testes de resiliência com arquivos de 0 bytes e cabeçalhos truncados.
* **Validação**: Testes de resiliência e integridade do scanner aprovados.

---

### Slice 3: Playback Mapping & End-to-End Parity
* **Meta Imutável Repetida**:
  * *Problema*: A biblioteca deve lidar corretamente com todos os formatos de áudio realmente suportados pelo Nagi/LibVLC e não apenas MP3, evitando divergência entre o que o scanner aceita e o que o player consegue tocar.
  * *Definição de Sucesso*: MP3, FLAC, WAV e demais formatos suportados pela baseline são descobertos, identificados e reproduzidos conforme a capability real da aplicação; arquivos incompatíveis são reportados de forma explícita.
  * *Regra de Ouro*: Não manter listas duplicadas de extensões se a codebase já possui fonte de capability.
* **Escopo**:
  * Alinhar `LibVlcAudioPlayerService.GetAvFormatHint` e `UsesNativeDemuxer` com 100% das extensões de `FileExtensions.MusicFileExtensions`.
  * Adicionar testes de paridade comprovando que zero formatos válidos ficam sem tratamento no player.
  * Whole-Solution Validation completa (`Resonance.sln`).
* **Validação**: 100% da suíte de testes aprovada, 0 erros no build de Release.
