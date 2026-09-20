# FEATURE SPEC: 010 — Modern Player UI / Classic Player Experience

**Feature Branch**: `010-modern-player-ui`  
**Created**: 2026-09-20  
**Status**: Draft  
**Input**: Baseado em `FEATURES_SUGERIDAS_SPEC_KIT.md` (Feature 010)

---

## 1. META IMUTÁVEL (Global Goal)

> **Problema de Negócio:** O usuário precisa de uma interface moderna, rápida e informativa que reúna biblioteca, playback, equalizador, letras, spectrum e detalhes sem perder a eficiência de players clássicos.
>
> **Definição de Sucesso:** O usuário navega, toca, busca, inspeciona, ajusta EQ, vê letras e spectrum através de uma UI WinUI coerente, responsiva e integrada às capabilities existentes.
>
> **Regra de Ouro:** UI não implementa regra de negócio nem duplica serviços.

---

## 2. CONTRATOS & LIMITES DA ARQUITETURA

* **Projetos Afetados na Solution (.sln):**
  - Projeto principal de apresentação WinUI 3 (App, MainWindow, Views, Navigation, Controls, ViewModels).
* **Tipos/Serviços Existentes que DEVEM ser reutilizados:**
  - Todas as capacidades criadas nas Features anteriores:
    - Feature 001 (Scanner de Pastas/Raízes)
    - Feature 002 (Formatos de Áudio)
    - Feature 003 (Inspector de Faixas)
    - Feature 004/005/006 (Enriquecimento e Edição de Tags)
    - Feature 007 (Letras Sincronizadas)
    - Feature 008 (Equalizador & Presets)
    - Feature 009 (Visualizador de Espectro FFT)
* **Áreas Integradas de Navegação:**
  - Home / Início
  - Biblioteca (Músicas, Álbuns, Artistas, Gêneros, Pastas, Playlists)
  - Now Playing (Barra de transporte fixada ou modo expandido com arte em destaque)
  - Painéis Acopláveis / Modais (Lyrics, Spectrum, Equalizer, Details/Inspector)
  - Configurações (Settings)
* **Convenções Obrigatórias:**
  - Separação estrita MVVM: a camada de UI apenas consome serviços injetados via DI.
  - Zero duplicação de regras de negócio ou chamadas diretas de filesystem em code-behind.
  - Navegação fluida e responsiva com atalhos de teclado globais/locais (Play/Pause, Próxima, Volume, Mute).
  - Suporte completo aos temas Claro, Escuro e Padrão do Sistema (Windows 11 Mica/Acrylic).

---

## 3. FATIAS VERTICAIS DE IMPLEMENTAÇÃO (Max 3 Slices)

### Slice 1: Now Playing Shell & Painel de Transporte
- **Meta Imutável repetida:** Oferecer barra e painel de reprodução de alta fidelidade com exibição de capa, metadados em tempo real, scrubber de progresso, controle de volume e botões de atalho para recursos extras.
- **Escopo ponta a ponta:** Desenvolver barra inferior de reprodução integrada aos estados do player (Play, Pause, Stop, Seek, Repeat, Shuffle), com visualização de arte de capa, título, artista, tempo decorrido/total e botões rápidos para abrir Lyrics, EQ, Spectrum e Detalhes.
- **Reutilização obrigatória:** ViewModel e motor de reprodução da baseline.
- **Teste obrigatório:** Testes unitários de comandos de reprodução, bindings e formatação de tempo no ViewModel.
- **Validação Local:** `dotnet build` + testes de ViewModel da barra de reprodução.

### Slice 2: Library, Navegação & Folders Experience
- **Meta Imutável repetida:** Navegar com extrema agilidade pela coleção musical através de visões especializadas (Músicas, Álbuns, Artistas, Pastas) com pesquisa e filtros instantâneos.
- **Escopo ponta a ponta:** Construir o shell de navegação lateral (NavigationView) e as páginas de listagem com virtualização de dados (para suportar dezenas de milhares de faixas sem perda de FPS), caixas de busca rápida e navegação estruturada por diretórios físicos (Folders view).
- **Reutilização obrigatória:** Serviço de banco de dados e repositórios da Feature 001.
- **Teste obrigatório:** Testes de filtragem, paginação/virtualização e agrupamento na biblioteca.
- **Validação Local:** `dotnet build` + testes de navegação e busca.

### Slice 3: Integrated Player Experience, Acessibilidade & Regressão
- **Meta Imutável repetida:** Consolidar a experiência completa unindo todas as janelas e painéis (EQ, Lyrics, Spectrum, Tag Editor) em uma aplicação coesa, acessível por teclado e testada de ponta a ponta.
- **Escopo ponta a ponta:** Integrar atalhos de teclado (Espaço, setas, Alt+Enter, Ctrl+F), gerenciar estados de painéis acopláveis/flutuantes, suporte a alto contraste e navegação por teclado (Tab/Focus), e testes de regressão de todo o fluxo do player.
- **Reutilização obrigatória:** Todos os componentes finalizados nas features 000 a 009.
- **Teste obrigatório:** Teste de jornada completa: abrir app -> buscar álbum -> tocar faixa -> abrir EQ -> ligar spectrum -> ver letra -> abrir inspector.
- **Validação Final:** Solution inteira compilando e testes de integração de ponta a ponta passando.

---

## 4. GATES DE VALIDAÇÃO (.NET Toolchain)

```powershell
dotnet restore
dotnet build --configuration Release --warnaserror
dotnet test --configuration Release --no-build
```

Nenhuma tarefa pode ser marcada como concluída se houver vazamento de memória na UI, congelamento de thread de interface ou duplicação de regras nos ViewModels.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Experiência Central de Reprodução ("Now Playing") Fluida (Priority: P1)
Como usuário ouvindo música durante o trabalho, quero uma barra de transporte moderna, com controles precisos, arte do álbum legível e acesso em um clique a letras e equalizador.

**Why this priority**: É o ponto focal permanente da interação do usuário com qualquer player de música.

**Independent Test**: Executar uma música; conferir atualização em tempo real de scrubber, tempo restante, arte do álbum e alternância de Play/Pause instantânea.

**Acceptance Scenarios**:
1. **Given** uma faixa tocando, **When** o tempo avança, **Then** o slider de progresso reflete a posição e permite arrastar para qualquer ponto com resposta imediata.
2. **Given** a barra de transporte, **When** o usuário clica no ícone de "Equalizador", **Then** o painel de EQ da Feature 008 abre sem interromper o som.

---

### User Story 2 - Navegação Instantânea em Grandes Bibliotecas (Priority: P2)
Como colecionador com 50.000 músicas, quero rolar a lista de músicas ou álbuns suavemente a 60 FPS e encontrar qualquer faixa digitando na busca sem engasgos.

**Why this priority**: Garante que o Resonance tenha o desempenho de players clássicos nativos sem o peso de navegadores web embutidos (Electron).

**Independent Test**: Carregar biblioteca com 50.000 itens; realizar scroll contínuo e busca por termo; verificar fluidez visual e resposta da busca em menos de 200ms.

**Acceptance Scenarios**:
1. **Given** uma grande biblioteca carregada, **When** o usuário rola a lista rapidamente, **Then** a virtualização de UI mantém a rolagem suave sem travar a interface.
2. **Given** a caixa de pesquisa, **When** o usuário digita o nome de um artista, **Then** a lista filtra os resultados em tempo real.

---

### User Story 3 - Controle Total por Teclado e Acessibilidade (Priority: P3)
Como power user, quero controlar a reprodução, pausar, avançar faixa e abrir propriedades usando apenas teclas de atalho sem encostar no mouse.

**Why this priority**: Produtividade e ergonomia consagradas pelos melhores players desktop clássicos.

**Independent Test**: Navegar e operar o player usando exclusivamente teclas `Espaço` (pause/play), `Ctrl+Right` (próxima), `Ctrl+Up` (volume) e `Alt+Enter` (propriedades).

**Acceptance Scenarios**:
1. **Given** o player em foco, **When** o usuário pressiona a barra de espaço, **Then** o áudio alterna entre pausado e reproduzindo.
2. **Given** uma faixa selecionada na lista, **When** o atalho `Alt+Enter` for acionado, **Then** o Track Inspector correspondente é aberto.

---

### Edge Cases
- Redimensionamento da janela para tamanhos compactos (mini-player mode ou modo responsivo estreito).
- Troca dinâmica de tema claro/escuro no Windows durante a reprodução.
- Biblioteca completamente vazia no primeiro uso (deve exibir tela amigável convidando a adicionar pastas raízes).

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: O sistema DEVE disponibilizar layout com barra de transporte, barra lateral de navegação e área de conteúdo com suporte aos temas do Windows.
- **FR-002**: O sistema DEVE renderizar listas de faixas, álbuns e artistas utilizando virtualização de UI para alta performance.
- **FR-003**: O sistema DEVE disponibilizar visualização em árvore de diretórios físicos (Folders view).
- **FR-004**: O sistema DEVE integrar os painéis de Letras, Equalizador, Espectro FFT e Inspector de forma harmoniosa.
- **FR-005**: O sistema DEVE suportar atalhos de teclado padrão para todas as operações essenciais de transporte e navegação.
- **FR-006**: O sistema DEVE respeitar o padrão MVVM, isolando toda lógica de negócio em serviços injetados.

### Key Entities

- **NavigationItem**: Identificador de página, ícone, título e ViewModel associado.
- **PlayerShellState**: Estado visual da janela (tamanho, painéis abertos/fechados, modo compacto/expandido, tema ativo).

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Rolagem da biblioteca mantém taxa estável de 60 FPS com até 50.000 faixas indexadas.
- **SC-002**: Busca na biblioteca responde e filtra resultados em menos de 200ms.
- **SC-003**: 100% dos atalhos de transporte e recursos das features 001 a 009 acessíveis via interface e teclado.

---

## Assumptions

- A plataforma de destino é Windows 10/11 utilizando WinUI 3 e Windows App SDK.
- Todos os serviços de backend (scanner, playback, EQ, FFT, metadata) foram implementados conforme suas respectivas specs.
