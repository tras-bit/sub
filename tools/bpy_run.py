#!/usr/bin/env python3
"""Запуск скрипта внутри bpy-модуля (Blender как Python-модуль, без отдельного бинарника).
Использование: python3 tools/bpy_run.py tools/blender/models_weapons.py -- --only W_rifle_ak
"""
import sys, runpy
script = sys.argv[1]
sys.argv = [script] + sys.argv[2:]
runpy.run_path(script, run_name="__main__")
