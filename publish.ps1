<#
.SYNOPSIS
    Script de Publicação em Pasta (Standalone) para o Resonance Player.
.DESCRIPTION
    Publica o Resonance Player como aplicativo desempacotado (standalone) para as arquiteturas
    win-x64 (PCs tradicionais Intel/AMD) e/ou win-arm64 (Surface ARM, Snapdragon X Elite),
    utilizando a solução Resonance.slnx.
.PARAMETER Architecture
    Arquitetura de destino: 'x64', 'arm64' ou 'both'. Se não especificado, solicita interativamente.
.PARAMETER Configuration
    Configuração de build: 'Debug' (recomendado para uso local) ou 'Release'.
.PARAMETER OutputDirectory
    Diretório base onde as pastas publicadas serão criadas. Padrão: <repo_root>\publish.
.EXAMPLE
    .\publish.ps1
.EXAMPLE
    .\publish.ps1 -Architecture x64 -Configuration Debug
.EXAMPLE
    .\publish.ps1 -Architecture both -Configuration Debug -OutputDirectory "C:\ResonanceBuilds"
#>
[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet("x64", "win-x64", "arm64", "win-arm64", "both", "all")]
    [string]$Architecture,

    [Parameter(Position = 1)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration,

    [Parameter(Position = 2)]
    [string]$OutputDirectory
)

$ErrorActionPreference = "Stop"

Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host "          Resonance Player - Script de Publicação em Pasta            " -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host ""

# 1. Verificar dotnet CLI
if (-not (Get-Command "dotnet" -ErrorAction SilentlyContinue)) {
    Write-Error "O comando 'dotnet' não foi encontrado no PATH. Instale o .NET 10 SDK e tente novamente."
    return
}

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$SolutionFile = Join-Path $ScriptDir "Resonance.slnx"
$ProjectFile = Join-Path $ScriptDir "src\Resonance.WinUI\Resonance.WinUI.csproj"

if (-not (Test-Path $SolutionFile)) {
    Write-Error "Não foi possível localizar o arquivo de solução Resonance.slnx em: $SolutionFile"
    return
}

if (-not (Test-Path $ProjectFile)) {
    Write-Error "Não foi possível localizar o arquivo de projeto em: $ProjectFile"
    return
}

# 2. Seleção interativa de arquitetura se não informada
if ([string]::IsNullOrWhiteSpace($Architecture)) {
    Write-Host "Selecione a arquitetura desejada:" -ForegroundColor Yellow
    Write-Host "  [1] win-x64   - Padrão para PCs tradicionais Intel / AMD 64-bit"
    Write-Host "  [2] win-arm64 - Dispositivos ARM: Surface Pro ARM, Snapdragon X Elite"
    Write-Host "  [3] Ambos     - Publicar ambas as arquiteturas"
    Write-Host ""
    $ChoiceRid = Read-Host "Opção [1, 2 ou 3] (padrão: 1)"
    if ([string]::IsNullOrWhiteSpace($ChoiceRid)) { $ChoiceRid = "1" }

    switch ($ChoiceRid.Trim()) {
        "1" { $TargetRids = @("win-x64") }
        "2" { $TargetRids = @("win-arm64") }
        "3" { $TargetRids = @("win-x64", "win-arm64") }
        default { $TargetRids = @("win-x64") }
    }
} else {
    switch ($Architecture.ToLowerInvariant()) {
        { $_ -in "all", "both" } { $TargetRids = @("win-x64", "win-arm64") }
        { $_ -in "x64", "win-x64" } { $TargetRids = @("win-x64") }
        { $_ -in "arm64", "win-arm64" } { $TargetRids = @("win-arm64") }
        default { $TargetRids = @("win-x64") }
    }
}

# 3. Seleção interativa de configuração se não informada
if ([string]::IsNullOrWhiteSpace($Configuration)) {
    Write-Host ""
    Write-Host "Selecione a configuração de compilação:" -ForegroundColor Yellow
    Write-Host "  [1] Debug   - Recomendado: não exige chave comercial local de ImageSharp"
    Write-Host "  [2] Release - Otimizado"
    Write-Host ""
    $ChoiceConfig = Read-Host "Opção [1 ou 2] (padrão: 1)"
    if ([string]::IsNullOrWhiteSpace($ChoiceConfig)) { $ChoiceConfig = "1" }

    switch ($ChoiceConfig.Trim()) {
        "1" { $Configuration = "Debug" }
        "2" { $Configuration = "Release" }
        default { $Configuration = "Debug" }
    }
}

# 4. Diretório de saída
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $BaseOutputDir = Join-Path $ScriptDir "publish"
} else {
    $BaseOutputDir = $OutputDirectory
}

Write-Host ""
Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host "Configuração da Publicação:" -ForegroundColor Cyan
Write-Host " - Solução:        $SolutionFile"
Write-Host " - Projeto:        $ProjectFile"
Write-Host " - Configuração:   $Configuration"
Write-Host " - Arquitetura(s): $($TargetRids -join ', ')"
Write-Host " - Pasta Base:     $BaseOutputDir"
Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host ""

# 5. Executar publicação para cada RID
foreach ($rid in $TargetRids) {
    $platform = if ($rid -eq "win-arm64") { "ARM64" } else { "x64" }
    $currentOutDir = Join-Path $BaseOutputDir $rid

    Write-Host "----------------------------------------------------------------------" -ForegroundColor DarkCyan
    Write-Host "[PUBLICANDO] $rid ($Configuration) em $currentOutDir ..." -ForegroundColor Green
    Write-Host "----------------------------------------------------------------------" -ForegroundColor DarkCyan

    $publishArgs = @(
        "publish",
        "`"$ProjectFile`"",
        "-c", $Configuration,
        "-p:Platform=$platform",
        "-r", $rid,
        "--self-contained", "true",
        "-p:WindowsPackageType=None",
        "-o", "`"$currentOutDir`""
    )

    $proc = Start-Process -FilePath "dotnet" -ArgumentList ($publishArgs -join " ") -NoNewWindow -Wait -PassThru

    if ($proc.ExitCode -ne 0) {
        Write-Host ""
        Write-Host "[ERRO] Falha ao publicar para $rid (Código de saída: $($proc.ExitCode))." -ForegroundColor Red
        if ($Configuration -eq "Release") {
            Write-Host "DICA: Caso o erro seja relacionado à licença do SixLabors.ImageSharp, publique em modo Debug." -ForegroundColor Yellow
        }
        return
    }

    Write-Host ""
    Write-Host "[SUCESSO] $rid publicado com êxito!" -ForegroundColor Green
    Write-Host "Executável pronto em: $(Join-Path $currentOutDir 'Resonance.exe')" -ForegroundColor Cyan
    Write-Host ""
}

Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host "          Publicação concluída com sucesso!                           " -ForegroundColor Green
Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host ""
