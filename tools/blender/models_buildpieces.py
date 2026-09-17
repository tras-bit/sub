"""
SUBSISTENCE — models_buildpieces.py
Модели элементов постройки, которых не было (раньше ставились кубами или чужой моделью):
  BD_foundation_tri  — треугольный фундамент (3×3, половина квадрата)
  BD_floor_tri       — треугольное перекрытие
  BD_roof            — крыша (скатная плита 3×3 с уклоном и кромкой)
  BD_ramp            — пандус (лестница-плоскость 3 м длиной, подъём 1.5 м)
  BD_ramp_corner     — рампа-угол (поворот на 90°)
  BD_high_wall       — высокая стена (3×6, с двумя поясами жёсткости)
  BD_pillar          — столб (0.36×3, с базой и оголовком)
  BD_railing         — перила (3 м, стойки + две перекладины + балясины)
  BD_shutters        — ставни (рама + ламели, закрывают проём 2.2×2.2)

Сетка стройки — 3 м (BuildController.GridSize), стена 3 м в высоту (как BD_wall 3.0×0.16×3.0).
Все детали — в абсолютных координатах.

Запуск: python3 tools/bpy_run.py tools/blender/models_buildpieces.py [--no-render]
"""
import sys, os, math
import bpy
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import subs_common as S
import subs_shapes as K

OUT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..",
      "UnityProject", "Assets", "Subsistence", "Models", "Props"))
PREV = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "docs", "previews"))
R = math.radians
G = 3.0            # размер клетки


def mats():
    wood = S.pbr_material("M_BPWood", (0.45, 0.30, 0.16, 1), 0.0, 0.72, noise_scale=60, bump=0.5)
    wood_dark = S.pbr_material("M_BPWoodDark", (0.28, 0.19, 0.10, 1), 0.0, 0.78)
    steel = S.pbr_material("M_BPSteel", (0.36, 0.37, 0.39, 1), 0.92, 0.34)
    concrete = S.pbr_material("M_BPConcrete", (0.52, 0.51, 0.47, 1), 0.0, 0.85, noise_scale=35, bump=0.55)
    return dict(wood=wood, wood_dark=wood_dark, steel=steel, concrete=concrete)


# ---------- треугольники: половина квадрата 3×3 (гипотенуза по диагонали) ----------

def tri_prism(name, thickness, z0, mat, inset=0.0):
    """Прямоугольный треугольник (катеты 3 м) выдавливается по Z на thickness."""
    a = (0.0 + inset, 0.0 + inset)
    b = (G - inset, 0.0 + inset)
    c = (0.0 + inset, G - inset)
    verts = [(a[0], a[1], z0), (b[0], b[1], z0), (c[0], c[1], z0),
             (a[0], a[1], z0 + thickness), (b[0], b[1], z0 + thickness), (c[0], c[1], z0 + thickness)]
    faces = [(0, 1, 2), (5, 4, 3), (0, 3, 4, 1), (1, 4, 5, 2), (2, 5, 3, 0)]
    return K.mesh_obj(name, verts, faces, mat, smooth=False)


def build_foundation_tri():
    """Треугольный фундамент: плита-треугольник + бортик по катетам."""
    m = mats(); p = [tri_prism("BD_foundation_tri", 0.30, 0.0, m["concrete"])]
    # бортик по двум катетам (как у квадратного фундамента)
    p.append(K.loft_z("BD_foundation_tri_lip_a", [
        (0.30, K.ring_xy(0.10, G, 0.05, G * 0.5)),
        (0.46, K.ring_xy(0.10, G, 0.05, G * 0.5))], mat=m["concrete"], smooth=False))
    p.append(K.loft_z("BD_foundation_tri_lip_b", [
        (0.30, K.ring_xy(G, 0.10, G * 0.5, 0.05)),
        (0.46, K.ring_xy(G, 0.10, G * 0.5, 0.05))], mat=m["concrete"], smooth=False))
    return p


def build_floor_tri():
    """Треугольное перекрытие: тонкая плита + балки под ней."""
    m = mats(); p = [tri_prism("BD_floor_tri", 0.16, 2.74, m["wood"])]
    for i in range(3):
        p.append(K.loft_z(f"BD_floor_tri_beam{i}", [
            (2.60, K.ring_xy(0.12, G - 0.2 - i * 0.55, 0.15 + i * 0.42, (G - 0.2 - i * 0.55) * 0.5)),
            (2.76, K.ring_xy(0.12, G - 0.2 - i * 0.55, 0.15 + i * 0.42, (G - 0.2 - i * 0.55) * 0.5))],
            mat=m["wood_dark"], smooth=False))
    return p


def build_roof():
    """Крыша: скатная плита (уклон), конёк, две кромки, стропила."""
    m = mats(); p = []
    slope = 0.42                                   # подъём на 1 м — ~23°
    for i in range(11):                            # ступенчатая скатная плита
        t = i / 10.0
        z = 2.60 + t * G * slope * 0.5
        p.append(K.loft_z(f"BD_roof_seg{i}", [
            (z, K.ring_xy(G, 0.32, 0.0, -G * 0.5 + 0.30 * i + 0.15)),
            (z + 0.14, K.ring_xy(G, 0.34, 0.0, -G * 0.5 + 0.30 * i + 0.15))],
            mat=m["wood"], smooth=False))
    # конёк
    p.append(K.loft_z("BD_roof_ridge", [
        (2.60 + G * slope * 0.5, K.ring_xy(G, 0.26, 0.0, G * 0.5 - 0.1)),
        (2.60 + G * slope * 0.5 + 0.20, K.ring_xy(G, 0.28, 0.0, G * 0.5 - 0.1))], mat=m["wood_dark"], smooth=False))
    # кромка у стены
    p.append(K.loft_z("BD_roof_edge", [
        (2.46, K.ring_xy(G, 0.16, 0.0, -G * 0.5 + 0.06)),
        (2.62, K.ring_xy(G, 0.16, 0.0, -G * 0.5 + 0.06))], mat=m["wood_dark"], smooth=False))
    # стропила
    for sx in (-1.2, 0.0, 1.2):
        for i in range(6):
            t = i / 5.0
            p.append(K.tube_between(f"BD_roof_rafter{int(sx*10)}_{i}",
                                    (sx, -G * 0.5 + 0.2 + t * 1.2, 2.52 + t * G * slope * 0.18),
                                    (sx, -G * 0.5 + 0.4 + t * 1.2, 2.62 + t * G * slope * 0.18),
                                    0.045, 8, mat=m["wood_dark"]))
    return p


def _ramp_run(prefix, x0, y0, axis, length, rise, width, mat_top, steps=9):
    """Наклонный настил полосами ВДОЛЬ ОСИ ('y' или 'x'). Детали не поворачиваем —
    только абсолютные координаты (урок: поворот крутит деталь вокруг мирового центра)."""
    p = []
    for i in range(steps):
        t = i / (steps - 1.0)
        z = rise * t
        off = -length * 0.5 + length * t
        seg = length / steps * 1.25
        ring = K.ring_xy(width, seg, x0, y0 + off) if axis == 'y' else K.ring_xy(seg, width, x0 + off, y0)
        p.append(K.loft_z(f"{prefix}_step{i}", [(z, ring), (z + 0.10, ring)], mat=mat_top, smooth=False))
    return p


def build_ramp():
    """Пандус: наклонный настил 3 м, подъём 1.5 м, бортики лесенкой, опоры."""
    m = mats()
    p = _ramp_run("BD_ramp", 0.0, 0.0, 'y', G, 1.5, G, m["wood"])
    for side in (-1, 1):
        for i in range(9):
            t = i / 8.0
            z = 1.5 * t
            y = -G * 0.5 + G * t
            p.append(K.loft_z(f"BD_ramp_kerb{side}_{i}", [
                (z + 0.10, K.ring_xy(0.12, 0.45, side * (G * 0.5 - 0.06), y)),
                (z + 0.26, K.ring_xy(0.12, 0.45, side * (G * 0.5 - 0.06), y))], mat=m["wood_dark"], smooth=False))
    for i in range(3):
        x = -0.9 + 0.9 * i
        p.append(K.tube_between(f"BD_ramp_leg{i}", (x, 0.55, 0.0), (x, 0.55, 0.75), 0.07, 8, mat=m["wood_dark"]))
    return p


def build_ramp_corner():
    """Рампа-угол: диагональный подъём в угол клетки + площадка сверху, опоры."""
    m = mats(); p = []
    steps = 9
    for i in range(steps):
        t = i / (steps - 1.0)
        z = 1.5 * t
        cx, cy = 1.5 * t, 1.5 * t
        size = 1.7 - 0.5 * t
        p.append(K.loft_z(f"BD_ramp_corner_step{i}", [
            (z, K.ring_xy(size, size, cx, cy)),
            (z + 0.10, K.ring_xy(size, size, cx, cy))], mat=m["wood"], smooth=False))
    p.append(K.loft_z("BD_ramp_corner_landing", [
        (1.60, K.ring_xy(1.50, 1.50, 1.10, 1.10)),
        (1.70, K.ring_xy(1.46, 1.46, 1.10, 1.10))], mat=m["wood"], smooth=False))
    for i in range(4):
        a = R(45 + i * 90)
        x, y = 1.10 + math.cos(a) * 0.55, 1.10 + math.sin(a) * 0.55
        p.append(K.tube_between(f"BD_ramp_corner_post{i}", (x, y, 0.0), (x, y, 1.58), 0.065, 8, mat=m["wood_dark"]))
    for i in range(4):
        t = 0.25 + i * 0.25
        x, y = 1.5 * t, 1.5 * t
        p.append(K.tube_between(f"BD_ramp_corner_prop{i}", (x, y, 0.0), (x, y, max(0.1, 1.5 * t - 0.1)), 0.05, 8, mat=m["wood_dark"]))
    return p


def build_high_wall():
    """Высокая стена 3×6: две секции по 3 м, два пояса жёсткости, стойки."""
    m = mats(); p = []
    p.append(K.loft_z("BD_high_wall", [
        (0.0, K.ring_xy(G, 0.18, 0.0, 0.0)),
        (2.9, K.ring_xy(G, 0.18, 0.0, 0.0))], mat=m["wood"], smooth=False))
    p.append(K.loft_z("BD_high_wall_up", [
        (2.9, K.ring_xy(G, 0.18, 0.0, 0.0)),
        (5.8, K.ring_xy(G, 0.18, 0.0, 0.0))], mat=m["wood_dark"], smooth=False))
    for z in (0.06, 2.84, 5.80):
        p.append(K.loft_z(f"BD_high_wall_belt{int(z*100)}", [
            (z - 0.07, K.ring_xy(G, 0.24, 0.0, 0.0)),
            (z + 0.07, K.ring_xy(G, 0.24, 0.0, 0.0))], mat=m["wood_dark"], smooth=False))
    for x in (-1.0, 0.0, 1.0):
        p.append(K.loft_z(f"BD_high_wall_post{int(x*10)}", [
            (0.0, K.ring_xy(0.14, 0.22, x, 0.0)),
            (5.86, K.ring_xy(0.14, 0.22, x, 0.0))], mat=m["wood_dark"], smooth=False))
    p.append(K.loft_z("BD_high_wall_cap", [
        (5.86, K.ring_xy(G, 0.26, 0.0, 0.0)),
        (5.98, K.ring_xy(G, 0.28, 0.0, 0.0))], mat=m["wood"], smooth=False))
    return p


def build_pillar():
    """Столб 0.36×3: ствол, база, оголовок, четыре накладки."""
    m = mats(); p = []
    p.append(K.loft_z("BD_pillar_base", [
        (0.0, K.ring_xy(0.56, 0.56)),
        (0.16, K.ring_xy(0.50, 0.50))], mat=m["wood_dark"], smooth=False))
    p.append(K.loft_z("BD_pillar", [
        (0.16, K.ring_xy(0.36, 0.36)),
        (2.84, K.ring_xy(0.34, 0.34))], mat=m["wood"], smooth=False))
    for sx, sy in ((-1, 0), (1, 0), (0, -1), (0, 1)):
        p.append(K.loft_z(f"BD_pillar_rib{sx}{sy}", [
            (0.20, K.ring_xy(0.10 if sx else 0.30, 0.30 if sx else 0.10, sx * 0.16, sy * 0.16)),
            (2.80, K.ring_xy(0.10 if sx else 0.30, 0.30 if sx else 0.10, sx * 0.16, sy * 0.16))],
            mat=m["wood_dark"], smooth=False))
    p.append(K.loft_z("BD_pillar_cap", [
        (2.84, K.ring_xy(0.52, 0.52)),
        (3.00, K.ring_xy(0.46, 0.46))], mat=m["wood_dark"], smooth=False))
    return p


def build_railing():
    """Перила: верхняя и нижняя перекладины, стойки, балясины."""
    m = mats(); p = []
    p.append(K.loft_z("BD_railing_top", [
        (1.02, K.ring_xy(G, 0.12, 0.0, 0.0)),
        (1.14, K.ring_xy(G, 0.14, 0.0, 0.0))], mat=m["wood"], smooth=False))
    p.append(K.loft_z("BD_railing_mid", [
        (0.54, K.ring_xy(G, 0.08, 0.0, 0.0)),
        (0.62, K.ring_xy(G, 0.09, 0.0, 0.0))], mat=m["wood_dark"], smooth=False))
    for i in range(4):
        x = -1.35 + 0.9 * i
        p.append(K.loft_z(f"BD_railing_post{i}", [
            (0.0, K.ring_xy(0.14, 0.16, x, 0.0)),
            (1.10, K.ring_xy(0.12, 0.14, x, 0.0))], mat=m["wood_dark"], smooth=False))
    for i in range(6):
        x = -1.1 + 0.44 * i
        p.append(K.tube_between(f"BD_railing_bal{i}", (x, 0.0, 0.02), (x, 0.0, 1.04), 0.022, 6, mat=m["wood"]))
    return p


def build_shutters():
    """Ставни: рама 3×2.4, ламели, петли, крючок."""
    m = mats(); p = []
    w, h = G, 2.4
    p.append(K.loft_z("BD_shutters_frame_l", [
        (0.0, K.ring_xy(0.14, 0.10, -w * 0.5 + 0.07, 0.0)),
        (h, K.ring_xy(0.14, 0.10, -w * 0.5 + 0.07, 0.0))], mat=m["wood_dark"], smooth=False))
    p.append(K.loft_z("BD_shutters_frame_r", [
        (0.0, K.ring_xy(0.14, 0.10, w * 0.5 - 0.07, 0.0)),
        (h, K.ring_xy(0.14, 0.10, w * 0.5 - 0.07, 0.0))], mat=m["wood_dark"], smooth=False))
    for z in (0.0, h - 0.14):
        p.append(K.loft_z(f"BD_shutters_frame_z{int(z*100)}", [
            (z, K.ring_xy(w, 0.10, 0.0, 0.0)),
            (z + 0.14, K.ring_xy(w, 0.10, 0.0, 0.0))], mat=m["wood_dark"], smooth=False))
    for i in range(9):
        z = 0.18 + i * 0.24
        p.append(K.loft_z(f"BD_shutters_lath{i}", [
            (z, K.ring_xy(w - 0.22, 0.05, 0.0, 0.05)),
            (z + 0.15, K.ring_xy(w - 0.24, 0.05, 0.0, 0.05))], mat=m["wood"], smooth=False))
    for i in range(2):
        z = 0.45 + i * 1.4
        p.append(K.tube_between(f"BD_shutters_hinge{i}", (-w * 0.5 + 0.10, -0.06, z), (-w * 0.5 + 0.10, -0.14, z), 0.030, 8, mat=m["steel"]))
    p.append(K.tube_between("BD_shutters_hook", (w * 0.5 - 0.22, 0.06, 1.20), (w * 0.5 - 0.06, 0.14, 1.20), 0.014, 8, mat=m["steel"]))
    return p


BUILDERS = [
    ("BD_foundation_tri", build_foundation_tri),
    ("BD_floor_tri", build_floor_tri),
    ("BD_roof", build_roof),
    ("BD_ramp", build_ramp),
    ("BD_ramp_corner", build_ramp_corner),
    ("BD_high_wall", build_high_wall),
    ("BD_pillar", build_pillar),
    ("BD_railing", build_railing),
    ("BD_shutters", build_shutters),
]

CAMS = {
    "BD_foundation_tri": (2.2, 1.6), "BD_floor_tri": (2.2, 3.0), "BD_roof": (3.4, 4.0),
    "BD_ramp": (3.0, 2.0), "BD_ramp_corner": (3.0, 2.0), "BD_high_wall": (4.6, 3.4),
    "BD_pillar": (3.4, 2.0), "BD_railing": (3.0, 1.4), "BD_shutters": (3.0, 1.6),
}


def build_all(render=True, only=None):
    for name, fn in BUILDERS:
        if only and name not in only:
            continue
        S.clean_scene()
        parts = fn()
        joined = S.join_objects(parts, name)
        S.smart_uv(joined)
        S.add_bevel(joined, 0.0015, 1)
        S.apply_modifiers(joined)
        S.shade_smooth(joined, 34)
        tris = S.tri_count(joined)
        S.export_fbx([joined], os.path.join(OUT, name + ".fbx"))
        S.export_glb([joined], os.path.join(OUT, name + ".glb"))
        print(f"[buildpiece] {name}: {tris} трис")
        if render:
            S.render_fit(os.path.join(PREV, f"_bp_{name}.png"), [joined],
                         samples=22, res=(820, 560), fov_deg=38, pad=1.34,
                         azimuth=-58.0, elevation=16.0)


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    only = None
    if "--only" in argv:
        i = argv.index("--only")
        only = set(argv[i + 1:]) or None
    build_all(render="--no-render" not in argv, only=only)
