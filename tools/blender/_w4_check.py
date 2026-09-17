"""Быстрая проверка геометрии оружия: 2 рендера (силуэт + крупный план) при малых сэмплах.
Запуск: python3 tools/bpy_run.py tools/blender/_w4_check.py -- W_rifle_ak [W_rifle_m4 ...]"""
import sys, os, math
import bpy
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import subs_common as S
import models_weapons as W

PREV = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "docs", "previews"))
WANT = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

for name, fn in W.BUILDERS:
    if WANT and name not in WANT:
        continue
    S.clean_scene()
    m = W._mats()
    obj = fn(m)
    if obj is None:
        print(f"[chk] {name}: пусто")
        continue
    c, sz, _ = S.bounds_of([obj])
    ext = max(sz.x, sz.y, sz.z)
    pad = 2.1 if ext < 0.5 else (1.05 if ext < 1.0 else 0.62)
    S.render_fit(os.path.join(PREV, "_wchk_" + name + ".png"), [obj], samples=14,
                 res=(760, 400), fov_deg=26, azimuth=-33.0, elevation=15.0, pad=pad)
    d = W.DETAIL.get(name)
    if d:
        W.render_detail(os.path.join(PREV, "_wchk_detail_" + name + ".png"), d[0], d[1],
                        azimuth=d[2], elevation=d[3], samples=14, res=(760, 440))
    print(f"[chk] {name}: трис {S.tri_count(obj)}, габарит {sz.x:.3f}×{sz.y:.3f}×{sz.z:.3f}")
