# FEATURE SPEC: 001 — Recursive Root Library Hardening

**Feature Branch**: `001-recursive-root-library`  
**Created**: 2026-09-20  
**Status**: Draft  
**Input**: Baseado em `FEATURES_SUGERIDAS_SPEC_KIT.md` (Feature 001)

---

## 1. META IMUTÁVEL (Global Goal)

> **Problema de Negócio:** O usuário deve apontar uma ou várias pastas raiz e ter todas as músicas válidas das subpastas indexadas automaticamente, sem cadastrar pasta por pasta e sem perder músicas por profundidade de diretório.
>
> **Definição de Sucesso:** Uma árvore com múltiplos níveis, raízes sobrepostas, arquivos inválidos e caminhos problemáticos é processada sem duplicatas, sem travar a UI e sem abortar o scan inteiro.
>
> **Regra de Ouro:** Melhorar o scanner e persistência existentes do Nagi. Não criar uma segunda biblioteca paralela.

---

## 2. CONTRATOS & LIMITES DA ARQUITETURA

* **Projetos Afetados na Solution (.sln):**
  - Projetos responsáveis pelo scanner de arquivos, persistência/banco de dados e ViewModels/Views da Biblioteca.
* **Tipos/Serviços Existentes que DEVEM ser reutilizados:**
  - Modelagem atual de roots e tracks.
  - Serviço de persistência e acesso ao banco de dados SQLite/EF existente.
  - Mecanismo de threading e despacho para a thread de UI (WinUI DispatcherQueue).
* **Convenções Obrigatórias:**
  - Reutilizar a arquitetura do scanner existente, aprimorando sua robustez.
  - Definir política explícita de travessia para junções, links simbólicos e reparse points (evitando loops infinitos).
  - Controle de concorrência limitada (bounded concurrency) para evitar exaustão de I/O e memória.
  - Suporte completo a cancelamento cooperativo via `CancellationToken`.
  - Notificação de progresso transparente em tempo real para a UI.
  - Suporte a varredura incremental para evitar reprocessamento desnecessário.

---

## 3. FATIAS VERTICAIS DE IMPLEMENTAÇÃO (Max 3 Slices)

### Slice 1: Recursive Traversal Resiliente
- **Meta Imutável repetida:** Descobrir todos os arquivos de áudio válidos em qualquer nível de subpastas a partir de raízes fornecidas, sem travar diante de caminhos inválidos ou ciclos.
- **Escopo ponta a ponta:** Implementar travessia recursiva protegida contra ciclos (cycle protection), tratamento de diretórios inacessíveis (permissão negada), tratamento de caminhos longos e arquivos corrompidos.
- **Reutilização obrigatória:** Estruturas de diretório e opções de varredura existentes no Nagi.
- **Teste obrigatório:** Testes unitários com árvore sintética de diretórios simulando ciclos, permissões negadas e arquivos corrompidos.
- **Validação Local:** `dotnet build` + testes da suite de filesystem/traversal.

### Slice 2: Indexação, Persistência e Varredura Incremental
- **Meta Imutável repetida:** Indexar faixas descobertas de forma idempotente, sem duplicatas entre raízes sobrepostas, com suporte a atualização incremental e cancelamento consistente.
- **Escopo ponta a ponta:** Lógica de desduplicação por caminho canônico/hash, detecção de adições, modificações e exclusões (incremental scan), sincronização transacional com o banco de dados e cancelamento gracioso.
- **Reutilização obrigatória:** Repositório/serviço de persistência existente.
- **Teste obrigatório:** Testes de persistência incremental, desduplicação de raízes sobrepostas e cancelamento no meio do scan.
- **Validação Local:** `dotnet build` + testes de persistência da biblioteca.

### Slice 3: UI, Progresso e Regressão
- **Meta Imutável repetida:** Apresentar progresso contínuo na UI sem travamentos e disponibilizar controles de cancelamento e relatório de erros não-bloqueantes.
- **Escopo ponta a ponta:** Conectar ViewModel e View da biblioteca ao serviço de scan com despacho seguro para UI, permitindo adicionar múltiplas raízes, cancelar o scan, exibir contagem em tempo real e tratar erros pontuais sem abortar a operação.
- **Reutilização obrigatória:** Views/ViewModels de biblioteca existentes.
- **Teste obrigatório:** Teste ponta a ponta simulando a seleção de pastas e o fluxo completo de scan e cancelamento na UI.
- **Validação Final:** Solution inteira compilando e suíte completa de testes passando.

---

## 4. GATES DE VALIDAÇÃO (.NET Toolchain)

```powershell
dotnet restore
dotnet build --configuration Release --warnaserror
dotnet test --configuration Release --no-build
```

Nenhuma tarefa pode ser marcada como concluída se o scanner bloquear a UI, corromper o banco de dados ou duplicar faixas.

---

## 5. AGENT GUARDRAILS

1. Não criar um novo scanner do zero; evoluir o existente no Nagi.
2. Não executar operações síncronas de I/O na thread de UI.
3. Não ignorar exceções de permissão ou caminho longo; tratá-las graciosamente.
4. Manter o limite de 3 fatias verticais.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Varredura Recursiva Profunda e Resiliente (Priority: P1)
Como usuário com uma coleção musical organizada em várias subpastas profundas, quero apontar apenas a pasta raiz e ter todas as faixas legíveis encontradas sem que erros de permissão ou arquivos inválidos abortem a leitura.

**Why this priority**: É a essência da biblioteca musical local; sem descoberta robusta, as músicas não chegam ao player.

**Independent Test**: Apontar uma pasta contendo 5 níveis de subpastas e arquivos inválidos propositais, verificando se 100% das faixas válidas são descobertas.

**Acceptance Scenarios**:
1. **Given** uma pasta raiz com subpastas em múltiplos níveis, **When** a varredura for iniciada, **Then** todas as músicas válidas em todas as subpastas são descobertas.
2. **Given** uma subpasta inacessível ou arquivo ilegível, **When** o scanner encontrar o item, **Then** ele registra o aviso/erro sem interromper o restante da varredura.

---

### User Story 2 - Prevenção de Duplicatas e Varredura Incremental (Priority: P2)
Como usuário que adiciona novas músicas ou organiza pastas, quero reexecutar o scan rapidamente sem duplicar faixas e sem precisar reescanear arquivos inalterados.

**Why this priority**: Evita poluição da biblioteca e otimiza o tempo de inicialização e sincronização.

**Independent Test**: Rodar o scan em uma raiz, adicionar uma nova faixa e rodar novamente; verificar se apenas a nova faixa foi processada e nenhuma duplicata foi criada.

**Acceptance Scenarios**:
1. **Given** duas pastas raízes configuradas onde uma é subpasta da outra, **When** o scan for executado, **Then** nenhuma faixa é inserida em duplicidade no catálogo.
2. **Given** uma biblioteca já indexada, **When** o scan incremental for disparado, **Then** apenas arquivos modificados ou novos são processados no banco.

---

### User Story 3 - Feedback em Tempo Real e Cancelamento na UI (Priority: P3)
Como usuário com dezenas de milhares de músicas, quero ver o progresso do scan em tempo real e ter um botão para cancelar a operação sem travar o player.

**Why this priority**: Experiência do usuário responsiva e controle total sobre operações de longa duração.

**Independent Test**: Iniciar scan de pasta grande, observar atualização do contador na tela, acionar botão de cancelamento e verificar se o processo para imediatamente mantendo os dados parciais válidos.

**Acceptance Scenarios**:
1. **Given** um scan em andamento, **When** o usuário acompanha a interface, **Then** a UI permanece totalmente fluida e exibe a contagem de faixas e pasta atual.
2. **Given** um scan longo, **When** o usuário clica em "Cancelar", **Then** a varredura é encerrada graciosamente e os itens já persistidos permanecem utilizáveis.

---

### Edge Cases
- Links simbólicos e junções que apontam para diretórios pais (evitar loop infinito via detecção de reparse point).
- Caminhos que excedem o limite MAX_PATH do Windows (utilizar suporte a caminhos longos).
- Remoção de um disco externo ou pasta de rede no meio da varredura.

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: O sistema DEVE percorrer recursivamente todas as subpastas a partir de cada pasta raiz cadastrada.
- **FR-002**: O sistema DEVE detectar e desconsiderar links simbólicos cíclicos para evitar recursão infinita.
- **FR-003**: O sistema DEVE identificar raízes sobrepostas e desduplicar arquivos com base em caminho canônico.
- **FR-004**: O sistema DEVE suportar cancelamento imediato e seguro em qualquer momento da varredura.
- **FR-005**: O sistema DEVE emitir eventos de progresso com número de faixas encontradas e pasta atual sem travar a interface.
- **FR-006**: O sistema DEVE registrar falhas individuais de arquivos corrompidos sem interromper o processo global de scan.
- **FR-007**: O sistema DEVE persistir as alterações na base de dados de forma transacional e eficiente.

### Key Entities

- **LibraryRoot**: Representa o diretório raiz configurado pelo usuário, status de sincronização e data do último scan.
- **TrackRecord**: Representa o arquivo de áudio indexado, caminho físico absoluto, data de modificação e hash de unicidade.
- **ScanProgressReport**: Dados transitórios de progresso (pastas processadas, faixas adicionadas, erros, status ativo/cancelado).

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Árvore com 10.000 faixas distribuídas em 10 níveis de profundidade é escaneada sem falhas ou travamentos de UI.
- **SC-002**: Zero duplicatas geradas mesmo quando uma pasta raiz A contém a pasta raiz B.
- **SC-003**: O cancelamento do scan responde e interrompe o trabalho em menos de 1 segundo.
- **SC-004**: A UI mantém taxa de resposta consistente (sem congelamento de tela) durante todo o processo de varredura.

---

## Assumptions

- O usuário possui privilégios de leitura nas pastas configuradas.
- O banco de dados local SQLite/EF existente do Nagi será mantido e otimizado com transações em lote.
