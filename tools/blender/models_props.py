"""
SUBSISTENCE — models_props.py
Пропсы трёх уровней + модульный строительный кит (что игрок ставит сам):
  Level 0: панель обоев со стыком, потолочная плитка + лампа-решётка, ковровая плитка,
           картотека, деревянный ящик, ведро, стул;
  Level 37: плиточный блок, бортик бассейна, шкафчик, труба-кит, лестница в воду, знак;
  Level 3: турбина, реактор (с ТВЭЛами), электрощит, трансформатор, труба, перила, паллета, бочка;
  Стройка: фундамент/стена/дверной проём/окно/перекрытие/лестница (базовые формы, тиры — материалом).
Все модели: UV, PBR-материалы, авто-кадрированные превью, FBX (+GLB) для Unity 2022.3.
"""
import sys, os, math
import bpy
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import subs_common as S

OUT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..",
      "UnityProject", "Assets", "Subsistence", "Models", "Props"))


# ------------------------------------------------------------------ Level 0
def wall_panel(wall, trim):
    """Панель жёлтых обоев (2×2 м) с плинтусом и стыком — заменяет серую стену коридора."""
    p = [S.box("L0_panel", (2.0, 0.06, 2.4), loc=(0, 0, 1.2), mat=wall),
         S.box("L0_skirting", (2.0, 0.09, 0.16), loc=(0, -0.02, 0.08), mat=trim),
         S.box("L0_chair_rail", (2.0, 0.075, 0.06), loc=(0, -0.012, 0.92), mat=trim),
         S.box("L0_cornice", (2.0, 0.08, 0.10), loc=(0, -0.01, 2.34), mat=trim)]
    for i in range(3):  # вертикальные швы обоев
        p.append(S.box(f"L0_seam{i}", (0.012, 0.066, 2.2), loc=(-0.66 + i * 0.66, -0.001, 1.2), mat=trim))
    return p


def ceiling_lamp(metal, lampglass, panel):
    """Потолочная плитка с решётчатой лампой — главный визуальный якорь Level 0."""
    p = [S.box("L0_ceilpanel", (1.2, 1.2, 0.05), loc=(0, 0, 0.0), mat=panel),
         S.box("L0_lampframe", (1.22, 0.34, 0.09), loc=(0, 0, -0.06), mat=metal),
         S.box("L0_lampglass", (1.16, 0.28, 0.06), loc=(0, 0, -0.10), mat=lampglass)]
    for i in range(7):
        p.append(S.box(f"L0_baffle{i}", (0.03, 0.30, 0.05), loc=(-0.5 + i * 0.1667, 0, -0.12), mat=metal))
    return p


def carpet_tile(carpet):
    """Плитка ковролина 1×1 м (кладётся тайлами — даёт «пятнистость» пола)."""
    p = [S.box("L0_carpet", (1.0, 1.0, 0.02), loc=(0, 0, 0.01), mat=carpet)]
    for i in range(4):
        p.append(S.box(f"L0_carpet_seam{i}", (0.008, 1.0, 0.021),
                       loc=(-0.5 + i * 0.333, 0, 0.012), mat=carpet))
    return p


def filing_cabinet(metal, handle):
    p = [S.box("L0_cabinet", (0.55, 0.62, 1.35), loc=(0, 0, 0.675), bevel=0.012, mat=metal)]
    for i in range(4):
        z = 0.18 + i * 0.31
        p.append(S.box(f"L0_drawer{i}", (0.51, 0.03, 0.27), loc=(0, -0.315, z), mat=metal))
        p.append(S.cylinder(f"L0_handle{i}", 0.012, 0.10, 14, loc=(0, -0.35, z),
                            rot=(0, math.radians(90), 0), mat=handle))
        p.append(S.box(f"L0_label{i}", (0.16, 0.01, 0.06), loc=(0, -0.335, z - 0.07), mat=handle))
    return p


def wood_crate(wood, metal):
    p = [S.box("CR_crate", (0.72, 0.72, 0.62), loc=(0, 0, 0.31), bevel=0.01, mat=wood)]
    for i in range(2):
        p.append(S.box(f"CR_band_h{i}", (0.74, 0.74, 0.035), loc=(0, 0, 0.12 + i * 0.36), mat=metal))
    for sx in (-1, 1):
        p.append(S.box(f"CR_band_v{sx}", (0.035, 0.74, 0.64), loc=(sx * 0.35, 0, 0.31), mat=metal))
        p.append(S.box(f"CR_plank{sx}", (0.02, 0.70, 0.10), loc=(sx * 0.365, 0, 0.45), mat=wood))
    p.append(S.box("CR_lid", (0.76, 0.76, 0.03), loc=(0, 0, 0.63), mat=wood))
    return p


# ------------------------------------------------------------------ Level 37
def pool_tile_block(tile, tile_dark):
    """Блок плитки 1.5×1.5 м + бортик бассейна со сливом."""
    p = [S.box("PO_tile", (1.5, 1.5, 1.5), loc=(0, 0, 0.75), mat=tile)]
    for i in range(5):  # швы плитки
        p.append(S.box(f"PO_seam_x{i}", (0.012, 1.5, 1.5), loc=(-0.6 + i * 0.3, 0, 0.75), mat=tile_dark))
        p.append(S.box(f"PO_seam_y{i}", (1.5, 0.012, 1.5), loc=(0, -0.6 + i * 0.3, 0.75), mat=tile_dark))
    p.append(S.box("PO_coping", (1.5, 0.3, 0.14), loc=(0, -0.75, 1.5), mat=tile))
    p.append(S.cylinder("PO_drain", 0.09, 0.03, 20, loc=(0, -0.72, 1.55), mat=tile_dark))
    for i in range(4):
        a = i * math.pi / 2
        p.append(S.box(f"PO_drain_slot{i}", (0.10, 0.016, 0.02),
                       loc=(math.cos(a) * 0.0, -0.72 + math.sin(a) * 0.0, 1.555),
                       rot=(0, 0, a), mat=tile_dark))
    return p


def locker(metal, paint):
    p = [S.box("PO_locker", (0.46, 0.45, 1.85), loc=(0, 0, 0.925), bevel=0.01, mat=paint)]
    p.append(S.box("PO_locker_top", (0.48, 0.47, 0.04), loc=(0, 0, 1.86), mat=metal))
    p.append(S.box("PO_locker_base", (0.48, 0.47, 0.06), loc=(0, 0, 0.03), mat=metal))
    for i in range(2):
        x = -0.115 + i * 0.23
        p.append(S.box(f"PO_door{i}", (0.215, 0.02, 1.72), loc=(x, -0.235, 0.94), mat=paint))
        p.append(S.cylinder(f"PO_vent{i}", 0.025, 0.008, 14, loc=(x, -0.25, 1.62),
                            rot=(math.radians(90), 0, 0), mat=metal))
        p.append(S.box(f"PO_handle{i}", (0.03, 0.03, 0.10), loc=(x + (0.07 if i == 0 else -0.07), -0.255, 1.0), mat=metal))
        p.append(S.box(f"PO_label{i}", (0.10, 0.006, 0.05), loc=(x, -0.25, 1.72), mat=metal))
    return p


def pipe_kit(pipe_mat, rust):
    """Кит труб: прямой сегмент, отвод, фланец, вентиль (собираются модульно)."""
    p = [S.cylinder("PO_pipe_seg", 0.14, 2.0, 24, loc=(0, 0, 0), rot=(math.radians(90), 0, 0), mat=pipe_mat),
         S.cylinder("PO_pipe_flange1", 0.19, 0.06, 26, loc=(0, -1.0, 0), rot=(math.radians(90), 0, 0), mat=rust),
         S.cylinder("PO_pipe_flange2", 0.19, 0.06, 26, loc=(0, 1.0, 0), rot=(math.radians(90), 0, 0), mat=rust),
         S.cylinder("PO_pipe_collar", 0.165, 0.09, 26, loc=(0, 0, 0), rot=(math.radians(90), 0, 0), mat=rust)]
    p.append(S.cylinder("PO_valve_body", 0.11, 0.16, 20, loc=(0.19, 0, 0), rot=(0, math.radians(90), 0), mat=rust))
    p.append(S.cylinder("PO_valve_stem", 0.03, 0.22, 16, loc=(0.30, 0, 0), rot=(0, math.radians(90), 0), mat=rust))
    p.append(S.cylinder("PO_valve_wheel", 0.13, 0.02, 22, loc=(0.41, 0, 0), rot=(math.radians(90), 0, 0), mat=rust))
    for i in range(6):
        a = i * math.pi / 3
        p.append(S.box(f"PO_wheel_spoke{i}", (0.11, 0.02, 0.016),
                       loc=(0.41, math.sin(a) * 0.0, 0), rot=(0, 0, a), mat=rust))
    return p


# ------------------------------------------------------------------ Level 3
def turbine(metal, copper, hazard):
    """Паровая турбина: корпус, лопатки, патрубки, щиток."""
    p = [S.cylinder("PS_turbine_body", 1.15, 3.2, 32, loc=(0, 0, 1.20), rot=(0, math.radians(90), 0), mat=metal),
         S.cylinder("PS_turbine_cap_l", 1.30, 0.22, 32, loc=(0, -1.65, 1.20), rot=(0, math.radians(90), 0), mat=metal),
         S.cylinder("PS_turbine_cap_r", 1.30, 0.22, 32, loc=(0, 1.65, 1.20), rot=(0, math.radians(90), 0), mat=metal),
         S.box("PS_turbine_base", (3.0, 3.4, 0.45), loc=(0, 0, 0.22), bevel=0.02, mat=metal),
         S.cylinder("PS_turbine_exhaust", 0.45, 1.4, 26, loc=(0, 0, 2.4), mat=copper),
         S.box("PS_turbine_panel", (0.5, 0.25, 0.7), loc=(-1.5, 0.8, 1.1), bevel=0.01, mat=hazard),
         S.box("PS_turbine_rail_l", (0.10, 3.4, 0.10), loc=(-1.4, 0, 1.9), mat=metal),
         S.box("PS_turbine_rail_r", (0.10, 3.4, 0.10), loc=(1.4, 0, 1.9), mat=metal)]
    for i in range(14):  # лопатки вентилятора на торце
        a = i / 14 * math.tau
        p.append(S.box(f"PS_blade{i}", (0.05, 0.55, 0.16),
                       loc=(1.78, math.sin(a) * 0.0, 1.20 + math.sin(a) * 0.0), rot=(a, 0, 0), mat=copper))
    return p


def reactor(vessel, glow, hazard):
    """Реактор: корпус, ТВЭЛ-кассеты, биозащита, пульт — центр Level 3."""
    p = [S.cylinder("PS_reactor_vessel", 1.6, 4.2, 36, loc=(0, 0, 2.1), mat=vessel),
         S.cylinder("PS_reactor_lid", 1.8, 0.35, 36, loc=(0, 0, 4.3), mat=hazard),
         S.cylinder("PS_reactor_base", 2.1, 0.6, 36, loc=(0, 0, 0.3), mat=hazard),
         S.cylinder("PS_reactor_core", 1.15, 2.6, 30, loc=(0, 0, 2.0), mat=glow)]
    for i in range(10):  # стержни управления / кассеты
        a = i / 10 * math.tau
        p.append(S.cylinder(f"PS_rod{i}", 0.11, 3.4, 16,
                            loc=(math.cos(a) * 1.32, math.sin(a) * 1.32, 2.4), mat=glow))
    for i in range(3):  # кольца биозащиты
        p.append(S.cylinder(f"PS_ring{i}", 1.72, 0.18, 36, loc=(0, 0, 1.0 + i * 1.3), mat=vessel))
    p.append(S.box("PS_console", (2.4, 0.9, 1.1), loc=(0, -3.0, 0.55), bevel=0.02, mat=hazard))
    p.append(S.box("PS_console_top", (2.3, 0.7, 0.10), loc=(0, -3.0, 1.12), rot=(math.radians(-18), 0, 0), mat=vessel))
    for i in range(6):
        p.append(S.box(f"PS_btn{i}", (0.10, 0.05, 0.03), loc=(-0.75 + i * 0.30, -3.1, 1.20),
                       rot=(math.radians(-18), 0, 0), mat=glow))
    return p


def electrical_panel(metal, paint, hazard):
    p = [S.box("PS_panel", (1.1, 0.35, 1.6), loc=(0, 0, 1.3), bevel=0.012, mat=paint),
         S.box("PS_panel_frame", (1.14, 0.06, 1.64), loc=(0, -0.17, 1.3), mat=metal),
         S.box("PS_panel_door", (1.04, 0.05, 1.5), loc=(0, -0.21, 1.3), mat=paint)]
    for i in range(3):  # ряды автоматов
        for j in range(6):
            p.append(S.box(f"PS_breaker{i}_{j}", (0.09, 0.06, 0.16),
                           loc=(-0.42 + j * 0.17, -0.25, 0.9 + i * 0.42), mat=metal))
    p.append(S.box("PS_panel_handle", (0.05, 0.06, 0.22), loc=(0.40, -0.25, 1.3), mat=metal))
    p.append(S.box("PS_panel_label", (0.4, 0.01, 0.10), loc=(-0.25, -0.245, 1.86), mat=hazard))
    return p


def transformer(metal, copper, hazard):
    p = [S.box("PS_trafo_body", (2.2, 1.4, 2.0), loc=(0, 0, 1.0), bevel=0.02, mat=metal),
         S.box("PS_trafo_fins", (2.3, 1.5, 0.12), loc=(0, 0, 2.05), mat=metal)]
    for i in range(9):  # радиаторные пластины
        p.append(S.box(f"PS_fin{i}", (0.06, 1.5, 1.5), loc=(-1.0 + i * 0.25, 0, 1.0), mat=metal))
    for i in range(3):  # вводы
        p.append(S.cylinder(f"PS_bushing{i}", 0.13, 0.9, 20, loc=(-0.6 + i * 0.6, 0, 2.5),
                            rot=(math.radians(6), 0, 0), mat=copper))
    p.append(S.box("PS_trafo_sign", (0.5, 0.02, 0.5), loc=(0, -0.72, 1.5), mat=hazard))
    return p


# ------------------------------------------------------------------ Стройка
def build_kit(wood, stone, metal, hqm):
    """Базовые формы строительного кита (тиры — назначением материала в Unity)."""
    objs = {}
    # стена 3×3 с рёбрами
    p = [S.box("BD_wall", (3.0, 0.16, 3.0), loc=(0, 0, 1.5), mat=wood),
         S.box("BD_wall_rib_top", (3.0, 0.20, 0.12), loc=(0, 0, 2.94), mat=wood),
         S.box("BD_wall_rib_bot", (3.0, 0.20, 0.12), loc=(0, 0, 0.06), mat=wood)]
    for i in range(3):
        p.append(S.box(f"BD_wall_plank{i}", (0.98, 0.19, 2.8), loc=(-1.0 + i * 1.0, 0, 1.5), mat=wood))
    objs["BD_wall"] = p
    # дверной проём (рама + перемычка)
    p = [S.box("BD_doorway_l", (0.35, 0.16, 3.0), loc=(-1.32, 0, 1.5), mat=wood),
         S.box("BD_doorway_r", (0.35, 0.16, 3.0), loc=(1.32, 0, 1.5), mat=wood),
         S.box("BD_doorway_top", (3.0, 0.16, 0.55), loc=(0, 0, 2.72), mat=wood)]
    objs["BD_doorway"] = p
    # окно (нижний/верхний пояс)
    p = [S.box("BD_window_bot", (3.0, 0.16, 1.0), loc=(0, 0, 0.5), mat=wood),
         S.box("BD_window_top", (3.0, 0.16, 0.8), loc=(0, 0, 2.6), mat=wood),
         S.box("BD_window_l", (0.4, 0.16, 3.0), loc=(-1.3, 0, 1.5), mat=wood),
         S.box("BD_window_r", (0.4, 0.16, 3.0), loc=(1.3, 0, 1.5), mat=wood)]
    objs["BD_window"] = p
    # фундамент 3×3 (с «шипами» под следующий этаж)
    p = [S.box("BD_foundation", (3.0, 0.4, 3.0), loc=(0, 0, -0.2), mat=stone)]
    for i in range(4):
        a = i * math.pi / 2 + math.pi / 4
        p.append(S.cylinder(f"BD_found_socket{i}", 0.10, 0.35, 14,
                            loc=(math.cos(a) * 1.2, math.sin(a) * 1.2, 0.05), mat=metal))
    objs["BD_foundation"] = p
    # перекрытие и лестница
    p = [S.box("BD_floor", (3.0, 3.0, 0.22), loc=(0, 0, 0), mat=wood)]
    for i in range(4):
        p.append(S.box(f"BD_floor_beam{i}", (3.0, 0.16, 0.10), loc=(0, -1.2 + i * 0.8, -0.14), mat=wood))
    objs["BD_floor"] = p
    p = []
    for i in range(9):
        p.append(S.box(f"BD_stair_step{i}", (1.2, 0.34, 0.12), loc=(0, i * 0.36, 0.10 + i * 0.30), mat=wood))
    p.append(S.box("BD_stair_rail_l", (0.08, 3.4, 0.9), loc=(-0.6, 1.5, 1.9), rot=(math.radians(-40), 0, 0), mat=wood))
    p.append(S.box("BD_stair_rail_r", (0.08, 3.4, 0.9), loc=(0.6, 1.5, 1.9), rot=(math.radians(-40), 0, 0), mat=wood))
    objs["BD_stairs"] = p
    # двери (металл/HQM) — та же форма, разные материалы
    objs["BD_door_metal"] = [S.box("BD_door", (1.0, 0.10, 2.05), loc=(0, 0, 1.02), bevel=0.01, mat=metal),
                             S.cylinder("BD_door_handle", 0.03, 0.14, 16, loc=(0.38, -0.07, 1.0),
                                        rot=(0, math.radians(90), 0), mat=metal),
                             S.box("BD_door_lock", (0.16, 0.06, 0.16), loc=(0.30, -0.06, 1.20), mat=metal)]
    objs["BD_door_armored"] = [S.box("BD_door_hqm", (1.0, 0.16, 2.05), loc=(0, 0, 1.02), bevel=0.012, mat=hqm),
                               S.box("BD_door_hqm_plate", (0.85, 0.04, 1.6), loc=(0, -0.10, 1.05), mat=hqm),
                               S.cylinder("BD_door_hqm_handle", 0.035, 0.16, 16, loc=(0.38, -0.14, 1.0),
                                          rot=(0, math.radians(90), 0), mat=metal)]
    return objs


def loot_bag(cloth, cloth_dark, strap, metal, paper):
    """Мешок с лутом (падает с игрока после смерти): брезентовая сумка с лямками,
    компрессионными ремнями, ручкой, биркой и металлической фурнитурой."""
    p = [S.box("BG_body", (0.70, 0.40, 0.42), loc=(0, 0, 0.22), bevel=0.075, mat=cloth),
         S.box("BG_bottom", (0.66, 0.37, 0.05), loc=(0, 0, 0.03), mat=cloth_dark),
         S.box("BG_opening", (0.66, 0.24, 0.06), loc=(0, 0, 0.435), mat=cloth_dark)]   # тень раскрытого верха
    # скатанные края брезента по обе стороны от проёма
    for i, y in enumerate((-0.15, 0.15)):
        p.append(S.cylinder(f"BG_roll{i}", 0.045, 0.72, 18, loc=(0, y, 0.44),
                            rot=(0, math.radians(90), 0), mat=cloth))
    # компрессионные ремни по корпусу + пряжки
    for i in range(3):
        x = -0.22 + i * 0.22
        p.append(S.box(f"BG_strap{i}", (0.085, 0.425, 0.44), loc=(x, 0, 0.22), bevel=0.012, mat=strap))
    for i in range(3):
        x = -0.22 + i * 0.22
        p.append(S.box(f"BG_buckle{i}", (0.10, 0.055, 0.075), loc=(x, -0.21, 0.26), bevel=0.008, mat=metal))
        p.append(S.box(f"BG_buckle_pin{i}", (0.055, 0.02, 0.02), loc=(x, -0.235, 0.26), mat=metal))
    # ручка-дуга и плечевой ремень с карабином
    # ручка — ровная полукруглая дуга (9 точек) с накладкой-грипсой
    arc = []
    for i in range(9):
        a = math.pi * i / 8.0
        arc.append((-0.16 * math.cos(a), 0.0, 0.42 + 0.17 * math.sin(a)))
    p.append(S.tube("BG_handle", arc, radius=0.022, mat=strap))
    p.append(S.cylinder("BG_handle_grip", 0.027, 0.11, 14, loc=(0, 0, 0.585),
                        rot=(0, math.radians(90), 0), mat=strap))
    for i, x in enumerate((-0.16, 0.16)):        # площадки крепления ручки
        p.append(S.box(f"BG_handle_mount{i}", (0.07, 0.06, 0.05), loc=(x, 0, 0.41), bevel=0.01, mat=metal))
    # плечевой ремень: лежит по корпусу, ниже ручки (не второй «хвост» сверху)
    p.append(S.tube("BG_sling", [(-0.30, 0.18, 0.10), (-0.33, 0.22, 0.30), (-0.20, 0.24, 0.445),
                                 (0.05, 0.24, 0.465), (0.26, 0.22, 0.34), (0.30, 0.18, 0.10)],
                    radius=0.018, mat=strap))
    p.append(S.box("BG_sling_buckle", (0.06, 0.045, 0.05), loc=(0.30, 0.18, 0.11), mat=metal))
    # бирка на шнурке и потёртая нашивка
    p.append(S.box("BG_tag", (0.10, 0.012, 0.075), loc=(0.24, -0.13, 0.33),
                   rot=(0, 0, math.radians(-18)), mat=paper))
    p.append(S.tube("BG_tag_cord", [(0.20, -0.12, 0.38), (0.235, -0.135, 0.365)], radius=0.004, mat=strap))
    p.append(S.box("BG_patch", (0.13, 0.012, 0.10), loc=(-0.16, -0.205, 0.16),
                   rot=(0, 0, math.radians(6)), mat=cloth_dark))
    # ножки: сумка стоит, а не вжата в пол
    for i, (x, y) in enumerate(((-0.28, -0.14), (0.28, -0.14), (-0.28, 0.14), (0.28, 0.14))):
        p.append(S.box(f"BG_foot{i}", (0.09, 0.09, 0.03), loc=(x, y, 0.012), bevel=0.01, mat=cloth_dark))
    return p


def build_all(render=True, only=None):
    render_dir = os.path.abspath(os.path.join(OUT, "..", "..", "..", "..", "..", "docs", "previews"))
    jobs = []   # (имя, лямбда(материалы))

    def mats():
        c = S.mats_pack("corridors"); q = S.mats_pack("poolrooms"); s3 = S.mats_pack("powerstation")
        return dict(
            wall=c["wallpaper"], trim=S.pbr_material("M_Trim_L0", (0.45, 0.42, 0.30, 1), 0.0, 0.55),
            carpet=c["carpet"], ceil=c["ceiling"], metal=c["metal"], lampglass=c["lamp"],
            tile=q["tile"], tile_dark=q["tile_dark"], pipe=q["metal"], rust=q["rust"],
            concrete=s3["concrete"], vessel=s3["metal"], glow=s3["reactor"], hazard=s3["hazard"],
            copper=s3["copper"], paint=S.pbr_material("M_PaintIndustrial", (0.16, 0.28, 0.34, 1), 0.0, 0.5),
            wood=S.pbr_material("M_WoodPlank", (0.32, 0.19, 0.10, 1), 0.0, 0.6, noise_scale=70, bump=0.4),
            stone=S.pbr_material("M_Stone", (0.42, 0.42, 0.40, 1), 0.0, 0.85, noise_scale=30, bump=0.5),
            hqm=S.pbr_material("M_HQM", (0.30, 0.31, 0.34, 1), 0.95, 0.28),
            cloth=S.pbr_material("M_Canvas", (0.34, 0.30, 0.20, 1), 0.0, 0.92, noise_scale=110, bump=0.6),
            cloth_dark=S.pbr_material("M_CanvasWorn", (0.20, 0.18, 0.13, 1), 0.0, 0.88, noise_scale=130, bump=0.5),
            strap=S.pbr_material("M_Webbing", (0.13, 0.12, 0.09, 1), 0.0, 0.80, noise_scale=160, bump=0.35),
            paper=S.pbr_material("M_PaperTag", (0.80, 0.78, 0.70, 1), 0.0, 0.78))
    M = {}

    jobs = [
        ("PR_wall_panel_L0", lambda: wall_panel(M["wall"], M["trim"])),
        ("PR_lamp_panel_L0", lambda: ceiling_lamp(M["metal"], M["lampglass"], M["ceil"])),
        ("PR_carpet_tile_L0", lambda: carpet_tile(M["carpet"])),
        ("PR_filing_cabinet", lambda: filing_cabinet(M["metal"], M["trim"])),
        ("PR_crate_wood", lambda: wood_crate(M["wood"], M["metal"])),
        ("PR_pool_tile_block", lambda: pool_tile_block(M["tile"], M["tile_dark"])),
        ("PR_locker", lambda: locker(M["metal"], M["paint"])),
        ("PR_pipe_kit", lambda: pipe_kit(M["pipe"], M["rust"])),
        ("PR_turbine", lambda: turbine(M["vessel"], M["copper"], M["hazard"])),
        ("PR_reactor", lambda: reactor(M["vessel"], M["glow"], M["hazard"])),
        ("PR_electrical_panel", lambda: electrical_panel(M["metal"], M["paint"], M["hazard"])),
        ("PR_transformer", lambda: transformer(M["vessel"], M["copper"], M["hazard"])),
        ("PR_loot_bag", lambda: loot_bag(M["cloth"], M["cloth_dark"], M["strap"], M["metal"], M["paper"])),
    ]
    if only:
        jobs = [j for j in jobs if j[0] == only]
    total = 0
    for name, fn in jobs:
        S.clean_scene()
        M = mats()
        parts = fn()
        for o in parts:
            S.smart_uv(o)
        joined = S.join_objects(parts, name)
        if joined is None:
            continue
        S.shade_smooth(joined, 32)
        tris = S.tri_count(joined)
        total += tris
        S.export_fbx([joined], os.path.join(OUT, name + ".fbx"))
        S.export_glb([joined], os.path.join(OUT, name + ".glb"))
        print(f"[props] {name}: {tris} трис")
        if render:
            S.render_fit(os.path.join(render_dir, name + ".png"), [joined], samples=24, res=(700, 520), fov_deg=34)

    # строительный кит (каждая деталь отдельным файлом) — только полной сборкой
    if not only:
        S.clean_scene()
        M = mats()
        kit = build_kit(M["wood"], M["stone"], M["metal"], M["hqm"])
        if only in kit:
            kit = {only: kit[only]}
        for name, parts in kit.items():
            for o in parts:
                S.smart_uv(o)
            joined = S.join_objects(parts, name)
            if joined is None:
                continue
            S.shade_smooth(joined, 32)
            total += S.tri_count(joined)
            S.export_fbx([joined], os.path.join(OUT, name + ".fbx"))
            S.export_glb([joined], os.path.join(OUT, name + ".glb"))
            print(f"[build] {name}: {S.tri_count(joined)} трис")
            if render:
                S.render_fit(os.path.join(render_dir, name + ".png"), [joined], samples=24, res=(700, 520), fov_deg=34)

    print(f"[props] ИТОГО трис: {total}")


if __name__ == "__main__":
    only = None
    if "--only" in sys.argv:
        k = sys.argv.index("--only")
        if k + 1 < len(sys.argv):
            only = sys.argv[k + 1]
    build_all(render="--no-render" not in sys.argv, only=only)
