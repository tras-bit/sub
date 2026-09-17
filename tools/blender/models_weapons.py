"""
SUBSISTENCE — models_weapons.py  (v4, «максимум деталей»)
Что нового против v3 (та же общая геометрия и точки крепления — код в Unity не меняется):
  • БУЛЕВЫ: настоящие окна выброса, щели вентиляции, пазы, дырки дульных тормозов,
    канал ствола, прорезь под ремень в прикладе — всё прорезано, а не нарисовано.
  • СТВОЛЬНАЯ КОРОБКА — ОБОЛОЧКА со стенками: сквозь окно выброса видно затворную группу.
  • ВСЯ МЕЛОЧКА отдельными деталями: заклёпки, штифты, оси, кнопки, флажки предохранителя,
    защёлки магазина, рычаги, антабки, вертлюги, направляющие, планки Пикатинни с зубьями.
  • Магазины с рёбрами жёсткости и пяткой, накладки с пальцевыми канавками, приклады
    с гребнем, затыльником и антабкой, рукояти с насечкой.
Координаты: ствол → +Y, верх → +Z, право → +X.
Запуск:  python3 tools/bpy_run.py tools/blender/models_weapons.py -- --only W_rifle_ak
"""
import sys, os, math
import bpy
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import subs_common as S
import subs_shapes as K

OUT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..",
      "UnityProject", "Assets", "Subsistence", "Models", "Weapons"))
R = math.radians


# ====================== БУЛЕВЫ И РЕЗКА (v4) ======================

def _drop(o):
    """Удалить объект-резец (после булева вычитания он больше не нужен)."""
    if o is None:
        return
    data = o.data
    try:
        bpy.data.objects.remove(o, do_unlink=True)
    except Exception:
        pass
    if data is not None and data.users == 0:
        try:
            bpy.data.meshes.remove(data)
        except Exception:
            pass


def cut(target, cutter):
    """Вычитание булевым модификатором: реальные дырки/пазы/окна. Резец удаляется.
    Терпит список целей (иногда деталь собирается несколькими лофтами) и «не меш»."""
    if isinstance(target, (list, tuple)):
        for t in target:
            cut(t, cutter)
        return cutter
    if target is None or cutter is None or not hasattr(target, "modifiers"):
        _drop(cutter)
        return target
    try:
        bpy.context.view_layer.update()
        md = target.modifiers.new("Cut", 'BOOLEAN')
        md.operation = 'DIFFERENCE'
        md.solver = 'EXACT'
        md.object = cutter
        bpy.context.view_layer.objects.active = target
        bpy.ops.object.modifier_apply(modifier=md.name)
    except Exception as e:
        print(f"[warn] cut {target.name} <- {cutter.name}: {e}")
    _drop(cutter)
    return target


def cut_box(name, size, loc, rot=(0, 0, 0)):
    """Прямоугольный резец."""
    return S.box(name, size=size, loc=loc, rot=rot)


def cut_cyl(name, r, depth, loc, axis="x", n=16):
    """Цилиндрический резец (дырки вентиляции, каналы, гнёзда)."""
    o = K.cyl_y(name, r, depth, n=n, mat=None)
    if axis == "x":
        o.rotation_euler = (0, 0, R(90))
    elif axis == "z":
        o.rotation_euler = (R(90), 0, 0)
    o.location = loc
    return o


def cut_slots(name, count, a, b, size, rot=(0, 0, 0)):
    """Ряд одинаковых резцов от точки a до b (вентиляция, пазы, зубья)."""
    out = []
    for i in range(count):
        t = 0.0 if count == 1 else i / (count - 1.0)
        loc = tuple(a[k] + (b[k] - a[k]) * t for k in range(3))
        out.append(cut_box(f"{name}{i}", size, loc, rot))
    return out


def _z(o, dz=0.0, dx=0.0, dy=0.0):
    for v in o.data.vertices:
        v.co.x += dx
        v.co.y += dy
        v.co.z += dz
    return o


# ====================== МЕЛОЧЁВКА (v4) ======================

def screw(name, loc, axis="x", r=0.0026, d=0.0034, mat=None):
    """Винт/штифт с шестигранной головкой на поверхности (заклёпка, ось, кнопка)."""
    o = K.cyl_y(name, r, d, n=6, mat=mat, smooth=False)
    if axis == "x":
        o.rotation_euler = (0, 0, R(90))
    elif axis == "z":
        o.rotation_euler = (R(90), 0, 0)
    o.location = loc
    return o


def screw_row(name, count, a, b, r=0.0026, d=0.0034, axis="x", mat=None):
    """Ряд заклёпок от точки a до точки b (ствольная коробка, кожух, ресивер)."""
    out = []
    for i in range(count):
        t = 0.0 if count == 1 else i / (count - 1.0)
        loc = tuple(a[k] + (b[k] - a[k]) * t for k in range(3))
        out.append(screw(f"{name}{i}", loc, axis, r, d, mat))
    return out


def pins(name, y0, y1, x, z, r, mat, count=2):
    """Оси/штифты, проходящие сквозь корпус (видны с обеих сторон)."""
    out = []
    for i in range(count):
        y = y0 + (y1 - y0) * (0.0 if count == 1 else i / (count - 1.0))
        for sx in (-1, 1):
            out.append(screw(f"{name}{i}{'r' if sx > 0 else 'l'}", (sx * x, y, z), "x", r, 0.004, mat))
    return out


def rail_picatinny(name, y0, y1, z, w=0.0215, mat=None, h=0.0062, pitch=0.0133, x=0.0):
    """Пикатинни: основание + зубья с зазорами + фаска по бортам (реальная геометрия)."""
    base = K.loft(name + "_base", [(y0, K.ring_rect(w, 0.0072, 0.0014, 1)),
                                   (y1, K.ring_rect(w, 0.0072, 0.0014, 1))], mat=mat, smooth=False)
    _z(base, z + 0.0036, x)
    out = [base]
    n = max(4, int((y1 - y0) / pitch))
    step = (y1 - y0) / n
    for i in range(n):
        yy = y0 + step * (i + 0.5)
        t = K.loft(f"{name}_t{i}", [(yy - step * 0.30, K.ring_rect(w + 0.0032, h, 0.0010, 1)),
                                    (yy + step * 0.30, K.ring_rect(w + 0.0032, h, 0.0010, 1))],
                   mat=mat, smooth=False)
        _z(t, z + 0.0090, x)
        out.append(t)
    return out


def barrel_y(name, y0, y1, r, mat, steps=None, bore=True, bore_r=None, n=24):
    """Ствол с уступами + НАСТОЯЩИЙ канал: на срезе видно отверстие.
    Канал режется в КАЖДОЙ ступени отдельно (без объединения объектов) — так работает
    и в обычной сборке, и в диагностике, где join подменён."""
    parts = []
    if steps is None:
        steps = [(y0, y1, r)]
    br = bore_r if bore_r is not None else r * 0.52
    for i, (a, b, rr) in enumerate(steps):
        seg = [K.cyl_y(f"{name}_{i}", rr, b - a, y0=a, n=n, mat=mat)]
        if bore:
            cut(seg[0], K.cyl_y(f"{name}_borecut{i}", br, (b - a) * 0.94, y0=a + (b - a) * 0.03,
                                n=18, mat=None))
        parts += seg
    return parts


def trigger_group(name, y, z, mat, w=0.008, big=False):
    """Спусковой крючок + скоба С ПРОРЕЗЬЮ (сквозь неё видно крючок)."""
    out = []
    hook = K.profile(name + "_hook", [(-0.004, 0.012), (0.005, 0.010), (0.009, -0.001),
                                      (0.003, -0.006), (-0.004, 0.001)], 0.006, y=y, z=z, mat=mat)
    out.append(hook)
    ln = 0.034 if big else 0.028
    bow = K.profile(name + "_bow", [(-ln, 0.003), (ln * 0.5, 0.003), (ln * 0.55, -0.024), (ln * 0.30, -0.028),
                                    (ln * 0.22, -0.008), (-ln * 0.80, -0.008), (-ln * 0.86, -0.026), (-ln, -0.024)],
                    w, y=y + 0.002, z=z - 0.002, mat=mat)
    out.append(bow)
    return out


def sling_loop(name, y, z, r=0.008, mat=None, x=0.0, axis="z"):
    o = K.ring_torus(name, r, 0.0018, 16, 6, rot=(0, R(90), 0) if axis == "z" else (R(90), 0, 0),
                     loc=(x, y, z), mat=mat)
    return [o]


def sight_front_post(name, y, z, mat, hood=True, height=0.028, guard=False):
    """Мушка: колодка + стойка + «уши»-намушник + подпружиненный фиксатор."""
    out = [K.loft(name + "_base", [(y - 0.011, K.ring_rect(0.021, height, 0.0016, 1)),
                                   (y + 0.011, K.ring_rect(0.021, height * 0.9, 0.0016, 1))], mat=mat, smooth=False)]
    _z(out[0], z + height * 0.5)
    out.append(K.cyl_y(name + "_pin", 0.0017, 0.017, y0=y, z=z + height, n=8, mat=mat))
    if hood:
        out.append(K.ring_torus(name + "_hood", 0.0102, 0.0021, 18, 6,
                                loc=(0, y, z + height + 0.004), rot=(R(90), 0, 0), mat=mat))
    if guard:
        out.append(K.profile(name + "_wing", [(-0.012, 0.0), (0.012, 0.0), (0.012, 0.024), (-0.012, 0.024)],
                             0.0035, y=y, x=0.0, z=z + height + 0.006, mat=mat))
    return out


def sight_rear_leaf(name, y, z, mat, angle=-8.0, marks=5):
    """Целик: колодка + наклонная листовая планка + НАСЕЧКИ делений + ползунок."""
    out = [K.loft(name + "_base", [(y - 0.013, K.ring_rect(0.023, 0.013, 0.0016, 1)),
                                   (y + 0.013, K.ring_rect(0.023, 0.013, 0.0016, 1))], mat=mat, smooth=False)]
    _z(out[0], z + 0.0065)
    leaf = K.profile(name + "_leaf", [(-0.016, -0.002), (0.020, -0.002), (0.020, 0.0022),
                                      (-0.016, 0.0045)], 0.026, y=0.0, z=0.0, mat=mat)
    leaf.rotation_euler = (R(angle), 0, 0)
    leaf.location = (0, y - 0.004, z + 0.013)
    out.append(leaf)
    for i in range(marks):
        t = i / max(1, marks - 1.0)
        m = K.profile(f"{name}_mark{i}", [(-0.0008, 0.0), (0.0008, 0.0), (0.0008, 0.0026), (-0.0008, 0.0026)],
                      0.030, y=0.0, z=0.0, mat=mat)
        m.rotation_euler = (R(angle), 0, 0)
        m.location = (0, y - 0.010 + 0.028 * t, z + 0.013 + 0.0043 + 0.004 * t)
        out.append(m)
    slider = K.profile(name + "_slider", [(-0.006, -0.003), (0.006, -0.003), (0.006, 0.006), (-0.006, 0.006)],
                       0.028, y=0.0, z=0.0, mat=mat)
    slider.rotation_euler = (R(angle), 0, 0)
    slider.location = (0, y + 0.002, z + 0.020)
    out.append(slider)
    return out


def optic_scope(name, y, z, mat, glass, length=0.30, r=0.017, rings=True, turrets=True):
    """Оптика: туба, объективы, барабанчики, КОЛЬЦА кронштейна, стекло с подсветкой."""
    out = [K.cyl_y(name + "_tube", r, length, y0=y, z=z, n=28, mat=mat),
           K.cyl_y(name + "_obj", r * 1.28, 0.032, y0=y + length * 0.5 - 0.006, z=z, n=28, mat=mat),
           K.cyl_y(name + "_objrim", r * 1.34, 0.006, y0=y + length * 0.5 + 0.026, z=z, n=28, mat=mat),
           K.cyl_y(name + "_ocu", r * 1.20, 0.028, y0=y - length * 0.5 - 0.016, z=z, n=28, mat=mat),
           K.cyl_y(name + "_eye", r * 0.92, 0.006, y0=y - length * 0.5 - 0.022, z=z, n=24, mat=mat),
           K.cyl_y(name + "_glass", r * 0.98, 0.0025, y0=y + length * 0.5 + 0.030, z=z, n=28, mat=glass)]
    if turrets:
        tower = K.cyl_y(name + "_tower", 0.0125, 0.020, n=20, mat=mat)
        tower.rotation_euler = (R(90), 0, 0)
        tower.location = (0, y - 0.012, z + 0.012)
        out.append(tower)
        cap = K.cyl_y(name + "_towercap", 0.0138, 0.005, n=20, mat=mat)
        cap.rotation_euler = (R(90), 0, 0)
        cap.location = (0, y - 0.012, z + 0.024)
        out.append(cap)
        knob = K.cyl_y(name + "_sideknob", 0.0105, 0.016, n=18, mat=mat)
        knob.rotation_euler = (0, 0, R(90))
        knob.location = (r + 0.006, y - 0.012, z)
        out.append(knob)
    if rings:
        for dy in (-length * 0.30, length * 0.30):
            ring = K.ring_torus(f"{name}_ring{int(dy*100)}", r + 0.0035, 0.0045, 20, 8,
                                loc=(0, y + dy, z), rot=(R(90), 0, 0), mat=mat)
            out.append(ring)
            leg = K.profile(f"{name}_leg{int(dy*100)}", [(-0.008, 0.0), (0.008, 0.0), (0.008, 0.016), (-0.008, 0.016)],
                            0.018, y=0.0, z=0.0, mat=mat)
            leg.location = (0, y + dy, z - r - 0.020)
            out.append(leg)
    return out


def mag_curved(name, y, z, length, curve, w, h, tilt=28.0, n=10, mat=None, ribs=0,
               spine=False, floor=True):
    """Магазин: лофт по кривой (v3) + рёбра жёсткости и пятка. Подвешивается в точке (y, z)."""
    o = K.mag_loft(name, length, curve, w, h, tilt=tilt, n=n, mat=mat)
    o.location = (0.0, y, z)
    out = [o]
    path_end = K.arc_path(0.0, 0.0, length, curve, n=n, tilt_max=tilt)[-1]
    if floor:
        cap = K.loft(name + "_floor", [(0.0, K.ring_rect(w + 0.004, h + 0.003, 0.002, 1)),
                                       (0.018, K.ring_rect(w + 0.004, h + 0.003, 0.002, 1))],
                     mat=mat, smooth=False)
        cap.rotation_euler = (R(path_end[2]), 0, 0)
        cap.location = (0.0, y + path_end[0], z + path_end[1])
        out.append(cap)
    if ribs > 0:
        path = K.arc_path(0.0, 0.0, length, curve, n=max(n, 12), tilt_max=tilt)
        for i in range(ribs):
            t = 0.16 + (0.62 / max(1, ribs - 1)) * i if ribs > 1 else 0.35
            lo = max(0, int((t - 0.055) * (len(path) - 1)))
            hi = min(len(path) - 1, int((t + 0.055) * (len(path) - 1)))
            seg = path[lo:hi + 1]
            if len(seg) < 2:
                continue
            r = K.loft_path(f"{name}_rib{i}", seg, w + 0.0025, h + 0.0015, corner=0.0015,
                            mat=mat, smooth=False)
            r.location = (0.0, y, z)
            out.append(r)
    return out


def grip_poly(name, pts, w, mat, grooves=0, y=0.0, z=0.0):
    """Рукоять с пальцевыми канавками (резы по передней кромке)."""
    g = K.profile(name, pts, w, y=y, z=z, mat=mat)
    if grooves:
        ys = [min(p[0] for p in pts), max(p[0] for p in pts)]
        for i in range(grooves):
            t = (i + 1.0) / (grooves + 1.0)
            yy = ys[0] + (ys[1] - ys[0]) * t
            c = cut_box(f"{name}_g{i}", (w + 0.004, 0.010, 0.012), (0, y + yy - 0.004, z - 0.012))
            cut(g, c)
    return g


def stock_wood(name, y_rear, y_front, z, mat, h=0.055, w=0.036, plate=True, slot=True, swivel=True):
    """Приклад: тело с гребнем + затыльник + ПРОРЕЗЬ под ремень + вертлюг."""
    out = []
    body = K.loft(name, [(y_rear, K.ring_rect(w * 0.86, h * 1.02, 0.006, 2)),
                         (y_rear + (y_front - y_rear) * 0.35, K.ring_rect(w, h * 1.12, 0.007, 2)),
                         (y_front - (y_front - y_rear) * 0.25, K.ring_rect(w * 0.94, h * 0.86, 0.006, 2)),
                         (y_front, K.ring_rect(w * 0.88, h * 0.62, 0.005, 2))], mat=mat, smooth=True)
    _z(body, z)
    out.append(body)
    comb = K.loft(name + "_comb", [(y_rear + 0.02, K.ring_rect(w * 0.80, 0.012, 0.004, 2)),
                                   (y_rear + (y_front - y_rear) * 0.55, K.ring_rect(w * 0.80, 0.010, 0.004, 2))],
                  mat=mat, smooth=True)
    _z(comb, z + h * 0.60)
    out.append(comb)
    if plate:
        pl = K.loft(name + "_plate", [(y_rear - 0.012, K.ring_rect(w * 0.90, h * 1.06, 0.008, 2)),
                                      (y_rear + 0.006, K.ring_rect(w * 0.92, h * 1.04, 0.008, 2))],
                    mat=mat, smooth=False)
        _z(pl, z)
        out.append(pl)
    if slot:
        c = cut_box(name + "_slotcut", (w + 0.01, 0.016, 0.016),
                    (0, y_rear + (y_front - y_rear) * 0.30, z - 0.002))
        cut(body, c)
    if swivel:
        out += sling_loop(name + "_sw", y_rear + (y_front - y_rear) * 0.42, z - h * 0.55, 0.009, mat)
    return out


def stock_tube_sliding(name, y0, y1, z, mat, tube_mat, w=0.040, collars=4):
    """Телескопический приклад: труба, обоймы, щёчки, затыльник, рычаг регулировки."""
    out = []
    tube = K.cyl_y(name + "_tube", 0.0155, y0 - y1, y0=y1, z=z, n=20, mat=tube_mat)
    out.append(tube)
    for i in range(collars):
        c = K.loft(f"{name}_col{i}", [(y1 + (y0 - y1) * (0.06 + 0.20 * i) - 0.006,
                                      K.ring_rect(w, 0.052, 0.004, 2)),
                                      (y1 + (y0 - y1) * (0.06 + 0.20 * i) + 0.006,
                                       K.ring_rect(w, 0.052, 0.004, 2))], mat=mat, smooth=False)
        _z(c, z - 0.005)
        out.append(c)
    body = K.profile(name + "_body", [(0.0, 0.020), (0.050, 0.024), (0.058, -0.026), (0.028, -0.032),
                                      (0.0, -0.028), (-0.012, -0.004)], 0.032, y=y0 + 0.082, z=z - 0.012, mat=mat)
    out.append(body)
    pad = K.loft(name + "_pad", [(y0 - 0.016, K.ring_rect(0.034, 0.056, 0.008, 2)),
                                 (y0 - 0.002, K.ring_rect(0.036, 0.058, 0.008, 2))], mat=mat, smooth=False)
    _z(pad, z - 0.010)
    out.append(pad)
    lever = K.profile(name + "_lever", [(0.0, 0.004), (0.020, 0.006), (0.022, -0.004), (0.0, -0.006)],
                      0.012, y=y0 + 0.050, z=z - 0.030, mat=mat)
    out.append(lever)
    return out


# ============================== ОРУЖИЕ (v4) ==============================

def build_ak(m):
    """АКМ: ресивер-оболочка (сквозь окно выброса виден затвор), дерево, наклонный тормоз."""
    metal, poly, wood, steel = m["metal"], m["poly"], m["wood"], m["steel"]
    p = []
    # ---------- ствольная коробка ----------
    pts = [(-0.155, 0.028), (0.075, 0.028), (0.075, -0.006), (0.045, -0.006),
           (0.045, -0.038), (-0.060, -0.038), (-0.060, -0.006), (-0.155, -0.006)]
    recv = K.profile("ak_recv", pts, 0.0315, 0, 0, metal)
    cy = sum(q[0] for q in pts) / len(pts)
    cz = sum(q[1] for q in pts) / len(pts)
    inner = [(cy + (q[0] - cy) * 0.90, cz + (q[1] - cz) * 0.74) for q in pts]
    cut(recv, K.profile("ak_recv_cav", inner, 0.0224, 0, 0))          # полость со стенками
    cut(recv, cut_box("ak_port", (0.032, 0.052, 0.013), (0.010, 0.030, 0.006)))
    cut(recv, cut_box("ak_hslot", (0.032, 0.040, 0.008), (0.010, -0.062, 0.012)))   # паз рукояти затвора
    p.append(recv)
    p.append(S.box("ak_bolt", size=(0.020, 0.086, 0.020), loc=(-0.001, 0.010, 0.004), bevel=0.0018, mat=steel))
    p.append(S.box("ak_chandle", size=(0.014, 0.016, 0.012), loc=(0.020, -0.062, 0.012), bevel=0.0022, mat=steel))
    p += screw_row("ak_rvf", 4, (0.0172, -0.138, 0.006), (0.0172, -0.092, 0.006), 0.0022, 0.0036, "x", metal)
    p += screw_row("ak_rvb", 4, (-0.0172, -0.138, 0.006), (-0.0172, -0.092, 0.006), 0.0022, 0.0036, "x", metal)
    p += screw_row("ak_rvr", 3, (0.0172, -0.148, -0.020), (0.0172, -0.104, -0.020), 0.0020, 0.0034, "x", metal)
    p += screw_row("ak_rvr2", 3, (-0.0172, -0.148, -0.020), (-0.0172, -0.104, -0.020), 0.0020, 0.0034, "x", metal)
    p += pins("ak_trp", -0.028, -0.086, 0.0174, -0.022, 0.0026, steel, count=2)      # оси УСМ
    p += pins("ak_magpin", 0.030, 0.030, 0.0174, -0.022, 0.0022, steel, count=1)     # ось защёлки
    # ---------- крышка ствольной коробки ----------
    cover = K.profile("ak_cover", [(-0.150, 0.022), (-0.120, 0.040), (0.055, 0.040),
                                   (0.070, 0.030), (0.055, 0.024), (-0.150, 0.024)], 0.0295, 0, 0, metal)
    p.append(cover)
    for i in range(3):
        rib = K.loft(f"ak_rib{i}", [(-0.104 + 0.048 * i, K.ring_rect(0.024, 0.0022, 0.0008, 1)),
                                    (-0.092 + 0.048 * i, K.ring_rect(0.024, 0.0022, 0.0008, 1))],
                     mat=metal, smooth=False)
        _z(rib, 0.0405)
        p.append(rib)
    latch = K.loft("ak_latch", [(-0.132, K.ring_rect(0.026, 0.010, 0.002, 1)),
                                (-0.118, K.ring_rect(0.026, 0.010, 0.002, 1))], mat=steel, smooth=False)
    _z(latch, 0.030)
    p.append(latch)
    p += screws_two("ak_cv", 2, -0.146, -0.128, 0.0105, 0.035, 0.0018, metal)
    # ---------- прицельные ----------
    p += sight_rear_leaf("ak_rear", 0.030, 0.038, metal, angle=-8.0, marks=6)
    p += sight_front_post("ak_front", 0.345, 0.010, metal, hood=True, height=0.030)
    # ---------- газовая камора и трубка ----------
    gas = K.loft("ak_gasblock", [(0.298, K.ring_rect(0.024, 0.050, 0.003, 2)),
                                 (0.332, K.ring_rect(0.024, 0.046, 0.003, 2))], mat=metal, smooth=False)
    _z(gas, 0.006)
    p.append(gas)
    p.append(K.tube_between("ak_gas_duct", (0, 0.318, 0.012), (0, 0.318, -0.008), 0.0062, 12, mat=metal))
    p.append(K.cyl_y("ak_gastube", 0.0085, 0.230, y0=0.098, z=0.041, n=20, mat=metal))
    for yy in (0.108, 0.238, 0.322):
        p.append(K.cyl_y(f"ak_gtc{int(yy*1000)}", 0.0102, 0.010, y0=yy, z=0.041, n=20, mat=metal))
    p += sling_loop("ak_sw_gas", 0.300, -0.020, 0.008, steel)
    # ---------- ствол, шомпол ----------
    p += barrel_y("ak_barrel", 0.075, 0.400, 0.0092, metal,
                  steps=[(0.075, 0.300, 0.0100), (0.300, 0.345, 0.0092), (0.345, 0.400, 0.0082)], bore_r=0.0050)
    p.append(K.cyl_y("ak_rod", 0.0032, 0.268, y0=0.102, z=-0.027, n=10, mat=steel))
    p.append(K.cyl_y("ak_rod_head", 0.0050, 0.010, y0=0.362, z=-0.027, n=12, mat=steel))
    p.append(K.profile("ak_lug", [(0.0, 0.0), (0.026, 0.0), (0.026, -0.012), (0.004, -0.012)],
                       0.012, y=0.330, z=-0.012, mat=metal))                      # штыковой упор
    # ---------- дульный тормоз ----------
    brake = K.loft("ak_brake", [(0.398, K.ring_circle(0.0158, 24)), (0.432, K.ring_circle(0.0152, 24)),
                                (0.455, K.ring_circle(0.0138, 24))], mat=metal, smooth=True)
    p.append(brake)
    cut(brake, cut_box("ak_brake_slant", (0.034, 0.060, 0.030), (0.0, 0.462, 0.010), rot=(R(-22), 0, 0)))
    cut(brake, cut_box("ak_brake_slot", (0.006, 0.048, 0.040), (0.0, 0.418, 0.006)))
    cut(brake, K.cyl_y("ak_brake_bore", 0.0052, 0.062, y0=0.396, n=16, mat=None))
    # ---------- цевьё ----------
    p += handguard_ak("ak_hg", 0.078, 0.296, 0.004, wood, metal)
    # ---------- магазин ----------
    p += mag_curved("ak_mag", 0.003, -0.034, 0.150, 0.066, 0.031, 0.080, tilt=27.0, n=12,
                    mat=metal, ribs=3, spine=True)
    # ---------- УСМ, рукоять, флажок, защёлка ----------
    p.append(K.profile("ak_grip", [(-0.070, -0.028), (-0.108, -0.036), (-0.150, -0.118), (-0.120, -0.132),
                                   (-0.086, -0.070), (-0.062, -0.036)], 0.032, y=0.0, z=0.0, mat=poly))
    p += trigger_group("ak_trg", -0.036, -0.030, metal, w=0.008)
    p.append(K.profile("ak_safety", [(0.0, 0.004), (0.086, 0.012), (0.086, -0.003), (0.0, -0.010)],
                       0.0045, y=-0.042, z=-0.004, x=0.0165, mat=steel))
    p.append(screw("ak_safety_pin", (0.0172, -0.040, -0.003), "x", 0.0030, 0.0050, steel))
    p.append(K.profile("ak_magrel", [(-0.030, 0.006), (-0.004, 0.008), (0.0, -0.006), (-0.030, -0.004)],
                       0.0050, y=0.016, z=-0.020, x=0.0168, mat=steel))
    p.append(K.profile("ak_sel_stop", [(-0.004, 0.0), (0.004, 0.0), (0.004, 0.006), (-0.004, 0.006)],
                       0.0060, y=-0.090, z=-0.004, x=0.0165, mat=steel))
    # ---------- приклад ----------
    p += stock_ak("ak_stock", -0.400, -0.150, -0.004, wood, steel)
    p += sling_loop("ak_sling1", -0.150, -0.036, 0.009, steel)
    p += sling_loop("ak_sling2", 0.150, -0.026, 0.008, steel)
    return S.join_objects(p, "W_rifle_ak")


def screws_two(name, count, y0, y1, z, x, r, mat):
    """Два ряда винтов по обоим бортам (симметрично)."""
    out = []
    for i in range(count):
        yy = y0 + (y1 - y0) * (0.0 if count == 1 else i / (count - 1.0))
        for sx in (-1, 1):
            out.append(screw(f"{name}{i}{'r' if sx > 0 else 'l'}", (sx * x, yy, z), "x", r, 0.0036, mat))
    return out


def handguard_ak(name, y0, y1, z, mat_wood, mat_metal, w=0.046):
    """АКМ-цевьё: нижняя накладка с ПАЛЬЦЕВЫМИ КАНАВКАМИ, верхняя с ВЕНТ-ЩЕЛЯМИ, хомут."""
    L = y1 - y0
    pts = [(0.0, 0.026), (0.10 * L, 0.030), (0.86 * L, 0.030), (1.00 * L, 0.019),
           (0.96 * L, -0.013), (0.86 * L, -0.021), (0.14 * L, -0.023), (0.0 * L, -0.016)]
    lower = K.profile(name + "_low", pts, w, y=y0, z=z, mat=mat_wood)
    for i in range(3):                                   # канавки под пальцы — РЕЗ
        yy = y0 + L * (0.30 + 0.22 * i)
        cut(lower, cut_box(f"{name}_groove{i}", (w + 0.006, 0.020, 0.009), (0, yy, z - 0.019)))
    out = [lower]
    upper = K.profile(name + "_up", [(0.0, -0.008), (0.06 * L, 0.006), (0.90 * L, 0.004), (1.0 * L, -0.010),
                                     (0.90 * L, -0.017), (0.08 * L, -0.019), (0.0, -0.016)],
                      0.036, y=y0, z=z + 0.044, mat=mat_wood)
    for i in range(2):                                   # вентиляционные щели — РЕЗ насквозь
        yy = y0 + L * (0.34 + 0.28 * i)
        cut(upper, cut_box(f"{name}_vent{i}", (0.046, 0.022, 0.030), (0, yy, z + 0.048)))
    out.append(upper)
    ring = K.loft(name + "_ring", [(y1 - 0.012, K.ring_rect(w + 0.004, 0.062, 0.008, 3)),
                                   (y1 + 0.006, K.ring_rect(w + 0.004, 0.058, 0.008, 3))],
                  mat=mat_metal, smooth=False)
    _z(ring, z + 0.006)
    out.append(ring)
    for i in range(3):
        yy = y0 + L * (0.20 + 0.20 * i)
        v = K.loft(f"{name}_rib{i}", [(yy, K.ring_rect(w + 0.0035, 0.010, 0.0016, 1)),
                                      (yy + 0.012, K.ring_rect(w + 0.0035, 0.010, 0.0016, 1))],
                   mat=mat_metal, smooth=False)
        _z(v, z - 0.021)
        out.append(v)
    return out


def stock_ak(name, y_rear, y_front, z, mat_wood, mat_steel, h=0.052, w=0.038):
    """Приклад АКМ (силуэт v3 + детали v4): настоящая ПРОРЕЗЬ под ремень, затыльник,
    стальная накладка на гребне, лючок и вертлюг."""
    pts = [
        (y_front, 0.016), (y_front - 0.06, 0.012), (y_front - 0.10, -0.004),
        (y_front - 0.15, 0.002), (y_rear + 0.012, 0.014), (y_rear, 0.016),
        (y_rear, -0.080), (y_rear + 0.012, -0.082),
        (y_front - 0.16, -0.046), (y_front - 0.12, -0.030), (y_front - 0.09, -0.030),
        (y_front - 0.05, -0.036), (y_front, -0.038),
    ]
    body = K.profile(name, pts, w, y=0.0, z=z, mat=mat_wood)
    out = [body]
    butt = K.profile(name + "_butt", [(y_rear - 0.010, 0.020), (y_rear + 0.001, 0.020),
                                      (y_rear + 0.001, -0.083), (y_rear - 0.010, -0.083)],
                     w + 0.002, y=0.0, z=z, mat=mat_steel)
    out.append(butt)
    comb = K.profile(name + "_comb", [(y_rear + 0.014, 0.019), (y_front - 0.052, 0.017),
                                      (y_front - 0.052, 0.010), (y_rear + 0.014, 0.011)],
                     w - 0.006, y=0.0, z=z, mat=mat_wood)
    out.append(comb)
    # ПРОРЕЗЬ под ремень — реальная дырка в дереве (вместо накладного «бобышки»)
    cut(body, cut_box(name + "_slotcut", (w + 0.012, 0.018, 0.016),
                      (0, y_rear + 0.038, z - 0.040)))
    out += sling_loop(name + "_sw", y_rear + 0.038, z - 0.056, 0.009, mat_steel)
    out.append(K.loft(name + "_trap", [(y_rear + 0.020, K.ring_rect(w * 0.55, 0.006, 0.001, 1)),
                                       (y_rear + 0.044, K.ring_rect(w * 0.55, 0.006, 0.001, 1))],
                      mat=mat_steel, smooth=False))
    _z(out[-1], z - 0.052)
    return out


def build_m4(m):
    """M4A1: плоский верх с планкой, окно выброса с откинутой крышкой, «птичья клетка»."""
    metal, poly, steel, brass = m["metal"], m["poly"], m["steel"], m["brass"]
    p = []
    # ---------- нижний ресивер ----------
    low = K.profile("m4_low", [(-0.118, 0.006), (0.014, 0.006), (0.014, -0.008), (-0.012, -0.008),
                               (-0.078, -0.034), (-0.118, -0.030)], 0.032, 0, 0, poly)
    cut(low, cut_box("m4_lowcav", (0.024, 0.100, 0.030), (0, -0.050, -0.008)))
    p.append(low)
    well = K.profile("m4_magwell", [(-0.060, 0.008), (0.010, 0.008), (0.010, -0.058), (-0.060, -0.058)],
                     0.034, 0, 0, poly)
    cut(well, cut_box("m4_wellcav", (0.024, 0.052, 0.056), (0, -0.025, -0.026)))
    cut(well, cut_box("m4_wellflare", (0.040, 0.040, 0.014), (0, -0.025, -0.056)))
    p.append(well)
    # ---------- верхний ресивер: плоский верх + полость ----------
    up = K.profile("m4_up", [(-0.118, 0.048), (0.118, 0.048), (0.118, 0.006), (-0.118, 0.006)],
                   0.032, 0, 0, metal)
    cut(up, K.profile("m4_upcav", [(-0.108, 0.042), (0.108, 0.042), (0.108, 0.011), (-0.108, 0.011)],
                      0.0230, 0, 0))
    cut(up, cut_box("m4_port", (0.030, 0.062, 0.017), (0.010, 0.030, 0.030)))     # окно выброса
    p.append(up)
    p.append(S.box("m4_bolt", size=(0.019, 0.086, 0.024), loc=(0.0, 0.008, 0.026), bevel=0.002, mat=steel))
    # крышка окна выброса — откинута (реальная деталь с осью)
    cover = K.loft("m4_portcover", [(-0.030, K.ring_rect(0.006, 0.030, 0.001, 1)),
                                    (0.032, K.ring_rect(0.006, 0.030, 0.001, 1))], mat=metal, smooth=False)
    cover.rotation_euler = (R(-38), 0, R(90))
    cover.location = (0.019, 0.030, 0.040)
    p.append(cover)
    p += pins("m4_coverpin", 0.000, 0.000, 0.0155, 0.042, 0.0022, steel, count=1)
    p.append(K.tube_between("m4_cover_spring", (0.014, 0.002, 0.036), (0.014, 0.050, 0.044), 0.0016, 8, steel))
    # лапка досылателя (шкаф с насечкой)
    p.append(K.cyl_y("m4_fwdassist", 0.0072, 0.016, y0=-0.112, x=0.012, z=0.020, n=14, mat=steel))
    for i in range(6):
        a = R(60 * i)
        fa = K.profile(f"m4_fa_rib{i}", [(-0.0012, 0.0072), (0.0012, 0.0072), (0.0012, 0.0092), (-0.0012, 0.0092)],
                       0.0032, y=0.0, z=0.0, mat=steel)
        fa.rotation_euler = (0, 0, a)
        fa.location = (-0.106, 0.012, 0.020)
        p.append(fa)
    p.append(K.profile("m4_deflector", [(-0.012, 0.0), (0.010, -0.004), (0.014, -0.014), (-0.006, -0.016)],
                       0.012, y=0.058, z=0.036, x=0.014, mat=metal))
    # рукоять затвора с защёлкой
    p.append(K.loft("m4_chouse", [(-0.128, K.ring_rect(0.030, 0.014, 0.002, 1)),
                                  (-0.104, K.ring_rect(0.030, 0.014, 0.002, 1))], mat=metal, smooth=False))
    _z(p[-1], 0.052)
    p.append(K.cyl_y("m4_chrod", 0.0058, 0.040, y0=-0.104, z=0.046, n=12, mat=metal))
    latch = K.profile("m4_chlatch", [(-0.010, 0.0), (0.002, 0.002), (0.014, -0.002), (0.002, -0.006)],
                      0.014, y=-0.096, z=0.046, x=0.010, mat=steel)
    p.append(latch)
    # ---------- планки и прицельные ----------
    p += rail_picatinny("m4_rail", -0.116, 0.116, 0.048, 0.0215, metal)
    p.append(K.loft("m4_rsight", [(-0.098, K.ring_rect(0.026, 0.020, 0.002, 1)),
                                  (-0.072, K.ring_rect(0.026, 0.020, 0.002, 1))], mat=metal, smooth=False))
    _z(p[-1], 0.076)
    ap = K.loft("m4_aper", [(-0.088, K.ring_disc(0.011, 0.011, 0, 0, 14)),
                            (-0.084, K.ring_disc(0.011, 0.011, 0, 0, 14))], mat=metal, smooth=True)
    _z(ap, 0.092)
    p.append(ap)
    cut(ap, K.cyl_y("m4_aper_hole", 0.0038, 0.014, y0=-0.092, z=0.100, n=12, mat=None))
    p += trigger_group("m4_guard", -0.030, -0.036, poly, w=0.008)
    # ---------- цевьё: половинки с настоящими вентиляционными отверстиями ----------
    for sx in (-1, 1):
        half = K.profile(f"m4_hg_{'r' if sx > 0 else 'l'}",
                         [(0.112, 0.026), (0.300, 0.025), (0.316, 0.012), (0.316, -0.012),
                          (0.300, -0.025), (0.112, -0.026)], 0.016, y=0, z=0, mat=poly)
        half.location.x = sx * 0.0080
        for i in range(5):
            yy = 0.130 + 0.034 * i
            cut(half, cut_cyl(f"m4_hg_hole{sx}{i}", 0.0052, 0.040, (sx * 0.0080, yy, 0.0085), axis="x", n=12))
            cut(half, cut_cyl(f"m4_hg_hole2{sx}{i}", 0.0052, 0.040, (sx * 0.0080, yy + 0.017, -0.0085), axis="x", n=12))
        p.append(half)
    p += rail_picatinny("m4_rail_low", 0.130, 0.290, -0.032, 0.019, poly, h=0.0055)
    p.append(K.cyl_y("m4_delta", 0.0235, 0.020, y0=0.112, z=0.014, n=24, mat=metal))
    for i in range(10):                                            # насечка кольца Дельта (радиальные рёбра)
        a = R(36 * i)
        p.append(K.tube_between(f"m4_delta_n{i}",
                                (math.cos(a) * 0.0190, 0.116, 0.014 + math.sin(a) * 0.0190),
                                (math.cos(a) * 0.0262, 0.116, 0.014 + math.sin(a) * 0.0262),
                                0.0011, 6, metal))
    # ---------- ствол, газовая система, газовая камора ----------
    p += barrel_y("m4_barrel", 0.112, 0.372, 0.0105, metal,
                  steps=[(0.112, 0.330, 0.0105), (0.330, 0.372, 0.0086)], bore_r=0.0057)
    p.append(K.cyl_y("m4_gastube", 0.0062, 0.236, y0=0.102, z=0.046, n=16, mat=metal))
    fsb = K.loft("m4_fsb", [(0.318, K.ring_rect(0.026, 0.048, 0.003, 2)),
                            (0.352, K.ring_rect(0.026, 0.044, 0.003, 2))], mat=metal, smooth=False)
    _z(fsb, 0.012)
    p.append(fsb)
    p.append(K.profile("m4_fsb_ear", [(0.0, 0.030), (0.022, 0.026), (0.022, -0.010), (0.0, -0.016)],
                       0.020, y=0.322, z=0.050, mat=metal))
    cut(p[-1], cut_box("m4_ear_slot", (0.010, 0.012, 0.030), (0, 0.336, 0.056)))
    p.append(K.cyl_y("m4_fsb_pin", 0.0016, 0.018, y0=0.336, z=0.062, n=8, mat=steel))
    p.append(K.profile("m4_lug", [(0.0, 0.0), (0.030, 0.0), (0.030, -0.014), (0.008, -0.014), (0.0, -0.008)],
                       0.014, y=0.320, z=-0.026, mat=metal))                      # штыковой упор
    p += sling_loop("m4_sw", 0.328, -0.030, 0.008, steel)
    # ---------- «птичья клетка»: настоящие прорези ----------
    fh = K.loft("m4_fh", [(0.368, K.ring_circle(0.0098, 20)), (0.400, K.ring_circle(0.0110, 20)),
                          (0.412, K.ring_circle(0.0102, 20))], mat=metal, smooth=True)
    for i in range(3):
        a = R(60 + 120 * i)
        s1 = cut_box(f"m4_fh_slot{i}", (0.0040, 0.028, 0.030), (0, 0.392, 0))
        s1.rotation_euler = (0, 0, a)
        cut(fh, s1)
    cut(fh, K.cyl_y("m4_fh_bore", 0.0058, 0.056, y0=0.364, n=16, mat=None))
    p.append(fh)
    # ---------- магазин STANAG ----------
    p += mag_curved("m4_mag", -0.026, -0.052, 0.176, -0.024, 0.026, 0.074, tilt=-8.0, n=10,
                    mat=poly, ribs=3)
    # ---------- группа управления огнём ----------
    p.append(grip_poly("m4_grip", [(0.0, 0.016), (0.034, 0.012), (0.050, -0.080), (0.030, -0.090),
                                   (0.002, -0.082), (-0.014, -0.012)], 0.032, poly, grooves=3,
                       y=-0.078, z=-0.044))
    p += trigger_group("m4_trg", -0.040, -0.028, metal)
    p.append(screw("m4_magrel", (0.0172, -0.052, -0.020), "x", 0.0038, 0.0050, steel))
    p.append(screw("m4_magrel2", (-0.0172, -0.052, -0.020), "x", 0.0038, 0.0050, steel))
    p.append(K.profile("m4_sel", [(-0.010, 0.004), (0.010, 0.006), (0.012, -0.002), (-0.008, -0.004)],
                       0.0050, y=-0.086, z=-0.014, x=0.0168, mat=steel))
    p.append(K.profile("m4_sel2", [(-0.010, 0.004), (0.010, 0.006), (0.012, -0.002), (-0.008, -0.004)],
                       0.0050, y=-0.086, z=-0.014, x=-0.0168, mat=steel))
    p += pins("m4_pins", -0.100, -0.010, 0.0162, 0.026, 0.0026, steel, count=2)     # оси ресивера
    # ---------- труба буфера и приклад ----------
    p += stock_tube_sliding("m4_stock", -0.300, -0.168, 0.010, poly, metal)
    p.append(K.hex_nut("m4_castle", 0.0188, 0.014, loc=(0, -0.160, 0.010), rot=(0, 0, 0), mat=metal))
    p.append(K.hex_nut("m4_castle2", 0.0180, 0.010, loc=(0, -0.142, 0.010), rot=(0, 0, 0), mat=metal))
    p.append(K.profile("m4_sling_tab", [(-0.006, 0.0), (0.006, 0.0), (0.006, -0.020), (-0.006, -0.020)],
                       0.010, y=-0.160, z=-0.022, mat=metal))
    p += sling_loop("m4_sling", -0.160, -0.044, 0.009, steel)
    return S.join_objects(p, "W_rifle_m4")


def build_mp5(m):
    """MP5: трубчатый ресивер с рукоятью взведения, барабанный целик, цевьё с отверстиями."""
    metal, poly, steel = m["metal"], m["poly"], m["steel"]
    p = []
    tube = K.loft("mp5_tube", [(-0.095, K.ring_round(0.030, 0.034, 0.25, 18)),
                               (0.100, K.ring_round(0.030, 0.034, 0.25, 18))], mat=metal, smooth=True)
    _z(tube, 0.004)
    cut(tube, K.cyl_y("mp5_tube_cav", 0.0230, 0.190, y0=-0.092, z=0.004, n=18, mat=None))
    cut(tube, cut_box("mp5_cock_slot", (0.014, 0.132, 0.014), (-0.027, 0.010, 0.030)))
    p.append(tube)
    # рукоять взведения: рычаг в пазу слева + кнопка (ось вдоль X)
    p.append(S.box("mp5_cock", size=(0.014, 0.034, 0.013), loc=(-0.034, 0.042, 0.030), bevel=0.002, mat=steel))
    knob = K.cyl_y("mp5_cock_knob", 0.0092, 0.022, n=14, mat=steel)
    knob.rotation_euler = (0, 0, R(90))
    knob.location = (-0.048, 0.042, 0.030)
    p.append(knob)
    p.append(K.profile("mp5_cock_base", [(-0.016, -0.007), (0.016, -0.007), (0.016, 0.007), (-0.016, 0.007)],
                       0.010, y=0.042, z=0.030, x=-0.030, mat=steel))
    # спусковая группа (полимерная) с полостью и насечкой
    low = K.profile("mp5_lower", [(-0.090, -0.014), (0.040, -0.014), (0.040, -0.030),
                                  (-0.030, -0.052), (-0.090, -0.044)], 0.030, 0, 0, poly)
    cut(low, cut_box("mp5_low_cav", (0.022, 0.066, 0.022), (0, -0.028, -0.030)))
    p.append(low)
    well = K.profile("mp5_magwell", [(-0.032, -0.010), (0.020, -0.010), (0.020, -0.052), (-0.032, -0.052)],
                     0.030, 0, 0, poly)
    cut(well, cut_box("mp5_well_cav", (0.022, 0.044, 0.044), (0, -0.006, -0.030)))
    p.append(well)
    # цевьё с НАСТОЯЩИМИ отверстиями
    hg = K.loft("mp5_hg", [(0.100, K.ring_round(0.027, 0.030, 0.30, 14)),
                           (0.205, K.ring_round(0.026, 0.029, 0.30, 14))], mat=poly, smooth=True)
    _z(hg, 0.003)
    for i in range(5):
        yy = 0.112 + 0.018 * i
        for sx in (-1, 1):
            cut(hg, cut_cyl(f"mp5_hole{sx}{i}", 0.0042, 0.060, (sx * 0.006, yy, 0.004), axis="x", n=12))
    p.append(hg)
    # ствол + 3-луг
    p += barrel_y("mp5_barrel", 0.098, 0.250, 0.0088, metal, bore_r=0.0048)
    for i in range(3):
        a = R(90 * i)
        lug = K.loft(f"mp5_lug{i}", [(-0.008, K.ring_rect(0.018, 0.0074, 0.001, 1)),
                                     (0.008, K.ring_rect(0.018, 0.0074, 0.001, 1))], mat=metal, smooth=False)
        lug.rotation_euler = (0, 0, a)
        lug.location = (math.cos(a) * 0.0094, 0.246, math.sin(a) * 0.0094)
        p.append(lug)
    # барабанный целик: 4 отверстия разного диаметра
    p.append(K.loft("mp5_rear", [(-0.086, K.ring_rect(0.028, 0.022, 0.002, 1)),
                                 (-0.056, K.ring_rect(0.028, 0.022, 0.002, 1))], mat=metal, smooth=False))
    _z(p[-1], 0.030)
    drum = K.loft("mp5_drum", [(-0.076, K.ring_disc(0.023, 0.023, 0, 0, 18)),
                               (-0.066, K.ring_disc(0.023, 0.023, 0, 0, 18))], mat=steel, smooth=True)
    _z(drum, 0.044)
    for i, (dx, r) in enumerate(((-0.008, 0.0018), (-0.003, 0.0026), (0.003, 0.0034), (0.008, 0.0042))):
        cut(drum, cut_cyl(f"mp5_drum_h{i}", r, 0.030, (0, -0.071, 0.044 + dx), axis="x", n=10))
    p.append(drum)
    p += screws_two("mp5_drum_s", 2, -0.084, -0.058, 0.033, 0.0138, 0.0014, steel)
    p.append(K.loft("mp5_fs_base", [(0.210, K.ring_rect(0.030, 0.026, 0.004, 2)),
                                    (0.242, K.ring_rect(0.030, 0.022, 0.004, 2))], mat=metal, smooth=False))
    _z(p[-1], 0.016)
    p += sight_front_post("mp5_front", 0.226, 0.026, metal, hood=True, height=0.024)
    # магазин
    p += mag_curved("mp5_mag", -0.014, -0.038, 0.180, 0.014, 0.026, 0.056, tilt=6.0, n=10,
                    mat=metal, ribs=3)
    # рукоять + спуск + переводчик с насечкой
    p.append(grip_poly("mp5_grip", [(-0.050, -0.030), (-0.088, -0.038), (-0.128, -0.118),
                                    (-0.100, -0.130), (-0.070, -0.070), (-0.044, -0.038)],
                       0.032, poly, grooves=3))
    p += trigger_group("mp5_trg", -0.020, -0.036, metal)
    drum_sel = K.cyl_y("mp5_selector", 0.0090, 0.014, n=14, mat=steel)
    drum_sel.rotation_euler = (0, 0, R(90))
    drum_sel.location = (0.0165, -0.030, -0.024)
    p.append(drum_sel)
    for i in range(3):                                    # три положения переводчика
        a = R(-40 + 40 * i)
        notch = K.profile(f"mp5_sel_n{i}", [(-0.001, 0.0088), (0.001, 0.0088), (0.001, 0.0105), (-0.001, 0.0105)],
                          0.0040, y=0.0, z=0.0, mat=steel)
        notch.rotation_euler = (0, a, 0)
        notch.location = (0.0165, -0.030, -0.024)
        p.append(notch)
    # выдвижной приклад: два прута, затыльник, антабка
    for sx in (-1, 1):
        p.append(K.cyl_y(f"mp5_rod{sx}", 0.0052, 0.165, y0=-0.255, x=sx * 0.014, z=0.006, n=12, mat=steel))
    butt = K.profile("mp5_butt", [(-0.268, 0.032), (-0.252, 0.032), (-0.252, -0.040), (-0.268, -0.040)],
                     0.048, 0, 0, poly)
    cut(butt, cut_box("mp5_butt_slot", (0.056, 0.010, 0.050), (0, -0.262, -0.004)))
    p.append(butt)
    p += sling_loop("mp5_sling", -0.250, -0.028, 0.008, steel)
    # затворная группа видна в полости
    p.append(S.box("mp5_bolt", size=(0.019, 0.070, 0.020), loc=(0, 0.010, 0.006), bevel=0.0018, mat=steel))
    p += screw_row("mp5_rv_t", 4, (0.0170, -0.060, 0.030), (0.0170, 0.080, 0.030), 0.0020, 0.0034, "x", metal)
    p += screw_row("mp5_rv_b", 4, (-0.0170, -0.060, 0.030), (-0.0170, 0.080, 0.030), 0.0020, 0.0034, "x", metal)
    return S.join_objects(p, "W_smg_mp5")


def build_shotgun(m):
    """Помпа: ствол с вентилируемой планкой, подствольный магазин, цевьё-помпа с рёбрами."""
    metal, poly, wood, steel, brass, shell = m["metal"], m["poly"], m["wood"], m["steel"], m["brass"], m["shell"]
    p = []
    recv = K.profile("sg_recv", [(-0.100, 0.030), (0.120, 0.030), (0.120, -0.010),
                                 (0.070, -0.010), (0.070, -0.040), (-0.100, -0.040)], 0.034, 0, 0, metal)
    cut(recv, K.profile("sg_recv_cav", [(-0.090, 0.024), (0.110, 0.024), (0.110, -0.004),
                                        (0.062, -0.004), (0.062, -0.034), (-0.090, -0.034)], 0.0244, 0, 0))
    cut(recv, cut_box("sg_port", (0.032, 0.062, 0.020), (0.008, 0.040, 0.020)))     # окно выброса
    p.append(recv)
    p.append(S.box("sg_bolt", size=(0.021, 0.110, 0.022), loc=(0, 0.020, 0.008), bevel=0.002, mat=steel))
    p.append(K.tube_between("sg_lifter", (0, 0.020, -0.014), (0.000, -0.050, -0.020), 0.0042, 10, steel))
    p += barrel_y("sg_barrel", 0.110, 0.520, 0.0122, metal, bore_r=0.0072)
    # вентилируемая планка с НАСТОЯЩИМИ прорезями
    rib = K.loft("sg_rib", [(0.120, K.ring_rect(0.011, 0.014, 0.0012, 1)),
                            (0.520, K.ring_rect(0.011, 0.014, 0.0012, 1))], mat=metal, smooth=False)
    _z(rib, 0.024)
    for i in range(13):
        yy = 0.142 + 0.027 * i
        cut(rib, cut_box(f"sg_ribslot{i}", (0.016, 0.013, 0.020), (0, yy, 0.024)))
    p.append(rib)
    p.append(K.cyl_y("sg_bead", 0.0030, 0.006, y0=0.502, z=0.034, n=10, mat=steel))
    # подствольный магазин + кольцо + колпачок
    p.append(K.cyl_y("sg_mag", 0.0115, 0.330, y0=0.120, z=-0.026, n=20, mat=metal))
    p.append(K.cyl_y("sg_cap", 0.0125, 0.014, y0=0.450, z=-0.026, n=20, mat=metal))
    cut(p[-1], cut_box("sg_cap_slot", (0.030, 0.008, 0.006), (0, 0.456, -0.026)))
    p += sling_loop("sg_sw1", 0.180, -0.048, 0.008, steel)
    # помпа: дерево с рёбрами (выступающими) и насечкой
    pump = K.loft("sg_pump", [(0.150, K.ring_round(0.036, 0.040, 0.35, 16)),
                              (0.290, K.ring_round(0.034, 0.038, 0.35, 16))], mat=wood, smooth=True)
    _z(pump, -0.014)
    for i in range(6):
        yy = 0.160 + 0.021 * i
        r = K.loft(f"sg_pumpr{i}", [(yy, K.ring_rect(0.072, 0.040, 0.004, 2)),
                                    (yy + 0.008, K.ring_rect(0.072, 0.040, 0.004, 2))], mat=wood, smooth=False)
        _z(r, -0.014)
        p.append(r)
    p.append(pump)
    # спуск + предохранитель-кнопка + дерево ложи
    p += trigger_group("sg_trg", -0.030, -0.030, metal, big=True)
    p.append(K.cyl_y("sg_safety", 0.0042, 0.020, y0=-0.026, z=-0.012, n=10, mat=steel))
    p.append(K.profile("sg_grip", [(0.0, 0.016), (0.032, 0.012), (0.048, -0.076), (0.028, -0.086),
                                   (0.0, -0.078), (-0.014, -0.010)], 0.032, y=-0.070, z=-0.046, mat=wood))
    stock = K.profile("sg_stock", [(-0.360, 0.016), (-0.300, 0.012), (-0.230, 0.000),
                                   (-0.150, -0.002), (-0.104, 0.010), (-0.100, -0.002),
                                   (-0.120, -0.020), (-0.230, -0.030), (-0.330, -0.052),
                                   (-0.360, -0.056)], 0.038, 0, -0.004, wood)
    cut(stock, cut_box("sg_stock_slot", (0.050, 0.018, 0.014), (0, -0.330, -0.030)))
    p.append(stock)
    p.append(K.profile("sg_butt", [(-0.372, 0.018), (-0.358, 0.018), (-0.358, -0.058), (-0.372, -0.060)],
                       0.042, 0, -0.004, poly))
    p += sling_loop("sg_sw2", -0.320, -0.044, 0.008, steel)
    p += screw_row("sg_rv", 3, (0.0165, -0.090, 0.016), (0.0165, 0.090, 0.016), 0.0022, 0.0036, "x", metal)
    p += screw_row("sg_rv2", 3, (-0.0165, -0.090, 0.016), (-0.0165, 0.090, 0.016), 0.0022, 0.0036, "x", metal)
    return S.join_objects(p, "W_shotgun_pump")


def build_lmg(m):
    """M249: кожух с настоящими щелями, ручка переноски, сошки, короб с лентой."""
    metal, poly, steel, brass = m["metal"], m["poly"], m["steel"], m["brass"]
    p = []
    body = K.profile("lmg_body", [(-0.175, 0.050), (0.125, 0.050), (0.125, -0.034),
                                  (-0.060, -0.034), (-0.175, -0.012)], 0.056, 0, 0, metal)
    cut(body, K.profile("lmg_body_cav", [(-0.165, 0.044), (0.115, 0.044), (0.115, -0.028),
                                         (-0.058, -0.028), (-0.165, -0.006)], 0.0360, 0, 0))
    p.append(body)
    top = K.profile("lmg_top", [(-0.165, 0.050), (0.055, 0.050), (0.065, 0.076), (-0.155, 0.076)],
                    0.054, 0, 0, metal)
    cut(top, cut_box("lmg_feed_win", (0.050, 0.070, 0.030), (0, 0.000, 0.062)))     # окно подачи ленты
    p.append(top)
    # лоток подачи внутри окна: видно, что пулемёт ленточный
    p.append(S.box("lmg_feedtray", size=(0.044, 0.064, 0.008), loc=(0, 0.000, 0.052), bevel=0.001, mat=metal))
    for i in range(7):                                                              # лента: звенья + патроны
        yy = -0.027 + 0.009 * i
        p.append(K.cyl_y(f"lmg_feed_link{i}", 0.0032, 0.030, y0=yy, z=0.056, n=8, mat=steel))
        p.append(K.cyl_y(f"lmg_feed_round{i}", 0.0038, 0.026, y0=yy + 0.001, z=0.060, n=10, mat=brass))
    p.append(S.box("lmg_feedcover", size=(0.048, 0.028, 0.006), loc=(0, -0.054, 0.066), bevel=0.001, mat=metal))
    p.append(S.box("lmg_bolt", size=(0.030, 0.120, 0.026), loc=(0, -0.020, 0.034), bevel=0.002, mat=steel))
    # кожух: отверстия трёх рядов
    sh = K.loft("lmg_shield", [(0.130, K.ring_round(0.033, 0.040, 0.30, 16)),
                               (0.470, K.ring_round(0.033, 0.040, 0.30, 16))], mat=metal, smooth=True)
    _z(sh, 0.010)
    for row, dz in (("t", 0.026), ("b", -0.026)):
        for i in range(7):
            cut(sh, cut_cyl(f"lmg_v{row}{i}", 0.0072, 0.070, (0.0, 0.150 + 0.042 * i, 0.010 + dz), axis="x", n=12))
    for i in range(7):
        cut(sh, cut_cyl(f"lmg_vs{i}", 0.0060, 0.070, (0.014, 0.150 + 0.042 * i, 0.010), axis="x", n=12))
        cut(sh, cut_cyl(f"lmg_vs2{i}", 0.0060, 0.070, (-0.014, 0.150 + 0.042 * i, 0.010), axis="x", n=12))
    p.append(sh)
    p += barrel_y("lmg_barrel", 0.130, 0.560, 0.0105, metal, bore_r=0.0056)
    # ручка переноски: труба + стойки + насечка
    p.append(K.tube_between("lmg_hb_l", (-0.020, 0.208, 0.046), (-0.020, 0.286, 0.080), 0.0058, 10, mat=steel))
    p.append(K.tube_between("lmg_hb_r", (0.020, 0.208, 0.046), (0.020, 0.286, 0.080), 0.0058, 10, mat=steel))
    p.append(K.tube_between("lmg_hb_top", (-0.020, 0.286, 0.080), (0.020, 0.286, 0.080), 0.0058, 12, mat=steel))
    for sx in (-1, 1):                                   # пятки ручки на кожухе
        p.append(S.box(f"lmg_hb_base{sx}", size=(0.016, 0.020, 0.010), loc=(sx * 0.020, 0.208, 0.042), bevel=0.001, mat=metal))
    for i in range(4):
        knob = K.cyl_y(f"lmg_hb_grip{i}", 0.0072, 0.007, n=10, mat=poly)
        knob.rotation_euler = (0, 0, R(90))
        knob.location = (-0.012 + 0.008 * i, 0.286, 0.080)
    # сошки с шарниром, пружиной и пятками
    p.append(K.cyl_y("lmg_hub", 0.014, 0.062, n=16, mat=metal))
    p[-1].rotation_euler = (0, 0, R(90))
    p[-1].location = (0.0, 0.244, -0.006)
    for sx in (-1, 1):
        p.append(K.tube_between(f"lmg_leg{sx}", (sx * 0.014, 0.250, -0.008), (sx * 0.112, 0.190, -0.180),
                                0.0075, 12, mat=metal))
        p.append(K.tube_between(f"lmg_spring{sx}", (sx * 0.030, 0.240, -0.040), (sx * 0.080, 0.208, -0.130),
                                0.0034, 8, mat=steel))
        foot = K.profile(f"lmg_foot{sx}", [(-0.014, 0.010), (0.014, 0.010), (0.014, -0.006), (-0.014, -0.006)],
                         0.022, y=0.190, z=-0.182, x=sx * 0.112, mat=poly)
        p.append(foot)
    # приклад-труба, буфер, затыльник, рукоять
    p.append(K.cyl_y("lmg_tube", 0.0170, 0.130, y0=-0.300, z=0.020, n=20, mat=poly))
    butt = K.profile("lmg_butt", [(-0.318, 0.046), (-0.300, 0.046), (-0.300, -0.030), (-0.318, -0.030)],
                     0.052, 0, 0, poly)
    cut(butt, cut_box("lmg_butt_cav", (0.040, 0.012, 0.058), (0, -0.310, 0.008)))
    p.append(butt)
    p.append(grip_poly("lmg_grip", [(0.0, 0.016), (0.034, 0.012), (0.050, -0.082), (0.030, -0.092),
                                    (0.002, -0.084), (-0.014, -0.012)], 0.032, poly, grooves=3,
                       y=-0.100, z=-0.026))
    p += trigger_group("lmg_trg", -0.060, -0.030, metal, big=True)
    p.append(K.profile("lmg_trig_guard", [(-0.030, 0.006), (0.030, 0.006), (0.030, -0.006), (-0.030, -0.006)],
                       0.010, y=-0.062, z=-0.008, mat=metal))
    # прицельные + планка
    p.append(K.loft("lmg_rear", [(-0.150, K.ring_rect(0.030, 0.024, 0.002, 1)),
                                 (-0.118, K.ring_rect(0.030, 0.024, 0.002, 1))], mat=metal, smooth=False))
    _z(p[-1], 0.090)
    p += sight_front_post("lmg_front", 0.455, 0.030, metal, hood=False, height=0.026)
    # короб с лентой и патронами
    box = K.profile("lmg_ammobox", [(-0.125, -0.006), (0.020, -0.006), (0.030, -0.100), (-0.115, -0.100)],
                    0.086, 0, 0, poly, smooth=False)
    cut(box, cut_box("lmg_box_lid", (0.070, 0.130, 0.010), (0, -0.050, -0.010)))
    p.append(box)
    p.append(S.box("lmg_box_strap", size=(0.010, 0.140, 0.096), loc=(0.008, -0.048, -0.053), bevel=0.001, mat=metal))
    for yy in (-0.100, 0.004):                    # защёлки крышки короба
        p.append(S.box(f"lmg_box_latch{int(yy*1000)}", size=(0.090, 0.014, 0.010), loc=(0.0, yy, -0.058), bevel=0.001, mat=steel))
    p += screw_row("lmg_rv", 4, (0.0285, -0.150, 0.010), (0.0285, 0.080, 0.010), 0.0026, 0.0042, "x", metal)
    p += screw_row("lmg_rv2", 4, (-0.0285, -0.150, 0.010), (-0.0285, 0.080, 0.010), 0.0026, 0.0042, "x", metal)
    p += sling_loop("lmg_sling", -0.170, -0.030, 0.009, steel)
    return S.join_objects(p, "W_lmg_m249")


def build_bolt(m):
    """Болтовка: долы на стволе, дульный тормоз с портами, оптика, ложе с гребнем."""
    metal, poly, steel, glass = m["metal"], m["poly"], m["steel"], m["glass"]
    p = []
    recv = K.profile("bl_recv", [(-0.130, 0.028), (0.090, 0.028), (0.090, -0.006), (-0.130, -0.006)],
                     0.038, 0, 0, metal)
    cut(recv, K.profile("bl_recv_cav", [(-0.120, 0.022), (0.080, 0.022), (0.080, -0.001), (-0.120, -0.001)],
                        0.0284, 0, 0))
    cut(recv, cut_box("bl_eject_port", (0.036, 0.056, 0.020), (0.010, 0.010, 0.020)))
    p.append(recv)
    # ствол с уступами + ДОЛЫ (продольные пазы) + дульный тормоз с портами
    p += barrel_y("bl_barrel", 0.085, 0.520, 0.0110, metal,
                  steps=[(0.085, 0.300, 0.0125), (0.300, 0.470, 0.0105), (0.470, 0.520, 0.0092)],
                  bore_r=0.0052)
    fluted = p[-1] if p else None
    if fluted is not None:
        for i in range(6):
            a = R(60 * i)
            cutter = cut_cyl(f"bl_flute{i}", 0.0034, 0.200, (0, 0.195, 0), axis="y", n=10)
            cutter.rotation_euler = (0, 0, a)
            cutter.location = (math.cos(a) * 0.0122, 0.195, math.sin(a) * 0.0122)
            cut(fluted, cutter)
    p.append(K.cyl_y("bl_thread", 0.0098, 0.030, y0=0.516, n=18, mat=steel))
    brake = K.cyl_y("bl_brake", 0.0128, 0.052, y0=0.544, n=20, mat=metal)
    for i in range(3):                                    # порты тормоза — сквозные
        a = R(90 * i)
        cutter = cut_box(f"bl_port{i}", (0.030, 0.010, 0.010), (0, 0.552 + 0.012 * i, 0))
        cutter.rotation_euler = (0, 0, a)
        cut(brake, cutter)
    cut(brake, K.cyl_y("bl_brake_bore", 0.0052, 0.060, y0=0.542, n=16, mat=None))
    p.append(brake)
    # затвор + рукоять + курок + предохранитель
    p.append(K.cyl_y("bl_bolt", 0.0120, 0.120, y0=-0.006, z=0.016, n=20, mat=steel))
    p.append(K.tube_between("bl_handle", (0.018, -0.010, 0.016), (0.074, -0.030, -0.012), 0.0075, 12, mat=steel))
    p.append(K.cyl_y("bl_knob", 0.0130, 0.024, n=14, mat=steel))
    p[-1].rotation_euler = (0, 0, R(90))
    p[-1].location = (0.076, -0.032, -0.014)
    p.append(K.cyl_y("bl_safety", 0.0075, 0.016, y0=-0.090, z=0.020, n=12, mat=steel))
    p.append(K.cyl_y("bl_shroud", 0.0140, 0.030, y0=-0.014, z=0.020, n=18, mat=metal))
    # оптика: кольца, барабанчики, стекло
    p += optic_scope("bl_scope", -0.020, 0.064, metal, glass, length=0.300, r=0.0195, rings=True)
    for yy in (-0.080, 0.060):
        base = K.loft(f"bl_mount{int(yy*1000)}", [(yy, K.ring_rect(0.034, 0.016, 0.003, 2)),
                                                  (yy + 0.016, K.ring_rect(0.034, 0.016, 0.003, 2))],
                      mat=metal, smooth=False)
        _z(base, 0.022)
        p.append(base)
    # ложе: гребень, вырез, насечка, затыльник
    stock = K.profile("bl_stock", [(-0.360, 0.030), (-0.300, 0.052), (-0.150, 0.046),
                                   (-0.120, 0.030), (-0.120, -0.030), (-0.240, -0.034),
                                   (-0.330, -0.030), (-0.360, 0.010)], 0.046, 0, -0.004, poly)
    cut(stock, cut_box("bl_stock_cut", (0.052, 0.070, 0.024), (0, -0.285, 0.030)))
    cut(stock, cut_box("bl_thumb", (0.052, 0.036, 0.030), (0, -0.250, 0.014)))
    p.append(stock)
    p.append(K.profile("bl_cheek", [(-0.330, 0.030), (-0.230, 0.046), (-0.150, 0.042), (-0.150, 0.030),
                                    (-0.250, 0.032), (-0.330, 0.020)], 0.038, 0, 0.022, poly))
    p.append(K.profile("bl_buttpad", [(-0.374, 0.032), (-0.358, 0.032), (-0.358, -0.032), (-0.374, -0.032)],
                       0.048, 0, -0.004, poly))
    fore = K.profile("bl_forend", [(0.090, 0.028), (0.330, 0.026), (0.330, -0.024), (0.090, -0.028)],
                     0.044, 0, 0, poly)
    for i in range(4):                                    # пальцевые канавки цевья
        cut(fore, cut_box(f"bl_fore_g{i}", (0.050, 0.016, 0.008), (0, 0.140 + 0.045 * i, -0.024)))
    p.append(fore)
    p += trigger_group("bl_trg", -0.062, -0.028, metal)
    # магазин с крышкой и защёлкой
    mag = K.profile("bl_mag", [(-0.030, 0.006), (0.040, 0.006), (0.040, -0.056), (-0.030, -0.056)],
                    0.034, 0, 0, poly)
    cut(mag, cut_box("bl_mag_cav", (0.022, 0.060, 0.050), (0, 0.005, -0.024)))
    p.append(mag)
    p.append(K.loft("bl_floorplate", [(-0.036, K.ring_rect(0.038, 0.010, 0.002, 1)),
                                      (-0.026, K.ring_rect(0.038, 0.010, 0.002, 1))], mat=metal, smooth=False))
    _z(p[-1], -0.052)
    # сошки
    for sx in (-1, 1):
        p.append(K.tube_between(f"bl_bipod{sx}", (sx * 0.012, 0.300, -0.020), (sx * 0.092, 0.250, -0.170),
                                0.0070, 10, mat=metal))
        p.append(K.profile(f"bl_bipod_f{sx}", [(-0.012, 0.008), (0.012, 0.008), (0.012, -0.006), (-0.012, -0.006)],
                           0.020, y=0.250, z=-0.172, x=sx * 0.092, mat=poly))
    p += sling_loop("bl_sling", -0.300, -0.034, 0.008, steel)
    p += screw_row("bl_rv", 3, (0.0180, -0.120, 0.014), (0.0180, 0.080, 0.014), 0.0022, 0.0036, "x", metal)
    p += screw_row("bl_rv2", 3, (-0.0180, -0.120, 0.014), (-0.0180, 0.080, 0.014), 0.0022, 0.0036, "x", metal)
    return S.join_objects(p, "W_rifle_bolt")


def build_rpg(m):
    """РПГ: труба с деревянными кожухами, боеголовка с оживалом, сопло Вентури, прицелы."""
    metal, poly, wood, steel = m["metal"], m["poly"], m["wood"], m["steel"]
    p = []
    tube = K.cyl_y("rpg_tube", 0.032, 0.620, y0=-0.150, n=26, mat=metal)
    cut(tube, K.cyl_y("rpg_tube_cav", 0.0240, 0.580, y0=-0.130, n=20, mat=None))
    p.append(tube)
    p.append(K.cyl_y("rpg_breech", 0.042, 0.070, y0=-0.180, n=26, mat=metal))
    nozzle = K.loft("rpg_nozzle", [(-0.255, K.ring_circle(0.043, 24)), (-0.205, K.ring_circle(0.030, 24))],
                    mat=metal, smooth=True)
    cut(nozzle, K.cyl_y("rpg_nozzle_cav", 0.0270, 0.070, y0=-0.250, n=20, mat=None))
    p.append(nozzle)
    p.append(K.cyl_y("rpg_venturi", 0.030, 0.030, y0=-0.250, n=24, mat=metal))
    for i in range(4):                                    # хомуты трубы
        yy = -0.140 + 0.190 * i
        p.append(K.loft(f"rpg_clamp{i}", [(yy, K.ring_rect(0.070, 0.068, 0.004, 2)),
                                          (yy + 0.020, K.ring_rect(0.070, 0.068, 0.004, 2))],
                        mat=metal, smooth=False))
    # деревянные кожухи с рёбрами
    for i in range(2):
        y0 = -0.075 + 0.150 * i
        sh = K.loft(f"rpg_wood{i}", [(y0, K.ring_circle(0.0360, 20)),
                                     (y0 + 0.030, K.ring_circle(0.0372, 20)),
                                     (y0 + 0.100, K.ring_circle(0.0360, 20))], mat=wood, smooth=True)
        p.append(sh)
        for k in range(2):                                  # металлические хомуты по краям кожуха
            rib = K.loft(f"rpg_wood{i}_c{k}", [(y0 + (0.006 if k == 0 else 0.086), K.ring_circle(0.0390, 20)),
                                               (y0 + (0.014 if k == 0 else 0.094), K.ring_circle(0.0390, 20))],
                         mat=metal, smooth=True)
            p.append(rib)
    # прицелы: откидной рамочный + мушка
    p.append(K.profile("rpg_sight", [(0.060, 0.038), (0.078, 0.038), (0.078, 0.004), (0.060, 0.004)],
                       0.008, 0, 0, metal))
    fr = K.loft("rpg_sight_frame", [(0.062, K.ring_rect(0.020, 0.026, 0.003, 2)),
                                    (0.076, K.ring_rect(0.020, 0.026, 0.003, 2))], mat=metal, smooth=False)
    _z(fr, 0.044)
    cut(fr, cut_box("rpg_sight_win", (0.024, 0.020, 0.016), (0, 0.069, 0.050)))
    p.append(fr)
    p.append(K.profile("rpg_rear", [(-0.060, 0.040), (-0.044, 0.040), (-0.044, 0.006), (-0.060, 0.006)],
                       0.008, 0, 0, metal))
    p.append(K.profile("rpg_rear_notch", [(-0.056, 0.042), (-0.048, 0.042), (-0.048, 0.030), (-0.056, 0.030)],
                       0.010, 0, 0, metal))
    # рукоятка с канавками + спуск + плечевой упор
    p.append(grip_poly("rpg_grip", [(0.0, 0.014), (0.030, 0.010), (0.046, -0.076), (0.026, -0.086),
                                    (0.0, -0.078), (-0.014, -0.014)], 0.030, poly, grooves=3,
                       y=-0.020, z=-0.048))
    p += trigger_group("rpg_trg", 0.010, -0.040, metal, big=True)
    p.append(K.profile("rpg_shoulder", [(-0.150, 0.020), (-0.120, 0.020), (-0.120, -0.026), (-0.150, -0.026)],
                       0.030, y=0.0, z=-0.056, mat=poly))
    # боеголовка: корпус + оживало + трассер + стабилизаторы
    head = K.loft("rpg_head", [
        (0.470, K.ring_circle(0.0330, 24)), (0.520, K.ring_circle(0.0480, 24)),
        (0.570, K.ring_circle(0.0520, 24)), (0.650, K.ring_circle(0.0450, 24)),
        (0.730, K.ring_circle(0.0250, 24)), (0.780, K.ring_circle(0.0105, 24)),
        (0.800, K.ring_circle(0.0040, 20))], mat=metal, smooth=True)
    p.append(head)
    p.append(K.cyl_y("rpg_cone_tip", 0.0042, 0.020, y0=0.796, n=16, mat=steel))
    p.append(K.cyl_y("rpg_booster", 0.0210, 0.090, y0=0.330, n=20, mat=metal))
    for i in range(4):                                    # сопла бустера
        a = R(90 * i)
        cut(p[-1], cut_cyl(f"rpg_booster_v{i}", 0.0042, 0.050, (0, 0.345, 0), axis="x", n=10))
    for i in range(4):                                    # стабилизаторы хвоста
        a = R(45 + 90 * i)
        fin = K.profile(f"rpg_fin{i}", [(0.440, 0.0), (0.500, 0.0), (0.500, 0.028), (0.440, 0.030)],
                        0.006, y=0.0, z=0.033, mat=metal)
        fin.rotation_euler = (0, 0, a)
        fin.location = (math.cos(a) * 0.0, 0.0, 0.0)
        p.append(fin)
    p += sling_loop("rpg_sling", -0.100, -0.036, 0.009, steel)
    return S.join_objects(p, "W_rocket_launcher")


def build_grenade(m):
    """Ф-1: корпус с НАСТОЯЩЕЙ насечкой (сетка резов), запал с чекой и рычагом."""
    metal, steel = m["metal"], m["steel"]
    p = []
    body = K.loft("gr_body", [
        (-0.045, K.ring_circle(0.0140, 22)), (-0.030, K.ring_circle(0.0250, 22)),
        (-0.010, K.ring_circle(0.0288, 22)), (0.012, K.ring_circle(0.0288, 22)),
        (0.032, K.ring_circle(0.0250, 22)), (0.045, K.ring_circle(0.0150, 22))], mat=metal, smooth=True)
    for i in range(5):                                    # горизонтальные канавки насечки
        z = -0.024 + 0.013 * i
        cut(body, K.loft(f"gr_cut{i}", [(z - 0.0006, K.ring_circle(0.0295, 22)),
                                        (z + 0.0006, K.ring_circle(0.0295, 22))], mat=None, cap=True))
    for i in range(16):                                   # вертикальные канавки насечки
        a = R(180.0 / 16 * i)
        cutter = K.loft(f"gr_vc{i}", [(-0.026, K.ring_rect(0.0014, 0.056, 0)),
                                      (0.030, K.ring_rect(0.0014, 0.056, 0))], mat=None)
        cutter.location = (math.cos(a) * 0.0290, 0.0, math.sin(a) * 0.0290)
        cutter.rotation_euler = (R(90), 0, 0)
        cut(body, cutter)
    p.append(body)
    p.append(K.cyl_y("gr_neck", 0.0115, 0.016, y0=0.043, n=18, mat=steel))
    p.append(K.cyl_y("gr_fuse", 0.0105, 0.052, y0=0.058, n=18, mat=steel))
    p.append(K.cyl_y("gr_cap", 0.0125, 0.012, y0=0.108, n=18, mat=metal))
    cut(p[-1], cut_box("gr_cap_slot", (0.028, 0.006, 0.008), (0, 0.114, 0)))
    p.append(K.ring_torus("gr_pin", 0.0120, 0.0019, 16, 6, loc=(0.017, 0, 0.106), rot=(0, R(90), 0), mat=steel))
    p.append(K.profile("gr_lever", [(0.0, 0.005), (0.008, 0.001), (0.064, -0.006), (0.066, 0.002),
                                    (0.010, 0.009)], 0.010, y=0.0, z=0.114, mat=steel))
    p.append(K.cyl_y("gr_spring", 0.0040, 0.030, y0=0.050, x=0.014, z=0.006, n=10, mat=steel))
    return S.join_objects(p, "EX_grenade_f1")


def build_c4(m):
    """C4: брикеты с надрезами, стяжки, детонатор с экраном/клавиатурой/проводами, капсюль."""
    metal, poly, hazard, steel = m["metal"], m["poly"], m["hazard"], m["steel"]
    p = []
    for i in range(3):
        br = K.loft(f"c4_brick{i}", [(-0.062, K.ring_rect(0.128, 0.032, 0.006, 2)),
                                     (0.062, K.ring_rect(0.128, 0.032, 0.006, 2))], mat=hazard, smooth=False)
        for k in range(3):                                # надрезы на брикете
            cut(br, cut_box(f"c4_score{i}{k}", (0.130, 0.0040, 0.010), (0, -0.030 + 0.030 * k, 0)))
        _z(br, 0.018 + 0.035 * i)
        p.append(br)
    for yy in (-0.042, 0.042):                            # стяжки
        st = K.profile(f"c4_strap{int(yy * 1000)}",
                       [(yy - 0.007, 0.0), (yy + 0.007, 0.0), (yy + 0.007, 0.150), (yy - 0.007, 0.150)],
                       0.148, y=0.0, x=0.0, z=0.0, mat=poly)
        p.append(st)
        p.append(K.profile(f"c4_buckle{int(yy * 1000)}", [(yy - 0.010, 0.150), (yy + 0.010, 0.150),
                                                          (yy + 0.010, 0.160), (yy - 0.010, 0.160)],
                           0.040, y=0.0, z=0.0, mat=metal))
    det = K.loft("c4_det", [(0.030, K.ring_rect(0.086, 0.030, 0.004, 1)),
                            (0.096, K.ring_rect(0.086, 0.030, 0.004, 1))], mat=poly, smooth=False)
    cut(det, cut_box("c4_det_cav", (0.070, 0.050, 0.020), (0, 0.063, 0.132)))
    _z(det, 0.118)
    p.append(det)
    screen = K.loft("c4_screen", [(0.040, K.ring_rect(0.044, 0.004, 0.001, 1)),
                                  (0.086, K.ring_rect(0.044, 0.004, 0.001, 1))], mat=m["glass"], smooth=False)
    _z(screen, 0.134)
    p.append(screen)
    for row in range(2):
        for col in range(3):
            k = K.loft(f"c4_key{row}{col}", [(0.034 + 0.020 * col, K.ring_rect(0.016, 0.004, 0.0006, 1)),
                                             (0.050 + 0.020 * col, K.ring_rect(0.016, 0.004, 0.0006, 1))],
                       mat=steel, smooth=False)
            _z(k, 0.134 + 0.008 * row)
            p.append(k)
    for i, x in enumerate((-0.050, 0.0, 0.050)):           # провода
        p.append(K.tube_between(f"c4_wire{i}", (x, 0.030 + 0.020 * i, 0.110), (x * 0.5, -0.020, 0.055),
                                0.0018, 8, mat=steel))
    cap = K.cyl_y("c4_cap", 0.0042, 0.030, n=10, mat=steel)                                    # капсюль
    cap.rotation_euler = (R(90), 0, 0)
    cap.location = (0.036, 0.030, 0.086)
    p.append(cap)
    ant = K.cyl_y("c4_antenna", 0.0022, 0.046, n=8, mat=steel)
    ant.rotation_euler = (R(34), 0, 0)
    ant.location = (-0.036, 0.096, 0.152)
    p.append(ant)
    return S.join_objects(p, "EX_explosive_timed")


# ============================== СБОРКА ==============================

def _mats():
    return dict(
        metal=S.pbr_material("M_GunSteel", (0.075, 0.080, 0.088, 1), 0.90, 0.38, noise_scale=110, bump=0.30),
        steel=S.pbr_material("M_GunSteel_bright", (0.26, 0.27, 0.29, 1), 0.95, 0.24),
        bright=S.pbr_material("M_GunSteel_bright", (0.26, 0.27, 0.29, 1), 0.95, 0.24),
        poly=S.pbr_material("M_GunPolymer", (0.048, 0.050, 0.054, 1), 0.06, 0.58, noise_scale=150, bump=0.35),
        wood=S.pbr_material("M_WoodFurniture", (0.30, 0.17, 0.075, 1), 0.0, 0.58, noise_scale=70, bump=0.40),
        hazard=S.pbr_material("M_HazardYellow", (0.86, 0.72, 0.05, 1), 0.0, 0.52),
        brass=S.pbr_material("M_BrassShell", (0.74, 0.52, 0.16, 1), 0.95, 0.26),
        shell=S.pbr_material("M_ShellRed", (0.42, 0.045, 0.035, 1), 0.05, 0.42),
        glass=S.pbr_material("M_OpticsGlass", (0.05, 0.12, 0.14, 1), 0.0, 0.05,
                             emission=(0.20, 0.95, 0.85, 1), emission_strength=1.1))


FN_PARTS = {}

# кудa наводить «крупный план»: (центр, радиус обзора, азимут, высота)
DETAIL = {
    "W_rifle_ak":         ((0.0, -0.02, 0.0), 0.115, -42.0, 22.0),
    "W_rifle_m4":         ((0.0, 0.00, 0.02), 0.105, -38.0, 24.0),
    "W_smg_mp5":          ((0.0, 0.05, 0.02), 0.105, -36.0, 22.0),
    "W_shotgun_pump":     ((0.0, 0.16, -0.02), 0.130, -40.0, 20.0),
    "W_lmg_m249":         ((0.0, -0.02, 0.02), 0.135, -40.0, 26.0),
    "W_rifle_bolt":       ((0.0, -0.02, 0.03), 0.115, -34.0, 24.0),
    "W_rocket_launcher":  ((0.0, 0.00, 0.00), 0.140, -42.0, 20.0),
    "EX_grenade_f1":      ((0.0, 0.02, 0.0), 0.050, -30.0, 18.0),
    "EX_explosive_timed": ((0.0, 0.02, 0.13), 0.095, -34.0, 26.0),
}


def render_detail(filepath, target, radius, azimuth=-38.0, elevation=20.0,
                  samples=40, res=(1180, 700), fov_deg=26):
    dist = radius / math.tan(math.radians(fov_deg) * 0.5) * 1.12
    return S.render_preview(filepath, target=target, distance=dist, height=radius * 0.4,
                            samples=samples, res=res, fov_deg=fov_deg,
                            floor_z=target[2] - radius * 1.9, azimuth=azimuth, elevation=elevation)


def _dbg_all(m, only=None):
    """Сборка НЕсклеенных деталей каждого ствола — сразу замеряем, потом чистим сцену."""
    old_join = S.join_objects
    captured = {}
    S.join_objects = lambda objs, name="X": (captured.__setitem__(name, objs), objs)[1]
    try:
        for name, fn in BUILDERS:
            if only and name not in only:
                continue
            S.clean_scene()
            mm = _mats()
            captured.clear()
            fn(mm)
            for nm, parts in captured.items():
                check_parts(parts, nm)
    finally:
        S.join_objects = old_join
    return None


BUILDERS = [
    ("W_rifle_ak",          lambda m: build_ak(m)),
    ("W_rifle_m4",          lambda m: build_m4(m)),
    ("W_smg_mp5",           lambda m: build_mp5(m)),
    ("W_shotgun_pump",      lambda m: build_shotgun(m)),
    ("W_lmg_m249",          lambda m: build_lmg(m)),
    ("W_rifle_bolt",        lambda m: build_bolt(m)),
    ("W_rocket_launcher",   lambda m: build_rpg(m)),
    ("EX_grenade_f1",       lambda m: build_grenade(m)),
    ("EX_explosive_timed",  lambda m: build_c4(m)),
]


def check_parts(parts, name):
    """Диагностика: печатает габарит каждой детали и ругается на явно оторванные."""
    import mathutils
    bpy.context.view_layer.update()
    print(f"--- {name}: деталей {len([p for p in parts if p])} ---")
    bad = []
    for o in parts:
        if o is None or o.type != 'MESH':
            continue
        pts = [o.matrix_world @ mathutils.Vector(c) for c in o.bound_box]
        mn = [min(p[i] for p in pts) for i in range(3)]
        mx = [max(p[i] for p in pts) for i in range(3)]
        flag = ""
        if mx[2] > 0.20 or mn[2] < -0.34 or mn[1] < -0.60 or mx[1] > 0.90 or abs(mn[0]) > 0.16 or abs(mx[0]) > 0.16:
            flag = "  <-- ПРОВЕРЬ"
            bad.append(o.name)
        print(f"  {o.name:<26} y[{mn[1]:+.3f},{mx[1]:+.3f}] z[{mn[2]:+.3f},{mx[2]:+.3f}] "
              f"x[{mn[0]:+.3f},{mx[0]:+.3f}]{flag}")
    if bad:
        print(f"  [!] выбиваются: {', '.join(bad[:12])}")
    return bad


def build_all(out_dir=OUT, render=True, only=None, check=False):
    render_dir = os.path.abspath(os.path.join(out_dir, "..", "..", "..", "..", "..", "docs", "previews"))
    total = 0
    for name, fn in BUILDERS:
        if only and name not in only:
            continue
        S.clean_scene()
        m = _mats()
        if check:
            parts = FN_PARTS.get(name)
            if parts is not None:
                check_parts(parts, name)
            continue
        obj = fn(m)
        if obj is None:
            continue
        S.smart_uv(obj)
        S.add_bevel(obj, 0.0010, 1)
        S.apply_modifiers(obj)
        S.shade_smooth(obj, 32)
        tris = S.tri_count(obj)
        total += tris
        ok = S.export_fbx([obj], os.path.join(out_dir, name + ".fbx"))
        # GLB не пишем: в проект идут только FBX (префабы собирает Editor-бутстрап)
        print(f"[weapons] {name}: {tris} трис, FBX={ok}")
        if render:
            _c, _sz, _ = S.bounds_of([obj])
            _ext = max(_sz.x, _sz.y, _sz.z)
            pad = 2.1 if _ext < 0.5 else (1.05 if _ext < 1.0 else 0.62)
            S.render_fit(os.path.join(render_dir, "_w4_" + name + ".png"), [obj], samples=40,
                         res=(1180, 640), fov_deg=26, azimuth=-33.0, elevation=15.0, pad=pad)
            d = DETAIL.get(name)
            if d is not None:
                render_detail(os.path.join(render_dir, "_w4_detail_" + name + ".png"),
                              d[0], d[1], azimuth=d[2], elevation=d[3])
    print(f"[weapons] ИТОГО трис: {total}")


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    only = None
    if "--only" in argv:
        i = argv.index("--only")
        only = set(argv[i + 1:]) or None
    if "--check" in argv:
        _dbg_all(_mats(), only)
    else:
        build_all(render="--no-render" not in argv, only=only)
