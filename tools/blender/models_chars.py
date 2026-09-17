"""
SUBSISTENCE — models_chars.py
Персонажи и монстры: хазмат-костюм (жёлтый, как на референсе: капюшон + тёмный визор,
синие перчатки, гофрированные шланги, чёрные ботинки), Smiler, Hound, Partygoer,
Skin-Stealer, Clump, босс Bacteria. Плюс риг (армейтура) для хазмата и монстров.
Экспорт FBX/GLB + превью-рендеры (Cycles CPU).
"""
import sys, os, math
import bpy
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import subs_common as S
import bpy
from mathutils import Vector

OUT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..",
      "UnityProject", "Assets", "Subsistence", "Models", "Characters"))

# --------------------------------------------------------------------------- #
#  МАТЕРИАЛЫ ХАЗМАТА (по референсу: жёлтый, атласный блеск, тёмный визор)
# --------------------------------------------------------------------------- #
def hazmat_materials():
    return dict(
        suit=S.pbr_material("M_HazmatYellow", (0.92, 0.86, 0.12, 1), 0.0, 0.38,
                            noise_scale=70, bump=0.6, sheen=0.5),
        suit_dark=S.pbr_material("M_HazmatYellowDark", (0.60, 0.54, 0.08, 1), 0.0, 0.45, noise_scale=90, bump=0.7),
        visor=S.pbr_material("M_HazmatVisor", (0.02, 0.02, 0.025, 1), 0.35, 0.08,
                             emission=(0.05, 0.05, 0.06, 1), emission_strength=0.1),
        gloves=S.pbr_material("M_HazmatGloves", (0.05, 0.28, 0.45, 1), 0.0, 0.55, noise_scale=140, bump=0.5),
        hose=S.pbr_material("M_HazmatHose", (0.10, 0.11, 0.12, 1), 0.0, 0.72, noise_scale=200, bump=0.4),
        boots=S.pbr_material("M_HazmatBoots", (0.035, 0.035, 0.04, 1), 0.05, 0.55, noise_scale=120, bump=0.5),
        filter=S.pbr_material("M_HazmatFilter", (0.25, 0.26, 0.28, 1), 0.85, 0.35, noise_scale=60, bump=0.3),
    )

def build_hazmat_suit():
    """
    Хазмат-костюм (референс: жёлтый капюшон + тёмный визор, синие перчатки, шланги,
    чёрные ботинки). Пропорции 7.5 «голов» — рост 1.85 м, руки с локтями, ноги с коленями.
    """
    m = hazmat_materials()
    suit, dark, visor, gloves, hose, boots, filt = (m["suit"], m["suit_dark"], m["visor"],
                                                   m["gloves"], m["hose"], m["boots"], m["filt"] if "filt" in m else m["filter"])
    parts = {}

    # ---------- ТУЛОВИЩЕ: сужается от плеч к поясу ----------
    parts["torso_upper"] = S.box("HZ_torso_upper", (0.42, 0.25, 0.30), loc=(0, 0, 1.36), bevel=0.05, taper=(0.92, 0.95), mat=suit)
    parts["torso_lower"] = S.box("HZ_torso_lower", (0.33, 0.23, 0.32), loc=(0, 0, 1.06), bevel=0.05, taper=(0.96, 0.96), mat=suit)
    parts["waist"] = S.box("HZ_waist", (0.31, 0.22, 0.10), loc=(0, 0, 0.92), bevel=0.03, mat=suit)
    for i, z in enumerate((1.00, 1.14, 1.42)):     # гофрированные пояса костюма
        parts[f"belt_{i}"] = S.box(f"HZ_belt{i}", (0.345, 0.245, 0.016), loc=(0, 0, z), mat=dark)
    parts["chest_flap"] = S.box("HZ_chestflap", (0.24, 0.04, 0.30), loc=(0, -0.115, 1.30), bevel=0.01, mat=suit)
    parts["pocket"] = S.box("HZ_pocket", (0.11, 0.03, 0.09), loc=(0.10, -0.122, 1.02), bevel=0.008, mat=dark)
    parts["regulator"] = S.box("HZ_regulator", (0.15, 0.07, 0.09), loc=(0, -0.125, 1.45), bevel=0.012, mat=dark)
    parts["nametag"] = S.box("HZ_nametag", (0.08, 0.015, 0.03), loc=(-0.11, -0.125, 1.16), mat=filt)

    # ---------- КАПЮШОН (сфероид) + ВИЗОР ----------
    parts["hood"] = S.sphere("HZ_hood", 0.20, 3, loc=(0, 0.015, 1.72), scale=(0.98, 1.12, 1.10), mat=suit)
    parts["hood_back"] = S.sphere("HZ_hoodback", 0.185, 2, loc=(0, 0.10, 1.70), scale=(0.95, 1.0, 1.05), mat=suit)
    parts["neck"] = S.cylinder("HZ_neck", 0.085, 0.14, 22, loc=(0, 0.01, 1.53), mat=dark)
    parts["hood_collar"] = S.cylinder("HZ_hoodcollar", 0.155, 0.07, 26, loc=(0, 0.01, 1.56), mat=suit)
    parts["visor_frame"] = S.sphere("HZ_visorframe", 0.163, 2, loc=(0, -0.045, 1.72), scale=(0.86, 0.68, 0.88), mat=dark)
    parts["visor"] = S.sphere("HZ_visor", 0.150, 3, loc=(0, -0.095, 1.72), scale=(0.80, 0.52, 0.80), mat=visor)
    # дыхательные канистры по бокам капюшона (как на фото)
    for side, sx in (("L", -1), ("R", 1)):
        parts[f"filter_{side}"] = S.cylinder(f"HZ_filter{side}", 0.040, 0.070, 22,
                                             loc=(sx * 0.185, -0.02, 1.62), rot=(0, 0, math.radians(sx * 8)), mat=filt)
        parts[f"filter_cap_{side}"] = S.cylinder(f"HZ_filtercap{side}", 0.044, 0.018, 22,
                                                 loc=(sx * 0.185, -0.02, 1.585), mat=dark)

    # ---------- РУКИ: плечо-локоть-кисть ----------
    for side, sx in (("L", -1), ("R", 1)):
        parts[f"shoulder_{side}"] = S.sphere(f"HZ_shoulder{side}", 0.105, 2, loc=(sx * 0.205, 0, 1.44),
                                              scale=(1.0, 0.95, 0.95), mat=suit)
        parts[f"upperarm_{side}"] = S.capsule(f"HZ_upperarm{side}", 0.078, 0.24, loc=(sx * 0.215, 0, 1.28),
                                              rot=(0, math.radians(sx * 5), 0), mat=suit)
        parts[f"elbow_{side}"] = S.sphere(f"HZ_elbow{side}", 0.074, 2, loc=(sx * 0.222, 0, 1.14), mat=suit)
        parts[f"forearm_{side}"] = S.capsule(f"HZ_forearm{side}", 0.068, 0.24, loc=(sx * 0.228, -0.01, 0.99),
                                             rot=(math.radians(-8), math.radians(sx * 2), 0), mat=suit)
        parts[f"cuff_{side}"] = S.cylinder(f"HZ_cuff{side}", 0.078, 0.05, 22, loc=(sx * 0.232, -0.025, 0.855), mat=dark)
        parts[f"glove_{side}"] = S.box(f"HZ_glove{side}", (0.115, 0.19, 0.075),
                                       loc=(sx * 0.233, -0.045, 0.795), bevel=0.028, mat=gloves)
        parts[f"thumb_{side}"] = S.capsule(f"HZ_thumb{side}", 0.024, 0.05, loc=(sx * 0.19, -0.10, 0.795),
                                           rot=(math.radians(70), 0, 0), mat=gloves)

    # ---------- НОГИ: бедро-колено-голень-ботинок ----------
    for side, sx in (("L", -1), ("R", 1)):
        parts[f"hip_{side}"] = S.sphere(f"HZ_hip{side}", 0.115, 2, loc=(sx * 0.105, 0, 0.92), mat=suit)
        parts[f"thigh_{side}"] = S.capsule(f"HZ_thigh{side}", 0.098, 0.42, loc=(sx * 0.105, 0, 0.68), mat=suit)
        parts[f"knee_{side}"] = S.sphere(f"HZ_knee{side}", 0.093, 2, loc=(sx * 0.105, 0, 0.46), mat=suit)
        parts[f"shin_{side}"] = S.capsule(f"HZ_shin{side}", 0.085, 0.34, loc=(sx * 0.105, 0, 0.28), mat=suit)
        parts[f"ankle_{side}"] = S.cylinder(f"HZ_ankle{side}", 0.088, 0.05, 20, loc=(sx * 0.105, 0, 0.10), mat=dark)
        parts[f"boot_{side}"] = S.box(f"HZ_boot{side}", (0.115, 0.26, 0.11), loc=(sx * 0.105, -0.03, 0.055),
                                      bevel=0.022, mat=boots)
        parts[f"boot_toe_{side}"] = S.sphere(f"HZ_boottoe{side}", 0.062, 2, loc=(sx * 0.105, -0.155, 0.05),
                                             scale=(0.95, 1.35, 0.85), mat=boots)
        parts[f"boot_sole_{side}"] = S.box(f"HZ_bootsole{side}", (0.12, 0.30, 0.025), loc=(sx * 0.105, -0.045, 0.012),
                                           bevel=0.008, mat=dark)

    # ---------- ШЛАНГИ: от канистр вниз к регулятору на груди (две петли) ----------
    for side, sx in (("L", -1), ("R", 1)):
        pts = [(sx * 0.185, -0.02, 1.585), (sx * 0.20, -0.10, 1.50), (sx * 0.155, -0.155, 1.44),
               (sx * 0.075, -0.16, 1.44)]
        t = S.tube(f"HZ_hose{side}", pts, 0.022, mat=hose)
        S.curve_to_mesh(t)
        parts[f"hose_{side}"] = t

    # ---------- ФИНАЛЬНАЯ ОБРАБОТКА ----------
    objs = []
    round_names = ("hood", "visor", "glove", "shoulder", "hip", "knee", "elbow", "boottoe", "hoodback")
    for k, o in parts.items():
        if o is None:
            continue
        if any(n in k for n in round_names):
            S.add_subsurf(o, 1)
        S.smart_uv(o)
        objs.append(o)
    return objs


# --------------------------------------------------------------------------- #
#  МОНСТРЫ
# --------------------------------------------------------------------------- #
def build_smiler():
    """Smiler: тёмная фигура с фосфоресцирующей улыбкой (главный образ Level 0)."""
    body = S.pbr_material("M_SmilerBody", (0.03, 0.03, 0.035, 1), 0.0, 0.65, noise_scale=40, bump=0.5)
    glow = S.pbr_material("M_SmilerTeeth", (1.0, 0.98, 0.9, 1), 0.0, 0.2, emission=(1.0, 0.95, 0.75, 1), emission_strength=25.0)
    eye = S.pbr_material("M_SmilerEye", (0.9, 0.95, 1.0, 1), 0.0, 0.15, emission=(0.85, 0.95, 1.0, 1), emission_strength=18.0)
    p = [S.sphere("Smiler_head", 0.30, 2, loc=(0, 0, 1.60), scale=(1.0, 0.95, 1.05), mat=body),
         S.capsule("Smiler_torso", 0.26, 0.72, loc=(0, 0, 1.15), mat=body),
         S.capsule("Smiler_armL", 0.09, 0.66, loc=(-0.32, 0, 1.18), rot=(0, math.radians(6), 0), mat=body),
         S.capsule("Smiler_armR", 0.09, 0.66, loc=(0.32, 0, 1.18), rot=(0, math.radians(-6), 0), mat=body),
         S.capsule("Smiler_legL", 0.11, 0.78, loc=(-0.13, 0, 0.42), mat=body),
         S.capsule("Smiler_legR", 0.11, 0.78, loc=(0.13, 0, 0.42), mat=body)]
    # УЛЫБКА: 9 сегментов, выступающих ИЗ лица (y ≈ -0.29 при радиусе головы 0.30)
    for i in range(9):
        a = math.radians(-56 + i * 14)
        p.append(S.box(f"Smiler_tooth{i}", (0.030, 0.030, 0.055),
                       loc=(math.sin(a) * 0.225, -0.285 + abs(math.sin(a)) * 0.02,
                            1.545 - (1.0 - math.cos(a)) * 0.10),
                       rot=(0, 0, math.radians(-i * 7.5 + 30)), mat=glow))
    p.append(S.sphere("Smiler_eyeL", 0.052, 2, loc=(-0.105, -0.275, 1.72), scale=(1.25, 0.55, 0.85), mat=eye))
    p.append(S.sphere("Smiler_eyeR", 0.052, 2, loc=(0.105, -0.275, 1.72), scale=(1.25, 0.55, 0.85), mat=eye))
    return p

def build_hound():
    """Hound: тонкая четвероногая тварь с вытянутой пастью (Level 37)."""
    skin = S.pbr_material("M_HoundSkin", (0.42, 0.40, 0.30, 1), 0.0, 0.55, noise_scale=90, bump=0.7)
    bone = S.pbr_material("M_HoundBone", (0.82, 0.78, 0.66, 1), 0.0, 0.35, noise_scale=80, bump=0.4)
    maw = S.pbr_material("M_HoundMaw", (0.35, 0.06, 0.07, 1), 0.0, 0.5, emission=(0.6, 0.05, 0.05, 1), emission_strength=3.0)
    p = [S.capsule("Hound_body", 0.17, 0.72, loc=(0, 0, 0.62), rot=(math.radians(90), 0, 0), mat=skin),
         S.sphere("Hound_chest", 0.21, 2, loc=(0, -0.30, 0.62), scale=(1.0, 1.1, 1.05), mat=skin),
         S.capsule("Hound_neck", 0.12, 0.30, loc=(0, -0.55, 0.72), rot=(math.radians(65), 0, 0), mat=skin),
         S.sphere("Hound_head", 0.145, 2, loc=(0, -0.72, 0.86), scale=(0.9, 1.25, 0.95), mat=skin),
         S.cone("Hound_snout", 0.10, 0.05, 0.24, 20, loc=(0, -0.90, 0.83), rot=(math.radians(-95), 0, 0), mat=skin),
         S.box("Hound_maw", (0.09, 0.16, 0.05), loc=(0, -0.94, 0.80), rot=(math.radians(6), 0, 0), mat=maw)]
    for i in range(6):
        p.append(S.box(f"Hound_tooth{i}", (0.012, 0.012, 0.03),
                       loc=(-0.035 + (i % 3) * 0.035, -0.98 + (i // 3) * 0.05, 0.79), mat=bone))
    for i in range(3):  # хребетные шипы
        p.append(S.cone(f"Hound_spike{i}", 0.05, 0.005, 0.16, 14, loc=(0, -0.15 + i * 0.22, 0.78),
                        rot=(0, 0, 0), mat=bone))
    for sx in (-1, 1):
        for i, y in enumerate((-0.26, 0.26)):
            p.append(S.capsule(f"Hound_leg{'L' if sx < 0 else 'R'}{i}", 0.055, 0.52,
                               loc=(sx * 0.17, y, 0.30), rot=(0, 0, math.radians(sx * 4)), mat=skin))
            p.append(S.box(f"Hound_paw{'L' if sx < 0 else 'R'}{i}", (0.09, 0.14, 0.06),
                           loc=(sx * 0.18, y - 0.02, 0.05), mat=bone))
    p.append(S.tube("Hound_tail", [(0, 0.34, 0.66), (0, 0.52, 0.72), (0, 0.66, 0.60)], 0.035, mat=skin))
    for o in p:
        if o and o.type == 'CURVE': S.curve_to_mesh(o)
    return p

def build_partygoer():
    """Partygoer: «воздушный» человек с нарисованной улыбкой и лентами (Level 37)."""
    skin = S.pbr_material("M_PartySkin", (0.95, 0.78, 0.72, 1), 0.0, 0.42, noise_scale=60, bump=0.35)
    cloth = S.pbr_material("M_PartyCloth", (0.85, 0.20, 0.35, 1), 0.0, 0.6, noise_scale=80, bump=0.4)
    paint = S.pbr_material("M_PartyPaint", (0.02, 0.02, 0.02, 1), 0.0, 0.4)
    glow = S.pbr_material("M_PartyGlow", (0.95, 0.95, 0.2, 1), 0.0, 0.3, emission=(0.95, 0.9, 0.2, 1), emission_strength=6.0)
    p = [S.sphere("Party_head", 0.28, 3, loc=(0, 0, 1.72), scale=(1.05, 1.1, 1.05), mat=skin),
         S.capsule("Party_torso", 0.30, 0.70, loc=(0, 0, 1.15), mat=cloth),
         S.sphere("Party_belly", 0.34, 2, loc=(0, -0.05, 1.00), scale=(1.0, 1.05, 0.9), mat=cloth)]
    for sx in (-1, 1):
        p.append(S.capsule(f"Party_arm{'L' if sx < 0 else 'R'}", 0.075, 0.86,
                           loc=(sx * 0.34, 0, 1.20), rot=(0, math.radians(sx * 10), 0), mat=skin))
        p.append(S.sphere(f"Party_hand{'L' if sx < 0 else 'R'}", 0.10, 1,
                          loc=(sx * 0.36, 0, 0.74), mat=skin))
        p.append(S.capsule(f"Party_leg{'L' if sx < 0 else 'R'}", 0.10, 0.92,
                           loc=(sx * 0.15, 0, 0.48), mat=cloth))
    # нарисованная улыбка (7 сегментов) + глаза-точки
    for i in range(7):
        a = math.radians(-52 + i * 17)
        p.append(S.box(f"Party_smile{i}", (0.022, 0.02, 0.055),
                       loc=(math.sin(a) * 0.20, -0.26, 1.58 - math.cos(a) * 0.06), mat=paint))
    p.append(S.sphere("Party_eyeL", 0.038, 2, loc=(-0.10, -0.27, 1.76), scale=(1.1, 0.5, 1.1), mat=paint))
    p.append(S.sphere("Party_eyeR", 0.038, 2, loc=(0.10, -0.27, 1.76), scale=(1.1, 0.5, 1.1), mat=paint))
    # праздничные ленты (анимация — шейдером/скриптом)
    for i in range(4):
        a = i / 4 * math.tau
        t = S.tube(f"Party_ribbon{i}", [(math.cos(a) * 0.25, math.sin(a) * 0.25, 1.55),
                                        (math.cos(a) * 0.45, math.sin(a) * 0.45, 1.25),
                                        (math.cos(a) * 0.55, math.sin(a) * 0.55, 0.95)], 0.035, mat=cloth)
        S.curve_to_mesh(t)
        p.append(t)
    p.append(S.sphere("Party_badge", 0.07, 1, loc=(0, -0.31, 1.30), scale=(1, 0.4, 1), mat=glow))
    return p

def build_skinstealer():
    """Skin-Stealer: высокий, с чужой «кожей»-плащом (Level 3)."""
    flesh = S.pbr_material("M_StealerFlesh", (0.72, 0.52, 0.48, 1), 0.0, 0.48, noise_scale=70, bump=0.6)
    cloth = S.pbr_material("M_StealerCloak", (0.30, 0.26, 0.24, 1), 0.0, 0.72, noise_scale=100, bump=0.6)
    p = [S.capsule("Stealer_torso", 0.24, 0.86, loc=(0, 0, 1.30), mat=flesh),
         S.sphere("Stealer_head", 0.20, 2, loc=(0, 0, 1.92), scale=(0.95, 1.15, 1.05), mat=flesh),
         S.box("Stealer_mask", (0.24, 0.06, 0.26), loc=(0, -0.155, 1.91), bevel=0.02, mat=cloth),
         S.capsule("Stealer_armL", 0.09, 0.92, loc=(-0.30, 0, 1.22), rot=(0, math.radians(7), 0), mat=flesh),
         S.capsule("Stealer_armR", 0.09, 0.92, loc=(0.30, 0, 1.22), rot=(0, math.radians(-7), 0), mat=flesh),
         S.capsule("Stealer_legL", 0.12, 1.00, loc=(-0.14, 0, 0.45), mat=flesh),
         S.capsule("Stealer_legR", 0.12, 1.00, loc=(0.14, 0, 0.45), mat=flesh),
         S.sphere("Stealer_cloak", 0.42, 1, loc=(0, 0.10, 1.05), scale=(1.1, 0.7, 1.3), mat=cloth)]
    for sx in (-1, 1):
        p.append(S.box(f"Stealer_clawL{'' if sx<0 else 'R'}", (0.05, 0.16, 0.05),
                       loc=(sx * 0.30, -0.12, 0.76), rot=(math.radians(20), 0, 0), mat=flesh))
    return p

def build_bacteria():
    """Bacteria (босс): гигантская масса с щупальцами и слабой точкой-ядром."""
    mass = S.pbr_material("M_BacteriaMass", (0.30, 0.34, 0.22, 1), 0.0, 0.55, noise_scale=55, bump=0.8)
    core = S.pbr_material("M_BacteriaCore", (0.85, 0.95, 0.45, 1), 0.0, 0.3,
                          emission=(0.75, 0.95, 0.35, 1), emission_strength=14.0)
    p = [S.sphere("Bacteria_body", 1.05, 2, loc=(0, 0, 1.55), scale=(1.15, 1.0, 1.25), mat=mass),
         S.sphere("Bacteria_core", 0.34, 3, loc=(0, -0.62, 1.70), mat=core),
         S.sphere("Bacteria_head", 0.55, 2, loc=(0, -0.42, 2.55), scale=(1.0, 1.25, 0.9), mat=mass)]
    for i in range(10):  # щупальца
        a = i / 10 * math.tau
        r0 = 0.9
        pts = [(math.cos(a) * r0 * 0.6, math.sin(a) * r0 * 0.6, 1.9),
               (math.cos(a) * r0, math.sin(a) * r0, 1.4),
               (math.cos(a) * r0 * 1.25, math.sin(a) * r0 * 1.25, 0.85),
               (math.cos(a) * r0 * 1.1, math.sin(a) * r0 * 1.1, 0.25)]
        t = S.tube(f"Bacteria_tent{i}", pts, 0.10 - (i % 3) * 0.02, mat=mass)
        S.curve_to_mesh(t)
        p.append(t)
    for i in range(9):  # «зубы»
        a = math.radians(-60 + i * 15)
        p.append(S.cone(f"Bacteria_tooth{i}", 0.07, 0.004, 0.26, 12,
                        loc=(math.sin(a) * 0.42, -0.92, 2.45 - abs(math.cos(a)) * 0.16),
                        rot=(math.radians(180), 0, 0), mat=core))
    return p

# --------------------------------------------------------------------------- #
#  РИГ (костная привязка частей — импортируется Unity как skinned/hierarchy)
# --------------------------------------------------------------------------- #
def make_rig(name="Rig_Humanoid", height=1.85):
    arm_data = bpy.data.armatures.new(name)
    arm = bpy.data.objects.new(name, arm_data)
    bpy.context.collection.objects.link(arm)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='EDIT')
    eb = arm_data.edit_bones
    def bone(bname, head, tail, parent=None):
        b = eb.new(bname); b.head = Vector(head); b.tail = Vector(tail)
        if parent: b.parent = parent; b.use_connect = False
        return b
    hips = bone("hips", (0, 0, 0.92), (0, 0, 1.06))
    spine = bone("spine", (0, 0, 1.06), (0, 0, 1.50), hips)
    head = bone("head", (0, 0, 1.50), (0, 0, 1.82), spine)
    for side, sx in (("L", -1), ("R", 1)):
        sh = bone(f"shoulder.{side}", (sx * 0.12, 0, 1.45), (sx * 0.24, 0, 1.44), spine)
        ua = bone(f"upperarm.{side}", (sx * 0.24, 0, 1.44), (sx * 0.27, 0, 1.12), sh)
        fa = bone(f"forearm.{side}", (sx * 0.27, 0, 1.12), (sx * 0.28, 0, 0.82), ua)
        bone(f"hand.{side}", (sx * 0.28, 0, 0.82), (sx * 0.28, -0.06, 0.74), fa)
        th = bone(f"thigh.{side}", (sx * 0.12, 0, 0.90), (sx * 0.12, 0, 0.50), hips)
        sn = bone(f"shin.{side}", (sx * 0.12, 0, 0.50), (sx * 0.12, 0, 0.12), th)
        bone(f"foot.{side}", (sx * 0.12, 0, 0.12), (sx * 0.12, -0.20, 0.06), sn)
    bpy.ops.object.mode_set(mode='OBJECT')
    return arm

def bind_parts_to_bones(parts_map, arm):
    """Раскладываем части по костям (родитель-кость) — быстро и надёжно для прототипа."""
    rule = [
        (("hood", "visor", "head", "eye", "tooth"), "head"),
        (("torso", "chest", "belt", "pocket", "name", "hose", "filter", "regulator", "backpack"), "spine"),
        (("shoulder", "upperarm"), None),      # по стороне
        (("forearm", "cuff", "glove", "thumb"), None),
        (("thigh", "hip"), None),
        (("shin", "boot", "ankle"), None),
    ]
    for obj in parts_map:
        low = obj.name.lower()
        bone_name = None
        for keys, target in rule:
            if any(k in low for k in keys):
                if target: bone_name = target
                else:
                    side = "L" if ("l" in low[-1].lower() or "_l" in low or low.endswith("l")) else "R"
                    if "shoulder" in low or "upperarm" in low: bone_name = f"upperarm.{side}"
                    elif "forearm" in low or "cuff" in low or "glove" in low or "thumb" in low: bone_name = f"forearm.{side}"
                    elif "thigh" in low or "hip" in low: bone_name = f"thigh.{side}"
                    elif "shin" in low: bone_name = f"shin.{side}"
                    elif "boot" in low or "ankle" in low: bone_name = f"foot.{side}"
                break
        if not bone_name: continue
        obj.parent = arm
        obj.parent_type = 'BONE'
        obj.parent_bone = bone_name
        # компенсируем смещение кости
        b = arm.data.bones.get(bone_name)
        if b:
            obj.matrix_parent_inverse = (arm.matrix_world @ b.matrix_local).inverted()

# --------------------------------------------------------------------------- #
#  СБОРКА
# --------------------------------------------------------------------------- #
def build_all(render=True):
    """Строит/экспортирует/рендерит каждый персонаж по отдельности (чистая сцена каждый раз)."""
    builders = [
        ("CH_hazmat_suit", "suit"),
        ("MN_smiler", "raw"), ("MN_hound", "raw"),
        ("MN_partygoer", "raw"), ("MN_skinstealer", "raw"), ("MN_bacteria", "raw"),
    ]
    render_dir = os.path.abspath(os.path.join(OUT, "..", "..", "..", "..", "..", "docs", "previews"))
    total = 0
    for name, mode in builders:
        S.clean_scene()
        if mode == "suit":
            parts = build_hazmat_suit()
            joined = S.join_objects(parts, name)
        else:
            fn = {"MN_smiler": build_smiler, "MN_hound": build_hound, "MN_partygoer": build_partygoer,
                  "MN_skinstealer": build_skinstealer, "MN_bacteria": build_bacteria}[name]
            parts = [o for o in fn() if o and o.type == 'MESH']
            for o in parts:
                S.smart_uv(o)
            joined = S.join_objects(parts, name)
        if joined is None:
            continue
        S.shade_smooth(joined, 40)
        tris = S.tri_count(joined)
        total += tris
        S.export_fbx([joined], os.path.join(OUT, name + ".fbx"))
        S.export_glb([joined], os.path.join(OUT, name + ".glb"))
        print(f"[chars] {name}: {tris} трис, материалов {len(joined.data.materials)}")
        if render:
            S.render_fit(os.path.join(render_dir, name + ".png"), [joined], samples=32, res=(600, 800), fov_deg=32)
    print(f"[chars] ИТОГО трис: {total}")

if __name__ == "__main__":
    build_all(render="--no-render" not in sys.argv)
