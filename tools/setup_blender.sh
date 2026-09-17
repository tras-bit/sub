#!/usr/bin/env bash
# SUBSISTENCE — восстановление Blender 4.3.2 LTS в песочнице.
# Кэш ~/.cache не переживает рестарт песочницы, поэтому этот скрипт ставят заново:
#   bash tools/setup_blender.sh
# Качает официальный linux-x64 tarball, распаковывает в ~/.cache/blender и
# добивает системные библиотеки через tools/provision_libs.py (без root).
set -e
VER=4.3.2
URL="https://cdn.blender.org/release/Blender4.3/blender-${VER}-linux-x64.tar.xz"
DL="$HOME/.cache/dl"
TAR="$DL/blender-${VER}-linux-x64.tar.xz"
HERE="$(cd "$(dirname "$0")" && pwd)"

mkdir -p "$DL" "$HOME/.cache"

if [ ! -s "$TAR" ]; then
  echo "[1/3] качаю Blender $VER (~350 МБ) — это несколько минут ..."
  curl -L --fail --retry 3 --retry-delay 5 -o "$TAR" "$URL"
else
  echo "[1/3] архив уже скачан: $TAR"
fi

if [ ! -x "$HOME/.cache/blender/blender" ]; then
  echo "[2/3] распаковываю ..."
  rm -rf "$HOME/.cache/blender"
  tar -xf "$TAR" -C "$HOME/.cache"
  mv "$HOME/.cache/blender-${VER}-linux-x64" "$HOME/.cache/blender"
else
  echo "[2/3] Blender уже распакован"
fi

echo "[3/3] подкладываю системные библиотеки (libxkbcommon и компания) ..."
python3 "$HERE/provision_libs.py" --target "$HOME/.cache/blender/blender"

"$HOME/.cache/blender/blender" --version | head -2
echo "[OK] Blender готов: $HOME/.cache/blender/blender"
