#!/usr/bin/env python3
# ============================================================================
#  SUBSISTENCE — tools/self_test_lint.py
#  Самотест линтера: синтетический проект tools/_selftest повторяет ВСЕ ошибки,
#  которые Unity находил в раундах 1–2, плюс warning CS0219. Тест падает, если
#
#    • какая-то проверка перестала срабатывать (пропустит ошибку в Unity), ЛИБО
#    • появилась новая находка (ложное срабатывание на ровном месте).
#
#  Запуск: python3 tools/self_test_lint.py
#  Код выхода: 0 — линтер ловит всё ровно; 1 — расхождение с ожиданием.
# ============================================================================
import os
import re
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
SELFTEST = os.path.join(HERE, "_selftest")

# (метка, файл, строка) — ровно то, что должно находиться в синтетике
EXPECTED = [
    ("B", "AI.cs", 10),          # нет using для типа проекта (CS0246, MonsterAudio)
    ("I", "SaveSystem.cs", 12),  # surv.health вместо surv.State.health (CS1061)
    ("P", "Boot.cs", 13),        # Player — свойство, а не namespace (CS1061)
    ("Q", "UI.cs", 15),          # AttachIcon — метод чужого класса (CS0103)
    ("R", "Gen.cs", 10),         # rng — поле другого класса, не в этой области (CS0103)
    ("R", "Gen.cs", 16),         # netId — то же (CS0103)
    ("S", "SaveSystem.cs", 20),  # словарь без .Value (CS1061)
    ("T", "UI.cs", 16),          # мертвая переменная, warning CS0219
]

RE_F = re.compile(r"^\[([A-Z0-9]+)\]\s+([^:\s]+):(\d+)\s")


def main():
    cmd = [sys.executable, os.path.join(HERE, "check_cs_api.py"), SELFTEST]
    p = subprocess.run(cmd, capture_output=True, text=True)
    found = []
    for line in p.stdout.splitlines():
        m = RE_F.match(line.strip())
        if m:
            found.append((m.group(1), m.group(2), int(m.group(3))))

    ok = True
    for want in EXPECTED:
        if want not in found:
            ok = False
            print(f"[self_test] ПРОПУЩЕНО: {want[0]} {want[1]}:{want[2]} — линтер перестал это ловить")
    for got in found:
        if got not in EXPECTED:
            ok = False
            print(f"[self_test] ЛИШНЕЕ: {got[0]} {got[1]}:{got[2]} — ложное срабатывание или сдвиг строк")

    print(f"[self_test] ожидали {len(EXPECTED)} находок, линтер дал {len(found)}: "
          f"{'+'.join(sorted({g[0] for g in found}))}")
    if ok:
        print("[self_test] САМОТЕСТ ОК — все классы ошибок раундов 1–2 (B/I/P/Q/R/S) и CS0219 ловятся, "
              "посторонних находок нет")
        return 0
    print("[self_test] САМОТЕСТ ПРОВАЛЕН — правь tools/check_cs_api.py, а не ожидания")
    return 1


if __name__ == "__main__":
    sys.exit(main())
