# Contract: CTR-FMT-001 & CTR-FMT-002 — Format Capability & Playback Mapping

**Feature**: 002 — Multi-Format Audio Library  
**Date**: 2026-09-20  
**Status**: Formal Contract  

---

## 1. CTR-FMT-001: Fonte Única de Extensões de Áudio Suportadas

O conjunto `Resonance.Core.Constants.FileExtensions.MusicFileExtensions` é a autoridade máxima e central para todos os componentes do sistema (Scanner, MetadataService, PlaybackService, FileAssociation).

### 1.1 Assinatura Formal
```csharp
namespace Resonance.Core.Constants;

public static class FileExtensions
{
    public static readonly HashSet<string> MusicFileExtensions;
}
```

### 1.2 Regras do Contrato:
1. **Completude da Baseline**: DEVE conter no mínimo:
   - `.mp3` (MPEG Layer 3)
   - `.flac` (Free Lossless Audio Codec)
   - `.wav` (Waveform Audio File Format)
   - `.aac` (Advanced Audio Coding)
   - `.m4a`, `.m4b`, `.mp4` (MPEG-4 Audio / ALAC)
   - `.ogg`, `.oga` (Ogg Vorbis)
   - `.opus` (Opus Audio)
   - `.wma`, `.asf` (Windows Media Audio)
   - `.aiff` (Audio Interchange File Format)
   - `.ape` (Monkey's Audio)
   - `.wv` (WavPack)
   - `.dsf`, `.dff` (Direct Stream Digital / DSD)
   - `.mpc`, `.mpp` (Musepack)
   - `.webm` (WebM Audio)
2. **Imutabilidade e Semântica de Comparação**:
   - As consultas DEVEM ser insensíveis a maiúsculas/minúsculas (`StringComparer.OrdinalIgnoreCase`).
   - Todos os membros DEVEM iniciar com o caractere ponto `.` minúsculo por convenção.
3. **Proibição de Listas Paralelas**:
   - Nenhum ViewModel, Service ou Helper pode declarar listas privadas ou duplicadas de formatos de música.

---

## 2. CTR-FMT-002: Contrato de Hints e Demuxers para LibVLC

O reprodutor `LibVlcAudioPlayerService` deve mapear de maneira determinística os parâmetros necessários para abertura de cada arquivo contido em `FileExtensions.MusicFileExtensions`.

### 2.1 Mapeamento Obrigatório:

```csharp
public static class FormatPlaybackContract
{
    public static bool UsesNativeDemuxer(string extension)
    {
        return extension is ".opus" or ".ogg" or ".oga" or ".webm";
    }

    public static string? GetAvFormatHint(string extension)
    {
        return extension switch
        {
            ".mp3" => "mp3",
            ".flac" => "flac",
            ".wav" => "wav",
            ".aac" => "aac",
            ".m4a" or ".m4b" or ".mp4" or ".m4v" => "mp4",
            ".wma" or ".asf" => "asf",
            ".aiff" => "aiff",
            ".ape" => "ape",
            ".dsf" or ".dff" => "dsf",
            ".mpc" or ".mpp" => null, // Content probing
            ".wv" => "wv",
            ".mpeg" or ".mpg" or ".mpe" => "mpeg",
            _ => null
        };
    }
}
```

### 2.2 Regras do Contrato:
1. Nenhuma extensão presente em `MusicFileExtensions` deve lançar exceção ao ser aberta pelo player.
2. Contêineres de múltiplos fluxos ou codecs complexos (ex: Musepack) devem utilizar sondagem de conteúdo (`null` hint).
