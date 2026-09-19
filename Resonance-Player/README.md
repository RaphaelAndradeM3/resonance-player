# Resonance

<p align="center">
  <strong>Modern local-first music player for Windows.</strong><br>
  <em>Player de música moderno e local-first para Windows.</em>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Platform-Windows-0078D6?logo=windows&logoColor=white" alt="Platform" />
  <img src="https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white" alt=".NET 10" />
  <img src="https://img.shields.io/badge/UI-WinUI%203-0078D7?logo=fluent-design&logoColor=white" alt="WinUI 3" />
  <img src="https://img.shields.io/badge/Language-C%23-239120?logo=csharp&logoColor=white" alt="C#" />
  <img src="https://img.shields.io/badge/License-GPL--3.0-blue.svg" alt="GPL-3.0 License" />
  <img src="https://img.shields.io/badge/Architecture-Local--First-success" alt="Local-First" />
</p>

---

## 🎵 Sobre o Projeto / About

**Resonance** é um player de música moderno para Windows, desenvolvido em **C#**, **.NET 10** e **WinUI 3**, projetado para pessoas que mantêm, organizam e valorizam grandes coleções locais de áudio.

O projeto parte de uma base open-source madura ([Nagi](https://github.com/Anthonyy232/Nagi), sob licença GPLv3) e evolui a experiência através de um modelo rigoroso de desenvolvimento guiado por especificações (**Spec-Driven Development** com GitHub Spec Kit), combinando a elegância visual do Windows moderno com a eficiência informacional de players clássicos de áudio.

> **GitHub Description (EN):** Modern local-first music player for Windows with large-library management, metadata enrichment, audio fingerprinting, synchronized lyrics, EQ presets and real-time FFT visualization.  
> **Descrição (PT-BR):** Player de música moderno e local-first para Windows, com gerenciamento de grandes bibliotecas, enriquecimento de metadados, fingerprint de áudio, letras sincronizadas, equalizador com presets e visualização FFT em tempo real.

---

## 🌟 Principais Recursos / Key Features

### 📂 1. Gerenciamento de Grandes Bibliotecas & Múltiplas Pastas Raiz
- **Varredura Recursiva Automática:** Adicione uma pasta raiz (ex.: `D:\Musicas`) e todas as subpastas elegíveis em qualquer profundidade são indexadas automaticamente sem necessidade de cadastro manual.
- **Múltiplos Locais de Armazenamento:** Suporte nativo e simultâneo a múltiplos drives, pastas locais e compartilhamentos de rede (`D:\Musicas`, `E:\FLAC`, `\\NAS\Music`).
- **Resiliência Extrema:** O scanner ignora pastas sem permissão ou arquivos corrompidos sem interromper o processo; previne loops de *junction points/symlinks* e evita duplicações mesmo com raízes sobrepostas.
- **Varredura Incremental & Não Bloqueante:** A interface permanece fluida durante o scan, com telemetria visual de progresso (descobertos, processados, adicionados, ignorados e erros).

### 🎧 2. Amplo Suporte a Formatos de Áudio
- Reprodução de alta fidelidade via **LibVLCSharp**.
- Suporte a múltiplos contêineres e formatos: **MP3, FLAC, AAC, OGG, WAV, AIFF, APE, DSD, M4A, WMA, WavPack** e outros.

### 🔍 3. Track Inspector & Metadados Técnicos Detalhados
- Painel de inspeção aprofundada de faixas exibindo dados técnicos e contextuais:
  - Título, Artista, Artista do Álbum, Álbum, Número de Faixa, Disco, Ano, Gênero e ISRC.
  - Informações de codificação: container, codec, taxa de bits (*bitrate* e modo), taxa de amostragem (*sample rate*), profundidade de bits (*bit depth*) e canais.
  - Metadados de volume e normalização (**ReplayGain**).
  - Capas embutidas em alta resolução.

### 🧬 4. Identificação por Fingerprint de Áudio (AcoustID)
- Identificação precisa de faixas desconhecidas ou mal identificadas via **Chromaprint** + **AcoustID** + **MusicBrainz**.
- **Privacidade Absoluta:** O arquivo de áudio original **nunca** é enviado para a rede — apenas a impressão digital matemática (fingerprint) e a duração são transmitidas.
- **Controle Total:** Nenhuma tag local é sobrescrita automaticamente. Os candidatos são apresentados para revisão prévia e aprovação do usuário.

### 🛡️ 5. Proveniência e Transparência de Tags
- Sistema explícito de proveniência de metadados (`MetadataValue<T>`): saiba com clareza a origem de cada campo (`LocalTag`, `Filename`, `MusicBrainz`, `AcoustID`, `LastFm`, `TheAudioDB` ou manual pelo usuário) com nível de confiança associado.

### 📝 6. Letras Locais e Sincronizadas
- Suporte nativo a letras sincronizadas (`.lrc`) e em texto puro (`.txt`), priorizando arquivos locais e tags embutidas no arquivo de áudio.
- Enriquecimento opcional e seguro via provedor remoto ([LRCLIB](https://github.com/tranxuanthang/lrclib)) com cache local inteligente, sem depender de nuvem proprietária.

### 🎚️ 7. Equalizador de 10 Bandas com Gerenciador de Presets
- Preservação do EQ existente de 10 bandas com controle de *pregain* e proteção contra clipping.
- Presets prontos para diversos gêneros e cenários acústicos.
- Gerenciamento completo: salvar presets personalizados, renomear, duplicar, exportar, importar e comparação rápida A/B.

### 📊 8. FFT e Visualizador de Espectro em Tempo Real
- Pipeline de renderização em tempo real independente do fluxo de áudio principal.
- Modos visuais: barras de frequência, linha de espectro, waveform e retenção de pico (*peak hold*).
- Calibração de tamanho de FFT, suavização, decaimento e escala logarítmica.
- **Eficiência Energética:** O pipeline de análise é completamente desativado quando o visualizador é ocultado, economizando CPU e bateria.

### 🎨 9. Interface Moderna em WinUI 3
- Design nativo e fluido alinhado aos padrões visuais do Windows 11 (Fluent Design System).
- Alternância rápida na visualização *Now Playing* entre Player, Letras (Lyrics), Espectro (Spectrum) e Detalhes Técnicos (Details).
- Identidade visual 100% original, inspirada na praticidade de players clássicos sem copiar marcas, skins ou assets de terceiros.

---

## 🏛️ Princípios & Filosofia / Core Principles

O desenvolvimento do Resonance é regido pela [Constituição do Projeto](CONSTITUTION.md):

1. **Local-First & Privacy-First:** Todas as operações essenciais (tocar, organizar, equalizar, visualizar letras locais) funcionam 100% offline. Serviços de rede são estritamente opcionais, com consentimento explícito e sem telemetria por padrão.
2. **O Código Existente é a Fonte da Verdade:** Nenhuma funcionalidade é criada do zero sem antes auditar a base herdada. Abstrações, serviços ou modelos paralelos são expressamente proibidos quando já existirem equivalentes.
3. **Spec-Driven Development:** Toda evolução do sistema segue especificações formais via [GitHub Spec Kit](https://github.com/github/spec-kit).
4. **Fatias Verticais (Vertical Slices):** Tarefas não são divididas horizontalmente (camadas técnicas isoladas). Cada incremento entrega um comportamento de ponta a ponta em no máximo três fatias verticais, prevenindo *goal drift*.
5. **Whole-Solution Validation:** Nenhuma alteração é aceita sem a compilação limpa da solution inteira e a aprovação de toda a suíte de testes automatizados.

---

## 🛠️ Stack Tecnológica / Tech Stack

| Camada | Tecnologia | Descrição |
| :--- | :--- | :--- |
| **Runtime & Linguagem** | .NET 10 / C# | Performance moderna com recursos avançados de tipagem |
| **Interface (UI)** | WinUI 3 / Windows App SDK | UI nativa Windows com Fluent Design e aceleração gráfica |
| **Motor de Reprodução** | LibVLCSharp (LibVLC) | Decodificação robusta para ampla variedade de codecs |
| **Leitura/Escrita de Tags** | ATL (Audio Tools Library) | Inspeção e manipulação de metadados embutidos de alta precisão |
| **Banco de Dados** | SQLite + Entity Framework Core | Persistência local rápida, leve e desacoplada |
| **Fingerprint de Áudio** | Chromaprint / AcoustID | Identificação de áudio preservando a privacidade do usuário |
| **Provedores de Metadados** | MusicBrainz, Last.fm, TheAudioDB, LRCLIB | Enriquecimento opcional de capas, letras e metadados |

---

## 🗺️ Roadmap de Features

A evolução inicial do Resonance está estruturada nas seguintes etapas verticais:

| Feature | Código | Foco Principal |
| :---: | :--- | :--- |
| **000** | `Baseline & Auditoria` | Mapeamento completo do repositório base Nagi, validação de build/testes e identificação de lacunas técnicas. |
| **001** | `Recursive Root Library Hardening` | Varredura recursiva robusta, múltiplas raízes, resiliência a falhas de I/O e telemetria de progresso na UI. |
| **002** | `Track Inspector` | Painel completo de metadados técnicos, codec, container, bitrate, ReplayGain e integridade do arquivo. |
| **003** | `Fingerprint Metadata Identification` | Pipeline Chromaprint + AcoustID para descoberta e sugestão segura de metadados sem envio de arquivos. |
| **004** | `Equalizer Preset Manager` | Gerenciador de curvas de EQ, pregain, proteção anti-clipping e importação/exportação de presets. |
| **005** | `FFT Spectrum Analyzer` | Pipeline de visualização em tempo real (barras, espectro, waveform) com baixo consumo de recursos. |
| **006** | `Lyrics Provider Boundary` | Fronteira estrita de provedores de letras priorizando fontes locais e permitindo LRCLIB opcional. |
| **007** | `Metadata Review & Apply` | Interface de comparação antes/depois para aplicação segura de tags nos arquivos locais. |

---

## 📚 Documentação do Repositório

Toda a documentação técnica, decisões de produto e governança estão disponíveis neste diretório:

- 💡 [**IDEIA.md**](IDEIA.md) — Visão de produto, contexto histórico, auditoria inicial e direcionamento estratégico.
- 📋 [**PRD.md**](PRD.md) — Documento de Requisitos de Produto (requisitos funcionais, cenários e critérios de aceite).
- 🧩 [**FEATURES_SUGERIDAS_SPEC_KIT.md**](FEATURES_SUGERIDAS_SPEC_KIT.md) — Catálogo de features e guardrails obrigatórios para o Spec Kit.
- 📜 [**CONSTITUTION.md**](CONSTITUTION.md) — Constituição do projeto e regras de arquitetura (cópia legível de `../.specify/memory/constitution.md`).
- 🎨 [**BRANDING.md**](BRANDING.md) — Posicionamento de marca, taglines e diretrizes de identidade visual.
- 🚀 [**SPEC_KIT_HANDOFF.md**](SPEC_KIT_HANDOFF.md) — Guia prático de inicialização e comandos para o fluxo Spec Kit.

---

## 💻 Como Compilar e Desenvolver / Building & Running

### Pré-requisitos
- **Windows 10 (versão 1809 ou superior)** ou **Windows 11**
- **.NET 10 SDK** instalado
- **Visual Studio 2022** (com suporte a *Windows App SDK / WinUI 3*) ou **Visual Studio Code** com *C# Dev Kit*

### Comandos de Validação (.NET CLI)

```powershell
# Restaurar dependências
dotnet restore

# Compilar em Release (modo estrito)
dotnet build --configuration Release

# Executar a suíte de testes automatizados
dotnet test --configuration Release --no-build
```

---

## ⚖️ Licença & Atribuições / License

Este projeto é software livre licenciado sob a **[GNU General Public License v3.0 (GPL-3.0)](../LICENSE)**.

Resonance é desenvolvido a partir do código do projeto open-source **[Nagi](https://github.com/Anthonyy232/Nagi)**, criado por Anthonyy232 e colaboradores. Todas as atribuições, avisos de direitos autorais originais e termos da licença GPL-3.0 são rigorosamente preservados.

As marcas, logotipos e referências externas pertencem aos seus respectivos proprietários. Resonance não reutiliza marcas, códigos proprietários, skins ou assets do Winamp ou de qualquer outro software comercial.
