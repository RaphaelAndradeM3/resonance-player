@echo off
setlocal enabledelayedexpansion

echo ======================================================================
echo           Resonance Player - Script de Publicacao em Pasta
echo ======================================================================
echo.

:: 1. Verificar se o dotnet CLI esta disponivel
where dotnet >nul 2>nul
if %errorlevel% neq 0 (
    echo [ERRO] O comando 'dotnet' nao foi encontrado no PATH.
    echo Por favor, instale o .NET 10 SDK e tente novamente.
    pause
    exit /b 1
)

:: Obter o diretorio raiz do repositorio (onde este script reside)
set "REPO_ROOT=%~dp0"
set "SLN_PATH=%REPO_ROOT%Resonance.slnx"
set "PROJ_PATH=%REPO_ROOT%src\Resonance.WinUI\Resonance.WinUI.csproj"

if not exist "%SLN_PATH%" (
    echo [ERRO] Nao foi possivel encontrar a solucao Resonance.slnx em:
    echo "%SLN_PATH%"
    pause
    exit /b 1
)

if not exist "%PROJ_PATH%" (
    echo [ERRO] Nao foi possivel encontrar o projeto em:
    echo "%PROJ_PATH%"
    pause
    exit /b 1
)

:: 2. Permitir passagem de argumentos por linha de comando
:: Uso: publish.bat [RID: x64|arm64|all] [CONFIG: Debug|Release] [OUTDIR]
set "ARG_RID=%~1"
set "ARG_CONFIG=%~2"
set "ARG_OUTDIR=%~3"

if not "%ARG_RID%"=="" goto PROCESS_RID

echo Selecione a arquitetura desejada:
echo   [1] win-x64   - Padrao para PCs tradicionais Intel / AMD 64-bit
echo   [2] win-arm64 - Dispositivos ARM: Surface Pro ARM, Snapdragon X Elite
echo   [3] Ambos     - Publicar ambas as arquiteturas
echo.
set "CHOICE_RID=1"
set /p "CHOICE_RID=Opcao [1, 2 ou 3] (padrao: 1): "

if "%CHOICE_RID%"=="1" set "TARGET_RIDS=win-x64"
if "%CHOICE_RID%"=="2" set "TARGET_RIDS=win-arm64"
if "%CHOICE_RID%"=="3" set "TARGET_RIDS=win-x64 win-arm64"
if "%TARGET_RIDS%"=="" set "TARGET_RIDS=win-x64"
goto SELECT_CONFIG

:PROCESS_RID
if /i "%ARG_RID%"=="all" set "TARGET_RIDS=win-x64 win-arm64"
if /i "%ARG_RID%"=="both" set "TARGET_RIDS=win-x64 win-arm64"
if /i "%ARG_RID%"=="x64" set "TARGET_RIDS=win-x64"
if /i "%ARG_RID%"=="arm64" set "TARGET_RIDS=win-arm64"
if /i "%ARG_RID%"=="win-x64" set "TARGET_RIDS=win-x64"
if /i "%ARG_RID%"=="win-arm64" set "TARGET_RIDS=win-arm64"
if "%TARGET_RIDS%"=="" set "TARGET_RIDS=%ARG_RID%"

:SELECT_CONFIG
if not "%ARG_CONFIG%"=="" (
    set "CONFIG=%ARG_CONFIG%"
    goto SELECT_OUTDIR
)

echo.
echo Selecione a configuracao:
echo   [1] Debug   - Recomendado: nao exige chave comercial local de ImageSharp
echo   [2] Release - Otimizado
echo.
set "CHOICE_CONFIG=1"
set /p "CHOICE_CONFIG=Opcao [1 ou 2] (padrao: 1): "

if "%CHOICE_CONFIG%"=="1" set "CONFIG=Debug"
if "%CHOICE_CONFIG%"=="2" set "CONFIG=Release"
if "%CONFIG%"=="" set "CONFIG=Debug"

:SELECT_OUTDIR
if "%ARG_OUTDIR%"=="" (
    set "BASE_OUTDIR=%REPO_ROOT%publish"
) else (
    set "BASE_OUTDIR=%ARG_OUTDIR%"
)

echo.
echo ======================================================================
echo Configurando Publicacao:
echo - Solucao:        %SLN_PATH%
echo - Projeto:        %PROJ_PATH%
echo - Configuracao:   %CONFIG%
echo - Arquitetura(s): %TARGET_RIDS%
echo - Pasta Base:     %BASE_OUTDIR%
echo ======================================================================
echo.

:: 3. Executar publicacao para cada arquitetura
for %%R in (%TARGET_RIDS%) do (
    call :PUBLISH_ONE "%%R"
    if !ERRORLEVEL! neq 0 (
        echo.
        echo [ERRO] Ocorreu uma falha durante a publicacao de %%R.
        pause
        exit /b 1
    )
)

echo ======================================================================
echo           Publicacao concluida com sucesso!
echo ======================================================================
echo Os arquivos estao disponiveis em: %BASE_OUTDIR%
echo.
pause
exit /b 0

:: Subrotina para publicar uma arquitetura especifica
:PUBLISH_ONE
set "CURR_RID=%~1"
set "CURR_PLATFORM=x64"
if /i "%CURR_RID%"=="win-arm64" set "CURR_PLATFORM=ARM64"
if /i "%CURR_RID%"=="arm64" set "CURR_PLATFORM=ARM64"

set "CURR_OUTDIR=%BASE_OUTDIR%\%CURR_RID%"

echo ----------------------------------------------------------------------
echo [PUBLICANDO] %CURR_RID% (%CONFIG%) em %CURR_OUTDIR% ...
echo ----------------------------------------------------------------------

dotnet publish "%PROJ_PATH%" -c "%CONFIG%" -p:Platform=%CURR_PLATFORM% -r %CURR_RID% --self-contained true -p:WindowsPackageType=None -o "%CURR_OUTDIR%"

if %ERRORLEVEL% neq 0 (
    echo.
    echo [ERRO] Falha ao publicar para %CURR_RID%.
    if /i "%CONFIG%"=="Release" (
        echo DICA: Se o erro for de licenca SixLabors.ImageSharp, use Debug:
        echo      publish.bat %CURR_RID% Debug
    )
    exit /b 1
)

echo.
echo [SUCESSO] %CURR_RID% publicado com exito!
echo Executavel pronto em: %CURR_OUTDIR%\Resonance.exe
echo.
exit /b 0
