"""
SUBSISTENCE — models_loot.py  (УЛЬТРА-ДЕТАЛИЗАЦИЯ)
Контейнеры лута под три уровня (решение 15_loot × 10_tiers):
  PR_supply_crate       — армейский ящик Tier2: зелёный металл, рёбра крышки, защёлки, трафарет;
  PR_safe_box           — сейф Tier3: толстая дверца, колесо-запор, клавиатура и экран, петли;
  PR_barrel             — бочка: гофрированные рёбра, обручи, пробка, hazard-табличка, ржавчина;
  PR_barrel_radioactive — та же бочка с трилистником и светящейся маркировкой (станция);
  PR_toolbox            — ящик для инструментов: ручка, защёлки, лоток, торчащие ключи, наклейки;
  PR_medical_cabinet    — медшкаф: белый корпус, красный крест, стеклянная дверца, полки с ампулами;
  PR_airdrop_crate      — ящик айрдропа: оранжевый корпус, чёрные углы, стропы парашюта, маяк.
Масштаб — метры, низ модели на z=0, фасад — в +Y. Экспорт: FBX + GLB → Models/Props/.
"""
import sys, os, math
import bpy
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import subs_common as S

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.abspath(os.path.join(HERE, "..", "..", "UnityProject", "Assets", "Subsistence", "Models", "Props"))
PREVIEW = os.path.abspath(os.path.join(HERE, "..", "..", "docs", "previews"))


def materials():
    return dict(
        steel=S.pbr_material("M_LO_Steel", (0.42, 0.43, 0.46, 1), 0.86, 0.36, noise_scale=65, bump=0.35),
        steel_dark=S.pbr_material("M_LO_SteelDark", (0.15, 0.155, 0.17, 1), 0.80, 0.46, noise_scale=95, bump=0.45),
        chrome=S.pbr_material("M_LO_Chrome", (0.75, 0.76, 0.78, 1), 0.97, 0.12),
        rust=S.pbr_material("M_LO_Rust", (0.33, 0.20, 0.12, 1), 0.45, 0.76, noise_scale=115, bump=0.65),
        army=S.pbr_material("M_LO_ArmyGreen", (0.16, 0.25, 0.15, 1), 0.20, 0.55, noise_scale=80, bump=0.45),
        army_dark=S.pbr_material("M_LO_ArmyDark", (0.09, 0.14, 0.09, 1), 0.18, 0.60, noise_scale=95, bump=0.4),
        orange=S.pbr_material("M_LO_Orange", (0.72, 0.30, 0.05, 1), 0.10, 0.48, noise_scale=85, bump=0.4),
        yellow=S.pbr_material("M_LO_Yellow", (0.86, 0.70, 0.10, 1), 0.08, 0.44, noise_scale=80, bump=0.4),
        red=S.pbr_material("M_LO_Red", (0.55, 0.09, 0.08, 1), 0.10, 0.46, noise_scale=85, bump=0.4),
        white=S.pbr_material("M_LO_White", (0.80, 0.81, 0.80, 1), 0.05, 0.42, noise_scale=70, bump=0.3),
        white_dirty=S.pbr_material("M_LO_WhiteDirty", (0.60, 0.60, 0.57, 1), 0.05, 0.60, noise_scale=110, bump=0.45),
        wood=S.pbr_material("M_LO_Wood", (0.35, 0.22, 0.12, 1), 0.0, 0.66, noise_scale=95, bump=0.6),
        rubber=S.pbr_material("M_LO_Rubber", (0.045, 0.045, 0.05, 1), 0.0, 0.80, noise_scale=240, bump=0.6),
        strap=S.pbr_material("M_LO_Webbing", (0.13, 0.12, 0.09, 1), 0.0, 0.80, noise_scale=160, bump=0.35),
        plastic=S.pbr_material("M_LO_Plastic", (0.10, 0.11, 0.12, 1), 0.0, 0.50, noise_scale=200, bump=0.4),
        glass=S.pbr_material("M_LO_Glass", (0.66, 0.74, 0.78, 1), 0.0, 0.05, alpha=0.07),
        paper=S.pbr_material("M_LO_Paper", (0.80, 0.78, 0.70, 1), 0.0, 0.78),
        lamp=S.pbr_material("M_LO_Lamp", (1.0, 0.55, 0.18, 1), 0.0, 0.30,
                            emission=(1.0, 0.45, 0.12, 1), emission_strength=9.0),
        screen=S.pbr_material("M_LO_Screen", (0.10, 0.30, 0.22, 1), 0.0, 0.30,
                              emission=(0.30, 1.0, 0.60, 1), emission_strength=5.0),
        glow=S.pbr_material("M_LO_Glow", (0.75, 0.72, 0.15, 1), 0.0, 0.40,
                            emission=(0.95, 0.92, 0.20, 1), emission_strength=6.0),
    )


# ================================================================= армейский ящик
def build_supply_crate(M):
    st, sd, gr, gd = M["steel"], M["steel_dark"], M["army"], M["army_dark"]
    p = [S.box("SC_body", (1.10, 0.55, 0.46), loc=(0, 0, 0.28), bevel=0.015, mat=gr),
         S.box("SC_floor", (1.12, 0.57, 0.05), loc=(0, 0, 0.05), mat=gd),
         S.box("SC_lid", (1.14, 0.59, 0.07), loc=(0, 0, 0.545), bevel=0.02, mat=gr)]
    # рёбра крышки и корпуса
    for i in range(3):
        p.append(S.box(f"SC_lid_rib{i}", (0.06, 0.60, 0.04), loc=(-0.35 + i * 0.35, 0, 0.585), mat=gd))
    for sx in (-1, 1):
        for sy in (-1, 1):
            p.append(S.box(f"SC_post{sx}{sy}", (0.07, 0.07, 0.52), loc=(sx * 0.53, sy * 0.26, 0.28), mat=gd))
            p.append(S.box(f"SC_foot{sx}{sy}", (0.11, 0.11, 0.04), loc=(sx * 0.50, sy * 0.24, 0.02), mat=sd))
    for sy in (-1, 1):
        p.append(S.box(f"SC_band{sy}", (1.14, 0.04, 0.05), loc=(0, sy * 0.285, 0.30), mat=gd))
    # защёлки с ручками и петли
    for sx in (-1, 1):
        p.append(S.box(f"SC_latch{sx}", (0.16, 0.05, 0.10), loc=(sx * 0.30, 0.30, 0.44), bevel=0.008, mat=st))
        p.append(S.box(f"SC_latch_clip{sx}", (0.09, 0.08, 0.05), loc=(sx * 0.30, 0.33, 0.50), mat=M["chrome"]))
        p.append(S.cylinder(f"SC_hinge{sx}", 0.03, 0.14, 14, loc=(sx * 0.30, -0.30, 0.53),
                            rot=(0, math.radians(90), 0), mat=st))
        p.append(S.box(f"SC_handle{sx}", (0.05, 0.14, 0.10), loc=(sx * 0.57, 0, 0.32), bevel=0.01, mat=sd))
    # трафарет, полоса и бирка
    p.append(S.box("SC_stencil", (0.42, 0.01, 0.16), loc=(-0.20, 0.281, 0.34), mat=M["paper"]))
    p.append(S.box("SC_stencil_line", (0.36, 0.01, 0.02), loc=(-0.20, 0.288, 0.34), mat=gd))
    p.append(S.box("SC_hazard", (1.06, 0.01, 0.05), loc=(0, 0.281, 0.16), mat=M["yellow"]))
    p.append(S.box("SC_plate", (0.22, 0.01, 0.09), loc=(0.34, 0.281, 0.24), mat=st))
    for i in range(6):
        p.append(S.cylinder(f"SC_rivet{i}", 0.011, 0.02, 10,
                            loc=(-0.45 + i * 0.18, 0.286, 0.50), rot=(math.radians(90), 0, 0), mat=M["chrome"]))
    return p


# ========================================================================== сейф
def build_safe(M):
    st, sd, ch = M["steel"], M["steel_dark"], M["chrome"]
    p = [S.box("SF_body", (0.78, 0.66, 0.78), loc=(0, 0, 0.42), bevel=0.02, mat=sd),
         S.box("SF_top", (0.82, 0.70, 0.05), loc=(0, 0, 0.83), mat=st),
         S.box("SF_plinth", (0.84, 0.72, 0.06), loc=(0, 0, 0.03), mat=sd)]
    # рамка дверцы и сама дверца
    p.append(S.box("SF_frame", (0.74, 0.04, 0.74), loc=(0, 0.35, 0.46), mat=st))
    p.append(S.box("SF_door", (0.66, 0.07, 0.66), loc=(0, 0.385, 0.46), bevel=0.015, mat=st))
    p.append(S.box("SF_door_inset", (0.52, 0.02, 0.36), loc=(-0.04, 0.418, 0.56), mat=sd))
    p.append(S.box("SF_door_edge_t", (0.68, 0.03, 0.03), loc=(0, 0.414, 0.80), mat=ch))
    p.append(S.box("SF_door_edge_b", (0.68, 0.03, 0.03), loc=(0, 0.414, 0.12), mat=ch))
    # колесо-запор: диск, три спицы, ступица
    p.append(S.cylinder("SF_wheel", 0.15, 0.04, 24, loc=(0, 0.45, 0.46), rot=(math.radians(90), 0, 0), mat=ch))
    for i in range(3):
        a = i * math.pi * 2 / 3
        p.append(S.box(f"SF_spoke{i}", (0.05, 0.03, 0.26), loc=(math.sin(a) * 0.0, 0.455, 0.46),
                       rot=(0, a, 0), mat=ch))
    p.append(S.cylinder("SF_hub", 0.055, 0.07, 16, loc=(0, 0.47, 0.46), rot=(math.radians(90), 0, 0), mat=st))
    # клавиатура с экраном и ключевина
    p.append(S.box("SF_code", (0.20, 0.05, 0.26), loc=(0.22, 0.40, 0.62), bevel=0.01, mat=sd))
    for r in range(3):
        for c in range(3):
            p.append(S.box(f"SF_key{r}{c}", (0.044, 0.016, 0.034),
                           loc=(0.17 + c * 0.05, 0.365, 0.69 - r * 0.045), mat=M["plastic"]))
            p.append(S.box(f"SF_key_face{r}{c}", (0.030, 0.006, 0.020),
                           loc=(0.17 + c * 0.05, 0.358, 0.69 - r * 0.045), mat=M["white"]))
    p.append(S.box("SF_screen", (0.14, 0.012, 0.036), loc=(0.22, 0.36, 0.75), mat=M["screen"]))
    p.append(S.cylinder("SF_keyhole", 0.028, 0.04, 14, loc=(-0.20, 0.40, 0.30),
                        rot=(math.radians(90), 0, 0), mat=ch))
    # петли, болты и табличка
    for z in (0.24, 0.68):
        p.append(S.box(f"SF_hinge{int(z*100)}", (0.10, 0.12, 0.12), loc=(-0.37, 0.40, z), bevel=0.01, mat=st))
        p.append(S.cylinder(f"SF_hinge_pin{int(z*100)}", 0.018, 0.26, 12, loc=(-0.40, 0.40, z), mat=ch))
    for i in range(4):
        p.append(S.cylinder(f"SF_bolt{i}", 0.014, 0.03, 12,
                            loc=(-0.30 + i * 0.20, 0.44, 0.14), rot=(math.radians(90), 0, 0), mat=ch))
    p.append(S.box("SF_plate", (0.26, 0.01, 0.10), loc=(-0.20, 0.44, 0.72), mat=M["paper"]))
    return p


# ========================================================================== бочка
def build_barrel(M, radioactive=False):
    st, sd, rt = M["steel"], M["steel_dark"], M["rust"]
    body_mat = M["yellow"] if radioactive else rt
    p = [S.cylinder("BR_body", 0.30, 0.86, 28, loc=(0, 0, 0.44), mat=body_mat)]
    # гофра: три обруча + рёбра
    for i, z in enumerate((0.26, 0.45, 0.64)):
        p.append(S.cylinder(f"BR_rib{i}", 0.315, 0.055, 28, loc=(0, 0, z), mat=body_mat))
        p.append(S.cylinder(f"BR_ring{i}", 0.318, 0.02, 28, loc=(0, 0, z + 0.035), mat=sd))
    p.append(S.cylinder("BR_rim_bot", 0.305, 0.05, 28, loc=(0, 0, 0.045), mat=sd))
    p.append(S.cylinder("BR_rim_top", 0.305, 0.05, 28, loc=(0, 0, 0.845), mat=sd))
    # крышка с пробкой и вентилем
    p.append(S.cylinder("BR_lid", 0.275, 0.03, 28, loc=(0, 0, 0.875), mat=sd))
    p.append(S.cylinder("BR_bung", 0.055, 0.045, 16, loc=(0.16, 0.10, 0.895), mat=M["steel"]))
    p.append(S.cylinder("BR_bung_ring", 0.07, 0.02, 16, loc=(0.16, 0.10, 0.90), mat=st))
    p.append(S.cylinder("BR_valve", 0.028, 0.10, 12, loc=(-0.14, -0.06, 0.92), mat=M["chrome"]))
    p.append(S.cylinder("BR_valve_top", 0.055, 0.03, 14, loc=(-0.14, -0.06, 0.98), mat=M["red"]))
    # маркировка и ржавые потёки
    if radioactive:
        p.append(S.cylinder("BR_trefoil", 0.13, 0.01, 24, loc=(0, 0.303, 0.60),
                            rot=(math.radians(90), 0, 0), mat=M["glow"]))
        for i in range(3):
            a = math.radians(i * 120 - 90)
            p.append(S.box(f"BR_trefoil_blade{i}", (0.10, 0.012, 0.05),
                           loc=(math.cos(a) * 0.055, 0.312, 0.60 + math.sin(a) * 0.055),
                           rot=(0, 0, a), mat=sd))
        p.append(S.box("BR_sign", (0.18, 0.01, 0.12), loc=(0.0, 0.307, 0.30), mat=M["yellow"]))
        p.append(S.box("BR_sign_line", (0.14, 0.008, 0.02), loc=(0, 0.313, 0.30), mat=sd))
    else:
        p.append(S.box("BR_label", (0.22, 0.01, 0.14), loc=(-0.02, 0.303, 0.62), mat=M["paper"]))
        for i in range(3):
            p.append(S.box(f"BR_label_line{i}", (0.16, 0.008, 0.015), loc=(-0.02, 0.309, 0.66 - i * 0.04),
                           mat=sd))
        p.append(S.box("BR_hazard", (0.16, 0.01, 0.05), loc=(0.10, 0.303, 0.38), mat=M["yellow"]))
    for i in range(5):
        a = math.radians(i * 72 + 18)
        p.append(S.box(f"BR_stain{i}", (0.035, 0.006, 0.20 + 0.05 * (i % 3)),
                       loc=(math.cos(a) * 0.299, math.sin(a) * 0.299, 0.30 - 0.05 * (i % 3)),
                       rot=(0, 0, a), mat=rt))
    for i in range(4):
        a = math.radians(i * 90 + 45)
        p.append(S.box(f"BR_dent{i}", (0.10, 0.02, 0.10),
                       loc=(math.cos(a) * 0.29, math.sin(a) * 0.29, 0.52), rot=(0, 0, a), mat=sd))
    return p


# ====================================================================== тулбокс
def build_toolbox(M):
    st, sd, ch = M["steel"], M["steel_dark"], M["chrome"]
    p = [S.box("TB_body", (0.56, 0.28, 0.18), loc=(0, 0, 0.12), bevel=0.012, mat=M["red"]),
         # крышка откинута назад (петли на -Y): видно лоток и инструмент
         S.box("TB_lid", (0.58, 0.30, 0.05), loc=(0, -0.05, 0.30),
               rot=(math.radians(-116), 0, 0), bevel=0.02, mat=M["red"]),
         S.box("TB_base", (0.58, 0.30, 0.03), loc=(0, 0, 0.015), mat=sd)]
    for sy in (-1, 1):
        p.append(S.box(f"TB_band{sy}", (0.58, 0.03, 0.19), loc=(0, sy * 0.145, 0.12), mat=sd))
    # ручка-дуга
    arc = []
    for i in range(7):
        a = math.pi * i / 6.0
        arc.append((-0.11 * math.cos(a), 0.0, 0.26 + 0.10 * math.sin(a)))
    p.append(S.tube("TB_handle", arc, 0.014, mat=sd, resolution=5))
    p.append(S.cylinder("TB_handle_grip", 0.018, 0.09, 12, loc=(0, 0, 0.35), rot=(0, math.radians(90), 0),
                        mat=M["rubber"]))
    # защёлки и петли
    for sx in (-1, 1):
        p.append(S.box(f"TB_latch{sx}", (0.09, 0.03, 0.09), loc=(sx * 0.20, 0.15, 0.20), bevel=0.006, mat=ch))
        p.append(S.cylinder(f"TB_hinge{sx}", 0.016, 0.10, 12, loc=(sx * 0.20, -0.15, 0.22),
                            rot=(0, math.radians(90), 0), mat=ch))
    # лоток с инструментом внутри (крышка приоткрыта → видно)
    p.append(S.box("TB_tray", (0.46, 0.20, 0.03), loc=(0, 0, 0.19), mat=st))
    p.append(S.box("TB_tray_hole", (0.10, 0.08, 0.02), loc=(0.12, 0, 0.195), mat=sd))
    p.append(S.box("TB_wrench", (0.045, 0.19, 0.025), loc=(-0.17, -0.01, 0.22), rot=(0, 0, math.radians(8)),
                   mat=ch))
    p.append(S.box("TB_wrench_jaw", (0.06, 0.05, 0.03), loc=(-0.19, -0.10, 0.22), mat=ch))
    p.append(S.box("TB_wrench2", (0.04, 0.16, 0.025), loc=(-0.08, 0.02, 0.22), mat=ch))
    for i in range(3):
        p.append(S.cylinder(f"TB_screwdriver{i}", 0.011, 0.16, 10, loc=(-0.02 + i * 0.07, -0.02, 0.235),
                            rot=(math.radians(90), 0, 0), mat=M["yellow"]))
        p.append(S.cylinder(f"TB_screwdriver_h{i}", 0.015, 0.06, 10, loc=(-0.02 + i * 0.07, 0.08, 0.235),
                            rot=(math.radians(90), 0, 0), mat=sd))
    p.append(S.box("TB_hammer_head", (0.09, 0.05, 0.05), loc=(0.17, -0.06, 0.22), mat=st))
    p.append(S.cylinder("TB_hammer_h", 0.013, 0.20, 10, loc=(0.17, 0.04, 0.22), rot=(math.radians(90), 0, 0),
                        mat=M["wood"]))
    # наклейки и потёртости
    p.append(S.box("TB_sticker", (0.14, 0.01, 0.08), loc=(0.16, 0.143, 0.14), mat=M["yellow"]))
    p.append(S.box("TB_sticker2", (0.09, 0.01, 0.06), loc=(-0.16, -0.143, 0.10), mat=M["paper"]))
    for i in range(4):
        p.append(S.box(f"TB_scratch{i}", (0.10 + 0.04 * i, 0.008, 0.012),
                       loc=(-0.14 + 0.06 * i, 0.146, 0.06 + 0.03 * i), mat=sd))
    return p


# ====================================================================== медшкаф
def build_medical(M):
    wh, wd, ch, gl = M["white"], M["white_dirty"], M["chrome"], M["glass"]
    p = [S.box("MC_back", (0.56, 0.04, 0.86), loc=(0, -0.16, 0.46), mat=wd),
         S.box("MC_side_l", (0.04, 0.32, 0.86), loc=(-0.28, 0, 0.46), mat=wh),
         S.box("MC_side_r", (0.04, 0.32, 0.86), loc=(0.28, 0, 0.46), mat=wh),
         S.box("MC_top", (0.60, 0.36, 0.05), loc=(0, 0, 0.905), mat=wh),
         S.box("MC_base", (0.60, 0.36, 0.06), loc=(0, 0, 0.03), mat=wd),
         S.box("MC_back_panel", (0.02, 0.30, 0.80), loc=(0, 0.0, 0.46), mat=wd)]
    # полки и наполнение: ампулы, бинты, бутылки, инструмент
    for i, z in enumerate((0.22, 0.48, 0.74)):
        p.append(S.box(f"MC_shelf{i}", (0.50, 0.30, 0.02), loc=(0, 0.02, z), mat=wh))
    for i in range(4):
        p.append(S.cylinder(f"MC_vial{i}", 0.018, 0.09, 10, loc=(-0.18 + i * 0.045, 0.02, 0.53), mat=gl))
        p.append(S.cylinder(f"MC_vial_cap{i}", 0.021, 0.015, 10, loc=(-0.18 + i * 0.045, 0.02, 0.58),
                            mat=M["red"]))
    for i in range(3):
        p.append(S.cylinder(f"MC_bandage{i}", 0.035, 0.11, 14, loc=(-0.14 + i * 0.09, 0.12, 0.28),
                            rot=(0, math.radians(90), 0), mat=M["paper"]))
    for i in range(3):
        p.append(S.cylinder(f"MC_bottle{i}", 0.035, 0.13, 14, loc=(-0.12 + i * 0.10, 0.06, 0.78), mat=gl))
        p.append(S.cylinder(f"MC_bottle_cap{i}", 0.024, 0.03, 12, loc=(-0.12 + i * 0.10, 0.06, 0.86),
                            mat=wd))
    p.append(S.box("MC_tray", (0.22, 0.16, 0.02), loc=(0.12, 0.04, 0.49), mat=ch))
    p.append(S.box("MC_scalpel", (0.02, 0.12, 0.01), loc=(0.10, 0.04, 0.51), mat=ch))
    p.append(S.box("MC_syringe", (0.02, 0.10, 0.02), loc=(0.16, 0.04, 0.51), mat=M["plastic"]))
    # красный крест на дверце и стекло
    p.append(S.box("MC_door_frame", (0.56, 0.03, 0.86), loc=(0, 0.17, 0.46), mat=wh))
    p.append(S.box("MC_glass", (0.46, 0.02, 0.60), loc=(0, 0.185, 0.50), mat=gl))
    p.append(S.box("MC_glass_frame_t", (0.50, 0.03, 0.05), loc=(0, 0.18, 0.82), mat=wh))
    p.append(S.box("MC_glass_frame_b", (0.50, 0.03, 0.05), loc=(0, 0.18, 0.18), mat=wh))
    p.append(S.box("MC_cross_h", (0.20, 0.01, 0.06), loc=(0, 0.196, 0.10), mat=M["red"]))
    p.append(S.box("MC_cross_v", (0.06, 0.01, 0.20), loc=(0, 0.196, 0.10), mat=M["red"]))
    p.append(S.box("MC_handle", (0.05, 0.04, 0.16), loc=(0.22, 0.19, 0.50), bevel=0.008, mat=ch))
    for z in (0.20, 0.72):
        p.append(S.cylinder(f"MC_hinge{int(z*100)}", 0.014, 0.12, 12, loc=(-0.28, 0.18, z), mat=ch))
    p.append(S.box("MC_note", (0.14, 0.01, 0.10), loc=(-0.16, 0.196, 0.88), mat=M["paper"]))
    p.append(S.box("MC_vent", (0.12, 0.02, 0.06), loc=(0.16, 0.196, 0.88), mat=wd))
    return p


# =================================================================== ящик айрдропа
def build_airdrop(M):
    orn, sd, st, ch = M["orange"], M["steel_dark"], M["steel"], M["chrome"]
    p = [S.box("AD_body", (1.00, 0.62, 0.56), loc=(0, 0, 0.34), bevel=0.02, mat=orn),
         S.box("AD_base", (1.04, 0.66, 0.06), loc=(0, 0, 0.04), mat=sd)]
    # чёрные углы и рёбра
    for sx in (-1, 1):
        for sy in (-1, 1):
            p.append(S.box(f"AD_corner{sx}{sy}", (0.09, 0.09, 0.60), loc=(sx * 0.48, sy * 0.29, 0.34), mat=sd))
    for i in range(3):
        p.append(S.box(f"AD_rib{i}", (0.05, 0.64, 0.06), loc=(-0.30 + i * 0.30, 0, 0.60), mat=sd))
    for sx in (-1, 1):
        p.append(S.box(f"AD_band{sx}", (0.05, 0.66, 0.60), loc=(sx * 0.32, 0, 0.34), mat=sd))
    # крышка с петлями и защёлками
    p.append(S.box("AD_lid", (1.02, 0.64, 0.06), loc=(0, 0, 0.65), bevel=0.015, mat=M["yellow"]))
    for sx in (-1, 1):
        p.append(S.cylinder(f"AD_hinge{sx}", 0.028, 0.14, 12, loc=(sx * 0.28, -0.33, 0.64),
                            rot=(0, math.radians(90), 0), mat=st))
        p.append(S.box(f"AD_latch{sx}", (0.14, 0.05, 0.12), loc=(sx * 0.28, 0.33, 0.54), bevel=0.008, mat=st))
        p.append(S.box(f"AD_latch_clip{sx}", (0.08, 0.06, 0.05), loc=(sx * 0.28, 0.36, 0.62), mat=ch))
    # стропы парашюта: две дуги через верх + рым-кольца
    for sy in (-1, 1):
        s_pts = [(-0.34, sy * 0.34, 0.06), (-0.36, sy * 0.30, 0.66),
                 (0.0, sy * 0.26, 0.76), (0.36, sy * 0.30, 0.66), (0.34, sy * 0.34, 0.06)]
        p.append(S.tube(f"AD_strap{sy}", s_pts, 0.022, mat=M["strap"], resolution=5))
    p.append(S.cylinder("AD_ring", 0.06, 0.03, 16, loc=(0, 0, 0.78), mat=ch))
    p.append(S.cylinder("AD_ring_post", 0.03, 0.08, 12, loc=(0, 0, 0.72), mat=st))
    # маяк с лампой и антенной
    p.append(S.cylinder("AD_beacon_base", 0.055, 0.06, 14, loc=(0.34, 0.20, 0.71), mat=sd))
    p.append(S.cylinder("AD_beacon_lens", 0.05, 0.10, 14, loc=(0.34, 0.20, 0.79), mat=M["lamp"]))
    p.append(S.cylinder("AD_beacon_cap", 0.058, 0.03, 14, loc=(0.34, 0.20, 0.85), mat=st))
    p.append(S.tube("AD_antenna", [(0.34, 0.20, 0.86), (0.36, 0.21, 1.06)], 0.008, mat=sd, resolution=4))
    # маркировка: полосы, трафарет, номер
    for i in range(4):
        p.append(S.box(f"AD_hazard{i}", (0.10, 0.01, 0.44), loc=(-0.42 + i * 0.28, 0.312, 0.34), mat=sd))
    p.append(S.box("AD_stencil", (0.44, 0.01, 0.18), loc=(-0.16, 0.312, 0.30), mat=M["paper"]))
    p.append(S.box("AD_stencil_line", (0.30, 0.012, 0.03), loc=(-0.16, 0.318, 0.30), mat=sd))
    for i in range(6):
        p.append(S.cylinder(f"AD_rivet{i}", 0.012, 0.02, 10, loc=(-0.40 + i * 0.16, 0.316, 0.58),
                            rot=(math.radians(90), 0, 0), mat=ch))
    return p


MODELS = [
    ("PR_supply_crate", lambda M: build_supply_crate(M), 36, 118.0),
    ("PR_safe_box", lambda M: build_safe(M), 34, 118.0),
    ("PR_barrel", lambda M: build_barrel(M, False), 34, 118.0),
    ("PR_barrel_radioactive", lambda M: build_barrel(M, True), 34, 118.0),
    ("PR_toolbox", lambda M: build_toolbox(M), 34, 118.0),
    ("PR_medical_cabinet", lambda M: build_medical(M), 34, 118.0),
    ("PR_airdrop_crate", lambda M: build_airdrop(M), 36, 118.0),
]


def build_all(render=True, only=None):
    S.ensure_dir(OUT)
    total = 0
    for name, fn, fov, az in MODELS:
        if only and name != only:
            continue
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
            print(f"[loot] {name}: ОШИБКА сборки")
            continue
        S.shade_smooth(joined, 32)
        tris = S.tri_count(joined)
        total += tris
        S.export_fbx([joined], os.path.join(OUT, name + ".fbx"))
        S.export_glb([joined], os.path.join(OUT, name + ".glb"))
        print(f"[loot] {name}: {tris} трис, материалов: {len(joined.data.materials)}")
        if render:
            S.render_fit(os.path.join(PREVIEW, name + ".png"), [joined], samples=44, res=(900, 700),
                         fov_deg=fov, azimuth=az, min_dist=1.5)
    print(f"[loot] ИТОГО трис: {total}")


if __name__ == "__main__":
    only = None
    if "--only" in sys.argv:
        k = sys.argv.index("--only")
        if k + 1 < len(sys.argv):
            only = sys.argv[k + 1]
    build_all(render="--no-render" not in sys.argv, only=only)
