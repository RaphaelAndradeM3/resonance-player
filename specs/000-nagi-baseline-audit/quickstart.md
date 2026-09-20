# Quickstart & Validation Guide: Feature 000 — Baseline / Audit do Fork Nagi

**Feature**: `000-nagi-baseline-audit`  
**Date**: 2026-09-20  
**Status**: Ready for Verification  

---

## 1. Overview

Este guia descreve os cenários executáveis para validar a baseline técnica do fork Nagi e comprovar a estabilidade da solution antes de qualquer modificação de código de produto.

Para especificações detalhadas de comandos e contratos de ambiente, consulte:
- [Toolchain Contract](file:///specs/000-nagi-baseline-audit/contracts/toolchain-contract.md)
- [Architecture Inventory Contract](file:///specs/000-nagi-baseline-audit/contracts/audit-matrix-contract.md)
- [PRD Gap Report Contract](file:///specs/000-nagi-baseline-audit/contracts/gap-report-contract.md)
- [Data Model](file:///specs/000-nagi-baseline-audit/data-model.md)

---

## 2. Prerequisites

1. **Windows 10 (1809+) ou Windows 11 x64**.
2. **.NET SDK 10.0** (versão 10.0.300+ / 10.0.401).
3. **Visual Studio 2026 ou Build Tools** com workloads de C++ e .NET Desktop.
4. **Git CLI** configurado no PATH.

---

## 3. Validation Scenarios

### Scenario 1: Toolchain & SDK Verification
Comprova que o ambiente local atende aos requisitos do .NET 10 e arquitetura x64.

**Comando:**
```powershell
dotnet --info
```

**Resultado Esperado:**
- SDK versão 10.0.x instalada.
- Runtime `Microsoft.WindowsDesktop.App` 10.0.x presente.
- RID: `win-x64`.

---

### Scenario 2: Package Restore
Restaura todas as dependências de pacotes NuGet gerenciadas centralmente.

**Comando:**
```powershell
dotnet restore Resonance.sln -p:Platform=x64
```

**Resultado Esperado:**
- Saída exibindo restauração concluída com sucesso para `Resonance.Core`, `Resonance.WinUI`, `ResonanceAppFunctions` e `Resonance.Core.Tests`.
- Código de saída: `0`.

---

### Scenario 3: Solution Build Baseline
Compila todos os projetos da solution no modo Release direcionado para x64.

**Comando:**
```powershell
dotnet build Resonance.sln --configuration Release -p:Platform=x64 --no-restore
```

**Resultado Esperado:**
- Compilação limpa de todos os 4 projetos da solution (`Resonance.Core.dll`, `Resonance.WinUI.dll`, `ResonanceAppFunctions.dll`, `Resonance.Core.Tests.dll`).
- Código de saída: `0`.
- *Nota de Baseline*: O pacote `SixLabors.ImageSharp` 4.1.1 emite validação de licença no MSBuild. No modo `Debug`, o build continua normalmente (`ContinueOnError=true`). No modo `Release`, é necessário que a chave de licença de código aberto esteja configurada (`SixLaborsLicenseKey`) ou seja fornecido o stub de validação no targets.

---

### Scenario 4: Automated Test Suite Execution
Executa a suíte de testes de unidade e integração do núcleo de áudio e dados (`Resonance.Core.Tests`).

**Comando:**
```powershell
$env:DOTNET_CLI_UI_LANGUAGE = "en"
dotnet test tests\Resonance.Core.Tests\Resonance.Core.Tests.csproj --configuration Debug
```

**Resultado Esperado:**
- Execução dos testes via runner `Microsoft.Testing.Platform` / `xunit.v3`.
- 100% dos testes aprovados (845/845 testes aprovados sob cultura neutra/inglês).
- Código de saída: `0`.

---

### Scenario 5: Architecture & Inventory Audit Verification
Confronta os tipos reais do repositório com o catálogo registrado em `audit-matrix-contract.md`.

**Verificação Manual / Automatizada:**
- Confirmar existência de `IAudioPlayer` e `LibVlcAudioPlayerService` em `Resonance.Core`/`Resonance.WinUI`.
- Confirmar injeção de dependência única no `App.xaml.cs`.
- Confirmar que nenhuma classe paralela de scanner ou reprodução foi adicionada.

---

### Scenario 6: PRD Gap Matrix Alignment
Verifica se todas as 10 features do catálogo Spec Kit (`001` a `010`) possuem lacunas claramente delineadas em relação à baseline.

**Critério de Sucesso:**
- Todas as 10 features possuem referência formal no `gap-report-contract.md`.
- Nenhuma feature do roadmap é iniciada sem prévia concordância com este documento de baseline.
