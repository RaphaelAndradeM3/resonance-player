# Quickstart & Validation Guide: Feature 005 — Online Metadata Enrichment

**Feature Branch**: `005-online-metadata-enrichment`  
**Date**: 2026-09-22  
**Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

---

## 1. Prerequisites

1. **Plataforma**: Windows 10/11 x64.
2. **SDK**: .NET 10.0 SDK instalado.
3. **Solução**: `Resonance.slnx` restaurada e pronta para compilação.
4. **Dependências**:
   - Feature 003 (Track Inspector e leitura de tags) e Feature 004 (Identificação Acústica AcoustID/MusicBrainz) mescladas na base.

---

## 2. Cenários de Validação Automatizados

### Cenário 1: Consulta Canônica e Cache do MusicBrainz (Slice 1)
Valida que o cliente recupera corretamente os dados canônicos de uma gravação usando MBID e armazena a resposta em cache JSON no disco.

```powershell
dotnet test tests/Resonance.Core.Tests --configuration Release --filter "FullyQualifiedName~MusicBrainzServiceTests"
```

**Resultados Esperados**:
- Requisição formatada com User-Agent `Resonance/1.0 (+https://github.com/RaphaelAndradeM3/resonance-player)`.
- Resposta desserializada corretamente com título, artista, álbum canônico oficial, ano original, número de faixa e disco.
- Segunda chamada para o mesmo MBID responde em < 15ms através do cache local em `%LocalAppData%/Resonance/cache/metadata/` sem disparar requisição HTTP.
- Cover Art Archive resolve a URL para `https://coverartarchive.org/release/{mbid}/front-500`.

---

### Cenário 2: Motor de Mesclagem & Modelo de Proveniência (Slice 2)
Valida a lógica pura de comparação de tags e a geração da proposta de enriquecimento.

```powershell
dotnet test tests/Resonance.Core.Tests --configuration Release --filter "FullyQualifiedName~MetadataEnrichmentServiceTests"
```

**Resultados Esperados**:
- Faixa sem tags locais: campos preenchidos geram status `NewValue` com proveniência `MusicBrainz` e `IsSelected = true`.
- Faixa com dados idênticos: status `Unchanged` com `IsSelected = false`.
- Divergência ortográfica/ano: status `Updated` com `IsSelected = true`.
- Gênero local existente "Rock" + remoto ["Progressive Rock", "Art Rock"]: proposto "Rock; Progressive Rock; Art Rock" com status `Updated`.
- Comentários ou campos locais não suportados pelo MusicBrainz permanecem intactos.

---

### Cenário 3: Fluxo Completo no Track Inspector & Regressão (Slice 3)
Valida o comportamento do ViewModel, comandos assíncronos e tratamento de erros.

```powershell
dotnet test tests/Resonance.Core.Tests --configuration Release --filter "FullyQualifiedName~TrackInspectorViewModelTests"
```

**Resultados Esperados**:
- `EnrichOnlineMetadataCommand` dispara consulta e preenche `EnrichmentProposal`.
- Checkbox individual atualiza `IsSelected` no modelo.
- Falha de rede ou timeout (offline) define `EnrichmentError` suavemente sem exceção não-tratada.

---

## 3. Validação da Solução Completa (.NET Toolchain Gates)

Para encerramento de qualquer fatia vertical ou conclusão da feature, execute os comandos obrigatórios:

```powershell
# 1. Restauração de dependências
dotnet restore Resonance.slnx

# 2. Compilação completa em modo Release x64
dotnet build Resonance.slnx --configuration Release -p:Platform=x64

# 3. Execução da suíte completa de testes
dotnet test Resonance.slnx --configuration Release --no-build
```

**Critérios de Aprovação**:
- 0 erros e 0 warnings novos na compilação.
- 100% dos testes unitários e de integração passando.
- Zero gravações físicas em arquivos de áudio em disco (preservando o isolamento para a Feature 006).
