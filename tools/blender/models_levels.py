"""
SUBSISTENCE — models_levels.py
Уникальный декор КАЖДОГО уровня (решение: «у каждого уровня всё своё»).

  L0 «Жёлтые коридоры» : панель с обоями, лампа дневного света, офисный стул,
                         картотека, кулер, коробки, швабра с ведром, табличка EXIT, вентилятор.
  L37 «Бассейны»       : лестница в бассейн, спасательный круг, лежак, насос с фильтром,
                         труба с вентилем, душевая лейка, знак «мокрый пол», плитка, надувной круг.
  L3 «Электростанция»  : корпус турбины, щит управления, трансформатор, шкаф рубильников,
                         труба с фланцем, колесо-вентиль, бак охлаждения, кабельный барабан, знак.

Плюс ДИОРАМЫ — три «как это будет выглядеть» комнаты (пол/стена/потолок, свет под
палитру уровня, расставленные вещи) → docs/previews/level_<имя>.png.

Запуск:  python3 tools/bpy_run.py tools/blender/models_levels.py [--only ИМЯ …] [--no-render]
         python3 tools/bpy_run.py tools/blender/models_levels.py --diorama
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


# =========================== ПАЛИТРЫ УРОВНЕЙ ===========================

def palette(level):
    """Своя палитра материалов на каждый уровень — вещи не повторяются между уровнями."""
    if level == "corridors":
        return dict(
            wallpaper=S.pbr_material("M_Wallpaper", (0.78, 0.66, 0.30, 1), 0.0, 0.78, noise_scale=45, bump=0.30),
            wall=S.pbr_material("M_Drywall", (0.72, 0.64, 0.44, 1), 0.0, 0.85, noise_scale=30, bump=0.45),
            carpet=S.pbr_material("M_DampCarpet", (0.36, 0.31, 0.21, 1), 0.0, 0.95, noise_scale=70, bump=0.60),
            ceiling=S.pbr_material("M_CeilingPanel", (0.74, 0.72, 0.62, 1), 0.0, 0.72),
            beige=S.pbr_material("M_OfficePlastic", (0.62, 0.56, 0.40, 1), 0.0, 0.55),
            steel=S.pbr_material("M_SteelPainted", (0.42, 0.40, 0.34, 1), 0.55, 0.52),
            lamp=S.pbr_material("M_Fluorescent", (0.92, 0.94, 0.86, 1), 0.0, 0.25,
                                emission=(1.0, 0.97, 0.82, 1), emission_strength=5.5),
            glass=S.pbr_material("M_SignGlass", (0.10, 0.30, 0.14, 1), 0.0, 0.15,
                                 emission=(0.20, 1.0, 0.35, 1), emission_strength=2.4),
            yellow=S.pbr_material("M_MopYellow", (0.88, 0.72, 0.08, 1), 0.0, 0.55),
            paper=S.pbr_material("M_Cardboard", (0.45, 0.34, 0.20, 1), 0.0, 0.88, noise_scale=60, bump=0.35))
    if level == "poolrooms":
        return dict(
            tile=S.pbr_material("M_PoolTile", (0.62, 0.76, 0.74, 1), 0.0, 0.18, noise_scale=220, bump=0.10),
            tile_dark=S.pbr_material("M_PoolTileDeep", (0.10, 0.22, 0.24, 1), 0.0, 0.55),
            grout=S.pbr_material("M_Grout", (0.07, 0.10, 0.11, 1), 0.0, 0.80),
            chrome=S.pbr_material("M_Chrome", (0.80, 0.82, 0.84, 1), 1.0, 0.12),
            water=S.pbr_material("M_Water", (0.14, 0.42, 0.48, 1), 0.0, 0.06,
                                 emission=(0.10, 0.30, 0.34, 1), emission_strength=0.35),
            plastic=S.pbr_material("M_PoolPlastic", (0.86, 0.86, 0.82, 1), 0.0, 0.35),
            safety=S.pbr_material("M_SafetyOrange", (0.92, 0.38, 0.06, 1), 0.0, 0.55),
            yellow=S.pbr_material("M_WetFloorYellow", (0.92, 0.80, 0.06, 1), 0.0, 0.60))
    return dict(
        concrete=S.pbr_material("M_PowerConcrete", (0.24, 0.24, 0.23, 1), 0.0, 0.88, noise_scale=35, bump=0.55),
        iron=S.pbr_material("M_CastIron", (0.16, 0.16, 0.17, 1), 0.75, 0.62, noise_scale=90, bump=0.45),
        rust=S.pbr_material("M_Rust", (0.36, 0.19, 0.09, 1), 0.35, 0.82, noise_scale=70, bump=0.55),
        copper=S.pbr_material("M_Copper", (0.62, 0.34, 0.16, 1), 0.95, 0.35),
        paint=S.pbr_material("M_IndustrialGreen", (0.20, 0.30, 0.24, 1), 0.25, 0.60),
        warn=S.pbr_material("M_WarningYellow", (0.90, 0.74, 0.05, 1), 0.0, 0.55),
        glass=S.pbr_material("M_GaugeGlass", (0.72, 0.78, 0.74, 1), 0.0, 0.10,
                             emission=(0.35, 0.75, 0.55, 1), emission_strength=1.6),
        lamp=S.pbr_material("M_IndustrialLamp", (0.95, 0.92, 0.80, 1), 0.0, 0.30,
                            emission=(1.0, 0.90, 0.70, 1), emission_strength=4.0))


# =========================== L0: ЖЁЛТЫЕ КОРИДОРЫ ===========================

def lv_corridor_panel(m):
    """Секция стены 2.4×2.6: обои в полоску (выступают наружу), плинтус, потёк влаги."""
    p = [K.loft("cp_wall", [(0.0, K.ring_rect(2.40, 2.60, 0.01, 1)),
                            (0.10, K.ring_rect(2.40, 2.60, 0.01, 1))],
                mat=m["wallpaper"], smooth=False)]
    # полоски обоев — выступают на +X
    for i in range(23):
        st = K.loft(f"cp_stripe{i}", [(0.098, K.ring_rect(0.030, 2.58, 0.002, 1)),
                                      (0.108, K.ring_rect(0.030, 2.58, 0.002, 1))],
                    mat=m["wall"], smooth=False)
        st.location = (-1.10 + 0.10 * i, 0, 0)
        p.append(st)
    # плинтус снизу
    base = K.loft("cp_base", [(0.0, K.ring_rect(2.40, 0.12, 0.005, 1)),
                              (0.115, K.ring_rect(2.40, 0.12, 0.005, 1))], mat=m["beige"], smooth=False)
    for v in base.data.vertices:
        v.co.z -= 1.24
    p.append(base)
    # потёк влаги (тёмное пятно на лицевой стороне)
    damp = K.loft("cp_damp", [(0.100, K.ring_rect(0.36, 0.90, 0.02, 2)),
                              (0.112, K.ring_rect(0.30, 0.82, 0.02, 2))], mat=m["carpet"], smooth=False)
    damp.location = (0.62, 0, 0.42)
    p.append(damp)
    return S.join_objects(p, "LV_corridor_panel")


def lv_ceiling_light(m):
    """Потолочная лампа дневного света: корпус, две колбы, рассеиватель."""
    p = [K.loft("cl_body", [(0.0, K.ring_rect(1.22, 0.16, 0.008, 2)),
                            (0.10, K.ring_rect(1.22, 0.16, 0.008, 2))], mat=m["ceiling"], smooth=False),
         K.loft("cl_diff", [(0.055, K.ring_rect(1.18, 0.13, 0.01, 2)),
                            (0.075, K.ring_rect(1.18, 0.13, 0.01, 2))], mat=m["lamp"], smooth=False)]
    for i, dz in ((0, 0.028), (1, -0.028)):
        p.append(K.cyl_y(f"cl_tube{i}", 0.016, 1.10, y0=-(1.10) * 0.5, n=14, mat=m["lamp"]))
        p[-1].rotation_euler = (0, 0, R(90))
        p[-1].location = (0, 0, dz)
    p.append(K.cyl_y("cl_chain", 0.004, 0.22, y0=0.10, n=8, mat=m["steel"]))
    return S.join_objects(p, "LV_ceiling_light")


def lv_office_chair(m):
    """Офисный стул: крестовина, газлифт, сиденье, спинка на стойке."""
    p = []
    for i in range(5):
        a = R(72 * i)
        leg = K.loft(f"oc_leg{i}", [(0.0, K.ring_rect(0.30, 0.035, 0.008, 1)),
                                    (0.045, K.ring_rect(0.30, 0.035, 0.008, 1))], mat=m["steel"], smooth=False)
        leg.rotation_euler = (0, 0, a)
        leg.location = (math.cos(a) * 0.15, math.sin(a) * 0.15, 0.02)
        p.append(leg)
        p.append(K.cyl_y(f"oc_wheel{i}", 0.030, 0.035, y0=-(0.035) * 0.5, n=12, mat=m["beige"]))
        p[-1].rotation_euler = (0, 0, R(90))
        p[-1].location = (math.cos(a) * 0.30, math.sin(a) * 0.30, 0.030)
    p.append(K.cyl_y("oc_lift", 0.028, 0.26, y0=0.06, n=16, mat=m["steel"]))
    p.append(K.loft("oc_seat", [(0.0, K.ring_rect(0.46, 0.46, 0.05, 2)),
                                (0.09, K.ring_rect(0.44, 0.44, 0.05, 2))], mat=m["beige"], smooth=True))
    for v in p[-1].data.vertices:
        v.co.z += 0.36
    p.append(K.cyl_y("oc_post", 0.018, 0.22, y0=-0.10, z=0.52, n=12, mat=m["steel"]))
    p[-1].rotation_euler = (R(8), 0, 0)
    p[-1].location = (0, 0.18, 0.42)
    back = K.loft("oc_back", [(0.0, K.ring_rect(0.42, 0.44, 0.05, 2)),
                              (0.10, K.ring_rect(0.40, 0.42, 0.05, 2))], mat=m["beige"], smooth=True)
    back.rotation_euler = (R(-10), 0, 0)
    back.location = (0, 0.02, 0.46)
    p.append(back)
    return S.join_objects(p, "LV_office_chair")


def lv_filing_cabinet(m):
    """Картотека: 4 ящика, ручки, замок, вмятина."""
    p = [K.loft("fc_body", [(0.0, K.ring_rect(0.52, 1.32, 0.01, 1)),
                            (0.62, K.ring_rect(0.52, 1.32, 0.01, 1))], mat=m["steel"], smooth=False)]
    for v in p[-1].data.vertices:
        v.co.z += 0.66
    for i in range(4):
        z = 0.08 + 0.32 * i
        p.append(K.loft(f"fc_draw{i}", [(0.575, K.ring_rect(0.48, 0.30, 0.01, 1)),
                                        (0.625, K.ring_rect(0.48, 0.30, 0.01, 1))], mat=m["beige"], smooth=False))
        for v in p[-1].data.vertices:
            v.co.z += z + 0.15
        p.append(K.loft(f"fc_handle{i}", [(0.620, K.ring_rect(0.30, 0.028, 0.006, 1)),
                                          (0.665, K.ring_rect(0.30, 0.028, 0.006, 1))], mat=m["steel"], smooth=False))
        for v in p[-1].data.vertices:
            v.co.z += z + 0.22
        p.append(K.cyl_y(f"fc_lock{i}", 0.020, 0.018, y0=-0.009, x=0.16, z=z + 0.15, n=12, mat=m["steel"]))
        p[-1].rotation_euler = (0, 0, R(90))
        p[-1].location = (0.16, 0.628, z + 0.15)
        p[-1].rotation_euler = (R(90), 0, 0)
    return S.join_objects(p, "LV_filing_cabinet")


def lv_water_cooler(m):
    """Кулер: корпус, бутыль с водой, краники, поддон."""
    p = [K.loft("wc_body", [(0.0, K.ring_rect(0.34, 0.86, 0.02, 2)),
                            (0.34, K.ring_rect(0.32, 0.84, 0.02, 2))], mat=m["beige"], smooth=False)]
    for v in p[-1].data.vertices:
        v.co.z += 0.43
    bottle = K.loft("wc_bottle", [(0.0, K.ring_circle(0.145, 18)), (0.18, K.ring_circle(0.148, 18)),
                                  (0.32, K.ring_circle(0.070, 18)), (0.40, K.ring_circle(0.055, 18))],
                    mat=m.get("water") or m["glass"], smooth=True)
    for v in bottle.data.vertices:
        v.co.z += 0.86
    p.append(bottle)
    p.append(K.cyl_y("wc_tap", 0.016, 0.10, y0=-(0.10) * 0.5, n=10, mat=m["steel"]))
    p[-1].rotation_euler = (0, 0, R(90))
    p[-1].location = (0.06, -0.10, 0.30)
    p.append(K.loft("wc_tray", [(0.0, K.ring_rect(0.26, 0.20, 0.01, 1)),
                                (0.045, K.ring_rect(0.24, 0.18, 0.01, 1))], mat=m["steel"], smooth=False))
    for v in p[-1].data.vertices:
        v.co.z += 0.05
    return S.join_objects(p, "LV_water_cooler")


def lv_cardboard_stack(m):
    """Стопка коробок: три разного размера, скотч, отогнутые клапаны."""
    p = []
    sizes = ((0.52, 0.40, 0.36), (0.44, 0.34, 0.30), (0.36, 0.30, 0.26))
    z = 0.0
    for i, (w, d, h) in enumerate(sizes):
        b = K.loft(f"cb_box{i}", [(0.0, K.ring_rect(w, h, 0.012, 2)),
                                  (d, K.ring_rect(w, h, 0.012, 2))], mat=m["paper"], smooth=False)
        b.rotation_euler = (0, 0, R(-14 + 12 * i))
        b.location = (0.02 * i - 0.02, 0.0, z + h * 0.5)
        p.append(b)
        t = K.loft(f"cb_tape{i}", [(0.0, K.ring_rect(0.045, 0.004, 0.001, 1)),
                                   (d + 0.001, K.ring_rect(0.045, 0.004, 0.001, 1))], mat=m["beige"], smooth=False)
        t.rotation_euler = (0, 0, R(-14 + 12 * i))
        t.location = (0.02 * i - 0.02, 0.0, z + h * 0.5 + h * 0.5)
        p.append(t)
        z += h + 0.005
    return S.join_objects(p, "LV_cardboard_stack")


def lv_mop_bucket(m):
    """Ведро с шваброй: жёлтое ведро, отжим, ручка, черенок с тряпкой."""
    p = [K.loft("mb_bucket", [(0.0, K.ring_circle(0.16, 20)), (0.06, K.ring_circle(0.17, 20)),
                              (0.34, K.ring_circle(0.20, 20)), (0.37, K.ring_circle(0.205, 20))],
                mat=m["yellow"], smooth=True),
         K.loft("mb_wringer", [(0.18, K.ring_rect(0.16, 0.12, 0.01, 1)),
                               (0.36, K.ring_rect(0.14, 0.10, 0.01, 1))], mat=m["yellow"], smooth=False)]
    for v in p[-1].data.vertices:
        v.co.x += 0.16
    p.append(K.cyl_y("mb_handle", 0.010, 0.42, y0=-(0.42) * 0.5, n=10, mat=m["steel"]))
    p[-1].rotation_euler = (0, 0, R(90))
    p[-1].location = (0, 0, 0.36)
    p.append(K.cyl_y("mb_stick", 0.014, 1.30, y0=0, n=12, mat=m["beige"]))
    p[-1].rotation_euler = (R(74), 0, R(20))
    p[-1].location = (0.10, -0.10, 0.65)
    p.append(K.loft("mb_head", [(0.0, K.ring_rect(0.26, 0.10, 0.02, 2)),
                                (0.30, K.ring_rect(0.22, 0.08, 0.02, 2))], mat=m["carpet"], smooth=False))
    p[-1].location = (-0.22, -0.46, 0.06)
    p[-1].rotation_euler = (R(74), 0, R(20))
    return S.join_objects(p, "LV_mop_bucket")


def lv_exit_sign(m):
    """Табличка EXIT: корпус, светящаяся вставка, текст-стрелка из геометрии."""
    p = [K.loft("ex_body", [(0.0, K.ring_rect(0.42, 0.18, 0.01, 1)),
                            (0.07, K.ring_rect(0.42, 0.18, 0.01, 1))], mat=m["beige"], smooth=False),
         K.loft("ex_face", [(0.055, K.ring_rect(0.36, 0.13, 0.008, 1)),
                            (0.075, K.ring_rect(0.36, 0.13, 0.008, 1))], mat=m["glass"], smooth=False)]
    for i in range(3):
        s = K.loft(f"ex_bar{i}", [(0.070, K.ring_rect(0.055, 0.075, 0.004, 1)),
                                  (0.082, K.ring_rect(0.055, 0.075, 0.004, 1))], mat=m["ceiling"], smooth=False)
        s.location = (-0.10 + 0.09 * i, 0, 0)
        p.append(s)
    arrow = K.loft("ex_arrow", [(0.070, K.ring_rect(0.10, 0.045, 0.002, 1)),
                                (0.082, K.ring_rect(0.10, 0.045, 0.002, 1))], mat=m["ceiling"], smooth=False)
    arrow.location = (0.10, 0, 0)
    p.append(arrow)
    obj = S.join_objects(p, "LV_exit_sign")
    obj.rotation_euler = (0, 0, R(90))     # лицо наружу (для настенного монтажа)
    return obj


def lv_desk_fan(m):
    """Настольный вентилятор: решётка, лопасти, стойка, кнопка."""
    p = [K.loft("df_motor", [(0.0, K.ring_circle(0.055, 16)), (0.09, K.ring_circle(0.050, 16))],
                mat=m["beige"], smooth=True)]
    for i in range(9):
        a = R(40 * i)
        p.append(K.tube_between(f"df_spoke{i}", (0.02, 0, 0), (0.14, math.cos(a) * 0.15, math.sin(a) * 0.15),
                                0.004, 8, mat=m["steel"]))
    p.append(K.loft("df_ring", [(0.155, K.ring_circle(0.165, 24)), (0.175, K.ring_circle(0.165, 24))],
                    mat=m["steel"], smooth=True))
    for i in range(3):
        a = R(120 * i)
        blade = K.loft(f"df_blade{i}", [(0.0, K.ring_rect(0.10, 0.02, 0.004, 1)),
                                        (0.02, K.ring_rect(0.10, 0.02, 0.004, 1))], mat=m["beige"], smooth=False)
        blade.rotation_euler = (R(18), 0, a)
        blade.location = (0.02, math.cos(a) * 0.06, math.sin(a) * 0.06)
        p.append(blade)
    p.append(K.cyl_y("df_post", 0.016, 0.20, y0=-(0.20) * 0.5, n=12, mat=m["steel"]))
    p[-1].rotation_euler = (0, 0, R(90))
    p[-1].location = (0, 0, -0.10)
    p.append(K.loft("df_base", [(0.0, K.ring_rect(0.20, 0.22, 0.02, 2)),
                                (0.03, K.ring_rect(0.18, 0.20, 0.02, 2))], mat=m["beige"], smooth=False))
    for v in p[-1].data.vertices:
        v.co.z -= 0.22
    return S.join_objects(p, "LV_desk_fan")


# =========================== L37: БАССЕЙНЫ ===========================

def lv_pool_ladder(m):
    """Лестница в бассейн: две стойки, ступени, хромированные поручни."""
    p = []
    for sx in (-1, 1):
        p.append(K.cyl_y(f"pl_rail{sx}", 0.020, 0.60, y0=-0.30, x=sx * 0.22, z=0.90, n=16, mat=m["chrome"]))
        for i in range(3):
            p.append(K.cyl_y(f"pl_leg{sx}_{i}", 0.018, 0.52, y0=-(0.52) * 0.5, n=14, mat=m["chrome"]))
            p[-1].rotation_euler = (0, 0, R(90))
            p[-1].location = (sx * 0.22, 0, 0.26)
            break
    for i in range(3):
        st = K.loft(f"pl_step{i}", [(0.0, K.ring_rect(0.46, 0.030, 0.008, 1)),
                                    (0.16, K.ring_rect(0.46, 0.030, 0.008, 1))], mat=m["chrome"], smooth=False)
        st.location = (0, 0, 0.10 + 0.22 * i)
        p.append(st)
        for sx in (-1, 1):
            p.append(K.tube_between(f"pl_post{sx}_{i}", (sx * 0.22, 0, 0.02),
                                    (sx * 0.22, 0, 0.10 + 0.22 * i), 0.016, 12, mat=m["chrome"]))
    return S.join_objects(p, "LV_pool_ladder")


def lv_lifebuoy(m):
    """Спасательный круг: тор + 4 белые полосы (сегменты того же тора) + верёвки."""
    p = []
    bands = [(10, 40), (100, 130), (190, 220), (280, 310)]
    pos = 0.0
    for a0, a1 in bands:
        p.append(K.ring_arc(f"lb_orange{int(a0)}", 0.36, 0.100, pos, a0, n_major=10,
                            mat=m["safety"]))
        p.append(K.ring_arc(f"lb_white{int(a0)}", 0.36, 0.101, a0, a1, n_major=6,
                            mat=m["plastic"]))
        pos = a1
    p.append(K.ring_arc("lb_orange_end", 0.36, 0.100, pos, 360.0, n_major=8, mat=m["safety"]))
    for i in range(4):
        a = R(90 * i + 45)
        p.append(K.cyl_y(f"lb_rope{i}", 0.009, 0.18, y0=0, n=10, mat=m["plastic"]))
        p[-1].rotation_euler = (0, 0, a)
        p[-1].location = (math.cos(a) * 0.50, math.sin(a) * 0.50, 0.0)
    return S.join_objects(p, "LV_lifebuoy")


def lv_deck_chair(m):
    """Лежак: рама, 18 планок, подголовник, колёсики."""
    p = [K.loft("dc_frame", [(0.0, K.ring_rect(0.62, 0.06, 0.01, 1)),
                             (1.80, K.ring_rect(0.62, 0.06, 0.01, 1))], mat=m["plastic"], smooth=False)]
    for v in p[-1].data.vertices:
        v.co.z += 0.22
    for i in range(16):
        s = K.loft(f"dc_slat{i}", [(0.06 + 0.11 * i, K.ring_rect(0.58, 0.030, 0.006, 1)),
                                   (0.13 + 0.11 * i, K.ring_rect(0.58, 0.030, 0.006, 1))],
                   mat=m["plastic"], smooth=False)
        for v in s.data.vertices:
            v.co.z += 0.24
        p.append(s)
    for i in range(4):
        z = -0.06 + 0.14 * i
        p.append(K.cyl_y(f"dc_leg{i}", 0.018, 0.24, y0=0, x=(-0.24 if i % 2 == 0 else 0.24), z=0.10, n=12, mat=m["chrome"]))
        p[-1].rotation_euler = (R(10), 0, 0)
    back = K.loft("dc_back", [(0.0, K.ring_rect(0.58, 0.05, 0.01, 1)),
                              (0.50, K.ring_rect(0.58, 0.05, 0.01, 1))], mat=m["plastic"], smooth=False)
    back.rotation_euler = (R(-42), 0, 0)
    back.location = (0, 1.26, 0.20)
    p.append(back)
    return S.join_objects(p, "LV_deck_chair")


def lv_pool_pump(m):
    """Насос с фильтром: корпус, манометр, патрубки, вентиль."""
    p = [K.loft("pp_body", [(0.0, K.ring_rect(0.52, 0.62, 0.03, 2)),
                            (0.42, K.ring_rect(0.50, 0.60, 0.03, 2))], mat=m["paint"] if "paint" in m else m["plastic"],
                smooth=True),
         K.loft("pp_lid", [(0.40, K.ring_rect(0.48, 0.58, 0.03, 2)),
                           (0.46, K.ring_rect(0.44, 0.54, 0.03, 2))], mat=m["chrome"], smooth=True)]
    for v in p[0].data.vertices:
        v.co.z += 0.32
    p.append(K.cyl_y("pp_pipe1", 0.075, 0.30, y0=-(0.30) * 0.5, n=18, mat=m["plastic"]))
    p[-1].rotation_euler = (0, 0, R(90))
    p[-1].location = (-0.16, 0, 0.46)
    p.append(K.cyl_y("pp_pipe2", 0.065, 0.60, y0=-0.60, x=0.18, z=0.10, n=18, mat=m["plastic"]))
    p.append(K.cyl_y("pp_gauge", 0.070, 0.045, y0=-(0.045) * 0.5, n=20, mat=m["chrome"]))
    p[-1].rotation_euler = (0, 0, R(90))
    p[-1].location = (0.12, 0, 0.56)
    p.append(K.cyl_y("pp_face", 0.060, 0.010, y0=-(0.010) * 0.5, n=20, mat=m["water"]))
    p[-1].rotation_euler = (0, 0, R(90))
    p[-1].location = (0.13, 0, 0.56)
    p.append(K.ring_torus("pp_valve", 0.075, 0.014, 20, 8, rot=(0, 0, 0), loc=(-0.16, 0.16, 0.46), mat=m["safety"]))
    return S.join_objects(p, "LV_pool_pump")


def lv_pipe_valve(m):
    """Труба с вентилем: фланцы, болты, колесо-штурвал."""
    p = [K.cyl_y("pv_pipe", 0.115, 1.10, y0=-0.55, n=24, mat=m["plastic"])]
    for y in (-0.40, 0.30):
        p.append(K.cyl_y(f"pv_flange{y}", 0.155, 0.055, y0=y, n=24, mat=m["chrome"]))
        for i in range(8):
            a = R(45 * i)
            p.append(K.cyl_y(f"pv_bolt{y}{i}", 0.016, 0.070, y0=y - 0.005, x=math.cos(a) * 0.125,
                             z=math.sin(a) * 0.125, n=10, mat=m["chrome"]))
            p[-1].rotation_euler = (0, R(90), 0)
    p.append(K.ring_torus("pv_wheel", 0.155, 0.020, 24, 8, rot=(R(90), 0, 0), loc=(0, -0.05, 0.30), mat=m["safety"]))
    for i in range(4):
        a = R(45 * i)
        p.append(K.tube_between(f"pv_spoke{i}", (math.cos(a) * 0.02, -0.05, 0.30 + math.sin(a) * 0.02),
                                (math.cos(a) * 0.15, -0.05, 0.30 + math.sin(a) * 0.15), 0.012, 8, mat=m["safety"]))
    p.append(K.cyl_y("pv_stem", 0.022, 0.24, y0=-0.05, z=0.18, n=12, mat=m["chrome"]))
    return S.join_objects(p, "LV_pipe_valve")


def lv_shower_head(m):
    """Душевая (настенная): труба-стояк, кран-штурвал, штанга, круглая лейка."""
    p = []
    # стояк от пола до 1.60 (в центре координат, потом сдвинем весь узел)
    p.append(K.cyl_y("sh_riser", 0.028, 1.60, y0=-0.80, n=16, mat=m["chrome"]))
    for v in p[-1].data.vertices:
        v.co.z += 0.80
    # нижний и верхний фланцы
    for z in (0.02, 1.58):
        f = K.cyl_y(f"sh_fl{z}", 0.055, 0.038, y0=-0.019, n=16, mat=m["chrome"])
        f.location = (0, 0, z)
        p.append(f)
    # штанга вперёд
    arm = K.cyl_y("sh_arm", 0.024, 0.34, y0=-0.17, n=14, mat=m["chrome"])
    arm.rotation_euler = (R(-90), 0, 0)
    arm.location = (0, 0.17, 1.58)
    p.append(arm)
    # лейка под штангой
    head = K.loft("sh_head", [(0.0, K.ring_circle(0.055, 20)), (0.05, K.ring_circle(0.105, 20)),
                              (0.085, K.ring_circle(0.150, 20))], mat=m["chrome"], smooth=True)
    head.rotation_euler = (R(140), 0, 0)
    head.location = (0, 0.34, 1.52)
    p.append(head)
    # кран-штурвал на стояке
    p.append(K.ring_torus("sh_wheel", 0.070, 0.014, 18, 8, rot=(0, R(90), 0),
                          loc=(0.045, 0, 1.15), mat=m["safety"]))
    p.append(K.tube_between("sh_spindle", (0, 0, 1.15), (0.045, 0, 1.15), 0.014, 10, mat=m["chrome"]))
    return S.join_objects(p, "LV_shower_head")


def lv_wet_floor_sign(m):
    """Знак «мокрый пол»: А-образная рама, жёлтый, складки."""
    p = []
    for sx in (-1, 1):
        panel = K.loft(f"wf_panel{sx}", [(0.0, K.ring_rect(0.30, 0.62, 0.02, 2)),
                                         (0.018, K.ring_rect(0.30, 0.62, 0.02, 2))],
                       mat=m["yellow"], smooth=False)
        panel.rotation_euler = (R(sx * 12), 0, 0)
        panel.location = (0, sx * 0.09, 0.31)
        p.append(panel)
    p.append(K.cyl_y("wf_hinge", 0.020, 0.30, y0=-(0.30) * 0.5, n=12, mat=m["yellow"]))
    p[-1].rotation_euler = (0, 0, R(90))
    p[-1].location = (0, 0, 0.62)
    return S.join_objects(p, "LV_wet_floor_sign")


def lv_pool_tile_panel(m):
    """Панель кафеля: 6×3 плитки с зазорами 0.06 и тёмной затиркой между ними."""
    p = [K.loft("tp_back", [(0.0, K.ring_rect(1.90, 0.98, 0.01, 1)),
                            (0.02, K.ring_rect(1.90, 0.98, 0.01, 1))], mat=m["grout"], smooth=False)]
    for row in range(3):
        for col in range(6):
            t = K.loft(f"tp_t{row}_{col}", [(0.015, K.ring_rect(0.22, 0.22, 0.012, 2)),
                                            (0.075, K.ring_rect(0.22, 0.22, 0.012, 2))],
                       mat=m["tile"], smooth=True)
            t.location = (-0.75 + 0.30 * col, 0, -0.30 + 0.30 * row)
            p.append(t)
    return S.join_objects(p, "LV_pool_tile_panel")


def lv_inflatable_ring(m):
    """Надувной круг: тор + 3 белые полосы сегментами + клапан."""
    p = []
    bands = [(20, 50), (140, 170), (260, 290)]
    pos = 0.0
    for a0, a1 in bands:
        p.append(K.ring_arc(f"ir_orange{int(a0)}", 0.34, 0.135, pos, a0, n_major=10, mat=m["safety"]))
        p.append(K.ring_arc(f"ir_white{int(a0)}", 0.34, 0.136, a0, a1, n_major=6, mat=m["plastic"]))
        pos = a1
    p.append(K.ring_arc("ir_orange_end", 0.34, 0.135, pos, 360.0, n_major=8, mat=m["safety"]))
    p.append(K.cyl_y("ir_valve", 0.022, 0.05, y0=-0.025, n=10, mat=m["plastic"]))
    p[-1].rotation_euler = (0, 0, R(90))
    p[-1].location = (0.485, 0.0, 0.0)
    return S.join_objects(p, "LV_inflatable_ring")


# =========================== L3: ЭЛЕКТРОСТАНЦИЯ ===========================

def lv_turbine_housing(m):
    """Корпус турбины: цилиндр, фланец с болтами, лопатки, вал, патрубки."""
    p = [K.cyl_y("th_body", 0.52, 1.20, y0=-0.60, n=28, mat=m["iron"]),
         K.cyl_y("th_flange", 0.60, 0.09, y0=-0.64, n=28, mat=m["rust"]),
         K.cyl_y("th_shaft", 0.09, 1.90, y0=-0.95, n=20, mat=m["copper"])]
    for i in range(14):
        a = R(360 * i / 14)
        p.append(K.cyl_y(f"th_bolt{i}", 0.026, 0.11, y0=-0.70, x=math.cos(a) * 0.56, z=math.sin(a) * 0.56,
                         n=10, mat=m["rust"]))
        p[-1].rotation_euler = (0, 0, 0)
        p[-1].rotation_euler = (0, 0, 0)
    for i in range(8):
        a = R(45 * i)
        p.append(K.tube_between(f"th_vane{i}", (0, 0.35, 0.20), (math.cos(a) * 0.46, 0.62, math.sin(a) * 0.46),
                                0.026, 8, mat=m["copper"]))
    p.append(K.cyl_y("th_pipe", 0.18, 0.70, y0=0.40, x=0, z=0.62, n=20, mat=m["rust"]))
    return S.join_objects(p, "LV_turbine_housing")


def lv_control_panel(m):
    """Щит управления: шкаф, 3 прибора, тумблеры, лампа, кабельные вводы."""
    p = [K.loft("cp_panel", [(0.0, K.ring_rect(1.10, 2.00, 0.02, 2)),
                             (0.34, K.ring_rect(1.08, 1.98, 0.02, 2))], mat=m["paint"], smooth=False)]
    for v in p[-1].data.vertices:
        v.co.z += 1.00
    p.append(K.loft("cp_door", [(0.335, K.ring_rect(0.50, 1.80, 0.015, 2)),
                                (0.36, K.ring_rect(0.48, 1.78, 0.015, 2))], mat=m["iron"], smooth=False))
    for v in p[-1].data.vertices:
        v.co.z += 1.00
        v.co.x -= 0.27
    for i in range(3):
        x = -0.18 + 0.18 * i
        p.append(K.cyl_y(f"cp_gauge{i}", 0.110, 0.05, y0=-(0.05) * 0.5, n=22, mat=m["iron"]))
        p[-1].rotation_euler = (0, 0, R(90))
        p[-1].location = (x, 0, 1.55)
        p.append(K.cyl_y(f"cp_glass{i}", 0.095, 0.012, y0=-(0.012) * 0.5, n=22, mat=m["glass"]))
        p[-1].rotation_euler = (0, 0, R(90))
        p[-1].location = (x + 0.005, 0, 1.55)
    for i in range(6):
        p.append(K.loft(f"cp_switch{i}", [(0.34, K.ring_rect(0.05, 0.09, 0.008, 1)),
                                          (0.40, K.ring_rect(0.05, 0.09, 0.008, 1))], mat=m["warn"], smooth=False))
        for v in p[-1].data.vertices:
            v.co.z += 0.95 - 0.16 * i
    p.append(K.loft("cp_lamp", [(0.33, K.ring_rect(0.16, 0.10, 0.01, 1)),
                                (0.38, K.ring_rect(0.16, 0.10, 0.01, 1))], mat=m["warn"], smooth=False))
    for v in p[-1].data.vertices:
        v.co.z += 1.85
    for i in range(4):
        p.append(K.cyl_y(f"cp_cable{i}", 0.030, 0.22, y0=-0.14, x=-0.30 + 0.20 * i, z=0.10, n=12, mat=m["iron"]))
    return S.join_objects(p, "LV_control_panel")


def lv_transformer(m):
    """Трансформатор: бак, рёбра охлаждения, изоляторы, выводы."""
    p = [K.loft("tr_tank", [(0.0, K.ring_rect(1.40, 1.30, 0.04, 2)),
                            (0.80, K.ring_rect(1.36, 1.26, 0.04, 2))], mat=m["paint"], smooth=False)]
    for v in p[-1].data.vertices:
        v.co.z += 0.65
    for i in range(9):
        f = K.loft(f"tr_fin{i}", [(-0.56 + 0.14 * i, K.ring_rect(0.46, 1.06, 0.01, 1)),
                                  (-0.53 + 0.14 * i, K.ring_rect(0.46, 1.06, 0.01, 1))],
                   mat=m["iron"], smooth=False)
        for v in f.data.vertices:
            v.co.z += 0.65
        p.append(f)
    for i in range(3):
        x = -0.40 + 0.40 * i
        p.append(K.loft(f"tr_ins{i}", [(1.24, K.ring_circle(0.075, 14)), (1.34, K.ring_circle(0.095, 14)),
                                       (1.48, K.ring_circle(0.075, 14)), (1.54, K.ring_circle(0.055, 14))],
                        mat=m["concrete"], smooth=True))
        p[-1].location = (0, x, 0)
        p.append(K.cyl_y(f"tr_term{i}", 0.024, 0.16, y0=-(0.16) * 0.5, n=12, mat=m["copper"]))
        p[-1].rotation_euler = (0, 0, R(90))
        p[-1].location = (0, x, 1.58)
    p.append(K.loft("tr_warn", [(0.56, K.ring_rect(0.70, 0.30, 0.01, 1)),
                                (0.60, K.ring_rect(0.70, 0.30, 0.01, 1))], mat=m["warn"], smooth=False))
    for v in p[-1].data.vertices:
        v.co.z += 0.32
    return S.join_objects(p, "LV_transformer")


def lv_breaker_cabinet(m):
    """Шкаф рубильников: корпус, дверца, рубильник, шильдики, ножки."""
    p = [K.loft("bc_body", [(0.0, K.ring_rect(0.90, 1.60, 0.02, 2)),
                            (0.32, K.ring_rect(0.88, 1.58, 0.02, 2))], mat=m["paint"], smooth=False)]
    for v in p[-1].data.vertices:
        v.co.z += 0.90
    p.append(K.loft("bc_door", [(0.315, K.ring_rect(0.78, 1.40, 0.015, 2)),
                                (0.34, K.ring_rect(0.76, 1.38, 0.015, 2))], mat=m["iron"], smooth=False))
    for v in p[-1].data.vertices:
        v.co.z += 0.92
    p.append(K.loft("bc_lever", [(0.33, K.ring_rect(0.10, 0.34, 0.02, 1)),
                                 (0.40, K.ring_rect(0.08, 0.30, 0.02, 1))], mat=m["rust"], smooth=False))
    for v in p[-1].data.vertices:
        v.co.z += 0.90
        v.co.x += 0.26
    p.append(K.cyl_y("bc_lock", 0.030, 0.05, y0=-(0.05) * 0.5, n=12, mat=m["iron"]))
    p[-1].rotation_euler = (0, 0, R(90))
    p[-1].location = (-0.28, 0, 0.90)
    for i in range(3):
        p.append(K.loft(f"bc_tag{i}", [(0.34, K.ring_rect(0.26, 0.11, 0.006, 1)),
                                       (0.365, K.ring_rect(0.26, 0.11, 0.006, 1))], mat=m["warn"], smooth=False))
        for v in p[-1].data.vertices:
            v.co.z += 1.45 - 0.22 * i
    for sx in (-1, 1):
        p.append(K.cyl_y(f"bc_leg{sx}", 0.030, 0.22, y0=0, x=sx * 0.36, z=0.10, n=10, mat=m["iron"]))
        p[-1].location = (0, sx * 0.36, 0.11)
    return S.join_objects(p, "LV_breaker_cabinet")


def lv_pipe_flange(m):
    """Труба с фланцем и рёбрами: то, что вьётся по стенам станции."""
    p = [K.cyl_y("pf_pipe", 0.170, 1.40, y0=-0.70, n=26, mat=m["rust"]),
         K.cyl_y("pf_flange", 0.225, 0.070, y0=-0.10, n=26, mat=m["iron"])]
    for i in range(10):
        a = R(36 * i)
        p.append(K.cyl_y(f"pf_bolt{i}", 0.022, 0.085, y0=-0.11, x=math.cos(a) * 0.185,
                         z=math.sin(a) * 0.185, n=8, mat=m["iron"]))
    for i in range(3):
        p.append(K.cyl_y(f"pf_rib{i}", 0.180, 0.035, y0=-0.62 + 0.34 * i, n=26, mat=m["iron"]))
    p.append(K.cyl_y("pf_elbow", 0.170, 0.42, y0=0.70, n=26, mat=m["rust"]))
    p[-1].rotation_euler = (R(90), 0, 0)
    p[-1].location = (0, 0.70, 0.42)
    return S.join_objects(p, "LV_pipe_flange")


def lv_valve_wheel(m):
    """Колесо-вентиль на стойке: штурвал, шток, фланец."""
    p = [K.ring_torus("vw_wheel", 0.32, 0.030, 26, 10, rot=(R(90), 0, 0), loc=(0, 0, 0.80), mat=m["rust"])]
    for i in range(4):
        a = R(45 * i)
        p.append(K.tube_between(f"vw_spoke{i}", (math.cos(a) * 0.03, 0, 0.80 + math.sin(a) * 0.03),
                                (math.cos(a) * 0.30, 0, 0.80 + math.sin(a) * 0.30), 0.022, 8, mat=m["rust"]))
    p.append(K.cyl_y("vw_stem", 0.050, 0.62, y0=0.18, n=16, mat=m["iron"]))
    p.append(K.cyl_y("vw_body", 0.140, 0.44, y0=-0.16, n=20, mat=m["iron"]))
    p.append(K.cyl_y("vw_flange", 0.190, 0.060, y0=-0.20, n=20, mat=m["rust"]))
    for i in range(8):
        a = R(45 * i)
        p.append(K.cyl_y(f"vw_bolt{i}", 0.020, 0.075, y0=-0.21, x=math.cos(a) * 0.15,
                         z=math.sin(a) * 0.15, n=8, mat=m["iron"]))
    return S.join_objects(p, "LV_valve_wheel")


def lv_coolant_tank(m):
    """Бак охлаждения: вертикальный цилиндр, днища, лестница, патрубки, манометр."""
    p = [K.loft("ct_tank", [(0.0, K.ring_circle(0.46, 26)), (0.20, K.ring_circle(0.50, 26)),
                            (1.90, K.ring_circle(0.50, 26)), (2.10, K.ring_circle(0.44, 26))],
                mat=m["paint"], smooth=True)]
    for i in range(6):
        p.append(K.cyl_y(f"ct_band{i}", 0.505, 0.035, y0=0.25 + 0.32 * i, n=26, mat=m["rust"]))
    for i in range(7):
        p.append(K.cyl_y(f"ct_step{i}", 0.030, 0.30, y0=0, x=0.56, z=0.20 + 0.28 * i, n=10, mat=m["rust"]))
        p[-1].rotation_euler = (0, 0, R(90))
    p.append(K.tube_between("ct_rail1", (0.56, -0.16, 0.20), (0.56, -0.16, 2.05), 0.020, 10, mat=m["rust"]))
    p.append(K.tube_between("ct_rail2", (0.56, 0.16, 0.20), (0.56, 0.16, 2.05), 0.020, 10, mat=m["rust"]))
    p.append(K.cyl_y("ct_pipe", 0.16, 0.70, y0=-(0.70) * 0.5, n=20, mat=m["rust"]))
    p[-1].rotation_euler = (0, 0, R(90))
    p[-1].location = (-0.30, 0, 1.20)
    p.append(K.cyl_y("ct_gauge", 0.10, 0.05, y0=-(0.05) * 0.5, n=20, mat=m["iron"]))
    p[-1].rotation_euler = (0, 0, R(90))
    p[-1].location = (0.52, 0, 1.60)
    return S.join_objects(p, "LV_coolant_tank")


def lv_cable_spool(m):
    """Кабельный барабан: два диска с ободом, сердечник, витки кабеля, ось."""
    p = [K.cyl_y("cs_hub", 0.40, 0.56, y0=-0.28, n=24, mat=m["iron"])]
    for sy in (-1, 1):
        p.append(K.cyl_y(f"cs_disc{sy}", 0.78, 0.10, y0=sy * 0.36 - 0.05, n=24, mat=m["rust"]))
        # обод диска (утолщение по краю)
        p.append(K.ring_arc(f"cs_rim{sy}_a", 0.74, 0.05, 0, 360, n_major=20, mat=m["iron"],
                            rot=(R(90), 0, 0), loc=(0, sy * 0.41, 0)))
        for i in range(6):
            a = R(60 * i)
            p.append(K.tube_between(f"cs_rib{sy}{i}", (math.cos(a) * 0.26, sy * 0.41, math.sin(a) * 0.26),
                                    (math.cos(a) * 0.72, sy * 0.41, math.sin(a) * 0.72), 0.024, 8, mat=m["iron"]))
    for i in range(7):
        p.append(K.ring_arc(f"cs_cable{i}", 0.47, 0.055, 0, 360, n_major=18,
                            rot=(R(90), 0, 0), loc=(0, -0.24 + 0.08 * i, 0), mat=m["iron"]))
    p.append(K.cyl_y("cs_axle", 0.06, 1.30, y0=-0.65, n=16, mat=m["iron"]))
    return S.join_objects(p, "LV_cable_spool")


def lv_warning_sign(m):
    """Знак на стойке: ромб с молнией, ржавая стойка, бетонная опора."""
    p = [K.cyl_y("ws_post", 0.032, 1.55, y0=-0.10, n=12, mat=m["rust"]),
         K.loft("ws_base", [(0.0, K.ring_rect(0.36, 0.10, 0.025, 2)),
                            (0.10, K.ring_rect(0.30, 0.08, 0.025, 2))], mat=m["concrete"], smooth=False)]
    # ромб (2 плоскости, повёрнутые на 45° в вертикальной плоскости)
    plate = K.loft("ws_plate", [(-0.015, K.ring_rect(0.46, 0.46, 0.02, 2)),
                                (0.015, K.ring_rect(0.46, 0.46, 0.02, 2))], mat=m["warn"], smooth=False)
    plate.rotation_euler = (0, R(45), 0)
    plate.location = (0, 0, 1.12)
    p.append(plate)
    # молния (ломаный профиль) поверх ромба
    bolt = K.profile("ws_bolt", [(-0.02, 0.14), (0.06, 0.02), (0.005, 0.02), (0.05, -0.14),
                                 (-0.06, -0.01), (-0.005, -0.01)], 0.010,
                     y=0.0, x=0.0, z=1.12, mat=m["iron"])
    bolt.rotation_euler = (0, R(90), 0)
    bolt.location = (0.012, 0, 0)
    p.append(bolt)
    return S.join_objects(p, "LV_warning_sign")


# =========================== РЕЕСТР + ДИОРАМЫ ===========================

LEVELS = {
    "corridors": [("LV_corridor_panel", lv_corridor_panel), ("LV_ceiling_light", lv_ceiling_light),
                  ("LV_office_chair", lv_office_chair), ("LV_filing_cabinet", lv_filing_cabinet),
                  ("LV_water_cooler", lv_water_cooler), ("LV_cardboard_stack", lv_cardboard_stack),
                  ("LV_mop_bucket", lv_mop_bucket), ("LV_exit_sign", lv_exit_sign),
                  ("LV_desk_fan", lv_desk_fan)],
    "poolrooms": [("LV_pool_ladder", lv_pool_ladder), ("LV_lifebuoy", lv_lifebuoy),
                  ("LV_deck_chair", lv_deck_chair), ("LV_pool_pump", lv_pool_pump),
                  ("LV_pipe_valve", lv_pipe_valve), ("LV_shower_head", lv_shower_head),
                  ("LV_wet_floor_sign", lv_wet_floor_sign), ("LV_pool_tile_panel", lv_pool_tile_panel),
                  ("LV_inflatable_ring", lv_inflatable_ring)],
    "powerstation": [("LV_turbine_housing", lv_turbine_housing), ("LV_control_panel", lv_control_panel),
                     ("LV_transformer", lv_transformer), ("LV_breaker_cabinet", lv_breaker_cabinet),
                     ("LV_pipe_flange", lv_pipe_flange), ("LV_valve_wheel", lv_valve_wheel),
                     ("LV_coolant_tank", lv_coolant_tank), ("LV_cable_spool", lv_cable_spool),
                     ("LV_warning_sign", lv_warning_sign)],
}


def _floor(level, m, size=9.0):
    """Пол диорамы под палитру уровня."""
    mat = m["carpet"] if level == "corridors" else (m["tile"] if level == "poolrooms" else m["concrete"])
    return K.loft("dio_floor", [(-size * 0.5, K.ring_rect(size, 0.06, 0.0, 1)),
                                (size * 0.5, K.ring_rect(size, 0.06, 0.0, 1))], mat=mat, smooth=False)


def _room(level, m, size=10.0, height=3.0):
    """Комната уровня: пол, две стены и потолок — иначе вещи «висят в пустоте»."""
    objs = []
    floor_mat = m["carpet"] if level == "corridors" else (m["tile"] if level == "poolrooms" else m["concrete"])
    ceil_mat = m["ceiling"] if level == "corridors" else floor_mat
    wall_mat = m["wallpaper"] if level == "corridors" else (m["tile_dark"] if level == "poolrooms" else m["concrete"])

    floor = K.loft("dio_floor", [(-size * 0.5, K.ring_rect(size, size, 0.0, 1)),
                                 (-size * 0.5 + 0.10, K.ring_rect(size, size, 0.0, 1))], mat=floor_mat, smooth=False)
    for v in floor.data.vertices:
        v.co.z -= 0.05
    objs.append(floor)

    ceil = K.loft("dio_ceil", [(-size * 0.5, K.ring_rect(size, size, 0.0, 1)),
                               (-size * 0.5 + 0.08, K.ring_rect(size, size, 0.0, 1))], mat=ceil_mat, smooth=False)
    for v in ceil.data.vertices:
        v.co.z += height + 0.04
    objs.append(ceil)

    for name, flip in (("dio_wallA", False), ("dio_wallB", True)):
        wall = K.loft(name, [(0.0, K.ring_rect(size, height, 0.0, 1)),
                             (0.16, K.ring_rect(size, height, 0.0, 1))], mat=wall_mat, smooth=False)
        for v in wall.data.vertices:
            v.co.z += height * 0.5
        wall.location = (0, -size * 0.5 - 0.08, 0) if not flip else (-size * 0.5 - 0.08, 0, 0)
        wall.rotation_euler = (0, 0, 0) if not flip else (0, 0, R(90))
        objs.append(wall)
    return objs


def diorama(level, renders=2):
    """Комната уровня: пол + стена + свет под палитру + расставленные уникальные вещи."""
    S.clean_scene()
    m = palette(level)
    objs = _room(level, m)
    if level == "corridors":
        for i in range(3):
            lamp = lv_ceiling_light(m)
            lamp.location = (-2.9 + 2.9 * i, -1.6, 2.94)
            objs.append(lamp)
        for i, (fn, x, y, rot) in enumerate(((lv_filing_cabinet, -3.4, -3.6, 20),
                                             (lv_cardboard_stack, -2.2, -3.9, -12),
                                             (lv_water_cooler, -0.2, -3.7, 0),
                                             (lv_mop_bucket, 1.4, -3.4, 24),
                                             (lv_desk_fan, -0.9, -2.6, 0),
                                             (lv_office_chair, 0.9, -2.4, -35))):
            o = fn(m)
            o.location = (x, y, 0 if fn is not lv_mop_bucket else 0)
            o.rotation_euler = (0, 0, R(rot))
            objs.append(o)
        sign = lv_exit_sign(m)
        sign.location = (1.8, -4.72, 2.10)
        objs.append(sign)
        # секции стены с обоями на обе стены
        for i in range(3):
            wp = lv_corridor_panel(m)
            wp.location = (-3.6 + 3.6 * i, -4.44, 0.55)
            objs.append(wp)
        wp2 = lv_corridor_panel(m)
        wp2.rotation_euler = (0, 0, R(90))
        wp2.location = (-4.44, -1.2, 0.55)
        objs.append(wp2)
    elif level == "poolrooms":
        for i, (fn, x, y, rot) in enumerate(((lv_pool_pump, -3.2, -3.6, 18),
                                             (lv_pipe_valve, -1.4, -3.9, 0),
                                             (lv_deck_chair, 0.6, -3.4, 28),
                                             (lv_lifebuoy, 2.6, -3.6, 0),
                                             (lv_inflatable_ring, 4.0, -2.6, 0),
                                             (lv_wet_floor_sign, -3.6, -1.6, 40))):
            o = fn(m)
            o.location = (x, y, 0)
            o.rotation_euler = (0, 0, R(rot))
            objs.append(o)
        ladder = lv_pool_ladder(m)
        ladder.location = (1.8, -2.2, -0.10)
        objs.append(ladder)
        shower = lv_shower_head(m)
        shower.location = (-4.2, -3.2, 0.0)
        objs.append(shower)
        tile = lv_pool_tile_panel(m)
        tile.location = (-4.2, -3.0, 1.2)
        tile.rotation_euler = (0, 0, R(90))
        objs.append(tile)
    else:
        for i, (fn, x, y, rot) in enumerate(((lv_control_panel, -3.0, -3.7, 14),
                                             (lv_breaker_cabinet, -1.2, -3.8, -8),
                                             (lv_transformer, 1.6, -3.2, 22),
                                             (lv_valve_wheel, 3.6, -3.5, 0),
                                             (lv_cable_spool, -3.8, -1.4, 30),
                                             (lv_warning_sign, 0.6, -1.2, 0))):
            o = fn(m)
            o.location = (x, y, 0)
            o.rotation_euler = (0, 0, R(rot))
            objs.append(o)
        turbine = lv_turbine_housing(m)
        turbine.location = (-0.6, -1.0, 0.55)
        turbine.rotation_euler = (0, 0, R(90))
        objs.append(turbine)
        tank = lv_coolant_tank(m)
        tank.location = (3.4, -1.0, 0.0)
        objs.append(tank)
        pipe = lv_pipe_flange(m)
        pipe.location = (-3.4, 0.6, 0.6)
        pipe.rotation_euler = (0, 0, R(90))
        objs.append(pipe)

    tag = {"corridors": "L0_желтые_коридоры", "poolrooms": "L37_бассейны",
           "powerstation": "L3_электростанция"}[level]
    # камера ВНУТРИ комнаты (обе стены на y=-5 и x=-5), смотрим в дальний угол с вещами
    cams = {
        "corridors":    [((3.6, 2.4, 2.15), (-2.4, -2.6, 1.05)),
                         ((1.2, 4.0, 2.05), (-2.8, -3.2, 1.15))],
        "poolrooms":    [((3.0, 3.2, 1.95), (-1.6, -2.8, 0.95)),
                         ((-0.2, 4.2, 1.85), (0.4, -3.0, 0.85))],
        "powerstation": [((3.4, 3.0, 2.05), (-1.4, -2.4, 1.15)),
                         ((-2.2, 3.8, 2.15), (1.6, -2.6, 1.05))],
    }[level]
    for i, (cam, tgt) in enumerate(cams[:renders]):
        dx, dy, dz = cam[0] - tgt[0], cam[1] - tgt[1], cam[2] - tgt[2]
        dist = max(0.5, (dx * dx + dy * dy + dz * dz) ** 0.5)
        az = math.degrees(math.atan2(dy, dx))
        el = math.degrees(math.asin(max(-1.0, min(1.0, dz / dist))))
        S.render_preview(os.path.join(PREV, f"level_{tag}_v{i + 1}.png"),
                         target=tgt, distance=dist, height=0.5,
                         samples=28, res=(1180, 664), fov_deg=48,
                         key_energy=2200.0, rim_energy=800.0,
                         floor_z=-0.30, azimuth=az, elevation=el)
    return objs


def build_all(only=None, render=True):
    for level, items in LEVELS.items():
        for name, fn in items:
            if only and name not in only:
                continue
            S.clean_scene()
            m = palette(level)
            obj = fn(m)
            if obj is None:
                continue
            S.smart_uv(obj)
            S.add_bevel(obj, 0.0015, 1)
            S.apply_modifiers(obj)
            S.shade_smooth(obj, 34)
            tris = S.tri_count(obj)
            S.export_fbx([obj], os.path.join(OUT, name + ".fbx"))
            S.export_glb([obj], os.path.join(OUT, name + ".glb"))
            print(f"[levels:{level}] {name}: {tris} трис")
            if render:
                _c, _sz, _ = S.bounds_of([obj])
                _ext = max(_sz.x, _sz.y, _sz.z)
                _pad = 0.72 if _ext < 1.2 else (0.95 if _ext < 2.0 else 1.18)
                S.render_fit(os.path.join(PREV, "_lv_" + name + ".png"), [obj], samples=18,
                             res=(760, 560), fov_deg=30, azimuth=-52.0, elevation=14.0, pad=_pad)


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    only = None
    if "--only" in argv:
        i = argv.index("--only")
        only = set(argv[i + 1:]) or None
    if "--diorama" in argv:
        want = [a for a in argv if a in ("corridors", "poolrooms", "powerstation", "L0", "L37", "L3")]
        alias = {"L0": "corridors", "L37": "poolrooms", "L3": "powerstation"}
        levels = [alias.get(w, w) for w in want] or ["corridors", "poolrooms", "powerstation"]
        for lv in levels:
            diorama(lv)
    else:
        build_all(only=only, render="--no-render" not in argv)
