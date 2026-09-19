# IDEIA.md — Resonance / Modern Local Music Player

> Status: proposta estratégica para evolução de um fork do Nagi  
> Nome do produto: **Resonance**
> Repositório sugerido: **Resonance-Player**
> Plataforma: Windows  
> Base pretendida: Nagi (C#, .NET 10, WinUI 3)  
> Modelo de desenvolvimento: Spec-Driven Development com GitHub Spec Kit  
> Data de referência da pesquisa: 2026-09-19

## 1. Visão

Criar um player de música local moderno para Windows aproveitando a base já madura do Nagi,
preservando sua proposta de privacidade e desempenho, mas elevando quatro áreas:

1. biblioteca local robusta para árvores de diretórios grandes e profundas;
2. experiência de áudio inspirada nos melhores recursos de players clássicos, sem copiar marca,
   código, assets ou skins proprietárias;
3. análise visual e técnica da reprodução, incluindo FFT/espectro e informações do arquivo;
4. enriquecimento opcional de metadados, capas e letras por provedores externos, com
   cache controlado e respeito explícito às licenças de cada fonte.

O objetivo não é reescrever um player do zero. O objetivo é partir do código real do Nagi,
validar o que já existe e evoluir por fatias verticais pequenas o suficiente para serem
revisáveis, mas grandes o suficiente para preservar o fluxo de ponta a ponta.

## 2. Princípio fundamental

> O código existente é a fonte da verdade.

Nenhuma feature descrita neste documento deve ser implementada antes de verificar se já existe
integral ou parcialmente na versão-base do Nagi.

O primeiro trabalho do projeto será uma auditoria técnica.

## 3. Estado conhecido do Nagi

A versão atual pesquisada do Nagi já declara:

- C# + .NET 10 + WinUI 3;
- LibVLCSharp para reprodução;
- ATL para leitura/escrita de metadados;
- SQLite via Entity Framework Core;
- letras sincronizadas embutidas ou em `.lrc`;
- obtenção opcional de letras via LRCLIB;
- biblioteca baseada em pastas;
- playlists e smart playlists;
- equalizador de 10 bandas com pregain;
- ReplayGain;
- suporte a MP3, FLAC, AAC, OGG, WAV, AIFF, APE, DSD, M4A, WMA, WavPack e outros;
- integrações Last.fm e ListenBrainz;
- metadados online com MusicBrainz, TheAudioDB, Fanart.tv e Last.fm.

Portanto, várias features originalmente imaginadas para o projeto são melhorias de recursos
já existentes.

## 4. Problema principal de biblioteca

O comportamento desejado é:

```text
D:\Musicas
D:\Musicas\Rock
D:\Musicas\Rock\Pink Floyd
D:\Musicas\Rock\Pink Floyd\The Wall
D:\Musicas\Jazz
D:\Musicas\Jazz\Miles Davis
```

Ao selecionar apenas `D:\Musicas`, todas as músicas em subpastas elegíveis devem ser
descobertas automaticamente.

O mesmo deve funcionar com várias raízes:

```text
D:\Musicas
E:\FLAC
\\NAS\Music
```

Nenhuma subpasta deve precisar ser cadastrada manualmente.

## 5. Contrato do scanner

Uma `LibraryRoot` representa uma árvore completa.

O scanner deve:

1. processar subdiretórios recursivamente;
2. suportar múltiplas raízes;
3. normalizar paths;
4. evitar músicas duplicadas;
5. detectar raízes sobrepostas;
6. impedir loops de junction/symlink/reparse point;
7. ignorar arquivos não suportados;
8. continuar após arquivo corrompido;
9. continuar após diretório sem acesso;
10. permitir cancelamento;
11. reportar progresso;
12. realizar atualização incremental;
13. trabalhar sem bloquear a UI;
14. suportar bibliotecas muito grandes.

Resultados do scan:

- encontrados;
- processados;
- adicionados;
- atualizados;
- ignorados;
- corrompidos;
- indisponíveis;
- erros.

## 6. Metadados locais

O arquivo continua sendo a fonte primária.

Ler, quando disponível:

- Title;
- Artist;
- Album Artist;
- Album;
- Track;
- Disc;
- Year;
- Genre;
- Comment;
- ISRC;
- ReplayGain;
- embedded artwork;
- lyrics;
- duration;
- bitrate;
- bitrate mode;
- sample rate;
- bit depth;
- channels;
- codec;
- container.

## 7. Track Inspector

Adicionar painel de detalhes da faixa com:

- título, artista, álbum, gênero e ano;
- caminho completo e tamanho;
- container e codec;
- duração;
- bitrate;
- sample rate;
- bit depth;
- canais;
- ReplayGain;
- ISRC;
- artwork;
- MusicBrainz IDs;
- AcoustID;
- origem de cada metadado.

## 8. Identificação por fingerprint

Pipeline:

```text
arquivo
  -> Chromaprint
  -> AcoustID
  -> MusicBrainz Recording ID
  -> MusicBrainz
  -> Candidate Metadata
  -> Review
  -> Apply
```

Nunca enviar o áudio completo.

Somente fingerprint, duração e dados necessários ao provider.

Nenhuma tag será sobrescrita automaticamente.

## 9. Metadata provenance

Cada valor enriquecido deve saber de onde veio.

Modelo conceitual:

```text
MetadataValue<T>
- Value
- Source
- Confidence
- RetrievedAt
- ExternalId
- UserConfirmed
```

Sources possíveis:

- LocalTag;
- Filename;
- MusicBrainz;
- AcoustID;
- LastFm;
- TheAudioDB;
- User.

## 10. Letras

Prioridade:

1. embedded synchronized lyrics;
2. embedded plain lyrics;
3. `.lrc`;
4. `.txt`;
5. cache;
6. provider remoto.

Arquitetura conceitual:

```text
ILyricsProvider
  LocalLyricsProvider
  LrcLyricsProvider
  CachedLyricsProvider
  LrclibLyricsProvider
```

Providers remotos devem ser opcionais.

Não criar banco redistribuível de letras de terceiros.

## 11. Equalizador

Preservar e evoluir o EQ existente de 10 bandas.

Recursos:

- enable/disable;
- pregain;
- sliders;
- reset;
- presets;
- presets personalizados;
- salvar;
- duplicar;
- renomear;
- excluir;
- import/export;
- comparação A/B;
- proteção contra clipping.

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

Os valores devem ser próprios do projeto, sem copiar presets proprietários.

## 12. FFT / Spectrum Analyzer

Adicionar pipeline independente de visualização:

```text
Playback
  -> audio samples
  -> window function
  -> FFT
  -> magnitude/smoothing
  -> Spectrum Model
  -> WinUI Renderer
```

Visualizações:

- barras;
- spectrum line;
- waveform;
- peak hold.

Configurações:

- FFT size;
- smoothing;
- decay;
- FPS;
- logarithmic frequency;
- amplitude scale.

A FFT deve ser desabilitável.

Desligar o visualizador também deve desligar processamento desnecessário.

## 13. UI

Não construir clone pixel-perfect do Winamp.

Direção de UX:

> player moderno com eficiência informacional de um player clássico.

No `Now Playing`, permitir alternar entre:

- Player;
- Lyrics;
- Spectrum;
- Details.

O equalizador deve continuar rapidamente acessível.

## 14. Privacidade

- local-first;
- offline-capable;
- APIs externas opcionais;
- sem nova telemetria por padrão;
- segredos fora do código;
- informar o que cada provider envia.

## 15. Licença

O projeto deriva do Nagi GPL-3.0.

O fork deverá:

- preservar avisos existentes;
- preservar atribuição;
- cumprir GPLv3;
- disponibilizar source correspondente quando aplicável;
- revisar licença de novas dependências;
- manter `THIRD_PARTY_NOTICES.md`.

Não reutilizar assets, marcas, ícones ou skins proprietárias do Winamp.

## 16. Estratégia Spec Kit contra goal drift

Não quebrar uma feature em microtarefas do tipo:

- criar DTO;
- criar interface;
- criar repository;
- registrar DI;
- criar service;
- criar ViewModel;
- criar View.

Usar vertical slices.

Exemplo:

> Adicionar uma pasta raiz, percorrer todos os subdiretórios, persistir músicas descobertas,
> mostrar progresso e provar através de teste de integração que músicas em três níveis aparecem.

Máximo padrão: **3 implementation slices por feature**.

## 17. Sequência estratégica

1. Feature 000 — Baseline/Audit;
2. Feature 001 — Recursive Root Library Hardening;
3. Feature 002 — Track Inspector;
4. Feature 003 — Fingerprint Metadata Identification;
5. Feature 004 — Equalizer Preset Manager;
6. Feature 005 — FFT Spectrum Analyzer;
7. Feature 006 — Lyrics Provider Boundary;
8. Feature 007 — Metadata Review & Apply.

## 18. Regra principal

Antes de implementar: **AUDITAR**.

Antes de criar abstração: **PROCURAR A EXISTENTE**.

Antes de modificar: **TESTAR O COMPORTAMENTO ATUAL**.

Antes de finalizar: **BUILD + TEST DA SOLUTION INTEIRA**.

## 19. Referências técnicas

- Nagi: https://github.com/Anthonyy232/Nagi
- Spec Kit: https://github.com/github/spec-kit
- MusicBrainz API: https://musicbrainz.org/doc/MusicBrainz_API
- MusicBrainz Data License: https://musicbrainz.org/doc/About/Data_License
- AcoustID Web Service: https://acoustid.org/webservice
- AcoustID Licensing: https://acoustid.org/license
- LRCLIB: https://github.com/tranxuanthang/lrclib
- Last.fm API Terms: https://www.last.fm/api/tos
