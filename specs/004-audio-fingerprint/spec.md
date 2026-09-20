# FEATURE SPEC: 004 — Audio Fingerprint & Music Recognition

**Feature Branch**: `004-audio-fingerprint`  
**Created**: 2026-09-20  
**Status**: Draft  
**Input**: Baseado em `FEATURES_SUGERIDAS_SPEC_KIT.md` (Feature 004)

---

## 1. META IMUTÁVEL (Global Goal)

> **Problema de Negócio:** Arquivos podem estar sem tags, com nomes genéricos ou metadata incorreta. O usuário precisa reconhecer a música pelo conteúdo do áudio, não apenas por filename/tag.
>
> **Definição de Sucesso:** A aplicação gera fingerprint local, consulta AcoustID quando habilitado, obtém candidatos e associa MusicBrainz IDs com confiança explícita, sem enviar o arquivo de áudio completo.
>
> **Regra de Ouro:** Resultado de reconhecimento é candidato, nunca alteração automática.

---

## 2. CONTRATOS & LIMITES DA ARQUITETURA

* **Projetos Afetados na Solution (.sln):**
  - Módulo nativo/gerenciado de geração de Chromaprint, serviço cliente de AcoustID e componentes de UI de reconhecimento.
* **Tipos/Serviços Existentes que DEVEM ser reutilizados:**
  - Modelos de `Track` e persistência de IDs externos estabelecidos na Feature 003.
  - Mecanismo de decodificação de áudio para alimentação do algoritmo de fingerprinting.
  - Sistema de configuração segura de API Keys e políticas de privacidade.
* **Convenções Obrigatórias:**
  - O cálculo do fingerprint acústico (Chromaprint) deve ocorrer **100% localmente**.
  - O arquivo de áudio bruto **JAMAIS** deve ser transmitido pela rede; apenas o fingerprint e a duração são enviados.
  - Provedor AcoustID é opcional e deve poder ser desativado pelo usuário.
  - Respeitar estritamente o rate limit da API pública do AcoustID (ex: máximo de 3 requisições por segundo por cliente).
  - Toda sugestão deve exibir pontuação de confiança (confidence score) e MusicBrainz Recording ID associado.
  - Nenhuma alteração pode ser gravada automaticamente no arquivo sem validação do usuário.

---

## 3. FATIAS VERTICAIS DE IMPLEMENTAÇÃO (Max 3 Slices)

### Slice 1: Fingerprint Local (Chromaprint Engine)
- **Meta Imutável repetida:** Extrair a impressão digital acústica e duração exata do arquivo local sem transmitir nenhum dado pela rede.
- **Escopo ponta a ponta:** Integrar biblioteca de Chromaprint (gerenciada ou wrapper nativo seguro) ao pipeline de áudio; extrair o fingerprint dos primeiros ~120 segundos de áudio com precisão determinística.
- **Reutilização obrigatória:** Decodificador de áudio existente.
- **Teste obrigatório:** Testes unitários com faixas conhecidas verificando consistência e determinismo do fingerprint gerado.
- **Validação Local:** `dotnet build` + testes de geração de Chromaprint.

### Slice 2: AcoustID Recognition & Rate Limiting
- **Meta Imutável repetida:** Consultar o serviço AcoustID de forma resiliente, respeitando rate limits e caching para obter candidatos com score de confiança.
- **Escopo ponta a ponta:** Implementar cliente HTTP para AcoustID com User-Agent configurado, gerenciador de rate limiting (token bucket / semaphore), cache de respostas em disco/memória e mapeamento para candidatos do MusicBrainz.
- **Reutilização obrigatória:** HttpClientFactory e camada de cache existente.
- **Teste obrigatório:** Testes de integração com mocks simulando respostas de sucesso, rate limit 429, rede indisponível e múltiplos candidatos.
- **Validação Local:** `dotnet build` + testes da suite de reconhecimento de rede.

### Slice 3: UI, Seleção de Candidatos e Regressão
- **Meta Imutável repetida:** Permitir ao usuário disparar o reconhecimento de uma faixa, visualizar os candidatos com suas respectivas pontuações de confiança e selecionar ou descartar a correspondência.
- **Escopo ponta a ponta:** Construir experiência visual no Track Inspector ou menu da biblioteca para "Identificar Música via Áudio", exibindo barra de progresso, lista de candidatos classificados por pontuação de confiança e botão para vincular IDs externos à faixa.
- **Reutilização obrigatória:** Janela/painel do Track Inspector (Feature 003).
- **Teste obrigatório:** Teste de fluxo completo de reconhecimento e vinculação na UI.
- **Validação Final:** Solution inteira compilando e testes passando.

---

## 4. GATES DE VALIDAÇÃO (.NET Toolchain)

```powershell
dotnet restore
dotnet build --configuration Release --warnaserror
dotnet test --configuration Release --no-build
```

Nenhuma tarefa pode ser marcada como concluída se arquivos de áudio completos forem enviados via rede ou se resultados de API modificarem tags automaticamente.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Identificação Acústica de Faixas Sem Tags (Priority: P1)
Como usuário com arquivos nomeados como "track_01.mp3" sem artista ou título, quero clicar em "Reconhecer Música" e obter o nome correto baseado exclusivamente no som da gravação.

**Why this priority**: É a proposta de valor principal: resolver metadados perdidos sem digitação manual.

**Independent Test**: Executar o reconhecimento em um arquivo sem tags e com nome alterado; conferir se o AcoustID retorna a gravação correta com score superior a 80%.

**Acceptance Scenarios**:
1. **Given** um arquivo sem metadados, **When** o usuário solicita o reconhecimento acústico, **Then** o sistema extrai o fingerprint localmente e consulta a base AcoustID.
2. **Given** um resultado positivo da consulta, **When** os candidatos são retornados, **Then** são exibidos título, artista e pontuação de confiança.

---

### User Story 2 - Respeito à Privacidade e Uso Offline (Priority: P2)
Como usuário preocupado com privacidade e consumo de dados, quero ter garantia de que minhas músicas não são enviadas para a nuvem e poder desligar completamente as consultas online.

**Why this priority**: Conformidade estrita com a Constituição do Resonance (Local-First e Privacy-First).

**Independent Test**: Monitorar o tráfego de rede durante o reconhecimento acústico para provar que apenas strings de fingerprint e durações são trafegadas.

**Acceptance Scenarios**:
1. **Given** o player sem conexão com a internet ou com a opção "Reconhecimento Online" desativada, **When** o usuário tentar reconhecer, **Then** o sistema avisa amigavelmente sobre o modo offline sem falhar.
2. **Given** uma requisição de reconhecimento ativa, **When** inspecionada, **Then** nenhum byte do conteúdo de áudio bruto é enviado para servidores externos.

---

### User Story 3 - Escolha Consciente Entre Múltiplos Candidatos (Priority: P3)
Como usuário organizando uma biblioteca com edições remasterizadas ou ao vivo, quero ver todos os candidatos sugeridos pelo AcoustID para escolher a versão exata do álbum.

**Why this priority**: Garante precisão e controle total nas mãos do usuário.

**Independent Test**: Reconhecer faixa clássica com várias versões e verificar se a interface apresenta a lista ordenada por relevância.

**Acceptance Scenarios**:
1. **Given** múltiplos lançamentos associados ao mesmo fingerprint, **When** a resposta é exibida, **Then** o usuário pode inspecionar cada candidato antes de aceitar.
2. **Given** uma lista de candidatos, **When** o usuário escolhe um ou descarta todos, **Then** apenas a escolha explícita é mantida na sessão.

---

### Edge Cases
- Gravações curtas (menos de 10 segundos) onde o fingerprint pode não ser único o suficiente.
- Áudios com ruído estático, gravações caseiras ou podcasts não cadastrados no banco de dados.
- Respostas 429 (Too Many Requests) do servidor AcoustID devendo acionar backoff exponencial automático.

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: O sistema DEVE calcular o fingerprint Chromaprint e a duração a partir do áudio decodificado localmente.
- **FR-002**: O sistema NUNCA DEVE realizar upload de arquivos de áudio para servidores externos.
- **FR-003**: O sistema DEVE consultar a API pública do AcoustID respeitando o limite de taxa de requisições.
- **FR-004**: O sistema DEVE atribuir e exibir uma pontuação de confiança percentual para cada candidato recebido.
- **FR-005**: O sistema DEVE obter e preservar o MusicBrainz Recording ID correspondente à gravação identificada.
- **FR-006**: O sistema DEVE tratar os resultados como sugestões, exigindo confirmação explícita do usuário para persistência.

### Key Entities

- **AcousticFingerprint**: Hash gerado pelo Chromaprint e duração correspondente em segundos.
- **RecognitionCandidate**: Candidato sugerido contendo título, artista, álbum, MusicBrainz ID e índice de confiança (0-100%).

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Geração do Chromaprint local concluída em menos de 1 segundo para arquivos de até 10 minutos.
- **SC-002**: 100% de conformidade de privacidade comprovada (zero uploads de payload de áudio).
- **SC-003**: Taxa de rejeição por rate limiting na API AcoustID inferior a 0,1% sob uso normal devido ao backoff implementado.

---

## Assumptions

- O usuário possui uma chave de API do AcoustID configurada ou a aplicação utiliza uma chave de aplicação devidamente registrada.
- As gravações comerciais mais comuns possuem dados correspondentes no banco comunitário do AcoustID/MusicBrainz.
