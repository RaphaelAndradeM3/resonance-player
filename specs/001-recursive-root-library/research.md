# Research: Feature 001 — Recursive Root Library Hardening

**Branch**: `001-recursive-root-library` | **Feature**: [spec.md](spec.md)  
**Date**: 2026-09-20  
**Status**: Completed  

---

## Executive Summary

Este documento consolida as decisões técnicas, análises de arquitetura e padrões adotados para o endurecimento do mecanismo de varredura recursiva de diretórios raiz da biblioteca no **Resonance**.

Conforme o princípio constitucional **"Existing Code Is the Source of Truth"**, todas as melhorias evoluem os componentes existentes (`SafeFileEnumerator`, `LibraryService`, `Folder`, `Song`, `ScanProgress`) sem criar bibliotecas ou persistências paralelas.

---

## Key Research Decisions

### Decision 1: Resolução de Reparse Points e Prevenção de Ciclos em Travessia Profunda (FR-002)

* **Contexto**: O `SafeFileEnumerator` existente ignorava sumariamente qualquer diretório onde `FileAttributes.ReparsePoint` estivesse presente (`if ((attributes & FileAttributes.ReparsePoint) != 0) return true;`). Isso impedia que coleções musicais legítimas organizadas com *NTFS Junctions* ou *Symbolic Links* fossem indexadas.
* **Decisão**: 
  1. Utilizar `DirectoryInfo.ResolveLinkTarget(returnFinalTarget: true)` do .NET 10 para obter o destino físico real do reparse point.
  2. Normalizar o caminho canônico do destino (`Path.GetFullPath`).
  3. Manter um conjunto em memória (`HashSet<string>(StringComparer.OrdinalIgnoreCase)`) com todos os caminhos canônicos visitados durante a sessão de travessia.
  4. Se o caminho de destino já constar no conjunto de visitados (ou for ancestral do diretório atual), descartar a expansão do nó para evitar recursão infinita.
* **Rationale**: Permite que links simbólicos legítimos sejam navegados com 100% de proteção contra loops cíclicos, mantendo a compatibilidade do Windows.
* **Alternativas Rejeitadas**:
  - *Ignorar todos os Reparse Points*: Rejeitado porque desconsidera bibliotecas reais que utilizam links entre múltiplos discos ou diretórios organizados.
  - *Detecção por contagem de profundidade máxima*: Rejeitada por ser arbitrária e falhar em árvores legítimas com mais de 10 níveis de subpastas.

---

### Decision 2: Detecção e Consolidação de Raízes Sobrepostas (FR-003)

* **Contexto**: O usuário pode configurar pastas raiz redundantes (ex.: adicionar `D:\Musicas` e, posteriormente, `D:\Musicas\MPB\Chico`).
* **Decisão**: 
  1. Implementar verificação de sobreposição já na camada de gerenciamento de pastas (`FolderService` / `SettingsViewModel`).
  2. Ao tentar adicionar uma pasta filha de uma raiz já cadastrada, a interface notifica amigavelmente o usuário de que a subpasta já está inclusa na raiz pai e descarta a inclusão duplicada.
  3. Se o usuário adicionar uma pasta pai de subpastas já cadastradas, o sistema absorve automaticamente as subpastas filhas, removendo-as da lista de raízes e mantendo apenas a pasta ancestral.
  4. No nível do scanner, o pipeline desduplica os caminhos físicos através de caminho canônico antes de qualquer inserção.
* **Rationale**: Elimina o processamento concorrente redundante no disco rígido e mantém a interface de configuração de pastas limpa e compreensível.
* **Alternativas Rejeitadas**:
  - *Permitir raízes redundantes e desduplicar apenas no banco*: Rejeitado porque gasta tempo de I/O lendo e decodificando metadados repetidamente nas subpastas sobrepostas.

---

### Decision 3: Estratégia de Sincronização Incremental e Hard Delete Seguro (FR-001, FR-007)

* **Contexto**: Quando faixas são excluídas ou movidas do disco, ou quando volumes externos (HDs externos, pen drives, rede) são desconectados, o catálogo precisa manter sua integridade sem causar perda de dados.
* **Decisão**:
  1. **Raízes Inacessíveis**: Antes de escanear uma raiz configurada, o sistema valida a acessibilidade (`Directory.Exists(rootPath)`). Se a raiz estiver desconectada ou inacessível, o scanner emite um aviso no relatório de progresso e **pula a raiz**, preservando intactas todas as faixas existentes associadas àquele `FolderId`.
  2. **Arquivos Ausentes em Raízes Acessíveis**: Durante o scan incremental de uma raiz acessível, o sistema compara as faixas existentes no banco para aquela raiz com os arquivos encontrados no disco. Arquivos que existiam no banco mas não foram encontrados no disco durante o scan são removidos via *hard delete* (`context.Songs.RemoveRange(...)`).
* **Rationale**: Impede que a desconexão temporária de um drive USB apague acidentalmente centenas de faixas da biblioteca, ao mesmo tempo em que limpa arquivos apagados localmente pelo usuário.
* **Alternativas Rejeitadas**:
  - *Soft Delete em todas as situações*: Rejeitado na sessão de esclarecimento (Clarification Q1) para evitar faixas fantasmas no catálogo.
  - *Excluir faixas mesmo com raiz inacessível*: Rejeitado por violar o princípio de segurança contra perda acidental de dados.

---

### Decision 4: Concorrência Limitada (Bounded Concurrency) e Prioridade de Threading

* **Contexto**: Leituras massivas de metadados em 10.000+ arquivos podem saturar o barramento de I/O, travar a thread de UI ou causar cortes na reprodução de áudio do LibVLC (*audio buffer underrun*).
* **Decisão**:
  1. O enumerador de arquivos (`SafeFileEnumerator`) produz caminhos sob demanda com enumeração preguiçosa (`yield return`).
  2. A leitura de metadados via ATL é processada através de um canal limitado (`Channel.CreateBounded<string>`) com um grau de paralelismo restrito a `Math.Clamp(Environment.ProcessorCount / 2, 2, 4)` workers.
  3. A thread de áudio e o dispatcher da WinUI têm prioridade absoluta; o processamento em lote persiste blocos de 100 faixas em transações do SQLite para evitar locks prolongados no banco de dados.
* **Rationale**: Mantém a UI responsiva a 60 fps e garante reprodução de música ininterrupta durante varreduras pesadas de biblioteca.

---

### Decision 5: Cancelamento Cooperativo e Notificação Desacoplada (FR-004, FR-005)

* **Contexto**: Varreduras longas devem poder ser interrompidas imediatamente pelo usuário e devem reportar contadores em tempo real.
* **Decisão**:
  1. Utilizar tokens de cancelamento cooperativos (`CancellationToken`) checados a cada iteração de diretório e a cada lote de persistência.
  2. Ao acionar o cancelamento, a operação interrompe o I/O imediatamente (< 1 segundo) e efetua o commit do último lote processado, deixando os dados parciais utilizáveis.
  3. Relatar progresso via `IProgress<ScanProgress>` com limitação de emissão (*throttling* de 100ms) para não sobrecarregar a fila de eventos do Dispatcher da WinUI.
* **Rationale**: Cumpre os critérios mensuráveis SC-003 (cancelamento < 1s) e SC-004 (fluidez de UI sem travamentos).
