# Research: Feature 002 — Multi-Format Audio Library

**Branch**: `002-multi-format-audio-library` | **Date**: 2026-09-20  
**Feature Spec**: [spec.md](spec.md)  

---

## 1. Contexto & Desafios Técnicos

O objetivo central desta feature é unificar e blindar o suporte a múltiplos formatos de áudio no **Resonance**, garantindo paridade total entre:
1. **Descoberta de Arquivos**: O que o scanner reconhece como arquivo de áudio.
2. **Extração de Metadados**: O que o ATL.NET lê com sucesso (tags, bitrate, sample rate, canais).
3. **Pipeline de Reprodução**: O que o LibVLC decodifica e reproduz sem falhas de demuxer.

Atualmente, extensões como `.dff` (variante comum de DSD junto com `.dsf`) não estavam listadas em `FileExtensions.MusicFileExtensions`. Além disso, o mapeamento de hints do LibVLC em `LibVlcAudioPlayerService.GetAvFormatHint` precisa cobrir rigorosamente todos os formatos suportados pelo scanner, evitando listas paralelas e discrepâncias.

---

## 2. Decisões Arquiteturais e Racional Técnico

### Decisão 1: Fonte Única da Verdade para Extensões de Áudio
- **Decisão**: Manter `Resonance.Core.Constants.FileExtensions.MusicFileExtensions` como a única autoridade para formatos suportados em toda a aplicação, adicionando a extensão DSD `.dff` ao conjunto.
- **Racional**:
  - Evita *drift* arquitetural onde o scanner aceita extensões que o player desconhece, ou vice-versa.
  - O formato DSD é amplamente distribuído tanto em `.dsf` (Sony) quanto em `.dff` (Philips DSDIFF). Ambos são suportados pelo ATL.NET e pelo LibVLC 4.0.
- **Alternativas consideradas**:
  - *Listas separadas por componente*: Rejeitado categoricamente pela Regra de Ouro ("Não manter listas duplicadas de extensões se a codebase já possui fonte de capability").
  - *Consultar ATL.NET dinamicamente em tempo de execução para cada arquivo*: Rejeitado por impacto severo de performance no I/O do scanner (testar centenas de milhares de arquivos com reflexão/instanciação do ATL tornaria a enumeração lenta).

---

### Decisão 2: Unificação do Mapeamento LibVLC (`GetAvFormatHint` e `UsesNativeDemuxer`)
- **Decisão**: Alinhar o método `LibVlcAudioPlayerService.GetAvFormatHint` e `UsesNativeDemuxer` para cobrir 100% das extensões presentes em `FileExtensions.MusicFileExtensions`.
  - `.dff` e `.dsf` mapeados explicitamente para o demuxer DSD (`"dsf"`/`"dff"` ou auto-probe).
  - `.wv` (WavPack), `.ape` (Monkey's Audio), `.aiff`, `.wma`/`.asf`, `.mp3`, `.flac`, `.wav`, `.aac`, `.m4a`/`.m4b`/`.mp4` mapeados com seus respectivos hints e demuxers.
  - `.opus`, `.ogg`, `.oga`, `.webm` continuam usando o demuxer nativo do LibVLC (`UsesNativeDemuxer`).
- **Racional**: O LibVLC 4.0 no Windows possui excelente suporte embutido a codecs via ffmpeg e demuxers nativos, desde que a extensão não seja enviada com hint incorreto.
- **Alternativas consideradas**:
  - *Deixar o LibVLC fazer auto-probe para todos os formatos*: Rejeitado porque contêineres MP4/M4A e streams WMA/ASF requerem hints explícitos para evitar atrasos na abertura do stream ou falha de demuxing.

---

### Decisão 3: Tratamento de Arquivos Inválidos, Vazios (0-byte) e Corrompidos
- **Decisão**: Fortalecer a detecção precoce no `AtlMetadataService`:
  1. Arquivos com tamanho zero (`Length == 0`) são imediatamente marcados com `ExtractionFailed = true` e `ErrorMessage = "EmptyFile"`.
  2. Arquivos onde ATL falha em ler o formato de áudio (`AudioFormat.Readable == false` ou formato `"Unknown"`) recebem `ExtractionFailed = true` e `ErrorMessage = "CorruptFile"` ou `"UnsupportedFormat"`.
  3. No `LibraryService`, arquivos com `ExtractionFailed` permanente não são gravados no banco SQLite, e um resumo agregado de falhas é logado, garantindo 0 falhas não-tratadas na varredura.
- **Racional**: Coleções de usuários reais frequentemente possuem downloads incompletos ou arquivos corrompidos. O scanner nunca deve abortar a varredura nem quebrar a UI por causa de uma faixa com cabeçalho truncado.
- **Alternativas consideradas**:
  - *Salvar arquivos corrompidos como músicas "desconhecidas"*: Rejeitado porque criaria faixas impossíveis de reproduzir que gerariam erros subsequentes no player ao clicar em "Play All".

---

### Decisão 4: Matriz de Capacidade de Formatos (`AudioFormatRegistry`)
- **Decisão**: Criar um helper leve e estático em `Resonance.Core.Helpers.AudioFormatRegistry` (ou estender `FileExtensions`) que expõe métodos de categorização do formato:
  - `IsLossless(string extension)` (ex: FLAC, WAV, AIFF, APE, WV, DSD)
  - `GetFormatDisplayName(string extension)` (ex: "FLAC", "MP3", "WavPack", "DSD")
  - `IsSupported(string extension)`
- **Racional**: Facilita a exibição de badges na UI e testes automatizados sem duplicar lógica em ViewModels.

---

## 3. Matriz de Compatibilidade de Formatos (Baseline Resonance)

| Formato | Extensão | Leitor de Tags (ATL) | Player (LibVLC) | Categoria | Observações |
|:---|:---:|:---:|:---:|:---:|:---|
| **MP3** | `.mp3` | ID3v1, ID3v2 | LibVLC demuxer | Lossy | Padrão da indústria |
| **FLAC** | `.flac` | Vorbis Comment | LibVLC native | Lossless | Suporta até 192kHz/24-bit |
| **WAV** | `.wav` | RIFF / ID3v2 | LibVLC native | Lossless / PCM | PCM Linear não comprimido |
| **AAC** | `.aac` | ADTS / Raw | LibVLC ffmpeg | Lossy | Advanced Audio Coding |
| **M4A / M4B** | `.m4a`, `.m4b` | QuickTime / MP4 | LibVLC mp4 hint | Lossy / ALAC | AAC ou ALAC em contêiner MP4 |
| **OGG / Vorbis** | `.ogg`, `.oga` | Vorbis Comment | Native demuxer | Lossy | Contêiner Ogg |
| **Opus** | `.opus` | Opus Comment | Native demuxer | Lossy (High-eff) | Codec moderno IETF |
| **WMA / ASF** | `.wma`, `.asf` | ASF / WMA | LibVLC asf hint | Lossy / Lossless | Windows Media Audio |
| **AIFF** | `.aiff` | AIFF chunks | LibVLC aiff hint | Lossless / PCM | Padrão Apple uncompressed |
| **Monkey's Audio**| `.ape` | APEv1, APEv2 | LibVLC ape hint | Lossless | Alta taxa de compressão |
| **WavPack** | `.wv` | APEv2 / WavPack | LibVLC wv hint | Lossless / Hybrid | Suporta 32-bit float |
| **DSD** | `.dsf`, `.dff` | ID3v2 (DSF) | LibVLC dsf hint | Hi-Res / 1-bit | Direct Stream Digital |
| **Musepack** | `.mpc`, `.mpp` | APEv2 | LibVLC content probe| Lossy | SV7 e SV8 |
| **WebM Audio** | `.webm` | Matroska / WebM | Native demuxer | Lossy | Vorbis/Opus em WebM |
