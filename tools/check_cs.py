#!/usr/bin/env python3
"""
check_cs.py — приёмка синтаксиса C# ПЕРЕД упаковкой архива.

Зачем: ошибки вида `error CS1002: ; expected` / `CS1513: } expected` ловятся
только компилятором, а Unity у пользователя — единственная «сборка». Этот скрипт
гоняет все .cs файлы проекта через настоящий парсер C# (tree-sitter) и падает,
если в дереве разбора есть ERROR/MISSING узлы. Именно так ловится:
  • пропущенная `;` в конце инструкции или выражения-тела метода (`=> x`);
  • разорванный инициализатор: `new List<T>(16)` + `{ ... }` без `;` после `}`;
  • незакрытые скобки/кавычки.

Установка (один раз, в песочнице):  pip3 install tree_sitter tree_sitter_c_sharp
Запуск:  python3 tools/check_cs.py [путь_к_Scripts]     (по умолчанию — находит сам)
Выход: 0 — чисто, 1 — есть ошибки (печатает файл:строка:колонка и сам текст).
"""
# парсер C# лежит рядом (tools/_vendor): окружение песочницы обнуляется между запусками
import os as _os, sys as _sys
_VENDOR = _os.path.join(_os.path.dirname(_os.path.abspath(__file__)), "_vendor")
if _os.path.isdir(_VENDOR) and _VENDOR not in _sys.path:
    _sys.path.insert(0, _VENDOR)

import os
import sys

try:
    import tree_sitter
    import tree_sitter_c_sharp
    from tree_sitter import Language, Parser
    _LANG = Language(tree_sitter_c_sharp.language())
    _PARSER = Parser(_LANG)
except Exception as exc:                                     # noqa: BLE001
    print(f"[check_cs] НЕТ ПАРСЕРА: {exc}")
    print("           поставь: pip3 install tree_sitter tree_sitter_c_sharp")
    sys.exit(2)


def find_scripts_dir(argv):
    if len(argv) > 1 and os.path.isdir(argv[1]):
        return argv[1]
    here = os.path.dirname(os.path.abspath(__file__))
    root = os.path.dirname(here)                              # корень проекта
    for cand in (
        os.path.join(root, "Assets", "Subsistence", "Scripts"),
        os.path.join(root, "UnityProject", "Assets", "Subsistence", "Scripts"),
    ):
        if os.path.isdir(cand):
            return cand
    return root


def collect(folder):
    out = []
    for base, _dirs, names in os.walk(folder):
        for n in names:
            if n.endswith(".cs"):
                out.append(os.path.join(base, n))
    return sorted(out)


def bad_nodes(node, acc):
    if node.type == "ERROR" or node.is_missing:
        acc.append(node)
    for ch in node.children:
        bad_nodes(ch, acc)


def main():
    folder = find_scripts_dir(sys.argv)
    files = collect(folder)
    if not files:
        print(f"[check_cs] не нашёл .cs в {folder}")
        return 1

    broken = 0
    for path in files:
        src = open(path, "rb").read()
        tree = _PARSER.parse(src)
        acc = []
        bad_nodes(tree.root_node, acc)
        if not acc:
            continue
        broken += 1
        rel = os.path.relpath(path, folder)
        lines = src.split(b"\n")
        print(f"[ОШИБКА СИНТАКСИСА] {rel}  —  мест: {len(acc)}")
        for nd in acc[:10]:
            row = nd.start_point[0]
            text = lines[row][:150].decode("utf-8", "replace").strip() if row < len(lines) else ""
            tag = "MISSING" if nd.is_missing else "ERROR"
            print(f"    строка {row + 1}, кол {nd.start_point[1] + 1}:  {tag} {nd.type}   |  {text}")

    print(f"\n[check_cs] файлов: {len(files)}, с ошибками: {broken}")
    if broken == 0:
        print("[check_cs] СИНТАКСИС ЧИСТ — Unity не должен ругаться на CS1002/CS1513.")
        return 0
    print("[check_cs] исправь ошибки выше и запусти снова.")
    return 1


if __name__ == "__main__":
    sys.exit(main())
