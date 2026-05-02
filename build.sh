#!/usr/bin/env bash
set -euo pipefail

# 项目根（本脚本所在目录）
ROOT="$(cd "$(dirname "$0")" && pwd)"
CSPROJ="$ROOT/Source/Celeste_X_DGLAB.csproj"
OUT_BIN="$ROOT/bin"

# 与 Celeste.dll 同目录，供 Publicizer / 引用
: "${CELESTE_PREFIX:=/home/niko/Games/Celeste/game}"
# 安装到游戏 Mods 下本 mod 目录（会覆盖 everest.yaml 与 bin 内文件）
: "${CELESTE_MOD_DIR:=$CELESTE_PREFIX/Mods/Celeste_X_DGLAB}"

CONFIG="Debug"
for arg in "$@"; do
  case "$arg" in
    -r|--release) CONFIG="Release" ;;
  esac
done

echo "==> dotnet build ($CONFIG)  CELESTE_PREFIX=$CELESTE_PREFIX"
dotnet build "$CSPROJ" -c "$CONFIG" -p:CelestePrefix="$CELESTE_PREFIX" -v minimal

if [[ ! -f "$OUT_BIN/Celeste_X_DGLAB.dll" ]]; then
  echo "error: 未找到 $OUT_BIN/Celeste_X_DGLAB.dll" >&2
  exit 1
fi

echo "==> 覆盖安装到: $CELESTE_MOD_DIR"
install -d "$CELESTE_MOD_DIR/bin"
install -m644 "$ROOT/everest.yaml" "$CELESTE_MOD_DIR/everest.yaml"
install -m644 "$OUT_BIN/Celeste_X_DGLAB.dll" "$CELESTE_MOD_DIR/bin/Celeste_X_DGLAB.dll"
if [[ -f "$OUT_BIN/Celeste_X_DGLAB.pdb" ]]; then
  install -m644 "$OUT_BIN/Celeste_X_DGLAB.pdb" "$CELESTE_MOD_DIR/bin/Celeste_X_DGLAB.pdb"
fi

echo "==> 完成"
