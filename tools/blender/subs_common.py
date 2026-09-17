"""
SUBSISTENCE — subs_common.py
Общие утилиты Blender-пайплайна: чистая сцена, меш-примитивы через bmesh,
PBR-материалы (процедурные, без внешних текстур), UV, модификаторы, экспорт в
FBX (Unity 2022.3) и GLB, а также превью-рендер на Cycles (CPU, headless).

Запуск: blender --background --factory-startup --python <модельный скрипт>
"""
import bpy, bmesh, math, os, sys
from mathutils import Vector, Matrix, Euler

# --------------------------------------------------------------------------- #
#  СЦЕНА
# --------------------------------------------------------------------------- #
def clean_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    for block in (bpy.data.meshes, bpy.data.materials, bpy.data.objects,
                  bpy.data.images, bpy.data.curves, bpy.data.armatures):
        for item in list(block):
            block.remove(item)

def ensure_dir(path):
    os.makedirs(path, exist_ok=True)
    return path

# --------------------------------------------------------------------------- #
#  МЕШ-ПРИМИТИВЫ (bmesh, без операторов — надёжно в headless)
# --------------------------------------------------------------------------- #
def _new_obj(name, bm, mat=None, smooth=False):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    if smooth:
        for p in me.polygons:
            p.use_smooth = True
    obj = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(obj)
    if mat:
        obj.data.materials.append(mat)
    return obj

def box(name, size=(1, 1, 1), loc=(0, 0, 0), rot=(0, 0, 0), bevel=0.0, segments=2, mat=None, taper=None):
    """Параллелепипед. taper=(sx,sy) — сужение верхней грани (для магазинов/прикладов)."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
    if taper:
        for v in bm.verts:
            if v.co.z > 0:
                v.co.x *= taper[0]
                v.co.y *= taper[1]
    if bevel > 0:
        bmesh.ops.bevel(bm, geom=list(bm.verts) + list(bm.edges) + list(bm.faces),
                        offset=bevel, segments=segments, affect='EDGES', profile=0.7, clamp_overlap=True)
    obj = _new_obj(name, bm, mat)
    obj.location = loc
    obj.rotation_euler = Euler(rot)
    return obj

def cylinder(name, radius=0.1, depth=1.0, verts=24, loc=(0, 0, 0), rot=(0, 0, 0), mat=None, cap=True, bevel=0.0):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=cap, cap_tris=False, segments=verts,
                          radius1=radius, radius2=radius, depth=depth)
    if bevel > 0:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, segments=2, affect='EDGES', clamp_overlap=True)
    obj = _new_obj(name, bm, mat)
    obj.location = loc
    obj.rotation_euler = Euler(rot)
    return obj

def cone(name, r1=0.1, r2=0.05, depth=0.4, verts=20, loc=(0, 0, 0), rot=(0, 0, 0), mat=None):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=verts,
                          radius1=r1, radius2=r2, depth=depth)
    obj = _new_obj(name, bm, mat)
    obj.location = loc
    obj.rotation_euler = Euler(rot)
    return obj

def sphere(name, radius=0.5, subdiv=0, loc=(0, 0, 0), scale=(1, 1, 1), mat=None, smooth=True,
           u_seg=20, v_seg=12):
    """Сфера. subdiv=0 → готовая плотность (u_seg×v_seg), subdiv>0 → ×4^n граней.
    Для «ультра» деталей (капюшон, визор) передавай u_seg=32, v_seg=18, subdiv=1."""
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=u_seg, v_segments=v_seg, radius=radius)
    bmesh.ops.scale(bm, vec=Vector(scale), verts=bm.verts)
    if subdiv > 0:
        bmesh.ops.subdivide_edges(bm, edges=list(bm.edges), cuts=subdiv, use_grid_fill=True)
    obj = _new_obj(name, bm, mat, smooth)
    obj.location = loc
    return obj

def capsule(name, radius=0.2, height=1.0, loc=(0, 0, 0), rot=(0, 0, 0), mat=None, smooth=True,
            r1=None, r2=None, segments=18, cap_segments=12):
    """Капсула (цилиндр + полусферы). r1/r2 — радиусы торцов (конусность для конечностей)."""
    height = max(0.001, height)
    r1 = radius if r1 is None else r1
    r2 = radius if r2 is None else r2
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segments,
                          radius1=r1, radius2=r2, depth=height)
    for sign, rr in ((1, r1), (-1, r2)):
        sph = bmesh.new()
        bmesh.ops.create_uvsphere(sph, u_segments=segments, v_segments=cap_segments, radius=rr)
        bmesh.ops.translate(sph, vec=Vector((0, 0, sign * height * 0.5)), verts=sph.verts)
        me_tmp = bpy.data.meshes.new("_tmp")
        sph.to_mesh(me_tmp); sph.free()
        bm.from_mesh(me_tmp)
        bpy.data.meshes.remove(me_tmp)
    obj = _new_obj(name, bm, mat, smooth)
    obj.location = loc
    obj.rotation_euler = Euler(rot)
    return obj


def tube(name, points, radius=0.03, mat=None, resolution=8, smooth=True):
    """Труба по точкам (шланги, кабели, поручни, трубы станции)."""
    curve = bpy.data.curves.new(name + "_c", 'CURVE')
    curve.dimensions = '3D'
    curve.resolution_u = resolution
    curve.bevel_depth = radius
    curve.bevel_resolution = 3
    spline = curve.splines.new('POLY')
    spline.points.add(len(points) - 1)
    for i, p in enumerate(points):
        spline.points[i].co = (p[0], p[1], p[2], 1.0)
    obj = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(mat)
    return obj

def curve_to_mesh(obj):
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.convert(target='MESH')
    obj.select_set(False)
    return obj

def grate_panel(name, size=(1.2, 0.1, 0.6), bars=7, loc=(0, 0, 0), mat_frame=None, mat_glass=None, rot=None):
    """Потолочная лампа-решётка из Level 0 (главный визуальный якорь коридоров)."""
    parts = [box(name + "_frame", size=(size[0], size[2], 0.05), loc=(loc[0], loc[1], loc[2] + 0.03), mat=mat_frame)]
    step = size[0] / (bars + 1)
    for i in range(bars):
        x = loc[0] - size[0] * 0.5 + step * (i + 1)
        parts.append(box(f"{name}_bar{i}", size=(0.06, size[2] * 0.9, 0.03),
                         loc=(x, loc[1], loc[2]), mat=mat_frame))
    parts.append(box(name + "_glass", size=(size[0] * 0.98, size[2], 0.12),
                     loc=(loc[0], loc[1], loc[2] + 0.07), mat=mat_glass))
    if rot is not None:
        parts = _rotate_around(parts, loc, rot)
    return parts


def _rotate_around(parts, pivot, rot):
    """Поворот готовых деталей вокруг точки pivot (для панелей с наклоном)."""
    from mathutils import Euler, Vector
    e = Euler(rot)
    for o in parts:
        off = Vector(o.location) - Vector(pivot)
        off.rotate(e)
        o.location = Vector(pivot) + off
        o.rotation_euler = e
    return parts

# --------------------------------------------------------------------------- #
#  МАТЕРИАЛЫ (PBR, процедурные — работают в HDRP после импорта FBX+материалов)
# --------------------------------------------------------------------------- #
def pbr_material(name, base=(0.8, 0.8, 0.8, 1), metallic=0.0, roughness=0.5,
                 emission=None, emission_strength=2.0, noise_scale=0.0, bump=0.0,
                 specular=0.5, sheen=0.0, alpha=1.0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial"); out.location = (600, 0)
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled"); bsdf.location = (200, 0)
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])

    def setv(key, val):
        if key in bsdf.inputs:
            bsdf.inputs[key].default_value = val

    setv("Base Color", base)
    setv("Metallic", metallic)
    setv("Roughness", roughness)
    setv("Specular IOR Level", specular)
    setv("Specular", specular)
    setv("Sheen Weight", sheen)
    setv("Alpha", alpha)
    if emission:
        setv("Emission Color", emission)
        setv("Emission", emission)
        setv("Emission Strength", emission_strength)
    if alpha < 1.0:
        mat.blend_method = 'BLEND'

    # Процедурный шум → bump (ткань, бетон, ковролин, обои)
    if noise_scale > 0:
        tex = nt.nodes.new("ShaderNodeTexNoise"); tex.location = (-400, -200)
        tex.inputs["Scale"].default_value = noise_scale
        tex.inputs["Detail"].default_value = 8.0
        tex.inputs["Roughness"].default_value = 0.6
        bmp = nt.nodes.new("ShaderNodeBump"); bmp.location = (-100, -250)
        bmp.inputs["Strength"].default_value = bump
        bmp.inputs["Distance"].default_value = 0.01
        nt.links.new(tex.outputs["Fac"], bmp.inputs["Height"])
        nt.links.new(bmp.outputs["Normal"], bsdf.inputs["Normal"])

        # затемнение в «швы» ткани: множим ШУМ на БАЗОВЫЙ ЦВЕТ (без потери оттенка!)
        mix = nt.nodes.new("ShaderNodeMixRGB")
        mix.location = (0, 100)
        mix.blend_type = 'MULTIPLY'
        mix.inputs["Fac"].default_value = 0.28
        mix.inputs["Color1"].default_value = base
        nt.links.new(tex.outputs["Color"], mix.inputs["Color2"])
        nt.links.new(mix.outputs["Color"], bsdf.inputs["Base Color"])
    return mat

def mats_pack(theme="corridors"):
    """Набор материалов под тему уровня (используется и моделями, и превью)."""
    if theme == "corridors":
        return dict(
            wallpaper=pbr_material("M_Wallpaper_L0", (0.72, 0.65, 0.33, 1), 0.0, 0.72, noise_scale=45, bump=0.35),
            carpet=pbr_material("M_Carpet_L0", (0.60, 0.55, 0.30, 1), 0.0, 0.92, noise_scale=120, bump=0.5, sheen=0.4),
            ceiling=pbr_material("M_Ceiling_L0", (0.78, 0.76, 0.60, 1), 0.0, 0.66, noise_scale=60, bump=0.2),
            lamp=pbr_material("M_LampEmissive", (1.0, 0.98, 0.86, 1), 0.0, 0.3, emission=(1.0, 0.97, 0.85, 1), emission_strength=8.0),
            metal=pbr_material("M_SteelDark", (0.32, 0.33, 0.35, 1), 0.9, 0.35),
            plastic=pbr_material("M_PlasticDark", (0.08, 0.08, 0.09, 1), 0.0, 0.42),
        )
    if theme == "poolrooms":
        return dict(
            tile=pbr_material("M_Tile_L1", (0.84, 0.88, 0.87, 1), 0.0, 0.22, noise_scale=200, bump=0.12),
            tile_dark=pbr_material("M_TileDark_L1", (0.35, 0.52, 0.55, 1), 0.0, 0.28, noise_scale=150, bump=0.15),
            water=pbr_material("M_Water_L1", (0.10, 0.45, 0.52, 1), 0.0, 0.03, alpha=0.72),
            rust=pbr_material("M_RustMetal", (0.35, 0.22, 0.14, 1), 0.7, 0.65, noise_scale=90, bump=0.4),
            metal=pbr_material("M_SteelWet", (0.45, 0.47, 0.49, 1), 0.85, 0.25),
        )
    return dict(
        concrete=pbr_material("M_Concrete_L2", (0.34, 0.34, 0.36, 1), 0.0, 0.82, noise_scale=35, bump=0.3),
        concrete_dark=pbr_material("M_ConcreteDark_L2", (0.20, 0.21, 0.22, 1), 0.0, 0.85, noise_scale=30, bump=0.35),
        metal=pbr_material("M_Metal_L2", (0.47, 0.49, 0.52, 1), 0.92, 0.33),
        copper=pbr_material("M_Copper", (0.62, 0.36, 0.18, 1), 0.95, 0.30),
        hazard=pbr_material("M_HazardStripe", (0.85, 0.72, 0.05, 1), 0.0, 0.5, noise_scale=10),
        reactor=pbr_material("M_ReactorGlow", (0.18, 0.35, 0.30, 1), 0.4, 0.35,
                             emission=(0.15, 0.95, 0.55, 1), emission_strength=12.0,
                             noise_scale=20, bump=0.2),
        rubber=pbr_material("M_Rubber", (0.06, 0.06, 0.07, 1), 0.0, 0.75, noise_scale=80, bump=0.3),
    )

# --------------------------------------------------------------------------- #
#  МОДИФИКАТОРЫ / UV
# --------------------------------------------------------------------------- #
def add_subsurf(obj, levels=2, apply=True):
    m = obj.modifiers.new("Subsurf", 'SUBSURF')
    m.levels = levels
    m.render_levels = levels
    if apply: apply_modifiers(obj)
    return obj

def add_bevel(obj, width=0.01, segments=2):
    m = obj.modifiers.new("Bevel", 'BEVEL')
    m.width = width
    m.segments = segments
    m.limit_method = 'ANGLE'
    return obj

def add_solidify(obj, thickness=0.02):
    m = obj.modifiers.new("Solidify", 'SOLIDIFY')
    m.thickness = thickness
    return obj

def apply_modifiers(obj):
    bpy.context.view_layer.objects.active = obj
    for m in list(obj.modifiers):
        try:
            bpy.ops.object.modifier_apply(modifier=m.name)
        except Exception as e:
            print(f"[warn] modifier_apply {obj.name}/{m.name}: {e}")

def smart_uv(obj, angle=66.0, island_margin=0.02):
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    try:
        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.uv.smart_project(angle_limit=math.radians(angle), island_margin=island_margin)
        bpy.ops.object.mode_set(mode='OBJECT')
    except Exception as e:
        print(f"[warn] uv {obj.name}: {e}")
    obj.select_set(False)

def shade_smooth(obj, angle=40):
    """Сглаживание с учётом угла — версия-безопасно (Blender 4.1+ убрал use_auto_smooth)."""
    if obj is None or obj.type != 'MESH':
        return obj
    for p in obj.data.polygons:
        p.use_smooth = True
    try:
        # Blender 4.1+: модификатор/оператор «Smooth by Angle»
        obj.data.use_auto_smooth = True
        obj.data.auto_smooth_angle = math.radians(angle)
    except AttributeError:
        try:
            bpy.context.view_layer.objects.active = obj
            obj.select_set(True)
            bpy.ops.object.shade_smooth_by_angle(angle=math.radians(angle))
            obj.select_set(False)
        except Exception:
            pass   # остаёмся на полном сглаживании — визуально допустимо
    return obj

def join_objects(objs, name="Joined"):
    """
    Надёжное объединение: бакаем мировые трансформы и ПЕРЕНОСИМ материалы
    с ремапом material_index (bpy.ops.object.join теряет слоты при разных наборах).
    """
    objs = [o for o in objs if o is not None and o.type == 'MESH']
    if not objs:
        return None
    # КРИТИЧНО: matrix_world вычисляется лениво — без update() позиции/повороты
    # только что созданных объектов ещё не применены, и части «слипаются» в ноль.
    bpy.context.view_layer.update()

    mats = []
    for o in objs:
        for m in o.data.materials:
            if m is not None and m not in mats:
                mats.append(m)

    bm = bmesh.new()
    for o in objs:
        me = o.data.copy()
        me.transform(o.matrix_world)               # запекаем позицию/поворот/масштаб
        local = list(me.materials)
        remap = []
        for m in local:
            remap.append(mats.index(m) if (m is not None and m in mats) else 0)
        if not remap:
            remap = [0]
        for poly in me.polygons:
            idx = poly.material_index if poly.material_index < len(remap) else 0
            poly.material_index = remap[idx]
        bm.from_mesh(me)
        bpy.data.meshes.remove(me)

    merged = bpy.data.meshes.new(name)
    bm.to_mesh(merged)
    bm.free()
    for m in mats:
        merged.materials.append(m)

    out = bpy.data.objects.new(name, merged)
    bpy.context.collection.objects.link(out)
    for o in objs:
        data = o.data
        bpy.data.objects.remove(o, do_unlink=True)
        if data.users == 0:
            bpy.data.meshes.remove(data)
    print(f"[join] {name}: материалов {len(merged.materials)}, полигонов {len(merged.polygons)}")
    return out


def tri_count(obj):
    me = obj.data
    return sum(len(p.vertices) - 2 for p in me.polygons)

# --------------------------------------------------------------------------- #
#  ЭКСПОРТ
# --------------------------------------------------------------------------- #
def export_fbx(objs, filepath, scale=1.0):
    ensure_dir(os.path.dirname(filepath))
    bpy.context.view_layer.update()
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        if o: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0] if objs else None
    try:
        bpy.ops.export_scene.fbx(
            filepath=filepath, use_selection=True, global_scale=scale,
            apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
            axis_forward='-Z', axis_up='Y',     # настройки Unity по умолчанию
            object_types={'MESH', 'ARMATURE', 'EMPTY'},
            mesh_smooth_type='FACE', use_mesh_modifiers=True,
            path_mode='COPY', embed_textures=False, bake_space_transform=False)
        print(f"[export] FBX → {filepath}")
        return True
    except Exception as e:
        print(f"[error] FBX экспорт не удался: {e}")
        return False

def export_glb(objs, filepath):
    ensure_dir(os.path.dirname(filepath))
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        if o: o.select_set(True)
    try:
        bpy.ops.export_scene.gltf(filepath=filepath, use_selection=True,
                                  export_format='GLB', export_apply=True)
        print(f"[export] GLB → {filepath}")
        return True
    except Exception as e:
        print(f"[error] GLB экспорт не удался: {e}")
        return False

def save_blend(filepath):
    ensure_dir(os.path.dirname(filepath))
    bpy.ops.wm.save_as_mainfile(filepath=filepath)
    print(f"[export] BLEND → {filepath}")

# --------------------------------------------------------------------------- #
#  ПРЕВЬЮ-РЕНДЕР (Cycles CPU — работает в headless без GPU)
# --------------------------------------------------------------------------- #
def render_preview(filepath, target=(0, 0, 0.9), distance=2.6, height=1.2,
                   samples=48, res=(640, 800), fov_deg=40,
                   bg=(0.045, 0.045, 0.05), key_energy=260, rim_energy=110,
                   floor_z=None, azimuth=-62.0, elevation=14.0):
    """Превью-рендер (Cycles CPU). target — центр модели; остальное считается от габаритов."""
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    try:
        scene.cycles.device = 'CPU'
        scene.cycles.samples = samples
        scene.cycles.use_denoising = True
        scene.cycles.max_bounces = 4
        scene.cycles.transparent_max_bounces = 4
    except Exception:
        pass
    scene.render.resolution_x, scene.render.resolution_y = res
    scene.render.film_transparent = False
    scene.render.image_settings.file_format = 'PNG'
    try:
        scene.view_settings.view_transform = 'Standard'   # верная передача цвета материалов
    except Exception:
        pass
    scene.view_settings.exposure = 0.0
    scene.view_settings.look = 'None'

    world = bpy.data.worlds.new("PreviewWorld")
    scene.world = world
    world.use_nodes = True
    node = world.node_tree.nodes.get("Background")
    if node:
        node.inputs[0].default_value = (bg[0], bg[1], bg[2], 1)
        node.inputs[1].default_value = 1.2

    tgt = Vector(target)
    az, el = math.radians(azimuth), math.radians(elevation)
    offset = Vector((math.cos(az) * math.cos(el), math.sin(az) * math.cos(el), math.sin(el))) * distance

    cam_data = bpy.data.cameras.new("PreviewCam")
    cam_data.sensor_fit = 'VERTICAL'
    cam_data.angle = math.radians(fov_deg)
    cam = bpy.data.objects.new("PreviewCam", cam_data)
    bpy.context.collection.objects.link(cam)
    cam.location = tgt + offset
    cam.rotation_euler = (tgt - cam.location).to_track_quat('-Z', 'Y').to_euler()
    scene.camera = cam

    for name, energy, loc, size in (
        ("Key", key_energy, (-2.2, -2.6, 2.6), 3.0),
        ("Rim", rim_energy, (2.6, 1.8, 1.9), 2.2),
        ("Fill", rim_energy * 0.6, (0.4, 2.8, 1.2), 4.0)):
        ld = bpy.data.lights.new(name, 'AREA')
        ld.energy = energy
        ld.size = size
        lo = bpy.data.objects.new(name, ld)
        bpy.context.collection.objects.link(lo)
        lo.location = Vector(loc) * (distance * 0.55)
        lo.rotation_euler = (tgt - Vector(lo.location)).to_track_quat('-Z', 'Y').to_euler()

    # подложка ПОД моделью (иначе модель утопает в полу)
    z_floor = floor_z if floor_z is not None else tgt.z - max(0.05, distance * 0.22)
    floor = box("PreviewFloor", (distance * 4, distance * 4, 0.04),
                loc=(tgt.x, tgt.y, z_floor),
                mat=pbr_material("M_PreviewFloor", (0.10, 0.10, 0.11, 1), 0.0, 0.55))

    ensure_dir(os.path.dirname(filepath))
    scene.render.filepath = filepath
    try:
        bpy.ops.render.render(write_still=True)
        print(f"[render] {filepath} (dist={distance:.2f}, floor_z={z_floor:.2f})")
    except Exception as e:
        print(f"[warn] рендер не удался: {e}")
    return filepath


def report(objs, title=""):
    total = 0
    for o in objs:
        if o and o.type == 'MESH':
            n = tri_count(o)
            total += n
            print(f"  • {o.name:<28} трис: {n:>7}")
    print(f"[stats] {title}: объектов {len([o for o in objs if o])}, трис всего {total}")
    return total


def bounds_of(objs):
    """Габариты набора объектов в мировых координатах."""
    import mathutils
    bpy.context.view_layer.update()
    mn = mathutils.Vector((1e9, 1e9, 1e9))
    mx = mathutils.Vector((-1e9, -1e9, -1e9))
    for o in objs:
        if o is None or o.type != 'MESH':
            continue
        for corner in o.bound_box:
            w = o.matrix_world @ mathutils.Vector(corner)
            mn = mathutils.Vector((min(mn.x, w.x), min(mn.y, w.y), min(mn.z, w.z)))
            mx = mathutils.Vector((max(mx.x, w.x), max(mx.y, w.y), max(mx.z, w.z)))
    center = (mn + mx) * 0.5
    size = (mx - mn)
    diag = max(0.1, (size.x ** 2 + size.y ** 2 + size.z ** 2) ** 0.5)
    return center, size, diag


def render_fit(filepath, objs, samples=32, res=(640, 640), fov_deg=32, pad=1.28, floor_z=None, azimuth=-62.0, elevation=14.0, min_dist=0.0):
    """Кадрирует модель по габаритам: расстояние от максимального габарита и FOV.
    floor_z — уровень «пола»; по умолчанию ниже самой модели (иначе пол режет низ объекта)."""
    center, size, diag = bounds_of(objs)
    extent = max(size.x, size.y, size.z)
    dist = max(min_dist, (extent * 0.5) / math.tan(math.radians(fov_deg) * 0.5) * pad)   # min_dist — для мелких деталей (замки)
    if floor_z is None:
        min_z = center.z - size.z * 0.5
        floor_z = min(min_z - 0.012, center.z - max(0.06, extent * 0.32))
    return render_preview(filepath, target=(center.x, center.y, center.z),
                          distance=dist, height=size.z * 0.3, samples=samples, res=res,
                          fov_deg=fov_deg, floor_z=floor_z, azimuth=azimuth, elevation=elevation)
