"""
SUBSISTENCE — models_deployables.py  (УЛЬТРА-ДЕТАЛИЗАЦИЯ)
Деплои, которые игрок ставит сам (21_deploy / «Rust-копия»):
  DD_workbench        — верстак: стальная рама, деревянная столешница, тиски, ящики, полка с ключами, лампа;
  DD_furnace          — печь: чугунный корпус, топка с решёткой и светящимся зевом, дымоход, заслонка, зольник;
  DD_furnace_large    — большая печь: два корпуса, лоток-конвейер, вентиляторы, разводка труб;
  DD_cupboard         — Tool Cupboard: шкаф с полками ресурсов, кодовый замок, табличка владельца;
  DD_sleepingbag      — спальник: развёрнутый брезент с подушкой, ремни, молния;
  DD_bed              — кровать: стальная рама, матрас, изголовье, пружины;
  DD_purifier         — водоочиститель: бак, крышка, краник, колбы фильтров, бутылка;
  DD_autoturret       — автотурель: стойка, поворотная голова, спарка стволов, камера, ящик патронов, кабель;
  DD_samsite          — ПВО: пусковая рама с ракетами, радар-тарелка, кабели;
  DD_repair_bench     — ремонтный верстак: тиски, доска инструментов, ящик;
  DD_research_table   — стол исследований: микроскоп, монитор-терминал, сканер-платформа, образцы;
  DD_wind_generator   — ветрогенератор: мачта с растяжками, лопасти, генератор, кабель;
  DD_door_wood/metal/armored — двери трёх тиров: доски/панель/HQM-плита, петли, ручки, замок;
  DD_lock_key/code    — ключевой и кодовый замки: корпус, скважина, клавиатура, дисплей;
  DD_sign             — табличка: рамка, лист, стрелка, крепления;
  DD_barricade_concrete/metal — баррикады: бетонные плиты с арматурой / стальные ежи;
  DD_trap_spikes/bear — ловушки: доска с шипами / капкан с дугами и пружинами.
Масштаб — метры, низ модели на z=0, фасад — в +Y. Экспорт: FBX + GLB → Models/Props/.
"""
import sys, os, math
import bpy
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import subs_common as S

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.abspath(os.path.join(HERE, "..", "..", "UnityProject", "Assets", "Subsistence", "Models", "Props"))
PREVIEW = os.path.abspath(os.path.join(HERE, "..", "..", "docs", "previews"))


# ======================================================================= материалы
def materials():
    return dict(
        steel=S.pbr_material("M_DD_Steel", (0.44, 0.45, 0.48, 1), 0.88, 0.34, noise_scale=60, bump=0.35),
        steel_dark=S.pbr_material("M_DD_SteelDark", (0.16, 0.165, 0.18, 1), 0.82, 0.44, noise_scale=90, bump=0.45),
        hqm=S.pbr_material("M_DD_HQM", (0.30, 0.31, 0.34, 1), 0.95, 0.26),
        chrome=S.pbr_material("M_DD_Chrome", (0.76, 0.77, 0.79, 1), 0.98, 0.11),
        rust=S.pbr_material("M_DD_Rust", (0.34, 0.21, 0.13, 1), 0.55, 0.72, noise_scale=110, bump=0.6),
        copper=S.pbr_material("M_DD_Copper", (0.60, 0.35, 0.17, 1), 0.92, 0.32),
        paint_red=S.pbr_material("M_DD_PaintRed", (0.52, 0.09, 0.08, 1), 0.12, 0.44, noise_scale=80, bump=0.4),
        paint_blue=S.pbr_material("M_DD_PaintBlue", (0.11, 0.24, 0.42, 1), 0.14, 0.42, noise_scale=85, bump=0.4),
        paint_yellow=S.pbr_material("M_DD_PaintYellow", (0.88, 0.71, 0.12, 1), 0.10, 0.38, noise_scale=75, bump=0.4),
        wood=S.pbr_material("M_DD_Wood", (0.36, 0.23, 0.12, 1), 0.0, 0.66, noise_scale=95, bump=0.6),
        wood_dark=S.pbr_material("M_DD_WoodDark", (0.22, 0.14, 0.08, 1), 0.0, 0.70, noise_scale=120, bump=0.55),
        cloth=S.pbr_material("M_DD_Canvas", (0.34, 0.30, 0.20, 1), 0.0, 0.92, noise_scale=120, bump=0.6),
        cloth_dark=S.pbr_material("M_DD_CanvasWorn", (0.19, 0.17, 0.13, 1), 0.0, 0.88, noise_scale=140, bump=0.5),
        strap=S.pbr_material("M_DD_Webbing", (0.13, 0.12, 0.09, 1), 0.0, 0.80, noise_scale=160, bump=0.35),
        rubber=S.pbr_material("M_DD_Rubber", (0.045, 0.045, 0.05, 1), 0.0, 0.80, noise_scale=240, bump=0.6),
        plastic=S.pbr_material("M_DD_Plastic", (0.10, 0.11, 0.12, 1), 0.0, 0.50, noise_scale=200, bump=0.4),
        concrete=S.pbr_material("M_DD_Concrete", (0.36, 0.36, 0.35, 1), 0.0, 0.84, noise_scale=35, bump=0.35),
        glass=S.pbr_material("M_DD_Glass", (0.60, 0.68, 0.72, 1), 0.0, 0.06, alpha=0.18),
        fire=S.pbr_material("M_DD_Fire", (0.95, 0.42, 0.10, 1), 0.0, 0.5,
                            emission=(1.0, 0.42, 0.10, 1), emission_strength=11.0, noise_scale=25),
        lamp=S.pbr_material("M_DD_Lamp", (1.0, 0.96, 0.84, 1), 0.0, 0.28,
                            emission=(1.0, 0.94, 0.80, 1), emission_strength=8.0),
        screen=S.pbr_material("M_DD_Screen", (0.10, 0.30, 0.22, 1), 0.0, 0.30,
                              emission=(0.25, 1.0, 0.60, 1), emission_strength=6.0),
        paper=S.pbr_material("M_DD_Paper", (0.80, 0.78, 0.70, 1), 0.0, 0.78),
        brass=S.pbr_material("M_DD_Brass", (0.66, 0.52, 0.22, 1), 0.90, 0.30),
    )


# ====================================================================== хелперы
def bolt(name, loc, r=0.012, d=0.02, mat=None, rot=(0, 0, 0)):
    return S.cylinder(name, r, d, 12, loc=loc, rot=rot, mat=mat)


def grille(name, w, h, loc, rot=None, frame=None, body=None, bars=5):
    """Решётка (вентиляция, топка, дверца): панель ставится ПОД 90° внутрь стены."""
    rot = rot or (0, 0, 0)
    parts = [S.box(name + "_frame", (w, 0.04, h), loc=loc, rot=rot, bevel=0.006, mat=frame)]
    parts.append(S.box(name + "_glass", (w - 0.05, 0.018, h - 0.05),
                       loc=(loc[0], loc[1] + 0.012, loc[2]), rot=rot, mat=body))
    step = w / (bars + 1)
    for i in range(bars):
        dx = -w * 0.5 + step * (i + 1)
        parts.append(S.box(f"{name}_bar{i}", (0.022, 0.026, h - 0.03),
                           loc=(loc[0] + dx, loc[1] + 0.022, loc[2]), rot=rot, mat=frame))
    return parts


def screws(name, count, x0, step, loc, axis="z", mat=None, r=0.009):
    parts = []
    for i in range(count):
        p = (x0 + step * i, loc[1], loc[2])
        rot = (0, 0, 0) if axis != "y" else (math.radians(90), 0, 0)
        parts.append(bolt(f"{name}{i}", p, r=r, mat=mat, rot=rot))
    return parts


def hinge(name, x, y, z, mat):
    """Петля двери: две площадки + ось."""
    return [S.box(name + "_a", (0.07, 0.10, 0.11), loc=(x, y, z), bevel=0.008, mat=mat),
            S.box(name + "_b", (0.07, 0.10, 0.11), loc=(x, y, z + 0.22), bevel=0.008, mat=mat),
            S.cylinder(name + "_pin", 0.014, 0.34, 12, loc=(x - 0.03, y, z + 0.11), mat=mat)]


# ==================================================================== верстаки
def build_workbench(M):
    """Верстак: рама из уголка, деревянная столешница, тиски, два ящика, полка с инструментом, лампа."""
    st, sd, wd, ch = M["steel"], M["steel_dark"], M["wood"], M["chrome"]
    p = [S.box("WB_top", (1.60, 0.80, 0.08), loc=(0, 0, 0.94), bevel=0.008, mat=wd),
         S.box("WB_top_edge", (1.62, 0.82, 0.02), loc=(0, 0, 0.905), mat=sd)]
    for i in range(3):
        p.append(S.box(f"WB_plank{i}", (1.56, 0.24, 0.03), loc=(0, -0.27 + i * 0.27, 0.985), mat=wd))
    # ноги и обвязка
    for sx in (-1, 1):
        for sy in (-1, 1):
            p.append(S.box(f"WB_leg{sx}{sy}", (0.07, 0.07, 0.90), loc=(sx * 0.74, sy * 0.34, 0.45), mat=st))
            p.append(S.box(f"WB_foot{sx}{sy}", (0.11, 0.11, 0.03), loc=(sx * 0.74, sy * 0.34, 0.015), mat=sd))
    for z in (0.20, 0.62):
        p.append(S.box(f"WB_rail_l{z}", (0.05, 0.72, 0.05), loc=(-0.74, 0, z), mat=st))
        p.append(S.box(f"WB_rail_r{z}", (0.05, 0.72, 0.05), loc=(0.74, 0, z), mat=st))
        p.append(S.box(f"WB_cross{z}", (1.5, 0.05, 0.05), loc=(0, -0.34, z), mat=st))
    # ящики под столешницей
    for i in range(2):
        z = 0.72 - i * 0.30
        p.append(S.box(f"WB_drawer{i}", (0.66, 0.58, 0.26), loc=(-0.36, 0.04, z), bevel=0.01, mat=st))
        p.append(S.box(f"WB_drawer_f{i}", (0.60, 0.02, 0.20), loc=(-0.36, 0.34, z), mat=sd))
        p.append(S.box(f"WB_handle{i}", (0.22, 0.03, 0.03), loc=(-0.36, 0.36, z), mat=ch))
    # тиски
    p.append(S.box("WB_vise_base", (0.20, 0.16, 0.05), loc=(0.48, -0.12, 0.99), mat=sd))
    p.append(S.box("WB_vise_jaw", (0.22, 0.10, 0.14), loc=(0.48, -0.18, 1.08), bevel=0.008, mat=sd))
    p.append(S.box("WB_vise_slide", (0.10, 0.22, 0.09), loc=(0.48, -0.02, 1.04), mat=st))
    p.append(S.cylinder("WB_vise_screw", 0.016, 0.26, 14, loc=(0.48, 0.12, 1.04),
                        rot=(math.radians(90), 0, 0), mat=ch))
    p.append(S.cylinder("WB_vise_handle", 0.012, 0.20, 12, loc=(0.48, 0.24, 1.04),
                        rot=(0, 0, math.radians(90)), mat=ch))
    # полка с инструментом: гаечные ключи, молоток, отвертки, банка
    p.append(S.box("WB_shelf", (1.5, 0.26, 0.03), loc=(0, -0.30, 1.34), mat=wd))
    for sx in (-1, 1):
        p.append(S.box(f"WB_shelf_br{sx}", (0.04, 0.22, 0.36), loc=(sx * 0.72, -0.30, 1.18), mat=st))
    for i in range(4):
        p.append(S.box(f"WB_wrench{i}", (0.05, 0.02, 0.20 + i * 0.02),
                       loc=(-0.52 + i * 0.16, -0.34, 1.46 + i * 0.01), mat=ch))
    p.append(S.box("WB_hammer_head", (0.09, 0.05, 0.05), loc=(0.30, -0.33, 1.54), mat=st))
    p.append(S.cylinder("WB_hammer_h", 0.014, 0.26, 12, loc=(0.30, -0.33, 1.40), mat=wd))
    p.append(S.cylinder("WB_jar", 0.05, 0.14, 16, loc=(0.58, -0.30, 1.43), mat=M["glass"]))
    p.append(S.cylinder("WB_jar_lid", 0.055, 0.02, 16, loc=(0.58, -0.30, 1.51), mat=st))
    # лампа на кронштейне
    p.append(S.box("WB_lamp_br", (0.04, 0.30, 0.04), loc=(0.70, 0.10, 1.60), mat=sd))
    p.append(S.cylinder("WB_lamp_shade", 0.11, 0.10, 18, loc=(0.70, 0.24, 1.55), mat=sd))
    p.append(S.cylinder("WB_lamp_bulb", 0.05, 0.07, 14, loc=(0.70, 0.24, 1.47), mat=M["lamp"]))
    p.append(S.tube("WB_cable", [(0.70, 0.10, 1.62), (0.80, 0.02, 1.30), (0.74, -0.20, 0.20)], 0.010,
                    mat=M["rubber"], resolution=6))
    return p


def build_repair_bench(M):
    """Ремонтный верстак: тяжёлые тиски, доска инструментов, ящик деталей, держатель оружия."""
    st, sd, wd, ch = M["steel"], M["steel_dark"], M["wood"], M["chrome"]
    p = [S.box("RB_top", (1.40, 0.70, 0.10), loc=(0, 0, 0.88), bevel=0.01, mat=sd),
         S.box("RB_top_plate", (1.34, 0.64, 0.02), loc=(0, 0, 0.94), mat=st)]
    for sx in (-1, 1):
        for sy in (-1, 1):
            p.append(S.box(f"RB_leg{sx}{sy}", (0.09, 0.09, 0.84), loc=(sx * 0.62, sy * 0.28, 0.42), mat=st))
    p.append(S.box("RB_shelf", (1.30, 0.60, 0.05), loc=(0, 0, 0.22), mat=st))
    p.append(S.box("RB_box", (0.40, 0.32, 0.18), loc=(-0.42, 0.02, 0.32), bevel=0.01, mat=wd))
    for i in range(3):
        p.append(S.box(f"RB_box_lid{i}", (0.38, 0.06, 0.02), loc=(-0.42, -0.10 + i * 0.11, 0.42), mat=M["wood_dark"]))
    # доска инструментов над столом
    p.append(S.box("RB_board", (1.30, 0.04, 0.60), loc=(0, -0.28, 1.34), mat=wd))
    for i in range(6):
        p.append(S.box(f"RB_tool{i}", (0.045, 0.02, 0.24), loc=(-0.50 + i * 0.20, -0.31, 1.34), mat=ch))
    p.append(S.box("RB_tool_hammer", (0.10, 0.03, 0.05), loc=(0.44, -0.31, 1.52), mat=st))
    # тяжёлые тиски
    p.append(S.box("RB_vise", (0.30, 0.24, 0.16), loc=(0.36, 0.06, 1.02), bevel=0.01, mat=sd))
    p.append(S.cylinder("RB_vise_screw", 0.022, 0.30, 16, loc=(0.36, 0.24, 1.02),
                        rot=(math.radians(90), 0, 0), mat=ch))
    p.append(S.cylinder("RB_vise_handle", 0.014, 0.24, 12, loc=(0.36, 0.40, 1.02),
                        rot=(0, 0, math.radians(90)), mat=ch))
    # держатель ствола и маслёнка
    p.append(S.box("RB_gunrack", (0.06, 0.30, 0.50), loc=(-0.62, 0.22, 1.28), mat=st))
    p.append(S.box("RB_gunrack_lip", (0.10, 0.08, 0.04), loc=(-0.62, 0.36, 1.50), mat=st))
    p.append(S.cylinder("RB_oil", 0.045, 0.14, 16, loc=(0.66, -0.20, 1.01), mat=M["paint_red"]))
    p.append(S.cylinder("RB_oil_spout", 0.012, 0.10, 10, loc=(0.66, -0.14, 1.09), mat=st))
    return p


def build_research_table(M):
    """Стол исследований: сканер-платформа, микроскоп, терминал с экраном, полка образцов."""
    st, sd, wd, ch = M["steel"], M["steel_dark"], M["wood"], M["chrome"]
    p = [S.box("RT_top", (1.50, 0.80, 0.08), loc=(0, 0, 0.90), bevel=0.008, mat=st),
         S.box("RT_top_mat", (1.30, 0.66, 0.02), loc=(0, 0, 0.95), mat=M["rubber"])]
    for sx in (-1, 1):
        for sy in (-1, 1):
            p.append(S.box(f"RT_leg{sx}{sy}", (0.08, 0.08, 0.86), loc=(sx * 0.68, sy * 0.34, 0.43), mat=sd))
    p.append(S.box("RT_cross", (1.4, 0.05, 0.05), loc=(0, 0, 0.30), mat=st))
    # сканер-платформа с подсветкой
    p.append(S.box("RT_scanner", (0.60, 0.50, 0.10), loc=(-0.36, 0.06, 0.99), bevel=0.01, mat=sd))
    p.append(S.box("RT_scan_glow", (0.52, 0.42, 0.02), loc=(-0.36, 0.06, 1.05), mat=M["screen"]))
    p.append(S.box("RT_sample", (0.24, 0.18, 0.10), loc=(-0.36, 0.06, 1.11), bevel=0.008, mat=M["steel"]))
    p.append(S.cylinder("RT_scan_arm", 0.02, 0.40, 14, loc=(-0.62, 0.06, 1.20), mat=st))
    p.append(S.box("RT_scan_head", (0.22, 0.16, 0.06), loc=(-0.36, 0.06, 1.42), mat=sd))
    p.append(S.cylinder("RT_scan_lens", 0.045, 0.04, 16, loc=(-0.36, 0.06, 1.38), mat=M["screen"]))
    # микроскоп
    p.append(S.box("RT_micro_base", (0.22, 0.18, 0.04), loc=(0.34, -0.16, 0.96), mat=sd))
    p.append(S.cylinder("RT_micro_col", 0.025, 0.30, 14, loc=(0.30, -0.16, 1.12), mat=st))
    p.append(S.box("RT_micro_stage", (0.16, 0.16, 0.02), loc=(0.36, -0.16, 1.10), mat=st))
    p.append(S.cylinder("RT_micro_tube", 0.032, 0.22, 16, loc=(0.36, -0.16, 1.28),
                        rot=(math.radians(-18), 0, 0), mat=sd))
    p.append(S.cylinder("RT_micro_eye", 0.026, 0.08, 14, loc=(0.32, -0.20, 1.38),
                        rot=(math.radians(-18), 0, 0), mat=ch))
    # терминал
    p.append(S.box("RT_term_base", (0.26, 0.20, 0.04), loc=(0.52, 0.22, 0.96), mat=sd))
    p.append(S.box("RT_term_col", (0.10, 0.08, 0.34), loc=(0.52, 0.22, 1.14), mat=st))
    p.append(S.box("RT_term_screen", (0.44, 0.06, 0.30), loc=(0.52, 0.22, 1.42),
                   rot=(math.radians(-12), 0, 0), bevel=0.01, mat=sd))
    p.append(S.box("RT_term_glow", (0.38, 0.02, 0.24), loc=(0.52, 0.19, 1.42),
                   rot=(math.radians(-12), 0, 0), mat=M["screen"]))
    p.append(S.box("RT_term_key", (0.36, 0.14, 0.02), loc=(0.52, 0.26, 1.00), mat=M["plastic"]))
    # полка с образцами
    p.append(S.box("RT_shelf", (1.4, 0.22, 0.03), loc=(0, -0.34, 1.30), mat=wd))
    for i in range(4):
        p.append(S.cylinder(f"RT_vial{i}", 0.032, 0.13, 14, loc=(-0.42 + i * 0.26, -0.34, 1.38), mat=M["glass"]))
        p.append(S.cylinder(f"RT_vial_cap{i}", 0.036, 0.02, 14, loc=(-0.42 + i * 0.26, -0.34, 1.45), mat=sd))
    return p


# ======================================================================= печи
def build_furnace(M, large=False):
    """Печь: корпус, топка с решёткой и светящимся зевом, дымоход с заслонкой, зольник."""
    st, sd, rt = M["steel"], M["steel_dark"], M["rust"]
    if large:
        return build_furnace_large(M)

    p = [S.box("FN_body", (0.80, 0.80, 1.00), loc=(0, 0, 0.52), bevel=0.03, mat=st),
         S.box("FN_top", (0.84, 0.84, 0.05), loc=(0, 0, 1.03), mat=sd),
         S.box("FN_base", (0.86, 0.86, 0.06), loc=(0, 0, 0.03), mat=sd)]
    # рёбра корпуса
    for sx in (-1, 1):
        p.append(S.box(f"FN_rib{sx}", (0.03, 0.82, 1.0), loc=(sx * 0.41, 0, 0.52), mat=sd))
    for sy in (-1, 1):
        p.append(S.box(f"FN_rib_y{sy}", (0.82, 0.03, 1.0), loc=(0, sy * 0.41, 0.52), mat=sd))
    # топка: рамка + решётка + светящийся зев
    p.append(S.box("FN_door_frame", (0.52, 0.06, 0.44), loc=(0, 0.42, 0.56), bevel=0.01, mat=sd))
    p.append(S.box("FN_fire_glow", (0.42, 0.02, 0.34), loc=(0, 0.40, 0.56), mat=M["fire"]))
    p.extend(grille("FN_hob", 0.44, 0.36, loc=(0, 0.455, 0.56), frame=st, body=rt, bars=4))
    p.append(S.box("FN_door_top", (0.56, 0.10, 0.10), loc=(0, 0.44, 0.86), mat=sd))
    p.append(S.cylinder("FN_handle", 0.02, 0.16, 14, loc=(0, 0.52, 0.78),
                        rot=(0, 0, math.radians(90)), mat=M["chrome"]))
    # зольник снизу
    p.append(S.box("FN_ash", (0.34, 0.03, 0.14), loc=(0, 0.415, 0.16), mat=sd))
    p.append(S.box("FN_ash_handle", (0.16, 0.04, 0.03), loc=(0, 0.44, 0.16), mat=M["chrome"]))
    # дымоход с заслонкой и кольцами
    p.append(S.cylinder("FN_flue", 0.11, 0.70, 20, loc=(0, -0.18, 1.40), mat=rt))
    p.append(S.cylinder("FN_flue_ring", 0.12, 0.04, 20, loc=(0, -0.18, 1.34), mat=sd))
    p.append(S.cylinder("FN_flue_ring2", 0.135, 0.06, 20, loc=(0, -0.18, 1.74), mat=sd))
    p.append(S.box("FN_flue_damp", (0.24, 0.02, 0.02), loc=(0, -0.30, 1.52), mat=st))
    p.append(S.cylinder("FN_flue_knob", 0.024, 0.05, 12, loc=(0, -0.40, 1.52),
                        rot=(math.radians(90), 0, 0), mat=M["chrome"]))
    # приточка и кабель
    p.extend(grille("FN_vent", 0.24, 0.16, loc=(0.30, -0.41, 0.30), frame=sd, body=rt, bars=3,
                    rot=(0, 0, math.radians(90))))
    p.append(S.tube("FN_pipe", [(0.40, -0.20, 0.16), (0.56, -0.24, 0.10), (0.66, -0.20, 0.03)], 0.014,
                    mat=M["copper"], resolution=6))
    p.append(S.box("FN_tag", (0.14, 0.02, 0.10), loc=(0.22, 0.405, 0.90), mat=M["paint_yellow"]))
    return p


def build_furnace_large(M):
    """Большая печь: два корпуса, лоток-конвейер, вентиляторы, разводка труб, балка крана."""
    st, sd, rt, cu = M["steel"], M["steel_dark"], M["rust"], M["copper"]
    p = [S.box("FNL_body", (1.60, 1.00, 1.10), loc=(0, 0, 0.60), bevel=0.03, mat=st),
         S.box("FNL_body2", (0.90, 0.90, 0.80), loc=(0.45, -0.10, 1.60), bevel=0.03, mat=st),
         S.box("FNL_body2_leg", (0.12, 0.12, 0.35), loc=(0.45, -0.10, 1.28), mat=sd),
         S.box("FNL_body2_collar", (0.98, 0.98, 0.06), loc=(0.45, -0.10, 1.20), mat=sd),
         S.box("FNL_base", (1.70, 1.10, 0.10), loc=(0, 0, 0.05), mat=sd)]
    for sx in (-1, 1):
        p.append(S.box(f"FNL_rib{sx}", (0.04, 1.02, 1.1), loc=(sx * 0.81, 0, 0.60), mat=sd))
    # топка с двумя светящимися зевами
    p.append(S.box("FNL_fire", (1.30, 0.04, 0.40), loc=(0, 0.51, 0.62), mat=M["fire"]))
    p.extend(grille("FNL_hob", 1.34, 0.44, loc=(0, 0.545, 0.62), frame=st, body=rt, bars=8))
    p.append(S.box("FNL_door_top", (1.40, 0.12, 0.12), loc=(0, 0.53, 1.00), mat=sd))
    # лоток-конвейер с роликами
    p.append(S.box("FNL_tray", (1.80, 0.34, 0.06), loc=(0, 0.66, 0.22), rot=(math.radians(-8), 0, 0), mat=st))
    for i in range(6):
        p.append(S.cylinder(f"FNL_roller{i}", 0.05, 0.30, 14, loc=(-0.75 + i * 0.30, 0.66, 0.26),
                            rot=(0, math.radians(90), 0), mat=sd))
    # вентиляторы и трубы
    for sx in (-1, 1):
        p.append(S.cylinder(f"FNL_fan{sx}", 0.24, 0.10, 20, loc=(sx * 0.60, -0.52, 1.30),
                            rot=(math.radians(90), 0, 0), mat=sd))
        for b in range(4):
            a = b * math.pi / 2 + math.radians(20)
            p.append(S.box(f"FNL_fan_blade{sx}{b}", (0.20, 0.05, 0.02),
                           loc=(sx * 0.60 + math.cos(a) * 0.10, -0.50, 1.30 + math.sin(a) * 0.10),
                           rot=(0, 0, math.radians(b * 45)), mat=st))
    p.append(S.cylinder("FNL_flue", 0.16, 1.40, 22, loc=(0.45, -0.28, 2.70), mat=rt))
    p.append(S.cylinder("FNL_flue_cap", 0.22, 0.12, 22, loc=(0.45, -0.28, 3.42), mat=sd))
    p.append(S.tube("FNL_pipe_a", [(-0.70, -0.30, 0.55), (-1.05, -0.34, 0.90), (-1.05, -0.34, 1.70)], 0.05,
                    mat=cu, resolution=6))
    p.append(S.tube("FNL_pipe_b", [(0.86, 0.20, 1.90), (1.30, 0.24, 2.10), (1.30, 0.24, 2.70)], 0.045,
                    mat=cu, resolution=6))
    p.append(S.box("FNL_hazard", (1.66, 1.02, 0.06), loc=(0, 0, 1.13), mat=M["paint_yellow"]))
    return p


# ================================================================= шкаф и кровати
def build_cupboard(M):
    """Tool Cupboard: шкаф с полками ресурсов, кодовый замок, табличка владельца, вентиляция."""
    st, sd, wd = M["steel"], M["steel_dark"], M["wood"]
    # Шкаф собирается ПАНЕЛЯМИ, а не сплошным кубом — иначе полки с ресурсами внутри не видны.
    p = [S.box("TC_side_l", (0.05, 0.46, 1.05), loc=(-0.35, 0, 0.55), bevel=0.012, mat=st),
         S.box("TC_side_r", (0.05, 0.46, 1.05), loc=(0.35, 0, 0.55), bevel=0.012, mat=st),
         S.box("TC_top", (0.75, 0.50, 0.05), loc=(0, 0, 1.10), mat=sd),
         S.box("TC_base", (0.75, 0.50, 0.07), loc=(0, 0, 0.035), mat=sd),
         S.box("TC_roof_lip", (0.77, 0.52, 0.03), loc=(0, 0, 1.14), mat=st)]
    # проём с полками и ресурсами
    p.append(S.box("TC_back", (0.66, 0.03, 1.0), loc=(0, -0.21, 0.55), mat=sd))
    for i in range(3):
        z = 0.22 + i * 0.32
        p.append(S.box(f"TC_shelf{i}", (0.62, 0.40, 0.03), loc=(0, 0, z), mat=st))
    # ресурсы: доски, камни, фрагменты металла, сера
    p.append(S.box("TC_wood", (0.30, 0.18, 0.10), loc=(-0.16, 0.06, 0.29), mat=wd))
    for i in range(3):
        p.append(S.box(f"TC_plank{i}", (0.28, 0.05, 0.02), loc=(-0.16, 0.06, 0.35 + i * 0.02), mat=M["wood_dark"]))
    p.append(S.box("TC_stone", (0.22, 0.16, 0.10), loc=(0.16, 0.06, 0.29), mat=M["concrete"]))
    p.append(S.cylinder("TC_stone2", 0.07, 0.10, 12, loc=(0.20, -0.02, 0.29), mat=M["concrete"]))
    p.append(S.box("TC_frags", (0.20, 0.14, 0.08), loc=(-0.18, 0.04, 0.61), mat=M["rust"]))
    p.append(S.box("TC_sulfur", (0.16, 0.12, 0.08), loc=(0.18, 0.04, 0.61), mat=M["paint_yellow"]))
    for i in range(4):
        p.append(S.box(f"TC_ammo{i}", (0.09, 0.07, 0.06), loc=(-0.20 + i * 0.13, 0.10, 0.925), mat=M["paint_blue"]))
    # рамка проёма и кодовый замок
    p.append(S.box("TC_frame_l", (0.04, 0.05, 1.05), loc=(-0.36, 0.23, 0.55), mat=sd))
    p.append(S.box("TC_frame_r", (0.04, 0.05, 1.05), loc=(0.36, 0.23, 0.55), mat=sd))
    p.append(S.box("TC_frame_t", (0.74, 0.05, 0.05), loc=(0, 0.23, 1.075), mat=sd))
    p.append(S.box("TC_frame_b", (0.74, 0.05, 0.05), loc=(0, 0.23, 0.075), mat=sd))
    # приоткрытая дверца: и полки, и замок читаются
    p.append(S.box("TC_door", (0.70, 0.045, 1.02), loc=(0.62, 0.30, 0.55),
                   rot=(0, 0, math.radians(-34)), bevel=0.012, mat=st))
    p.append(S.box("TC_door_brace", (0.66, 0.02, 0.08), loc=(0.60, 0.28, 1.00),
                   rot=(0, 0, math.radians(-34)), mat=sd))
    for hz in (0.25, 0.90):
        p.append(S.cylinder(f"TC_door_hinge{int(hz*100)}", 0.022, 0.09, 12, loc=(0.35, 0.26, hz), mat=sd))
    p.append(S.box("TC_lock", (0.16, 0.06, 0.20), loc=(0.22, 0.25, 0.62), bevel=0.01, mat=sd))
    for r in range(3):
        for c in range(3):
            p.append(S.box(f"TC_key{r}{c}", (0.035, 0.012, 0.028),
                           loc=(0.17 + c * 0.05, 0.285, 0.68 - r * 0.045), mat=M["plastic"]))
    p.append(S.box("TC_code_screen", (0.13, 0.01, 0.03), loc=(0.22, 0.285, 0.55), mat=M["screen"]))
    p.append(S.cylinder("TC_lock_bolt", 0.022, 0.08, 12, loc=(-0.30, 0.26, 0.62),
                        rot=(0, math.radians(90), 0), mat=M["chrome"]))
    # вентиляция и табличка
    p.extend(grille("TC_vent", 0.30, 0.20, loc=(0, -0.235, 0.30), frame=sd, body=M["rust"], bars=4,
                    rot=(0, 0, math.radians(180))))
    p.append(S.box("TC_plate", (0.28, 0.02, 0.14), loc=(-0.18, 0.245, 0.92), mat=M["paint_yellow"]))
    p.append(S.box("TC_plate2", (0.22, 0.01, 0.02), loc=(-0.18, 0.255, 0.94), mat=M["paper"]))
    for sx in (-1, 1):
        for sy in (-1, 1):
            p.append(S.box(f"TC_foot{sx}{sy}", (0.10, 0.10, 0.03), loc=(sx * 0.28, sy * 0.18, 0.015), mat=sd))
    return p


def build_sleepingbag(M):
    """Спальник: развёрнутый брезент с подушкой и молнией, ремни, фонарик рядом."""
    cl, cd, st = M["cloth"], M["cloth_dark"], M["strap"]
    p = [S.box("SB_body", (0.86, 1.90, 0.16), loc=(0, 0, 0.09), bevel=0.05, mat=cl),
         S.box("SB_under", (0.80, 1.82, 0.04), loc=(0, 0, 0.02), mat=M["rubber"])]
    # скатка у головы и подушка
    p.append(S.cylinder("SB_roll", 0.10, 0.84, 18, loc=(0, 0.86, 0.14),
                        rot=(0, math.radians(90), 0), mat=cd))
    p.append(S.box("SB_pillow", (0.42, 0.30, 0.10), loc=(0, 0.66, 0.20), bevel=0.06, mat=cd))
    # одеяло-отворот и молния
    p.append(S.box("SB_cover", (0.86, 0.70, 0.06), loc=(0, -0.50, 0.19), bevel=0.03, mat=cd))
    p.append(S.box("SB_zip", (0.03, 1.60, 0.02), loc=(0.40, -0.05, 0.18), mat=st))
    p.append(S.box("SB_zip_pull", (0.05, 0.09, 0.02), loc=(0.40, 0.72, 0.19), mat=M["chrome"]))
    # ремни-компрессия и бирка
    for i in range(2):
        p.append(S.box(f"SB_strap{i}", (0.90, 0.07, 0.19), loc=(0, -0.30 - i * 0.55, 0.10), mat=st))
    p.append(S.box("SB_tag", (0.10, 0.02, 0.07), loc=(-0.30, 0.84, 0.26), rot=(0, 0, math.radians(-14)),
                   mat=M["paper"]))
    # фонарик и ботинок рядом — «кто-то спал здесь недавно»
    p.append(S.cylinder("SB_torch", 0.035, 0.18, 14, loc=(0.62, -0.40, 0.06),
                        rot=(0, math.radians(90), 0), mat=M["paint_red"]))
    p.append(S.cylinder("SB_torch_head", 0.05, 0.06, 14, loc=(0.72, -0.40, 0.06),
                        rot=(0, math.radians(90), 0), mat=M["chrome"]))
    p.append(S.box("SB_boot", (0.14, 0.30, 0.16), loc=(-0.64, -0.20, 0.10), bevel=0.03, mat=M["cloth_dark"]))
    p.append(S.box("SB_boot_sole", (0.15, 0.31, 0.04), loc=(-0.64, -0.20, 0.02), mat=M["rubber"]))
    return p


def build_bed(M):
    """Кровать: стальная рама на ножках, матрас, изголовье-сетка, подушка, пружины."""
    st, sd, cl = M["steel"], M["steel_dark"], M["cloth"]
    p = [S.box("BD_frame", (1.10, 2.00, 0.10), loc=(0, 0, 0.30), bevel=0.01, mat=st),
         S.box("BD_mattress", (1.00, 1.88, 0.20), loc=(0, 0, 0.45), bevel=0.05, mat=cl),
         S.box("BD_sheet", (1.00, 1.86, 0.03), loc=(0, 0, 0.56), mat=M["cloth_dark"])]
    for sx in (-1, 1):
        for sy in (-1, 1):
            p.append(S.box(f"BD_leg{sx}{sy}", (0.08, 0.08, 0.30), loc=(sx * 0.50, sy * 0.94, 0.15), mat=st))
            p.append(S.box(f"BD_cap{sx}{sy}", (0.11, 0.11, 0.03), loc=(sx * 0.50, sy * 0.94, 0.015), mat=sd))
    # изголовье-сетка
    p.append(S.box("BD_head_l", (0.07, 0.07, 0.70), loc=(-0.52, 0.96, 0.70), mat=st))
    p.append(S.box("BD_head_r", (0.07, 0.07, 0.70), loc=(0.52, 0.96, 0.70), mat=st))
    p.append(S.box("BD_head_top", (1.10, 0.07, 0.07), loc=(0, 0.96, 1.02), mat=st))
    for i in range(5):
        p.append(S.box(f"BD_head_bar{i}", (0.04, 0.04, 0.62), loc=(-0.40 + i * 0.20, 0.96, 0.70), mat=sd))
    for i in range(3):
        p.append(S.box(f"BD_head_row{i}", (1.02, 0.04, 0.04), loc=(0, 0.96, 0.45 + i * 0.22), mat=sd))
    # пружины рамы и подушка
    for i in range(6):
        p.append(S.cylinder(f"BD_spring{i}", 0.030, 0.06, 10, loc=(-0.40 + (i % 3) * 0.40, -0.60 + (i // 3) * 1.2,
                                                                       0.335), mat=sd))
    p.append(S.box("BD_pillow", (0.52, 0.32, 0.12), loc=(0, 0.72, 0.62), bevel=0.06, mat=M["paper"]))
    p.append(S.box("BD_blanket", (0.98, 1.00, 0.06), loc=(0, -0.45, 0.58), bevel=0.02, mat=M["cloth_dark"]))
    p.append(S.box("BD_bag", (0.30, 0.22, 0.18), loc=(-0.30, -0.92, 0.09), bevel=0.03, mat=M["strap"]))
    return p


# ==================================================================== вода, ПВО
def build_purifier(M):
    """Водоочиститель: бак, крышка, краник, колбы фильтров, манометр, бутылка."""
    st, sd, ch = M["steel"], M["steel_dark"], M["chrome"]
    p = [S.cylinder("WP_tank", 0.28, 0.80, 24, loc=(0, 0, 0.46), mat=st),
         S.cylinder("WP_cap", 0.30, 0.06, 24, loc=(0, 0, 0.88), mat=sd),
         S.cylinder("WP_top", 0.20, 0.05, 20, loc=(0, 0, 0.93), mat=sd),
         S.box("WP_base", (0.60, 0.60, 0.08), loc=(0, 0, 0.04), bevel=0.01, mat=sd)]
    for i in range(0, 360, 45):
        a = math.radians(i)
        p.append(S.box(f"WP_rib{i}", (0.03, 0.02, 0.70), loc=(math.cos(a) * 0.29, math.sin(a) * 0.29, 0.46),
                       rot=(0, 0, -a), mat=sd))
    # краник и труба
    p.append(S.cylinder("WP_spout", 0.03, 0.16, 14, loc=(0, 0.30, 0.26), rot=(math.radians(90), 0, 0), mat=ch))
    p.append(S.cylinder("WP_spout_v", 0.03, 0.10, 14, loc=(0, 0.38, 0.22), mat=ch))
    p.append(S.cylinder("WP_valve", 0.05, 0.06, 14, loc=(0, 0.22, 0.34), mat=M["paint_red"]))
    p.append(S.tube("WP_hose", [(0.16, 0.10, 0.80), (0.30, 0.24, 0.62), (0.26, 0.30, 0.26)], 0.018,
                    mat=M["rubber"], resolution=6))
    # фильтры: две колбы со стеклом
    for i in range(2):
        x = -0.20 + i * 0.40
        p.append(S.cylinder(f"WP_filter{i}", 0.08, 0.34, 16, loc=(x, -0.30, 0.26), mat=st))
        p.append(S.cylinder(f"WP_filter_glass{i}", 0.085, 0.18, 16, loc=(x, -0.30, 0.22), mat=M["glass"]))
        p.append(S.cylinder(f"WP_filter_cap{i}", 0.09, 0.04, 16, loc=(x, -0.30, 0.45), mat=sd))
        p.append(S.tube(f"WP_filter_line{i}", [(x, -0.24, 0.42), (x * 0.5, -0.16, 0.60), (0, -0.06, 0.70)],
                        0.014, mat=M["copper"], resolution=6))
    # манометр и бутылка
    p.append(S.cylinder("WP_gauge", 0.07, 0.04, 18, loc=(0, 0.26, 0.66), rot=(math.radians(90), 0, 0), mat=ch))
    p.append(S.box("WP_gauge_face", (0.10, 0.01, 0.10), loc=(0, 0.285, 0.66), mat=M["paper"]))
    p.append(S.cylinder("WP_bottle", 0.045, 0.20, 16, loc=(0.42, 0.20, 0.10), mat=M["glass"]))
    p.append(S.cylinder("WP_bottle_cap", 0.03, 0.05, 14, loc=(0.42, 0.20, 0.22), mat=M["paint_blue"]))
    p.append(S.box("WP_label", (0.16, 0.06, 0.10), loc=(0, 0.30, 0.52), mat=M["paint_blue"]))
    return p


def build_autoturret(M):
    """Автотурель: стойка, поворотная голова, спарка стволов, камера, ящик патронов, кабель."""
    st, sd, ch = M["steel"], M["steel_dark"], M["chrome"]
    p = [S.box("AT_base", (0.46, 0.46, 0.08), loc=(0, 0, 0.04), bevel=0.01, mat=sd),
         S.cylinder("AT_col", 0.09, 0.90, 18, loc=(0, 0, 0.53), mat=st),
         S.cylinder("AT_collar", 0.13, 0.10, 20, loc=(0, 0, 0.96), mat=sd),
         S.box("AT_yoke", (0.34, 0.20, 0.16), loc=(0, 0, 1.10), bevel=0.01, mat=st)]
    # голова с крышкой и коробом
    p.append(S.box("AT_head", (0.30, 0.40, 0.22), loc=(0, 0.02, 1.26), bevel=0.02, mat=sd))
    p.append(S.box("AT_head_top", (0.26, 0.36, 0.05), loc=(0, 0.02, 1.39), mat=st))
    for sx in (-1, 1):
        p.append(S.cylinder(f"AT_barrel{sx}", 0.032, 0.44, 16, loc=(sx * 0.075, 0.32, 1.24),
                            rot=(math.radians(90), 0, 0), mat=sd))
        p.append(S.cylinder(f"AT_barrel_tip{sx}", 0.042, 0.10, 16, loc=(sx * 0.075, 0.56, 1.24),
                            rot=(math.radians(90), 0, 0), mat=st))
        p.append(S.cylinder(f"AT_ammo{sx}", 0.085, 0.18, 16, loc=(sx * 0.075, 0.16, 1.33),
                            rot=(math.radians(90), 0, 0), mat=M["paint_blue"]))
    p.append(S.box("AT_mag", (0.20, 0.14, 0.30), loc=(0, -0.16, 1.06), bevel=0.01, mat=sd))
    # камера, фонарь, датчик
    p.append(S.box("AT_cam", (0.10, 0.16, 0.10), loc=(0, 0.20, 1.44), mat=M["plastic"]))
    p.append(S.cylinder("AT_cam_lens", 0.035, 0.05, 14, loc=(0, 0.30, 1.44), rot=(math.radians(90), 0, 0),
                        mat=M["screen"]))
    p.append(S.cylinder("AT_lamp", 0.05, 0.10, 14, loc=(0.16, 0.22, 1.36), rot=(math.radians(90), 0, 0),
                        mat=M["lamp"]))
    p.append(S.box("AT_lamp_br", (0.04, 0.10, 0.04), loc=(0.10, 0.20, 1.36), mat=sd))
    # ящик патронов на стойке и кабель
    p.append(S.box("AT_crate", (0.34, 0.24, 0.16), loc=(0, -0.30, 0.22), bevel=0.01, mat=M["paint_blue"]))
    p.append(S.box("AT_crate_belt", (0.36, 0.26, 0.02), loc=(0, -0.30, 0.30), mat=st))
    p.append(S.tube("AT_cable", [(0.10, -0.20, 0.90), (0.26, -0.26, 0.50), (0.30, -0.24, 0.06)], 0.014,
                    mat=M["rubber"], resolution=6))
    p.append(S.cylinder("AT_sensor", 0.04, 0.06, 12, loc=(0, 0.24, 1.20), rot=(math.radians(90), 0, 0),
                        mat=M["paint_red"]))
    return p


def build_samsite(M):
    """ПВО-станция: пусковая рама, две ракеты, радар-тарелка, стойка, кабели."""
    st, sd = M["steel"], M["steel_dark"]
    p = [S.box("SS_base", (0.90, 0.90, 0.10), loc=(0, 0, 0.05), bevel=0.01, mat=sd),
         S.cylinder("SS_col", 0.11, 0.70, 18, loc=(0, 0, 0.44), mat=st),
         S.box("SS_frame", (0.80, 0.24, 0.12), loc=(0, 0, 0.84), bevel=0.01, mat=st)]
    # направляющие и ракеты
    for sx in (-1, 1):
        p.append(S.box(f"SS_rail{sx}", (0.16, 1.10, 0.06), loc=(sx * 0.22, 0.30, 1.02),
                       rot=(math.radians(38), 0, 0), mat=sd))
        p.append(S.cylinder(f"SS_rocket{sx}", 0.075, 0.90, 18, loc=(sx * 0.22, 0.34, 1.24),
                            rot=(math.radians(38), 0, 0), mat=M["paint_yellow"]))
        p.append(S.cylinder(f"SS_rocket_nose{sx}", 0.075, 0.16, 18, loc=(sx * 0.22, 0.62, 1.60),
                            rot=(math.radians(38), 0, 0), mat=M["paint_red"]))
        for f in range(3):
            a = f * math.pi * 2 / 3
            p.append(S.box(f"SS_fin{sx}{f}", (0.02, 0.14, 0.16),
                           loc=(sx * 0.22 + math.cos(a) * 0.08, 0.02, 0.96 + math.sin(a) * 0.08),
                           rot=(math.radians(38), 0, a), mat=sd))
    # радар-тарелка на шарнире
    p.append(S.cylinder("SS_radar_mast", 0.05, 0.70, 14, loc=(-0.34, -0.28, 1.15), mat=st))
    p.append(S.cylinder("SS_dish", 0.42, 0.06, 24, loc=(-0.34, -0.28, 1.52),
                        rot=(math.radians(58), 0, math.radians(20)), mat=st))
    p.append(S.cylinder("SS_dish_hub", 0.07, 0.14, 16, loc=(-0.34, -0.16, 1.60),
                        rot=(math.radians(58), 0, math.radians(20)), mat=sd))
    p.append(S.box("SS_dish_grid", (0.62, 0.02, 0.02), loc=(-0.34, -0.25, 1.60),
                   rot=(math.radians(58), 0, math.radians(20)), mat=sd))
    # шкаф управления и кабели
    p.append(S.box("SS_cab", (0.34, 0.28, 0.50), loc=(0.34, -0.30, 0.35), bevel=0.01, mat=M["paint_blue"]))
    p.append(S.box("SS_cab_door", (0.30, 0.02, 0.44), loc=(0.34, -0.15, 0.35), mat=sd))
    p.append(S.box("SS_cab_screen", (0.16, 0.01, 0.10), loc=(0.34, -0.14, 0.48), mat=M["screen"]))
    p.append(S.tube("SS_cable", [(0.34, -0.16, 0.30), (0.20, -0.02, 0.12), (-0.10, 0.10, 0.03)], 0.014,
                    mat=M["rubber"], resolution=6))
    return p


def build_wind_generator(M):
    """Ветрогенератор (скрап): мачта с растяжками, ротор с лопастями, генератор, кабель."""
    st, sd, rt = M["steel"], M["steel_dark"], M["rust"]
    p = [S.cylinder("WG_base", 0.30, 0.10, 20, loc=(0, 0, 0.05), mat=sd),
         S.cylinder("WG_mast", 0.07, 2.00, 18, loc=(0, 0, 1.05), mat=st),
         S.cylinder("WG_mast2", 0.055, 1.10, 18, loc=(0, 0, 2.55), mat=st)]
    # растяжки
    for i in range(3):
        a = i * math.pi * 2 / 3
        p.append(S.tube(f"WG_guy{i}", [(math.cos(a) * 0.16, math.sin(a) * 0.16, 2.30),
                                       (math.cos(a) * 1.20, math.sin(a) * 1.20, 0.05)], 0.014,
                        mat=M["strap"], resolution=5))
        p.append(S.box(f"WG_peg{i}", (0.10, 0.10, 0.05), loc=(math.cos(a) * 1.20, math.sin(a) * 1.20, 0.03),
                       mat=st))
    # генератор и хвост
    p.append(S.box("WG_gen", (0.28, 0.40, 0.26), loc=(0, 0.24, 2.72), bevel=0.02, mat=rt))
    for i in range(4):
        p.append(S.cylinder(f"WG_gen_rib{i}", 0.03, 0.38, 12, loc=(-0.12 + i * 0.08, 0.24, 2.72),
                            rot=(math.radians(90), 0, 0), mat=sd))
    p.append(S.box("WG_tail", (0.06, 0.70, 0.34), loc=(0, 0.72, 2.80), rot=(math.radians(-8), 0, 0), mat=st))
    # ротор: ступица + 3 лопасти
    p.append(S.cylinder("WG_hub", 0.10, 0.16, 18, loc=(0, -0.10, 2.72), rot=(math.radians(90), 0, 0), mat=sd))
    for b in range(3):
        a = math.radians(b * 120)
        px, pz = math.cos(a) * 0.55, math.sin(a) * 0.55
        p.append(S.box(f"WG_blade{b}", (0.16, 0.04, 1.10), loc=(px, -0.08, 2.72 + pz),
                       rot=(0, -a, 0), mat=M["paper"]))
        p.append(S.box(f"WG_blade_rib{b}", (0.04, 0.05, 1.05), loc=(px, -0.08, 2.72 + pz),
                       rot=(0, -a, 0), mat=st))
    p.append(S.cylinder("WG_tip", 0.03, 0.12, 12, loc=(0, -0.22, 2.72), rot=(math.radians(90), 0, 0), mat=st))
    p.append(S.tube("WG_cable", [(0.10, 0.04, 0.10), (0.40, 0.16, 0.05), (0.70, 0.10, 0.02)], 0.012,
                    mat=M["rubber"], resolution=6))
    p.append(S.box("WG_box", (0.30, 0.24, 0.20), loc=(0.55, 0.10, 0.10), bevel=0.01, mat=M["paint_blue"]))
    return p


# ===================================================================== двери/замки
def build_door(M, tier="wood"):
    """Дверь: полотно, петли, ручка, замок. Тиры: wood (доски), metal (панель), armored (HQM)."""
    st, sd, ch = M["steel"], M["steel_dark"], M["chrome"]
    if tier == "wood":
        body, thick = M["wood"], 0.06
    elif tier == "metal":
        body, thick = M["steel"], 0.09
    else:
        body, thick = M["hqm"], 0.14
    name = {"wood": "DW", "metal": "DM", "armored": "DA"}[tier]
    p = [S.box(name + "_slab", (1.00, thick, 2.05), loc=(0, 0, 1.025), bevel=0.012, mat=body)]
    if tier == "wood":
        for i in range(5):
            p.append(S.box(f"{name}_plank{i}", (1.02, 0.02, 0.34), loc=(0, -0.035, 0.24 + i * 0.40), mat=M["wood_dark"]))
        for i in range(2):
            p.append(S.box(f"{name}_brace{i}", (1.02, 0.02, 0.10), loc=(0, -0.05, 0.70 + i * 0.70),
                           rot=(0, 0, math.radians(14 - i * 28)), mat=M["wood_dark"]))
    elif tier == "metal":
        for i in range(3):
            p.append(S.box(f"{name}_rib{i}", (1.02, 0.03, 0.08), loc=(0, -0.055, 0.45 + i * 0.60), mat=sd))
        for i in range(6):
            p.append(bolt(f"{name}_rivet{i}", (-0.42 + i * 0.17, -0.07, 1.90), r=0.011, mat=ch,
                          rot=(math.radians(90), 0, 0)))
    else:
        p.append(S.box(name + "_plate", (0.86, 0.04, 1.60), loc=(0, -0.08, 1.05), bevel=0.012, mat=M["hqm"]))
        for i in range(3):
            p.append(S.box(f"{name}_hqm_rib{i}", (0.88, 0.03, 0.06), loc=(0, -0.10, 0.55 + i * 0.50), mat=sd))
    # петли, ручка, замок
    for z in (0.35, 1.72):
        p.extend(hinge(f"{name}_hinge{int(z*100)}", -0.53, -thick * 0.4, z, sd))
    p.append(S.cylinder(name + "_handle", 0.022, 0.16, 14, loc=(0.34, -0.09, 1.02),
                        rot=(0, math.radians(90), 0), mat=ch))
    p.append(S.box(name + "_handle_base", (0.10, 0.04, 0.14), loc=(0.28, -0.07, 1.02), bevel=0.008, mat=sd))
    p.append(S.box(name + "_lock", (0.16, 0.06, 0.20), loc=(0.30, -0.06, 1.30), bevel=0.01, mat=sd))
    p.append(S.cylinder(name + "_keyhole", 0.022, 0.05, 12, loc=(0.30, -0.10, 1.30),
                        rot=(math.radians(90), 0, 0), mat=ch))
    p.append(S.box(name + "_plate_num", (0.12, 0.01, 0.06), loc=(-0.28, -0.08, 1.55), mat=M["paint_yellow"]))
    return p


def build_lock(M, code=True):
    """Замок: ключевой (скоба + скважина) или кодовый (клавиатура + дисплей)."""
    st, sd, ch = M["steel"], M["steel_dark"], M["chrome"]
    tag = "LK" if code else "KL"
    p = [S.box(tag + "_body", (0.20, 0.10, 0.26), loc=(0, 0, 0.13), bevel=0.012, mat=sd),
         S.box(tag + "_face", (0.17, 0.02, 0.22), loc=(0, -0.06, 0.13), mat=st)]
    if code:
        for r in range(3):
            for c in range(3):
                p.append(S.box(f"{tag}_key{r}{c}", (0.042, 0.014, 0.032),
                               loc=(-0.055 + c * 0.055, -0.075, 0.205 - r * 0.05), mat=M["plastic"]))
        p.append(S.box(tag + "_screen", (0.14, 0.012, 0.036), loc=(0, -0.075, 0.045), mat=M["screen"]))
        p.append(S.cylinder(tag + "_bat", 0.02, 0.05, 12, loc=(0.07, -0.07, 0.26),
                            rot=(math.radians(90), 0, 0), mat=ch))
    else:
        p.append(S.cylinder(tag + "_keyhole", 0.03, 0.05, 14, loc=(0, -0.07, 0.09),
                            rot=(math.radians(90), 0, 0), mat=ch))
        p.append(S.box(tag + "_key_slot", (0.02, 0.02, 0.05), loc=(0, -0.075, 0.09), mat=sd))
        p.append(S.cylinder(tag + "_cylinder", 0.055, 0.04, 16, loc=(0, -0.065, 0.09),
                            rot=(math.radians(90), 0, 0), mat=ch))
    p.append(S.cylinder(tag + "_bolt", 0.028, 0.10, 14, loc=(0, -0.12, 0.20), rot=(math.radians(90), 0, 0), mat=ch))
    p.append(S.box(tag + "_strap", (0.26, 0.03, 0.04), loc=(0, -0.05, 0.28), mat=st))
    p.append(S.box(tag + "_strap2", (0.26, 0.03, 0.04), loc=(0, -0.05, 0.02), mat=st))
    return p


def build_sign(M):
    """Табличка: рамка, лист с текстом, стрелка, крепления."""
    st, sd = M["steel"], M["steel_dark"]
    p = [S.box("SG_frame", (0.62, 0.05, 0.82), loc=(0, 0, 0.45), bevel=0.008, mat=sd),
         S.box("SG_sheet", (0.56, 0.02, 0.76), loc=(0, 0.035, 0.45), mat=M["paper"]),
         S.box("SG_line0", (0.44, 0.006, 0.06), loc=(0, 0.05, 0.70), mat=sd),
         S.box("SG_line1", (0.36, 0.006, 0.03), loc=(-0.04, 0.05, 0.58), mat=sd),
         S.box("SG_arrow", (0.30, 0.008, 0.05), loc=(-0.06, 0.05, 0.40), mat=M["paint_yellow"]),
         S.box("SG_arrow_head", (0.12, 0.008, 0.14), loc=(0.14, 0.05, 0.40), rot=(0, 0, math.radians(45)),
               mat=M["paint_yellow"]),
         S.box("SG_l0", (0.14, 0.02, 0.10), loc=(-0.18, 0.05, 0.14), mat=M["paint_red"]),
         S.box("SG_l1", (0.20, 0.02, 0.10), loc=(0.10, 0.05, 0.14), mat=M["paint_red"])]
    for i in range(4):
        x = -0.25 if i % 2 == 0 else 0.25
        z = 0.14 if i < 2 else 0.76
        p.append(bolt(f"SG_screw{i}", (x, -0.03, z), r=0.010, mat=st, rot=(math.radians(90), 0, 0)))
    p.append(S.box("SG_hanger", (0.06, 0.06, 0.10), loc=(0, -0.02, 0.90), mat=st))
    return p


def build_barricade(M, concrete=True):
    """Баррикада: бетонные плиты с арматурой или стальные ежи и листы."""
    st, sd = M["steel"], M["steel_dark"]
    p = []
    if concrete:
        for i in range(3):
            z = 0.14 + i * 0.26
            sx = -1 if i % 2 == 0 else 1
            p.append(S.box(f"BC_slab{i}", (1.50, 0.34, 0.24), loc=(sx * 0.05, 0, z), bevel=0.02, mat=M["concrete"]))
            p.append(S.box(f"BC_slab_edge{i}", (1.52, 0.36, 0.03), loc=(sx * 0.05, 0, z + 0.13), mat=M["concrete"]))
        for i in range(6):
            p.append(S.cylinder(f"BC_rebar{i}", 0.016, 0.50, 10, loc=(-0.55 + i * 0.22, -0.02, 0.92),
                                rot=(math.radians(12) * (1 if i % 2 else -1), 0, 0), mat=M["rust"]))
        p.append(S.box("BC_foot_l", (0.30, 0.60, 0.10), loc=(-0.62, 0, 0.05), bevel=0.01, mat=M["concrete"]))
        p.append(S.box("BC_foot_r", (0.30, 0.60, 0.10), loc=(0.62, 0, 0.05), bevel=0.01, mat=M["concrete"]))
    else:
        # Противотанковый «еж»: три балки-луча под 35° (низ касается земли), стяжка в центре,
        # колючая проволока, наклонный лист с рёбрами и предупреждающая полоса.
        cz, tilt, half = 0.49, math.radians(35), 0.85
        for k in range(3):
            az = math.radians(k * 120)
            p.append(S.box(f"BM_beam{k}", (1.70, 0.11, 0.11), loc=(0, 0, cz),
                           rot=(0, tilt, az), bevel=0.008, mat=st))
            # пятка луча + шип на верхнем конце
            p.append(S.box(f"BM_heel{k}", (0.16, 0.14, 0.14),
                           loc=(half * math.cos(tilt) * math.cos(az), half * math.cos(tilt) * math.sin(az), 0.07),
                           rot=(0, tilt, az), mat=sd))
            p.append(S.box(f"BM_tip{k}", (0.14, 0.09, 0.09),
                           loc=(-half * math.cos(tilt) * math.cos(az), -half * math.cos(tilt) * math.sin(az),
                                cz + half * math.sin(tilt) + 0.04),
                           rot=(0, tilt, az), mat=sd))
        # стяжка балок в центре
        p.append(S.cylinder("BM_clamp", 0.13, 0.16, 16, loc=(0, 0, cz), mat=sd))
        for k in range(3):
            a = math.radians(k * 120 + 60)
            p.append(S.cylinder(f"BM_clamp_bolt{k}", 0.022, 0.10, 12,
                                loc=(math.cos(a) * 0.10, math.sin(a) * 0.10, cz + 0.10), mat=M["chrome"]))
        # колючая проволока вокруг нижней части
        wire = []
        for k in range(13):
            a = k * math.pi * 2 / 12
            wire.append((math.cos(a) * 0.62, math.sin(a) * 0.62, 0.16 + 0.05 * (k % 2)))
        p.append(S.tube("BM_wire", wire, 0.009, mat=sd, resolution=4))
        for k in range(8):
            a = math.radians(k * 45)
            p.append(S.box(f"BM_barb{k}", (0.02, 0.02, 0.14),
                           loc=(math.cos(a) * 0.62, math.sin(a) * 0.62, 0.20), rot=(0, 0, a), mat=st))
        # наклонный лист с рёбрами и полосой
        p.append(S.box("BM_sheet", (1.30, 0.05, 0.78), loc=(0.30, -0.48, 0.40),
                       rot=(math.radians(18), 0, math.radians(-12)), bevel=0.008, mat=M["rust"]))
        for k in range(4):
            p.append(S.box(f"BM_sheet_rib{k}", (1.24, 0.03, 0.04), loc=(0.30, -0.50, 0.20 + k * 0.18),
                           rot=(math.radians(18), 0, math.radians(-12)), mat=sd))
        p.append(S.box("BM_hazard", (1.26, 0.02, 0.09), loc=(0.30, -0.52, 0.76),
                       rot=(math.radians(18), 0, math.radians(-12)), mat=M["paint_yellow"]))
    return p


def build_trap(M, bear=True):
    """Ловушки: доска с шипами (spikes) или капкан с дугами и пружинами (bear)."""
    st, sd, wd, ch = M["steel"], M["steel_dark"], M["wood"], M["chrome"]
    p = []
    if bear:
        # Капкан «взведён»: база, две полукруглые челюсти по земле зубьями вверх,
        # пружины на шарнирах, нажимная пластина с крестовиной, цепь с колом.
        p.append(S.cylinder("TB_plate", 0.26, 0.035, 24, loc=(0, 0, 0.02), mat=st))
        p.append(S.cylinder("TB_plate_rim", 0.27, 0.015, 24, loc=(0, 0, 0.045), mat=sd))
        for sx in (-1, 1):
            arc = []
            for k in range(9):
                a = math.pi * (k / 8.0)
                arc.append((0.30 * math.cos(a) * sx, sx * 0.30 * math.sin(a), 0.075))
            p.append(S.tube(f"TB_jaw{sx}", arc, 0.020, mat=st, resolution=5))
            for k in range(7):
                a = math.pi * (k / 6.0)
                p.append(S.cylinder(f"TB_tooth{sx}{k}", 0.010, 0.075, 8,
                                    loc=(0.29 * math.cos(a) * sx, sx * 0.29 * math.sin(a), 0.115), mat=sd))
            p.append(S.box(f"TB_hinge{sx}", (0.09, 0.09, 0.10), loc=(0.30 * sx, 0, 0.06), bevel=0.01, mat=sd))
            p.append(S.cylinder(f"TB_spring{sx}", 0.045, 0.07, 14, loc=(0.34 * sx, 0.02, 0.07),
                                rot=(0, math.radians(90), 0), mat=M["rust"]))
            p.append(S.cylinder(f"TB_spring2{sx}", 0.030, 0.10, 12, loc=(0.34 * sx, 0.02, 0.07),
                                rot=(0, math.radians(90), 0), mat=ch))
        p.append(S.cylinder("TB_pan", 0.10, 0.02, 18, loc=(0, 0, 0.055), mat=ch))
        p.append(S.box("TB_pan_bar", (0.22, 0.03, 0.012), loc=(0, 0, 0.065), mat=sd))
        p.append(S.box("TB_pan_bar2", (0.03, 0.22, 0.012), loc=(0, 0, 0.065), mat=sd))
        p.append(S.tube("TB_link", [(0.0, 0.02, 0.08), (0.16, 0.14, 0.09), (0.30, 0.16, 0.10)], 0.008,
                        mat=st, resolution=4))
        p.append(S.tube("TB_chain", [(0.30, 0.16, 0.08), (0.52, 0.30, 0.05), (0.70, 0.40, 0.03)], 0.012,
                        mat=sd, resolution=5))
        p.append(S.cylinder("TB_stake", 0.030, 0.26, 12, loc=(0.72, 0.42, 0.13),
                            rot=(math.radians(70), 0, 0), mat=st))
        p.append(S.cylinder("TB_stake_head", 0.055, 0.04, 14, loc=(0.76, 0.50, 0.24),
                            rot=(math.radians(70), 0, 0), mat=sd))
    else:
        p.append(S.box("TS_board", (1.10, 1.10, 0.08), loc=(0, 0, 0.04), bevel=0.01, mat=wd))
        for i in range(2):
            p.append(S.box(f"TS_brace{i}", (1.10, 0.10, 0.06), loc=(0, -0.44 + i * 0.88, 0.10), mat=M["wood_dark"]))
        for r in range(5):
            for c in range(5):
                if (r + c) % 2: continue
                x, y = -0.40 + c * 0.20, -0.40 + r * 0.20
                p.append(S.cylinder(f"TS_spike{r}{c}", 0.020, 0.26, 10, loc=(x, y, 0.21), mat=st))
                p.append(S.cylinder(f"TS_spike_tip{r}{c}", 0.010, 0.10, 8, loc=(x, y, 0.38), mat=sd))
        p.append(S.box("TS_edge_l", (0.06, 1.12, 0.10), loc=(-0.52, 0, 0.07), mat=sd))
        p.append(S.box("TS_edge_r", (0.06, 1.12, 0.10), loc=(0.52, 0, 0.07), mat=sd))
        p.append(S.box("TS_handle", (0.30, 0.06, 0.04), loc=(0, 0.48, 0.12), mat=sd))
    return p


# ========================================================================== сборка
MODELS = [
    # (имя, билдер, FOV камеры, азимут камеры)
    ("DD_workbench", lambda M: build_workbench(M), 36, 118.0),
    ("DD_repair_bench", lambda M: build_repair_bench(M), 36, 118.0),
    ("DD_research_table", lambda M: build_research_table(M), 36, 118.0),
    ("DD_furnace", lambda M: build_furnace(M), 36, 118.0),
    ("DD_furnace_large", lambda M: build_furnace_large(M), 38, 118.0),
    ("DD_cupboard", lambda M: build_cupboard(M), 36, 118.0),
    ("DD_sleepingbag", lambda M: build_sleepingbag(M), 40, 118.0),
    ("DD_bed", lambda M: build_bed(M), 36, 118.0),
    ("DD_purifier", lambda M: build_purifier(M), 36, 118.0),
    ("DD_autoturret", lambda M: build_autoturret(M), 36, 118.0),
    ("DD_samsite", lambda M: build_samsite(M), 36, 118.0),
    ("DD_wind_generator", lambda M: build_wind_generator(M), 38, 118.0),
    ("DD_door_wood", lambda M: build_door(M, "wood"), 34, 118.0),
    ("DD_door_metal", lambda M: build_door(M, "metal"), 34, 118.0),
    ("DD_door_armored", lambda M: build_door(M, "armored"), 34, 118.0),
    ("DD_lock_code", lambda M: build_lock(M, True), 34, 118.0),
    ("DD_lock_key", lambda M: build_lock(M, False), 34, 118.0),
    ("DD_sign", lambda M: build_sign(M), 34, 118.0),
    ("DD_barricade_concrete", lambda M: build_barricade(M, True), 36, 118.0),
    ("DD_barricade_metal", lambda M: build_barricade(M, False), 36, 118.0),
    ("DD_trap_spikes", lambda M: build_trap(M, False), 38, 118.0),
    ("DD_trap_bear", lambda M: build_trap(M, True), 38, 118.0),
]


def build_all(render=True, only=None):
    S.ensure_dir(OUT)
    total = 0
    jobs = [m for m in MODELS if only is None or m[0] == only]
    for name, fn, fov, az in jobs:
        S.clean_scene()
        M = materials()
        parts = fn(M)
        flat = []
        for it in parts:
            if isinstance(it, (list, tuple)):
                flat.extend(it)
            else:
                flat.append(it)
        parts = flat
        for o in parts:
            if o and getattr(o, "type", None) == 'CURVE':
                S.curve_to_mesh(o)
        for o in parts:
            S.smart_uv(o)
        joined = S.join_objects(parts, name)
        if joined is None:
            print(f"[deploy] {name}: ОШИБКА сборки")
            continue
        S.shade_smooth(joined, 32)
        tris = S.tri_count(joined)
        total += tris
        S.export_fbx([joined], os.path.join(OUT, name + ".fbx"))
        S.export_glb([joined], os.path.join(OUT, name + ".glb"))
        print(f"[deploy] {name}: {tris} трис, материалов: {len(joined.data.materials)}")
        if render:
            # мелочь (замки) — дальше и узким объективом, иначе кадр заполняет фон студии
            small = name.startswith("DD_lock")
            S.render_fit(os.path.join(PREVIEW, name + ".png"), [joined], samples=44, res=(900, 700),
                         fov_deg=14 if small else fov,
                         azimuth=(az - 180.0) % 360.0 if small else az,   # замок: лицом к камере
                         min_dist=2.6 if small else 1.5)
    print(f"[deploy] ИТОГО трис: {total}")


if __name__ == "__main__":
    only = None
    if "--only" in sys.argv:
        k = sys.argv.index("--only")
        if k + 1 < len(sys.argv):
            only = sys.argv[k + 1]
    build_all(render="--no-render" not in sys.argv, only=only)
