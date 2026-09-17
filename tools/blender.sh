#!/usr/bin/env bash
# Subsistence — запуск Blender 4.3.2 LTS с локальным sysroot (без root)
set -e
source "$HOME/.cache/blender_env.sh"
BLENDER="$HOME/.cache/blender/blender"
SCRIPT="$1"; shift || true
if [ ! -x "$BLENDER" ]; then
  echo "Blender не найден ($BLENDER). См. tools/provision_libs.py или pip install bpy" >&2
  exit 1
fi
exec "$BLENDER" --background --factory-startup --python "$SCRIPT" -- "$@"
