"""
SUBSISTENCE — models_transport.py  (УЛЬТРА-ДЕТАЛИЗАЦИЯ)
Транспорт и объекты решений 26_transport / 09_connect_levels / 39_trading:
  PR_scooter          — самокат: дека с наждаком и рёбрами, складная стойка со шпилькой,
                        тормоз с тросиком и диском, корзина-решётка, подножка, фара с динамкой;
  PR_loot_cart        — тележка для лута: платформа с рёбрами, 4 поворотные каретки с вилками
                        и тормозами, трубчатая рукоять с резиновыми грипсами, стяжной ремень, ящик;
  PR_minecart         — вагонетка: клёпаная сужающаяся ванна, 4 фланцевых колеса на осях,
                        буферы-сцепки, рычаги, руда внутри, ржавые потёки, габаритный фонарь;
  BD_elevator_car     — кабина лифта по ключ-картам: решётчатые стенки, сварная рама, аккордеонные
                        ворота, панель с кнопками и считывателем карт, знак уровня, поручень;
  PR_vending_machine  — вендинг-автомат: витрина с 4 полками банок, клавиатура, щель для скрапа,
                        лоток выдачи, световая вывеска, вентиляция, кабель.
Приёмы: решётки ставятся отдельным хелпером grille() (панель поворачивается в стену/пол),
все мелкие детали (болты, заклёпки, хомуты) — геометрией, а не текстурами.
Масштаб — метры, низ модели на z=0. Экспорт: FBX + GLB → Models/Props/, превью → docs/previews/.
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
        steel=S.pbr_material("M_Tr_Steel", (0.46, 0.47, 0.50, 1), 0.86, 0.32, noise_scale=60, bump=0.35),
        steel_dark=S.pbr_material("M_Tr_SteelDark", (0.17, 0.175, 0.19, 1), 0.80, 0.42, noise_scale=90, bump=0.45),
        chrome=S.pbr_material("M_Tr_Chrome", (0.78, 0.79, 0.80, 1), 0.98, 0.10),
        paint_green=S.pbr_material("M_Tr_PaintGreen", (0.13, 0.34, 0.24, 1), 0.15, 0.40, noise_scale=70, bump=0.4),
        paint_red=S.pbr_material("M_Tr_PaintRed", (0.55, 0.09, 0.08, 1), 0.12, 0.42, noise_scale=80, bump=0.4),
        paint_yellow=S.pbr_material("M_Tr_PaintYellow", (0.90, 0.72, 0.11, 1), 0.10, 0.38, noise_scale=75, bump=0.4),
        rubber=S.pbr_material("M_Tr_Rubber", (0.045, 0.045, 0.05, 1), 0.0, 0.80, noise_scale=260, bump=0.7),
        grip=S.pbr_material("M_Tr_GripTape", (0.075, 0.075, 0.08, 1), 0.0, 0.94, noise_scale=420, bump=0.95),
        plastic=S.pbr_material("M_Tr_Plastic", (0.10, 0.11, 0.12, 1), 0.0, 0.52, noise_scale=200, bump=0.4),
        wood=S.pbr_material("M_Tr_Wood", (0.36, 0.23, 0.12, 1), 0.0, 0.64, noise_scale=95, bump=0.6),
        wood_pale=S.pbr_material("M_Tr_WoodPale", (0.52, 0.38, 0.22, 1), 0.0, 0.60, noise_scale=120, bump=0.55),
        rust=S.pbr_material("M_Tr_Rust", (0.30, 0.15, 0.07, 1), 0.20, 0.84, noise_scale=180, bump=0.85),
        ore=S.pbr_material("M_Tr_Ore", (0.19, 0.17, 0.16, 1), 0.30, 0.78, noise_scale=160, bump=0.9),
        glass=S.pbr_material("M_Tr_Glass", (0.62, 0.70, 0.75, 1), 0.0, 0.05, alpha=0.12),
        lamp=S.pbr_material("M_Tr_LampPanel", (0.95, 0.97, 0.90, 1), 0.0, 0.20,
                            emission=(1.0, 0.97, 0.82, 1), emission_strength=4.0),
        led_green=S.pbr_material("M_Tr_LedGreen", (0.12, 0.85, 0.35, 1), 0.0, 0.15,
                                 emission=(0.15, 1.0, 0.45, 1), emission_strength=6.0),
        led_red=S.pbr_material("M_Tr_LedRed", (0.85, 0.14, 0.10, 1), 0.0, 0.15,
                               emission=(1.0, 0.18, 0.12, 1), emission_strength=6.0),
        tape=S.pbr_material("M_Tr_ReflectTape", (0.92, 0.92, 0.90, 1), 0.30, 0.26,
                            emission=(0.9, 0.9, 0.85, 1), emission_strength=1.0),
        can=S.pbr_material("M_Tr_Can", (0.70, 0.71, 0.74, 1), 0.82, 0.26, noise_scale=70, bump=0.3),
    )


def _bolt(name, r, h, loc, rot, mat):
    """Болт/заклёпка: стержень + шляпка."""
    return [S.cylinder(name + "_n", r, h, 10, loc=loc, rot=rot, mat=mat),
            S.cylinder(name + "_h", r * 1.4, h * 0.34, 12, loc=loc, rot=rot, mat=mat)]


def grille(name, w, h, loc, rot=None, bars=None, frame=None, body=None):
    """Решётка/сетка нужной ориентации: rot=None — лежит (пол), (90,0,0) — стена лицом по Y,
    (90,0,90) — стена лицом по X."""
    if bars is None:
        bars = max(4, int(w / 0.12))
    return S.grate_panel(name, (w, 0.12, h), bars=bars, loc=loc, mat_frame=frame, mat_glass=body, rot=rot)


def wire_basket(name, w, d, h, loc, mat, mat_rim=None, step=0.085, wire=0.0065, rail=0.011):
    """ОТКРЫТАЯ проволочная корзина: дно-сетка, 4 стенки из вертикальных прутьев, обод и угловые
    стойки. Никаких сплошных панелей — видно содержимое. loc — центр ДНА корзины."""
    rim = mat_rim or mat
    x, y, z = loc
    p = []
    # ---- дно: рама + продольные прутья ----
    p.append(S.box(name + "_bot_f", (w, rail * 1.2, rail), loc=(x, y + d * 0.5, z), mat=rim))
    p.append(S.box(name + "_bot_b", (w, rail * 1.2, rail), loc=(x, y - d * 0.5, z), mat=rim))
    for sx in (-1, 1):
        p.append(S.box(f"{name}_bot_s{sx}", (rail, d, rail), loc=(x + sx * w * 0.5, y, z), mat=rim))
    ny = max(2, int(d / step))
    for j in range(1, ny):
        p.append(S.box(f"{name}_bot_w{j}", (w - rail * 0.8, wire, wire),
                       loc=(x, y - d * 0.5 + d * j / ny, z + 0.003), mat=mat))
    # ---- стенки: вертикальные прутья (перед/зад + борта) ----
    nb = max(3, int(w / step))
    for i in range(1, nb):
        xx = x - w * 0.5 + w * i / nb
        for sy in (-1, 1):
            p.append(S.cylinder(f"{name}_fw{i}{'p' if sy > 0 else 'n'}", wire, h, 8,
                                loc=(xx, y + sy * d * 0.5, z + h * 0.5), mat=mat))
    ns = max(3, int(d / step))
    for j in range(1, ns):
        yy = y - d * 0.5 + d * j / ns
        for sx in (-1, 1):
            p.append(S.cylinder(f"{name}_sw{j}{'p' if sx > 0 else 'n'}", wire, h, 8,
                                loc=(x + sx * w * 0.5, yy, z + h * 0.5), mat=mat))
    # ---- угловые стойки и обод ----
    for sx in (-1, 1):
        for sy in (-1, 1):
            p.append(S.cylinder(f"{name}_post{sx}{sy}", wire * 1.6, h, 10,
                                loc=(x + sx * w * 0.5, y + sy * d * 0.5, z + h * 0.5), mat=rim))
    p.append(S.box(name + "_rim_f", (w + rail, rail * 1.3, rail * 1.3), loc=(x, y + d * 0.5, z + h), mat=rim))
    p.append(S.box(name + "_rim_b", (w + rail, rail * 1.3, rail * 1.3), loc=(x, y - d * 0.5, z + h), mat=rim))
    for sx in (-1, 1):
        p.append(S.box(f"{name}_rim_s{sx}", (rail * 1.3, d + rail, rail * 1.3),
                       loc=(x + sx * w * 0.5, y, z + h), mat=rim))
    return p


# ======================================================================= PR_scooter
def build_scooter(M):
    st, std, gr, rub, pl, ch = M["steel"], M["steel_dark"], M["grip"], M["rubber"], M["plastic"], M["chrome"]
    pnt, led_r, tape, yel, grn = M["paint_red"], M["led_red"], M["tape"], M["paint_yellow"], M["paint_green"]

    p = []
    # ---- дека: плита, наждак, рёбра, жёлтые бортики ----
    p.append(S.box("SC_deck", (0.175, 0.66, 0.030), loc=(0, 0, 0.085), bevel=0.008, mat=st))
    p.append(S.box("SC_griptape", (0.166, 0.645, 0.006), loc=(0, 0, 0.103), mat=gr))
    for i in range(9):
        p.append(S.box(f"SC_gs{i}", (0.150, 0.006, 0.0035), loc=(0, -0.30 + i * 0.075, 0.107), mat=gr))
    for i in range(4):
        p.append(S.box(f"SC_rib{i}", (0.155, 0.028, 0.045), loc=(0, -0.22 + i * 0.147, 0.062), mat=std))
    p.append(S.box("SC_rail_l", (0.020, 0.60, 0.030), loc=(-0.096, 0, 0.062), bevel=0.006, mat=yel))
    p.append(S.box("SC_rail_r", (0.020, 0.60, 0.030), loc=(0.096, 0, 0.062), bevel=0.006, mat=yel))
    p.append(S.box("SC_nose", (0.150, 0.06, 0.05), loc=(0, 0.355, 0.075), rot=(math.radians(-18), 0, 0), bevel=0.01, mat=std))
    p.append(S.box("SC_tail", (0.130, 0.05, 0.04), loc=(0, -0.345, 0.072), rot=(math.radians(14), 0, 0), bevel=0.01, mat=std))
    p.append(S.box("SC_stencil", (0.10, 0.006, 0.028), loc=(0, 0.20, 0.104), mat=M["tape"]))
    for i in range(3):
        p.append(S.box(f"SC_stencil_ln{i}", (0.085, 0.004, 0.004), loc=(0, 0.185 + i * 0.014, 0.106), mat=std))
    p.extend(_bolt("SC_db", 0.006, 0.010, (0.075, 0.24, 0.070), (math.radians(90), 0, 0), std))
    p.extend(_bolt("SC_db2", 0.006, 0.010, (-0.075, -0.22, 0.070), (math.radians(90), 0, 0), std))

    # ---- заднее колесо с тормозным диском ----
    ry = -0.30
    p.append(S.cylinder("SC_wheel_rear", 0.062, 0.038, 28, loc=(0, ry, 0.062), rot=(0, math.radians(90), 0), mat=rub))
    p.append(S.cylinder("SC_tread_rear", 0.066, 0.026, 28, loc=(0, ry, 0.062), rot=(0, math.radians(90), 0), mat=rub))
    p.append(S.cylinder("SC_hub_rear", 0.026, 0.048, 20, loc=(0, ry, 0.062), rot=(0, math.radians(90), 0), mat=st))
    p.append(S.cylinder("SC_disc", 0.038, 0.004, 24, loc=(0.028, ry, 0.062), rot=(0, math.radians(90), 0), mat=ch))
    for i in range(8):
        a = i * math.pi / 4
        p.append(S.cylinder(f"SC_disc_hole{i}", 0.005, 0.008, 8,
                            loc=(0.030, ry + 0.026 * math.cos(a), 0.062 + 0.026 * math.sin(a)),
                            rot=(0, math.radians(90), 0), mat=std))
    p.append(S.cylinder("SC_axle_rear", 0.008, 0.11, 12, loc=(0, ry, 0.062), rot=(0, math.radians(90), 0), mat=std))
    for i in range(6):
        a = i * math.pi / 3
        p.append(S.box(f"SC_spoke_r{i}", (0.010, 0.006, 0.046), loc=(0.021 * math.cos(a), ry + 0.021 * math.sin(a), 0.062),
                       rot=(0, math.radians(90), a), mat=ch))
    for i in range(4):
        a = math.radians(-40 + i * 40)
        p.append(S.box(f"SC_fender{i}", (0.055, 0.032, 0.005), loc=(0.030, ry + 0.075 * math.sin(a) * 0.55, 0.062 + 0.075 * math.cos(a) * 0.55),
                       rot=(a, 0, 0), mat=std))
    p.append(S.box("SC_reflector", (0.014, 0.006, 0.03), loc=(0, ry - 0.075, 0.085), rot=(math.radians(-10), 0, 0), mat=pnt))
    # суппорт дискового тормоза (а не «торчащий прут»)
    p.append(S.box("SC_caliper", (0.022, 0.058, 0.048), loc=(0.042, ry + 0.004, 0.062), bevel=0.004, mat=std))
    p.append(S.box("SC_caliper_arm", (0.012, 0.046, 0.026), loc=(0.052, ry - 0.032, 0.068), rot=(math.radians(-14), 0, 0), bevel=0.003, mat=st))
    p.extend(_bolt("SC_caliper_bolt", 0.005, 0.014, (0.050, ry + 0.004, 0.062), (0, math.radians(90), 0), ch))
    p.append(S.cylinder("SC_torque_arm", 0.007, 0.10, 10, loc=(0.042, ry + 0.075, 0.075), rot=(math.radians(70), 0, 0), mat=st))

    # ---- переднее колесо ----
    fy = 0.34
    p.append(S.cylinder("SC_wheel_front", 0.075, 0.042, 30, loc=(0, fy, 0.075), rot=(0, math.radians(90), 0), mat=rub))
    p.append(S.cylinder("SC_tread_front", 0.080, 0.030, 30, loc=(0, fy, 0.075), rot=(0, math.radians(90), 0), mat=rub))
    p.append(S.cylinder("SC_hub_front", 0.030, 0.052, 22, loc=(0, fy, 0.075), rot=(0, math.radians(90), 0), mat=st))
    p.append(S.cylinder("SC_hubcap_front1", 0.017, 0.058, 18, loc=(-0.030, fy, 0.075), rot=(0, math.radians(90), 0), mat=ch))
    p.append(S.cylinder("SC_hubcap_front2", 0.017, 0.058, 18, loc=(0.030, fy, 0.075), rot=(0, math.radians(90), 0), mat=ch))
    for i in range(8):
        a = i * math.pi / 4
        p.append(S.box(f"SC_spoke{i}", (0.012, 0.007, 0.056), loc=(0.0235 * math.cos(a), fy + 0.0235 * math.sin(a), 0.075),
                       rot=(0, math.radians(90), a), mat=ch))
    p.append(S.cylinder("SC_axle_front", 0.009, 0.20, 12, loc=(0, fy, 0.075), rot=(0, math.radians(90), 0), mat=std))

    # ---- вилка ----
    for sx in (-1, 1):
        p.append(S.cylinder(f"SC_fork{sx}", 0.011, 0.30, 14, loc=(sx * 0.055, fy - 0.02, 0.20),
                            rot=(math.radians(12), 0, 0), mat=st))
        p.append(S.cylinder(f"SC_fork_blade{sx}", 0.014, 0.10, 14, loc=(sx * 0.055, fy + 0.01, 0.10), mat=st))
    p.append(S.box("SC_crown", (0.135, 0.10, 0.035), loc=(0, fy - 0.045, 0.335), bevel=0.008, mat=std))
    p.append(S.box("SC_fork_plate", (0.10, 0.07, 0.014), loc=(0, fy - 0.06, 0.352), bevel=0.005, mat=st))

    # ---- стойка, складной узел, замок ----
    p.append(S.cylinder("SC_stem", 0.017, 0.52, 18, loc=(0, fy - 0.075, 0.60), rot=(math.radians(6), 0, 0), mat=st))
    p.append(S.box("SC_hinge_a", (0.052, 0.075, 0.038), loc=(0, fy - 0.055, 0.80), rot=(math.radians(6), 0, 0), bevel=0.008, mat=std))
    p.append(S.box("SC_hinge_b", (0.044, 0.062, 0.030), loc=(0, fy - 0.075, 0.845), rot=(math.radians(6), 0, 0), bevel=0.008, mat=std))
    p.append(S.cylinder("SC_hinge_pin", 0.008, 0.075, 12, loc=(0, fy - 0.065, 0.822), rot=(0, math.radians(90), 0), mat=ch))
    p.append(S.box("SC_latch", (0.022, 0.05, 0.055), loc=(0.030, fy - 0.060, 0.79), bevel=0.006, mat=std))
    p.append(S.cylinder("SC_seatclamp", 0.024, 0.045, 16, loc=(0, fy - 0.088, 0.865), rot=(math.radians(6), 0, 0), mat=st))
    p.append(S.box("SC_clamp_bolt", (0.030, 0.018, 0.016), loc=(0, fy - 0.105, 0.868), bevel=0.003, mat=std))
    p.append(S.box("SC_stem_tape", (0.038, 0.008, 0.10), loc=(0, fy - 0.093, 0.53), mat=tape))

    # ---- Т-образный руль ----
    p.append(S.cylinder("SC_bar", 0.0155, 0.46, 18, loc=(0, fy - 0.092, 0.90), rot=(0, math.radians(90), 0), mat=st))
    for sx in (-1, 1):
        p.append(S.capsule(f"SC_grip{sx}", 0.022, 0.13, loc=(sx * 0.165, fy - 0.092, 0.90),
                           rot=(0, math.radians(90), 0), mat=rub, r1=0.023, r2=0.021))
        for i in range(3):
            p.append(S.cylinder(f"SC_gripring{sx}_{i}", 0.0242, 0.006, 16,
                                loc=(sx * (0.120 + i * 0.042), fy - 0.092, 0.90), rot=(0, math.radians(90), 0), mat=rub))
        p.append(S.cylinder(f"SC_barend{sx}", 0.019, 0.014, 14, loc=(sx * 0.232, fy - 0.092, 0.90), rot=(0, math.radians(90), 0), mat=ch))
        p.append(S.cylinder(f"SC_brake_ring{sx}", 0.019, 0.024, 16, loc=(sx * 0.085, fy - 0.092, 0.90), rot=(0, math.radians(90), 0), mat=std))
    p.append(S.box("SC_lever", (0.016, 0.115, 0.014), loc=(0.125, fy - 0.145, 0.895), rot=(math.radians(-8), 0, 0), bevel=0.006, mat=st))
    p.append(S.cylinder("SC_lever_pivot", 0.009, 0.03, 12, loc=(0.115, fy - 0.10, 0.895), rot=(0, math.radians(90), 0), mat=std))
    p.append(S.box("SC_lever_tip", (0.020, 0.030, 0.016), loc=(0.128, fy - 0.198, 0.898), bevel=0.005, mat=pl))
    p.append(S.tube("SC_cable", [(0.115, fy - 0.115, 0.895), (0.075, fy - 0.135, 0.80), (0.045, fy - 0.10, 0.62),
                                 (0.030, fy + 0.02, 0.42), (0.020, fy + 0.115, 0.24), (0.012, fy + 0.16, 0.135)],
                    0.005, mat=pl, resolution=6))
    p.append(S.cylinder("SC_cable_ferrule", 0.008, 0.022, 12, loc=(0.012, fy + 0.16, 0.135), rot=(math.radians(60), 0, 0), mat=st))

    # ---- подножка (убирается под деку) ----
    p.append(S.tube("SC_kick", [(-0.082, -0.175, 0.078), (-0.130, -0.215, 0.038), (-0.148, -0.238, 0.012)],
                    0.011, mat=st, resolution=8))
    p.append(S.box("SC_kick_foot", (0.030, 0.062, 0.012), loc=(-0.150, -0.246, 0.008), bevel=0.004, mat=std))
    p.append(S.cylinder("SC_kick_spring", 0.014, 0.028, 14, loc=(-0.086, -0.182, 0.092), rot=(math.radians(34), 0, math.radians(-18)), mat=std))
    p.append(S.box("SC_kick_bracket", (0.024, 0.05, 0.035), loc=(-0.078, -0.168, 0.088), bevel=0.004, mat=std))

    # ---- корзина-сетка спереди (открытая, видно содержимое) ----
    bw, bd, bh = 0.30, 0.21, 0.21
    bz = 0.50
    p.extend(wire_basket("SC_basket", bw, bd, bh, (0, fy + 0.14, bz), grn, mat_rim=grn, step=0.075))
    # крепление корзины к вилке/короне
    for sx in (-1, 1):
        p.append(S.box(f"SC_basket_arm{sx}", (0.018, 0.10, 0.016), loc=(sx * 0.10, fy + 0.045, bz - 0.004), bevel=0.003, mat=std))
    p.append(S.box("SC_basket_mount", (0.05, 0.11, 0.022), loc=(0, fy - 0.035, bz - 0.006), bevel=0.004, mat=std))
    p.append(S.box("SC_fork_pad", (0.11, 0.02, 0.07), loc=(0, fy + 0.01, bz + 0.02), mat=rub))

    # ---- фара на короне вилки (под корзиной, светит вперёд) + динамка ----
    p.append(S.box("SC_lamp_bracket", (0.024, 0.062, 0.055), loc=(0, fy + 0.004, 0.386), bevel=0.003, mat=std))
    p.append(S.box("SC_lamp", (0.060, 0.052, 0.050), loc=(0, fy + 0.075, 0.408), bevel=0.008, mat=std))
    p.append(S.cylinder("SC_lamp_shell", 0.025, 0.03, 18, loc=(0, fy + 0.100, 0.408), rot=(math.radians(90), 0, 0), mat=ch))
    p.append(S.sphere("SC_lamp_lens", 0.022, subdiv=0, loc=(0, fy + 0.112, 0.408), mat=M["lamp"], u_seg=16, v_seg=10))
    p.append(S.box("SC_lamp_switch", (0.022, 0.014, 0.008), loc=(0, fy + 0.072, 0.436), mat=led_r))
    p.extend(_bolt("SC_lamp_bolt", 0.005, 0.012, (0, fy - 0.012, 0.372), (math.radians(90), 0, 0), ch))
    p.append(S.tube("SC_lamp_wire", [(0.020, fy + 0.045, 0.40), (0.030, fy - 0.02, 0.50), (0.026, ry + 0.06, 0.09)],
                    0.004, mat=pl, resolution=6))
    p.append(S.box("SC_taillight", (0.052, 0.012, 0.030), loc=(0, ry - 0.082, 0.108), mat=led_r))
    p.append(S.box("SC_taillight_ring", (0.062, 0.006, 0.040), loc=(0, ry - 0.076, 0.108), mat=std))
    p.append(S.box("SC_dyno", (0.035, 0.05, 0.035), loc=(0, ry + 0.055, 0.075), bevel=0.005, mat=pl))
    p.append(S.cylinder("SC_sensor", 0.012, 0.02, 12, loc=(0, ry + 0.085, 0.075), rot=(math.radians(90), 0, 0), mat=led_r))
    p.append(S.box("SC_tape_l", (0.008, 0.10, 0.006), loc=(-0.088, fy - 0.02, 0.115), mat=tape))
    p.append(S.box("SC_tape_r", (0.008, 0.10, 0.006), loc=(0.088, fy - 0.02, 0.115), mat=tape))
    return p


# ==================================================================== PR_loot_cart
def build_loot_cart(M):
    st, std, rub, wd, wp = M["steel"], M["steel_dark"], M["rubber"], M["wood"], M["wood_pale"]
    gr, tape, yel, rdy, ch, pl = M["grip"], M["tape"], M["paint_yellow"], M["paint_red"], M["chrome"], M["plastic"]

    p = []
    # ---- платформа ----
    p.append(S.box("CT_deck", (0.64, 1.06, 0.05), loc=(0, 0, 0.30), bevel=0.008, mat=st))
    p.append(S.box("CT_deck_top", (0.60, 1.02, 0.006), loc=(0, 0, 0.328), mat=gr))
    for i in range(6):
        p.append(S.box(f"CT_rib{i}", (0.60, 0.028, 0.055), loc=(0, -0.42 + i * 0.168, 0.255), mat=std))
    p.append(S.box("CT_edge_front", (0.66, 0.035, 0.075), loc=(0, 0.535, 0.315), bevel=0.008, mat=yel))
    p.append(S.box("CT_edge_back", (0.66, 0.035, 0.075), loc=(0, -0.535, 0.315), bevel=0.008, mat=yel))
    for sx in (-1, 1):
        p.append(S.box(f"CT_edge_side{sx}", (0.035, 1.06, 0.075), loc=(sx * 0.325, 0, 0.315), bevel=0.008, mat=yel))
        for i in range(3):
            p.append(S.box(f"CT_tape{sx}_{i}", (0.006, 0.16, 0.03), loc=(sx * 0.345, -0.34 + i * 0.34, 0.315), mat=tape))
        for sy in (-1, 1):
            p.append(S.box(f"CT_hook{sx}{sy}", (0.026, 0.026, 0.09), loc=(sx * 0.29, sy * 0.50, 0.355), bevel=0.006, mat=ch))
    for i in range(3):
        p.append(S.box(f"CT_tape_f{i}", (0.16, 0.006, 0.03), loc=(-0.16 + i * 0.16, 0.552, 0.315), mat=tape))
    p.append(S.box("CT_plate", (0.22, 0.006, 0.13), loc=(0, 0.554, 0.28), mat=M["tape"]))

    # ---- угловые стойки ----
    for sx in (-1, 1):
        for sy in (-1, 1):
            p.append(S.cylinder(f"CT_post{sx}{sy}", 0.019, 0.40, 16, loc=(sx * 0.295, sy * 0.49, 0.52), mat=st))
            p.append(S.sphere(f"CT_post_cap{sx}{sy}", 0.021, subdiv=0, loc=(sx * 0.295, sy * 0.49, 0.72), mat=std, u_seg=14, v_seg=8))
            p.extend(_bolt(f"CT_post_bolt{sx}{sy}", 0.006, 0.012, (sx * 0.295, sy * 0.49, 0.34), (0, 0, 0), std))

    # ---- перила по бортам ----
    for sx in (-1, 1):
        p.append(S.tube(f"CT_rail{sx}", [(sx * 0.295, 0.49, 0.70), (sx * 0.295, 0.20, 0.66),
                                         (sx * 0.295, -0.20, 0.66), (sx * 0.295, -0.49, 0.70)], 0.012, mat=ch, resolution=8))
        for sy in (-1, 1):
            p.append(S.cylinder(f"CT_rail_mount{sx}{sy}", 0.010, 0.07, 12, loc=(sx * 0.295, sy * 0.42, 0.68), mat=std))

    # ---- рукоять: две наклонные трубы НАЗАД (30°), верхняя перекладина, грипсы, укосины и тормоз ----
    ang = math.radians(30)
    for sx in (-1, 1):
        p.append(S.cylinder(f"CT_handle{sx}", 0.017, 0.72, 16, loc=(sx * 0.235, -0.68, 0.64), rot=(ang, 0, 0), mat=st))
        p.append(S.box(f"CT_handle_base{sx}", (0.052, 0.09, 0.05), loc=(sx * 0.235, -0.505, 0.345),
                       rot=(ang, 0, 0), bevel=0.006, mat=std))
        p.append(S.tube(f"CT_brace{sx}", [(sx * 0.235, -0.72, 0.70), (sx * 0.295, -0.50, 0.355)],
                        0.011, mat=std, resolution=8))
        p.extend(_bolt(f"CT_handle_bolt{sx}", 0.006, 0.014, (sx * 0.235, -0.50, 0.325), (0, 0, 0), ch))
    p.append(S.cylinder("CT_handle_top", 0.017, 0.56, 16, loc=(0, -0.86, 0.952), rot=(0, math.radians(90), 0), mat=st))
    p.append(S.cylinder("CT_handle_mid", 0.012, 0.47, 14, loc=(0, -0.755, 0.72), rot=(0, math.radians(90), 0), mat=std))
    for sx in (-1, 1):
        p.append(S.capsule(f"CT_grip{sx}", 0.022, 0.16, loc=(sx * 0.17, -0.86, 0.952),
                           rot=(0, math.radians(90), 0), mat=rub, r1=0.024, r2=0.022))
        for i in range(3):
            p.append(S.cylinder(f"CT_gripring{sx}{i}", 0.0252, 0.007, 16, loc=(sx * (0.115 + i * 0.05), -0.86, 0.952),
                                rot=(0, math.radians(90), 0), mat=rub))
        p.append(S.cylinder(f"CT_barend{sx}", 0.021, 0.016, 14, loc=(sx * 0.286, -0.86, 0.952),
                            rot=(0, math.radians(90), 0), mat=ch))
        p.append(S.box(f"CT_brake{sx}", (0.012, 0.085, 0.046), loc=(sx * 0.115, -0.805, 0.888),
                       rot=(math.radians(-42), 0, 0), bevel=0.004, mat=std))
        p.append(S.cylinder(f"CT_brake_pivot{sx}", 0.009, 0.026, 12, loc=(sx * 0.112, -0.845, 0.925),
                            rot=(0, math.radians(90), 0), mat=ch))
    p.append(S.box("CT_handle_plate", (0.50, 0.035, 0.10), loc=(0, -0.755, 0.72), bevel=0.005, mat=std))
    p.append(S.box("CT_plate_sign", (0.34, 0.006, 0.05), loc=(0, -0.777, 0.72), mat=yel))
    p.append(S.tube("CT_cable", [(0.112, -0.85, 0.93), (0.10, -0.90, 0.80), (0.085, -0.86, 0.60),
                                 (0.06, -0.78, 0.45), (0.03, -0.55, 0.33)], 0.005, mat=pl, resolution=6))

    # ---- 4 поворотные каретки ----
    for sx in (-1, 1):
        for sy in (-1, 1):
            x, y = sx * 0.245, sy * 0.42
            p.append(S.cylinder(f"CT_kingpin{sx}{sy}", 0.014, 0.06, 14, loc=(x, y, 0.245), mat=st))
            p.append(S.box(f"CT_swivel{sx}{sy}", (0.07, 0.10, 0.03), loc=(x, y - sy * 0.02, 0.215), bevel=0.006, mat=std))
            for k in (-1, 1):
                p.append(S.box(f"CT_fork{sx}{sy}{k}", (0.012, 0.055, 0.10), loc=(x + k * 0.035, y - 0.03, 0.16), bevel=0.004, mat=std))
            p.append(S.cylinder(f"CT_wheel{sx}{sy}", 0.058, 0.036, 24, loc=(x, y - 0.03, 0.058), rot=(0, math.radians(90), 0), mat=rub))
            p.append(S.cylinder(f"CT_wheel_hub{sx}{sy}", 0.024, 0.044, 18, loc=(x, y - 0.03, 0.058), rot=(0, math.radians(90), 0), mat=ch))
            p.append(S.cylinder(f"CT_caster_axle{sx}{sy}", 0.007, 0.10, 12, loc=(x, y - 0.03, 0.058), rot=(0, math.radians(90), 0), mat=std))
            p.append(S.box(f"CT_caster_brake{sx}{sy}", (0.030, 0.055, 0.014), loc=(x, y - 0.075, 0.098),
                           rot=(math.radians(-20), 0, 0), bevel=0.003, mat=rdy))

    # ---- груз: ящик со стяжным ремнём ----
    p.append(S.box("CT_crate", (0.46, 0.56, 0.34), loc=(0, 0.06, 0.50), bevel=0.008, mat=wd))
    for i in range(4):
        p.append(S.box(f"CT_crate_slat{i}", (0.47, 0.035, 0.02), loc=(0, -0.19 + i * 0.17, 0.50), mat=wp))
    for sx in (-1, 1):
        p.append(S.box(f"CT_crate_post{sx}", (0.03, 0.02, 0.35), loc=(sx * 0.225, -0.275, 0.50), mat=wp))
        p.append(S.box(f"CT_crate_post2{sx}", (0.03, 0.02, 0.35), loc=(sx * 0.225, 0.275, 0.50), mat=wp))
    p.append(S.box("CT_crate_lid", (0.47, 0.57, 0.02), loc=(0, 0.06, 0.675), bevel=0.005, mat=wp))
    p.append(S.box("CT_strap", (0.05, 0.60, 0.008), loc=(0, 0.06, 0.686), mat=rub))
    p.append(S.box("CT_buckle", (0.07, 0.05, 0.016), loc=(0, 0.34, 0.690), bevel=0.004, mat=ch))
    p.append(S.box("CT_crate_mark", (0.20, 0.004, 0.13), loc=(0, -0.222, 0.52), mat=yel))
    p.append(S.box("CT_crate_mark2", (0.16, 0.004, 0.06), loc=(0, -0.222, 0.60), mat=M["tape"]))
    p.extend(_bolt("CT_bucket", 0.007, 0.014, (0.20, 0.30, 0.686), (0, 0, 0), ch))
    return p


# ==================================================================== PR_minecart
def build_minecart(M):
    st, std, rub, rdy, ore = M["steel"], M["steel_dark"], M["rubber"], M["rust"], M["ore"]
    green, tape, lamp, ch = M["paint_green"], M["tape"], M["lamp"], M["chrome"]

    p = []
    # ---- ванна ----
    p.append(S.box("MC_tub", (1.06, 1.44, 0.62), loc=(0, 0, 0.72), bevel=0.02, segments=3, mat=green, taper=(0.86, 0.88)))
    p.append(S.box("MC_tub_floor", (0.98, 1.36, 0.04), loc=(0, 0, 0.43), bevel=0.008, mat=std))
    p.append(S.box("MC_tub_rim", (1.10, 1.48, 0.05), loc=(0, 0, 1.03), bevel=0.012, mat=std))
    for sy in (-1, 1):
        p.append(S.box(f"MC_side{sy}", (1.06, 0.05, 0.60), loc=(0, sy * 0.72, 0.72),
                       rot=(math.radians(sy * -13), 0, 0), bevel=0.01, mat=green))
    for sx in (-1, 1):
        p.append(S.box(f"MC_end{sx}", (0.05, 1.40, 0.60), loc=(sx * 0.53, 0, 0.72),
                       rot=(0, math.radians(sx * 8), 0), bevel=0.01, mat=green))
    # потёки ржавчины (вровень с наклонной стенкой)
    for sy in (-1, 1):
        for i in range(3):
            z = 0.60 - i * 0.05
            p.append(S.box(f"MC_rust{sy}{i}", (0.05 + 0.02 * i, 0.012, 0.20 - 0.03 * i),
                           loc=(-0.35 + i * 0.36, sy * (0.72 + 0.225 * (z - 0.72) + 0.024), z),
                           rot=(math.radians(sy * -13), 0, 0), mat=rdy))
    # ---- рёбра и заклёпки ----
    for sx in (-1, 1):
        for i in range(5):
            p.append(S.box(f"MC_rib{sx}{i}", (0.030, 0.032, 0.60), loc=(sx * 0.545, -0.56 + i * 0.28, 0.72),
                           rot=(0, math.radians(sx * 8), 0), mat=std))
    for sy in (-1, 1):
        for i in range(7):
            x = -0.42 + i * 0.14
            for z, tag in ((0.93, "a"), (0.50, "b")):
                ys = 0.72 + 0.225 * (z - 0.72) + 0.024
                p.extend(_bolt(f"MC_rv{tag}{sy}{i}", 0.008, 0.016, (x, sy * ys, z), (math.radians(90), 0, 0), std))
    for sx in (-1, 1):
        for i in range(5):
            y = -0.56 + i * 0.28
            z = 0.86
            p.extend(_bolt(f"MC_rve{sx}{i}", 0.008, 0.016, (sx * (0.53 + 0.139 * (z - 0.72) + 0.024), y, z),
                           (0, math.radians(90), 0), std))
    # ---- поручни-скобы по бортам (на ободе, а не в воздухе) ----
    for sy in (-1, 1):
        p.append(S.cylinder(f"MC_grip{sy}", 0.017, 0.40, 16, loc=(0, sy * 0.705, 1.115), rot=(0, math.radians(90), 0), mat=ch))
        for sx in (-1, 1):
            p.append(S.box(f"MC_grip_st{sy}{sx}", (0.030, 0.05, 0.10), loc=(sx * 0.175, sy * 0.72, 1.06), bevel=0.004, mat=st))
            p.extend(_bolt(f"MC_grip_bolt{sy}{sx}", 0.006, 0.014, (sx * 0.175, sy * 0.70, 1.02), (0, 0, 0), std))
        p.append(S.box(f"MC_grip_pad{sy}", (0.26, 0.055, 0.02), loc=(0, sy * 0.70, 1.02), mat=rub))
    # ---- руда внутри (ниже кромки) ----
    p.append(S.sphere("MC_load", 0.40, subdiv=0, loc=(0, 0, 0.60), scale=(1.15, 1.55, 0.34), mat=ore, u_seg=26, v_seg=16))
    for i in range(9):
        a = i * 0.7
        p.append(S.sphere(f"MC_chunk{i}", 0.07 + 0.02 * (i % 3), subdiv=0,
                          loc=(0.30 * math.cos(a), 0.48 * math.sin(a), 0.70 + 0.03 * (i % 2)),
                          scale=(1.3, 1.1, 0.75), mat=std if i % 3 == 0 else ore, u_seg=16, v_seg=10))

    # ---- рама, оси, колёса, буферы ----
    p.append(S.box("MC_frame", (0.92, 1.60, 0.09), loc=(0, 0, 0.40), bevel=0.012, mat=std))
    for sy in (-1, 1):
        p.append(S.box(f"MC_axlebox{sy}", (0.26, 0.12, 0.14), loc=(0, sy * 0.70, 0.34), bevel=0.01, mat=std))
        p.append(S.cylinder(f"MC_axle{sy}", 0.030, 1.10, 20, loc=(0, sy * 0.70, 0.30), rot=(0, math.radians(90), 0), mat=st))
    for sy in (-1, 1):
        for sx in (-1, 1):
            x, y = sx * 0.52, sy * 0.70
            p.append(S.cylinder(f"MC_wheel{sx}{sy}", 0.155, 0.065, 32, loc=(x, y, 0.30), rot=(0, math.radians(90), 0), mat=std))
            p.append(S.cylinder(f"MC_flange{sx}{sy}", 0.185, 0.014, 32, loc=(x, y, 0.30), rot=(0, math.radians(90), 0), mat=st))
            p.append(S.cylinder(f"MC_hubcap{sx}{sy}", 0.055, 0.075, 20, loc=(x, y, 0.30), rot=(0, math.radians(90), 0), mat=ch))
            for i in range(6):
                a = i * math.pi / 3
                p.append(S.cylinder(f"MC_wbolt{sx}{sy}{i}", 0.009, 0.02, 10,
                                    loc=(x + 0.078 * math.cos(a), y + 0.078 * math.sin(a), 0.30),
                                    rot=(0, math.radians(90), 0), mat=ch))
    for sx in (-1, 1):
        p.append(S.cylinder(f"MC_buffer{sx}", 0.070, 0.09, 20, loc=(sx * 0.60, 0, 0.42), rot=(0, math.radians(90), 0), mat=st))
        p.append(S.box(f"MC_coupler{sx}", (0.06, 0.16, 0.09), loc=(sx * 0.66, 0, 0.42), bevel=0.01, mat=std))
        p.append(S.box(f"MC_pin{sx}", (0.03, 0.05, 0.16), loc=(sx * 0.72, 0, 0.42), bevel=0.006, mat=ch))
        p.append(S.box(f"MC_coupler_hole{sx}", (0.02, 0.02, 0.05), loc=(sx * 0.755, 0, 0.42), mat=std))
    p.append(S.box("MC_tape_l", (0.006, 1.32, 0.045), loc=(-0.565, 0, 0.62), mat=tape))
    p.append(S.box("MC_tape_r", (0.006, 1.32, 0.045), loc=(0.565, 0, 0.62), mat=tape))
    # габаритный фонарь — на переднем ободе, светит вперёд (+Y)
    p.append(S.box("MC_lamp_post", (0.036, 0.036, 0.20), loc=(0.34, 0.58, 1.15), bevel=0.004, mat=std))
    p.append(S.box("MC_lamp_arm", (0.030, 0.15, 0.030), loc=(0.34, 0.665, 1.245), bevel=0.004, mat=std))
    p.append(S.box("MC_lamp", (0.13, 0.09, 0.11), loc=(0.34, 0.735, 1.205), bevel=0.008, mat=std))
    p.append(S.cylinder("MC_lamp_shell", 0.048, 0.03, 18, loc=(0.34, 0.786, 1.205), rot=(math.radians(90), 0, 0), mat=ch))
    p.append(S.box("MC_lamp_lens", (0.098, 0.02, 0.074), loc=(0.34, 0.80, 1.205), mat=lamp))
    p.append(S.box("MC_lamp_hood", (0.155, 0.115, 0.02), loc=(0.34, 0.735, 1.272), rot=(math.radians(16), 0, 0), bevel=0.005, mat=std))
    p.append(S.box("MC_lamp_switch", (0.018, 0.014, 0.026), loc=(0.275, 0.735, 1.205), mat=M["led_red"]))
    p.append(S.box("MC_plate", (0.34, 0.004, 0.16), loc=(0, -(0.72 + 0.225 * 0.08 + 0.026), 0.80),
                   rot=(math.radians(13), 0, 0), mat=M["paint_yellow"]))
    p.append(S.box("MC_plate2", (0.30, 0.004, 0.10), loc=(0, 0.72 + 0.225 * 0.0 + 0.026, 0.72),
                   rot=(math.radians(-13), 0, 0), mat=M["tape"]))
    return p


# =============================================================== BD_elevator_car
def build_elevator_car(M):
    st, std, gr, rub = M["steel"], M["steel_dark"], M["grip"], M["rubber"]
    yel, lamp, led_g, led_r, tape, ch = M["paint_yellow"], M["lamp"], M["led_green"], M["led_red"], M["tape"], M["chrome"]

    W, D, H = 2.10, 2.10, 2.55
    p = []
    # ---- пол: рифлёная плита + решётка сверху + жёлтые пороги ----
    p.append(S.box("EL_floor", (W, D, 0.08), loc=(0, 0, 0.04), bevel=0.008, mat=std))
    p.extend(grille("EL_floor_grate", W - 0.24, D - 0.24, loc=(0, 0, 0.115), frame=st, body=gr, bars=8))
    for sy in (-1, 1):
        p.append(S.box(f"EL_threshold{sy}", (W, 0.10, 0.02), loc=(0, sy * (D / 2 - 0.12), 0.092), mat=yel))
    # ---- рама ----
    for sx in (-1, 1):
        for sy in (-1, 1):
            p.append(S.box(f"EL_post{sx}{sy}", (0.10, 0.10, H), loc=(sx * (W / 2 - 0.05), sy * (D / 2 - 0.05), H / 2), bevel=0.008, mat=st))
            p.append(S.box(f"EL_gusset{sx}{sy}", (0.16, 0.16, 0.03), loc=(sx * (W / 2 - 0.06), sy * (D / 2 - 0.06), 0.10), bevel=0.004, mat=std))
            p.extend(_bolt(f"EL_post_bolt{sx}{sy}", 0.008, 0.018, (sx * (W / 2 - 0.05), sy * (D / 2 - 0.05), 1.60), (math.radians(90), 0, 0), std))
    p.append(S.box("EL_topframe_x", (W, 0.09, 0.09), loc=(0, -D / 2 + 0.045, H - 0.05), bevel=0.008, mat=st))
    p.append(S.box("EL_topframe_x2", (W, 0.09, 0.09), loc=(0, D / 2 - 0.045, H - 0.05), bevel=0.008, mat=st))
    p.append(S.box("EL_topframe_y", (0.09, D, 0.09), loc=(-W / 2 + 0.045, 0, H - 0.05), bevel=0.008, mat=st))
    p.append(S.box("EL_topframe_y2", (0.09, D, 0.09), loc=(W / 2 - 0.045, 0, H - 0.05), bevel=0.008, mat=st))
    p.append(S.box("EL_roof", (W, D, 0.06), loc=(0, 0, H - 0.10), bevel=0.008, mat=std))
    p.append(S.box("EL_roof_rib1", (W, 0.06, 0.05), loc=(0, 0, H - 0.145), mat=st))
    p.append(S.box("EL_roof_rib2", (0.06, D, 0.05), loc=(0, 0, H - 0.145), mat=st))
    p.extend(grille("EL_vent", 0.60, 0.60, loc=(0.55, 0.55, H - 0.16), frame=st, body=std, bars=6))
    # ---- решётчатые стены: задняя и две боковые ----
    p.extend(grille("EL_wall_back", W - 0.16, H - 0.34, loc=(0, -D / 2 + 0.075, H / 2 - 0.05),
                    rot=(math.radians(90), 0, 0), frame=st, body=std, bars=12))
    for sx in (-1, 1):
        p.extend(grille(f"EL_wall_side{sx}", D - 0.16, H - 0.34, loc=(sx * (W / 2 - 0.075), 0, H / 2 - 0.05),
                        rot=(math.radians(90), 0, math.radians(90)), frame=st, body=std, bars=12))
        p.append(S.box(f"EL_sidebrace{sx}", (0.035, D - 0.30, 0.05), loc=(sx * (W / 2 - 0.055), 0, 1.15), mat=std))
        p.append(S.box(f"EL_sidebrace2{sx}", (0.035, D - 0.30, 0.05), loc=(sx * (W / 2 - 0.055), 0, 0.45), mat=std))
    # ---- аккордеонные ворота ----
    for i in range(9):
        x = -0.72 + i * 0.18
        p.append(S.box(f"EL_gate_v{i}", (0.028, 0.035, H - 0.40), loc=(x, D / 2 - 0.075, H / 2 - 0.06),
                       rot=(0, math.radians(12 if i % 2 else -12), 0), bevel=0.004, mat=st))
    for k in (-1, 1):
        p.append(S.box(f"EL_gate_rail{k}", (W - 0.12, 0.045, 0.045), loc=(0, D / 2 - 0.075, H / 2 - 0.06 - k * (H - 0.44) * 0.5),
                       rot=(0, math.radians(-8 * k), 0), mat=std))
    p.append(S.box("EL_gate_lock", (0.05, 0.06, 0.10), loc=(0.60, D / 2 - 0.09, 1.05), bevel=0.006, mat=std))
    p.append(S.cylinder("EL_gate_handle", 0.014, 0.12, 12, loc=(0.72, D / 2 - 0.10, 1.05), mat=ch))

    # ---- панель управления ----
    p.append(S.box("EL_panel", (0.22, 0.14, 0.44), loc=(-(W / 2 - 0.13), D / 2 - 0.16, 1.32), bevel=0.008, mat=std))
    p.append(S.box("EL_panel_face", (0.19, 0.02, 0.40), loc=(-(W / 2 - 0.11), D / 2 - 0.225, 1.32), bevel=0.004, mat=st))
    for i, col in enumerate([led_g, led_r, led_g, led_r]):
        p.append(S.cylinder(f"EL_btn{i}", 0.014, 0.012, 14,
                            loc=(-(W / 2 - 0.15 + (i % 2) * 0.06), D / 2 - 0.235, 1.45 - (i // 2) * 0.06),
                            rot=(math.radians(90), 0, 0), mat=col))
    p.append(S.box("EL_display", (0.13, 0.012, 0.05), loc=(-(W / 2 - 0.14), D / 2 - 0.235, 1.55), mat=led_g))
    p.append(S.box("EL_reader", (0.12, 0.035, 0.16), loc=(-(W / 2 - 0.14), D / 2 - 0.235, 1.16), bevel=0.005, mat=std))
    p.append(S.box("EL_reader_slot", (0.09, 0.012, 0.018), loc=(-(W / 2 - 0.14), D / 2 - 0.253, 1.13), mat=led_g))
    # ---- знак уровня + светильник ----
    p.append(S.box("EL_sign", (0.54, 0.03, 0.22), loc=(0, D / 2 - 0.075, H - 0.36), mat=std))
    p.append(S.box("EL_sign_face", (0.46, 0.012, 0.14), loc=(0, D / 2 - 0.092, H - 0.36), mat=lamp))
    p.append(S.box("EL_sign_arm", (0.06, 0.16, 0.03), loc=(0, D / 2 - 0.16, H - 0.30), mat=std))
    p.append(S.box("EL_ceiling_lamp", (0.74, 0.36, 0.07), loc=(0, 0.30, H - 0.20), bevel=0.008, mat=std))
    p.append(S.box("EL_lamp_lens", (0.68, 0.30, 0.03), loc=(0, 0.30, H - 0.245), mat=lamp))
    for i in range(7):
        p.append(S.box(f"EL_lamp_bar{i}", (0.68, 0.018, 0.018), loc=(0, 0.30 - 0.12 + i * 0.04, H - 0.26), mat=std))
    # ---- поручень, кабель-канал, разметка ----
    # поручни в два уровня + 5 стоек (раньше висели в воздухе)
    for lvl, zz in ((0, 1.00), (1, 0.52)):
        p.append(S.tube(f"EL_handrail{lvl}", [(-0.75, -D / 2 + 0.16, zz), (0, -D / 2 + 0.125, zz + 0.03),
                                              (0.75, -D / 2 + 0.16, zz)], 0.017, mat=ch, resolution=8))
    for i in range(5):
        x = -0.75 + i * 0.375
        p.append(S.cylinder(f"EL_handrail_post{i}", 0.014, 1.12, 12, loc=(x, -D / 2 + 0.165, 0.56), mat=std))
        p.extend(_bolt(f"EL_handrail_bolt{i}", 0.006, 0.012, (x, -D / 2 + 0.10, 1.00), (math.radians(90), 0, 0), std))
    p.append(S.box("EL_conduit", (0.06, 0.06, H - 0.30), loc=(-(W / 2 - 0.07), -D / 2 + 0.10, H / 2 - 0.10), mat=std))
    p.append(S.tube("EL_conduit_bend", [(-(W / 2 - 0.07), -D / 2 + 0.10, H - 0.20), (-(W / 2 - 0.07), -D / 2 + 0.10, H - 0.13),
                                        (-(W / 2 - 0.20), -D / 2 + 0.10, H - 0.10)], 0.02, mat=std, resolution=6))
    for sx in (-1, 1):
        p.append(S.box(f"EL_tape{sx}", (0.008, D - 0.20, 0.04), loc=(sx * (W / 2 - 0.075), 0, 0.075), mat=tape))
    p.append(S.box("EL_floor_plate", (0.40, 0.004, 0.16), loc=(0.52, 0.10, 0.092), mat=yel))
    return p


# ============================================================ PR_vending_machine
def build_vending(M):
    st, std, pl, gl = M["steel"], M["steel_dark"], M["plastic"], M["glass"]
    yel, lamp, can, led_g, ch, red = M["paint_yellow"], M["lamp"], M["can"], M["led_green"], M["chrome"], M["paint_red"]

    p = []
    # ---- корпус: ПУСТОТЕЛЫЙ каркас из панелей (иначе сплошной ящик прячет витрину) ----
    p.append(S.box("VM_side_l", (0.05, 0.68, 1.86), loc=(-0.425, 0, 0.95), bevel=0.010, mat=red))
    p.append(S.box("VM_side_r", (0.05, 0.68, 1.86), loc=(0.425, 0, 0.95), bevel=0.010, mat=red))
    p.append(S.box("VM_top", (0.90, 0.68, 0.06), loc=(0, 0, 1.83), bevel=0.008, mat=red))
    p.append(S.box("VM_bottom", (0.90, 0.68, 0.10), loc=(0, 0, 0.07), bevel=0.008, mat=red))
    p.append(S.box("VM_back", (0.90, 0.04, 1.72), loc=(0, -0.32, 0.95), bevel=0.006, mat=st))
    for i, z in enumerate((1.55, 0.35)):
        p.append(S.box(f"VM_back_rib{i}", (0.86, 0.02, 0.05), loc=(0, -0.345, z), mat=std))
    # внутренняя обшивка витрины + подсветка полок
    p.append(S.box("VM_cav_back", (0.86, 0.03, 1.10), loc=(0, 0.05, 1.23), mat=std))
    p.append(S.box("VM_cav_floor", (0.86, 0.30, 0.03), loc=(0, 0.20, 0.745), mat=std))
    p.append(S.box("VM_cav_ceil", (0.86, 0.30, 0.03), loc=(0, 0.20, 1.715), mat=std))
    for sx in (-1, 1):
        p.append(S.box(f"VM_cav_side{sx}", (0.03, 0.30, 1.00), loc=(sx * 0.42, 0.20, 1.23), mat=std))
    for i in range(5):
        p.append(S.box(f"VM_cav_light{i}", (0.60, 0.05, 0.020), loc=(-0.14, 0.30 - i * 0.05, 1.700), mat=lamp))
    # ---- дверь: РАМКА вокруг витрины (не сплошная панель) ----
    p.append(S.box("VM_door_l", (0.05, 0.03, 1.68), loc=(-0.425, 0.335, 0.96), bevel=0.006, mat=st))
    p.append(S.box("VM_door_r", (0.33, 0.03, 1.68), loc=(0.285, 0.335, 0.96), bevel=0.006, mat=st))
    p.append(S.box("VM_door_top", (0.90, 0.03, 0.06), loc=(0, 0.335, 1.77), bevel=0.004, mat=st))
    p.append(S.box("VM_door_bot", (0.90, 0.03, 0.60), loc=(0, 0.335, 0.42), bevel=0.006, mat=st))
    for sx, lx in ((-1, -0.405), (1, 0.125)):
        p.append(S.box(f"VM_win_side{sx}", (0.025, 0.024, 1.05), loc=(lx, 0.330, 1.23), mat=std))
    p.append(S.box("VM_win_top", (0.56, 0.024, 0.025), loc=(-0.14, 0.330, 1.750), mat=std))
    p.append(S.box("VM_win_bot", (0.56, 0.024, 0.030), loc=(-0.14, 0.330, 0.710), mat=std))
    p.append(S.box("VM_glass", (0.52, 0.014, 1.02), loc=(-0.14, 0.343, 1.23), mat=gl))
    p.append(S.box("VM_glass_hilite", (0.10, 0.004, 0.90), loc=(-0.34, 0.352, 1.23), mat=M["tape"]))
    p.append(S.box("VM_select_strip", (0.52, 0.006, 0.022), loc=(-0.14, 0.366, 1.775), mat=M["tape"]))
    p.append(S.cylinder("VM_handle", 0.014, 0.26, 14, loc=(0.30, 0.375, 1.02), mat=ch))
    for z in (1.15, 0.89):
        p.append(S.cylinder(f"VM_handle_base{int(z * 100)}", 0.026, 0.02, 16, loc=(0.30, 0.358, z),
                            rot=(math.radians(90), 0, 0), mat=st))
    # ---- витрина: 4 полки с банками и бутылками (видно через стекло) ----
    for s in range(4):
        z = 0.80 + s * 0.235
        p.append(S.box(f"VM_shelf{s}", (0.56, 0.26, 0.012), loc=(-0.14, 0.20, z), mat=st))
        p.append(S.box(f"VM_shelf_lip{s}", (0.56, 0.014, 0.028), loc=(-0.14, 0.325, z + 0.018), mat=std))
        for t in range(3):
            p.append(S.box(f"VM_tag{s}{t}", (0.05, 0.005, 0.018), loc=(-0.32 + t * 0.18, 0.330, z + 0.008),
                           mat=M["tape"] if (s + t) % 2 else yel))
        for i in range(6):
            x = -0.38 + i * 0.096
            if i == 3 and s % 2 == 0:
                p.append(S.cylinder(f"VM_bottle{s}", 0.027, 0.115, 18, loc=(x, 0.20, z + 0.070), mat=can))
                p.append(S.cylinder(f"VM_bottle_lbl{s}", 0.0275, 0.060, 18, loc=(x, 0.20, z + 0.075), mat=led_g))
                p.append(S.cylinder(f"VM_bottle_neck{s}", 0.013, 0.050, 12, loc=(x, 0.20, z + 0.150), mat=can))
                p.append(S.cylinder(f"VM_bottle_cap{s}", 0.017, 0.018, 12, loc=(x, 0.20, z + 0.181), mat=yel))
            else:
                p.append(S.cylinder(f"VM_can{s}{i}", 0.032, 0.095, 18, loc=(x, 0.20, z + 0.060), mat=can))
                p.append(S.cylinder(f"VM_can_label{s}{i}", 0.0325, 0.050, 18, loc=(x, 0.20, z + 0.060),
                                    mat=led_g if (s + i) % 3 == 0 else yel))
                p.append(S.cylinder(f"VM_can_top{s}{i}", 0.030, 0.008, 18, loc=(x, 0.20, z + 0.110), mat=ch))
    # ---- клавиатура и дисплей ----
    p.append(S.box("VM_keypanel", (0.22, 0.03, 0.66), loc=(0.29, 0.365, 1.30), bevel=0.006, mat=std))
    p.append(S.box("VM_display", (0.16, 0.012, 0.11), loc=(0.29, 0.383, 1.55), mat=led_g))
    p.append(S.box("VM_display_frame", (0.19, 0.02, 0.14), loc=(0.29, 0.372, 1.55), bevel=0.005, mat=st))
    for r in range(4):
        for c in range(3):
            p.append(S.cylinder(f"VM_btn{r}{c}", 0.012, 0.010, 12, loc=(0.225 + c * 0.058, 0.383, 1.38 - r * 0.072),
                                rot=(math.radians(90), 0, 0), mat=pl))
    # табличка «выбор / оплата» на нижней панели двери (рядом с лотком)
    p.append(S.box("VM_info_plate", (0.30, 0.008, 0.16), loc=(-0.16, 0.352, 0.60), bevel=0.004, mat=M["tape"]))
    for i in range(4):
        p.append(S.box(f"VM_info_line{i}", (0.24 - 0.03 * (i % 2), 0.004, 0.016),
                       loc=(-0.16, 0.358, 0.66 - i * 0.030), mat=std))
    # ---- щель для скрапа, лоток ----
    p.append(S.box("VM_coin_slot", (0.16, 0.03, 0.06), loc=(0.29, 0.365, 1.06), bevel=0.005, mat=st))
    p.append(S.box("VM_coin_hole", (0.09, 0.012, 0.012), loc=(0.29, 0.383, 1.06), mat=std))
    p.append(S.box("VM_coin_return", (0.05, 0.03, 0.05), loc=(0.29, 0.365, 0.94), bevel=0.004, mat=st))
    p.append(S.box("VM_tray", (0.46, 0.18, 0.16), loc=(0.14, 0.33, 0.33), bevel=0.008, mat=std))
    p.append(S.box("VM_tray_flap", (0.42, 0.02, 0.13), loc=(0.14, 0.415, 0.33), rot=(math.radians(-26), 0, 0), bevel=0.005, mat=st))
    p.append(S.box("VM_tray_recess", (0.36, 0.06, 0.07), loc=(0.14, 0.315, 0.30), mat=pl))
    # ---- вывеска ----
    p.append(S.box("VM_header", (0.88, 0.16, 0.30), loc=(0, 0.28, 1.86), bevel=0.010, mat=std))
    p.append(S.box("VM_header_face", (0.80, 0.02, 0.22), loc=(0, 0.365, 1.86), mat=lamp))
    for i in range(9):
        p.append(S.box(f"VM_letter{i}", (0.055, 0.008, 0.12), loc=(-0.32 + i * 0.08, 0.377, 1.86), mat=std))
    p.append(S.box("VM_header_trim", (0.90, 0.05, 0.03), loc=(0, 0.34, 1.72), rot=(math.radians(20), 0, 0), mat=yel))
    # ---- вентиляция, ножки, кабель ----
    for sy in (-1, 1):
        p.extend(grille(f"VM_vent{sy}", 0.32, 0.36, loc=(0, sy * 0.352, 0.45), rot=(math.radians(90), 0, 0), frame=st, body=std, bars=5))
    for sx in (-1, 1):
        p.append(S.box(f"VM_side_rail{sx}", (0.03, 0.62, 0.05), loc=(sx * 0.44, 0, 0.30), mat=yel))
    for sx in (-1, 1):
        for sy in (-1, 1):
            p.append(S.cylinder(f"VM_leg{sx}{sy}", 0.028, 0.10, 14, loc=(sx * 0.38, sy * 0.26, 0.05), mat=std))
    p.append(S.tube("VM_cord", [(0.44, -0.30, 0.14), (0.54, -0.36, 0.06), (0.64, -0.30, 0.03)], 0.012, mat=pl, resolution=6))
    p.append(S.box("VM_sticker", (0.18, 0.006, 0.12), loc=(-0.30, 0.36, 0.46), mat=yel))
    p.append(S.box("VM_sign", (0.14, 0.006, 0.10), loc=(0.30, 0.36, 0.70), mat=M["tape"]))
    return p


# ========================================================================== сборка
MODELS = [
    # (имя, билдер, FOV камеры, азимут камеры — фронт модели смотрит в +Y)
    ("PR_scooter", build_scooter, 34, 118.0),
    ("PR_loot_cart", build_loot_cart, 36, -62.0),
    ("PR_minecart", build_minecart, 34, 118.0),
    ("BD_elevator_car", build_elevator_car, 38, 118.0),
    ("PR_vending_machine", build_vending, 34, 118.0),
]


def build_all(render=True):
    S.ensure_dir(OUT)
    total = 0
    for name, fn, fov, az in MODELS:
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
            print(f"[transport] {name}: ОШИБКА сборки")
            continue
        S.shade_smooth(joined, 32)
        tris = S.tri_count(joined)
        total += tris
        S.export_fbx([joined], os.path.join(OUT, name + ".fbx"))
        S.export_glb([joined], os.path.join(OUT, name + ".glb"))
        print(f"[transport] {name}: {tris} трис, материалов: {len(joined.data.materials)}")
        if render:
            S.render_fit(os.path.join(PREVIEW, name + ".png"), [joined], samples=48, res=(900, 700),
                         fov_deg=fov, azimuth=az)
    print(f"[transport] ИТОГО трис: {total}")


if __name__ == "__main__":
    build_all(render="--no-render" not in sys.argv)
