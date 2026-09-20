# Quickstart: Feature 002 — Multi-Format Audio Library

**Branch**: `002-multi-format-audio-library` | **Feature**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)  
**Date**: 2026-09-20  

---

## 1. Visão Geral

Este guia descreve os passos executáveis para validar a capacidade multi-formato do **Resonance**:
1. Compatibilidade e paridade da lista única de extensões de áudio (`FileExtensions.MusicFileExtensions`).
2. Extração correta de metadados técnicos (duração, bitrate, sample rate, canais) nos formatos MP3, FLAC, WAV, AAC, M4A, OGG, Opus, WMA, AIFF, APE, WavPack (`.wv`) e DSD (`.dsf`, `.dff`).
3. Resiliência do scanner e descarte correto de arquivos corrompidos ou de tamanho zero (0 bytes).
4. Mapeamento consistente de hints e demuxers para o backend de reprodução LibVLC.

---

## 2. Pré-requisitos

* SDK .NET 10.0 instalado (`dotnet --version`).
* Cargas de trabalho Windows Desktop e WinUI 3 instaladas.
* PowerShell 7+ ou Windows PowerShell.

---

## 3. Roteiro Executável de Testes e Validação

### Passo 1: Compilação Completa da Solution
```powershell
dotnet restore Resonance.sln -p:Platform=x64
dotnet build Resonance.sln -p:Platform=x64 --no-restore
```
*Critério de Sucesso*: 0 erros em todos os projetos da solution.

---

### Passo 2: Validação da Matriz de Formatos e Extensões (Slice 1)
Executa os testes unitários dedicados à lista canônica de extensões:
```powershell
dotnet test tests/Resonance.Core.Tests/Resonance.Core.Tests.csproj --filter "FullyQualifiedName~FileExtensions" --no-build
```
*Cenários Validados*:
* Confirmação de presença de todos os formatos mandatados (MP3, FLAC, WAV, AAC, M4A, OGG, Opus, WMA, AIFF, APE, WV, DSF, DFF).
* Rejeição de formatos de vídeo puro ou protegidos por DRM (ex: `.m2v`, `.aax`, `.m4p`).

---

### Passo 3: Validação de Extração de Metadados e Resiliência a Arquivos Corrompidos (Slice 2)
Executa os testes de extração multi-formato e isolamento de erros:
```powershell
dotnet test tests/Resonance.Core.Tests/Resonance.Core.Tests.csproj --filter "FullyQualifiedName~AtlMetadataService|FullyQualifiedName~FormatResilience" --no-build
```
*Cenários Validados*:
* Extração de metadados técnicos (bitrate, sample rate, canais) para formatos sem perda (FLAC, WAV, AIFF, WV, DSD) e com perda (MP3, AAC, OGG, Opus).
* Teste com arquivo de tamanho zero: deve ser sinalizado com `ExtractionFailed = true` e `ErrorMessage = "EmptyFile"` sem lançar exceções.
* Teste com arquivo de cabeçalho truncado: deve ser sinalizado com `ExtractionFailed = true` e `ErrorMessage = "CorruptFile"`.

---

### Passo 4: Validação de Paridade com o Reprodutor de Áudio (Slice 3)
Executa os testes de mapeamento de demuxers e hints do LibVLC:
```powershell
dotnet test tests/Resonance.Core.Tests/Resonance.Core.Tests.csproj --filter "FullyQualifiedName~PlayerFormatHint|FullyQualifiedName~AudioFormatRegistry" --no-build
```
*Cenários Validados*:
* Todo formato contido em `MusicFileExtensions` possui hint de formato adequado ou demuxer nativo mapeado em `LibVlcAudioPlayerService`.
* Zero formatos órfãos no reprodutor.

---

### Passo 5: Gate Final Constitucional (Whole-Solution Validation)
```powershell
dotnet test tests/Resonance.Core.Tests/Resonance.Core.Tests.csproj --no-build
```
*Critério de Sucesso*: 100% dos testes da suíte aprovados sem nenhuma regressão.
