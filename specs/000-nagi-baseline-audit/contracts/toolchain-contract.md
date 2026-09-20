# Toolchain & Execution Contract

**Feature**: `000-nagi-baseline-audit`  
**Contract Version**: 1.0.0  
**Scope**: Toolchain oficial do .NET para restauração, compilação, testes e empacotamento da solution Resonance/Nagi.

---

## 1. Environment Prerequisites

- **Operating System**: Windows 10 (versão 1809 / Build 17763 ou superior) ou Windows 11.
- **.NET SDK**: .NET 10.0 (versão mínima suportada: 10.0.300+; recomendada: 10.0.401).
- **Windows App SDK**: Versão 2.4.0 (Windows App SDK Runtime / Windows 10.0.26100 SDK).
- **Visual Studio / Build Tools**: Visual Studio 2026 ou Build Tools com as cargas de trabalho:
  - Desenvolvimento para desktop com .NET
  - Ferramentas de Build C++ do Windows App SDK (para runtime nativo do WinUI 3)
- **Git**: Git 2.40+ configurado no PATH do sistema.

---

## 2. Mandatory CLI Command Contracts

### 2.1 Package Restore Contract
Restaura todas as dependências centrais gerenciadas em `Directory.Packages.props`.

```powershell
dotnet restore Nagi.sln -p:Platform=x64
```

- **Exit Code**: 0
- **Time Limit**: < 120 segundos
- **Forbidden**: Não ignorar falhas de restauração de pacotes nativos (LibVLC ou WinAppSDK).

---

### 2.2 Solution Build Contract
Compila todos os projetos da solution (`Nagi.Core`, `Nagi.WinUI`, `NagiAppFunctions`, `Nagi.Core.Tests`).

```powershell
dotnet build Nagi.sln --configuration Release -p:Platform=x64 --no-restore
```

- **Exit Code**: 0
- **Warning Policy**: Avisos pré-existentes documentados no relatório de auditoria não devem ser aumentados. Novos avisos devem ser tratados como erro (`TreatWarningsAsErrors`).
- **Target Platform**: O flag `-p:Platform=x64` é estritamente obrigatório porque projetos WinUI 3 não compilam com `Any CPU`.

---

### 2.3 Automated Test Suite Contract
Executa a suíte de testes de unidade e integração sobre o runner MTP (`Microsoft.Testing.Platform`) e xUnit v3.

```powershell
$env:DOTNET_CLI_UI_LANGUAGE = "en"
dotnet test tests\Nagi.Core.Tests\Nagi.Core.Tests.csproj --configuration Release --no-build
```

- **Exit Code**: 0
- **Success Rate**: 100% dos testes suportados da baseline (845 testes) devem passar sem regressão.
- **Culture / Locale**: A variável `$env:DOTNET_CLI_UI_LANGUAGE = "en"` é mandatória para evitar falhas de asserção causadas por localização de recursos em ambientes não-ingleses (ex.: `pt-BR`).

---

### 2.4 Startup Verification Contract (Opcional para Validação da UI)
Executa o script de sanidade da inicialização do WinUI 3 com captura do tempo total de boot.

```powershell
powershell -ExecutionPolicy Bypass -File verify_startup.ps1
```

- **Exit Code**: 0
- **Output Marker**: `STARTUP_TOTAL_MS=<milissegundos>`
