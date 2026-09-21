# FEATURE SPEC: 003 — Track Inspector & Local Metadata

**Feature Branch**: `003-track-inspector-local-metadata`  
**Created**: 2026-09-20  
**Status**: Draft  
**Input**: User description: "FEATURE 003 — Track Inspector & Local Metadata"

---

## 1. META IMUTÁVEL (Global Goal)

> **Problema de Negócio:** O usuário precisa visualizar com clareza tanto as tags musicais quanto os detalhes técnicos reais do arquivo que está tocando ou foi indexado na biblioteca.
>
> **Definição de Sucesso:** A aplicação mostra metadata local, propriedades técnicas completas, identificadores disponíveis e a origem dos dados para qualquer faixa suportada, com carregamento assíncrono e sem travamentos.
>
> **Regra de Ouro:** Reutilizar o mecanismo atual de leitura de metadata antes de adicionar qualquer novo parser. Não duplicar abstrações. Não alterar tags automaticamente sem confirmação do usuário.

---

## Clarifications

### Session 2026-09-20

- Q: Como o Track Inspector deve ser apresentado na interface gráfica do player? (FR-007) → A: Painel lateral retrátil (Collapsible Side Panel) acoplado à direita da janela principal, permitindo abrir e fechar sem cobrir a biblioteca ou o player.
- Q: Como o Track Inspector deve se comportar quando o usuário selecionar múltiplas faixas na biblioteca? (FR-011) → A: Exibir os detalhes da primeira faixa selecionada e fornecer controles de navegação rápida (< Anterior / Próxima >) no topo do painel com indicador numérico para alternar entre as faixas da seleção.
- Q: Como o usuário deve poder interagir com a arte da capa exibida no Track Inspector? (FR-004) → A: Exibir miniatura nítida no painel com ação de clique para expandir em tamanho original (LightBox) e botão de ação para exportar/salvar a imagem no disco.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Inspeção Técnica Profunda e Fidelidade do Formato (Priority: P1)

Como ouvinte exigente e audiófilo, quero abrir as propriedades detalhadas de uma faixa para conferir com exatidão suas especificações técnicas de áudio (codec, sample rate, bit depth, canais, bitrate CBR/VBR, formato de contêiner e tamanho em disco), sabendo se estou ouvindo um arquivo Lossless/Hi-Res de alta fidelidade ou um arquivo com perdas (lossy).

**Why this priority**: É o valor central e diferenciador do Resonance. Permite que o usuário compreenda exatamente a qualidade de codificação do arquivo e a pureza do fluxo sonoro que está sendo reproduzido.

**Independent Test**: Reproduzir ou selecionar arquivos locais de diversos formatos (ex.: FLAC 24-bit/96kHz, MP3 320kbps CBR, Opus VBR, WAV PCM) e abrir o Inspector; verificar se todas as grandezas físicas e técnicas de codificação são calculadas e renderizadas com fidelidade.

**Acceptance Scenarios**:

1. **Given** uma faixa em reprodução no player, **When** o usuário aciona o Track Inspector, **Then** as especificações técnicas reais (codec de áudio, formato de contêiner, taxa de amostragem em Hz/kHz, profundidade de bits, quantidade e layout de canais, taxa de bits em kbps, modo de bitrate e tamanho do arquivo) são apresentadas de forma clara e legível.
2. **Given** um arquivo com taxa de bits variável (VBR), **When** inspecionado, **Then** o sistema indica claramente o modo VBR juntamente com a taxa média estimada, sem exibir valores estáticos incorretos.
3. **Given** um arquivo em formato compactado sem profundidade de bits explícita no contêiner (ex.: MP3 ou AAC), **When** inspecionado, **Then** o sistema trata graciosamente a ausência dessa informação sem exibir valores enganosos ou nulos.

---

### User Story 2 - Leitura Completa de Tags Musicais, Letras e Capa de Álbum (Priority: P2)

Como usuário e colecionador musical, quero conferir todos os metadados artísticos e editoriais embutidos no arquivo (título, artista, artista do álbum, álbum, número da faixa e do disco, ano/data, gênero, compositor, comentários, código ISRC, valores ReplayGain e presença de letras), além de visualizar a arte da capa em alta resolução.

**Why this priority**: Permite que os usuários auditem e apreciem os detalhes de catalogação de seus álbuns, verificando a consistência dos dados da coleção local.

**Independent Test**: Inspecionar faixas com metadados completos e conferir a exibição correta de cada campo de texto, indicadores de ganho de volume (ReplayGain), presença de letras embutidas e renderização nítida da arte de capa.

**Acceptance Scenarios**:

1. **Given** uma faixa contendo tags musicais completas, **When** o Inspector é aberto, **Then** todos os campos (Title, Artist, Album Artist, Album, Track/Disc numbers, Year, Genre, Composer, Comment, ISRC) são listados sem truncamentos indevidos.
2. **Given** uma faixa que possui valores calculados de ReplayGain, **When** o Inspector for consultado, **Then** os valores de ganho de faixa (Track Gain), pico de faixa (Track Peak), ganho de álbum (Album Gain) e pico de álbum (Album Peak) são exibidos em decibéis (dB).
3. **Given** um arquivo de áudio com arte de capa embutida ou na pasta, **When** o usuário clica sobre a arte no Inspector, **Then** o sistema exibe a imagem em tamanho original (LightBox modal) e disponibiliza ação para exportar a arte para um arquivo de imagem no disco.
4. **Given** uma faixa que contenha letras embutidas no arquivo, **When** inspecionada, **Then** o Inspector indica a disponibilidade de letras e exibe um painel de pré-visualização do texto.

---

### User Story 3 - Proveniência dos Dados e Identificadores Externos (Priority: P3)

Como usuário, quero saber com transparência a origem de cada metadado exibido (se extraído diretamente das tags do arquivo local físico ou de enriquecimento externo) e visualizar identificadores externos conhecidos (como MusicBrainz IDs e AcoustID) quando disponíveis.

**Why this priority**: Cumpre os princípios de Local-First e integridade de dados (Princípio IV e VII da Constituição), assegurando que o usuário entenda o que pertence ao arquivo físico e o que é anotação externa.

**Independent Test**: Inspecionar faixas contendo apenas tags locais e faixas enriquecidas com identificadores externos; verificar que crachás/rótulos indicam a proveniência e que os IDs possuem opção de cópia rápida.

**Acceptance Scenarios**:

1. **Given** uma faixa com metadados provenientes do arquivo local, **When** o usuário inspeciona os campos, **Then** cada seção exibe a indicação de procedência como "Arquivo Local".
2. **Given** uma faixa associada a identificadores externos (ex.: MusicBrainz Recording ID, Release ID, Artist ID ou AcoustID), **When** o Inspector for visualizado, **Then** esses identificadores são exibidos com botão para copiar o código para a área de transferência.

---

### User Story 4 - Acesso Rápido, Teclas de Atalho e Sincronização Dinâmica (Priority: P4)

Como usuário navegando na biblioteca ou ouvindo uma playlist, quero poder inspecionar qualquer faixa imediatamente usando o teclado (`Alt+Enter`), menu de contexto ou botão na barra do player, com atualização automática dos dados se a música em reprodução mudar.

**Why this priority**: Fornece ergonomia, agilidade e fluidez no uso diário do aplicativo.

**Independent Test**: Selecionar itens na lista de músicas, na visualização de álbuns e na fila de reprodução, acionando o atalho `Alt+Enter` e conferindo a abertura instantânea e assíncrona do painel; verificar também a sincronização ao trocar de música no player.

**Acceptance Scenarios**:

1. **Given** qualquer faixa selecionada na biblioteca de músicas, **When** o usuário pressiona `Alt+Enter` ou escolhe "Inspecionar Faixa / Propriedades" no menu de contexto, **Then** o painel lateral retrátil à direita abre suavemente sem atrasar a interface gráfica e sem cobrir o conteúdo principal.
2. **Given** o Inspector aberto e focado na faixa que está sendo reproduzida ("Now Playing"), **When** a faixa atual termina e a próxima inicia, **Then** o Inspector atualiza suas informações automaticamente para a nova faixa caso a opção "Seguir reprodução" esteja ativa.
3. **Given** um arquivo em armazenamento lento ou com capa muito grande, **When** a solicitação de inspeção ocorre, **Then** o painel exibe indicador de carregamento não bloqueante e a navegação pela biblioteca permanece totalmente responsiva.
4. **Given** múltiplas faixas selecionadas na biblioteca de músicas, **When** o Inspector é acionado, **Then** as propriedades da primeira faixa selecionada são apresentadas, acompanhadas de controles de navegação rápida ("< Anterior" e "Próxima >") e indicador numérico (ex.: "1 de 12") no cabeçalho do painel para alternar entre as músicas da seleção.

---

### Edge Cases

- **Arquivos sem nenhuma tag gravada**: Quando o arquivo de áudio não possui nenhuma tag (ex.: WAV PCM simples ou MP3 cru), o sistema deve derivar o título do próprio nome do arquivo de forma clara, preenchendo os demais campos como "Desconhecido" ou "Não informado" de maneira elegante.
- **Capas de álbum gigantescas ou corrompidas**: Arquivos contendo imagens embutidas muito pesadas (ex.: 20MB+) ou com streams de imagem parcialmente corrompidos devem ser carregados e decodificados em background com limites seguros de memória e tratamento de falha, impedindo travamento da UI.
- **Mídias removíveis ou de rede desconectadas**: Se o arquivo inspecionado residir em um pendrive ou pasta de rede que se torne inacessível durante a leitura, o Inspector deve exibir uma mensagem descritiva de erro amigável sem lançar exceções não tratadas.
- **Múltiplos artistas ou múltiplos gêneros**: Faixas que contêm listas delimitadas de artistas (ex.: por ponto-e-vírgula ou barra) devem ser apresentadas de forma legível e estruturada.
- **Arquivos com caracteres especiais e codificações antigas**: Preservar a correta decodificação de tags com acentuação e alfabetos não-latinos (UTF-8, UTF-16, ISO-8859-1), evitando caracteres corrompidos (*mojibake*).

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: O sistema DEVE extrair e exibir todos os metadados editoriais fundamentais: Título, Artista(s), Artista do Álbum, Álbum, Número da Faixa, Total de Faixas do Álbum, Número do Disco, Total de Discos, Ano/Data de Lançamento, Gênero(s), Compositor, Comentários e código ISRC.
- **FR-002**: O sistema DEVE extrair e exibir os parâmetros técnicos do áudio: Caminho completo do arquivo, Tamanho formatado (Bytes, KB, MB), Duração exata (HH:MM:SS), Formato do contêiner, Codec de áudio, Taxa de bits (kbps), Modo de taxa de bits (CBR vs VBR), Taxa de amostragem (Hz / kHz), Profundidade de bits (quando aplicável ao formato) e Quantidade/Configuração de canais.
- **FR-003**: O sistema DEVE extrair e exibir as informações de ganho de volume ReplayGain (Track Gain, Track Peak, Album Gain e Album Peak) quando disponíveis nas tags do arquivo.
- **FR-004**: O sistema DEVE carregar e exibir a arte da capa embutida no arquivo ou presente na pasta de origem (`cover.jpg`, `folder.jpg`), informando suas dimensões em pixels e formato de imagem, com suporte a clique para ampliação em tamanho real (visualizador integrado/LightBox) e botão de ação para exportar/salvar a imagem em disco.
- **FR-005**: O sistema DEVE identificar a presença de letras embutidas no arquivo e disponibilizar uma visualização estática do texto.
- **FR-006**: O sistema DEVE indicar a proveniência dos metadados (Tags Locais vs Provedores Remotos) e exibir identificadores externos conhecidos (AcoustID, MusicBrainz IDs) com ação de cópia para a área de transferência.
- **FR-007**: O sistema DEVE disponibilizar o Track Inspector como um painel lateral retrátil (Collapsible Side Panel) acoplado à direita da janela principal, acessível através de múltiplos pontos de entrada: menu de contexto ("Inspecionar Faixa / Propriedades"), atalho de teclado `Alt+Enter` e botão na barra de reprodução (Now Playing), mantendo a navegação da biblioteca visível e interativa.
- **FR-008**: O sistema DEVE executar toda a leitura de metadados e decodificação de imagem em segundo plano de forma assíncrona, assegurando que a thread de interface nunca seja bloqueada.
- **FR-009**: O sistema DEVE suportar modo de acompanhamento dinâmico ("Seguir reprodução"), atualizando os dados do Inspector conforme novas faixas são reproduzidas.
- **FR-010**: O sistema NÃO DEVE realizar alterações destrutivas ou gravações automáticas de tags no arquivo físico nesta funcionalidade (escopo reservado à Feature 006 com workflow explícito de Review, Diff e Confirmação).
- **FR-011**: Quando múltiplas faixas estiverem selecionadas na biblioteca, o Inspector DEVE exibir as propriedades da primeira faixa selecionada e fornecer controles de navegação rápida ("< Anterior" e "Próxima >", acompanhado de contador numérico "X de N") para alternar a inspeção entre todas as faixas selecionadas sem fechar o painel.

### Key Entities

- **TrackTechnicalDetails**: Entidade que encapsula as propriedades de baixo nível do arquivo de áudio (formato de contêiner, codec, taxa de amostragem, profundidade de bits, canais, taxa de bits, modo CBR/VBR, duração, tamanho e caminho físico).
- **TrackTagDetails**: Entidade que encapsula as informações editoriais e artísticas da faixa (título, artistas, álbum, numeração de faixa/disco, ano, gêneros, compositor, comentários, ISRC, dados ReplayGain, letras e capa).
- **TrackInspectorViewData**: Modelo unificado de apresentação que agrupa os dados técnicos, tags musicais, atributos de proveniência e identificadores externos para visualização na interface de usuário.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: O painel do Track Inspector abre e apresenta os dados iniciais da faixa em menos de 150 milissegundos para arquivos locais.
- **SC-002**: 100% dos 23 atributos de metadados e propriedades técnicas definidos nos limites da arquitetura são suportados e exibidos pelo modelo de apresentação.
- **SC-003**: Zero travamentos ou bloqueios da interface de usuário (> 16ms bloqueando a thread de UI) durante a leitura e renderização de propriedades e capas de áudio.
- **SC-004**: 100% de clareza na procedência dos dados, sem qualquer campo ambíguo quanto a ser originado localmente ou externamente.
- **SC-005**: O usuário consegue abrir o Inspector a partir de qualquer visualização da biblioteca ou da barra de reprodução com no máximo 2 cliques ou um único comando de teclado (`Alt+Enter`).

---

## Assumptions

- A extração de propriedades técnicas e tags locais continuará sendo realizada através do leitor de metadados existente (`AtlMetadataService`), sem adição de bibliotecas conflitantes.
- A funcionalidade nesta etapa é focada estritamente em inspeção e visualização rica (leitura); o fluxo de edição de tags e gravação em disco é de responsabilidade da Feature 006 (Metadata Review & Tag Editor).
- A identificação acústica automática (Chromaprint / AcoustID) e busca online de dados adicionais são de responsabilidade das Features 004 e 005; aqui o Inspector apenas exibe os identificadores se já estiverem presentes.
- A sincronização em tempo real de letras (estilo karaokê) é tratada na Feature 007 (Lyrics Engine); nesta feature é exibido apenas o status de presença de letra e visualização estática do texto se existente no arquivo.
