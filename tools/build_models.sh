#!/usr/bin/env bash
# SUBSISTENCE — сборка всех моделей (Blender 4.3.2 LTS, headless)
# Использование: bash tools/build_models.sh [weapons|chars|props|transport|deploy|loot|all] [--no-render]
#   weapons   — 9 стволов Rust-набора      → Models/Weapons/
#   chars     — монстры (Smiler/AI)        → Models/Characters/
#   props     — пропсы + строит. детали    → Models/Props/
#   transport — транспорт/лифт/вендинг     → Models/Props/   (PR_scooter, PR_loot_cart,
#               PR_minecart, PR_vending_machine, BD_elevator_car)
#   deploy    — деплои игрока (22 модели)  → Models/Props/   (DD_workbench, DD_furnace,
#               DD_cupboard, DD_bed, DD_autoturret, DD_door_*, DD_trap_*, …)
#   loot      — лут-контейнеры (7 моделей)  → Models/Props/   (PR_supply_crate, PR_safe_box,
#               PR_barrel, PR_barrel_radioactive, PR_toolbox, PR_medical_cabinet, PR_airdrop_crate)
#   NAME      — одиночная модель: bash tools/build_models.sh props --only PR_loot_bag
# ВНИМАНИЕ: хазмат-костюм собирается ТОЛЬКО скриптом chars_hazmat.py (models_chars.py
# его не трогает). Запуск: bash tools/blender.sh chars_hazmat.py
set -e
HERE="$(cd "$(dirname "$0")" && pwd)"
WHAT="${1:-all}"; shift || true
ARGS="$@"
source "$HOME/.cache/blender_env.sh"
BLENDER="$HOME/.cache/blender/blender"
run() { "$BLENDER" --background --factory-startup --python "$HERE/blender/$1" -- $ARGS 2>&1 | grep -vE "^(Blender|Read|Info|Fra:)" | tail -40; }
case "$WHAT" in
  weapons)   run models_weapons.py ;;
  chars)     run models_chars.py ;;
  props)     run models_props.py ;;
  transport) run models_transport.py ;;
  deploy)    run models_deployables.py ;;
  loot)      run models_loot.py ;;
  all)       run models_weapons.py; run models_chars.py; run models_props.py; run models_transport.py; run models_deployables.py; run models_loot.py ;;
  *) echo "usage: $0 [weapons|chars|props|transport|deploy|loot|all] [--no-render] [--only MODEL]"; exit 1 ;;
esac
echo "[build_models] готово → UnityProject/Assets/Subsistence/Models/"
