# Architecture & Capability Inventory Contract

**Feature**: `000-nagi-baseline-audit`  
**Contract Version**: 1.0.0  
**Scope**: Catálogo formal de componentes auditados na baseline Nagi, vinculando interfaces existentes às suas implementações e definindo regras estritas de não-duplicação.

---

## 1. Inventory Catalog

A tabela abaixo lista os subsistemas oficiais do Nagi. Conforme o Princípio I e VII da Constituição, **agentes não podem criar novos services, repositories ou pipelines paralelos para essas responsabilidades.**

| Subsistema / Capability | Interface Primária (Abstração) | Implementação Concreta | Projeto / Camada | Ciclo de Vida DI | Reutilização Obrigatória |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Playback Engine** | `IAudioPlayer` | `LibVlcAudioPlayerService` | `Nagi.WinUI` | `Singleton` | **MANDATÓRIO**: Motor de áudio LibVLC 4.0. Não substituir por NAudio/MediaEngine. |
| **Playback Coordinator** | `IMusicPlaybackService` | `MusicPlaybackService` | `Nagi.Core` | `Singleton` | **MANDATÓRIO**: Orquestra fila, estado, histórico e eventos de reprodução. |
| **Library Scanner** | `ILibraryScanner` | `LibraryService` | `Nagi.Core` | `Singleton` | **MANDATÓRIO**: Ponto de entrada da Feature 001. Não criar segundo scanner. |
| **Library Reader** | `ILibraryReader` | `LibraryService` | `Nagi.Core` | `Singleton` | **MANDATÓRIO**: Consultas de faixas, álbuns, artistas e pastas. |
| **Library Writer** | `ILibraryWriter` | `LibraryService` | `Nagi.Core` | `Singleton` | **MANDATÓRIO**: Mutação e persistência de dados de biblioteca. |
| **Database Context** | `MusicDbContext` | `MusicDbContext` | `Nagi.Core` | `Pooled / Factory` | **MANDATÓRIO**: Único banco de dados SQLite / EF Core da aplicação. |
| **Metadata Engine** | `IMetadataService` | `AtlMetadataService` | `Nagi.Core` | `Singleton` | **MANDATÓRIO**: Leitura/escrita de tags via ATL. Não usar TagLibSharp. |
| **Lyrics Parser/Service**| `ILrcService` | `LrcService` | `Nagi.Core` | `Singleton` | **MANDATÓRIO**: Manipulação de letras locais (.lrc, tags embutidas). |
| **Online Lyrics Provider**| `IOnlineLyricsService` | `LrcLibService` | `Nagi.Core` | `Singleton` | **MANDATÓRIO**: Provider de letras LRCLIB. Manter opcional e offline-first. |
| **Equalizer / DSP** | `LibVlcAudioPlayerService` | `LibVlcAudioPlayerService` | `Nagi.WinUI` | `Singleton` | **MANDATÓRIO**: 10 bandas de EQ com pregain e presets. |
| **ReplayGain Engine** | `IReplayGainService` | `ReplayGainService` | `Nagi.WinUI` | `Singleton` | **MANDATÓRIO**: Cálculo e aplicação de ganho de volume ReplayGain. |
| **PCM Extractor** | `IPcmExtractor` | `FFmpegPcmExtractor` | `Nagi.WinUI` | `Singleton` | **REUTILIZÁVEL**: Extração de PCM via FFmpeg para análise e ReplayGain. |
| **Settings / Config** | `ISettingsService` | `SettingsService` | `Nagi.WinUI` | `Singleton` | **MANDATÓRIO**: Repositório central de configurações tipadas. |
| **Navigation** | `INavigationService` | `NavigationService` | `Nagi.WinUI` | `Singleton` | **MANDATÓRIO**: Navegação Fluent entre páginas WinUI 3. |

---

## 2. Injeção de Dependência (DI) Rules

1. Todo novo recurso ou extensão deve ser registrado no método `ConfigureServices` em `src\Nagi.WinUI\App.xaml.cs`.
2. Se uma nova interface for estendida (ex.: `IAudioFingerprintService` para a Feature 004), ela deve se integrar ao container existente sem criar um segundo container ou service locator paralelo.
3. Ciclos de vida devem respeitar `Singleton` para serviços de infraestrutura e gerenciamento de estado de reprodução.
