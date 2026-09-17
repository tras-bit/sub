"""
Черновая проверка оружия: крупные рендеры выбранных стволов в docs/previews/_dbg_w*.png.
Запуск: bash tools/blender.sh _dbg_weapons.py
"""
import sys, os
import bpy
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import subs_common as S
import models_weapons as W

OUT = W.OUT
RENDER_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "docs", "previews"))

WANT = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

CASES = [
    ("W_rifle_ak",      lambda m: W.build_ak(m["metal"], m["poly"], m["wood"]), 1.15),
    ("W_rifle_m4",      lambda m: W.build_m4(m["metal"], m["poly"]), 1.15),
    ("W_shotgun_pump",  lambda m: W.build_shotgun(m["metal"], m["poly"]), 1.25),
    ("W_lmg_m249",      lambda m: W.build_lmg(m["metal"], m["poly"]), 1.35),
    ("W_smg_mp5",       lambda m: W.build_smg(m["metal"], m["poly"]), 1.05),
    ("W_rifle_bolt",    lambda m: W.build_bolt_sniper(m["metal"], m["poly"]), 1.45),
]

for name, fn, dist in CASES:
    if WANT and name not in WANT:
        continue
    S.clean_scene()
    m = W._mats()
    obj = fn(m)
    S.smart_uv(obj)
    S.add_bevel(obj, 0.0015, 1)
    S.apply_modifiers(obj)
    S.shade_smooth(obj, 30)
    print(f"[dbg] {name}: {S.tri_count(obj)} трис")
    S.render_fit(os.path.join(RENDER_DIR, "_dbg_" + name + ".png"), [obj], samples=32,
                 res=(1200, 620), fov_deg=26, azimuth=-33.0, elevation=15.0, pad=0.62)
    S.render_fit(os.path.join(RENDER_DIR, "_dbg_" + name + "_side.png"), [obj], samples=26,
                 res=(1200, 480), fov_deg=22, azimuth=0.0, elevation=6.0, pad=0.50)
print("[dbg] готово")
