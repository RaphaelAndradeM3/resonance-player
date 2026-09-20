# Quickstart: Feature 001 — Recursive Root Library Hardening

**Branch**: `001-recursive-root-library` | **Feature**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)  
**Date**: 2026-09-20  

---

## 1. Visão Geral

Este guia descreve os passos e comandos executáveis para validar ponta a ponta as capacidades entregues pela **Feature 001**:
1. Travessia recursiva profunda sem limites arbitrários de pastas.
2. Prevenção de loops infinitos em *NTFS Junctions* e *Symbolic Links*.
3. Consolidação inteligente de raízes sobrepostas.
4. Sincronização incremental com remoção (*hard delete*) de faixas ausentes sob raízes acessíveis.
5. Preservação integral do catálogo quando raízes inteiras estiverem offline/desconectadas.
6. Cancelamento cooperativo rápido (< 1s) sem travamento da interface.

---

## 2. Pré-requisitos

* SDK .NET 10.0 instalado (`dotnet --version`).
* Cargas de trabalho Windows Desktop e WinUI 3 instaladas.
* Powershell 7+ ou Windows PowerShell.

---

## 3. Roteiro Executável de Testes e Validação

### Passo 1: Restauração e Compilação da Solution
```powershell
dotnet restore Resonance.sln -p:Platform=x64
dotnet build Resonance.sln --configuration Release -p:Platform=x64 --no-restore
```
*Critério de Sucesso*: 0 erros em todos os projetos da solution.

---

### Passo 2: Validação de Travessia e Proteção contra Ciclos (Slice 1)
Executa os testes unitários dedicados à enumeração segura de arquivos e detecção de ciclos:
```powershell
$env:DOTNET_CLI_UI_LANGUAGE = "en"
dotnet test tests/Resonance.Core.Tests/Resonance.Core.Tests.csproj --filter "FullyQualifiedName~SafeFileEnumerator" --no-build
```
*Cenários Validados*:
* Criação de árvore sintética com 10 níveis de subpastas.
* Inclusão de links simbólicos/junções cíclicas apontando para diretórios pais (garantindo que o enumerador descarte o ciclo e termine normalmente).
* Presença de subpastas inacessíveis ou arquivos com permissão negada (garantindo que o enumerador continue sem lançar exceção não-tratada).

---

### Passo 3: Validação de Consolidação de Raízes e Varredura Incremental (Slice 2)
Executa os testes de lógica de sobreposição e persistência transacional:
```powershell
$env:DOTNET_CLI_UI_LANGUAGE = "en"
dotnet test tests/Resonance.Core.Tests/Resonance.Core.Tests.csproj --filter "FullyQualifiedName~RootOverlapValidator|FullyQualifiedName~LibraryService" --no-build
```
*Cenários Validados*:
* Avaliação de sobreposição de caminhos: rejeição de subpastas quando a pasta pai já existe; absorção de subpastas ao adicionar o ancestral.
* Varredura incremental: adição de nova música indexada sem reprocessar arquivos inalterados.
* Exclusão segura: arquivo apagado da pasta raiz acessível é expurgado do banco de dados SQLite.
* Raiz offline: pasta raiz com volume simulado inacessível é ignorada, mantendo 100% de suas músicas gravadas no banco de dados.

---

### Passo 4: Validação de Cancelamento e UI (Slice 3)
Executa os testes de cancelamento rápido e notificação de progresso:
```powershell
$env:DOTNET_CLI_UI_LANGUAGE = "en"
dotnet test tests/Resonance.Core.Tests/Resonance.Core.Tests.csproj --filter "FullyQualifiedName~ScanCancellation|FullyQualifiedName~ScanProgress" --no-build
```
*Cenários Validados*:
* Acionamento de `CancellationTokenSource.Cancel()` durante o processamento de lotes.
* O processo é interrompido em menos de 1000ms.
* O banco de dados preserva o estado do último lote completado de forma consistente.

---

### Passo 5: Gate Final Constitucional (Whole-Solution Validation)
```powershell
dotnet test tests/Resonance.Core.Tests/Resonance.Core.Tests.csproj --configuration Release --no-build
```
*Critério de Sucesso*: 100% dos testes da suíte aprovados sem nenhuma regressão.
