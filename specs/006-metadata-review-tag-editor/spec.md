# FEATURE SPEC: 006 — Metadata Review, Tag Editor & File Update

**Feature Branch**: `006-metadata-review-tag-editor`  
**Created**: 2026-09-20  
**Status**: Draft  
**Input**: Baseado em `FEATURES_SUGERIDAS_SPEC_KIT.md` (Feature 006)

---

## 1. META IMUTÁVEL (Global Goal)

> **Problema de Negócio:** O usuário precisa atualizar MP3/FLAC/outros formatos com metadata melhor, mas sem risco de sobrescrever dados corretos ou corromper arquivos.
>
> **Definição de Sucesso:** Antes de escrever tags, a aplicação mostra um diff campo a campo, permite escolher alterações e só grava após confirmação explícita.
>
> **Regra de Ouro:** Nenhuma escrita automática em arquivo por resultado de API.

---

## 2. CONTRATOS & LIMITES DA ARQUITETURA

* **Projetos Afetados na Solution (.sln):**
  - Módulos de escrita de tags físicas, sincronização com banco de dados local e UI de Review/Tag Editor.
* **Tipos/Serviços Existentes que DEVEM ser reutilizados:**
  - Gravador de tags existente (ex.: TagLib# ou TagWriter nativo do Nagi).
  - Repositório de persistência para atualização imediata dos registros do banco após escrita física bem-sucedida.
  - Modelo de proposta de enriquecimento da Feature 005.
* **Convenções Obrigatórias:**
  - **Zero escrita automática:** Nenhuma chamada a API ou processo de background pode modificar tags no arquivo físico sem intervenção explícita do usuário.
  - **Fluxo Obrigatório:** Review -> Diff Visual -> Seleção Campo a Campo -> Confirmação Explícita -> Gravação Segura.
  - **Gravação Atômica e Segura:** Escrita em arquivo temporário antes de substituir o original para prevenir perda de dados em caso de falha de energia ou I/O.
  - Tratamento adequado para arquivos somente-leitura (read-only), arquivos bloqueados por outro processo ou permissão negada.
  - Atualização síncrona do catálogo da biblioteca/banco logo após a gravação bem-sucedida.

---

## 3. FATIAS VERTICAIS DE IMPLEMENTAÇÃO (Max 3 Slices)

### Slice 1: Metadata Diff & Seletor de Alterações
- **Meta Imutável repetida:** Comparar o estado atual do arquivo físico com o estado proposto e permitir ligar/desligar cada campo individualmente para aplicação.
- **Escopo ponta a ponta:** Construir mecanismo de cálculo de diferenças (diff) com suporte a campos de texto, números, gêneros múltiplos e imagem de capa, gerando um plano de aplicação seletivo (`TagWritePlan`).
- **Reutilização obrigatória:** Modelos de tags e proposta de enriquecimento das Features 003 e 005.
- **Teste obrigatório:** Testes unitários com matrizes de diff (campos idênticos, campos modificados, campos adicionados, campos removidos).
- **Validação Local:** `dotnet build` + testes da suite de diff.

### Slice 2: Safe Tag Write Engine (Gravação Segura e Atômica)
- **Meta Imutável repetida:** Gravar as tags selecionadas de forma atômica no arquivo físico preservando o arquivo original contra corrupção em caso de erro, e atualizando a persistência.
- **Escopo ponta a ponta:** Implementar rotina de gravação que escreve em arquivo temporário `.tmp`, valida a integridade do áudio resultante, substitui atomicamente o original e sincroniza o banco de dados do Resonance.
- **Reutilização obrigatória:** Escritor de tags e repositório de faixas existente.
- **Teste obrigatório:** Testes de round-trip (ler -> diff -> gravar -> reler) em MP3, FLAC e M4A, incluindo simulação de falha de escrita forçada.
- **Validação Local:** `dotnet build` + testes de integridade de arquivo e escrita.

### Slice 3: UI do Tag Editor, Diálogo de Revisão e Regressão
- **Meta Imutável repetida:** Disponibilizar uma interface rica onde o usuário revisa o diff, edita campos manualmente se desejar, visualiza avisos de arquivo travado e confirma a gravação.
- **Escopo ponta a ponta:** Criar diálogo de edição de tags e revisão de metadados com checkboxes campo a campo, visualização antes/depois, tratamento de erros de permissão e atualização instantânea na UI do player.
- **Reutilização obrigatória:** Diálogos e estilos WinUI existentes.
- **Teste obrigatório:** Teste de integração ponta a ponta do fluxo de edição manual e aplicação de enriquecimento na UI.
- **Validação Final:** Solution inteira compilando e testes passando.

---

## 4. GATES DE VALIDAÇÃO (.NET Toolchain)

```powershell
dotnet restore
dotnet build --configuration Release --warnaserror
dotnet test --configuration Release --no-build
```

Nenhuma tarefa pode ser marcada como concluída se um arquivo de áudio original for truncado, corrompido ou perder dados em caso de falha de gravação.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Revisão e Confirmação de Enriquecimento Automático (Priority: P1)
Como usuário que buscou dados online da música, quero ver um comparativo lado a lado (Antes vs Depois) e escolher exatamente quais campos aplicar no arquivo de áudio.

**Why this priority**: É a proteção definitiva contra sobrescrita indevida de dados do usuário.

**Independent Test**: Selecionar proposta com 5 campos modificados, desmarcar o campo "Ano", confirmar a gravação e verificar que apenas os 4 campos marcados foram alterados no arquivo.

**Acceptance Scenarios**:
1. **Given** uma proposta de metadados, **When** o diálogo de revisão é exibido, **Then** o usuário vê os valores atuais e sugeridos para cada campo lado a lado.
2. **Given** a tela de revisão, **When** o usuário clica em "Gravar Alterações", **Then** apenas os campos explicitamente selecionados são gravados fisicamente.

---

### User Story 2 - Edição Manual de Tags por Faixa (Priority: P2)
Como usuário meticuloso, quero corrigir manualmente a grafia de um artista ou ajustar o número da faixa diretamente pelo editor de tags do player.

**Why this priority**: Permite correções manuais rápidas sem a necessidade de abrir softwares externos de etiquetagem (ex: Mp3tag).

**Independent Test**: Abrir o editor de tags em uma música, alterar o título, salvar e conferir o arquivo em um leitor de tags independente.

**Acceptance Scenarios**:
1. **Given** qualquer faixa na biblioteca, **When** o usuário aciona "Editar Tags", **Then** um formulário editável com os campos da música é apresentado.
2. **Given** campos alterados manualmente, **When** salvo, **Then** o arquivo em disco e o banco de dados são atualizados imediatamente.

---

### User Story 3 - Resiliência e Proteção Contra Corrupção de Arquivos (Priority: P3)
Como usuário com arquivos valiosos, quero ter certeza absoluta de que, se o computador desligar ou a gravação falhar por falta de espaço, meu arquivo de áudio original não será perdido.

**Why this priority**: Segurança e integridade de dados são inegociáveis.

**Independent Test**: Simular erro de I/O durante o processo de escrita; verificar que o arquivo de áudio original permanece intacto e legível.

**Acceptance Scenarios**:
1. **Given** um arquivo marcado como somente-leitura ou bloqueado, **When** a gravação for tentada, **Then** o sistema exibe mensagem de erro clara sem corromper o arquivo.
2. **Given** uma falha no meio da gravação, **When** o erro ocorre, **Then** o arquivo temporário é limpo e o original é preservado.

---

### Edge Cases
- Arquivo sendo reproduzido no momento da gravação (o engine de áudio deve liberar o handle do arquivo ou gerenciar o lock adequadamente).
- Arquivo em partição com espaço em disco insuficiente para gerar a cópia temporária.
- Tags com caracteres especiais ou emojis no título/artista.

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: O sistema DEVE apresentar visão diferencial campo a campo antes de qualquer escrita de metadados.
- **FR-002**: O sistema DEVE permitir seleção e deseleção individual de cada atributo a ser gravado.
- **FR-003**: O sistema DEVE gravar as alterações fisicamente no container do arquivo de áudio (ID3v2, Vorbis Comments, etc.).
- **FR-004**: O sistema DEVE utilizar técnica de escrita atômica (arquivo temporário seguido de substituição segura).
- **FR-005**: O sistema DEVE atualizar imediatamente a persistência do banco de dados após a gravação física.
- **FR-006**: O sistema NUNCA DEVE gravar alterações em arquivos sem confirmação prévia e explícita do usuário.

### Key Entities

- **TagDiffRecord**: Registro individual de campo com valor original, novo valor, status de alteração e flag de inclusão.
- **TagWritePlan**: Conjunto de instruções validadas para aplicação segura em um arquivo de áudio.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% das escritas de tags preservam a integridade do stream de áudio (zero corrupção).
- **SC-002**: O banco de dados local reflete os novos dados em menos de 50ms após a confirmação da gravação.
- **SC-003**: 100% de conformidade com a Regra de Ouro (zero escritas automáticas sem confirmação).

---

## Assumptions

- O usuário possui permissão de escrita no sistema de arquivos para os diretórios onde as músicas residem.
- O engine de playback fecha handles de arquivo ou permite compartilhamento de leitura durante a gravação.
