"""
SUBSISTENCE — models_loot_items.py
Модели предметам, у которых визуала не было вообще (раньше — серые кубы): еда, медицина,
патроны, броня/одежда, ресурсы, инструменты. Всё строится из тех же примитивов, что и
предметы уровней (loft_z / tube_between), детали не поворачиваем — только абсолютные координаты.

Группы (OUT = Models/Items, превью _li_<имя>.png):
  ЕДА/ВОДА:    IT_can_beans, IT_can_tuna, IT_water_bottle, IT_apple, IT_chocolate, IT_meat_raw
  МЕДИЦИНА:    IT_medkit_large, IT_bandage, IT_antidote
  ПАТРОНЫ:     IT_ammo_556 (магазин), IT_ammo_shell (патрон 12к), IT_arrow_bundle
  БРОНЯ:       IT_wood_helmet, IT_wood_chestplate, IT_hide_vest, IT_boots_hide
  РЕСУРСЫ:     IT_scrap_pile, IT_wood_pile, IT_stone_pile, IT_sulfur_lump, IT_cloth_roll
  ИНСТРУМЕНТЫ: IT_bucket, IT_flashlight, IT_geiger, IT_torch_lantern

Запуск: python3 tools/bpy_run.py tools/blender/models_loot_items.py [--only IT_x …]
"""
import sys, os, math
import bpy
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import subs_common as S
import subs_shapes as K

OUT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..",
      "UnityProject", "Assets", "Subsistence", "Models", "Items"))
PREV = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "docs", "previews"))
R = math.radians


def mats():
    return dict(
        steel=S.pbr_material("M_LIsteel", (0.44, 0.45, 0.47, 1), 0.92, 0.34),
        steel_dark=S.pbr_material("M_LIsteel_dark", (0.22, 0.23, 0.24, 1), 0.85, 0.48),
        tin=S.pbr_material("M_LItin", (0.70, 0.72, 0.74, 1), 0.80, 0.30),
        brass=S.pbr_material("M_LIbrass", (0.68, 0.52, 0.20, 1), 0.95, 0.26),
        wood=S.pbr_material("M_LIwood", (0.46, 0.31, 0.16, 1), 0.0, 0.74, noise_scale=60, bump=0.5),
        wood_dark=S.pbr_material("M_LIwood_dark", (0.28, 0.18, 0.09, 1), 0.0, 0.80),
        cloth=S.pbr_material("M_LIcloth", (0.42, 0.40, 0.34, 1), 0.0, 0.90),
        cloth_white=S.pbr_material("M_LIcloth_white", (0.86, 0.86, 0.82, 1), 0.0, 0.88),
        leather=S.pbr_material("M_LIleather", (0.42, 0.26, 0.14, 1), 0.0, 0.72),
        glass=S.pbr_material("M_LIglass", (0.55, 0.78, 0.72, 1), 0.0, 0.14),
        water=S.pbr_material("M_LIwater", (0.30, 0.52, 0.55, 1), 0.0, 0.10),
        paper=S.pbr_material("M_LIpaper", (0.80, 0.78, 0.70, 1), 0.0, 0.86),
        red=S.pbr_material("M_LIred", (0.62, 0.16, 0.14, 1), 0.0, 0.55),
        green=S.pbr_material("M_LIgreen", (0.24, 0.42, 0.22, 1), 0.0, 0.60),
        sulfur=S.pbr_material("M_LIsulfur", (0.86, 0.78, 0.30, 1), 0.0, 0.62),
        stone=S.pbr_material("M_LIstone", (0.44, 0.44, 0.42, 1), 0.0, 0.88),
    )


# --------------------------------------------------------------- ЕДА И ВОДА

def build_can_beans():
    m = mats(); p = []
    p.append(K.loft_z("IT_can_beans_body", [
        (0.0, K.ring_disc(0.30, 0.30)),
        (0.05, K.ring_disc(0.32, 0.32)),
        (0.56, K.ring_disc(0.32, 0.32)),
        (0.61, K.ring_disc(0.29, 0.29))], mat=m["tin"], smooth=True))
    p.append(K.loft_z("IT_can_beans_label", [
        (0.14, K.ring_disc(0.325, 0.325)),
        (0.44, K.ring_disc(0.325, 0.325))], mat=m["red"], smooth=True))
    p.append(K.loft_z("IT_can_beans_lid", [
        (0.60, K.ring_disc(0.30, 0.30)),
        (0.64, K.ring_disc(0.28, 0.28))], mat=m["steel"], smooth=True))
    p.append(K.tube_between("IT_can_beans_tab", (-0.10, 0.0, 0.65), (0.10, 0.0, 0.65), 0.020, 8, mat=m["steel"]))
    return p


def build_can_tuna():
    m = mats(); p = []
    p.append(K.loft_z("IT_can_tuna_body", [
        (0.0, K.ring_disc(0.34, 0.26)),
        (0.04, K.ring_disc(0.36, 0.28)),
        (0.26, K.ring_disc(0.36, 0.28)),
        (0.30, K.ring_disc(0.33, 0.25))], mat=m["tin"], smooth=True))
    p.append(K.loft_z("IT_can_tuna_label", [
        (0.08, K.ring_disc(0.365, 0.285)),
        (0.22, K.ring_disc(0.365, 0.285))], mat=m["green"], smooth=True))
    p.append(K.tube_between("IT_can_tuna_key", (-0.20, 0.32, 0.31), (0.16, 0.32, 0.31), 0.018, 8, mat=m["steel"]))
    return p


def build_water_bottle():
    m = mats(); p = []
    # плавная бутылка: без «стопки тарелок» (рёбер всего два, они тонкие)
    p.append(K.loft_z("IT_water_bottle_body", [
        (0.0,  K.ring_disc(0.24, 0.24)),
        (0.10, K.ring_disc(0.30, 0.30)),
        (0.34, K.ring_disc(0.30, 0.30)),
        (0.52, K.ring_disc(0.24, 0.24)),
        (0.60, K.ring_disc(0.13, 0.13))], mat=m["glass"], smooth=True))
    p.append(K.loft_z("IT_water_bottle_water", [
        (0.04, K.ring_disc(0.27, 0.27)),
        (0.31, K.ring_disc(0.27, 0.27))], mat=m["water"], smooth=True))
    p.append(K.loft_z("IT_water_bottle_label", [
        (0.16, K.ring_disc(0.31, 0.31)),
        (0.30, K.ring_disc(0.31, 0.31))], mat=m["paper"], smooth=True))
    for i in range(2):
        z = 0.10 + i * 0.05
        p.append(K.loft_z(f"IT_water_bottle_rib{i}", [
            (z, K.ring_disc(0.305, 0.305)),
            (z + 0.018, K.ring_disc(0.305, 0.305))], mat=m["glass"], smooth=True))
    p.append(K.loft_z("IT_water_bottle_neck", [
        (0.60, K.ring_disc(0.14, 0.14)),
        (0.70, K.ring_disc(0.15, 0.15))], mat=m["glass"], smooth=True))
    p.append(K.loft_z("IT_water_bottle_cap", [
        (0.70, K.ring_disc(0.17, 0.17)),
        (0.78, K.ring_disc(0.175, 0.175))], mat=m["green"], smooth=True))
    return p

def build_apple():
    m = mats(); p = []
    p.append(K.loft_z("IT_apple_body", [
        (0.0, K.ring_disc(0.20, 0.20)),
        (0.12, K.ring_disc(0.32, 0.32)),
        (0.26, K.ring_disc(0.34, 0.34)),
        (0.38, K.ring_disc(0.26, 0.26)),
        (0.44, K.ring_disc(0.12, 0.12))], mat=m["red"], smooth=True))
    p.append(K.tube_between("IT_apple_stem", (0.0, 0.0, 0.44), (0.03, 0.02, 0.60), 0.018, 6, mat=m["wood_dark"]))
    p.append(K.loft_z("IT_apple_leaf", [
        (0.50, K.ring_disc(0.12, 0.06, 0.10, 0.02)),
        (0.53, K.ring_disc(0.10, 0.05, 0.10, 0.02))], mat=m["green"], smooth=False))
    return p


def build_chocolate():
    m = mats(); p = []
    choco = S.pbr_material("M_LIchoco", (0.31, 0.17, 0.09, 1), 0.0, 0.52)
    # толстая плитка 0.20 м с крупными дольками: иначе читалась как «полоска на полу»
    p.append(K.loft_z("IT_chocolate_bar", [
        (0.0,  K.ring_xy(0.64, 0.24)),
        (0.20, K.ring_xy(0.64, 0.24))], mat=choco, smooth=False))
    for i in range(6):
        x = -0.53 + i * 0.212
        p.append(K.loft_z(f"IT_chocolate_piece{i}", [
            (0.20, K.ring_xy(0.17, 0.20, x, 0.0)),
            (0.27, K.ring_xy(0.165, 0.195, x, 0.0))], mat=choco, smooth=False))
    p.append(K.loft_z("IT_chocolate_wrap", [
        (0.0,  K.ring_xy(0.34, 0.265)),
        (0.23, K.ring_xy(0.34, 0.265))], mat=m["paper"], smooth=False))
    p.append(K.loft_z("IT_chocolate_band", [
        (0.07, K.ring_xy(0.35, 0.275)),
        (0.15, K.ring_xy(0.35, 0.275))], mat=m["red"], smooth=False))
    return p
def build_meat_raw():
    m = mats(); p = []
    p.append(K.loft_z("IT_meat_raw_body", [
        (0.0, K.ring_disc(0.34, 0.24)),
        (0.10, K.ring_disc(0.40, 0.30)),
        (0.18, K.ring_disc(0.30, 0.22))], mat=S.pbr_material("M_LImeat", (0.62, 0.26, 0.24, 1), 0.0, 0.66), smooth=True))
    p.append(K.tube_between("IT_meat_raw_bone", (-0.30, 0.0, 0.10), (0.32, 0.0, 0.12), 0.045, 8, mat=m["cloth_white"]))
    p.append(K.loft_z("IT_meat_raw_fat", [
        (0.12, K.ring_disc(0.16, 0.12, 0.10, 0.06)),
        (0.18, K.ring_disc(0.14, 0.10, 0.10, 0.06))], mat=m["paper"], smooth=True))
    return p


# --------------------------------------------------------------- МЕДИЦИНА

def build_medkit_large():
    m = mats(); p = []
    p.append(K.loft_z("IT_medkit_body", [
        (0.0, K.ring_xy(0.48, 0.34)),
        (0.24, K.ring_xy(0.50, 0.36))], mat=m["green"], smooth=False))
    p.append(K.loft_z("IT_medkit_lid", [
        (0.24, K.ring_xy(0.50, 0.36)),
        (0.30, K.ring_xy(0.48, 0.34))], mat=S.pbr_material("M_LIgreen_dark", (0.18, 0.30, 0.17, 1), 0.0, 0.62), smooth=False))
    # крест на крышке: две планки
    p.append(K.loft_z("IT_medkit_cross_a", [
        (0.30, K.ring_xy(0.30, 0.06, 0.0, 0.0)),
        (0.33, K.ring_xy(0.30, 0.06, 0.0, 0.0))], mat=m["cloth_white"], smooth=False))
    p.append(K.loft_z("IT_medkit_cross_b", [
        (0.30, K.ring_xy(0.06, 0.26, 0.0, 0.0)),
        (0.33, K.ring_xy(0.06, 0.26, 0.0, 0.0))], mat=m["cloth_white"], smooth=False))
    p.append(K.tube_between("IT_medkit_handle", (-0.14, 0.0, 0.30), (0.14, 0.0, 0.34), 0.028, 8, mat=m["steel_dark"]))
    for sx in (-1, 1):
        p.append(K.loft_z(f"IT_medkit_latch{sx}", [
            (0.16, K.ring_xy(0.05, 0.05, sx * 0.40, -0.30)),
            (0.24, K.ring_xy(0.05, 0.05, sx * 0.40, -0.30))], mat=m["brass"], smooth=False))
    return p


def build_bandage():
    m = mats(); p = []
    band = S.pbr_material("M_LIbandage2", (0.76, 0.74, 0.68, 1), 0.0, 0.9)
    core = S.pbr_material("M_LIbandcore", (0.34, 0.29, 0.24, 1), 0.0, 0.9)
    # рулон бинта: цилиндр с втулкой, витки в два оттенка, хвост свисает по боку
    p.append(K.loft_z("IT_bandage_roll", [
        (0.0,  K.ring_disc(0.30, 0.30)),
        (0.20, K.ring_disc(0.30, 0.30)),
        (0.26, K.ring_disc(0.27, 0.27))], mat=m["cloth_white"], smooth=True))
    p.append(K.loft_z("IT_bandage_core", [
        (0.235, K.ring_disc(0.11, 0.11)),
        (0.275, K.ring_disc(0.11, 0.11))], mat=core, smooth=True))
    for i in range(4):
        z = 0.03 + i * 0.052
        r = 0.312 if i % 2 == 0 else 0.298
        p.append(K.loft_z(f"IT_bandage_wrap{i}", [
            (z, K.ring_disc(r, r)),
            (z + 0.024, K.ring_disc(r, r))], mat=band if i % 2 == 0 else m["cloth_white"], smooth=True))
    p.append(K.loft_z("IT_bandage_tail", [
        (0.03, K.ring_disc(0.09, 0.24, 0.205, 0.03)),
        (0.13, K.ring_disc(0.09, 0.19, 0.20, 0.02)),
        (0.23, K.ring_disc(0.09, 0.15, 0.195, 0.02))], mat=m["cloth_white"], smooth=False))
    return p

def build_antidote():
    m = mats(); p = []
    plunger = S.pbr_material("M_LIplunger", (0.88, 0.88, 0.86, 1), 0.0, 0.55)
    z = 0.075
    # шприц лежит вдоль X: поршень слева, игла справа (читается как шприц, а не «крест»)
    p.append(K.tube_between("IT_antidote_barrel", (-0.10, 0, z), (0.24, 0, z), 0.075, 14, mat=m["glass"]))
    p.append(K.tube_between("IT_antidote_liquid", (-0.08, 0, z), (0.17, 0, z), 0.066, 14, mat=m["sulfur"]))
    p.append(K.tube_between("IT_antidote_neck", (0.24, 0, z), (0.31, 0, z), 0.030, 10, mat=m["glass"]))
    p.append(K.tube_between("IT_antidote_needle", (0.31, 0, z), (0.50, 0, z), 0.010, 6, mat=m["steel"], r1=0.004))
    p.append(K.tube_between("IT_antidote_plunger", (-0.30, 0, z), (-0.10, 0, z), 0.024, 10, mat=plunger))
    p.append(K.tube_between("IT_antidote_thumb", (-0.335, 0, z), (-0.30, 0, z), 0.075, 12, mat=plunger, r1=0.075))
    for sy in (-1, 1):          # усики у основания цилиндра
        p.append(K.loft_z(f"IT_antidote_flange{'p' if sy > 0 else 'm'}", [
            (z - 0.022, K.ring_xy(0.035, 0.11, -0.095, sy * 0.085)),
            (z + 0.022, K.ring_xy(0.035, 0.11, -0.095, sy * 0.085))], mat=m["steel"], smooth=False))
    p.append(K.tube_between("IT_antidote_tape", (0.13, 0, z), (0.19, 0, z), 0.078, 14, mat=m["red"]))
    return p

def build_ammo_556():
    m = mats(); p = []
    p.append(K.loft_z("IT_ammo_556_box", [
        (0.0, K.ring_disc(0.44, 0.30)),
        (0.20, K.ring_disc(0.46, 0.32))], mat=S.pbr_material("M_LIammo_box", (0.42, 0.38, 0.28, 1), 0.0, 0.72), smooth=False))
    for i in range(4):     # патроны торчат из коробки
        x = -0.30 + i * 0.20
        p.append(K.loft_z(f"IT_ammo_556_round{i}", [
            (0.18, K.ring_disc(0.06, 0.06, x, 0.0)),
            (0.40, K.ring_disc(0.06, 0.06, x, 0.0))], mat=m["brass"], smooth=True))
        p.append(K.loft_z(f"IT_ammo_556_tip{i}", [
            (0.40, K.ring_disc(0.055, 0.055, x, 0.0)),
            (0.50, K.ring_disc(0.02, 0.02, x, 0.0))], mat=m["brass"], smooth=True))
    return p


def build_ammo_shell():
    m = mats(); p = []
    for i in range(3):
        x = -0.26 + i * 0.26
        p.append(K.loft_z(f"IT_ammo_shell_body{i}", [
            (0.0, K.ring_disc(0.085, 0.085, x, 0.0)),
            (0.34, K.ring_disc(0.09, 0.09, x, 0.0))], mat=S.pbr_material("M_LIshell", (0.72, 0.20, 0.16, 1), 0.0, 0.42), smooth=True))
        p.append(K.loft_z(f"IT_ammo_shell_base{i}", [
            (-0.02, K.ring_disc(0.095, 0.095, x, 0.0)),
            (0.06, K.ring_disc(0.095, 0.095, x, 0.0))], mat=m["brass"], smooth=True))
    return p


def build_arrow_bundle():
    m = mats(); p = []
    # 6 стрел, поставленных в пучок с наклоном: оперение внизу, наконечники сходятся вверху
    lean = [(-0.24, -0.12), (0.0, -0.22), (0.24, -0.12), (-0.24, 0.14), (0.0, 0.22), (0.24, 0.14)]
    for i, (bx, by) in enumerate(lean):
        tx, ty = bx * 0.20, by * 0.20
        p.append(K.tube_between(f"IT_arrow_bundle_shaft{i}", (bx, by, 0.03), (tx, ty, 0.60), 0.013, 6, mat=m["wood"]))
        p.append(K.tube_between(f"IT_arrow_bundle_tip{i}", (tx, ty, 0.60), (tx, ty, 0.68), 0.030, 8, mat=m["steel"], r1=0.004))
        for j in (-1, 1):       # два пера у каждой стрелы
            p.append(K.loft_z(f"IT_arrow_bundle_fe{i}{'p' if j > 0 else 'm'}", [
                (0.05, K.ring_xy(0.06, 0.010, bx, by + j * 0.036)),
                (0.17, K.ring_xy(0.035, 0.010, bx + (tx - bx) * 0.2, by + j * 0.030))],
                mat=m["cloth_white"], smooth=False))
    for i, zt in enumerate((0.17, 0.36)):    # два кожаных стяжка поперёк пучка
        p.append(K.tube_between(f"IT_arrow_bundle_tie{i}a", (-0.36, 0.0, zt), (0.36, 0.0, zt), 0.020, 8, mat=m["leather"]))
        p.append(K.tube_between(f"IT_arrow_bundle_tie{i}b", (0.0, -0.30, zt), (0.0, 0.30, zt), 0.020, 8, mat=m["leather"]))
    return p

def build_wood_helmet():
    m = mats(); p = []
    p.append(K.loft_z("IT_wood_helmet_dome", [
        (0.0, K.ring_xy(0.40, 0.36)),
        (0.16, K.ring_xy(0.42, 0.38)),
        (0.30, K.ring_xy(0.30, 0.27))], mat=m["wood"], smooth=True))
    for i in range(5):     # клёпки по ободу
        a = R(i * 72)
        p.append(K.loft_z(f"IT_wood_helmet_rivet{i}", [
            (0.14, K.ring_xy(0.04, 0.04, math.cos(a) * 0.38, math.sin(a) * 0.34)),
            (0.20, K.ring_xy(0.04, 0.04, math.cos(a) * 0.38, math.sin(a) * 0.34))], mat=m["brass"], smooth=False))
    p.append(K.loft_z("IT_wood_helmet_brim", [
        (0.0, K.ring_xy(0.44, 0.40)),
        (0.05, K.ring_xy(0.42, 0.38))], mat=m["wood_dark"], smooth=False))
    return p


def build_wood_chestplate():
    m = mats(); p = []
    p.append(K.loft_z("IT_wood_chest_body", [
        (0.0, K.ring_xy(0.70, 0.26)),
        (0.10, K.ring_xy(0.74, 0.28)),
        (0.50, K.ring_xy(0.72, 0.27))], mat=m["wood"], smooth=False))
    for i in range(4):     # шнуровка по центру
        z = 0.10 + i * 0.12
        p.append(K.loft_z(f"IT_wood_chest_lace{i}", [
            (z, K.ring_xy(0.06, 0.30)),
            (z + 0.03, K.ring_xy(0.06, 0.30))], mat=m["leather"], smooth=False))
    for sx in (-1, 1):
        p.append(K.loft_z(f"IT_wood_chest_strap{sx}", [
            (0.30, K.ring_xy(0.05, 0.05, sx * 0.74, 0.0)),
            (0.50, K.ring_xy(0.05, 0.05, sx * 0.74, 0.0))], mat=m["leather"], smooth=False))
    return p


def build_hide_vest():
    m = mats(); p = []
    p.append(K.loft_z("IT_hide_vest_body", [
        (0.0, K.ring_xy(0.66, 0.24)),
        (0.16, K.ring_xy(0.70, 0.26)),
        (0.54, K.ring_xy(0.62, 0.23))], mat=m["leather"], smooth=True))
    p.append(K.loft_z("IT_hide_vest_collar", [
        (0.54, K.ring_xy(0.40, 0.20)),
        (0.62, K.ring_xy(0.30, 0.16))], mat=m["leather"], smooth=True))
    for i in range(3):     # завязки
        p.append(K.tube_between(f"IT_hide_vest_tie{i}",
                                (-0.30 + i * 0.30, -0.24, 0.24), (-0.30 + i * 0.30, -0.30, 0.16), 0.016, 6, mat=m["cloth"]))
    return p


def build_boots_hide():
    m = mats(); p = []
    for sx, y in ((-1, -0.20), (1, 0.20)):
        p.append(K.loft_z(f"IT_boots_hide_sole{sx}", [
            (0.0, K.ring_xy(0.22, 0.42, sx * 0.20, y - 0.04)),
            (0.05, K.ring_xy(0.23, 0.43, sx * 0.20, y - 0.04))], mat=m["wood_dark"], smooth=False))
        p.append(K.loft_z(f"IT_boots_hide_foot{sx}", [
            (0.04, K.ring_xy(0.21, 0.38, sx * 0.20, y - 0.05)),
            (0.16, K.ring_xy(0.20, 0.26, sx * 0.20, y + 0.02))], mat=m["leather"], smooth=True))
        p.append(K.loft_z(f"IT_boots_hide_shaft{sx}", [
            (0.15, K.ring_xy(0.20, 0.24, sx * 0.20, y + 0.02)),
            (0.46, K.ring_xy(0.21, 0.22, sx * 0.20, y))], mat=m["leather"], smooth=True))
        p.append(K.tube_between(f"IT_boots_hide_lace{sx}", (sx * 0.20 - 0.16, y - 0.10, 0.24),
                                (sx * 0.20 + 0.16, y - 0.10, 0.24), 0.014, 6, mat=m["cloth"]))
    return p


# --------------------------------------------------------------- РЕСУРСЫ

def build_scrap_pile():
    m = mats(); p = []
    for i, (x, y, w, h) in enumerate([(-0.16, -0.10, 0.34, 0.10), (0.16, -0.06, 0.30, 0.08),
                                      (-0.02, 0.14, 0.38, 0.12), (0.10, -0.20, 0.24, 0.06)]):
        p.append(K.loft_z(f"IT_scrap_plate{i}", [
            (0.0, K.ring_xy(w, 0.26, x, y)),
            (h, K.ring_xy(w * 0.94, 0.24, x, y))], mat=m["steel"] if i % 2 else m["steel_dark"], smooth=False))
    for i in range(3):
        a = R(20 + i * 120)
        p.append(K.tube_between(f"IT_scrap_rod{i}", (math.cos(a) * 0.10, math.sin(a) * 0.06, 0.06),
                                (math.cos(a) * 0.36, math.sin(a) * 0.24, 0.20), 0.022, 6, mat=m["steel_dark"]))
    return p


def build_wood_pile():
    m = mats(); p = []
    rows = [(-0.22, 0.0, 0.0), (-0.02, 0.0, 0.0), (0.18, 0.0, 0.0), (-0.12, 0.0, 0.16), (0.08, 0.0, 0.16)]
    for i, (x, y, z) in enumerate(rows):
        p.append(K.loft_z(f"IT_wood_pile_log{i}", [
            (z, K.ring_xy(0.10, 0.42, x, y)),
            (z + 0.14, K.ring_xy(0.10, 0.42, x, y))], mat=m["wood"], smooth=True))
    p.append(K.tube_between("IT_wood_pile_rope", (-0.26, 0.30, 0.08), (-0.26, -0.30, 0.08), 0.018, 6, mat=m["cloth"]))
    return p


def build_stone_pile():
    m = mats(); p = []
    for i, (x, y, r, h) in enumerate([(-0.18, -0.10, 0.20, 0.16), (0.14, -0.14, 0.16, 0.13),
                                      (0.02, 0.12, 0.22, 0.18), (-0.20, 0.16, 0.13, 0.10)]):
        p.append(K.loft_z(f"IT_stone_pile_rock{i}", [
            (0.0, K.ring_xy(r, r * 0.9, x, y)),
            (h * 0.7, K.ring_xy(r * 0.9, r * 0.8, x, y)),
            (h, K.ring_xy(r * 0.5, r * 0.45, x, y))], mat=m["stone"], smooth=False))
    return p


def build_sulfur_lump():
    m = mats(); p = []
    p.append(K.loft_z("IT_sulfur_lump_body", [
        (0.0, K.ring_disc(0.30, 0.26)),
        (0.14, K.ring_disc(0.34, 0.30)),
        (0.26, K.ring_disc(0.22, 0.20)),
        (0.32, K.ring_disc(0.08, 0.07))], mat=m["sulfur"], smooth=False))
    for i in range(4):
        a = R(i * 90 + 20)
        p.append(K.loft_z(f"IT_sulfur_lump_bit{i}", [
            (0.04, K.ring_disc(0.07, 0.07, math.cos(a) * 0.30, math.sin(a) * 0.26)),
            (0.12, K.ring_disc(0.06, 0.06, math.cos(a) * 0.32, math.sin(a) * 0.28))], mat=m["sulfur"], smooth=False))
    return p


def build_cloth_roll():
    m = mats(); p = []
    strap = S.pbr_material("M_LIstrap", (0.33, 0.20, 0.11, 1), 0.0, 0.72)
    # плотный рулон ткани: цилиндр, светлая кромка сверху, широкий язык ткани и ремень
    p.append(K.loft_z("IT_cloth_roll_body", [
        (0.0,  K.ring_disc(0.36, 0.36)),
        (0.26, K.ring_disc(0.36, 0.36)),
        (0.32, K.ring_disc(0.32, 0.32))], mat=m["cloth"], smooth=True))
    p.append(K.loft_z("IT_cloth_roll_edge", [
        (0.30, K.ring_disc(0.33, 0.33)),
        (0.35, K.ring_disc(0.30, 0.30))], mat=m["cloth_white"], smooth=True))
    p.append(K.loft_z("IT_cloth_roll_core", [
        (0.30, K.ring_disc(0.13, 0.13)),
        (0.35, K.ring_disc(0.13, 0.13))], mat=S.pbr_material("M_LIcore2", (0.30, 0.26, 0.21, 1), 0.0, 0.9), smooth=True))
    p.append(K.loft_z("IT_cloth_roll_tail", [     # язык ткани по боку
        (0.04, K.ring_disc(0.10, 0.30, 0.24, 0.03)),
        (0.16, K.ring_disc(0.10, 0.24, 0.235, 0.02)),
        (0.31, K.ring_disc(0.10, 0.18, 0.23, 0.02))], mat=m["cloth_white"], smooth=False))
    for i, z in enumerate((0.09, 0.24)):          # два ремня
        p.append(K.loft_z(f"IT_cloth_roll_strap{i}", [
            (z, K.ring_disc(0.375, 0.375)),
            (z + 0.05, K.ring_disc(0.375, 0.375))], mat=strap, smooth=True))
    return p

def build_bucket():
    m = mats(); p = []
    # ведро: конус вверх, отбортовка, ушки и низкая скоба (высокая дуга читалась как наковальня)
    p.append(K.loft_z("IT_bucket_body", [
        (0.0,  K.ring_xy(0.22, 0.22)),
        (0.05, K.ring_xy(0.26, 0.26)),
        (0.34, K.ring_xy(0.31, 0.31)),
        (0.38, K.ring_xy(0.325, 0.325))], mat=m["tin"], smooth=True))
    p.append(K.loft_z("IT_bucket_rim", [
        (0.38, K.ring_xy(0.34, 0.34)),
        (0.42, K.ring_xy(0.335, 0.335))], mat=m["steel_dark"], smooth=True))
    for sx in (-1, 1):
        p.append(K.loft_z(f"IT_bucket_lug{'p' if sx > 0 else 'm'}", [
            (0.30, K.ring_xy(0.035, 0.05, sx * 0.30, 0.0)),
            (0.46, K.ring_xy(0.035, 0.05, sx * 0.30, 0.0))], mat=m["steel_dark"], smooth=False))
    p.append(K.tube_between("IT_bucket_handle_a", (-0.30, 0.0, 0.45), (0.0, 0.0, 0.47), 0.014, 6, mat=m["steel_dark"]))
    p.append(K.tube_between("IT_bucket_handle_b", (0.30, 0.0, 0.45), (0.0, 0.0, 0.47), 0.014, 6, mat=m["steel_dark"]))
    return p

def build_flashlight_item():
    m = mats(); p = []
    body = S.pbr_material("M_LIflash", (0.29, 0.31, 0.33, 1), 0.6, 0.42)
    lens = S.pbr_material("M_LIlens", (1.0, 0.96, 0.80, 1), 0.0, 0.12,
                          emission=(1.0, 0.94, 0.72, 1), emission_strength=2.0)
    z = 0.085
    # фонарь лежит вдоль X: корпус → раструб конусом → линза, сзади кольцо под темляк
    p.append(K.tube_between("IT_flashlight_body", (-0.28, 0, z), (0.10, 0, z), 0.070, 14, mat=body))
    p.append(K.tube_between("IT_flashlight_head", (0.10, 0, z), (0.26, 0, z), 0.070, 14, mat=m["steel_dark"], r1=0.125))
    p.append(K.tube_between("IT_flashlight_lens", (0.26, 0, z), (0.285, 0, z), 0.113, 14, mat=lens))
    p.append(K.tube_between("IT_flashlight_tail", (-0.34, 0, z), (-0.28, 0, z), 0.075, 14, mat=m["steel_dark"]))
    p.append(K.tube_between("IT_flashlight_ring", (-0.385, 0, z), (-0.34, 0, z), 0.082, 10, mat=m["steel"]))
    for i in range(4):                       # накатка на грипсе
        x = -0.24 + i * 0.05
        p.append(K.tube_between(f"IT_flashlight_grip{i}", (x, 0, z), (x + 0.02, 0, z), 0.076, 14, mat=m["steel_dark"]))
    p.append(K.loft_z("IT_flashlight_switch", [   # кнопка сверху
        (z + 0.055, K.ring_xy(0.05, 0.035, -0.10, 0.0)),
        (z + 0.09, K.ring_xy(0.045, 0.030, -0.10, 0.0))], mat=m["steel_dark"], smooth=False))
    return p

def build_geiger():
    m = mats(); p = []
    p.append(K.loft_z("IT_geiger_body", [
        (0.0, K.ring_xy(0.22, 0.30)),
        (0.30, K.ring_xy(0.24, 0.32)),
        (0.38, K.ring_xy(0.20, 0.26))], mat=S.pbr_material("M_LIgeiger", (0.36, 0.40, 0.38, 1), 0.0, 0.55), smooth=False))
    p.append(K.loft_z("IT_geiger_screen", [
        (0.24, K.ring_xy(0.16, 0.10, 0.0, -0.24)),
        (0.30, K.ring_xy(0.15, 0.09, 0.0, -0.24))], mat=S.pbr_material("M_LIscreen", (0.30, 0.85, 0.45, 1), 0.0, 0.25,
                                                                      emission=(0.30, 0.85, 0.45, 1), emission_strength=1.6), smooth=False))
    p.append(K.tube_between("IT_geiger_antenna", (0.16, 0.10, 0.38), (0.20, 0.16, 0.62), 0.012, 6, mat=m["steel"]))
    p.append(K.tube_between("IT_geiger_strap", (-0.22, 0.0, 0.30), (0.22, 0.0, 0.30), 0.016, 6, mat=m["cloth"]))
    for i in range(3):
        p.append(K.loft_z(f"IT_geiger_button{i}", [
            (0.30, K.ring_xy(0.035, 0.035, -0.08 + i * 0.08, -0.30)),
            (0.34, K.ring_xy(0.035, 0.035, -0.08 + i * 0.08, -0.30))], mat=m["red"] if i == 0 else m["steel_dark"], smooth=True))
    return p


def build_torch_lantern():
    m = mats(); p = []
    p.append(K.loft_z("IT_torch_lantern_base", [
        (0.0, K.ring_xy(0.22, 0.22)),
        (0.06, K.ring_xy(0.24, 0.24))], mat=m["steel_dark"], smooth=True))
    p.append(K.loft_z("IT_torch_lantern_glass", [
        (0.06, K.ring_xy(0.20, 0.20)),
        (0.36, K.ring_xy(0.18, 0.18))], mat=S.pbr_material("M_LIlampglass", (1.0, 0.90, 0.62, 1), 0.0, 0.16,
                                                           emission=(1.0, 0.88, 0.55, 1), emission_strength=2.4), smooth=True))
    p.append(K.loft_z("IT_torch_lantern_flame", [
        (0.14, K.ring_xy(0.09, 0.09)),
        (0.26, K.ring_xy(0.06, 0.06))], mat=S.pbr_material("M_LIflame", (1.0, 0.72, 0.30, 1), 0.0, 0.3,
                                                           emission=(1.0, 0.65, 0.22, 1), emission_strength=3.0), smooth=True))
    p.append(K.loft_z("IT_torch_lantern_top", [
        (0.36, K.ring_xy(0.22, 0.22)),
        (0.44, K.ring_xy(0.10, 0.10))], mat=m["steel_dark"], smooth=True))
    p.append(K.tube_between("IT_torch_lantern_hook", (-0.10, 0.0, 0.44), (0.10, 0.0, 0.52), 0.014, 6, mat=m["steel"]))
    for i in range(4):     # стойки каркаса
        a = R(45 + i * 90)
        p.append(K.tube_between(f"IT_torch_lantern_post{i}",
                                (math.cos(a) * 0.20, math.sin(a) * 0.20, 0.06),
                                (math.cos(a) * 0.20, math.sin(a) * 0.20, 0.38), 0.014, 6, mat=m["steel_dark"]))
    return p


BUILDERS = [
    ("IT_can_beans", build_can_beans), ("IT_can_tuna", build_can_tuna),
    ("IT_water_bottle", build_water_bottle), ("IT_apple", build_apple),
    ("IT_chocolate", build_chocolate), ("IT_meat_raw", build_meat_raw),
    ("IT_medkit_large", build_medkit_large), ("IT_bandage", build_bandage), ("IT_antidote", build_antidote),
    ("IT_ammo_556", build_ammo_556), ("IT_ammo_shell", build_ammo_shell), ("IT_arrow_bundle", build_arrow_bundle),
    ("IT_wood_helmet", build_wood_helmet), ("IT_wood_chestplate", build_wood_chestplate),
    ("IT_hide_vest", build_hide_vest), ("IT_boots_hide", build_boots_hide),
    ("IT_scrap_pile", build_scrap_pile), ("IT_wood_pile", build_wood_pile),
    ("IT_stone_pile", build_stone_pile), ("IT_sulfur_lump", build_sulfur_lump),
    ("IT_cloth_roll", build_cloth_roll),
    ("IT_bucket", build_bucket), ("IT_flashlight", build_flashlight_item),
    ("IT_geiger", build_geiger), ("IT_torch_lantern", build_torch_lantern),
]


def build_all(render=True, only=None):
    for name, fn in BUILDERS:
        if only and name not in only:
            continue
        S.clean_scene()
        parts = fn()
        joined = S.join_objects(parts, name)
        S.smart_uv(joined)
        S.add_bevel(joined, 0.0012, 1)
        S.apply_modifiers(joined)
        S.shade_smooth(joined, 34)
        tris = S.tri_count(joined)
        S.export_fbx([joined], os.path.join(OUT, name + ".fbx"))
        # GLB не пишем: в Unity-проект идут только FBX (иначе дубли и лишний вес)
        print(f"[lootitem] {name}: {tris} трис")
        if render:
            S.render_fit(os.path.join(PREV, f"_li_{name}.png"), [joined],
                         samples=20, res=(760, 520), fov_deg=34, pad=1.30,
                         min_dist=0.5, azimuth=-56.0, elevation=18.0)


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    only = None
    if "--only" in argv:
        i = argv.index("--only")
        only = set(argv[i + 1:]) or None
    build_all(render="--no-render" not in argv, only=only)
