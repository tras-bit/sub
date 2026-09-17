"""
Демонстрация процедурной анимации монстров (AI/MonsterMotion.cs):
рендерим одного монстра в 4 фазах — покой, шаг влево, шаг вправо, смерть.
Смещения/крены ровно те, что считает рантайм (bob 0.055, roll 5.5°, pitch 6.5°, смерть 82°).
"""
import sys, os, math
import bpy
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import subs_common as S
import models_monsters3 as M

OUT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "docs", "previews"))
R = math.radians
# фазы как в рантайме: покой (дыхание), два шага (bob+крен+наклон, погоня ×1.4), смерть
PHASES = [("покой", 0.0, 0.0, 0.012, "дыхание 1.5 Гц"),
          ("шаг: крен влево", 7.0 * 1.4, 8.0 * 1.4, 0.07 * 1.4, "bob 7 см · крен 7° · наклон 8° · погоня ×1.4"),
          ("шаг: крен вправо", -7.0 * 1.4, 8.0 * 1.4, 0.07 * 1.4, "то же, другая фаза шага"),
          ("смерть", 82.0, 0.0, -0.12, "заваливание 82° за 0.6 с")]

def main(only=None):
    for name, fn in M.BUILDERS:
        if only and name not in only:
            continue
        S.clean_scene()
        parts = fn()
        joined = S.join_objects(parts, name)
        S.add_bevel(joined, 0.0015, 1); S.apply_modifiers(joined)
        S.shade_smooth(joined, 34)
        for i, (label, roll, pitch, dy, note) in enumerate(PHASES):
            joined.rotation_euler = (R(pitch), R(roll * 0.4), R(roll))
            joined.location = (0, 0, dy)
            # фиксированный кадр: пол и стены в кадре, ничего не «улетает»
            S.render_preview(os.path.join(OUT, f"_anim_{name}_{i}.png"),
                             target=(0.0, 0.0, 0.95), distance=4.6, height=0.6,
                             samples=20, res=(420, 560), fov_deg=34,
                             key_energy=2000.0, rim_energy=700.0, floor_z=0.0,
                             azimuth=-52.0, elevation=8.0)
        print(f"[anim] {name}: 4 фазы")
_argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
_only = None
if "--only" in _argv:
    i = _argv.index("--only")
    _only = set(_argv[i + 1:]) or None
main(_only)
