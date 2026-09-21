#!/usr/bin/env bash
set -e

echo "======================================================================"
echo "          Resonance Player - Script de Publicação em Pasta"
echo "======================================================================"
echo ""

# 1. Verificar se o dotnet CLI está disponível
if ! command -v dotnet &> /dev/null; then
    echo "[ERRO] O comando 'dotnet' não foi encontrado no PATH."
    echo "Por favor, instale o .NET 10 SDK e tente novamente."
    exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SLN_PATH="${SCRIPT_DIR}/Resonance.slnx"
PROJ_PATH="${SCRIPT_DIR}/src/Resonance.WinUI/Resonance.WinUI.csproj"

if [ ! -f "$SLN_PATH" ]; then
    echo "[ERRO] Não foi possível encontrar a solução Resonance.slnx em:"
    echo "$SLN_PATH"
    exit 1
fi

if [ ! -f "$PROJ_PATH" ]; then
    echo "[ERRO] Não foi possível encontrar o projeto em:"
    echo "$PROJ_PATH"
    exit 1
fi

# 2. Argumentos de linha de comando
# Uso: ./publish.sh [RID: x64|arm64|all] [CONFIG: Debug|Release] [OUTDIR]
ARG_RID="${1:-}"
ARG_CONFIG="${2:-}"
ARG_OUTDIR="${3:-}"

if [ -z "$ARG_RID" ]; then
    echo "Selecione a arquitetura desejada:"
    echo "  [1] win-x64   - Padrão para PCs tradicionais Intel / AMD 64-bit"
    echo "  [2] win-arm64 - Dispositivos ARM: Surface Pro ARM, Snapdragon X Elite"
    echo "  [3] Ambos     - Publicar ambas as arquiteturas"
    echo ""
    read -r -p "Opção [1, 2 ou 3] (padrão: 1): " CHOICE_RID
    CHOICE_RID="${CHOICE_RID:-1}"

    case "$CHOICE_RID" in
        1) TARGET_RIDS=("win-x64") ;;
        2) TARGET_RIDS=("win-arm64") ;;
        3) TARGET_RIDS=("win-x64" "win-arm64") ;;
        *) TARGET_RIDS=("win-x64") ;;
    esac
else
    case "${ARG_RID,,}" in
        all|both) TARGET_RIDS=("win-x64" "win-arm64") ;;
        x64|win-x64) TARGET_RIDS=("win-x64") ;;
        arm64|win-arm64) TARGET_RIDS=("win-arm64") ;;
        *) TARGET_RIDS=("$ARG_RID") ;;
    esac
fi

if [ -z "$ARG_CONFIG" ]; then
    echo ""
    echo "Selecione a configuração:"
    echo "  [1] Debug   - Recomendado: não exige chave comercial de ImageSharp"
    echo "  [2] Release - Otimizado"
    echo ""
    read -r -p "Opção [1 ou 2] (padrão: 1): " CHOICE_CONFIG
    CHOICE_CONFIG="${CHOICE_CONFIG:-1}"

    case "$CHOICE_CONFIG" in
        1) CONFIG="Debug" ;;
        2) CONFIG="Release" ;;
        *) CONFIG="Debug" ;;
    esac
else
    CONFIG="$ARG_CONFIG"
fi

if [ -z "$ARG_OUTDIR" ]; then
    BASE_OUTDIR="${SCRIPT_DIR}/publish"
else
    BASE_OUTDIR="$ARG_OUTDIR"
fi

echo ""
echo "======================================================================"
echo "Configurando Publicação:"
echo "- Solução:        $SLN_PATH"
echo "- Projeto:        $PROJ_PATH"
echo "- Configuração:   $CONFIG"
echo "- Arquitetura(s): ${TARGET_RIDS[*]}"
echo "- Pasta Base:     $BASE_OUTDIR"
echo "======================================================================"
echo ""

# 3. Executar publicação para cada arquitetura selecionada
for RID in "${TARGET_RIDS[@]}"; do
    if [ "$RID" = "win-arm64" ] || [ "$RID" = "arm64" ]; then
        PLATFORM="ARM64"
    else
        PLATFORM="x64"
    fi

    CURRENT_OUTDIR="${BASE_OUTDIR}/${RID}"

    echo "----------------------------------------------------------------------"
    echo "[PUBLICANDO] $RID ($CONFIG) em $CURRENT_OUTDIR ..."
    echo "----------------------------------------------------------------------"

    dotnet publish "$PROJ_PATH" \
        -c "$CONFIG" \
        -p:Platform="$PLATFORM" \
        -r "$RID" \
        --self-contained true \
        -p:WindowsPackageType=None \
        -o "$CURRENT_OUTDIR"

    echo ""
    echo "[SUCESSO] $RID publicado com êxito!"
    echo "Executável pronto em: ${CURRENT_OUTDIR}/Resonance.exe"
    echo ""
done

echo "======================================================================"
echo "          Publicação concluída com sucesso!"
echo "======================================================================"
echo ""
