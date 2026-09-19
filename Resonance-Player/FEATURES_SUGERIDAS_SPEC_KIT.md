# FEATURES_SUGERIDAS_SPEC_KIT.md

> Projeto: **Resonance**
> Repositório: **Resonance-Player**  
> Plataforma: Windows / C# / .NET / WinUI 3  
> Base: fork do Nagi  
> Finalidade: catálogo mestre de features para o Spec Kit, com regras obrigatórias para evitar *goal drift*, fragmentação excessiva e criação de arquitetura paralela.  
> Regra central: **cada feature deve ser implementada como uma capacidade vertical do produto, em no máximo 3 slices por padrão.**

---

# 1. REGRA OBRIGATÓRIA PARA TODA FEATURE

Toda nova feature/change criada no Spec Kit DEVE começar com o cabeçalho abaixo.

O agente NÃO pode omitir, resumir, reescrever ou substituir esta estrutura por outra.

```markdown
# FEATURE SPEC: [Nome da Feature / Change]

## 1. META IMUTÁVEL (Global Goal)
> **Problema de Negócio:** [Descreva em 2 frases o que o usuário/sistema precisa resolver]
> **Definição de Sucesso:** [O que o sistema faz quando terminar com sucesso, do ponto de vista funcional]
> **Regra de Ouro:** Não crie abstrações, camadas extras ou padrões que não existam na codebase atual.

---

## 2. CONTRATOS & LIMITES DA ARQUITETURA
* **Projetos Afetados na Solution (.sln):**
  - `[preencher somente após inspecionar a solution real]`
* **Tipos/Serviços Existentes que DEVEM ser reutilizados:**
  - `[listar após auditoria do código]`
* **Convenções Obrigatórias:**
  - Reutilizar a arquitetura real já existente no Nagi.
  - Não criar services, repositories, DTOs, ViewModels, factories, adapters ou pipelines paralelos quando já houver equivalente.
  - Não trocar biblioteca/framework existente sem justificativa aprovada no `plan.md`.
  - Respeitar DI, persistência, logging, MVVM, threading e convenções atuais do repositório.
  - Nullable reference types e analyzers devem seguir a configuração real dos projetos afetados.
  - Mudanças em interfaces devem ser acompanhadas, na mesma slice, por atualização de implementações, DI e testes afetados.
  - Alterações de banco devem incluir migration e teste de compatibilidade quando aplicável.

---

## 3. FATIAS VERTICAIS DE IMPLEMENTAÇÃO (Max 3 Slices)
> **Instrução ao Agente:** Implemente cada fatia de ponta a ponta na mesma sessão.  
> NÃO quebre em subtarefas técnicas horizontais como “criar DTO”, “criar interface”, “criar repository”, “registrar DI”, “criar ViewModel”.

### Slice 1: [Capacidade vertical observável]
- **Meta Imutável repetida:** [repetir problema + definição de sucesso]
- **Escopo ponta a ponta:** [UI/entrada + regra + integração + persistência quando necessário]
- **Reutilização obrigatória:** [tipos existentes]
- **Teste obrigatório:** [teste de unidade/integração/characterization]
- **Validação Local:** `dotnet build` + testes relevantes.

### Slice 2: [Capacidade vertical observável]
- **Meta Imutável repetida:** [repetir problema + definição de sucesso]
- **Escopo ponta a ponta:** [...]
- **Reutilização obrigatória:** [...]
- **Teste obrigatório:** [...]
- **Validação Local:** `dotnet build` + testes relevantes.

### Slice 3: [Integração completa e regressão]
- **Meta Imutável repetida:** [repetir problema + definição de sucesso]
- **Escopo:** integração completa da feature, casos extremos e regressão.
- **Teste obrigatório:** fluxo completo da feature.
- **Validação Final:** solution inteira compilando e testes passando.

---

## 4. GATES DE VALIDAÇÃO (.NET Toolchain)

Para considerar qualquer slice ou feature concluída, os comandos abaixo devem executar sem falhas.

> Ajuste nomes/paths somente depois de auditar o repositório real.

```bash
dotnet restore
dotnet build --configuration Release --warnaserror
dotnet test --configuration Release --no-build
```

Se a solution possuir comandos específicos, scripts, analyzers, packaging ou testes adicionais, eles passam a ser obrigatórios.

Nenhuma tarefa pode ser marcada como concluída se:
- a solution não compilar;
- DI estiver quebrada;
- testes falharem;
- migration estiver inconsistente;
- a UI não iniciar;
- o fluxo ponta a ponta estiver quebrado;
- um projeto afetado ficar com warnings novos não justificados.

---

## 5. AGENT GUARDRAILS

1. **Leia antes de alterar.**
   - `.specify/memory/constitution.md`
   - `IDEIA.md`
   - `PRD.md`
   - feature `spec.md`
   - `plan.md`
   - código real afetado

2. **Não invente arquitetura.**
   - O código atual é a fonte da verdade.
   - Se algo já existe, reutilize.

3. **Não faça refatoração alheia à feature.**
   - Refactors fora do escopo devem virar change separado.

4. **Não crie microtarefas horizontais.**
   - DTO/interface/repository/service/DI/ViewModel/View pertencem à mesma slice quando servem ao mesmo comportamento.

5. **Máximo padrão: 3 slices.**
   - Se parecer necessário mais do que isso, pare e proponha divisão em outra feature.

6. **Atualize a spec se a realidade invalidar a spec.**
   - Não crie workaround local silencioso.

7. **Nunca sobrescreva metadata do usuário automaticamente.**
   - Mudanças em tags exigem preview/diff/confirmação.

8. **Providers externos são opcionais.**
   - Playback/local library não pode depender de internet.

9. **Licença e termos são gate.**
   - Dependência/API com termos incertos bloqueia merge até revisão.

10. **A feature deve terminar funcionando de ponta a ponta.**
    - Código parcialmente conectado não é conclusão.

---

# 2. CATÁLOGO DE FEATURES SUGERIDAS

Total sugerido: **11 features principais**, sendo 1 de baseline e 10 funcionais.

---

# FEATURE 000 — Baseline / Audit do Fork Nagi

## 1. META IMUTÁVEL (Global Goal)
> **Problema de Negócio:** Antes de modificar o Nagi, precisamos saber exatamente o que a versão escolhida já implementa, como a solution está organizada e quais lacunas são reais. Sem isso, agentes podem duplicar recursos, criar arquitetura paralela ou quebrar comportamentos existentes.
> **Definição de Sucesso:** Existe uma baseline reproduzível com tag/SHA fixados, solution compilando, testes executados e documentação técnica verificável sobre scanner, formatos, metadata, equalizador, lyrics, playback, DI, persistência e testes existentes.
> **Regra de Ouro:** Não implementar feature nova nesta etapa. Não refatorar código de produto.

## 2. CONTRATOS & LIMITES DA ARQUITETURA
- Fixar tag/SHA base do Nagi.
- Mapear projetos da `.sln`.
- Mapear DI.
- Mapear persistência.
- Mapear pipeline de reprodução.
- Mapear scanner de biblioteca.
- Mapear equalizador.
- Mapear metadata local/remota.
- Mapear lyrics.
- Mapear testes.
- Registrar gaps reais contra `PRD.md`.

## 3. FATIAS VERTICAIS DE IMPLEMENTAÇÃO (Max 3 Slices)

### Slice 1 — Build/Test Baseline
- clonar/fixar upstream;
- restore/build/test;
- registrar comandos oficiais;
- documentar warnings/falhas existentes.

### Slice 2 — Architecture & Capability Audit
- mapear scanner, playback, EQ, metadata, lyrics, persistence, DI;
- identificar tipos existentes que serão reutilizados.

### Slice 3 — Characterization & Gap Report
- adicionar somente testes de caracterização necessários;
- gerar relatório de gaps;
- atualizar PRD/specs futuras com realidade confirmada.

---

# FEATURE 001 — Recursive Root Library Hardening

## 1. META IMUTÁVEL (Global Goal)
> **Problema de Negócio:** O usuário deve apontar uma ou várias pastas raiz e ter todas as músicas válidas das subpastas indexadas automaticamente, sem cadastrar pasta por pasta e sem perder músicas por profundidade de diretório.
> **Definição de Sucesso:** Uma árvore com múltiplos níveis, raízes sobrepostas, arquivos inválidos e caminhos problemáticos é processada sem duplicatas, sem travar a UI e sem abortar o scan inteiro.
> **Regra de Ouro:** Melhorar o scanner e persistência existentes do Nagi. Não criar uma segunda biblioteca paralela.

## 2. CONTRATOS & LIMITES DA ARQUITETURA
- Reutilizar modelagem atual de roots/tracks.
- Reutilizar persistência atual.
- Respeitar política real de WinUI/dispatcher.
- Definir política explícita para junction/symlink/reparse point.
- Bounded concurrency.
- Cancelamento.
- Progresso.
- Incremental scan quando possível.

## 3. FATIAS VERTICAIS DE IMPLEMENTAÇÃO

### Slice 1 — Recursive Traversal Resiliente
- raiz -> subpastas;
- cycle protection;
- inaccessible directories;
- corrupt files;
- testes com árvore sintética.

### Slice 2 — Indexação/Persistência/Incremental
- deduplicação;
- raízes sobrepostas;
- update/remove/move;
- cancelamento consistente;
- persistência existente.

### Slice 3 — UI/Progress/Regression
- progresso;
- cancel;
- erros;
- múltiplas roots;
- teste ponta a ponta.

---

# FEATURE 002 — Multi-Format Audio Library

## 1. META IMUTÁVEL (Global Goal)
> **Problema de Negócio:** A biblioteca deve lidar corretamente com todos os formatos de áudio realmente suportados pelo Nagi/LibVLC e não apenas MP3, evitando divergência entre o que o scanner aceita e o que o player consegue tocar.
> **Definição de Sucesso:** MP3, FLAC, WAV e demais formatos suportados pela baseline são descobertos, identificados e reproduzidos conforme a capability real da aplicação; arquivos incompatíveis são reportados de forma explícita.
> **Regra de Ouro:** Não manter listas duplicadas de extensões se a codebase já possui fonte de capability.

## 2. CONTRATOS & LIMITES DA ARQUITETURA
Formatos esperados para validação da baseline:
- MP3
- FLAC
- WAV
- AAC
- M4A
- OGG
- Opus
- WMA
- AIFF
- APE
- WavPack
- DSD
- outros suportados pela base real.

## 3. FATIAS VERTICAIS DE IMPLEMENTAÇÃO

### Slice 1 — Capability Inventory
- mapear scanner/player/metadata;
- criar matriz format x playback x metadata.

### Slice 2 — Unificação Scanner/Playback
- remover inconsistências;
- tratar unsupported/corrupt;
- testes representativos.

### Slice 3 — UI/Regression
- status claro;
- filtros;
- integração completa com biblioteca.

---

# FEATURE 003 — Track Inspector & Local Metadata

## 1. META IMUTÁVEL (Global Goal)
> **Problema de Negócio:** O usuário precisa visualizar com clareza tanto as tags musicais quanto os detalhes técnicos reais do arquivo que está tocando ou foi indexado.
> **Definição de Sucesso:** A aplicação mostra metadata local, propriedades técnicas, IDs disponíveis e origem dos dados para qualquer faixa suportada.
> **Regra de Ouro:** Reutilizar o mecanismo atual de leitura de metadata antes de adicionar novo parser.

## 2. CONTRATOS & LIMITES DA ARQUITETURA
Campos desejados:
- title;
- artist;
- album artist;
- album;
- track/disc;
- year;
- genre;
- comment;
- ISRC;
- ReplayGain;
- artwork;
- lyrics presence;
- path;
- size;
- duration;
- container;
- codec;
- bitrate;
- bitrate mode;
- sample rate;
- bit depth;
- channels;
- external IDs.

## 3. FATIAS VERTICAIS DE IMPLEMENTAÇÃO

### Slice 1 — Technical/Tag Read Model
- obter dados usando infraestrutura existente;
- normalizar ausência/erro;
- testes por formato.

### Slice 2 — Inspector UI
- ViewModel/tela;
- local vs online provenance;
- tratamento de loading/error.

### Slice 3 — Integration/Regression
- Now Playing + Library;
- testes completos.

---

# FEATURE 004 — Audio Fingerprint & Music Recognition

## 1. META IMUTÁVEL (Global Goal)
> **Problema de Negócio:** Arquivos podem estar sem tags, com nomes genéricos ou metadata incorreta. O usuário precisa reconhecer a música pelo conteúdo do áudio, não apenas por filename/tag.
> **Definição de Sucesso:** A aplicação gera fingerprint local, consulta AcoustID quando habilitado, obtém candidatos e associa MusicBrainz IDs com confiança explícita, sem enviar o arquivo de áudio completo.
> **Regra de Ouro:** Resultado de reconhecimento é candidato, nunca alteração automática.

## 2. CONTRATOS & LIMITES DA ARQUITETURA
- Chromaprint local.
- AcoustID provider.
- rate limit.
- API key segura.
- provenance.
- confidence.
- sem upload do áudio completo.

## 3. FATIAS VERTICAIS DE IMPLEMENTAÇÃO

### Slice 1 — Fingerprint Local
- extrair fingerprint;
- duração;
- testes determinísticos.

### Slice 2 — AcoustID Recognition
- provider;
- rate limiter;
- cache;
- candidatos/confiança.

### Slice 3 — UI/Integration
- reconhecer faixa;
- mostrar candidatos;
- selecionar/descartar;
- regressão.

---

# FEATURE 005 — Online Metadata Enrichment

## 1. META IMUTÁVEL (Global Goal)
> **Problema de Negócio:** Depois de reconhecer ou selecionar uma faixa, o usuário deve conseguir complementar tags incompletas usando fontes online confiáveis sem perder o controle sobre os dados locais.
> **Definição de Sucesso:** A aplicação obtém metadata online, preserva a origem de cada campo, faz cache respeitando provider e apresenta proposta de enriquecimento sem alterar o arquivo.
> **Regra de Ouro:** Dados remotos não substituem silenciosamente tags locais.

## 2. CONTRATOS & LIMITES DA ARQUITETURA
Providers potenciais:
- MusicBrainz;
- providers já existentes no Nagi;
- Last.fm;
- TheAudioDB;
- outros aprovados.

Regras:
- User-Agent correto;
- rate limits;
- cache;
- terms/license;
- provider failures isoladas.

## 3. FATIAS VERTICAIS DE IMPLEMENTAÇÃO

### Slice 1 — MusicBrainz/Provider Integration
- lookup;
- IDs;
- rate limit;
- cache.

### Slice 2 — Metadata Merge/Provenance
- local vs remoto;
- prioridade;
- confidence;
- candidate model.

### Slice 3 — UI/Regression
- proposta de metadata;
- origem visível;
- offline mode preservado.

---

# FEATURE 006 — Metadata Review, Tag Editor & File Update

## 1. META IMUTÁVEL (Global Goal)
> **Problema de Negócio:** O usuário precisa atualizar MP3/FLAC/outros formatos com metadata melhor, mas sem risco de sobrescrever dados corretos ou corromper arquivos.
> **Definição de Sucesso:** Antes de escrever tags, a aplicação mostra um diff campo a campo, permite escolher alterações e só grava após confirmação explícita.
> **Regra de Ouro:** Nenhuma escrita automática em arquivo por resultado de API.

## 2. CONTRATOS & LIMITES DA ARQUITETURA
- reutilizar writer atual;
- preview;
- selective apply;
- logging;
- tratamento de arquivo read-only;
- erro de escrita não pode destruir arquivo.

## 3. FATIAS VERTICAIS DE IMPLEMENTAÇÃO

### Slice 1 — Metadata Diff
- current vs proposed;
- seleção por campo;
- testes.

### Slice 2 — Safe Tag Write
- aplicar;
- preservar original quando possível;
- atualizar DB após sucesso.

### Slice 3 — UI/Regression
- review dialog;
- erros;
- confirmação;
- round-trip tests.

---

# FEATURE 007 — Lyrics Engine

## 1. META IMUTÁVEL (Global Goal)
> **Problema de Negócio:** O usuário deve visualizar letras da música atual priorizando dados locais e, opcionalmente, complementar com providers online.
> **Definição de Sucesso:** Embedded lyrics, `.lrc`, `.txt`, cache e provider remoto seguem uma ordem previsível; letras sincronizadas acompanham playback e a origem é visível.
> **Regra de Ouro:** Provider remoto é opcional e não transforma o projeto em distribuidor de corpus de letras.

## 2. CONTRATOS & LIMITES DA ARQUITETURA
Ordem:
1. embedded synced;
2. embedded plain;
3. `.lrc`;
4. `.txt`;
5. cache;
6. provider remoto.

## 3. FATIAS VERTICAIS DE IMPLEMENTAÇÃO

### Slice 1 — Local Lyrics Resolution
- embedded/lrc/txt;
- synced/plain;
- testes.

### Slice 2 — Remote Provider/Cache
- LRCLIB atual ou boundary existente;
- cache;
- provenance;
- settings.

### Slice 3 — Synchronized Lyrics UI
- acompanhar playback;
- scrolling/highlight;
- offline regression.

---

# FEATURE 008 — Equalizer & Preset Manager

## 1. META IMUTÁVEL (Global Goal)
> **Problema de Negócio:** O equalizador existente precisa oferecer uma experiência de uso comparável aos players clássicos, com presets claros e personalização persistente.
> **Definição de Sucesso:** O usuário escolhe, cria, salva, aplica e gerencia presets sem reiniciar perceptivelmente a música e sem causar clipping silencioso.
> **Regra de Ouro:** Evoluir o equalizador existente. Não criar segundo pipeline DSP.

## 2. CONTRATOS & LIMITES DA ARQUITETURA
- preservar 10 bandas se confirmadas pela baseline;
- pregain;
- Flat;
- built-in presets originais;
- user presets;
- persistência;
- clipping guard;
- live apply.

Presets candidatos:
- Flat
- Classical
- Club
- Dance
- Full Bass
- Full Bass & Treble
- Full Treble
- Laptop
- Large Hall
- Live
- Party
- Pop
- Reggae
- Rock
- Ska
- Soft
- Soft Rock
- Techno
- Vocal

## 3. FATIAS VERTICAIS DE IMPLEMENTAÇÃO

### Slice 1 — Preset Model/Persistence
- built-in/custom;
- salvar/renomear/duplicar/excluir.

### Slice 2 — Live EQ UI
- aplicar sem restart perceptível;
- pregain;
- Flat;
- A/B se aprovado.

### Slice 3 — Clipping/Regression
- clipping guard;
- persistência;
- testes de comportamento.

---

# FEATURE 009 — FFT / Spectrum / Waveform Visualizer

## 1. META IMUTÁVEL (Global Goal)
> **Problema de Negócio:** O usuário quer visualizar em tempo real o conteúdo espectral da música tocando, sem prejudicar reprodução, responsividade ou consumo quando o recurso estiver desligado.
> **Definição de Sucesso:** O player expõe spectrum em tempo real com barras/linha, testes FFT determinísticos e visualização desligável sem impacto perceptível no playback.
> **Regra de Ouro:** Não substituir o playback engine apenas para obter FFT sem antes provar necessidade técnica.

## 2. CONTRATOS & LIMITES DA ARQUITETURA
- analisar acesso real a PCM/LibVLC;
- renderer separado de analyzer;
- não bloquear UI;
- FPS controlado;
- smoothing/decay;
- FFT desligada = pipeline desligado.

## 3. FATIAS VERTICAIS DE IMPLEMENTAÇÃO

### Slice 1 — Sample Capture + FFT Core
- janela;
- FFT;
- magnitudes;
- teste com sine wave.

### Slice 2 — Spectrum Renderer
- bars;
- line;
- scaling;
- smoothing.

### Slice 3 — Performance/Regression
- efficiency mode;
- FPS;
- playback continuity;
- waveform opcional se seguro.

---

# FEATURE 010 — Modern Player UI / Classic Player Experience

## 1. META IMUTÁVEL (Global Goal)
> **Problema de Negócio:** O usuário precisa de uma interface moderna, rápida e informativa que reúna biblioteca, playback, equalizador, letras, spectrum e detalhes sem perder a eficiência de players clássicos.
> **Definição de Sucesso:** O usuário navega, toca, busca, inspeciona, ajusta EQ, vê letras e spectrum através de uma UI WinUI coerente, responsiva e integrada às capabilities existentes.
> **Regra de Ouro:** UI não implementa regra de negócio nem duplica serviços.

## 2. CONTRATOS & LIMITES DA ARQUITETURA
Áreas sugeridas:
- Home;
- Library;
- Songs;
- Albums;
- Artists;
- Genres;
- Folders;
- Playlists;
- Now Playing;
- Lyrics;
- Spectrum;
- Details;
- Equalizer;
- Settings.

## 3. FATIAS VERTICAIS DE IMPLEMENTAÇÃO

### Slice 1 — Now Playing Shell
- artwork;
- controls;
- progress;
- track info;
- details entry points.

### Slice 2 — Library/Folders Experience
- roots;
- tree/list;
- search/filter;
- states de scan.

### Slice 3 — Integrated Player Experience
- Lyrics/Spectrum/EQ/Details;
- keyboard/accessibility;
- regressão de navegação.

---

# 3. REGRAS DE ORQUESTRAÇÃO ENTRE FEATURES

## Ordem recomendada

```text
000 Baseline / Audit
  |
  +--> 001 Recursive Root Library
  |
  +--> 002 Multi-Format Audio
  |
  +--> 003 Track Inspector
           |
           +--> 004 Audio Fingerprint
                    |
                    +--> 005 Online Metadata
                             |
                             +--> 006 Metadata Review/Tag Writer

003 Track Inspector
  |
  +--> 007 Lyrics Engine

000 Baseline
  |
  +--> 008 Equalizer/Presets
  |
  +--> 009 FFT/Spectrum

001..009
  |
  +--> 010 Modern Player UI
```

## Dependências

- Feature 000 precede todas.
- 004 depende de 003.
- 005 depende de 004 ou de um identificador confiável existente.
- 006 depende de 003 e 005.
- 010 integra capabilities anteriores, mas não deve ser usada para implementar lógica dessas capabilities.

---

# 4. REGRA CONTRA TASK SLICING EXCESSIVO

## Proibido

```text
Task 1 - criar interface
Task 2 - criar DTO
Task 3 - criar repository
Task 4 - registrar DI
Task 5 - criar service
Task 6 - criar ViewModel
Task 7 - criar View
Task 8 - criar teste
```

## Obrigatório

```text
Slice 1 - entregar comportamento ponta a ponta A
Slice 2 - entregar comportamento ponta a ponta B
Slice 3 - integrar, testar e fechar a feature
```

Uma slice pode conter internamente:
- interface;
- implementation;
- DI;
- model;
- persistence;
- ViewModel;
- UI;
- tests;

desde que tudo exista para entregar **uma única capacidade vertical observável**.

---

# 5. PROMPT OBRIGATÓRIO DE RECUPERAÇÃO DE FOCO

Se o agente demonstrar *goal drift*, criar arquitetura paralela, mudar escopo ou começar a gerar muitos arquivos sem integração, interromper e aplicar:

```text
PARE A IMPLEMENTAÇÃO.

Releia obrigatoriamente:
1. `.specify/memory/constitution.md`
2. `IDEIA.md`
3. `PRD.md`
4. esta feature em `FEATURES_SUGERIDAS_SPEC_KIT.md`
5. `spec.md`
6. `plan.md`
7. o código real afetado

Responda antes de continuar:

A. Qual é a META IMUTÁVEL desta feature?
B. Qual comportamento observável a slice atual deve entregar?
C. Quais tipos/serviços existentes serão reutilizados?
D. Quais arquivos/abstrações criados são redundantes?
E. Qual é o menor diff coerente para concluir a slice?
F. `dotnet build` da solution passa?
G. Os testes relevantes passam?

NÃO escreva código adicional até reconciliar essas respostas com a spec.
```

---

# 6. DEFINITION OF DONE GLOBAL

Uma feature somente pode ser concluída quando:

- [ ] META IMUTÁVEL continua satisfeita.
- [ ] No máximo 3 slices por padrão.
- [ ] Nenhuma arquitetura paralela foi criada.
- [ ] Tipos existentes foram reutilizados quando aplicável.
- [ ] Solution completa compila.
- [ ] Testes relevantes passam.
- [ ] Fluxo ponta a ponta da feature passa.
- [ ] DI está consistente.
- [ ] Persistência/migrations estão consistentes.
- [ ] Providers externos respeitam rate limit/terms/licença.
- [ ] Offline/local-first continua funcionando quando aplicável.
- [ ] Nenhum secret foi commitado.
- [ ] Nenhuma tag do usuário é alterada sem confirmação.
- [ ] Logs não expõem tokens nem conteúdo sensível desnecessário.
- [ ] `spec.md` corresponde ao comportamento entregue.
- [ ] `plan.md` corresponde à implementação.
- [ ] `tasks.md` representa slices verticais e não microtarefas.
- [ ] `/speckit.analyze` não possui bloqueador.
- [ ] `/speckit.converge` não possui lacuna crítica.

---

# 7. REGRA FINAL PARA O SPEC KIT

> O objetivo deste documento NÃO é obrigar o projeto a implementar todas as features imediatamente.
>
> Ele é o **catálogo mestre e guardrail de execução**.
>
> Cada feature deve virar sua própria pasta/spec apenas quando for iniciada.
>
> Antes de criar a próxima feature, o agente deve consultar este arquivo e a Constitution.
>
> Se houver conflito entre este documento e a codebase real, a codebase deve ser investigada e a spec corrigida antes da implementação.
>
> O agente nunca deve “resolver” conflito de especificação inventando uma arquitetura nova localmente.
