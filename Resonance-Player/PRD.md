# PRD.md — Resonance

## 1. Product Statement

Resonance é uma evolução GPLv3 do Nagi para Windows, focada em grandes bibliotecas
locais, inspeção de áudio, enriquecimento de metadados, equalização avançada e visualização
FFT.

A aplicação permanece local-first e offline-capable.

## 2. Product Goals

### G1 — Recursive Library

Selecionar uma pasta raiz descobre todos os arquivos suportados em subpastas elegíveis.

### G2 — Multiple Roots

Múltiplos locais de música podem coexistir.

### G3 — Metadata Transparency

O usuário distingue metadados locais de dados enriquecidos externamente.

### G4 — Music Identification

Músicas desconhecidas ou mal tagueadas podem ser identificadas opcionalmente por fingerprint.

### G5 — Advanced Equalizer

Oferecer presets úteis e personalizáveis sobre o EQ de 10 bandas existente.

### G6 — Audio Visualization

Oferecer FFT/spectrum analyzer em tempo real.

### G7 — Local-First Lyrics

Priorizar letras embutidas/locais e usar provider remoto apenas como enriquecimento opcional.

## 3. Out of Scope

Inicialmente não implementar:

- streaming comercial;
- DRM;
- download de música;
- plugins binários do Winamp;
- clone exato do Winamp;
- cloud library;
- DAW/editor profissional;
- escrita destrutiva automática em massa de tags;
- redistribuição de base de letras de terceiros.

## 4. User Scenario — Root Folder

Usuário seleciona:

```text
D:\Music
```

Existem:

```text
D:\Music\a.mp3
D:\Music\Rock\b.flac
D:\Music\Rock\Pink Floyd\c.flac
D:\Music\Jazz\Miles Davis\d.wav
```

Todos os arquivos suportados devem aparecer na biblioteca.

Nenhuma subpasta precisa ser cadastrada.

## 5. Functional Requirements — Library

**FR-LIB-001** O sistema MUST suportar uma ou mais Library Roots.

**FR-LIB-002** Uma Library Root MUST ser varrida recursivamente por padrão.

**FR-LIB-003** Músicas em subpastas MUST ser indexadas automaticamente.

**FR-LIB-004** Raízes sobrepostas MUST NOT produzir tracks duplicadas.

**FR-LIB-005** Um arquivo inválido MUST NOT abortar todo o scan.

**FR-LIB-006** Um diretório inacessível MUST NOT abortar todo o scan.

**FR-LIB-007** Scan MUST suportar cancelamento.

**FR-LIB-008** UI MUST mostrar:

- discovered;
- processed;
- added;
- updated;
- ignored;
- failed.

**FR-LIB-009** Comportamento de reparse point/junction/symlink MUST ser explícito.

**FR-LIB-010** Ciclos de filesystem MUST NOT causar loop infinito.

**FR-LIB-011** Scan normal MUST NOT bloquear a thread WinUI.

**FR-LIB-012** Rescans SHOULD ser incrementais.

## 6. Supported Audio

O fork MUST inicialmente preservar todos os formatos suportados e testados pela base Nagi
selecionada.

Exemplos esperados:

- MP3;
- FLAC;
- AAC;
- OGG;
- WAV;
- AIFF;
- APE;
- DSD;
- M4A;
- WMA;
- WavPack;
- Opus.

A lista real deve derivar de capabilities testadas, não de listas hard-coded divergentes.

## 7. Track Inspector

Exibir quando disponível:

- Title;
- Artist;
- Album Artist;
- Album;
- Genre;
- Year;
- Track;
- Disc;
- File path;
- File size;
- Last modified;
- Container;
- Codec;
- Duration;
- Bitrate;
- Bitrate Mode;
- Sample Rate;
- Bit Depth;
- Channels;
- ReplayGain;
- ISRC;
- Artwork;
- MusicBrainz IDs;
- AcoustID;
- Metadata provenance.

## 8. Metadata Providers

### Local

Maior prioridade.

### AcoustID

Objetivo: identificar áudio usando fingerprint.

### MusicBrainz

Objetivo: catálogo, gravação, release, artista, MBID e ISRC.

### Providers existentes do Nagi

Auditar e reutilizar integrações existentes antes de criar novas.

Possíveis providers já existentes:

- MusicBrainz;
- Last.fm;
- ListenBrainz;
- TheAudioDB;
- Fanart.tv.

## 9. Identification Flow

```text
Track
  -> Generate Chromaprint
  -> AcoustID lookup
  -> Candidate recording
  -> MusicBrainz enrichment
  -> Metadata comparison
  -> Accept/Cancel
  -> Preview Diff
  -> Apply Tags
```

Nenhum overwrite automático.

## 10. Metadata Provenance

Modelo conceitual:

```text
MetadataValue<T>
- Value
- Source
- Confidence
- ExternalId
- RetrievedAt
- UserConfirmed
```

Um dado enriquecido não substitui a tag local até confirmação explícita.

## 11. Lyrics

Resolução:

1. Embedded synchronized lyrics;
2. Embedded plain lyrics;
3. Sidecar `.lrc`;
4. Sidecar `.txt`;
5. Local cache;
6. Remote provider.

Remote lyrics MUST ser opt-in.

Origem MUST ser visível.

Providers MUST ser substituíveis.

## 12. Equalizer

Preservar o EQ de 10 bandas existente.

Melhorias:

- preset selector;
- factory presets;
- custom presets;
- save;
- rename;
- duplicate;
- delete;
- pregain;
- reset;
- Flat;
- A/B;
- clipping warning/protection.

Presets candidatos:

- Flat;
- Classical;
- Club;
- Dance;
- Full Bass;
- Full Bass & Treble;
- Full Treble;
- Laptop;
- Large Hall;
- Live;
- Party;
- Pop;
- Reggae;
- Rock;
- Ska;
- Soft;
- Soft Rock;
- Techno;
- Vocal.

Preset values devem ser originais deste projeto.

## 13. FFT

Modos mínimos:

- Bars;
- Spectrum Line.

Opcional no primeiro release:

- Waveform.

Pipeline:

```text
Audio samples
  -> Window Function
  -> FFT
  -> Magnitude
  -> Smoothing
  -> SpectrumModel
  -> WinUI Composition Renderer
```

FFT MUST NOT bloquear playback.

FFT MUST NOT bloquear UI.

FFT MUST ser testável com sinais determinísticos.

Exemplo:

```text
generate 1 kHz sine
-> FFT
-> expected dominant frequency ~= 1 kHz
```

## 14. Non-Functional Requirements

### Performance

- UI responsiva durante scan;
- concorrência limitada;
- benchmark de biblioteca grande;
- FFT desligada sem trabalho residual desnecessário.

### Reliability

- falha de provider não encerra playback;
- cancelamento de scan não corrompe persistence;
- arquivo corrompido não interrompe toda biblioteca.

### Privacy

- offline-first;
- sem nova telemetria por padrão;
- credentials fora do source control.

### Licensing

- GPLv3 preservada;
- dependências/providers revisados;
- licença/termos desconhecidos bloqueiam release da integração.

## 15. Success Criteria

**SC-001** Uma pasta de teste com 3+ níveis é completamente indexada.

**SC-002** Um arquivo corrompido não impede as demais faixas.

**SC-003** Duas raízes sobrepostas não produzem track duplicada.

**SC-004** Todo campo enriquecido online expõe sua origem.

**SC-005** Desabilitar integrações de internet preserva playback, biblioteca, playlists, EQ,
lyrics locais e inspector.

**SC-006** Alterar preset do EQ não reinicia perceptivelmente a faixa.

**SC-007** Ativar/desativar Spectrum não interrompe playback.

**SC-008** Testes sintéticos de FFT detectam corretamente frequências dominantes.

**SC-009** Nenhuma tag é escrita sem confirmação explícita.

## 16. Feature Roadmap

### Feature 000 — Baseline Audit

Objetivo:

Comprovar o que a revisão selecionada do Nagi realmente faz.

Entregas:

- upstream tag/SHA;
- clean build;
- current tests;
- architecture map;
- scanner map;
- EQ pipeline;
- metadata provider map;
- lyrics pipeline;
- persistence map;
- known gaps.

Nenhuma feature de produto.

### Feature 001 — Recursive Root Library Hardening

Máximo 3 slices:

1. filesystem traversal + deterministic tests;
2. persistence + progress + cancellation;
3. WinUI integration + full regression.

### Feature 002 — Track Inspector

1. technical metadata acquisition;
2. ViewModel/UI;
3. formats/errors/integration tests.

### Feature 003 — Metadata Identification

1. Chromaprint + AcoustID;
2. MusicBrainz + cache + provenance;
3. review/confirmation UI.

### Feature 004 — Equalizer Presets

1. preset model/storage;
2. live EQ integration + UI;
3. clipping + regression.

### Feature 005 — FFT Spectrum Analyzer

1. audio analyzer + deterministic FFT;
2. WinUI renderer;
3. performance + efficiency + playback regression.

### Feature 006 — Lyrics Providers

1. resolution pipeline;
2. LRCLIB/cache provider boundary;
3. settings/provenance/tests.

## 17. Mandatory Slice Template

```markdown
### Slice N — [Observable Capability]

## META IMUTÁVEL

Problem:
[repeat global problem]

Definition of Success:
[repeat global definition]

## Vertical Scope

- input/UI;
- application behavior;
- infrastructure;
- persistence if required;
- dependency registration;
- automated tests.

## Do Not

- refactor unrelated code;
- create parallel architecture;
- create abstraction without need;
- replace dependency without approved rationale.

## Gate

dotnet build Nagi.sln --configuration Release

dotnet test Nagi.sln --configuration Release --no-build

[feature-specific integration test]
```

## 18. Definition of Done

Uma feature está Done apenas quando:

- [ ] Global Goal continua satisfeito;
- [ ] máximo de 3 slices salvo aprovação;
- [ ] nenhuma arquitetura duplicada;
- [ ] solution compila;
- [ ] testes passam;
- [ ] integration behavior passa;
- [ ] provider/license review passa;
- [ ] offline behavior permanece válido;
- [ ] nenhum secret em log/source;
- [ ] spec corresponde à implementação;
- [ ] plan corresponde à implementação;
- [ ] `/speckit.converge` sem gaps bloqueantes.

## 19. Questions Feature 000 Must Answer

1. Qual tag/SHA do Nagi será usada?
2. O scanner atual recurse corretamente?
3. Como trata junction/reparse points?
4. Como roots são persistidas?
5. Como identidade de track é determinada?
6. Como duplicatas são detectadas?
7. Qual é o pipeline real do EQ?
8. Que presets já existem?
9. O LibVLC atual fornece samples adequados para FFT?
10. Quais metadata providers estão habilitados?
11. Como LRCLIB está integrado?
12. Que caching já existe?
13. Que testes já cobrem essas áreas?
14. Como o fork sincronizará futuras mudanças do Nagi?
15. Existe intenção futura de distribuição comercial?
