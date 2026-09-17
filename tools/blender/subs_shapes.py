"""
SUBSISTENCE — subs_shapes.py
Профильные формы для хард-сюрфейса (оружие, декор уровней). Тот самый инструмент,
которого не хватало в v2: вместо «склейки кубиков» — силуэты и лофт по сечениям.

Координаты оружия: ствол смотрит в +Y, Z — вверх, X — вправо.
  • profile(...)  — 2D-силуэт в плоскости YZ, выдавленный по X (ресивер, приклад, цевьё).
  • loft(...)     — труба, сшитая из колец сечений (стволы с уступом, магазины, дульные
                    устройства, кожухи).
  • loft_path(...) — то же, но кольца расставлены вдоль кривой (изогнутый магазин АК).
  • ring_rect / ring_circle / ring_round — сечения.
  • tube_between / cyl_y — цилиндры между точками и вдоль Y.
  • hex_nut / screw — мелочёвка.

Все функции возвращают объект bpy (mesh), материал — по желанию. Нормали
пересчитываются bmesh'ем, поэтому капы не «выворачиваются».
"""
import bpy
import bmesh
import math
from mathutils import Vector

R = math.radians


# --------------------------------------------------------------------------- #
def mesh_obj(name, verts, faces, mat=None, smooth=False):
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], faces)
    me.validate(verbose=False)
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    if mat is not None:
        ob.data.materials.append(mat)
    if smooth:
        for p in me.polygons:
            p.use_smooth = True
    return ob


# ------------------------------------------------------------------ сечения
def ring_rect(w, h, c=0.0, n_corner=2):
    """Прямоугольник (X×Z) со срезанными углами. Возвращает [(x,z)] по кругу."""
    hw, hh = w * 0.5, h * 0.5
    if c <= 0.0:
        return [(-hw, -hh), (hw, -hh), (hw, hh), (-hw, hh)]
    c = min(c, hw * 0.9, hh * 0.9)
    pts = []
    for sx, sz, a0 in ((-1, -1, 180), (1, -1, 270), (1, 1, 0), (-1, 1, 90)):
        for i in range(n_corner + 1):
            a = R(a0 + 90.0 * i / (n_corner + 1.0))
            pts.append((sx * (hw - c) + math.cos(a) * c, sz * (hh - c) + math.sin(a) * c))
    # убрать дубли
    out = []
    for p in pts:
        if not out or (abs(p[0] - out[-1][0]) > 1e-6 or abs(p[1] - out[-1][1]) > 1e-6):
            out.append(p)
    return out


def ring_circle(r, n=16):
    return [(math.cos(2 * math.pi * i / n) * r, math.sin(2 * math.pi * i / n) * r) for i in range(n)]


def ring_round(w, h, bulge=0.35, n=16):
    """Скруглённое сечение (эллипс с плоскими боками) — ресиверы, цевьё, кожухи."""
    pts = []
    for i in range(n):
        a = 2 * math.pi * i / n
        x = math.cos(a) * w * 0.5
        z = math.sin(a) * h * 0.5 * (1.0 - bulge * abs(math.cos(a)) ** 2)
        pts.append((x, z))
    return pts


# ------------------------------------------------------------------- фигуры
def profile(name, pts, width, y=0.0, x=0.0, z=0.0, mat=None, smooth=False):
    """Силуэт pts=[(y,z)] (вид сбоку) → деталь толщиной width по X.
    Терпит позиционный вызов profile(name, pts, w, y, z, mat): материал распознаётся
    по типу (не число) и переезжает в нужный аргумент."""
    if mat is None and not isinstance(z, (int, float)):
        mat, z, x = z, (x if isinstance(x, (int, float)) else 0.0), 0.0
    if mat is None and not isinstance(x, (int, float)):
        mat, x = x, 0.0
    hw = width * 0.5
    n = len(pts)
    verts = [(x - hw, y + p[0], z + p[1]) for p in pts] + \
            [(x + hw, y + p[0], z + p[1]) for p in pts]
    faces = []
    for i in range(n):
        j = (i + 1) % n
        faces.append((i, j, n + j, n + i))
    faces.append(tuple(range(n)))
    faces.append(tuple(range(n, 2 * n)))
    return mesh_obj(name, verts, faces, mat, smooth)


def loft(name, sections, mat=None, smooth=False, cap=True):
    """sections = [(y, ring)] , ring = [(x,z)] (одинаковое число точек)."""
    n = len(sections[0][1])
    verts, faces = [], []
    for y, ring in sections:
        for (x, z) in ring:
            verts.append((x, y, z))
    m = len(sections)
    for s in range(m - 1):
        for i in range(n):
            j = (i + 1) % n
            a = s * n + i
            b = s * n + j
            c = (s + 1) * n + j
            d = (s + 1) * n + i
            faces.append((a, b, c, d))
    if cap:
        faces.append(tuple(range(n)))
        faces.append(tuple(range((m - 1) * n, m * n)))
    return mesh_obj(name, verts, faces, mat, smooth)


def ring_xy(w, d, cx=0.0, cy=0.0, c=0.0):
    """Кольцо в плоскости XY (для вертикальных фигур): X — ширина, Y — глубина."""
    return [(x + cx, y + cy) for (x, y) in ring_rect(w, d, c, 2)]


def ring_disc(w, d, cx=0.0, cy=0.0, n=18):
    """Эллиптическое кольцо: круглые в плане фигуры (банки, бутылки, рулоны, плоды)."""
    hw, hd = w * 0.5, d * 0.5
    return [(cx + math.cos(R(360.0 * i / n)) * hw, cy + math.sin(R(360.0 * i / n)) * hd) for i in range(n)]


def loft_z(name, sections, mat=None, smooth=True, cap=True):
    """Стек колец по высоте: sections = [(z, [(x,y)] )] — вертикальные фигуры (торс, шея).
    Центр каждого кольца можно смещать — так делаются наклоны и изгибы без поворотов."""
    n = len(sections[0][1])
    verts, faces = [], []
    for z, ring in sections:
        for (x, y) in ring:
            verts.append((x, y, z))
    m = len(sections)
    for s_i in range(m - 1):
        for i in range(n):
            j = (i + 1) % n
            faces.append((s_i * n + i, s_i * n + j, (s_i + 1) * n + j, (s_i + 1) * n + i))
    if cap:
        faces.append(tuple(range(n)))
        faces.append(tuple(range((m - 1) * n, m * n)))
    return mesh_obj(name, verts, faces, mat, smooth)


def loft_path(name, path, w, h, corner=0.0, mat=None, smooth=True, cap=True):
    """path = [(y, z, tilt_deg)] — кольца ставятся вдоль кривой с доворотом (магазины)."""
    ring0 = ring_rect(w, h, corner, 2)
    secs = []
    for (y, z, tilt) in path:
        a = R(tilt)
        ca, sa = math.cos(a), math.sin(a)
        ring = [(px * ca - pz * sa, px * sa + pz * ca) for (px, pz) in ring0]
        secs.append((y, [(x, zz + z) for (x, zz) in ring]))
    return loft(name, secs, mat=mat, smooth=smooth, cap=cap)


def arc_path(y0, z0, length, curve, n=8, forward=1.0, tilt_max=None):
    """Путь магазина: вниз на length, вперёд на curve (АК — вперёд, STANAG — назад).
    Возвращает [(y, z, tilt_deg)] — кольца сами доворачиваются по кривой."""
    if tilt_max is None:
        tilt_max = math.degrees(math.atan2(abs(curve), max(length, 1e-4))) * 1.35
    out = []
    for i in range(n):
        t = i / (n - 1.0)
        out.append((y0 + forward * curve * (t ** 1.6),
                    z0 - length * t,
                    forward * tilt_max * (t ** 1.15)))
    return out


def mag_loft(name, length, curve, w, h, tilt=28.0, n=9, mat=None, corner=None, smooth=True):
    """Магазин: путь сам идёт вниз (−Z) и вперёд (+Y), кольца доворачиваются по кривой.
    Сечение: w — толщина (X), h — глубина тела магазина. Никаких доп. поворотов."""
    path = arc_path(0.0, 0.0, length, curve, n=n, forward=1.0, tilt_max=tilt)
    c = corner if corner is not None else min(w, h) * 0.16
    return loft_path(name, path, w, h, corner=c, mat=mat, smooth=smooth)


def cyl_y(name, r, length, y0=0.0, x=0.0, z=0.0, n=20, mat=None, smooth=True, r1=None):
    """Цилиндр ВДОЛЬ ОСИ Y (ствол/трубка) от y0 до y0+length."""
    r1 = r if r1 is None else r1
    secs = [(y0, ring_circle(r, n)), (y0 + length, ring_circle(r1, n))]
    o = loft(name, secs, mat=mat, smooth=smooth)
    for v in o.data.vertices:
        v.co.x += x
        v.co.z += z
    return o


def tube_between(name, p0, p1, r, n=12, mat=None, smooth=True, r1=None):
    """Цилиндр (или конус при r1) между двумя точками — сошки, рукоять, провода, игла шприца."""
    a, b = Vector(p0), Vector(p1)
    d = b - a
    L = d.length
    o = cyl_y(name, r, max(L, 1e-5), y0=0.0, n=n, mat=mat, smooth=smooth, r1=r1)
    q = d.to_track_quat('Y', 'Z')
    o.rotation_mode = 'QUATERNION'
    o.rotation_quaternion = q
    o.location = a
    return o


def hex_nut(name, r, depth, loc=(0, 0, 0), rot=(0, 0, 0), mat=None):
    o = cyl_y(name, r, depth, n=6, mat=mat, smooth=False)
    o.rotation_euler = rot
    o.location = loc
    return o


def slats(name, count, y0, y1, z, w, h, mat=None, x=0.0, skip=0.5):
    """Рёбра/вентиляция: набор отдельных пластин с зазорами (реальная геометрия, не текстура)."""
    out = []
    step = (y1 - y0) / count
    for i in range(count):
        yy = y0 + step * (i + 0.5)
        o = loft(name + f"_{i}", [(yy - step * skip * 0.5, ring_rect(w, 1e-3, 0)),
                                  (yy + step * skip * 0.5, ring_rect(w, 1e-3, 0))], mat=mat)
        for v in o.data.vertices:
            v.co.z += z + h * 0.5
            v.co.x += x
        out.append(o)
    return out


def ring_torus(name, r_major, r_minor, n_major=20, n_minor=8, loc=(0, 0, 0), rot=(0, 0, 0), mat=None):
    """Кольцо (чека гранаты, петли)."""
    secs = []
    for i in range(n_major + 1):
        a = 2 * math.pi * i / n_major
        cx, cz = math.cos(a) * r_major, math.sin(a) * r_major
        ring = []
        for j in range(n_minor):
            b = 2 * math.pi * j / n_minor
            rr = r_major + math.cos(b) * r_minor
            ring.append((math.cos(a) * rr, math.sin(a) * rr + 0))
        # кольцо строится в плоскости XY — пересоберём как трубу
        secs.append((i, [(math.cos(a) * (r_major + math.cos(b) * r_minor),
                          math.sin(b) * r_minor) for b in
                         [2 * math.pi * j / n_minor for j in range(n_minor)]]))
    # проще: соберём вручную
    verts, faces = [], []
    for i in range(n_major):
        a = 2 * math.pi * i / n_major
        for j in range(n_minor):
            b = 2 * math.pi * j / n_minor
            rr = r_major + math.cos(b) * r_minor
            verts.append((math.cos(a) * rr, math.sin(a) * rr, math.sin(b) * r_minor))
    for i in range(n_major):
        for j in range(n_minor):
            i2, j2 = (i + 1) % n_major, (j + 1) % n_minor
            faces.append((i * n_minor + j, i2 * n_minor + j, i2 * n_minor + j2, i * n_minor + j2))
    o = mesh_obj(name, verts, faces, mat, True)
    o.rotation_euler = rot
    o.location = loc
    return o


def ring_arc(name, r_major, r_minor, a0_deg, a1_deg, mat=None, n_major=8, n_minor=8,
             loc=(0, 0, 0), rot=(0, 0, 0)):
    """Сектор тора (полоса на спасательном круге/надувном круге). Углы в градусах."""
    verts, faces = [], []
    for i in range(n_major + 1):
        a = math.radians(a0_deg + (a1_deg - a0_deg) * i / n_major)
        for j in range(n_minor):
            b = 2 * math.pi * j / n_minor
            rr = r_major + math.cos(b) * r_minor
            verts.append((math.cos(a) * rr, math.sin(a) * rr, math.sin(b) * r_minor))
    for i in range(n_major):
        for j in range(n_minor):
            j2 = (j + 1) % n_minor
            a0 = i * n_minor + j
            b0 = i * n_minor + j2
            c0 = (i + 1) * n_minor + j2
            d0 = (i + 1) * n_minor + j
            faces.append((a0, b0, c0, d0))
    o = mesh_obj(name, verts, faces, mat, True)
    o.rotation_euler = rot
    o.location = loc
    return o


def span(objs):
    """Габариты набора (мин/макс) — для компактной подгонки деталей."""
    mn = [1e9, 1e9, 1e9]
    mx = [-1e9, -1e9, -1e9]
    for o in objs:
        if o is None or o.type != 'MESH':
            continue
        for v in o.data.vertices:
            w = o.matrix_world @ v.co
            for i in range(3):
                mn[i] = min(mn[i], w[i])
                mx[i] = max(mx[i], w[i])
    return mn, mx
