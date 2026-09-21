# Quickstart Guide: 004 — Audio Fingerprint & Music Recognition

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)  
**Date**: 2026-09-21  

---

## 1. Pré-Requisitos

1. **.NET 10.0 SDK** instalado no ambiente de desenvolvimento.
2. **FFmpeg** disponível no `PATH` do sistema com o muxer `chromaprint` habilitado (`ffmpeg -h muxer=chromaprint`).
3. Solution compilada: `Resonance.slnx`.

---

## 2. Validação por Fatias Verticais

### Slice 1: Geração Local de Chromaprint
Valida que a extração local do Chromaprint a partir de arquivos de áudio é executada em menos de 1,5s, de forma determinística e sem transmitir dados de rede.

```powershell
# Executar testes unitários da engine de fingerprint
dotnet test tests/Resonance.Core.Tests --filter FullyQualifiedName~FingerprintServiceTests
```

**Critérios de Aceitação Verificados:**
- O hash Base64 é gerado com sucesso para arquivos com áudio válido.
- Arquivos com áudio em silêncio absoluto ou arquivos não-áudio retornam erro controlado sem falhar.
- O tempo total de execução é inferior a 1,5 segundo.

---

### Slice 2: Cliente AcoustID, Rate Limiting e Resiliência
Valida o cliente HTTP do AcoustID contra mocks de resposta, comprovando o respeito ao limite de 3 req/s, o cálculo correto do índice de confiança e o filtro de corte (score ≥ 40%).

```powershell
# Executar testes de integração simulados do AcoustIdService
dotnet test tests/Resonance.Core.Tests --filter FullyQualifiedName~AcoustIdServiceTests
```

**Critérios de Aceitação Verificados:**
- Mapeia respostas de sucesso para `RecognitionCandidate` contendo Título, Artista, Álbum e MusicBrainz ID.
- Descarta candidatos com pontuação inferior a 40%.
- Limita o resultado a no máximo 5 candidatos ordenados decrescentemente.
- Em caso de resposta 429 (Too Many Requests), aciona backoff exponencial e retenta com segurança.
- Em modo offline ou provedor desativado, retorna status `OfflineOrDisabled` sem lançar exceções.

---

### Slice 3: Experiência Visual no Track Inspector
Valida a apresentação do card de Fingerprint, indicador de progresso e vinculação de identificadores na interface.

1. Iniciar o player:
   ```powershell
   dotnet run --project src/Resonance.WinUI --configuration Debug
   ```
2. Na biblioteca de músicas, selecionar uma faixa qualquer ou faixa sem metadados.
3. Pressionar `Alt+Enter` ou clicar com o botão direito e escolher **"Inspecionar Faixa"**.
4. No painel retrátil à direita, localizar o card **"Fingerprint & Reconhecimento Acústico"**.
5. Clicar em **"Identificar Música via Áudio"**:
   - Observar a barra de progresso suave indicando análise acústica e consulta de rede.
   - Observar a exibição da lista de candidatos com seus crachás de confiança (verde/roxo para ≥ 80%).
6. Clicar em **"Vincular Este Candidato"**:
   - Confirmar que o status da faixa passa para **"Já Identificada"**.
   - Confirmar que os identificadores `MusicBrainzTrackId` e `AcoustId` aparecem na seção de Identificadores Externos com botões de cópia.
   - Constatar que o arquivo de áudio físico no disco permanece intacto (sem modificações indevidas nas tags em disco).

---

## 3. Gates Obrigatórios de Validação Global

```powershell
dotnet restore Resonance.slnx
dotnet build Resonance.slnx --configuration Release -p:Platform=x64
dotnet test Resonance.slnx --configuration Release --no-build
```
