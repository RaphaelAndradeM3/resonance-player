# Research: Feature 000 — Baseline / Audit do Fork Nagi

**Feature**: `000-nagi-baseline-audit`  
**Date**: 2026-09-20  
**Status**: Completed  

---

## 1. Upstream Repository and Commit SHA Baseline

### Decision
Fixar como baseline o repositório oficial do Nagi no GitHub:
- **Upstream URL**: `https://github.com/Anthonyy232/Nagi`
- **Release Tag Base**: `2.3.0` (commit `88b9790e7fd176841dc055a51b61f07b5011f849`)
- **Upstream Tracking Commit (HEAD)**: `e242b8b0ae406144aa94b3b40a8e7815d8143cb4` (Merge PR #185: fix/benchmark-ci-validation)

### Rationale
A tag `2.3.0` representa o marco estável mais recente lançado pelo upstream, contendo toda a infraestrutura moderna de UI em WinUI 3, LibVLCSharp, ATL, SQLite/EF Core 10 e suporte a letras sincronizadas. O commit HEAD `e242b8b0` adiciona validações de CI e correções de benchmarks sem alterar a arquitetura essencial do produto. Fixar a referência no tag 2.3.0 com rastreabilidade ao HEAD garante total reprodutibilidade contra o repositório original.

### Alternatives Considered
- *Clonar a partir de branch de desenvolvimento sem tag*: Rejeitado por falta de garantia de estabilidade e ausência de release notes oficiais.
- *Usar versão anterior (v2.2.0 ou v2.1.0)*: Rejeitado porque as versões anteriores utilizavam .NET 9 ou versões prévias do Windows App SDK, enquanto a v2.3.0 já está consolidada no .NET 10.

---

## 2. Target Framework, Toolchain & Build Platform Flags

### Decision
- **SDK .NET**: .NET 10.0 (SDK 10.0.401 instalado localmente, com `global.json` especificando `version: 10.0.300`, `rollForward: latestFeature`, `allowPrerelease: true`).
- **Linguagem C#**: C# 13/preview (`<LangVersion>preview</LangVersion>`).
- **Target Frameworks**:
  - `net10.0-windows10.0.26100` (Projeto `Resonance.WinUI` com Windows App SDK 2.4.0 e BuildTools 10.0.28000.2705).
  - `net10.0` (Projetos `Resonance.Core`, `Resonance.Core.Tests`, `ResonanceAppFunctions`).
- **Arquitetura de Plataforma Obrigatória**: `x64` (ou `ARM64`).
  - Flags de compilação da Solution: `dotnet build Resonance.sln --configuration Release -p:Platform=x64`
  - Restauração de pacotes: `dotnet restore Resonance.sln -p:Platform=x64`
  - Execução de testes: `dotnet test tests\Resonance.Core.Tests\Resonance.Core.Tests.csproj --configuration Release --no-build`

### Rationale
Projetos WinUI 3 / Windows App SDK possuem restrições arquiteturais que impedem a compilação no modo genérico `Any CPU`. O target nativo é x64 ou ARM64 devido às dependências C++ do Windows App Runtime e do LibVLC. O parâmetro `-p:Platform=x64` é mandatório para que o MSBuild selecione a configuração correta de packaging e runtime native binaries.

### Alternatives Considered
- *Compilar com Any CPU*: Falha imediata no projeto `Resonance.WinUI` com erro do Windows App SDK indicando que a plataforma `AnyCPU` não é suportada para projetos com packaging ou runtime nativo.
- *Usar apenas o Visual Studio IDE*: Inviável para automação e gates do Spec Kit, que requerem execução via CLI na toolchain oficial do .NET.

---

## 3. Audio Engine & DSP Architecture

### Decision
Reutilizar o pipeline existente baseado em:
- **Decodificador/Player**: `LibVLCSharp` v4.0.0-alpha (`VideoLAN.LibVLC.Windows` v4.0.0-alpha).
- **Interface Principal**: `Resonance.Core.Services.Abstractions.IAudioPlayer` e `Resonance.Core.Services.Abstractions.IMusicPlaybackService`.
- **Implementação WinUI**: `Resonance.WinUI.Services.Implementations.LibVlcAudioPlayerService` e `Resonance.Core.Services.Implementations.MusicPlaybackService`.
- **Processamento de PCM & ReplayGain**: `IPcmExtractor` (`FFmpegPcmExtractor`) e `IReplayGainService` (`ReplayGainService` / `LoudnessMeter`).
- **Equalizador**: Equalizador nativo do LibVLC de 10 bandas gerenciado via `LibVlcAudioPlayerService.SetEqualizer` e modelos `EqualizerPreset`, `EqualizerSettings`.

### Rationale
A Constituição do Projeto (Princípio I e VII) proíbe expressamente a criação de pipelines de áudio concorrentes. O Nagi já possui uma integração madura com LibVLC 4.0, suportando reprodução contínua, ReplayGain com normalização de ganho e equalizador de 10 bandas integrado diretamente ao motor VLC.

### Alternatives Considered
- *Substituir LibVLC por NAudio ou MediaFoundation*: Rejeitado por violar o Princípio I da Constituição, perder suporte multiplataforma a formatos exóticos (DSD, WavPack, APE) e demandar reescrita do zero de todo o motor.
- *Adicionar pipeline paralelo de visualização FFT durante a baseline*: Rejeitado porque a baseline não deve adicionar código de produto; a visualização FFT deve ser implementada no slot da Feature 009 conectando-se aos ganchos de áudio existentes.

---

## 4. Metadata Architecture & Tag Engine

### Decision
Preservar o leitor/escritor de metadados baseado na **ATL (Audio Tools Library)**:
- **Pacote**: `z440.atl.core` v7.16.0.
- **Interface Principal**: `Resonance.Core.Services.Abstractions.IMetadataService`.
- **Implementação**: `Resonance.Core.Services.Implementations.AtlMetadataService`.
- **Formatos suportados**: ID3v1, ID3v2.2-2.4, Vorbis Comments (FLAC, Ogg), MP4/AAC atoms, APEv1/v2, RIFF/WAV, AIFF, WavPack.

### Rationale
A biblioteca ATL é extremamente rápida, suporta todos os formatos exigidos pelo PRD (MP3, FLAC, AAC, OGG, WAV, AIFF, APE, DSD, M4A, WMA, WavPack) e permite leitura de streams de imagem de capa e metadados técnicos avançados (sample rate, bit depth, bitrate mode).

### Alternatives Considered
- *TagLibSharp*: Rejeitado porque ATL já está integrada, possui performance superior comprovada no Nagi e evita dependência redundante.

---

## 5. Library Scanner & Persistence Architecture

### Decision
- **Persistência**: SQLite via Entity Framework Core 10.0.11 (`Microsoft.EntityFrameworkCore.Sqlite`).
- **DbContext**: `Resonance.Core.Data.MusicDbContext` com factory `DesignTimeDbContextFactory`.
- **Scanner**: `Resonance.Core.Services.Abstractions.ILibraryScanner`, `ILibraryReader`, `ILibraryWriter` implementados em `Resonance.Core.Services.Implementations.LibraryService`.
- **Transações & Concorrência**: SQLite com WAL (Write-Ahead Logging) habilitado via interceptors de conexão.

### Rationale
O `LibraryService` já centraliza leitura, escrita, indexação e busca de faixas e pastas. A Feature 001 estenderá essa estrutura para garantir recursividade irrestrita, suporte a múltiplas raízes e resiliência a links simbólicos e pastas bloqueadas, sem duplicar tabelas ou criar um segundo banco de dados.

### Alternatives Considered
- *Substituir EF Core por Dapper ou LiteDB*: Rejeitado por violar a Constituição (não recriar arquitetura existente).
- *Criar banco de dados separado para metadados enriquecidos*: Rejeitado; todas as entidades de áudio devem residir no esquema unificado do `MusicDbContext`.

---

## 6. Lyrics Engine Architecture

### Decision
- **Parser de Letras Sincronizadas**: `ModernLrc` v1.2.0.
- **Serviço de Letras**: `Resonance.Core.Services.Abstractions.ILrcService` / `LrcService`.
- **Provedores Remotos Existentes**:
  - `LrcLibService` (implementa `IOnlineLyricsService` consumindo a API LRCLIB).
  - `NetEaseLyricsService` (implementa `INetEaseLyricsService`).
- **Política de Local-First**: O serviço busca primeiramente arquivos `.lrc`同名 (mesmo nome na mesma pasta) ou tags USLT/SYLT embutidas no arquivo de áudio. Provedores remotos são estritamente opcionais.

### Rationale
Atende perfeitamente ao Princípio IV da Constituição (Local-First). A Feature 007 aproveitará integralmente o contrato de `ILrcService` e `IOnlineLyricsService`, refinando a apresentação visual e o cache local.

---

## 7. Testing Platform & Test Suite Architecture

### Decision
- **Test Runner**: `Microsoft.Testing.Platform` (configurado em `global.json` com `test.runner: "Microsoft.Testing.Platform"`).
- **Framework de Testes**: `xunit.v3` v4.0.0.
- **Asserções**: `FluentAssertions` v8.10.0.
- **Mocks**: `NSubstitute` v6.2.0 com `NSubstitute.Analyzers.CSharp`.
- **Cobertura**: `coverlet.MTP` v10.0.1.
- **Projeto de Testes**: `tests\Resonance.Core.Tests\Resonance.Core.Tests.csproj`.

### Rationale
A base upstream adotou o moderno runner MTP (Microsoft.Testing.Platform) com xUnit v3, proporcionando execução ultrarrápida de testes de unidade sem sobrecarga de adaptadores VSTest legados.

### Baseline Test Execution Findings (Comprovado em Execução Real)
- **Total de Testes**: 845 testes automatizados.
- **Aprovados**: 841 testes (99,5% de taxa de aprovação).
- **Tempo de Execução**: ~12 segundos na toolchain local.
- **Falhas de Baseline Auditadas (4 testes)**:
  1. `AtlMetadataServiceTests.ExtractMetadataAsync_WithMinimalMetadata_ProvidesSaneDefaults`: Falha por divergência de localização cultural (`CultureInfo.CurrentUICulture` em `pt-BR` retorna `"Desconhecido Artista"` onde a asserção espera o literal inglês `"Unknown Artist"`).
  2. `LastFmScrobblerServiceTests.UpdateNowPlayingAsync_WithMinimalSongData_SendsCorrectParameters`: Mesma causa de localização cultural (`"Desconhecido Artista"` vs `"Unknown Artist"`).
  3. `LrcLibServiceTests.GetLyricsAsync_WithUnknownArtistPlaceholder_SkipsStrictMatch`: Falha decorrente do placeholder localizado na busca de letras.
  4. `SmartPlaylistQueryBuilderTests.BuildQuery_SortByArtistAsc_ReturnsSortedWithSecondarySort`: Divergência de ordenação alfabética secundária influenciada pela collation local de cultura.
- **Tratamento Recomendado**: Conforme o contrato da Feature 000, essas 4 falhas de ambiente local devem ser catalogadas como comportamento pré-existente documentado. Para executá-los em modo inglês sem alterar código de produto, pode-se rodar com `DOTNET_CLI_UI_LANGUAGE=en` ou fixação de cultura.

---

## 8. Licenciamento e Validação de Build SixLabors.ImageSharp

### Decision
Documentar a exigência de licenciamento do pacote `SixLabors.ImageSharp` 4.1.1:
- Em modo `Debug`, o MSBuild compila normalmente (`ContinueOnError=true` com warning).
- Em modo `Release`, o target de validação de licença do ImageSharp falha se não houver `$(SixLaborsLicenseKey)` ou declaração de licença de código aberto.
- O upstream do Nagi injeta `SixLaborsLicenseKey: ${{ secrets.SIXLABORS_LICENSE_KEY }}` via GitHub Actions.

### Rationale
A Constituição (Princípio V) exige revisão e documentação estrita de termos de dependências de terceiros.
