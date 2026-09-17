"""
SUBSISTENCE — models_levelitems.py
Модели «своих» предметов уровней — по 4 на уровень (было по 2).
  L0 (жёлтые коридоры):  IT_glow_mushroom (светогриб), IT_duct_tape (скотч),
                         IT_lamp_portable (лампа-переноска), IT_respirator (респиратор)
  L37 (бассейны):        IT_diving_mask (маска ныряльщика), IT_chlorine (хлорка),
                         IT_flippers (ласты), IT_oxygen_tank (кислородный баллон)
  L3 (электростанция):   IT_rubber_gloves (диэлектрические перчатки), IT_fuse_hi (предохранитель),
                         IT_boots_rubber (резиновые сапоги), IT_wrench_insulated (изолированный ключ)

Все детали — в абсолютных координатах (loft_z / tube_between / cyl_y), без поворотов
и локальных сдвигов.

Запуск: python3 tools/bpy_run.py tools/blender/models_levelitems.py [--no-render] [--only IT_x …]
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
        rubber=S.pbr_material("M_ITRubber", (0.09, 0.09, 0.10, 1), 0.0, 0.92),
        plastic=S.pbr_material("M_ITPlastic", (0.55, 0.57, 0.58, 1), 0.0, 0.55),
        plastic_y=S.pbr_material("M_ITPlasticYellow", (0.78, 0.68, 0.24, 1), 0.0, 0.50),
        steel=S.pbr_material("M_ITSteel", (0.40, 0.41, 0.43, 1), 0.92, 0.32),
        brass=S.pbr_material("M_ITBrass", (0.66, 0.50, 0.20, 1), 0.95, 0.28),
        glass=S.pbr_material("M_ITGlass", (0.72, 0.84, 0.86, 1), 0.0, 0.12),
        cloth=S.pbr_material("M_ITCloth", (0.30, 0.30, 0.28, 1), 0.0, 0.88),
        glow=S.pbr_material("M_ITGlow", (0.62, 0.88, 0.42, 1), 0.0, 0.35,
                            emission=(0.55, 0.95, 0.40, 1), emission_strength=1.4),
        lamp=S.pbr_material("M_ITLamp", (1.0, 0.92, 0.70, 1), 0.0, 0.20,
                            emission=(1.0, 0.90, 0.62, 1), emission_strength=2.6),
        chem=S.pbr_material("M_ITChlorine", (0.72, 0.80, 0.55, 1), 0.0, 0.30,
                            emission=(0.60, 0.78, 0.42, 1), emission_strength=0.5),
    )


# ============================== L0 ==============================

def build_glow_mushroom():
    """Светогриб: колокол-шляпка светится изнутри, пластинки под ней, комок земли."""
    m = mats(); p = []
    p.append(K.loft_z("IT_glow_mushroom_soil", [
        (0.0, K.ring_xy(0.34, 0.32)),
        (0.09, K.ring_xy(0.28, 0.26))], mat=m["cloth"], smooth=False))
    p.append(K.loft_z("IT_glow_mushroom_stem", [
        (0.07, K.ring_xy(0.09, 0.09)),
        (0.30, K.ring_xy(0.065, 0.065))], mat=m["plastic"], smooth=True))
    # шляпка: широкий низ, купол
    p.append(K.loft_z("IT_glow_mushroom_cap", [
        (0.29, K.ring_xy(0.28, 0.26)),
        (0.33, K.ring_xy(0.32, 0.30)),
        (0.40, K.ring_xy(0.29, 0.27)),
        (0.47, K.ring_xy(0.21, 0.19)),
        (0.52, K.ring_xy(0.10, 0.09)),
        (0.55, K.ring_xy(0.03, 0.03))], mat=m["glow"], smooth=True))
    for i in range(8):     # пластинки под шляпкой — внутрь, не торчат
        a = R(i * 45)
        p.append(K.tube_between(f"IT_glow_mushroom_gill{i}",
                                (math.cos(a) * 0.06, math.sin(a) * 0.06, 0.295),
                                (math.cos(a) * 0.17, math.sin(a) * 0.17, 0.31), 0.013, 6, mat=m["glow"]))
    return p


def build_duct_tape():
    """Скотч: рулон на картонной втулке, хвост ленты отклеен."""
    m = mats(); p = []
    p.append(K.loft_z("IT_duct_tape_roll", [
        (0.0, K.ring_xy(0.30, 0.30)),
        (0.16, K.ring_xy(0.30, 0.30))], mat=m["cloth"], smooth=True))
    p.append(K.loft_z("IT_duct_tape_core", [
        (-0.01, K.ring_xy(0.15, 0.15)),
        (0.17, K.ring_xy(0.15, 0.15))], mat=m["plastic_y"], smooth=True))
    # хвост ленты: короткий, отходит от рулона и чуть провисает
    # хвост ленты: три ШИРОКИХ сегмента внахлёст — читается как один отклеенный язык
    for i, (x, y, l, h) in enumerate([(0.31, -0.04, 0.16, 0.045),
                                      (0.40, -0.10, 0.15, 0.038),
                                      (0.48, -0.17, 0.14, 0.030)]):
        p.append(K.loft_z(f"IT_duct_tape_tail{i}", [
            (0.048, K.ring_xy(l, 0.115, x, y)),
            (0.048 + h, K.ring_xy(l, 0.115, x, y))], mat=m["cloth"], smooth=False))
    return p


def build_lamp_portable():
    """Лампа-переноска: корпус, решётка, крюк, кабель, светящееся стекло."""
    m = mats(); p = []
    p.append(K.loft_z("IT_lamp_portable_body", [
        (0.0, K.ring_xy(0.34, 0.30)),
        (0.10, K.ring_xy(0.36, 0.32)),
        (0.34, K.ring_xy(0.30, 0.27))], mat=m["plastic_y"], smooth=False))
    for i in range(4):     # прутья решётки
        x = -0.15 + 0.10 * i
        p.append(K.tube_between(f"IT_lamp_portable_bar{i}", (x, -0.17, 0.06), (x, -0.17, 0.32), 0.012, 6, mat=m["steel"]))
    p.append(K.loft_z("IT_lamp_portable_glass", [
        (0.10, K.ring_xy(0.26, 0.22)),
        (0.30, K.ring_xy(0.24, 0.20))], mat=m["lamp"], smooth=True))
    p.append(K.tube_between("IT_lamp_portable_handle", (-0.22, 0.0, 0.34), (-0.22, 0.0, 0.52), 0.014, 8, mat=m["steel"]))
    p.append(K.tube_between("IT_lamp_portable_handle2", (-0.22, 0.0, 0.52), (0.06, 0.0, 0.54), 0.014, 8, mat=m["steel"]))
    p.append(K.tube_between("IT_lamp_portable_hook", (0.06, 0.0, 0.54), (0.16, 0.0, 0.60), 0.010, 6, mat=m["steel"]))
    p.append(K.tube_between("IT_lamp_portable_cable", (0.18, 0.10, 0.04), (0.52, 0.34, 0.02), 0.016, 8, mat=m["rubber"]))
    p.append(K.tube_between("IT_lamp_portable_tip", (0.00, 0.0, 0.34), (0.00, 0.0, 0.40), 0.020, 8, mat=m["brass"]))
    return p


def build_respirator():
    """Респиратор: маска-купол, два круглых фильтра по бокам, клапан выдоха, ремни."""
    m = mats(); p = []
    p.append(K.loft_z("IT_respirator_mask", [
        (0.00, K.ring_xy(0.30, 0.14, 0.0, -0.06)),
        (0.10, K.ring_xy(0.38, 0.20, 0.0, -0.02)),
        (0.20, K.ring_xy(0.36, 0.19, 0.0, 0.00)),
        (0.26, K.ring_xy(0.24, 0.12, 0.0, 0.00))], mat=m["rubber"], smooth=True))
    for sx in (-1, 1):
        p.append(K.loft_z(f"IT_respirator_filter{sx}", [
            (0.05, K.ring_xy(0.17, 0.17, sx * 0.20, -0.16)),
            (0.17, K.ring_xy(0.19, 0.19, sx * 0.20, -0.16)),
            (0.23, K.ring_xy(0.13, 0.13, sx * 0.20, -0.16))], mat=m["plastic"], smooth=True))
        p.append(K.loft_z(f"IT_respirator_filtercap{sx}", [
            (0.21, K.ring_xy(0.12, 0.12, sx * 0.20, -0.17)),
            (0.26, K.ring_xy(0.10, 0.10, sx * 0.20, -0.17))], mat=m["brass"], smooth=False))
    p.append(K.loft_z("IT_respirator_valve", [
        (0.06, K.ring_xy(0.08, 0.08, 0.0, -0.24)),
        (0.16, K.ring_xy(0.06, 0.06, 0.0, -0.25))], mat=m["plastic_y"], smooth=True))
    for sx in (-1, 1):     # ремни: от оправы назад
        p.append(K.tube_between(f"IT_respirator_strap{sx}",
                                (sx * 0.36, -0.02, 0.16), (sx * 0.40, 0.22, 0.12), 0.020, 8, mat=m["cloth"]))
    p.append(K.tube_between("IT_respirator_strap_top", (-0.12, 0.02, 0.26), (0.12, 0.02, 0.26), 0.018, 8, mat=m["cloth"]))
    return p


# ============================== L37 ==============================

def build_diving_mask():
    """Маска ныряльщика: стекло в резиновой оправе, зажим для носа, ремень по бокам."""
    m = mats(); p = []
    p.append(K.loft_z("IT_diving_mask_frame", [
        (0.00, K.ring_xy(0.46, 0.30, 0.0, 0.02)),
        (0.10, K.ring_xy(0.50, 0.34, 0.0, 0.02)),
        (0.18, K.ring_xy(0.44, 0.28, 0.0, 0.02))], mat=m["rubber"], smooth=True))
    p.append(K.loft_z("IT_diving_mask_glass", [
        (0.06, K.ring_xy(0.42, 0.26, 0.0, -0.02)),
        (0.14, K.ring_xy(0.40, 0.25, 0.0, -0.02))], mat=m["glass"], smooth=True))
    p.append(K.loft_z("IT_diving_mask_nose", [
        (0.04, K.ring_xy(0.14, 0.10, 0.0, -0.20)),
        (0.12, K.ring_xy(0.12, 0.08, 0.0, -0.21))], mat=m["rubber"], smooth=True))
    for sx in (-1, 1):
        p.append(K.tube_between(f"IT_diving_mask_strap{sx}",
                                (sx * 0.46, 0.04, 0.10), (sx * 0.62, 0.16, 0.08), 0.026, 8, mat=m["cloth"]))
        p.append(K.loft_z(f"IT_diving_mask_buckle{sx}", [
            (0.04, K.ring_xy(0.09, 0.06, sx * 0.47, 0.04)),
            (0.14, K.ring_xy(0.09, 0.06, sx * 0.47, 0.04))], mat=m["brass"], smooth=False))
    return p


def build_chlorine():
    """Хлорка: бутылка с ручкой, этикетка, светящаяся жидкость."""
    m = mats(); p = []
    p.append(K.loft_z("IT_chlorine_bottle", [
        (0.0, K.ring_xy(0.26, 0.24)),
        (0.10, K.ring_xy(0.28, 0.26)),
        (0.44, K.ring_xy(0.26, 0.24)),
        (0.54, K.ring_xy(0.14, 0.13))], mat=m["chem"], smooth=True))
    p.append(K.loft_z("IT_chlorine_cap", [
        (0.54, K.ring_xy(0.13, 0.12)),
        (0.62, K.ring_xy(0.14, 0.13))], mat=m["plastic_y"], smooth=False))
    p.append(K.loft_z("IT_chlorine_label", [
        (0.16, K.ring_xy(0.29, 0.05, 0.0, -0.24)),
        (0.34, K.ring_xy(0.29, 0.05, 0.0, -0.24))], mat=m["plastic"], smooth=False))
    p.append(K.tube_between("IT_chlorine_handle", (-0.26, 0.0, 0.50), (-0.40, 0.0, 0.40), 0.020, 8, mat=m["plastic"]))
    p.append(K.tube_between("IT_chlorine_handle2", (-0.40, 0.0, 0.40), (-0.30, 0.0, 0.16), 0.020, 8, mat=m["plastic"]))
    return p


def build_flippers():
    """Ласты: два пера с рёбрами и регулируемые ремешки."""
    m = mats(); p = []
    for sx, y in ((-1, -0.16), (1, 0.16)):
        # калоша
        p.append(K.loft_z(f"IT_flippers_foot{sx}", [
            (0.0, K.ring_xy(0.22, 0.30, sx * 0.20, y)),
            (0.12, K.ring_xy(0.20, 0.28, sx * 0.20, y))], mat=m["rubber"], smooth=True))
        # перо — вытянутое, сужается к концу
        p.append(K.loft_z(f"IT_flippers_blade{sx}", [
            (0.02, K.ring_xy(0.24, 0.20, sx * 0.20, y - 0.26)),
            (0.06, K.ring_xy(0.30, 0.26, sx * 0.20, y - 0.46)),
            (0.08, K.ring_xy(0.26, 0.22, sx * 0.20, y - 0.66)),
            (0.09, K.ring_xy(0.12, 0.10, sx * 0.20, y - 0.80))], mat=m["plastic"], smooth=True))
        for i in range(3):     # рёбра жёсткости
            x = sx * 0.20 + (-0.10 + 0.10 * i)
            p.append(K.tube_between(f"IT_flippers_rib{sx}{i}", (x, y - 0.36, 0.07), (x, y - 0.74, 0.08), 0.010, 6, mat=m["rubber"]))
        p.append(K.tube_between(f"IT_flippers_strap{sx}", (sx * 0.20 - 0.18, y + 0.10, 0.10),
                                (sx * 0.20 + 0.18, y + 0.10, 0.10), 0.016, 8, mat=m["rubber"]))
        p.append(K.loft_z(f"IT_flippers_buckle{sx}", [
            (0.04, K.ring_xy(0.07, 0.05, sx * 0.20 + 0.16, y + 0.10)),
            (0.12, K.ring_xy(0.07, 0.05, sx * 0.20 + 0.16, y + 0.10))], mat=m["brass"], smooth=False))
    return p


def build_oxygen_tank():
    """Кислородный баллон: корпус, вентиль, манометр, ремни, насадка."""
    m = mats(); p = []
    p.append(K.loft_z("IT_oxygen_tank_body", [
        (0.0, K.ring_xy(0.34, 0.34)),
        (0.10, K.ring_xy(0.40, 0.40)),
        (0.90, K.ring_xy(0.40, 0.40)),
        (1.02, K.ring_xy(0.30, 0.30))], mat=m["plastic_y"], smooth=True))
    p.append(K.tube_between("IT_oxygen_tank_valve", (0.0, 0.0, 1.02), (0.0, 0.0, 1.16), 0.055, 10, mat=m["brass"]))
    p.append(K.loft_z("IT_oxygen_tank_wheel", [
        (1.12, K.ring_xy(0.18, 0.18)),
        (1.18, K.ring_xy(0.16, 0.16))], mat=m["steel"], smooth=True))
    p.append(K.loft_z("IT_oxygen_tank_gauge", [
        (0.80, K.ring_xy(0.14, 0.14, 0.30, -0.20)),
        (0.92, K.ring_xy(0.14, 0.14, 0.30, -0.20))], mat=m["glass"], smooth=True))
    for z in (0.30, 0.70):
        p.append(K.tube_between(f"IT_oxygen_tank_strap{int(z*100)}", (-0.42, 0.0, z), (-0.42, 0.0, z + 0.10), 0.030, 8, mat=m["cloth"]))
    p.append(K.tube_between("IT_oxygen_tank_hose", (0.0, 0.0, 1.10), (0.30, -0.34, 1.02), 0.024, 8, mat=m["rubber"]))
    return p


# ============================== L3 ==============================

def build_rubber_gloves():
    """Диэлектрические перчатки: пара, отвороты, рифлёные пальцы."""
    m = mats(); p = []
    for sx, y in ((-1, -0.14), (1, 0.14)):
        p.append(K.loft_z(f"IT_rubber_gloves_cuff{sx}", [
            (0.0, K.ring_xy(0.24, 0.16, sx * 0.18, y)),
            (0.10, K.ring_xy(0.26, 0.18, sx * 0.18, y))], mat=m["rubber"], smooth=True))
        p.append(K.loft_z(f"IT_rubber_gloves_palm{sx}", [
            (0.08, K.ring_xy(0.22, 0.15, sx * 0.18, y)),
            (0.26, K.ring_xy(0.24, 0.16, sx * 0.18, y)),
            (0.34, K.ring_xy(0.20, 0.14, sx * 0.18, y))], mat=m["rubber"], smooth=True))
        for f in range(4):
            fx = sx * 0.18 + (-0.09 + 0.06 * f)
            p.append(K.tube_between(f"IT_rubber_gloves_finger{sx}{f}", (fx, y, 0.34), (fx, y - 0.03, 0.50), 0.022, 8, mat=m["rubber"]))
        p.append(K.tube_between(f"IT_rubber_gloves_thumb{sx}", (sx * 0.18 + 0.14, y - 0.04, 0.20),
                                (sx * 0.18 + 0.22, y - 0.14, 0.30), 0.024, 8, mat=m["rubber"]))
        p.append(K.loft_z(f"IT_rubber_gloves_band{sx}", [
            (0.02, K.ring_xy(0.27, 0.19, sx * 0.18, y)),
            (0.06, K.ring_xy(0.27, 0.19, sx * 0.18, y))], mat=m["plastic_y"], smooth=False))
    return p


def build_fuse_hi():
    """Силовой предохранитель: керамический корпус, латунные колпачки, маркировка."""
    m = mats(); p = []
    p.append(K.loft_z("IT_fuse_hi_body", [
        (0.0, K.ring_xy(0.16, 0.16)),
        (0.30, K.ring_xy(0.16, 0.16))], mat=m["plastic"], smooth=True))
    for z in (0.0, 0.26):
        p.append(K.loft_z(f"IT_fuse_hi_cap{int(z*100)}", [
            (z - 0.02, K.ring_xy(0.13, 0.13)),
            (z + 0.08, K.ring_xy(0.17, 0.17))], mat=m["brass"], smooth=True))
    p.append(K.loft_z("IT_fuse_hi_band", [
        (0.11, K.ring_xy(0.17, 0.17)),
        (0.19, K.ring_xy(0.17, 0.17))], mat=m["plastic_y"], smooth=False))
    p.append(K.tube_between("IT_fuse_hi_pin", (0.0, 0.0, 0.34), (0.0, 0.0, 0.42), 0.022, 8, mat=m["steel"]))
    return p


def build_boots_rubber():
    """Резиновые сапоги: пара, высокие голенища, рифлёная подошва, отвороты."""
    m = mats(); p = []
    for sx, y in ((-1, -0.18), (1, 0.18)):
        # подошва с носком вперёд (-Y) и каблуком
        p.append(K.loft_z(f"IT_boots_rubber_sole{sx}", [
            (0.0, K.ring_xy(0.24, 0.46, sx * 0.20, y - 0.05)),
            (0.05, K.ring_xy(0.25, 0.47, sx * 0.20, y - 0.05))], mat=m["rubber"], smooth=False))
        p.append(K.loft_z(f"IT_boots_rubber_toe{sx}", [
            (0.03, K.ring_xy(0.20, 0.20, sx * 0.20, y - 0.20)),
            (0.10, K.ring_xy(0.15, 0.16, sx * 0.20, y - 0.22))], mat=m["rubber"], smooth=True))
        p.append(K.loft_z(f"IT_boots_rubber_foot{sx}", [
            (0.04, K.ring_xy(0.23, 0.40, sx * 0.20, y - 0.06)),
            (0.15, K.ring_xy(0.21, 0.28, sx * 0.20, y + 0.02))], mat=m["rubber"], smooth=True))
        # голенище сужается вверх, отворот светлый
        p.append(K.loft_z(f"IT_boots_rubber_shaft{sx}", [
            (0.14, K.ring_xy(0.21, 0.26, sx * 0.20, y + 0.02)),
            (0.40, K.ring_xy(0.19, 0.22, sx * 0.20, y + 0.01)),
            (0.60, K.ring_xy(0.20, 0.21, sx * 0.20, y))], mat=m["rubber"], smooth=True))
        p.append(K.loft_z(f"IT_boots_rubber_cuff{sx}", [
            (0.58, K.ring_xy(0.22, 0.23, sx * 0.20, y)),
            (0.66, K.ring_xy(0.23, 0.24, sx * 0.20, y))], mat=m["plastic_y"], smooth=False))
        for i in range(4):     # рифление подошвы
            z = 0.005
            p.append(K.loft_z(f"IT_boots_rubber_tread{sx}{i}", [
                (z, K.ring_xy(0.20, 0.06, sx * 0.20, y - 0.14 + 0.09 * i)),
                (z + 0.02, K.ring_xy(0.21, 0.06, sx * 0.20, y - 0.14 + 0.09 * i))], mat=m["cloth"], smooth=False))
    return p


def build_wrench_insulated():
    """Изолированный ключ ~0.42 м: тело, открытый зев «C», резиновая изоляция рукояти."""
    m = mats(); p = []
    p.append(K.loft_z("IT_wrench_insulated_body", [
        (0.00, K.ring_xy(0.42, 0.09)),
        (0.045, K.ring_xy(0.42, 0.09))], mat=m["steel"], smooth=False))
    # головка (шире тела) и зев из двух губок с прорезью между ними
    p.append(K.loft_z("IT_wrench_insulated_head", [
        (0.00, K.ring_xy(0.13, 0.16, 0.155, 0.0)),
        (0.05, K.ring_xy(0.13, 0.16, 0.155, 0.0))], mat=m["steel"], smooth=False))
    for sy in (-1, 1):
        p.append(K.loft_z(f"IT_wrench_insulated_jaw{sy}", [
            (0.00, K.ring_xy(0.06, 0.045, 0.20, sy * 0.055)),
            (0.05, K.ring_xy(0.06, 0.045, 0.20, sy * 0.055))], mat=m["steel"], smooth=False))
    # изоляция рукояти с мелким рифлением
    p.append(K.loft_z("IT_wrench_insulated_grip", [
        (-0.010, K.ring_xy(0.22, 0.115, -0.085, 0.0)),
        (0.055, K.ring_xy(0.22, 0.115, -0.085, 0.0))], mat=m["rubber"], smooth=False))
    for i in range(6):
        x = -0.170 + 0.032 * i
        p.append(K.loft_z(f"IT_wrench_insulated_rib{i}", [
            (0.055, K.ring_xy(0.010, 0.117, x, 0.0)),
            (0.066, K.ring_xy(0.010, 0.117, x, 0.0))], mat=m["rubber"], smooth=False))
    p.append(K.loft_z("IT_wrench_insulated_hole", [
        (-0.012, K.ring_xy(0.030, 0.030, -0.185, 0.0)),
        (0.057, K.ring_xy(0.030, 0.030, -0.185, 0.0))], mat=m["rubber"], smooth=True))
    return p


BUILDERS = [
    ("IT_glow_mushroom", build_glow_mushroom),
    ("IT_duct_tape", build_duct_tape),
    ("IT_lamp_portable", build_lamp_portable),
    ("IT_respirator", build_respirator),
    ("IT_diving_mask", build_diving_mask),
    ("IT_chlorine", build_chlorine),
    ("IT_flippers", build_flippers),
    ("IT_oxygen_tank", build_oxygen_tank),
    ("IT_rubber_gloves", build_rubber_gloves),
    ("IT_fuse_hi", build_fuse_hi),
    ("IT_boots_rubber", build_boots_rubber),
    ("IT_wrench_insulated", build_wrench_insulated),
]

ZOOM = {
    "IT_glow_mushroom": 0.55, "IT_duct_tape": 0.75, "IT_lamp_portable": 0.75, "IT_respirator": 0.7,
    "IT_diving_mask": 0.75, "IT_chlorine": 0.8, "IT_flippers": 1.0, "IT_oxygen_tank": 1.25,
    "IT_rubber_gloves": 0.75, "IT_fuse_hi": 0.5, "IT_boots_rubber": 0.9, "IT_wrench_insulated": 0.6,
}


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
        S.export_glb([joined], os.path.join(OUT, name + ".glb"))
        print(f"[item] {name}: {tris} трис")
        if render:
            # кадр по габаритам модели: не режет и не «теряет» предмет в кадре
            S.render_fit(os.path.join(PREV, f"_it_{name}.png"), [joined],
                         samples=22, res=(760, 520), fov_deg=34, pad=1.30,
                         min_dist=0.55, azimuth=-56.0, elevation=18.0)


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    only = None
    if "--only" in argv:
        i = argv.index("--only")
        only = set(argv[i + 1:]) or None
    build_all(render="--no-render" not in argv, only=only)
