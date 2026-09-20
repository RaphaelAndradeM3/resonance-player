# FEATURE SPEC: 005 — Online Metadata Enrichment

**Feature Branch**: `005-online-metadata`  
**Created**: 2026-09-20  
**Status**: Draft  
**Input**: Baseado em `FEATURES_SUGERIDAS_SPEC_KIT.md` (Feature 005)

---

## 1. META IMUTÁVEL (Global Goal)

> **Problema de Negócio:** Depois de reconhecer ou selecionar uma faixa, o usuário deve conseguir complementar tags incompletas usando fontes online confiáveis sem perder o controle sobre os dados locais.
>
> **Definição de Sucesso:** A aplicação obtém metadata online, preserva a origem de cada campo, faz cache respeitando provider e apresenta proposta de enriquecimento sem alterar o arquivo.
>
> **Regra de Ouro:** Dados remotos não substituem silenciosamente tags locais.

---

## 2. CONTRATOS & LIMITES DA ARQUITETURA

* **Projetos Afetados na Solution (.sln):**
  - Módulos de clientes de provedores externos (MusicBrainz, Last.fm, TheAudioDB), camada de cache e modelo de enriquecimento.
* **Tipos/Serviços Existentes que DEVEM ser reutilizados:**
  - Identificadores obtidos na Feature 004 (MusicBrainz Recording ID).
  - Provedores ou infraestrutura HTTP já existentes no Nagi.
  - Modelos de faixa e metadados da Feature 003.
* **Provedores Suportados e Regras de Integração:**
  - **MusicBrainz:** consulta primária de lançamentos, faixas, artistas e ISRC com User-Agent identificando o Resonance e rate limit de 1 req/s.
  - **Cover Art Archive / Provedores Adicionais:** obtenção de capa de álbum e metadados complementares respeitando licença e termos de uso.
* **Convenções Obrigatórias:**
  - Todo dado obtido deve manter sua etiqueta de origem (provenance) visível.
  - Cache local persistente para evitar requisições redundantes a provedores públicos.
  - Falha em um provedor externo não pode impactar o playback nem outros serviços.
  - Nenhum dado remoto substitui tags em disco de forma silenciosa ou automática.

---

## 3. FATIAS VERTICAIS DE IMPLEMENTAÇÃO (Max 3 Slices)

### Slice 1: MusicBrainz & External Providers Client
- **Meta Imutável repetida:** Consultar metadados detalhados de gravações e lançamentos na API do MusicBrainz e serviços complementares de forma segura e tolerante a falhas.
- **Escopo ponta a ponta:** Implementar cliente com User-Agent padronizado, respeitando rate limits (throttler de 1 req/s para MusicBrainz), política de retry inteligente e cache local em disco com expiração configurável.
- **Reutilização obrigatória:** Infraestrutura HTTP e serializadores JSON existentes.
- **Teste obrigatório:** Testes unitários com simulação de payloads reais do MusicBrainz, tratamento de timeouts e respeito a rate limits.
- **Validação Local:** `dotnet build` + testes da suite de provedores.

### Slice 2: Metadata Merge & Provenance Model
- **Meta Imutável repetida:** Criar modelo de dados unificado que combine tags locais existentes com as sugestões online, preservando o valor e a fonte de cada campo individual.
- **Escopo ponta a ponta:** Desenvolver motor de mesclagem (merge engine) que compara campo a campo (ex.: título local vs título remoto) e gera um objeto `EnrichmentProposal` com grau de confiança e procedência.
- **Reutilização obrigatória:** Modelo de metadados da Feature 003.
- **Teste obrigatório:** Testes de regras de merge cobrindo faixas completas, faixas com campos faltantes e discrepâncias ortográficas.
- **Validação Local:** `dotnet build` + testes de merge e proveniência.

### Slice 3: UI de Proposta de Enriquecimento e Regressão
- **Meta Imutável repetida:** Apresentar a proposta de enriquecimento de forma clara na interface, permitindo ao usuário comparar o que tem hoje com o que a web sugere, funcionando offline graciosamente.
- **Escopo ponta a ponta:** Construir visão na UI para visualização da proposta de enriquecimento (com badges indicando a fonte: ex. "Local", "MusicBrainz", "CoverArtArchive") e botão para avançar para a revisão/aplicação (Feature 006).
- **Reutilização obrigatória:** Componentes de diálogo e visualização do Track Inspector.
- **Teste obrigatório:** Teste de interface simulando consulta online, exibição da proposta e modo offline.
- **Validação Final:** Solution inteira compilando e testes passando.

---

## 4. GATES DE VALIDAÇÃO (.NET Toolchain)

```powershell
dotnet restore
dotnet build --configuration Release --warnaserror
dotnet test --configuration Release --no-build
```

Nenhuma tarefa pode ser marcada como concluída se dados de APIs externas forem gravados diretamente no disco sem passar pelo fluxo de revisão da Feature 006.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Obtenção de Dados Canônicos de Lançamento (Priority: P1)
Como colecionador de música, quero buscar os dados oficiais do álbum no MusicBrainz para padronizar o ano, gravadora, capa oficial e numeração de disco.

**Why this priority**: Constrói uma biblioteca rica e consistente a partir de fontes abertas e curadas.

**Independent Test**: Consultar faixa com MusicBrainz ID válido; verificar se o retorno traz título do álbum, ano original e arte de capa oficial.

**Acceptance Scenarios**:
1. **Given** uma faixa com identificador MusicBrainz, **When** o enriquecimento online é solicitado, **Then** as informações de lançamento e faixas são recuperadas com sucesso.
2. **Given** a resposta do provedor, **When** processada, **Then** o sistema armazena a resposta no cache local para reutilização futura.

---

### User Story 2 - Preservação da Proveniência dos Campos (Priority: P2)
Como usuário que possui anotações personalizadas nos comentários das músicas, quero ver exatamente quais campos o provedor online quer atualizar sem perder minhas anotações locais.

**Why this priority**: Evita que edições manuais do usuário sejam sobrescritas sem consentimento.

**Independent Test**: Simular merge onde a tag local possui comentários personalizados e a tag remota possui novos gêneros; verificar se o sistema preserva os campos distintos.

**Acceptance Scenarios**:
1. **Given** campos locais preenchidos, **When** a proposta é gerada, **Then** cada campo indica sua origem ("Local", "MusicBrainz", etc.).
2. **Given** um campo existente localmente, **When** o provedor não possuir informação sobre ele, **Then** o campo local é mantido inalterado na proposta.

---

### User Story 3 - Resiliência a Falhas de Rede e Respeito a Rate Limits (Priority: P3)
Como usuário em conexão instável ou lenta, quero que o player continue funcionando normalmente mesmo se os servidores externos demorarem para responder ou caírem.

**Why this priority**: Mantém a promessa de Local-First da Constituição.

**Independent Test**: Desconectar o cabo de rede durante a busca de metadados; verificar se a aplicação encerra a busca com mensagem discreta e mantém a reprodução sem estalos.

**Acceptance Scenarios**:
1. **Given** falta de conectividade, **When** o usuário solicita enriquecimento, **Then** o sistema notifica a indisponibilidade sem travar a interface.
2. **Given** múltiplas buscas consecutivas, **When** disparadas, **Then** o limitador de taxa garante o intervalo mínimo exigido pelas diretrizes do MusicBrainz.

---

### Edge Cases
- Álbuns com múltiplos volumes (discos) e faixas bônus em edições regionais específicas.
- Provedor retornando caracteres com acentuação ou alfabetos não-latinos (UTF-8 completo).
- Capas de álbuns indisponíveis no Cover Art Archive (manter fallback para capa local).

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: O sistema DEVE consultar a API do MusicBrainz utilizando cabeçalho User-Agent identificador e respeitando o limite de 1 requisição por segundo.
- **FR-002**: O sistema DEVE armazenar as respostas de provedores em cache local para evitar tráfego repetitivo de rede.
- **FR-003**: O sistema DEVE rastrear e expor a procedência (provenance) de cada campo de metadado obtido.
- **FR-004**: O sistema DEVE gerar uma proposta de enriquecimento (`EnrichmentProposal`) sem gravar nada no arquivo físico.
- **FR-005**: O sistema DEVE isolar erros de comunicação com provedores de modo que a reprodução local nunca seja interrompida.

### Key Entities

- **EnrichmentProposal**: Proposta contendo valores atuais vs valores sugeridos com metadados de procedência e confiança.
- **MetadataProvenance**: Enumeração e detalhe da origem do dado (LocalTag, MusicBrainz, LastFm, UserOverride).

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% das requisições enviadas ao MusicBrainz cumprem a política de User-Agent e taxa limite.
- **SC-002**: Respostas em cache respondem em menos de 10ms.
- **SC-003**: Zero gravações físicas efetuadas em disco nesta etapa de enriquecimento.

---

## Assumptions

- O usuário autorizou o uso de conexões online para consulta de metadados nas configurações.
- A aplicação possui acesso ao serviço do Cover Art Archive para artes de álbum abertas.
