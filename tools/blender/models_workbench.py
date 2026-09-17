"""
SUBSISTENCE — models_workbench.py
Три модели верстаков (раньше все три уровня ставили одну и ту же DD_workbench):
  DD_workbench1 — деревянный стол, толстая столешница, тиски, пара инструментов на полке.
  DD_workbench2 — стальная рама, металлическая кромка, сверлильный станок, перфопанель с
                  инструментами, два ящика, лампа.
  DD_workbench3 — промышленный: рама с раскосами, ТОКАРНЫЙ узел (станина, шпиндель, патрон,
                  суппорт, рычаги подачи), шкаф с дверцами, кабельный барабан, яркий светильник.

Всё в абсолютных координатах (никаких location/rotation у деталей — урок v3/v4):
вертикальные панели через loft_z, стойки и трубы через tube_between/cyl_y.

Запуск:  python3 tools/bpy_run.py tools/blender/models_workbench.py [--no-render]
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


# Палитры по уровням (ответ 5в): один и тот же верстак в коридорах, бассейнах и на станции
# выглядит по-своему — другое дерево/металл/акценты и свет лампы.
PALETTES = {
    "L0": dict(wood=(0.50, 0.34, 0.15, 1), wood_dark=(0.31, 0.20, 0.10, 1),
               steel=(0.42, 0.40, 0.33, 1), steel_dark=(0.22, 0.21, 0.17, 1),
               brass=(0.70, 0.55, 0.22, 1), lamp=(1.0, 0.92, 0.66, 1), emis=(1.0, 0.90, 0.60, 1), power=8.0),
    "L37": dict(wood=(0.53, 0.50, 0.42, 1), wood_dark=(0.30, 0.30, 0.27, 1),
                steel=(0.47, 0.51, 0.52, 1), steel_dark=(0.24, 0.28, 0.29, 1),
                brass=(0.62, 0.55, 0.36, 1), lamp=(0.88, 0.98, 1.0, 1), emis=(0.80, 0.95, 1.0, 1), power=7.0),
    "L3":  dict(wood=(0.31, 0.24, 0.17, 1), wood_dark=(0.18, 0.15, 0.12, 1),
                steel=(0.26, 0.28, 0.27, 1), steel_dark=(0.13, 0.15, 0.14, 1),
                brass=(0.52, 0.34, 0.16, 1), lamp=(1.0, 0.86, 0.52, 1), emis=(1.0, 0.80, 0.42, 1), power=9.0),
}
THEME = "L0"


def mats():
    pal = PALETTES[THEME]
    sfx = "_" + THEME
    wood = S.pbr_material("M_WBWood" + sfx, pal["wood"], 0.0, 0.72, noise_scale=60, bump=0.5)
    wood_dark = S.pbr_material("M_WBWoodDark" + sfx, pal["wood_dark"], 0.0, 0.78, noise_scale=70, bump=0.55)
    steel = S.pbr_material("M_WBSteel" + sfx, pal["steel"], 0.92, 0.34)
    steel_dark = S.pbr_material("M_WBSteelDark" + sfx, pal["steel_dark"], 0.88, 0.45)
    brass = S.pbr_material("M_WBBrass" + sfx, pal["brass"], 0.95, 0.28)
    rubber = S.pbr_material("M_WBRubber" + sfx, (0.08, 0.08, 0.09, 1), 0.0, 0.92)
    lamp = S.pbr_material("M_WBLamp" + sfx, pal["lamp"], 0.0, 0.20,
                          emission=pal["emis"], emission_strength=pal["power"])
    paper = S.pbr_material("M_WBPaper" + sfx, (0.86, 0.84, 0.72, 1), 0.0, 0.85)
    return dict(wood=wood, wood_dark=wood_dark, steel=steel, steel_dark=steel_dark,
                brass=brass, rubber=rubber, lamp=lamp, paper=paper)


# ============================== DD_workbench1 ==============================

def build_wb1():
    """Простой деревянный верстак: столешница, 4 ноги, полка, тиски, молоток и пила."""
    m = mats(); p = []
    # столешница 1.2 × 0.8, толщина 0.06, верх на 0.98
    p.append(K.loft_z("wb1_top", [
        (0.94, K.ring_xy(1.20, 0.80)),
        (1.00, K.ring_xy(1.20, 0.80))], mat=m["wood"], smooth=False))
    # кромка столешницы (тёмная доска спереди)
    p.append(K.loft_z("wb1_edge", [
        (0.935, K.ring_xy(1.24, 0.06, 0, -0.40)),
        (0.995, K.ring_xy(1.24, 0.06, 0, -0.40))], mat=m["wood_dark"], smooth=False))
    # ноги
    for sx in (-0.54, 0.54):
        for sy in (-0.32, 0.32):
            p.append(K.loft_z(f"wb1_leg{sx}_{sy}", [
                (0.0, K.ring_xy(0.09, 0.09, sx, sy)),
                (0.94, K.ring_xy(0.08, 0.08, sx, sy))], mat=m["wood_dark"], smooth=False))
    # продольные прожилины
    for sy in (-0.32, 0.32):
        p.append(K.loft_z(f"wb1_rail{sy}", [
            (0.26, K.ring_xy(1.10, 0.05, 0, sy)),
            (0.34, K.ring_xy(1.10, 0.05, 0, sy))], mat=m["wood_dark"], smooth=False))
    # полка
    p.append(K.loft_z("wb1_shelf", [
        (0.24, K.ring_xy(1.10, 0.70)),
        (0.28, K.ring_xy(1.10, 0.70))], mat=m["wood"], smooth=False))
    # полка с инструментами: пила (пластина) и молоток
    saw = K.loft_z("wb1_saw", [
        (0.30, K.ring_xy(0.42, 0.03, -0.28, 0.0)),
        (0.36, K.ring_xy(0.44, 0.03, -0.28, 0.0))], mat=m["steel"], smooth=False)
    p.append(saw)
    p.append(K.tube_between("wb1_saw_handle", (-0.48, 0.0, 0.34), (-0.56, 0.02, 0.34), 0.028, 10, mat=m["rubber"]))
    p.append(K.tube_between("wb1_hammer_handle", (0.30, -0.10, 0.30), (0.42, -0.14, 0.30), 0.014, 8, mat=m["wood"]))
    p.append(K.loft_z("wb1_hammer_head", [
        (0.285, K.ring_xy(0.11, 0.05, 0.29, -0.10)),
        (0.325, K.ring_xy(0.11, 0.05, 0.29, -0.10))], mat=m["steel"], smooth=False))
    # ящик для мелочи справа на столешнице
    p.append(K.loft_z("wb1_tray", [
        (1.00, K.ring_xy(0.34, 0.22, 0.34, 0.16)),
        (1.12, K.ring_xy(0.34, 0.22, 0.34, 0.16))], mat=m["wood_dark"], smooth=False))
    for i in range(4):     # гвозди/шурупы в лотке
        x = 0.24 + 0.05 * i
        p.append(K.tube_between(f"wb1_nail{i}", (x, 0.16, 1.12), (x + 0.01, 0.16, 1.19), 0.006, 6, mat=m["brass"]))
    # тиски на левом углу: основание, губки, винт, рукоятка
    p.append(K.loft_z("wb1_vise_base", [
        (1.00, K.ring_xy(0.20, 0.24, -0.40, -0.26)),
        (1.10, K.ring_xy(0.18, 0.22, -0.40, -0.26))], mat=m["steel_dark"], smooth=False))
    p.append(K.loft_z("wb1_vise_jaw", [
        (1.10, K.ring_xy(0.20, 0.06, -0.40, -0.36)),
        (1.24, K.ring_xy(0.20, 0.06, -0.40, -0.36))], mat=m["steel"], smooth=False))
    p.append(K.loft_z("wb1_vise_jaw2", [
        (1.10, K.ring_xy(0.20, 0.06, -0.40, -0.16)),
        (1.24, K.ring_xy(0.20, 0.06, -0.40, -0.16))], mat=m["steel"], smooth=False))
    p.append(K.tube_between("wb1_vise_screw", (-0.40, -0.44, 1.17), (-0.40, -0.58, 1.17), 0.022, 10, mat=m["steel"]))
    p.append(K.tube_between("wb1_vise_bar", (-0.40, -0.62, 1.17 - 0.09), (-0.40, -0.62, 1.17 + 0.09), 0.013, 8, mat=m["steel_dark"]))
    # струбцина: корпус за кромкой + две губки, которые её обнимают (было — висящая планка)
    p.append(K.loft_z("wb1_clamp_body", [
        (0.86, K.ring_xy(0.06, 0.06, 0.16, -0.45)),
        (1.10, K.ring_xy(0.06, 0.06, 0.16, -0.45))], mat=m["steel"], smooth=False))
    p.append(K.loft_z("wb1_clamp_jaw_low", [
        (0.935, K.ring_xy(0.10, 0.10, 0.16, -0.41)),
        (0.985, K.ring_xy(0.10, 0.10, 0.16, -0.41))], mat=m["steel"], smooth=False))
    p.append(K.loft_z("wb1_clamp_jaw_top", [
        (1.00, K.ring_xy(0.10, 0.10, 0.16, -0.41)),
        (1.05, K.ring_xy(0.10, 0.10, 0.16, -0.41))], mat=m["steel"], smooth=False))
    p.append(K.tube_between("wb1_clamp_screw", (0.16, -0.50, 0.98), (0.16, -0.56, 0.98), 0.014, 8, mat=m["brass"]))
    return p


# ============================== DD_workbench2 ==============================

def build_wb2():
    """Верстак 2 уровня: стальная рама, перфопанель, сверлильный станок, два ящика, лампа."""
    m = mats(); p = []
    # столешница потолще + стальная кромка
    p.append(K.loft_z("wb2_top", [
        (0.92, K.ring_xy(1.40, 0.90)),
        (1.00, K.ring_xy(1.40, 0.90))], mat=m["wood"], smooth=False))
    for sy in (-0.45, 0.45):
        p.append(K.loft_z(f"wb2_topedge{sy}", [
            (0.905, K.ring_xy(1.44, 0.05, 0, sy)),
            (1.01, K.ring_xy(1.44, 0.05, 0, sy))], mat=m["steel"], smooth=False))
    # стальные ноги уголком (2 пластины крест-накрест)
    for sx in (-0.64, 0.64):
        for sy in (-0.38, 0.38):
            for ax in (-1, 1):
                p.append(K.loft_z(f"wb2_leg{sx}_{sy}_{ax}", [
                    (0.0, K.ring_xy(0.06, 0.10, sx + ax * 0.03, sy)),
                    (0.92, K.ring_xy(0.06, 0.10, sx + ax * 0.03, sy))], mat=m["steel_dark"], smooth=False))
    # раскосы
    p.append(K.tube_between("wb2_brace_l", (-0.64, -0.38, 0.10), (-0.64, 0.38, 0.80), 0.020, 8, mat=m["steel"]))
    p.append(K.tube_between("wb2_brace_r", (0.64, -0.38, 0.10), (0.64, 0.38, 0.80), 0.020, 8, mat=m["steel"]))
    # два ящика под столешницей
    for i, z in enumerate((0.42, 0.68)):
        p.append(K.loft_z(f"wb2_drawer{i}", [
            (z - 0.11, K.ring_xy(1.20, 0.62, 0, -0.02)),
            (z + 0.11, K.ring_xy(1.20, 0.62, 0, -0.02))], mat=m["steel_dark"], smooth=False))
        p.append(K.tube_between(f"wb2_handle{i}", (-0.16, -0.34, z), (0.16, -0.34, z), 0.014, 8, mat=m["steel"]))
    # перфопанель на задней стенке (за столешницей)
    p.append(K.loft_z("wb2_board", [
        (1.00, K.ring_xy(1.36, 0.05, 0, 0.44)),
        (1.72, K.ring_xy(1.36, 0.05, 0, 0.44))], mat=m["steel"], smooth=False))
    for i in range(11):         # отверстия-наплывы (визуальные «дырки»)
        for j in range(3):
            p.append(K.loft_z(f"wb2_hole{i}_{j}", [
                (1.10 + 0.055 * j, K.ring_xy(0.030, 0.055, -0.60 + 0.12 * i, 0.46)),
                (1.125 + 0.055 * j, K.ring_xy(0.030, 0.055, -0.60 + 0.12 * i, 0.46))], mat=m["rubber"], smooth=False))
    # инструменты на панели: два ключа, отвёртки, рулетка
    for i, x in enumerate((-0.44, -0.20)):
        p.append(K.tube_between(f"wb2_wrench{i}", (x, 0.40, 1.16), (x + 0.16, 0.40, 1.16), 0.016, 8, mat=m["steel"]))
        p.append(K.loft_z(f"wb2_wrenchhead{i}", [
            (1.14, K.ring_xy(0.05, 0.05, x - 0.03, 0.40)),
            (1.18, K.ring_xy(0.05, 0.05, x - 0.03, 0.40))], mat=m["steel"], smooth=False))
    for i, x in enumerate((0.06, 0.18, 0.30)):
        p.append(K.tube_between(f"wb2_driver{i}", (x, 0.40, 1.10), (x, 0.40, 1.34), 0.013, 8, mat=m["brass"]))
        p.append(K.loft_z(f"wb2_driverh{i}", [
            (1.34, K.ring_xy(0.05, 0.05, x, 0.40)),
            (1.44, K.ring_xy(0.045, 0.045, x, 0.40))], mat=m["rubber"], smooth=False))
    # сверлильный станок справа на столешнице: колонна, стол, шпиндель, патрон, рукоятка
    p.append(K.loft_z("wb2_drill_base", [
        (1.00, K.ring_xy(0.34, 0.26, 0.40, 0.06)),
        (1.06, K.ring_xy(0.32, 0.24, 0.40, 0.06))], mat=m["steel_dark"], smooth=False))
    p.append(K.tube_between("wb2_drill_col", (0.46, 0.10, 1.04), (0.46, 0.10, 1.78), 0.030, 12, mat=m["steel"]))
    p.append(K.loft_z("wb2_drill_table", [
        (1.30, K.ring_xy(0.26, 0.22, 0.30, 0.10)),
        (1.34, K.ring_xy(0.26, 0.22, 0.30, 0.10))], mat=m["steel"], smooth=False))
    p.append(K.loft_z("wb2_drill_head", [
        (1.62, K.ring_xy(0.26, 0.30, 0.36, 0.10)),
        (1.80, K.ring_xy(0.24, 0.28, 0.36, 0.10))], mat=m["steel_dark"], smooth=False))
    p.append(K.tube_between("wb2_drill_chuck", (0.36, 0.10, 1.60), (0.36, 0.10, 1.44), 0.032, 12, mat=m["brass"]))
    p.append(K.tube_between("wb2_drill_bit", (0.36, 0.10, 1.44), (0.36, 0.10, 1.30), 0.010, 8, mat=m["steel"]))
    for i in range(3):          # рукоятка подачи (три луча)
        a = R(i * 120)
        p.append(K.tube_between(f"wb2_drill_feed{i}",
                                (0.36 + math.cos(a) * 0.03, 0.10 + math.sin(a) * 0.03, 1.72),
                                (0.36 + math.cos(a) * 0.16, 0.10 + math.sin(a) * 0.16, 1.72), 0.012, 8, mat=m["steel"]))
    # лампа на кронштейне
    p.append(K.tube_between("wb2_lamp_arm", (-0.60, 0.40, 1.72), (-0.30, 0.10, 1.86), 0.016, 8, mat=m["steel_dark"]))
    p.append(K.loft_z("wb2_lamp_head", [
        (1.80, K.ring_xy(0.16, 0.16, -0.26, 0.06)),
        (1.90, K.ring_xy(0.13, 0.13, -0.26, 0.06))], mat=m["steel"], smooth=True))
    p.append(K.loft_z("wb2_lamp_glow", [
        (1.74, K.ring_xy(0.12, 0.12, -0.26, 0.06)),
        (1.80, K.ring_xy(0.14, 0.14, -0.26, 0.06))], mat=m["lamp"], smooth=True))
    return p


# ============================== DD_workbench3 ==============================

def build_wb3():
    """Верстак 3 уровня: промышленный, с токарным узлом, шкафом и кабельным барабаном."""
    m = mats(); p = []
    # усиленная столешница с Т-пазами (полосы)
    p.append(K.loft_z("wb3_top", [
        (0.94, K.ring_xy(1.60, 1.00)),
        (1.02, K.ring_xy(1.60, 1.00))], mat=m["steel_dark"], smooth=False))
    for i in range(5):
        p.append(K.loft_z(f"wb3_slot{i}", [
            (1.015, K.ring_xy(1.50, 0.05, 0, -0.34 + 0.17 * i)),
            (1.03, K.ring_xy(1.50, 0.05, 0, -0.34 + 0.17 * i))], mat=m["rubber"], smooth=False))
    # рама с раскосами (жёстче)
    for sx in (-0.74, 0.74):
        for sy in (-0.44, 0.44):
            for ax in (-1, 1):
                p.append(K.loft_z(f"wb3_leg{sx}_{sy}_{ax}", [
                    (0.0, K.ring_xy(0.08, 0.12, sx + ax * 0.04, sy)),
                    (0.94, K.ring_xy(0.08, 0.12, sx + ax * 0.04, sy))], mat=m["steel"], smooth=False))
            p.append(K.tube_between(f"wb3_br{sx}{sy}", (sx, sy, 0.08), (-sx * 0.55, sy, 0.86), 0.018, 8, mat=m["steel"]))
    # шкаф с дверцами (под столешницей)
    p.append(K.loft_z("wb3_cab", [
        (0.14, K.ring_xy(1.30, 0.80, 0, 0.02)),
        (0.88, K.ring_xy(1.30, 0.80, 0, 0.02))], mat=m["steel_dark"], smooth=False))
    for sx in (-0.32, 0.32):
        p.append(K.loft_z(f"wb3_door{sx}", [
            (0.18, K.ring_xy(0.58, 0.04, sx, -0.40)),
            (0.84, K.ring_xy(0.58, 0.04, sx, -0.40))], mat=m["steel"], smooth=False))
        p.append(K.tube_between(f"wb3_doorh{sx}", (sx * 0.2, -0.43, 0.50), (sx * 0.6, -0.43, 0.50), 0.014, 8, mat=m["brass"]))
    # ТОКАРНЫЙ УЗЕЛ слева: станина, шпиндель, патрон, суппорт, ходовой винт, рычаги
    p.append(K.loft_z("wb3_lathe_bed", [
        (1.00, K.ring_xy(0.86, 0.34, -0.32, -0.02)),
        (1.16, K.ring_xy(0.86, 0.34, -0.32, -0.02))], mat=m["steel_dark"], smooth=False))
    p.append(K.loft_z("wb3_lathe_base", [
        (1.00, K.ring_xy(0.94, 0.40, -0.32, -0.02)),
        (1.02, K.ring_xy(0.94, 0.40, -0.32, -0.02))], mat=m["steel"], smooth=False))
    p.append(K.loft_z("wb3_lathe_head", [
        (1.14, K.ring_xy(0.30, 0.34, -0.66, -0.02)),
        (1.44, K.ring_xy(0.28, 0.32, -0.66, -0.02))], mat=m["steel"], smooth=False))
    p.append(K.tube_between("wb3_lathe_spindle", (-0.52, -0.02, 1.30), (-0.30, -0.02, 1.30), 0.055, 14, mat=m["steel"]))
    p.append(K.loft_z("wb3_lathe_chuck", [
        (1.16, K.ring_xy(0.14, 0.14, -0.26, -0.02)),
        (1.44, K.ring_xy(0.14, 0.14, -0.26, -0.02))], mat=m["brass"], smooth=True))
    for i in range(3):      # кулачки патрона
        a = R(30 + i * 120)
        p.append(K.tube_between(f"wb3_chuckjaw{i}",
                                (-0.26 + math.cos(a) * 0.06, -0.02 + math.sin(a) * 0.06, 1.30),
                                (-0.26 + math.cos(a) * 0.11, -0.02 + math.sin(a) * 0.11, 1.30), 0.018, 8, mat=m["steel"]))
    # суппорт с резцом + маховичок подачи
    p.append(K.loft_z("wb3_saddle", [
        (1.14, K.ring_xy(0.24, 0.26, 0.02, -0.02)),
        (1.26, K.ring_xy(0.22, 0.24, 0.02, -0.02))], mat=m["steel"], smooth=False))
    p.append(K.tube_between("wb3_tool", (0.02, -0.02, 1.26), (-0.14, -0.02, 1.26), 0.014, 8, mat=m["brass"]))
    p.append(K.tube_between("wb3_lead_screw", (-0.60, -0.20, 1.10), (0.30, -0.20, 1.10), 0.014, 8, mat=m["steel"]))
    p.append(K.loft_z("wb3_wheel", [
        (1.30, K.ring_xy(0.20, 0.20, 0.30, -0.02)),
        (1.36, K.ring_xy(0.20, 0.20, 0.30, -0.02))], mat=m["steel"], smooth=True))
    p.append(K.tube_between("wb3_wheel_h", (0.30 - 0.14, -0.02, 1.33), (0.30 + 0.14, -0.02, 1.33), 0.012, 8, mat=m["steel_dark"]))
    for i, z in enumerate((1.52, 1.62)):    # рычаги на бабке
        p.append(K.tube_between(f"wb3_lever{i}", (-0.70, 0.10, z), (-0.52, 0.16, z + 0.08), 0.013, 8, mat=m["brass"]))
    # проходная лампа: узкая, лежит НА стойках (было — «плавающая» плита сверху)
    for sx in (-0.62, 0.62):
        p.append(K.tube_between(f"wb3_lampstand{sx}", (sx, 0.42, 1.02), (sx, 0.42, 1.74), 0.020, 10, mat=m["steel"]))
    p.append(K.loft_z("wb3_lampbar", [
        (1.74, K.ring_xy(1.30, 0.10, 0, 0.42)),
        (1.80, K.ring_xy(1.28, 0.09, 0, 0.42))], mat=m["steel_dark"], smooth=False))
    p.append(K.loft_z("wb3_lampglow", [
        (1.70, K.ring_xy(1.20, 0.07, 0, 0.42)),
        (1.74, K.ring_xy(1.22, 0.08, 0, 0.42))], mat=m["lamp"], smooth=True))
    # кабельный барабан: стоит на столешнице вертикально (был — висел в воздухе справа)
    p.append(K.loft_z("wb3_drum", [
        (1.02, K.ring_xy(0.30, 0.30, 0.56, -0.26)),
        (1.34, K.ring_xy(0.30, 0.30, 0.56, -0.26))], mat=m["wood_dark"], smooth=True))
    for i in range(10):     # витки кабеля по цилиндру
        a = R(i * 36)
        p.append(K.tube_between(f"wb3_cable{i}",
                                (0.56 + math.cos(a) * 0.16, -0.26 + math.sin(a) * 0.16, 1.06),
                                (0.56 + math.cos(a) * 0.16, -0.26 + math.sin(a) * 0.16, 1.30), 0.011, 6, mat=m["rubber"]))
    p.append(K.loft_z("wb3_drum_flange_top", [
        (1.32, K.ring_xy(0.34, 0.34, 0.56, -0.26)),
        (1.36, K.ring_xy(0.32, 0.32, 0.56, -0.26))], mat=m["wood_dark"], smooth=True))
    p.append(K.loft_z("wb3_drum_flange_bot", [
        (1.00, K.ring_xy(0.34, 0.34, 0.56, -0.26)),
        (1.04, K.ring_xy(0.32, 0.32, 0.56, -0.26))], mat=m["wood_dark"], smooth=True))
    # чертежи на столешнице
    p.append(K.loft_z("wb3_paper", [
        (1.02, K.ring_xy(0.34, 0.26, 0.44, -0.30)),
        (1.028, K.ring_xy(0.34, 0.26, 0.44, -0.30))], mat=m["paper"], smooth=False))
    return p


BUILDERS = [
    ("DD_workbench1", build_wb1),
    ("DD_workbench2", build_wb2),
    ("DD_workbench3", build_wb3),
]

# 3 модели × 3 уровня = 9 вариантов (ответ 5в)
VARIANTS = [(base, level) for base, fn in BUILDERS for level in ("L0", "L37", "L3")]


def build_all(render=True, only=None, variants=True):
    global THEME
    jobs = VARIANTS if variants else BUILDERS
    for base, level in jobs:
        THEME = level
        name = (base + "_" + level) if variants else base
        fn = dict(BUILDERS)[base]
        if only and base not in only and name not in only:
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
        print(f"[workbench] {name}: {tris} трис")
        if render:
            # один ракурс на вариант: в листе сравниваем уровни между собой
            S.render_preview(os.path.join(PREV, f"_wb_{name}.png"),
                             target=(0.0, 0.0, 0.95), distance=3.3, height=1.4,
                             samples=22, res=(820, 580), fov_deg=36,
                             key_energy=2400.0, rim_energy=800.0, floor_z=0.0,
                             azimuth=-58.0, elevation=16.0)


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    only = None
    if "--only" in argv:
        i = argv.index("--only")
        only = set(argv[i + 1:]) or None
    build_all(render="--no-render" not in argv, only=only,
              variants=("--base" not in argv))
