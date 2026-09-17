"""
SUBSISTENCE — models_monsters3.py
Три «своих» монстра уровней (по одному хозяину на уровень), на инструменте формы
subs_shapes (силуэты + лофт по кривой) — то есть выше качеством, чем v1-капсулы:

  MN_whisperer — L0 «Жёлтые коридоры». Тощий вытянутый силуэт, непропорционально длинные
                 руки до колен, голова набок, пасть-щель, светящиеся глаза. Идёт на свет.
  MN_drowned   — L37 «Бассейны». Раздутое тело, сгорбленная поза, длинные руки вперёд,
                 широко открытая пасть с водой, водоросли на плечах. Медленный, толстый.
  MN_spark     — L3 «Электростанция». Обгоревшая фигура с проводами вместо мышц, один
                 светящийся глаз, открытая грудная клетка с искрящимися катушками.

Запуск:  python3 tools/bpy_run.py tools/blender/models_monsters3.py [--no-render]
"""
import sys, os, math
import bpy
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import subs_common as S
import subs_shapes as K

OUT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..",
      "UnityProject", "Assets", "Subsistence", "Models", "Characters"))
PREV = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "docs", "previews"))
R = math.radians


# ============================== MN_WHISPERER (L0) ==============================
# Все детали строятся в АБСОЛЮТНЫХ координатах: никаких location/rotation —
# именно из-за них фигуры разъезжались. Наклоны делаются сдвигом центров колец.

def build_whisperer():
    """Шептун: вытянутый, руки до колен, голова набок, пасть-щель, светящиеся глаза."""
    skin = S.pbr_material("M_WhisperSkin", (0.54, 0.52, 0.46, 1), 0.0, 0.70, noise_scale=70, bump=0.55)
    dark = S.pbr_material("M_WhisperDark", (0.045, 0.045, 0.05, 1), 0.0, 0.85)
    teeth = S.pbr_material("M_WhisperTeeth", (0.92, 0.90, 0.82, 1), 0.0, 0.30)
    eye = S.pbr_material("M_WhisperEye", (0.95, 0.95, 0.85, 1), 0.0, 0.15,
                         emission=(1.0, 0.92, 0.62, 1), emission_strength=9.0)
    p = []
    # ТОРС: кольца по высоте, талия узкая, плечи чуть вперёд (сутулость)
    p.append(K.loft_z("wh_torso", [
        (0.86, K.ring_xy(0.24, 0.19, 0, 0.0)),
        (1.02, K.ring_xy(0.28, 0.21, 0, 0.01)),
        (1.20, K.ring_xy(0.26, 0.20, 0, 0.02)),
        (1.40, K.ring_xy(0.34, 0.24, 0, 0.03)),
        (1.54, K.ring_xy(0.36, 0.25, 0, 0.03)),
        (1.62, K.ring_xy(0.26, 0.20, 0, 0.02))], mat=skin, smooth=True))
    # рёбра торчат (тощий)
    for i in range(5):
        z = 1.02 + 0.085 * i
        p.append(K.loft_z(f"wh_rib{i}", [
            (z - 0.011, K.ring_xy(0.335, 0.020)),
            (z + 0.011, K.ring_xy(0.335, 0.020))], mat=skin, smooth=False))
    # ШЕЯ (наклон вперёд) + ГОЛОВА набок
    p.append(K.loft_z("wh_neck", [
        (1.62, K.ring_xy(0.15, 0.14, 0, 0.02)),
        (1.76, K.ring_xy(0.13, 0.13, 0.02, 0.02))], mat=skin, smooth=True))
    p.append(K.loft_z("wh_head", [
        (1.74, K.ring_xy(0.17, 0.20, 0.02, 0.00)),
        (1.86, K.ring_xy(0.21, 0.24, 0.03, -0.01)),
        (1.96, K.ring_xy(0.18, 0.21, 0.04, -0.01))], mat=skin, smooth=True))
    # ПАСТЬ-ЩЕЛЬ: наклонная, узкая, с зубами по всей длине
    p.append(K.loft_z("wh_maw", [
        (1.760, K.ring_xy(0.055, 0.030, 0.02, -0.115)),
        (1.790, K.ring_xy(0.105, 0.032, 0.03, -0.118)),
        (1.820, K.ring_xy(0.095, 0.028, 0.035, -0.115))], mat=dark, smooth=False))
    for i in range(8):
        x = -0.070 + 0.020 * i
        h = 1.782 + 0.004 * i - 0.006 * (i % 2)
        p.append(K.loft_z(f"wh_tooth{i}", [
            (h, K.ring_xy(0.013, 0.020, x * 0.85 + 0.028, -0.126)),
            (h + 0.026, K.ring_xy(0.008, 0.012, x * 0.85 + 0.028, -0.126))], mat=teeth, smooth=False))
    # ГЛАЗА-ЩЁЛКИ (светятся), голова чуть повёрнута — левый выше
    for sx in (-1, 1):
        e = K.loft_z(f"wh_eye_{sx}", [
            (1.858, K.ring_xy(0.052, 0.020, sx * 0.062 + 0.03, -0.112)),
            (1.880, K.ring_xy(0.048, 0.018, sx * 0.062 + 0.03, -0.112))], mat=eye, smooth=False)
        p.append(e)
    # РУКИ до колен: плечо → локоть → запястье (абсолютные точки)
    for sx in (-1, 1):
        p.append(K.tube_between(f"wh_shoulder{sx}", (sx * 0.09, 0.03, 1.54), (sx * 0.20, 0.04, 1.50), 0.058, 12, mat=skin))
        p.append(K.tube_between(f"wh_upper{sx}", (sx * 0.20, 0.04, 1.50), (sx * 0.25, 0.06, 1.08), 0.052, 14, mat=skin))
        p.append(K.tube_between(f"wh_fore{sx}", (sx * 0.25, 0.06, 1.08), (sx * 0.21, 0.16, 0.66), 0.044, 14, mat=skin))
        # кисть с четырьмя пальцами
        p.append(K.loft_z(f"wh_hand{sx}", [
            (0.52, K.ring_xy(0.10, 0.13, sx * 0.21, 0.16)),
            (0.66, K.ring_xy(0.085, 0.11, sx * 0.21, 0.16))], mat=skin, smooth=True))
        for f in range(4):
            fx = -0.035 + 0.023 * f
            p.append(K.tube_between(f"wh_finger{sx}_{f}",
                                    (sx * 0.21 + fx, 0.16, 0.53), (sx * 0.21 + fx * 1.3, 0.20, 0.40), 0.013, 8, mat=skin))
    # НОГИ
    for sx in (-1, 1):
        p.append(K.tube_between(f"wh_thigh{sx}", (sx * 0.10, 0.0, 0.88), (sx * 0.13, 0.03, 0.48), 0.072, 14, mat=skin))
        p.append(K.tube_between(f"wh_shin{sx}", (sx * 0.13, 0.03, 0.48), (sx * 0.12, -0.02, 0.07), 0.055, 14, mat=skin))
        p.append(K.loft_z(f"wh_foot{sx}", [
            (0.02, K.ring_xy(0.11, 0.24, sx * 0.12, -0.05)),
            (0.10, K.ring_xy(0.10, 0.22, sx * 0.12, -0.05))], mat=skin, smooth=False))
    return p


# ============================== MN_DROWNED (L37) ==============================

def build_drowned():
    """Утопленник: раздутое тело, сгорблен, руки вперёд, пасть с водой, водоросли."""
    skin = S.pbr_material("M_DrownedSkin", (0.60, 0.64, 0.62, 1), 0.0, 0.45, noise_scale=50, bump=0.30)
    dark = S.pbr_material("M_DrownedDark", (0.05, 0.08, 0.08, 1), 0.0, 0.70)
    water = S.pbr_material("M_DrownedWater", (0.20, 0.45, 0.48, 1), 0.0, 0.08,
                           emission=(0.15, 0.40, 0.42, 1), emission_strength=0.8)
    weed = S.pbr_material("M_DrownedWeed", (0.16, 0.30, 0.14, 1), 0.0, 0.85, noise_scale=90, bump=0.60)
    p = []
    # ТЕЛО: бочка кверху, сгорбленное (центры колец уходят вперёд)
    p.append(K.loft_z("dr_body", [
        (0.42, K.ring_xy(0.42, 0.36, 0, 0.02)),
        (0.62, K.ring_xy(0.60, 0.52, 0, 0.02)),
        (0.86, K.ring_xy(0.68, 0.60, 0, 0.00)),
        (1.06, K.ring_xy(0.62, 0.54, 0, -0.03)),
        (1.24, K.ring_xy(0.52, 0.46, 0, -0.07)),
        (1.38, K.ring_xy(0.42, 0.38, 0, -0.10))], mat=skin, smooth=True))
    # ШЕЯ короткая, ГОЛОВА опущена вперёд и вниз
    p.append(K.loft_z("dr_neck", [
        (1.36, K.ring_xy(0.26, 0.24, 0, -0.12)),
        (1.48, K.ring_xy(0.24, 0.22, 0, -0.18))], mat=skin, smooth=True))
    p.append(K.loft_z("dr_head", [
        (1.34, K.ring_xy(0.30, 0.34, 0, -0.22)),
        (1.48, K.ring_xy(0.34, 0.40, 0, -0.30)),
        (1.60, K.ring_xy(0.32, 0.38, 0, -0.38)),
        (1.68, K.ring_xy(0.24, 0.28, 0, -0.42))], mat=skin, smooth=True))
    # ГОЛОВА ОПУЩЕНА: затылок-складка поверх глаз (смотрит вниз, в воду)
    p.append(K.loft_z("dr_brow", [
        (1.56, K.ring_xy(0.30, 0.16, 0, -0.40)),
        (1.66, K.ring_xy(0.26, 0.14, 0, -0.46))], mat=skin, smooth=True))
    # ПАСТЬ СВИСАЕТ ОТКРЫТОЙ: челюсть-карман + тёмная полость + зубы
    p.append(K.loft_z("dr_jaw", [
        (1.10, K.ring_xy(0.24, 0.22, 0, -0.44)),
        (1.24, K.ring_xy(0.30, 0.30, 0, -0.46)),
        (1.36, K.ring_xy(0.32, 0.34, 0, -0.44))], mat=skin, smooth=True))
    p.append(K.loft_z("dr_maw", [
        (1.28, K.ring_xy(0.22, 0.24, 0, -0.52)),
        (1.40, K.ring_xy(0.26, 0.28, 0, -0.54))], mat=dark, smooth=False))
    for i in range(6):
        x = -0.10 + 0.04 * i
        p.append(K.loft_z(f"dr_tooth{i}", [
            (1.26, K.ring_xy(0.022, 0.030, x, -0.60)),
            (1.36, K.ring_xy(0.016, 0.022, x, -0.60))], mat=skin, smooth=False))
    # вода стекает из пасти
    p.append(K.loft_z("dr_stream", [
        (0.86, K.ring_xy(0.032, 0.032, 0, -0.62)),
        (1.26, K.ring_xy(0.048, 0.048, 0, -0.60))], mat=water, smooth=True))
    # мокрые волосы-пряди по голове
    for i in range(7):
        a = math.radians(28 + i * 34)
        hx, hy = math.cos(a) * 0.24, math.sin(a) * 0.20 - 0.30
        p.append(K.tube_between(f"dr_hair{i}", (hx, hy, 1.66), (hx * 1.1, hy * 1.15 + 0.06, 1.28), 0.013, 6, mat=weed))
    # РУКИ ВПЕРЁД (хватающая поза) — прямые, абсолютные точки
    for sx in (-1, 1):
        p.append(K.tube_between(f"dr_upper{sx}", (sx * 0.26, -0.06, 1.26), (sx * 0.38, -0.34, 1.12), 0.088, 14, mat=skin))
        p.append(K.tube_between(f"dr_fore{sx}", (sx * 0.38, -0.34, 1.12), (sx * 0.34, -0.74, 1.10), 0.072, 14, mat=skin))
        p.append(K.loft_z(f"dr_hand{sx}", [
            (1.00, K.ring_xy(0.15, 0.17, sx * 0.34, -0.80)),
            (1.14, K.ring_xy(0.13, 0.15, sx * 0.34, -0.84))], mat=skin, smooth=True))
        for f in range(4):
            fx = -0.05 + 0.033 * f
            p.append(K.tube_between(f"dr_finger{sx}_{f}",
                                    (sx * 0.34 + fx, -0.86, 1.06), (sx * 0.34 + fx, -1.00, 1.02), 0.017, 8, mat=skin))
    # НОГИ (короткие, вразвалку)
    for sx in (-1, 1):
        p.append(K.tube_between(f"dr_thigh{sx}", (sx * 0.17, 0.02, 0.46), (sx * 0.23, 0.0, 0.26), 0.10, 14, mat=skin))
        p.append(K.tube_between(f"dr_shin{sx}", (sx * 0.23, 0.0, 0.26), (sx * 0.21, -0.05, 0.06), 0.08, 14, mat=skin))
        p.append(K.loft_z(f"dr_foot{sx}", [
            (0.02, K.ring_xy(0.13, 0.26, sx * 0.21, -0.07)),
            (0.11, K.ring_xy(0.12, 0.24, sx * 0.21, -0.07))], mat=skin, smooth=False))
    # ВОДОРОСЛИ: свисают с плеч и живота
    for i in range(8):
        a = math.radians(20 + i * 42)
        bx, by = math.cos(a) * 0.34, math.sin(a) * 0.26 - 0.02
        p.append(K.tube_between(f"dr_weed{i}", (bx, by, 1.30), (bx * 1.15, by * 1.15, 0.86), 0.015, 6, mat=weed))
    return p


# ============================== MN_SPARK (L3) ==============================

def build_spark():
    """Искровик: обгоревшая фигура, провода вместо мышц, один глаз, искрящая грудь."""
    char = S.pbr_material("M_SparkChar", (0.05, 0.048, 0.046, 1), 0.0, 0.80, noise_scale=110, bump=0.70)
    copper = S.pbr_material("M_SparkCopper", (0.62, 0.33, 0.15, 1), 0.95, 0.35)
    steel = S.pbr_material("M_SparkSteel", (0.35, 0.36, 0.38, 1), 0.90, 0.35)
    eye = S.pbr_material("M_SparkEye", (1.0, 0.55, 0.12, 1), 0.0, 0.20,
                         emission=(1.0, 0.55, 0.10, 1), emission_strength=14.0)
    arc = S.pbr_material("M_SparkArc", (0.80, 0.92, 1.0, 1), 0.0, 0.10,
                         emission=(0.75, 0.90, 1.0, 1), emission_strength=22.0)
    p = []
    # ТОРС: обгоревший, с «вдавленной» грудиной
    p.append(K.loft_z("sp_torso", [
        (0.96, K.ring_xy(0.30, 0.24, 0, 0.0)),
        (1.14, K.ring_xy(0.34, 0.26, 0, 0.01)),
        (1.34, K.ring_xy(0.40, 0.30, 0, 0.02)),
        (1.52, K.ring_xy(0.44, 0.32, 0, 0.02)),
        (1.62, K.ring_xy(0.34, 0.26, 0, 0.01))], mat=char, smooth=True))
    # МЕТАЛЛИЧЕСКИЕ РЁБРА НАРУЖУ (каркас)
    for i in range(5):
        z = 1.06 + 0.105 * i
        w = 0.34 + 0.026 * i
        p.append(K.loft_z(f"sp_rib{i}", [
            (z - 0.015, K.ring_xy(w, 0.022, 0, 0.01)),
            (z + 0.015, K.ring_xy(w, 0.022, 0, 0.01))], mat=steel, smooth=False))
    # ОТКРЫТАЯ ГРУДЬ: катушки и искры между ними
    for i in range(3):
        p.append(K.loft_z(f"sp_coil{i}", [
            (1.26, K.ring_xy(0.085, 0.085, -0.10 + 0.10 * i, -0.10)),
            (1.40, K.ring_xy(0.085, 0.085, -0.10 + 0.10 * i, -0.10))], mat=copper, smooth=True))
        p.append(K.loft_z(f"sp_arc{i}", [
            (1.32, K.ring_xy(0.030, 0.030, -0.10 + 0.10 * i, -0.16)),
            (1.38, K.ring_xy(0.026, 0.026, -0.10 + 0.10 * i, -0.16))], mat=arc, smooth=True))
    # провода вдоль тела (заменяют мышцы)
    for i in range(9):
        x = -0.16 + 0.04 * i
        p.append(K.tube_between(f"sp_wire{i}", (x, -0.10, 1.00), (x * 0.8, -0.14, 1.52), 0.011, 8, mat=copper))
    # ШЕЯ + ГОЛОВА (череп в маске), один глаз
    p.append(K.loft_z("sp_neck", [
        (1.60, K.ring_xy(0.16, 0.15, 0, 0.0)),
        (1.72, K.ring_xy(0.15, 0.14, 0, -0.01))], mat=char, smooth=True))
    p.append(K.loft_z("sp_head", [
        (1.70, K.ring_xy(0.20, 0.24, 0, -0.02)),
        (1.84, K.ring_xy(0.24, 0.28, 0, -0.03)),
        (1.96, K.ring_xy(0.20, 0.24, 0, -0.04))], mat=char, smooth=True))
    p.append(K.loft_z("sp_mask", [
        (1.76, K.ring_xy(0.21, 0.06, 0, -0.15)),
        (1.90, K.ring_xy(0.23, 0.065, 0, -0.16))], mat=steel, smooth=False))
    p.append(K.loft_z("sp_eye", [
        (1.80, K.ring_xy(0.055, 0.045, 0, -0.17)),
        (1.88, K.ring_xy(0.050, 0.040, 0, -0.17))], mat=eye, smooth=True))
    # антенна-провод из макушки
    p.append(K.tube_between("sp_ant", (0.05, 0.02, 1.92), (0.17, -0.06, 2.24), 0.008, 6, mat=steel))
    p.append(K.loft_z("sp_ant_tip", [
        (2.22, K.ring_xy(0.02, 0.02, 0.17, -0.06)),
        (2.26, K.ring_xy(0.016, 0.016, 0.17, -0.06))], mat=arc, smooth=True))
    # РУКИ-КАРКАС с клешнями
    for sx in (-1, 1):
        p.append(K.tube_between(f"sp_upper{sx}", (sx * 0.22, 0.02, 1.50), (sx * 0.32, -0.14, 1.08), 0.060, 14, mat=char))
        p.append(K.tube_between(f"sp_fore{sx}", (sx * 0.32, -0.14, 1.08), (sx * 0.30, -0.40, 0.70), 0.050, 14, mat=char))
        for i in range(3):
            p.append(K.tube_between(f"sp_awire{sx}_{i}", (sx * (0.20 + 0.03 * i), 0.02, 1.46),
                                    (sx * (0.34 + 0.03 * i), -0.20, 1.02), 0.009, 6, mat=copper))
        p.append(K.loft_z(f"sp_claw{sx}", [
            (0.60, K.ring_xy(0.13, 0.16, sx * 0.30, -0.44)),
            (0.74, K.ring_xy(0.11, 0.14, sx * 0.30, -0.44))], mat=steel, smooth=True))
        for f in range(3):
            fx = -0.05 + 0.05 * f
            p.append(K.tube_between(f"sp_clawpin{sx}_{f}",
                                    (sx * 0.30 + fx, -0.50, 0.64), (sx * 0.30 + fx, -0.64, 0.58), 0.013, 6, mat=steel))
    # НОГИ-ПРОТЕЗЫ
    for sx in (-1, 1):
        p.append(K.tube_between(f"sp_thigh{sx}", (sx * 0.14, 0.0, 0.94), (sx * 0.17, -0.03, 0.52), 0.075, 14, mat=char))
        p.append(K.tube_between(f"sp_shin{sx}", (sx * 0.17, -0.03, 0.52), (sx * 0.16, -0.05, 0.12), 0.058, 12, mat=steel))
        p.append(K.tube_between(f"sp_legcable{sx}", (sx * 0.11, 0.06, 0.90), (sx * 0.21, 0.0, 0.18), 0.011, 6, mat=copper))
        p.append(K.loft_z(f"sp_foot{sx}", [
            (0.02, K.ring_xy(0.13, 0.28, sx * 0.16, -0.07)),
            (0.12, K.ring_xy(0.12, 0.26, sx * 0.16, -0.07))], mat=steel, smooth=False))
    return p


# ============================== СБОРКА ==============================

BUILDERS = [
    ("MN_whisperer", build_whisperer),
    ("MN_drowned", build_drowned),
    ("MN_spark", build_spark),
]


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
        print(f"[monsters3] {name}: {tris} трис")
        if render:
            S.render_fit(os.path.join(PREV, "_m4_" + name + ".png"), [joined], samples=26,
                         res=(620, 880), fov_deg=30, azimuth=-52.0, elevation=10.0, pad=1.42)


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    only = None
    if "--only" in argv:
        i = argv.index("--only")
        only = set(argv[i + 1:]) or None
    build_all(render="--no-render" not in argv, only=only)
