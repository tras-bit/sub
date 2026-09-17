"""
SUBSISTENCE — chars_hazmat.py
Хазмат-костюм по референсу: жёлтый комбинезон, капюшон с тёмным визором, синие
перчатки, дыхательные канистры и шланги, чёрные ботинки.
Пропорции: рост 1.84 м, 7.5 «голов», суставы (локти/колени) на месте.
Плотность: капюшон/визор — ultra (subdiv), остальное — game-ready.
"""
import sys, os, math
import bpy
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import subs_common as S

OUT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..",
      "UnityProject", "Assets", "Subsistence", "Models", "Characters"))


def hazmat_materials():
    return dict(
        suit=S.pbr_material("M_HazmatYellow", (0.93, 0.86, 0.10, 1), 0.0, 0.36,
                            noise_scale=85, bump=0.55, sheen=0.45),
        suit_shade=S.pbr_material("M_HazmatYellowShade", (0.66, 0.60, 0.07, 1), 0.0, 0.44,
                                  noise_scale=120, bump=0.65),
        visor=S.pbr_material("M_HazmatVisor", (0.015, 0.015, 0.02, 1), 0.45, 0.06,
                             emission=(0.04, 0.045, 0.05, 1), emission_strength=0.15),
        gloves=S.pbr_material("M_HazmatGloves", (0.04, 0.30, 0.50, 1), 0.0, 0.52,
                              noise_scale=150, bump=0.5),
        hose=S.pbr_material("M_HazmatHose", (0.09, 0.10, 0.11, 1), 0.0, 0.70, noise_scale=240, bump=0.45),
        boots=S.pbr_material("M_HazmatBoots", (0.03, 0.03, 0.035, 1), 0.05, 0.52, noise_scale=130, bump=0.5),
        metal=S.pbr_material("M_HazmatMetal", (0.30, 0.31, 0.33, 1), 0.88, 0.34, noise_scale=70, bump=0.3),
    )


def build_hazmat_suit():
    m = hazmat_materials()
    suit, shade, visor, gloves, hose, boots, metal = (
        m["suit"], m["suit_shade"], m["visor"], m["gloves"], m["hose"], m["boots"], m["metal"])
    P = {}

    # ============ ТУЛОВИЩЕ (плечи 0.44 → талия 0.30, высота 0.60) ============
    P["chest"] = S.box("HZ_chest", (0.44, 0.26, 0.34), loc=(0, 0, 1.32), bevel=0.055,
                       taper=(0.86, 0.94), mat=suit)
    P["belly"] = S.box("HZ_belly", (0.33, 0.24, 0.30), loc=(0, 0, 1.04), bevel=0.05, mat=suit)
    P["pelvis"] = S.box("HZ_pelvis", (0.31, 0.23, 0.14), loc=(0, 0, 0.88), bevel=0.045, mat=suit)
    for i, z in enumerate((0.96, 1.10, 1.46)):
        P[f"seam{i}"] = S.box(f"HZ_seam{i}", (0.335, 0.245, 0.014), loc=(0, 0, z),
                              bevel=0.004, mat=shade)
    P["chest_flap"] = S.box("HZ_chestflap", (0.22, 0.035, 0.26), loc=(0, -0.125, 1.34), bevel=0.012, mat=suit)
    P["pocket"] = S.box("HZ_pocket", (0.10, 0.025, 0.085), loc=(0.095, -0.132, 1.00), bevel=0.008, mat=shade)
    P["regulator"] = S.box("HZ_regulator", (0.14, 0.06, 0.075), loc=(0, -0.135, 1.44), bevel=0.012, mat=metal)
    P["nametag"] = S.box("HZ_nametag", (0.075, 0.012, 0.028), loc=(-0.10, -0.135, 1.14), mat=metal)

    # ============ КАПЮШОН (ultra) + ВИЗОР ============
    P["neck"] = S.cylinder("HZ_neck", 0.075, 0.16, 22, loc=(0, 0.005, 1.56), mat=shade)
    P["hood"] = S.sphere("HZ_hood", 0.145, 1, loc=(0, 0.028, 1.71), scale=(1.0, 1.10, 1.16),
                         u_seg=32, v_seg=18, mat=suit)                 # ← ultra
    P["hood_back"] = S.sphere("HZ_hoodback", 0.138, 1, loc=(0, 0.06, 1.69), scale=(1.0, 1.05, 1.05),
                              u_seg=24, v_seg=14, mat=shade)
    P["collar"] = S.cylinder("HZ_collar", 0.128, 0.07, 26, loc=(0, 0.01, 1.55), mat=suit)
    # ВИЗОР выступает за капюшон (как на референсе): рама + тёмное стекло
    P["visor_frame"] = S.sphere("HZ_visorframe", 0.132, 1, loc=(0, -0.10, 1.705),
                                scale=(1.0, 0.50, 0.96), u_seg=32, v_seg=18, mat=metal)
    P["visor"] = S.sphere("HZ_visor", 0.125, 1, loc=(0, -0.128, 1.705),
                          scale=(0.97, 0.42, 0.90), u_seg=32, v_seg=18, mat=visor)   # ← ultra

    # ============ РУКИ (плечо 1.44 → кисть 0.80) ============
    for side, sx in (("L", -1), ("R", 1)):
        P[f"shoulder_{side}"] = S.sphere(f"HZ_shoulder{side}", 0.088, 0, loc=(sx * 0.205, 0, 1.42),
                                         scale=(1.0, 0.95, 0.95), u_seg=20, v_seg=12, mat=suit)
        P[f"upperarm_{side}"] = S.capsule(f"HZ_upperarm{side}", 0.070, 0.26, loc=(sx * 0.212, 0, 1.28),
                                          rot=(0, math.radians(sx * 4), 0), r1=0.078, r2=0.064, mat=suit)
        P[f"elbow_{side}"] = S.sphere(f"HZ_elbow{side}", 0.066, 0, loc=(sx * 0.218, 0, 1.14),
                                      u_seg=18, v_seg=10, mat=suit)
        P[f"forearm_{side}"] = S.capsule(f"HZ_forearm{side}", 0.062, 0.24, loc=(sx * 0.224, -0.008, 1.00),
                                         rot=(math.radians(-6), math.radians(sx * 2), 0), r1=0.066, r2=0.058, mat=suit)
        P[f"cuff_{side}"] = S.cylinder(f"HZ_cuff{side}", 0.071, 0.05, 22, loc=(sx * 0.228, -0.02, 0.875), mat=shade)
        P[f"glove_{side}"] = S.box(f"HZ_glove{side}", (0.10, 0.175, 0.065),
                                   loc=(sx * 0.229, -0.035, 0.815), bevel=0.026, mat=gloves)
        P[f"thumb_{side}"] = S.capsule(f"HZ_thumb{side}", 0.021, 0.05, loc=(sx * 0.19, -0.085, 0.815),
                                       rot=(math.radians(72), 0, 0), mat=gloves)
        P[f"fingers_{side}"] = S.box(f"HZ_fingers{side}", (0.095, 0.075, 0.055),
                                     loc=(sx * 0.229, -0.135, 0.812), bevel=0.022, mat=gloves)

    # ============ НОГИ (бедро 0.92 → подошва 0.02) ============
    for side, sx in (("L", -1), ("R", 1)):
        P[f"hip_{side}"] = S.sphere(f"HZ_hip{side}", 0.098, 0, loc=(sx * 0.098, 0, 0.90),
                                    u_seg=20, v_seg=12, mat=suit)
        P[f"thigh_{side}"] = S.capsule(f"HZ_thigh{side}", 0.088, 0.40, loc=(sx * 0.098, 0, 0.68),
                                       r1=0.096, r2=0.080, mat=suit)
        P[f"knee_{side}"] = S.sphere(f"HZ_knee{side}", 0.082, 0, loc=(sx * 0.098, 0, 0.47),
                                     u_seg=18, v_seg=10, mat=suit)
        P[f"shin_{side}"] = S.capsule(f"HZ_shin{side}", 0.076, 0.33, loc=(sx * 0.098, 0, 0.30),
                                      r1=0.080, r2=0.070, mat=suit)
        P[f"ankle_{side}"] = S.cylinder(f"HZ_ankle{side}", 0.078, 0.05, 20, loc=(sx * 0.098, 0, 0.12), mat=shade)
        P[f"boot_{side}"] = S.box(f"HZ_boot{side}", (0.105, 0.24, 0.10), loc=(sx * 0.098, -0.025, 0.065),
                                  bevel=0.02, mat=boots)
        P[f"boot_toe_{side}"] = S.sphere(f"HZ_boottoe{side}", 0.055, 0, loc=(sx * 0.098, -0.145, 0.055),
                                         scale=(0.95, 1.30, 0.85), u_seg=20, v_seg=12, mat=boots)
        P[f"sole_{side}"] = S.box(f"HZ_sole{side}", (0.11, 0.27, 0.022), loc=(sx * 0.098, -0.04, 0.012),
                                  bevel=0.008, mat=shade)

    # ============ ДЫХАТЕЛЬНАЯ СИСТЕМА: канистры + шланги (две петли) ============
    for side, sx in (("L", -1), ("R", 1)):
        P[f"canister_{side}"] = S.cylinder(f"HZ_canister{side}", 0.036, 0.070, 22,
                                           loc=(sx * 0.145, -0.085, 1.615),
                                           rot=(math.radians(74), 0, math.radians(sx * 14)), mat=metal)
        P[f"canister_cap_{side}"] = S.cylinder(f"HZ_cancap{side}", 0.040, 0.014, 22,
                                               loc=(sx * 0.152, -0.115, 1.607),
                                               rot=(math.radians(74), 0, math.radians(sx * 14)), mat=shade)
        pts = [(sx * 0.150, -0.118, 1.600), (sx * 0.185, -0.14, 1.52), (sx * 0.15, -0.165, 1.465),
               (sx * 0.055, -0.16, 1.45)]
        t = S.tube(f"HZ_hose{side}", pts, 0.019, mat=hose)
        S.curve_to_mesh(t)
        P[f"hose_{side}"] = t

    objs = []
    for k, o in P.items():
        if o is None:
            continue
        S.smart_uv(o)
        objs.append(o)
    return objs


if __name__ == "__main__":
    from mathutils import Matrix
    S.clean_scene()
    parts = build_hazmat_suit()
    suit = S.join_objects(parts, "CH_hazmat_suit")
    S.shade_smooth(suit, 38)
    print(f"[hazmat] {S.tri_count(suit)} трис, материалов {len(suit.data.materials)}")
    S.export_fbx([suit], os.path.join(OUT, "CH_hazmat_suit.fbx"))
    S.export_glb([suit], os.path.join(OUT, "CH_hazmat_suit.glb"))
    prev = os.path.abspath(os.path.join(OUT, "..", "..", "..", "..", "..", "docs", "previews"))
    S.render_fit(os.path.join(prev, "CH_hazmat_suit.png"), [suit], samples=48, res=(620, 860), fov_deg=30)
