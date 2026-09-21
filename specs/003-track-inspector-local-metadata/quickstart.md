# Quickstart Validation Guide: 003 — Track Inspector & Local Metadata

**Feature Branch**: `003-track-inspector-local-metadata`  
**Date**: 2026-09-20  
**Status**: Ready  
**Spec Reference**: [spec.md](./spec.md)  
**Contracts**: [track-inspector-contract.md](./contracts/track-inspector-contract.md)  
**Data Model**: [data-model.md](./data-model.md)

---

## 1. Pré-Requisitos e Ambiente de Execução

- .NET 10 SDK instalado (`dotnet --version` deve reportar `10.0.x`).
- Visual Studio 2026 ou VS Code com extensão C# Dev Kit.
- Solução configurada via `Resonance.slnx`.

---

## 2. Comandos de Compilação e Gates de Qualidade

Antes de qualquer teste manual, certifique-se de que a solução compila limpa e sem warnings tratados como erro:

```powershell
# 1. Restaurar dependências
dotnet restore Resonance.slnx

# 2. Compilar toda a solução em Release com verificação estrita
dotnet build Resonance.slnx --configuration Release --no-restore

# 3. Executar suíte de testes unitários automatizados
dotnet test Resonance.slnx --configuration Release --no-build
```

---

## 3. Cenários de Validação Ponta a Ponta

### Cenário 1: Inspeção de Faixa Hi-Res Lossless (FLAC 24-bit/96kHz)

- **Ação**:
  1. Inicie a aplicação `Resonance.WinUI`.
  2. Na biblioteca de músicas, selecione uma faixa em formato FLAC de alta resolução.
  3. Pressione `Alt+Enter` no teclado ou clique com o botão direito e selecione **"Inspecionar Faixa"**.
- **Resultado Esperado**:
  - O painel lateral retrátil desliza suavemente na margem direita em menos de 150ms.
  - A seção **"Propriedades Técnicas"** exibe:
    - Formato de Contêiner: `FLAC`
    - Codec: `FLAC`
    - Taxa de Amostragem: `96.0 kHz` (ou correspondente)
    - Profundidade de Bits: `24-bit`
    - Canais: `Estéreo (2 canais)`
    - Modo de Bitrate: `VBR` ou taxa exata em `kbps`
    - Tamanho do Arquivo: Formatado legivelmente em MB
  - A thread de interface não apresenta travamento durante o carregamento da faixa.

---

### Cenário 2: Inspeção de Tags Musicais, ReplayGain e Presença de Letras

- **Ação**:
  1. No painel aberto, examine a seção **"Metadados Musicais"**.
  2. Observe campos como Título, Artista, Álbum, Número de Faixa/Disco, Ano, Gênero e Compositor.
  3. Verifique a existência de indicadores de **ReplayGain** (Track Gain / Album Gain em dB).
  4. Caso o arquivo possua letra embutida, verifique o selo "Letra Disponível" e clique para expandir o preview.
- **Resultado Esperado**:
  - Todos os metadados são exibidos sem caracteres truncados ou corrupções de codificação (*mojibake*).
  - Valores de ganho são exibidos com sinal e unidade (ex.: `-6.5 dB`).

---

### Cenário 3: Interação com a Capa e Exportação de Imagem (LightBox)

- **Ação**:
  1. No painel de inspeção, localize a imagem de capa da faixa.
  2. Observe as dimensões exibidas (ex.: `1400 x 1400`).
  3. Clique sobre a miniatura da capa para abrir o modo **LightBox** (tamanho real).
  4. Clique no botão **"Salvar Imagem..."** e escolha um diretório no disco.
- **Resultado Esperado**:
  - A imagem expande em tamanho original em um visualizador modal nítido.
  - O arquivo é salvo com sucesso no diretório de destino com o formato original preservado (`.jpg` ou `.png`).

---

### Cenário 4: Inspeção em Lote com Seleção Múltipla e Navegação Rápida

- **Ação**:
  1. Na lista de músicas da biblioteca, selecione 5 faixas usando `Shift+Clique` ou `Ctrl+Clique`.
  2. Pressione `Alt+Enter` para abrir o Inspector.
  3. Observe o cabeçalho do painel indicando `1 de 5`.
  4. Use os botões `< Anterior` e `Próxima >` ou as setas do teclado.
- **Resultado Esperado**:
  - O painel exibe imediatamente os dados da primeira faixa selecionada.
  - Ao clicar em `Próxima >`, os dados atualizam de forma rápida e assíncrona para a segunda faixa, sucessivamente até a quinta, sem fechar ou reiniciar o painel lateral.

---

### Cenário 5: Sincronização Dinâmica com a Reprodução ("Now Playing")

- **Ação**:
  1. Reproduza uma playlist ou álbum.
  2. Abra o Inspector e certifique-se de que a opção **"Seguir reprodução"** está ativada.
  3. Pule para a próxima música na barra de controles do player (`Next`).
- **Resultado Esperado**:
  - O painel do Track Inspector detecta a mudança de faixa e atualiza todos os metadados técnicos e artísticos para refletir a nova música que começou a tocar.
