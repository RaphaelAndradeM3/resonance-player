# FEATURE SPEC: 002 — Multi-Format Audio Library

**Feature Branch**: `002-multi-format-audio`  
**Created**: 2026-09-20  
**Status**: Draft  
**Input**: Baseado em `FEATURES_SUGERIDAS_SPEC_KIT.md` (Feature 002)

---

## 1. META IMUTÁVEL (Global Goal)

> **Problema de Negócio:** A biblioteca deve lidar corretamente com todos os formatos de áudio realmente suportados pelo Nagi/LibVLC e não apenas MP3, evitando divergência entre o que o scanner aceita e o que o player consegue tocar.
>
> **Definição de Sucesso:** MP3, FLAC, WAV e demais formatos suportados pela baseline são descobertos, identificados e reproduzidos conforme a capability real da aplicação; arquivos incompatíveis são reportados de forma explícita.
>
> **Regra de Ouro:** Não manter listas duplicadas de extensões se a codebase já possui fonte de capability.

---

## 2. CONTRATOS & LIMITES DA ARQUITETURA

* **Projetos Afetados na Solution (.sln):**
  - Módulos de detecção de formato/mime, leitor de tags/metadata, scanner e player (LibVLC/MediaEngine).
* **Tipos/Serviços Existentes que DEVEM ser reutilizados:**
  - Provedor de reprodução existente (ex.: LibVLC / MediaPlayer).
  - Leitor de tags/metadata unificado (ex.: TagLib# ou leitor embutido).
  - Tabela/enumeração de extensões e codecs suportados.
* **Formatos Esperados para Validação da Baseline:**
  - MP3, FLAC, WAV, AAC, M4A, OGG, Opus, WMA, AIFF, APE, WavPack, DSD e demais formatos suportados pela base real.
* **Convenções Obrigatórias:**
  - Fonte única de verdade (Single Source of Truth) para formatos aceitos pelo scanner e player.
  - Não descartar formatos sem antes verificar as capabilities do motor de áudio.
  - Mensagens de erro amigáveis caso um arquivo tenha extensão suportada mas codec interno incompatível.

---

## 3. FATIAS VERTICAIS DE IMPLEMENTAÇÃO (Max 3 Slices)

### Slice 1: Capability Inventory & Matriz de Suporte
- **Meta Imutável repetida:** Identificar com precisão quais formatos são suportados para scanner, metadata e playback, eliminando inconsistências.
- **Escopo ponta a ponta:** Mapear os codecs/containers suportados pelo motor de playback e leitor de tags; gerar matriz de compatibilidade (Formato x Playback x Metadata).
- **Reutilização obrigatória:** Motor de áudio existente (LibVLC) e bibliotecas de tag existentes.
- **Teste obrigatório:** Testes com amostras de áudio sintéticas ou reais cobrindo cada formato suportado da matriz.
- **Validação Local:** `dotnet build` + execução de testes de leitura e decodificação por formato.

### Slice 2: Unificação Scanner e Playback Pipeline
- **Meta Imutável repetida:** Assegurar que todo arquivo descoberto pelo scanner possa ser aberto e reproduzido pelo player sem falhas de extensão não reconhecida.
- **Escopo ponta a ponta:** Unificar as listas e validações de extensões entre o módulo de scanner e o módulo de playback; implementar tratamento gracioso para arquivos corrompidos ou com codec interno não suportado.
- **Reutilização obrigatória:** Serviços unificados de scanner e reprodução.
- **Teste obrigatório:** Testes automatizados verificando que 100% das extensões registradas na matriz são aceitas pelo scanner e enfileiradas pelo player.
- **Validação Local:** `dotnet build` + testes de integração scanner/playback.

### Slice 3: UI, Filtros de Formato e Regressão
- **Meta Imutável repetida:** Exibir claramente o formato/codec do arquivo na interface da biblioteca e permitir filtragem por formato sem quebrar o fluxo do player.
- **Escopo ponta a ponta:** Adicionar indicação visual de formato (badges/labels ex: FLAC, MP3, HI-RES), filtros por tipo de arquivo na biblioteca e garantir que a fila de reprodução intercale formatos diversos sem engasgos.
- **Reutilização obrigatória:** Controles e templates de exibição de faixas da biblioteca.
- **Teste obrigatório:** Teste de reprodução em sequência contínua (ex: MP3 -> FLAC -> WAV -> M4A -> OGG) verificando estabilidade.
- **Validação Final:** Solution inteira compilando e testes passando.

---

## 4. GATES DE VALIDAÇÃO (.NET Toolchain)

```powershell
dotnet restore
dotnet build --configuration Release --warnaserror
dotnet test --configuration Release --no-build
```

Nenhuma tarefa pode ser marcada como concluída se um formato aceito pelo scanner falhar silenciosamente no playback.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Reprodução Transparente de Arquivos Lossless e Lossy (Priority: P1)
Como amante de música com faixas em MP3, FLAC, WAV e M4A, quero que todas sejam adicionadas à minha biblioteca e toquem perfeitamente sem necessidade de converter arquivos.

**Why this priority**: Usuários modernos de desktop possuem coleções em múltiplos formatos e esperam suporte nativo.

**Independent Test**: Carregar uma pasta com faixas nos formatos MP3, FLAC, WAV, AAC, M4A, OGG e Opus, e tocar cada uma até o final com sucesso.

**Acceptance Scenarios**:
1. **Given** arquivos de áudio em diferentes formatos suportados, **When** forem adicionados à biblioteca, **Then** todos são indexados com suas respectivas extensões identificadas.
2. **Given** faixas de formatos distintos na fila, **When** a reprodução transicionar de um formato para outro, **Then** o áudio toca sem estalos, interrupções ou travamento.

---

### User Story 2 - Identificação Precisa de Formatos Incompatíveis ou Corrompidos (Priority: P2)
Como usuário que possui arquivos com extensões incorretas ou dados danificados, quero que a aplicação informe claramente o motivo da falha sem travar a lista de reprodução.

**Why this priority**: Evita que arquivos inválidos interrompam a sessão de reprodução ou causem frustração silenciosa.

**Independent Test**: Tentar tocar um arquivo renomeado para .flac que contenha texto plano; verificar se o player exibe mensagem explicativa e pula para a próxima faixa.

**Acceptance Scenarios**:
1. **Given** um arquivo corrompido, **When** o player tentar decodificá-lo, **Then** ele emite aviso não-bloqueante e avança para a faixa seguinte.
2. **Given** um arquivo com formato não suportado, **When** escaneado, **Then** ele é ignorado ou marcado com status claro no log de scan.

---

### User Story 3 - Visualização e Filtro por Formato de Áudio (Priority: P3)
Como audiófilo, quero visualizar os formatos das minhas músicas na biblioteca e poder filtrar para ver apenas meus álbuns em FLAC ou Lossless.

**Why this priority**: Facilita a gestão da coleção e agrega valor para usuários exigentes em fidelidade de áudio.

**Independent Test**: Aplicar filtro "FLAC" na biblioteca e verificar se somente faixas FLAC são listadas.

**Acceptance Scenarios**:
1. **Given** a lista de músicas, **When** exibida na biblioteca, **Then** cada item exibe seu formato/container de áudio.
2. **Given** o filtro de formatos na UI, **When** o usuário seleciona um formato específico, **Then** a exibição é filtrada instantaneamente.

---

### Edge Cases
- Arquivos de áudio com taxa de amostragem incomum (ex.: 192kHz/24-bit ou 96kHz).
- Arquivos com container MP4/M4A contendo streams não suportados (ex.: vídeo sem áudio).
- Transição sem pausas (gapless) entre faixas de diferentes taxas de amostragem.

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: O sistema DEVE unificar o catálogo de extensões suportadas entre o módulo de scanner e o módulo de áudio.
- **FR-002**: O sistema DEVE suportar nativamente a decodificação de MP3, FLAC, WAV, AAC, M4A, OGG, Opus e WMA.
- **FR-003**: O sistema DEVE extrair metadados compatíveis com cada formato especificado.
- **FR-004**: O sistema DEVE tratar falhas de decodificação individual sem congelar ou derrubar a thread de playback.
- **FR-005**: O sistema DEVE disponibilizar filtro por formato de áudio na interface da biblioteca.

### Key Entities

- **AudioFormatDescriptor**: Representa o formato, extensão, mime-type, container e capacidades (playback, tags, seek).
- **FormatCompatibilityMatrix**: Matriz com os formatos suportados e capacidades comprovadas na baseline.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% dos formatos da matriz aprovada (MP3, FLAC, WAV, M4A, OGG, Opus) tocam sem erro.
- **SC-002**: Transição entre faixas de formatos diferentes ocorre em menos de 300ms.
- **SC-003**: Zero falhas catastróficas (crashes) provocadas por arquivos de áudio malformados ou corrompidos.

---

## Assumptions

- O backend de áudio (LibVLC ou Windows Media Foundation) possui codecs nativos para os formatos listados.
- Tags são lidas respeitando os padrões de cada container (ID3v2, Vorbis Comments, MP4 Atoms, RIFF INFO).
