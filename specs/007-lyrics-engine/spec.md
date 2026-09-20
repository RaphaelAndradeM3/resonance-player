# FEATURE SPEC: 007 — Lyrics Engine

**Feature Branch**: `007-lyrics-engine`  
**Created**: 2026-09-20  
**Status**: Draft  
**Input**: Baseado em `FEATURES_SUGERIDAS_SPEC_KIT.md` (Feature 007)

---

## 1. META IMUTÁVEL (Global Goal)

> **Problema de Negócio:** O usuário deve visualizar letras da música atual priorizando dados locais e, opcionalmente, complementar com providers online.
>
> **Definição de Sucesso:** Embedded lyrics, `.lrc`, `.txt`, cache e provider remoto seguem uma ordem previsível; letras sincronizadas acompanham playback e a origem é visível.
>
> **Regra de Ouro:** Provider remoto é opcional e não transforma o projeto em distribuidor de corpus de letras.

---

## 2. CONTRATOS & LIMITES DA ARQUITETURA

* **Projetos Afetados na Solution (.sln):**
  - Módulos de resolução e parser de letras (local/externo), cache de letras e UI de visualização sincronizada (Now Playing / Lyrics View).
* **Tipos/Serviços Existentes que DEVEM ser reutilizados:**
  - Leitor de tags para extração de letras embutidas (ID3 USLT/SYLT, Vorbis LYRICS).
  - Provedor LRCLIB ou infraestrutura HTTP já existente no Nagi.
  - Eventos de atualização de posição/tempo de reprodução do player.
* **Ordem Canônica Obrigatória de Resolução de Letras:**
  1. Letras sincronizadas embutidas no arquivo (Embedded Synced).
  2. Letras de texto puro embutidas no arquivo (Embedded Plain).
  3. Arquivo `.lrc` na mesma pasta do arquivo de áudio.
  4. Arquivo `.txt` na mesma pasta do arquivo de áudio.
  5. Cache local de buscas anteriores.
  6. Provedor remoto (ex.: LRCLIB), se habilitado pelo usuário.
* **Convenções Obrigatórias:**
  - Resolução de letras deve ser não-bloqueante e priorizar sempre o arquivo local.
  - O provedor remoto deve ser totalmente opcional e respeitar configurações de privacidade.
  - Indicação clara da fonte da letra na interface ("Embutida", "Arquivo .lrc", "LRCLIB").
  - Sincronização em tempo real acompanhando a posição do áudio (com scroll automático suave).

---

## 3. FATIAS VERTICAIS DE IMPLEMENTAÇÃO (Max 3 Slices)

### Slice 1: Local Lyrics Resolution & Parser
- **Meta Imutável repetida:** Resolver e parsear letras locais (embutidas ou em arquivos adjacentes `.lrc`/`.txt`) seguindo a ordem estrita de precedência.
- **Escopo ponta a ponta:** Implementar leitor de tags locais para letras e leitor de arquivos no mesmo diretório; parser de formato LRC com timestamps `[mm:ss.xx]` e linhas de texto correspondentes.
- **Reutilização obrigatória:** Leitor de tags existente e serviços de filesystem.
- **Teste obrigatório:** Testes unitários com arquivos LRC válidos, inválidos, timestamps fora de ordem e tags embutidas.
- **Validação Local:** `dotnet build` + testes da suite de parsing de letras.

### Slice 2: Remote Provider & Cache Layer
- **Meta Imutável repetida:** Consultar provedor remoto opcional (ex.: LRCLIB) somente quando não houver letra local, mantendo cache e isolamento de falhas.
- **Escopo ponta a ponta:** Integrar cliente de letras online com busca por artista, título, álbum e duração; implementar cache local de letras em disco com registro da procedência; respeitar modo offline.
- **Reutilização obrigatória:** Cliente HTTP e sistema de configurações de provedores.
- **Teste obrigatório:** Testes com respostas mockadas do LRCLIB, verificação de cache e comportamento sem conexão.
- **Validação Local:** `dotnet build` + testes de integração de rede/cache.

### Slice 3: Synchronized Lyrics UI & Regressão
- **Meta Imutável repetida:** Renderizar as letras na tela com destaque na linha atual, rolagem suave conforme a música toca e suporte a letras estáticas.
- **Escopo ponta a ponta:** Desenvolver componente de visualização de letras (Lyrics View) sincronizado aos ticks de tempo do player, permitindo clicar em uma linha para pular o áudio para aquele timestamp (karaokê/seek).
- **Reutilização obrigatória:** Controles de áudio e eventos de playback do player.
- **Teste obrigatório:** Teste de interface verificando destaque de linha no avanço do tempo e comportamento em modo offline.
- **Validação Final:** Solution inteira compilando e testes passando.

---

## 4. GATES DE VALIDAÇÃO (.NET Toolchain)

```powershell
dotnet restore
dotnet build --configuration Release --warnaserror
dotnet test --configuration Release --no-build
```

Nenhuma tarefa pode ser marcada como concluída se a busca por letras atrasar a reprodução de áudio ou sobrescrever arquivos `.lrc` locais sem autorização.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Letras Sincronizadas Acompanhando a Reprodução (Priority: P1)
Como ouvinte que gosta de cantar junto, quero que a tela de letras exiba as frases sincronizadas com a música e role automaticamente destacando a frase do momento.

**Why this priority**: Experiência rica e imersiva fundamental em players modernos.

**Independent Test**: Tocar música com arquivo `.lrc` associado; verificar se o texto rola e a frase atual fica iluminada em sincronia com o áudio.

**Acceptance Scenarios**:
1. **Given** uma faixa com letras sincronizadas, **When** o áudio avança, **Then** a linha correspondente ao timestamp atual é destacada.
2. **Given** a visualização de letras, **When** o usuário clica em uma estrofe futura, **Then** o player realiza o seek para o segundo correspondente àquela linha.

---

### User Story 2 - Priorização Rigorosa de Letras Locais e Arquivos Próprios (Priority: P2)
Como usuário que possui arquivos `.lrc` editados manualmente, quero que o player use meu arquivo local antes de tentar qualquer consulta na internet.

**Why this priority**: Preserva a soberania dos dados do usuário e garante funcionamento sem internet.

**Independent Test**: Ter uma música com arquivo `.lrc` na mesma pasta e rede conectada; verificar se nenhuma requisição externa de letras é emitida.

**Acceptance Scenarios**:
1. **Given** uma música com arquivo `.lrc` local, **When** a reprodução começa, **Then** o sistema carrega o arquivo local e não consulta o provedor remoto.
2. **Given** uma faixa sem letras locais e provedor remoto desabilitado, **When** tocada, **Then** a tela informa amigavelmente "Nenhuma letra disponível".

---

### User Story 3 - Busca Online Transparente com Indicação de Fonte (Priority: P3)
Como usuário com músicas sem letras locais, quero que o player busque letras de fontes confiáveis (ex: LRCLIB) e me informe de onde o texto foi obtido.

**Why this priority**: Preenche lacunas na coleção de forma conveniente.

**Independent Test**: Habilitar busca online em faixa sem dados locais; verificar retorno do LRCLIB, armazenamento no cache e etiqueta "Fonte: LRCLIB".

**Acceptance Scenarios**:
1. **Given** uma faixa sem letras locais e busca online ativada, **When** a música toca, **Then** a letra é baixada em background e exibida.
2. **Given** uma letra baixada da web, **When** exibida, **Then** um selo indica a fonte do provedor.

---

### Edge Cases
- Arquivos `.lrc` com formatos de tempo atípicos (ex.: vírgula em vez de ponto, horas completas `[hh:mm:ss]`).
- Músicas instrumentais sem letra (o provedor retorna sinalizador instrumental ou vazio).
- Variações de fuso e diferença sutil de duração entre versão da web e arquivo local.

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: O sistema DEVE resolver letras obedecendo estritamente à ordem canônica de 6 etapas.
- **FR-002**: O sistema DEVE suportar formatos de letra sincronizada (.lrc) e texto puro (.txt).
- **FR-003**: O sistema DEVE atualizar o destaque da linha ativa com base no tempo de reprodução com precisão mínima de 100ms.
- **FR-004**: O sistema DEVE permitir salto no áudio (seek) ao clicar diretamente em uma linha da letra sincronizada.
- **FR-005**: O sistema DEVE armazenar em cache local as letras obtidas de provedores externos.
- **FR-006**: O sistema DEVE exibir a origem da letra (local vs remoto) de maneira visível ao usuário.

### Key Entities

- **LyricsDocument**: Conjunto de linhas de letra com indicação de tipo (sincronizada ou texto puro), proveniência e metadados.
- **LyricLine**: Timestamp de início e texto da estrofe/verso.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Leitura e renderização de arquivo `.lrc` local ocorrem em menos de 50ms.
- **SC-002**: Scroll automático suave mantendo a linha ativa centralizada sem sobressaltos visuais.
- **SC-003**: Zero requisições de rede executadas quando o arquivo local (embutido ou .lrc/.txt) já contiver a letra.

---

## Assumptions

- O engine de playback emite eventos regulares de posição de áudio (posição em milissegundos).
- Letras baixadas remotamente respeitam termos de uso e são mantidas apenas em cache de leitura do usuário.
