#!/usr/bin/env python3
"""pack_archive.py — сборка архива: корень = Unity-проект.
Включает модели, код, доки и ТОЛЬКО сводные листы превью (иначе архив пухнет).
Запуск: python3 tools/pack_archive.py
"""
import os, zipfile, hashlib

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
NEW = os.path.join(os.path.dirname(ROOT), "Subsistence_1.0.0-alpha.zip")
SKIP = ("__pycache__", ".git", ".cache", ".arena", ".venv", "node_modules",
        "Library", "Temp", "obj", "Logs", "UserSettings", "Builds")
# из превью берём только листы и диорамы уровней (остальное — сотни файлов)
PREVIEW_KEEP = ("_sheet_", "level_", "_all_models_sheet", "_dbg_akm4")


def keep(rel):
    if any(p in SKIP for p in rel.split(os.sep)):
        return False
    # служебное для линтеров: вендоренный парсер C# и синтетика самотеста —
    # в игровом архиве не нужны (они лежат в воркспейсе рядом с tools)
    if rel.split(os.sep)[:2] in (["tools", "_vendor"], ["tools", "_selftest"], ["tools", "_internal"]):
        return False
    if rel.endswith((".pyc", ".DS_Store", ".tmp")):
        return False
    parts = rel.split(os.sep)
    if len(parts) >= 2 and parts[0] == "docs" and parts[1] == "previews":
        return parts[-1].startswith(PREVIEW_KEEP)     # только листы и диорамы
    return True


def main():
    entries = []
    for base, dirs, names in os.walk(ROOT):
        dirs[:] = [d for d in dirs if d not in SKIP]
        for n in names:
            full = os.path.join(base, n)
            rel = os.path.relpath(full, ROOT)
            if not keep(rel):
                continue
            if rel.startswith("UnityProject" + os.sep):
                arc = os.path.relpath(rel, "UnityProject")
            else:
                arc = rel
            entries.append((full, arc.replace(os.sep, "/")))
    entries.sort(key=lambda x: x[1])
    with zipfile.ZipFile(NEW, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as z:
        for full, arc in entries:
            z.write(full, arc)
    with zipfile.ZipFile(NEW) as z:
        names = z.namelist()
        bad = z.testzip()
        fbx = sum(1 for n in names if n.endswith(".fbx"))
        cs = sum(1 for n in names if n.endswith(".cs"))
        png = [n.split("/")[-1] for n in names if n.endswith(".png")]
    size = os.path.getsize(NEW) / 1024 / 1024
    print(f"[pack] файлов: {len(names)} | FBX: {fbx} | .cs: {cs} | PNG: {len(png)} | {size:.1f} МБ")
    print(f"[pack] целостность: {bad or 'ок'}")
    print(f"[pack] листы: {', '.join(sorted(png)[:8])}")
    print(f"[pack] {NEW}")
    print(f"[pack] sha256: {hashlib.sha256(open(NEW,'rb').read()).hexdigest()[:16]}…")


if __name__ == "__main__":
    main()
