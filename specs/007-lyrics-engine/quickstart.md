# Quickstart & Validation Guide: Feature 007 — Lyrics Engine

## Overview
Este guia estabelece os cenários executáveis e testes de ponta a ponta para validação da Feature 007 (Lyrics Engine), cobrindo a cadeia de resolução canônica, cache local, letras sincronizadas e estáticas, compensação de offset e interface do usuário.

---

## 1. Pré-Requisitos e Setup Local

### 1.1 Restauração e Compilação
```powershell
# Na raiz do repositório
dotnet restore Resonance.slnx
dotnet build Resonance.slnx --configuration Release -p:Platform=x64 --warnaserror
```

### 1.2 Execução da Suíte de Testes
```powershell
dotnet test Resonance.slnx --configuration Release -p:Platform=x64 --no-build
```

---

## 2. Cenários de Validação Automatizada

### Cenário 1: Resolução Canônica Local-First (6 Etapas)
- **Objetivo**: Garantir que fontes locais sejam priorizadas e que nenhuma chamada HTTP seja feita se existir letra local.
- **Entradas**:
  - Arquivo A: MP3 com tag ID3 SYLT embutida.
  - Arquivo B: FLAC sem tags de letra, com `ArquivoB.lrc` na mesma pasta.
  - Arquivo C: M4A sem tags de letra, com `Artista - Titulo.lrc` na mesma pasta.
  - Arquivo D: OGG sem `.lrc`, com `ArquivoD.txt` na mesma pasta.
- **Passos**:
  1. Invocar `ILrcService.ResolveLyricsAsync(song)`.
  2. Verificar que `LyricsDocument.Provenance` corresponde exatamente à primeira fonte encontrada.
  3. Verificar que o mock do cliente HTTP (`IOnlineLyricsService` / `HttpClient`) registrou 0 chamadas.
- **Critério de Sucesso**: Resolução em < 50ms (SC-001) e zero chamadas remotas (SC-003).

### Cenário 2: Cache Isolado e Exportação Manual de Sidecar
- **Objetivo**: Validar persistência no cache interno (`%LocalAppData%`) e gravação sob demanda na pasta da música.
- **Passos**:
  1. Faixa sem letra local consulta LRCLIB com resposta simulada.
  2. Verificar que o arquivo é gravado no diretório de cache gerenciado e `Provenance == LyricsProvenance.RemoteLrcLib`.
  3. Invocar comando `ExportSidecarLrcAsync(song)`.
  4. Verificar existência do arquivo `<NomeDoAudio>.lrc` no diretório da mídia.
- **Critério de Sucesso**: Pasta do usuário intocada até ação explícita (Princípio Constitucional VII).

### Cenário 3: Sincronização em Tempo Real e Calibração de Offset
- **Objetivo**: Validar acompanhamento de reprodução, salto temporal por estrofe e ajuste fino de offset.
- **Passos**:
  1. Carregar letra sincronizada com estrofes em `00:10.000` e `00:20.000`.
  2. Simular playback em `00:10.500` -> Estrofe 1 ativa.
  3. Aplicar offset de `-500ms` via `AdjustOffsetCommand(-500)`.
  4. Simular clique na estrofe 2 -> Player busca posição `00:19.800` (descontando o lead time).
  5. Verificar que `LyricsOffsetMs` foi gravado no banco de dados para a faixa.
- **Critério de Sucesso**: Estrofe correta destacada a cada tick e seek funcional (FR-003, FR-004, FR-010).

### Cenário 4: Faixa Instrumental e Modo Offline
- **Objetivo**: Validar que faixas instrumentais exibem mensagem amigável e não refazem buscas desnecessárias.
- **Passos**:
  1. Consulta ao LRCLIB retorna `{ "instrumental": true }`.
  2. `ResolveLyricsAsync` retorna `LyricsDocument` com `IsInstrumental == true`.
  3. `Song.IsInstrumental` é persistido como `true` e `LyricsLastCheckedUtc` é atualizado.
  4. Próxima chamada a `ResolveLyricsAsync` não consulta rede mesmo em modo online.
- **Critério de Sucesso**: UI exibe `♫ Faixa Instrumental` e chamadas subsequentes são evitadas (FR-009).

---

## 3. Validação Manual na UI WinUI 3

1. Iniciar a aplicação no Windows:
   ```powershell
   dotnet run --project src/Resonance.WinUI/Resonance.WinUI.csproj --configuration Release -p:Platform=x64
   ```
2. Reproduzir uma faixa que possua `.lrc` na mesma pasta -> Abrir tela de letras (`LyricsPage`).
3. Observar:
   - Selo no topo com a indicação `[Arquivo .lrc]`.
   - Linha atual destacada e centralizada conforme a música toca.
   - Pressionar botões `+100ms` ou `-100ms` e verificar sincronia fina imediata.
   - Clicar em uma linha futura da letra -> áudio salta imediatamente para o trecho.
4. Reproduzir faixa sem letra com busca online habilitada:
   - Observar badge de proveniência `[Fonte: LRCLIB]`.
   - Botão "Exportar como .lrc" habilitado; clicar e confirmar que o arquivo `.lrc` foi criado ao lado do áudio.
