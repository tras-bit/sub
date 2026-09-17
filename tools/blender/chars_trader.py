"""
SUBSISTENCE — chars_trader.py
Торговец в безопасной комнате (AI/TraderNpc.cs ждёт CH_trader_npc.fbx).
Образ: бывший инженер-снабженец в промасленной полевой куртке, кожаный разгрузочный
жилет с подсумками, респиратор с двумя фильтрами, янтарные очки, кепка, перчатки,
ботинки, фонарь и рация на поясе, связка ключей, планшет-накладная.
Пропорции: рост 1.80 м, 7.5 «голов», суставы (локти/колени) на месте.
Плотность: голова/респиратор/очки — ultra (subdiv), снаряжение — game-ready.
"""
import sys, os, math
import bpy
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import subs_common as S

OUT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..",
      "UnityProject", "Assets", "Subsistence", "Models", "Characters"))
PREVIEW = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "docs", "previews"))


def trader_materials():
    return dict(
        jacket=S.pbr_material("M_TraderJacket", (0.16, 0.19, 0.13, 1), 0.0, 0.62,
                              noise_scale=110, bump=0.7, sheen=0.2),
        jacket_worn=S.pbr_material("M_TraderJacketWorn", (0.21, 0.24, 0.17, 1), 0.0, 0.68,
                                   noise_scale=180, bump=0.85),
        vest=S.pbr_material("M_TraderVest", (0.33, 0.22, 0.12, 1), 0.05, 0.55,
                            noise_scale=140, bump=0.8),
        vest_dark=S.pbr_material("M_TraderVestDark", (0.20, 0.13, 0.07, 1), 0.05, 0.62,
                                 noise_scale=170, bump=0.85),
        pants=S.pbr_material("M_TraderPants", (0.095, 0.098, 0.108, 1), 0.0, 0.72,
                             noise_scale=130, bump=0.75),
        mask=S.pbr_material("M_TraderMask", (0.045, 0.048, 0.052, 1), 0.10, 0.44,
                            noise_scale=200, bump=0.5),
        filter=S.pbr_material("M_TraderFilter", (0.42, 0.43, 0.46, 1), 0.85, 0.38,
                              noise_scale=90, bump=0.4),
        goggle=S.pbr_material("M_TraderGoggle", (0.55, 0.36, 0.10, 1), 0.30, 0.10,
                              emission=(0.30, 0.18, 0.05, 1), emission_strength=0.25),
        glove=S.pbr_material("M_TraderGloves", (0.09, 0.13, 0.20, 1), 0.0, 0.66,
                             noise_scale=170, bump=0.7),
        boots=S.pbr_material("M_TraderBoots", (0.035, 0.033, 0.030, 1), 0.04, 0.58,
                             noise_scale=150, bump=0.6),
        metal=S.pbr_material("M_TraderMetal", (0.34, 0.35, 0.37, 1), 0.86, 0.34,
                             noise_scale=80, bump=0.35),
        brass=S.pbr_material("M_TraderBrass", (0.68, 0.50, 0.16, 1), 0.90, 0.30),
        plastic=S.pbr_material("M_TraderPlastic", (0.075, 0.08, 0.09, 1), 0.0, 0.45,
                               noise_scale=210, bump=0.35),
        lamp=S.pbr_material("M_TraderLamp", (0.95, 0.92, 0.80, 1), 0.0, 0.22,
                            emission=(1.0, 0.95, 0.78, 1), emission_strength=2.4),
        led=S.pbr_material("M_TraderLed", (0.12, 0.85, 0.35, 1), 0.0, 0.15,
                           emission=(0.2, 1.0, 0.45, 1), emission_strength=5.0),
        patch=S.pbr_material("M_TraderPatch", (0.80, 0.78, 0.72, 1), 0.0, 0.60, noise_scale=180, bump=0.5),
        paper=S.pbr_material("M_TraderPaper", (0.86, 0.83, 0.72, 1), 0.0, 0.72, noise_scale=260, bump=0.4),
    )


def build_trader():
    m = trader_materials()
    jk, jk2, vt, vt2 = m["jacket"], m["jacket_worn"], m["vest"], m["vest_dark"]
    pn, mk, flt, gg = m["pants"], m["mask"], m["filter"], m["goggle"]
    gl, bt, mt = m["glove"], m["boots"], m["metal"]
    br, pl, lamp, led, pt, pp = m["brass"], m["plastic"], m["lamp"], m["led"], m["patch"], m["paper"]
    P = {}

    # ================= ТУЛОВИЩЕ: куртка + разгрузочный жилет =================
    P["chest"] = S.box("TR_chest", (0.46, 0.27, 0.36), loc=(0, 0, 1.31), bevel=0.055,
                       taper=(0.88, 0.95), mat=jk)
    P["belly"] = S.box("TR_belly", (0.35, 0.25, 0.30), loc=(0, 0, 1.03), bevel=0.05, mat=jk2)
    P["pelvis"] = S.box("TR_pelvis", (0.32, 0.24, 0.15), loc=(0, 0, 0.87), bevel=0.045, mat=pn)
    for i, z in enumerate((0.95, 1.12, 1.45)):
        P[f"seam{i}"] = S.box(f"TR_seam{i}", (0.345, 0.255, 0.012), loc=(0, 0, z), bevel=0.004, mat=vt2)
    # воротник-стойка + отвороты
    P["collar"] = S.cylinder("TR_collar", 0.115, 0.075, 24, loc=(0, 0.01, 1.505), mat=vt2)
    P["lapel_l"] = S.box("TR_lapel_l", (0.10, 0.03, 0.24), loc=(-0.085, -0.135, 1.34),
                         rot=(0, 0, math.radians(9)), bevel=0.008, mat=vt)
    P["lapel_r"] = S.box("TR_lapel_r", (0.10, 0.03, 0.24), loc=(0.085, -0.135, 1.34),
                         rot=(0, 0, math.radians(-9)), bevel=0.008, mat=vt)
    P["zipper"] = S.box("TR_zipper", (0.012, 0.012, 0.34), loc=(0, -0.145, 1.30), mat=mt)
    for i in range(6):
        P[f"zip_tooth{i}"] = S.box(f"TR_ziptooth{i}", (0.022, 0.008, 0.014),
                                   loc=(0, -0.150, 1.44 - i * 0.055), mat=mt)
    # разгрузочный жилет: спинка/грудь + подсумки с клапанами и пряжками
    P["rig_back"] = S.box("TR_rig_back", (0.34, 0.045, 0.40), loc=(0, 0.135, 1.32), bevel=0.02, mat=vt)
    P["rig_front"] = S.box("TR_rig_front", (0.30, 0.045, 0.30), loc=(0, -0.145, 1.24), bevel=0.02, mat=vt)
    for sx in (-1, 1):
        P[f"rig_strap{sx}"] = S.box(f"TR_rigstrap{sx}", (0.055, 0.28, 0.020),
                                    loc=(sx * 0.115, -0.005, 1.475), bevel=0.006, mat=vt2)
    POUCH = [(-0.105, 1.17, 0.11, 0.075), (0.105, 1.17, 0.11, 0.075),
             (-0.105, 1.32, 0.10, 0.085), (0.105, 1.32, 0.10, 0.085)]
    for i, (x, z, w, h) in enumerate(POUCH):
        P[f"pouch{i}"] = S.box(f"TR_pouch{i}", (w, 0.055, h), loc=(x, -0.185, z), bevel=0.012, mat=vt)
        P[f"pouch_flap{i}"] = S.box(f"TR_pouchflap{i}", (w * 0.98, 0.062, 0.030),
                                    loc=(x, -0.188, z + h * 0.5 - 0.012), bevel=0.008, mat=vt2)
        P[f"pouch_buckle{i}"] = S.box(f"TR_pouchbuckle{i}", (0.026, 0.016, 0.020),
                                      loc=(x, -0.216, z + h * 0.5 - 0.030), mat=br)
    # рация на левой лямке + провод к гарнитуре
    P["radio"] = S.box("TR_radio", (0.062, 0.040, 0.115), loc=(-0.155, -0.145, 1.40), bevel=0.008, mat=pl)
    P["radio_ant"] = S.cylinder("TR_radio_ant", 0.006, 0.13, 10, loc=(-0.155, -0.150, 1.50), mat=pl)
    P["radio_led"] = S.box("TR_radio_led", (0.014, 0.006, 0.010), loc=(-0.155, -0.168, 1.435), mat=led)
    P["radio_knob"] = S.cylinder("TR_radio_knob", 0.012, 0.014, 12,
                                 loc=(-0.155, -0.168, 1.372), rot=(math.radians(90), 0, 0), mat=mt)
    t = S.tube("TR_radio_wire", [(-0.155, -0.150, 1.47), (-0.145, -0.115, 1.52), (-0.095, -0.075, 1.545),
                                 (-0.052, -0.055, 1.560)], 0.006, mat=pl)
    S.curve_to_mesh(t); P["radio_wire"] = t
    # планшет-накладная: висит у левого бедра, прижатый к поясу (единый локальный фрейм)
    from mathutils import Euler as _E, Vector as _V

    def off(base, rot, o):
        v = _V(o); v.rotate(_E(rot))
        return (base[0] + v.x, base[1] + v.y, base[2] + v.z)

    bp, br_ = (-0.196, -0.028, 0.940), (math.radians(7), math.radians(-13), math.radians(5))
    P["board"] = S.box("TR_board", (0.150, 0.013, 0.230), loc=bp, rot=br_, bevel=0.005, mat=pl)
    P["board_paper"] = S.box("TR_board_paper", (0.130, 0.005, 0.198), loc=off(bp, br_, (0.004, -0.009, 0.006)),
                             rot=br_, mat=pp)
    for i in range(5):
        P[f"board_line{i}"] = S.box(f"TR_boardline{i}", (0.098 - 0.012 * (i % 2), 0.003, 0.007),
                                    loc=off(bp, br_, (0.004, -0.013, 0.062 - i * 0.028)), rot=br_, mat=pl)
    P["board_clip"] = S.box("TR_board_clip", (0.048, 0.018, 0.028), loc=off(bp, br_, (0.0, -0.004, 0.104)),
                            rot=br_, bevel=0.004, mat=mt)

    # шеврон на плече + бирка с именем
    for sx in (-1, 1):
        P[f"patch{sx}"] = S.box(f"TR_patch{sx}", (0.085, 0.010, 0.060), loc=(sx * 0.205, -0.055, 1.44),
                                rot=(0, math.radians(sx * 78), 0), mat=pt)
    P["nametag"] = S.box("TR_nametag", (0.080, 0.010, 0.026), loc=(0.10, -0.150, 1.42), mat=mt)
    P["belt"] = S.box("TR_belt", (0.36, 0.26, 0.055), loc=(0, 0, 0.94), bevel=0.012, mat=vt2)
    P["belt_buckle"] = S.box("TR_belt_buckle", (0.070, 0.020, 0.050), loc=(0, -0.135, 0.94), bevel=0.006, mat=br)

    # ================= ГОЛОВА: кепка + респиратор + очки (ultra) =================
    P["neck"] = S.cylinder("TR_neck", 0.072, 0.150, 22, loc=(0, 0.005, 1.545), mat=jk2)
    P["head"] = S.sphere("TR_head", 0.118, 1, loc=(0, 0.012, 1.665), scale=(1.0, 1.06, 1.11),
                         u_seg=32, v_seg=18, mat=jk2)                      # ← ultra
    P["jaw"] = S.sphere("TR_jaw", 0.098, 1, loc=(0, -0.028, 1.615), scale=(1.0, 1.02, 0.86),
                        u_seg=28, v_seg=16, mat=jk2)
    P["cap"] = S.sphere("TR_cap", 0.126, 1, loc=(0, 0.014, 1.705), scale=(1.0, 1.02, 0.72),
                        u_seg=32, v_seg=18, mat=vt)
    P["cap_band"] = S.cylinder("TR_capband", 0.128, 0.030, 30, loc=(0, 0.014, 1.690), mat=vt2)
    P["cap_brim"] = S.box("TR_capbrim", (0.20, 0.115, 0.018), loc=(0, -0.158, 1.722),
                          rot=(math.radians(-7), 0, 0), bevel=0.008, mat=vt2)
    P["cap_btn"] = S.sphere("TR_capbtn", 0.016, 0, loc=(0, 0.014, 1.796), u_seg=14, v_seg=8, mat=vt2)
    # респиратор: корпус + два фильтра + клапан выдоха
    P["mask_body"] = S.sphere("TR_mask", 0.098, 1, loc=(0, -0.062, 1.625), scale=(1.0, 0.72, 0.86),
                              u_seg=32, v_seg=18, mat=mk)                 # ← ultra
    P["mask_seal"] = S.cylinder("TR_mask_seal", 0.093, 0.028, 26, loc=(0, -0.030, 1.628),
                                rot=(math.radians(90), 0, 0), mat=pl)
    P["mask_valve"] = S.cylinder("TR_mask_valve", 0.024, 0.020, 16, loc=(0, -0.122, 1.600),
                                 rot=(math.radians(90), 0, 0), mat=pl)
    for sx in (-1, 1):
        P[f"filter{sx}"] = S.cylinder(f"TR_filter{sx}", 0.044, 0.085, 24,
                                      loc=(sx * 0.078, -0.115, 1.622),
                                      rot=(math.radians(74), 0, math.radians(sx * 26)), mat=flt)
        P[f"filter_cap{sx}"] = S.cylinder(f"TR_filter_cap{sx}", 0.049, 0.014, 24,
                                          loc=(sx * 0.095, -0.146, 1.615),
                                          rot=(math.radians(74), 0, math.radians(sx * 26)), mat=pl)
        P[f"filter_ring{sx}"] = S.cylinder(f"TR_filter_ring{sx}", 0.040, 0.012, 24,
                                           loc=(sx * 0.070, -0.098, 1.626),
                                           rot=(math.radians(74), 0, math.radians(sx * 26)), mat=mt)
    # очки: оправа + янтарные линзы + дужки к кепке
    P["goggle_frame"] = S.box("TR_goggle_frame", (0.215, 0.052, 0.064), loc=(0, -0.112, 1.686),
                              rot=(math.radians(6), 0, 0), bevel=0.014, mat=pl)
    P["goggle_lens"] = S.box("TR_goggle_lens", (0.196, 0.028, 0.048), loc=(0, -0.134, 1.686),
                             rot=(math.radians(6), 0, 0), bevel=0.012, mat=gg)
    P["goggle_bridge"] = S.box("TR_goggle_bridge", (0.028, 0.024, 0.026), loc=(0, -0.142, 1.686), mat=pl)
    for sx in (-1, 1):
        P[f"goggle_strap{sx}"] = S.box(f"TR_goggle_strap{sx}", (0.020, 0.150, 0.030),
                                       loc=(sx * 0.124, -0.036, 1.692), mat=pl)

    # ================= РУКИ (плечо 1.44 → кисть 0.80) =================
    for side, sx in (("L", -1), ("R", 1)):
        P[f"shoulder_{side}"] = S.sphere(f"TR_shoulder{side}", 0.090, 0, loc=(sx * 0.215, 0, 1.415),
                                         scale=(1.0, 0.95, 0.95), u_seg=20, v_seg=12, mat=jk)
        P[f"shoulder_pad_{side}"] = S.box(f"TR_shoulderpad{side}", (0.085, 0.150, 0.055),
                                          loc=(sx * 0.215, 0, 1.478), bevel=0.016, mat=vt)
        P[f"upperarm_{side}"] = S.capsule(f"TR_upperarm{side}", 0.072, 0.26, loc=(sx * 0.222, 0, 1.275),
                                          rot=(0, math.radians(sx * 4), 0), r1=0.080, r2=0.066, mat=jk)
        P[f"elbow_{side}"] = S.sphere(f"TR_elbow{side}", 0.068, 0, loc=(sx * 0.228, 0, 1.135),
                                      u_seg=18, v_seg=10, mat=jk2)
        P[f"forearm_{side}"] = S.capsule(f"TR_forearm{side}", 0.064, 0.24, loc=(sx * 0.234, -0.010, 0.995),
                                         rot=(math.radians(-6), math.radians(sx * 2), 0),
                                         r1=0.068, r2=0.058, mat=jk)
        for i in range(3):
            P[f"cuff_{side}{i}"] = S.cylinder(f"TR_cuff{side}{i}", 0.064, 0.008, 20,
                                              loc=(sx * 0.236, -0.020, 1.075 - i * 0.030), mat=vt2)
        P[f"glove_{side}"] = S.box(f"TR_glove{side}", (0.105, 0.180, 0.068),
                                   loc=(sx * 0.238, -0.040, 0.815), bevel=0.026, mat=gl)
        P[f"fingers_{side}"] = S.box(f"TR_fingers{side}", (0.098, 0.078, 0.058),
                                     loc=(sx * 0.238, -0.140, 0.812), bevel=0.022, mat=gl)
        P[f"thumb_{side}"] = S.capsule(f"TR_thumb{side}", 0.022, 0.050, loc=(sx * 0.198, -0.088, 0.815),
                                       rot=(math.radians(72), 0, 0), mat=gl)
        P[f"cuff_knit_{side}"] = S.cylinder(f"TR_cuffknit{side}", 0.062, 0.045, 22,
                                            loc=(sx * 0.238, -0.030, 0.868), mat=vt2)
        P[f"watch_{side}"] = S.cylinder(f"TR_watch{side}", 0.026, 0.016, 16, loc=(sx * 0.238, -0.030, 0.905),
                                        rot=(0, 0, math.radians(90)), mat=mt)

    # ================= НОГИ (бедро 0.92 → подошва 0.02) =================
    for side, sx in (("L", -1), ("R", 1)):
        P[f"hip_{side}"] = S.sphere(f"TR_hip{side}", 0.100, 0, loc=(sx * 0.100, 0, 0.89),
                                    u_seg=20, v_seg=12, mat=pn)
        P[f"thigh_{side}"] = S.capsule(f"TR_thigh{side}", 0.090, 0.40, loc=(sx * 0.100, 0, 0.67),
                                       r1=0.098, r2=0.082, mat=pn)
        P[f"cargo_{side}"] = S.box(f"TR_cargo{side}", (0.055, 0.150, 0.130), loc=(sx * 0.152, -0.020, 0.70),
                                   bevel=0.012, mat=pn)
        P[f"cargo_flap_{side}"] = S.box(f"TR_cargoflap{side}", (0.058, 0.156, 0.028),
                                        loc=(sx * 0.152, -0.020, 0.762), bevel=0.008, mat=jk2)
        P[f"knee_{side}"] = S.sphere(f"TR_knee{side}", 0.084, 0, loc=(sx * 0.100, 0, 0.46),
                                     u_seg=18, v_seg=10, mat=pn)
        P[f"kneepad_{side}"] = S.box(f"TR_kneepad{side}", (0.115, 0.075, 0.115),
                                     loc=(sx * 0.100, -0.078, 0.455), rot=(math.radians(-6), 0, 0),
                                     bevel=0.024, mat=vt)
        P[f"shin_{side}"] = S.capsule(f"TR_shin{side}", 0.078, 0.32, loc=(sx * 0.100, 0, 0.295),
                                      r1=0.082, r2=0.070, mat=pn)
        P[f"ankle_{side}"] = S.cylinder(f"TR_ankle{side}", 0.079, 0.05, 20, loc=(sx * 0.100, 0, 0.115), mat=bt)
        P[f"boot_{side}"] = S.box(f"TR_boot{side}", (0.110, 0.250, 0.105), loc=(sx * 0.100, -0.028, 0.062),
                                  bevel=0.020, mat=bt)
        P[f"boot_toe_{side}"] = S.sphere(f"TR_boottoe{side}", 0.057, 0, loc=(sx * 0.100, -0.152, 0.052),
                                         scale=(0.95, 1.30, 0.85), u_seg=20, v_seg=12, mat=bt)
        P[f"boot_lace_{side}"] = S.box(f"TR_bootlace{side}", (0.070, 0.012, 0.075),
                                       loc=(sx * 0.100, -0.130, 0.078), rot=(math.radians(30), 0, 0), mat=jk2)
        P[f"sole_{side}"] = S.box(f"TR_sole{side}", (0.115, 0.280, 0.022), loc=(sx * 0.100, -0.045, 0.011),
                                  bevel=0.008, mat=pl)

    # ================= ПОЯС: фонарь, ключи, кобура, фляга, сумка =================
    P["torch"] = S.cylinder("TR_torch", 0.026, 0.150, 18, loc=(0.150, -0.115, 0.90),
                            rot=(math.radians(88), 0, math.radians(-8)), mat=mt)
    P["torch_head"] = S.cylinder("TR_torch_head", 0.034, 0.045, 18, loc=(0.150, -0.192, 0.902),
                                 rot=(math.radians(88), 0, math.radians(-8)), mat=mt)
    P["torch_lens"] = S.cylinder("TR_torch_lens", 0.028, 0.012, 18, loc=(0.150, -0.216, 0.902),
                                 rot=(math.radians(88), 0, math.radians(-8)), mat=lamp)
    P["keys_ring"] = S.cylinder("TR_keys_ring", 0.022, 0.008, 16, loc=(-0.135, -0.128, 0.885),
                                rot=(math.radians(90), 0, 0), mat=mt)
    for i in range(4):
        P[f"key{i}"] = S.box(f"TR_key{i}", (0.014, 0.006, 0.052 + 0.008 * (i % 2)),
                             loc=(-0.135 - 0.008 + 0.006 * i, -0.128, 0.852 - 0.004 * i),
                             rot=(0, 0, math.radians(-16 + i * 11)), mat=br)
    P["holster"] = S.box("TR_holster", (0.075, 0.075, 0.170), loc=(0.160, 0.010, 0.845), bevel=0.014, mat=vt2)
    P["holster_grip"] = S.box("TR_holster_grip", (0.038, 0.105, 0.045), loc=(0.160, -0.035, 0.935),
                              rot=(math.radians(16), 0, 0), bevel=0.008, mat=pl)
    P["holster_strap"] = S.box("TR_holster_strap", (0.085, 0.085, 0.020), loc=(0.160, 0.010, 0.905), mat=vt)
    P["canteen"] = S.cylinder("TR_canteen", 0.052, 0.115, 20, loc=(-0.165, 0.095, 0.875), mat=mt)
    P["canteen_cap"] = S.cylinder("TR_canteen_cap", 0.024, 0.030, 16, loc=(-0.165, 0.095, 0.945), mat=pl)
    t = S.tube("TR_canteen_strap", [(-0.165, 0.095, 0.945), (-0.120, 0.105, 0.975), (-0.075, 0.115, 0.985)],
               0.008, mat=vt2)
    S.curve_to_mesh(t); P["canteen_strap"] = t
    # сумка через плечо + ремень
    P["satchel"] = S.box("TR_satchel", (0.215, 0.115, 0.185), loc=(0.195, 0.150, 0.955), bevel=0.020, mat=vt)
    P["satchel_flap"] = S.box("TR_satchel_flap", (0.222, 0.120, 0.040), loc=(0.195, 0.148, 1.040),
                              rot=(math.radians(-6), 0, 0), bevel=0.012, mat=vt2)
    P["satchel_buckle"] = S.box("TR_satchel_buckle", (0.034, 0.020, 0.026), loc=(0.195, 0.088, 0.995), mat=br)
    t = S.tube("TR_satchel_strap", [(0.185, 0.150, 1.060), (0.150, 0.010, 1.330), (-0.030, -0.115, 1.430),
                                    (-0.150, -0.060, 1.320), (-0.170, 0.030, 1.150)], 0.014, mat=vt2)
    S.curve_to_mesh(t); P["satchel_strap"] = t

    objs = []
    for _, o in P.items():
        if o is None:
            continue
        if getattr(o, "type", None) == 'CURVE':
            S.curve_to_mesh(o)
        S.smart_uv(o)
        objs.append(o)
    return objs


if __name__ == "__main__":
    S.clean_scene()
    parts = build_trader()
    trader = S.join_objects(parts, "CH_trader_npc")
    S.shade_smooth(trader, 38)
    print(f"[trader] {S.tri_count(trader)} трис, материалов {len(trader.data.materials)}")
    S.export_fbx([trader], os.path.join(OUT, "CH_trader_npc.fbx"))
    S.export_glb([trader], os.path.join(OUT, "CH_trader_npc.glb"))
    # фронт персонажа смотрит в -Y (маска/очки/подсумки) → камера по умолчанию (-62°)
    S.render_fit(os.path.join(PREVIEW, "CH_trader_npc.png"), [trader], samples=48, res=(620, 860),
                 fov_deg=30, azimuth=-62.0)
