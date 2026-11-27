# builds every ABYSS environment model from scratch and exports FBX for unity.
# run: blender -b -P Tools/blender/build_assets.py -- <out_dir>
# everything is original, made here. colour lives in vertex colours (+ baked AO) so the
# quest only needs one cheap shader and zero textures for most of the world
import bpy, bmesh, math, random, sys, os, json
from mathutils import Vector, noise, Matrix

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = os.path.abspath(argv[0] if argv else "out")
os.makedirs(OUT, exist_ok=True)
REPORT = {}

def clear():
    bpy.ops.wm.read_factory_settings(use_empty=True)

def lerp(a, b, t): return a + (b - a) * t
def mix(c1, c2, t): return tuple(lerp(a, b, t) for a, b in zip(c1, c2))
def srgb(h):  # hex -> linear-ish tuple, vertex colours get stored as srgb bytes
    h = h.lstrip("#"); return tuple(int(h[i:i+2], 16) / 255.0 for i in (0, 2, 4))

def to_object(bm, name):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob

def paint(ob, fn, ao=0.55, ao_spread=1.0):
    # fn(co, normal) -> rgb. then multiply in a cheap cavity AO from vertex normals vs neighbours
    me = ob.data
    if "Col" not in me.color_attributes:
        me.color_attributes.new("Col", 'BYTE_COLOR', 'POINT')
    attr = me.color_attributes["Col"]
    me.calc_loop_triangles()
    bm = bmesh.new(); bm.from_mesh(me); bm.verts.ensure_lookup_table()
    cav = []
    for v in bm.verts:
        if not v.link_edges: cav.append(0.0); continue
        d = 0.0
        for e in v.link_edges:
            o = e.other_vert(v)
            d += (o.co - v.co).normalized().dot(v.normal)
        cav.append(d / len(v.link_edges))  # >0 = concave (crevice)
    bm.free()
    lo, hi = min(cav), max(cav)
    for i, v in enumerate(me.vertices):
        c = fn(v.co, v.normal)
        k = 0.0 if hi - lo < 1e-6 else (cav[i] - lo) / (hi - lo)
        occ = 1.0 - ao * max(0.0, (k - 0.45) / 0.55) ** ao_spread
        # also darken things facing down, underwater light comes from above
        occ *= lerp(0.62, 1.0, max(0.0, v.normal.z * 0.5 + 0.5))
        attr.data[i].color = (c[0] * occ, c[1] * occ, c[2] * occ, 1.0)

def shade(ob, smooth=True, angle=60):
    for p in ob.data.polygons: p.use_smooth = smooth
    if smooth:
        try:
            bpy.context.view_layer.objects.active = ob
            ob.select_set(True)
            bpy.ops.object.shade_auto_smooth(angle=math.radians(angle))
            ob.select_set(False)
        except Exception:
            pass

def decimate(ob, target_tris):
    tris = sum(len(p.vertices) - 2 for p in ob.data.polygons)
    if tris <= target_tris: return
    m = ob.modifiers.new("dec", 'DECIMATE'); m.ratio = target_tris / tris
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.modifier_apply(modifier=m.name)

def tri_count(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)

def export(objs, name, folder):
    d = os.path.join(OUT, folder); os.makedirs(d, exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    path = os.path.join(d, name + ".fbx")
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_scale_options='FBX_SCALE_ALL',
        axis_forward='-Z', axis_up='Y', bake_space_transform=True, mesh_smooth_type='FACE',
        use_mesh_modifiers=True, add_leaf_bones=False, colors_type='SRGB', use_tspace=False,
        object_types={'MESH'})
    t = sum(tri_count(o) for o in objs)
    dims = objs[0].dimensions
    REPORT[name] = {"file": f"{folder}/{name}.fbx", "tris": t, "size_m": [round(dims.x, 2), round(dims.y, 2), round(dims.z, 2)]}
    print(f"exported {name}: {t} tris")

def blob(radius=1.0, subdiv=4, seed=0, rough=0.35, freq=1.2, squash=(1, 1, 1), ridged=0.0, flat_bottom=True):
    random.seed(seed)
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=radius)
    off = Vector((random.random() * 50, random.random() * 50, random.random() * 50))
    for v in bm.verts:
        n = v.co.normalized()
        p = n * freq + off
        d = noise.fractal(p, 0.55, 2.1, 4) * rough
        if ridged:
            d += (1.0 - abs(noise.noise(p * 2.7))) * ridged
        # chunky facets, rocks aren't smooth blobs
        d += noise.voronoi(p * 1.6)[0][0] * rough * 0.6
        v.co = n * (radius + d * radius)
        v.co.x *= squash[0]; v.co.y *= squash[1]; v.co.z *= squash[2]
        if flat_bottom and v.co.z < -radius * 0.35 * squash[2]:
            v.co.z = lerp(v.co.z, -radius * 0.35 * squash[2], 0.85)
    bm.normal_update()
    return bm

# ---------------------------------------------------------------- palette
SAND = srgb("#c9b48a"); SAND_DK = srgb("#8f7b58"); SAND_WET = srgb("#a59167")
ROCK = srgb("#5d635f"); ROCK_DK = srgb("#353b3a"); ROCK_WARM = srgb("#6f6556"); ALGAE = srgb("#4f6a46")
CORAL_ROSE = srgb("#c98b7a"); CORAL_OCHRE = srgb("#c9a15e"); CORAL_PLUM = srgb("#8a5a6e"); CORAL_SAGE = srgb("#93a07f")
WOOD = srgb("#4a3a2c"); WOOD_DK = srgb("#2c231c"); RUST = srgb("#7a4a2e"); BRASS = srgb("#8f7a4a")
DEEP_ROCK = srgb("#2b3033"); BIO = srgb("#6fe0d0")

# ---------------------------------------------------------------- terrain
def terrain():
    clear()
    size, res = 150.0, 150
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=res, y_segments=res, size=size / 2)
    for v in bm.verts:
        x, y = v.co.x, v.co.y
        # layout: hub/descent around (0,0), reef north (+y ~ 25), wreck (+x 25, +y 55), deep (y > 60) drops away
        h = noise.fractal(Vector((x * 0.02, y * 0.02, 1.3)), 0.5, 2.0, 4) * 2.2
        h += math.sin(x * 0.9 + noise.noise(Vector((x * 0.1, y * 0.1, 0))) * 3) * 0.04  # sand ripples
        # gentle basin around the hub so it feels like a natural clearing
        r0 = math.hypot(x, y); h -= math.exp(-(r0 / 14) ** 2) * 0.8
        # reef shelf a touch higher
        h += math.exp(-((x - 0) ** 2 + (y - 26) ** 2) / 300.0) * 1.2
        # deep: slope off past y=58, to about -14m at the far end
        if y > 52: h -= ((y - 52) / 23.0) ** 1.6 * 14.0 * (0.85 + 0.15 * noise.noise(Vector((x * 0.05, 0, 3))))
        # rim walls at the edges so the world closes off naturally instead of a hard edge
        edge = max(abs(x), abs(y) if y < 0 else 0) - 50
        if edge > 0: h += (edge / 25.0) ** 2 * 18
        v.co.z = h
    ob = to_object(bm, "seafloor")
    def col(co, n):
        t = noise.fractal(Vector((co.x * 0.05, co.y * 0.05, 4)), 0.5, 2, 3) * 0.5 + 0.5
        c = mix(SAND, SAND_WET, t)
        if co.z < -3: c = mix(c, mix(ROCK_DK, SAND_DK, 0.4), min(1, (-3 - co.z) / 8))
        if n.z < 0.8: c = mix(c, ROCK, min(1, (0.8 - n.z) * 3))
        return c
    paint(ob, col, ao=0.3)
    shade(ob, True, 50)
    decimate(ob, 26000)
    export([ob], "seafloor_terrain", "Environment")

# ---------------------------------------------------------------- rocks
def rocks():
    shapes = [  # (seed, squash, rough, name)
        (11, (1.4, 1.0, 0.7), 0.30, "rock_boulder_a"),
        (23, (1.0, 1.1, 1.2), 0.38, "rock_boulder_b"),
        (37, (2.2, 1.2, 0.6), 0.25, "rock_slab_a"),
        (41, (0.8, 0.9, 1.9), 0.33, "rock_spire_a"),
        (58, (1.2, 1.3, 0.9), 0.45, "rock_cluster_a"),
    ]
    for seed, sq, rough, name in shapes:
        clear()
        ob = to_object(blob(1.0, 5, seed, rough, 1.1, sq), name)
        def col(co, n, s=seed):
            t = noise.noise(co * 0.9 + Vector((s, 0, 0))) * 0.5 + 0.5
            c = mix(ROCK, ROCK_WARM, t)
            if n.z > 0.55: c = mix(c, ALGAE, min(1, (n.z - 0.55) * 2.2) * 0.65)  # algae on top faces
            return c
        paint(ob, col, ao=0.6)
        shade(ob, True, 40)
        decimate(ob, 900)
        export([ob], name, "Environment")
    # big arch landmark between hub and reef
    clear()
    bm = bmesh.new()
    bmesh.ops.create_circle(bm, segments=24, radius=0.9)
    curve_pts = []
    for i in range(33):
        a = math.pi * i / 32
        curve_pts.append(Vector((math.cos(a) * 5.0, 0, math.sin(a) * 5.5)))
    ob = None
    bm.free()
    bm = bmesh.new()
    rings = []
    for i, p in enumerate(curve_pts):
        t = i / 32
        rad = lerp(1.6, 0.9, math.sin(t * math.pi)) * (1 + noise.noise(p * 0.5) * 0.25)
        tangent = (curve_pts[min(i + 1, 32)] - curve_pts[max(i - 1, 0)]).normalized()
        side = Vector((0, 1, 0)); up = tangent.cross(side).normalized()
        ring = []
        for k in range(14):
            a = 2 * math.pi * k / 14
            off = side * math.cos(a) * rad * 1.3 + up * math.sin(a) * rad
            q = p + off
            q += off.normalized() * noise.fractal(q * 0.6, 0.5, 2, 3) * 0.5
            ring.append(bm.verts.new(q))
        rings.append(ring)
    for i in range(len(rings) - 1):
        for k in range(14):
            bm.faces.new((rings[i][k], rings[i][(k + 1) % 14], rings[i + 1][(k + 1) % 14], rings[i + 1][k]))
    bm.normal_update()
    ob = to_object(bm, "rock_arch")
    paint(ob, lambda co, n: mix(mix(ROCK, ROCK_WARM, noise.noise(co * 0.3) * 0.5 + 0.5), ALGAE, max(0, n.z - 0.4) * 0.8), ao=0.5)
    shade(ob, True, 45)
    export([ob], "rock_arch", "Environment")

# ---------------------------------------------------------------- corals
def coral_brain():
    clear()
    ob = to_object(blob(0.6, 5, 5, 0.06, 1.5, (1.1, 1.0, 0.72), ridged=0.0), "coral_brain")
    me = ob.data
    for v in me.vertices:
        n = v.co.normalized()
        # meandering grooves
        g = abs(math.sin(noise.noise(n * 3.0) * 9.0 + n.x * 6))
        v.co += n * (g * 0.045)
    paint(ob, lambda co, n: mix(CORAL_OCHRE, CORAL_SAGE, noise.noise(co * 3) * 0.5 + 0.5), ao=0.75, ao_spread=0.8)
    shade(ob, True, 70)
    decimate(ob, 1800)
    export([ob], "coral_brain", "Coral")

def tube_branch(bm, start, direction, length, radius, depth, rng, segs=6, spread=0.55, kids=3):
    # recursive tapered tube, the staghorn / sea fan workhorse
    pts = [start]
    d = direction.normalized()
    steps = 4
    for i in range(steps):
        d = (d + Vector((rng.uniform(-0.25, 0.25), rng.uniform(-0.25, 0.25), rng.uniform(-0.05, 0.25)))).normalized()
        pts.append(pts[-1] + d * (length / steps))
    rings = []
    for i, p in enumerate(pts):
        t = i / steps
        r = radius * lerp(1.0, 0.62, t)
        tan = (pts[min(i + 1, steps)] - pts[max(i - 1, 0)]).normalized()
        a = tan.orthogonal().normalized(); b = tan.cross(a).normalized()
        ring = [bm.verts.new(p + (a * math.cos(2 * math.pi * k / segs) + b * math.sin(2 * math.pi * k / segs)) * r) for k in range(segs)]
        rings.append(ring)
    for i in range(len(rings) - 1):
        for k in range(segs):
            bm.faces.new((rings[i][k], rings[i][(k + 1) % segs], rings[i + 1][(k + 1) % segs], rings[i + 1][k]))
    tip = bm.verts.new(pts[-1] + d * radius * 0.8)
    for k in range(segs):
        bm.faces.new((rings[-1][k], rings[-1][(k + 1) % segs], tip))
    if depth > 0:
        for c in range(kids):
            nd = (d + Vector((rng.uniform(-spread, spread), rng.uniform(-spread, spread), rng.uniform(0.0, spread)))).normalized()
            at = pts[rng.randint(2, steps)]
            tube_branch(bm, at, nd, length * rng.uniform(0.55, 0.75), radius * 0.68, depth - 1, rng, segs, spread, kids)

def coral_staghorn():
    clear()
    rng = random.Random(7)
    bm = bmesh.new()
    for i in range(5):
        a = i / 5 * math.tau + rng.uniform(-0.3, 0.3)
        tube_branch(bm, Vector((math.cos(a) * 0.08, math.sin(a) * 0.08, 0)), Vector((math.cos(a) * 0.5, math.sin(a) * 0.5, 1)), 0.45, 0.045, 2, rng, segs=5)
    bm.normal_update()
    ob = to_object(bm, "coral_staghorn")
    paint(ob, lambda co, n: mix(CORAL_ROSE, srgb("#e3c7a8"), min(1, co.z / 0.9)), ao=0.5)
    shade(ob, True, 80)
    decimate(ob, 2600)
    export([ob], "coral_staghorn", "Coral")

def coral_table():
    clear()
    bm = bmesh.new()
    rng = random.Random(3)
    # stem
    tube_branch(bm, Vector((0, 0, 0)), Vector((0.05, 0, 1)), 0.35, 0.07, 0, rng, segs=8)
    # plate: polar grid, fluted edge
    rings, segs, R = 7, 40, 0.85
    center = bm.verts.new((0, 0, 0.36))
    prev = None
    grid = []
    for r in range(1, rings + 1):
        t = r / rings
        row = []
        for s in range(segs):
            a = math.tau * s / segs
            rad = R * t * (1 + 0.12 * noise.noise(Vector((math.cos(a) * 2, math.sin(a) * 2, 0))))
            z = 0.36 + 0.06 * t + 0.05 * math.sin(a * 9) * t ** 3
            row.append(bm.verts.new((math.cos(a) * rad, math.sin(a) * rad * 0.85, z)))
        grid.append(row)
    for s in range(segs):
        bm.faces.new((center, grid[0][s], grid[0][(s + 1) % segs]))
    for r in range(rings - 1):
        for s in range(segs):
            bm.faces.new((grid[r][s], grid[r + 1][s], grid[r + 1][(s + 1) % segs], grid[r][(s + 1) % segs]))
    # thickness by duplicating down a bit
    plate = [f for f in bm.faces if f.calc_center_median().z > 0.33]
    ret = bmesh.ops.extrude_face_region(bm, geom=plate)
    for v in [e for e in ret["geom"] if isinstance(e, bmesh.types.BMVert)]:
        v.co.z -= 0.035
    bm.normal_update()
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    ob = to_object(bm, "coral_table")
    paint(ob, lambda co, n: mix(CORAL_SAGE, CORAL_OCHRE, min(1, math.hypot(co.x, co.y) / 0.85)), ao=0.6)
    shade(ob, True, 60)
    export([ob], "coral_table", "Coral")

def sea_fan():
    clear()
    rng = random.Random(19)
    bm = bmesh.new()
    # flat-ish fan: branches constrained to the xz plane
    def fan(start, d, length, radius, depth):
        d = Vector((d.x, d.y * 0.1, d.z)).normalized()
        end = start + d * length
        tube_branch_flat(start, end, radius)
        if depth > 0:
            for s in (-1, 1):
                nd = Matrix.Rotation(s * rng.uniform(0.35, 0.6), 3, 'Y') @ d
                fan(end, nd, length * 0.78, radius * 0.75, depth - 1)
    def tube_branch_flat(a, b, r, segs=4):
        tan = (b - a).normalized(); x = tan.orthogonal().normalized(); y = tan.cross(x)
        r1 = [bm.verts.new(a + (x * math.cos(k * math.tau / segs) + y * math.sin(k * math.tau / segs)) * r) for k in range(segs)]
        r2 = [bm.verts.new(b + (x * math.cos(k * math.tau / segs) + y * math.sin(k * math.tau / segs)) * r * 0.8) for k in range(segs)]
        for k in range(segs):
            bm.faces.new((r1[k], r1[(k + 1) % segs], r2[(k + 1) % segs], r2[k]))
    fan(Vector((0, 0, 0)), Vector((0, 0, 1)), 0.35, 0.03, 6)
    bm.normal_update()
    ob = to_object(bm, "coral_seafan")
    paint(ob, lambda co, n: mix(CORAL_PLUM, CORAL_ROSE, min(1, co.z / 1.6)), ao=0.2)
    shade(ob, False)
    decimate(ob, 2400)
    export([ob], "coral_seafan", "Coral")

def sponge_barrel():
    clear()
    bm = bmesh.new()
    segs, rings = 22, 10
    grid = []
    for r in range(rings + 1):
        t = r / rings
        row = []
        for s in range(segs):
            a = math.tau * s / segs
            rad = lerp(0.32, 0.45, math.sin(t * math.pi * 0.85)) * (1 + 0.08 * math.sin(a * 11)) * (1 + 0.1 * noise.noise(Vector((a, t * 3, 0))))
            row.append(bm.verts.new((math.cos(a) * rad, math.sin(a) * rad, t * 0.9)))
        grid.append(row)
    inner = []
    for r in range(4):
        t = r / 3
        row = [bm.verts.new((math.cos(math.tau * s / segs) * 0.28, math.sin(math.tau * s / segs) * 0.28, lerp(0.9, 0.35, t))) for s in range(segs)]
        inner.append(row)
    allr = grid + inner
    for r in range(len(allr) - 1):
        for s in range(segs):
            bm.faces.new((allr[r][s], allr[r][(s + 1) % segs], allr[r + 1][(s + 1) % segs], allr[r + 1][s]))
    bm.normal_update(); bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    ob = to_object(bm, "sponge_barrel")
    paint(ob, lambda co, n: mix(srgb("#8a5a46"), srgb("#b07d5c"), co.z / 0.9), ao=0.7)
    shade(ob, True, 60)
    export([ob], "sponge_barrel", "Coral")

def kelp():
    clear()
    bm = bmesh.new()
    rng = random.Random(4)
    H = 5.5
    prev_l = prev_r = None
    steps = 22
    for i in range(steps + 1):
        t = i / steps
        z = t * H
        x = math.sin(t * 3.0) * 0.25 * t
        w = 0.06 + 0.12 * math.sin(t * math.pi) + 0.03 * math.sin(t * 40)
        l = bm.verts.new((x - w, math.sin(t * 17) * 0.05, z)); r = bm.verts.new((x + w, -math.sin(t * 17) * 0.05, z))
        if prev_l:
            bm.faces.new((prev_l, prev_r, r, l))
        prev_l, prev_r = l, r
    bm.normal_update()
    ob = to_object(bm, "kelp_strand")
    paint(ob, lambda co, n: mix(srgb("#4b5a2c"), srgb("#8c8a44"), co.z / H), ao=0.1)
    shade(ob, True, 80)
    export([ob], "kelp_strand", "Environment")

def seagrass():
    clear()
    bm = bmesh.new()
    rng = random.Random(9)
    for b in range(26):
        a = rng.uniform(0, math.tau); r = rng.uniform(0, 0.35)
        base = Vector((math.cos(a) * r, math.sin(a) * r, 0))
        h = rng.uniform(0.35, 0.8); lean = Vector((rng.uniform(-0.2, 0.2), rng.uniform(-0.2, 0.2), 1)).normalized()
        side = lean.orthogonal().normalized() * 0.012
        pts = [base + lean * h * (k / 4) + Vector((0, 0, 0)) * k for k in range(5)]
        vs = []
        for k, p in enumerate(pts):
            p = p + Vector((lean.x, lean.y, 0)) * (k / 4) ** 2 * 0.15
            w = side * (1 - k / 5)
            vs.append((bm.verts.new(p - w), bm.verts.new(p + w)))
        for k in range(4):
            bm.faces.new((vs[k][0], vs[k][1], vs[k + 1][1], vs[k + 1][0]))
    bm.normal_update()
    ob = to_object(bm, "seagrass_clump")
    paint(ob, lambda co, n: mix(srgb("#3f5a33"), srgb("#93a066"), co.z / 0.8), ao=0.0)
    shade(ob, False)
    export([ob], "seagrass_clump", "Environment")

def anemone_glow():
    clear()
    bm = bmesh.new()
    rng = random.Random(21)
    # column + tentacle ring, colour encodes glow (shader reads vertex alpha-ish via brightness)
    tube_branch(bm, Vector((0, 0, 0)), Vector((0, 0, 1)), 0.18, 0.09, 0, rng, segs=10)
    for k in range(18):
        a = math.tau * k / 18
        tube_branch(bm, Vector((math.cos(a) * 0.07, math.sin(a) * 0.07, 0.19)), Vector((math.cos(a), math.sin(a), 0.9)), 0.22, 0.015, 0, rng, segs=4)
    bm.normal_update()
    ob = to_object(bm, "anemone_glow")
    paint(ob, lambda co, n: mix(srgb("#2c3c44"), BIO, min(1, max(0, (co.z - 0.15) / 0.2))), ao=0.2)
    shade(ob, True, 70)
    export([ob], "anemone_glow", "Coral")

def basalt_columns():
    clear()
    bm = bmesh.new()
    rng = random.Random(31)
    for i in range(11):
        cx, cy = rng.uniform(-1.6, 1.6), rng.uniform(-1.6, 1.6)
        h = rng.uniform(2.0, 6.5) * (1.3 - math.hypot(cx, cy) / 3)
        r = rng.uniform(0.35, 0.55)
        ret = bmesh.ops.create_cone(bm, cap_ends=True, segments=6, radius1=r, radius2=r * 0.92, depth=h)
        for v in ret["verts"]:
            v.co += Vector((cx, cy, h / 2))
            v.co += Vector((noise.noise(v.co * 2), noise.noise(v.co * 2 + Vector((5, 0, 0))), 0)) * 0.04
    bm.normal_update()
    ob = to_object(bm, "basalt_columns")
    paint(ob, lambda co, n: mix(DEEP_ROCK, srgb("#3f4a4e"), noise.noise(co * 0.8) * 0.5 + 0.5), ao=0.4)
    shade(ob, False)
    export([ob], "basalt_columns", "Environment")

def tube_worms():
    clear()
    bm = bmesh.new()
    rng = random.Random(44)
    for i in range(14):
        a = rng.uniform(0, math.tau); r = rng.uniform(0, 0.4)
        tube_branch(bm, Vector((math.cos(a) * r, math.sin(a) * r, 0)), Vector((rng.uniform(-0.15, 0.15), rng.uniform(-0.15, 0.15), 1)), rng.uniform(0.4, 1.1), 0.03, 0, rng, segs=6)
    bm.normal_update()
    ob = to_object(bm, "tube_worms")
    paint(ob, lambda co, n: mix(srgb("#d9d2c2"), srgb("#b3413a") if co.z > 0.35 else srgb("#cfc7b4"), min(1, co.z / 1.0)), ao=0.3)
    shade(ob, True, 70)
    export([ob], "tube_worms", "Coral")

# ---------------------------------------------------------------- interactive objects
def giant_clam():
    clear()
    def shell(sign):
        bm = bmesh.new()
        segs, rings = 26, 10
        grid = []
        for r in range(rings + 1):
            t = r / rings
            row = []
            for s in range(segs + 1):
                u = s / segs
                a = lerp(-1.25, 1.25, u)
                rad = 0.55 * t
                flute = 0.05 * math.sin(u * math.pi * 6) * t
                x = math.sin(a) * rad
                y = math.cos(a) * rad * 0.6 * sign
                z = (0.12 + flute) * math.sin(t * math.pi * 0.5) + 0.02
                row.append(bm.verts.new((x, y * 0.9 + 0.02 * sign, z * (0.8 + 0.4 * (1 - abs(u - 0.5) * 2)))))
            grid.append(row)
        for r in range(rings):
            for s in range(segs):
                f = (grid[r][s], grid[r][s + 1], grid[r + 1][s + 1], grid[r + 1][s])
                bm.faces.new(f if sign > 0 else tuple(reversed(f)))
        bm.normal_update()
        return bm
    a = to_object(shell(1), "clam_a"); b = to_object(shell(-1), "clam_b")
    for o in (a, b):
        sol = o.modifiers.new("t", 'SOLIDIFY'); sol.thickness = 0.025
        bpy.context.view_layer.objects.active = o; bpy.ops.object.modifier_apply(modifier=sol.name)
    b.rotation_euler.x = math.radians(-28); a.rotation_euler.x = math.radians(28)
    # mantle between the shells
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=20, v_segments=8, radius=0.38)
    for v in bm.verts: v.co.y *= 0.22; v.co.z = v.co.z * 0.18 + 0.12
    mantle = to_object(bm, "clam_mantle")
    for o in (a, b, mantle):
        bpy.context.view_layer.objects.active = o; o.select_set(True)
    bpy.context.view_layer.objects.active = a
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active; ob.name = "giant_clam"
    def col(co, n):
        if abs(co.y) < 0.09 and co.z > 0.05: return mix(srgb("#3f6f7a"), srgb("#7aa6a0"), noise.noise(co * 25) * 0.5 + 0.5)  # mantle
        return mix(srgb("#c8bfae"), srgb("#8f8676"), noise.noise(co * 8) * 0.5 + 0.5)
    paint(ob, col, ao=0.6)
    shade(ob, True, 50)
    export([ob], "giant_clam", "InteractiveObjects")

def ships_bell():
    clear()
    bm = bmesh.new()
    prof = [(0.0, 0.0), (0.16, 0.0), (0.17, 0.03), (0.14, 0.08), (0.11, 0.18), (0.10, 0.26), (0.08, 0.3), (0.03, 0.33), (0.0, 0.335)]
    segs = 28
    rings = []
    for (r, z) in prof:
        rings.append([bm.verts.new((math.cos(math.tau * s / segs) * r, math.sin(math.tau * s / segs) * r, z)) for s in range(segs)])
    for i in range(len(rings) - 1):
        for s in range(segs):
            bm.faces.new((rings[i][s], rings[i][(s + 1) % segs], rings[i + 1][(s + 1) % segs], rings[i + 1][s]))
    # crown loop
    ret = bmesh.ops.create_circle(bm, segments=1, radius=0.01)
    bm.normal_update(); bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    ob = to_object(bm, "ships_bell")
    sol = ob.modifiers.new("t", 'SOLIDIFY'); sol.thickness = 0.012
    bpy.context.view_layer.objects.active = ob; bpy.ops.object.modifier_apply(modifier=sol.name)
    bpy.ops.mesh.primitive_torus_add(major_radius=0.045, minor_radius=0.012, location=(0, 0, 0.36), rotation=(math.radians(90), 0, 0))
    loop = bpy.context.active_object
    ob.select_set(True); loop.select_set(True); bpy.context.view_layer.objects.active = ob
    bpy.ops.object.join()
    def col(co, n):
        verd = noise.fractal(co * 12, 0.5, 2, 3) * 0.5 + 0.5
        c = mix(BRASS, srgb("#4f7a6a"), max(0, verd - 0.35) * 1.6)  # verdigris patches
        return mix(c, RUST, max(0, noise.noise(co * 30)) * 0.3)
    paint(ob, col, ao=0.5)
    shade(ob, True, 60)
    export([ob], "ships_bell", "InteractiveObjects")

def info_marker():
    clear()
    # small stone plinth with an inset ring, marks a discovery point. low and unobtrusive
    ob = to_object(blob(0.28, 3, 99, 0.12, 1.4, (1.0, 1.0, 1.4)), "info_marker")
    for v in ob.data.vertices:
        if v.co.z > 0.18: v.co.z = 0.18 + (v.co.z - 0.18) * 0.15
    bpy.ops.mesh.primitive_torus_add(major_radius=0.17, minor_radius=0.018, location=(0, 0, 0.2), major_segments=32, minor_segments=6)
    ring = bpy.context.active_object
    ob.select_set(True); ring.select_set(True); bpy.context.view_layer.objects.active = ob
    bpy.ops.object.join()
    paint(ob, lambda co, n: BRASS if co.z > 0.185 else mix(ROCK, ROCK_WARM, noise.noise(co * 5) * 0.5 + 0.5), ao=0.5)
    shade(ob, True, 45)
    export([ob], "info_marker", "InteractiveObjects")

def destination_ring():
    clear()
    # flat ring on the sand + soft vertical beam, the beam fades out via vertex colour (shader uses it as alpha)
    bpy.ops.mesh.primitive_torus_add(major_radius=1.0, minor_radius=0.04, major_segments=48, minor_segments=6)
    ring = bpy.context.active_object; ring.scale.z = 0.4
    bpy.ops.object.transform_apply(scale=True)
    paint(ring, lambda co, n: srgb("#e6d6a8"), ao=0.0)
    ring.name = "destination_ring"
    export([ring], "destination_ring", "InteractiveObjects")
    clear()
    bm = bmesh.new()
    segs = 24
    b0 = [bm.verts.new((math.cos(math.tau * s / segs) * 0.9, math.sin(math.tau * s / segs) * 0.9, 0)) for s in range(segs)]
    b1 = [bm.verts.new((math.cos(math.tau * s / segs) * 0.7, math.sin(math.tau * s / segs) * 0.7, 4.0)) for s in range(segs)]
    for s in range(segs):
        bm.faces.new((b0[s], b0[(s + 1) % segs], b1[(s + 1) % segs], b1[s]))
    ob = to_object(bm, "destination_beam")
    me = ob.data; me.color_attributes.new("Col", 'BYTE_COLOR', 'POINT')
    for i, v in enumerate(me.vertices):
        a = 1.0 if v.co.z < 0.1 else 0.0
        me.color_attributes["Col"].data[i].color = (1, 0.95, 0.8, a)
    export([ob], "destination_beam", "InteractiveObjects")

# ---------------------------------------------------------------- wreck
def wreck():
    clear()
    rng = random.Random(77)
    parts = []
    # hull: ribs along a curved keel, half buried and broken off at one end
    L = 14.0
    ribs = 15
    bm = bmesh.new()
    for i in range(ribs):
        t = i / (ribs - 1)
        y = lerp(-L / 2, L / 2, t)
        beam = 2.6 * math.sin(lerp(0.25, 0.95, t) * math.pi) + 0.4
        broken = 1.0 if t < 0.75 else rng.uniform(0.3, 0.7)
        pts = []
        for k in range(13):
            a = lerp(0.05, math.pi - 0.05, k / 12)
            if (k / 12) > broken and rng.random() < 0.8: continue
            pts.append(Vector((math.cos(a) * beam, y, -math.sin(a) * 2.2 + 2.0)))
        for k in range(len(pts) - 1):
            a, b = pts[k], pts[k + 1]
            d = (b - a); n = Vector((0, 1, 0)); s = d.cross(n).normalized() * 0.09
            v = [bm.verts.new(a - s - n * 0.08), bm.verts.new(b - s - n * 0.08), bm.verts.new(b + s - n * 0.08), bm.verts.new(a + s - n * 0.08),
                 bm.verts.new(a - s + n * 0.08), bm.verts.new(b - s + n * 0.08), bm.verts.new(b + s + n * 0.08), bm.verts.new(a + s + n * 0.08)]
            for f in ((0, 1, 2, 3), (7, 6, 5, 4), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)):
                bm.faces.new([v[x] for x in f])
    # planks on the lower hull, gaps where they've rotted through
    for row in range(7):
        a = lerp(0.35, math.pi - 0.35, row / 6)
        for seg in range(10):
            if rng.random() < 0.28: continue
            t0, t1 = seg / 10, (seg + 1) / 10 - 0.008
            if t0 > 0.72 and rng.random() < 0.6: continue
            quad = []
            for t in (t0, t1):
                y = lerp(-L / 2, L / 2, t)
                beam = 2.6 * math.sin(lerp(0.25, 0.95, t) * math.pi) + 0.48
                for da in (-0.12, 0.12):
                    aa = a + da
                    quad.append(Vector((math.cos(aa) * beam, y, -math.sin(aa) * 2.25 + 2.0)))
            q = [bm.verts.new(p) for p in (quad[0], quad[2], quad[3], quad[1])]
            bm.faces.new(q)
    bm.normal_update()
    hull = to_object(bm, "wreck_hull")
    sol = hull.modifiers.new("t", 'SOLIDIFY'); sol.thickness = 0.05
    bpy.context.view_layer.objects.active = hull; bpy.ops.object.modifier_apply(modifier=sol.name)
    def wc(co, n):
        c = mix(WOOD, WOOD_DK, noise.noise(co * 0.8) * 0.5 + 0.5)
        if co.z < 0.6: c = mix(c, SAND_DK, 0.35)
        if n.z > 0.5: c = mix(c, ALGAE, 0.4)
        return c
    paint(hull, wc, ao=0.6)
    shade(hull, False)
    export([hull], "wreck_hull", "Structures")

    # fallen mast with a cross spar
    clear()
    bm = bmesh.new()
    tube_branch(bm, Vector((0, 0, 0)), Vector((1, 0.05, 0.12)), 9.0, 0.16, 0, rng, segs=8)
    tube_branch(bm, Vector((6.0, 0, 0.6)), Vector((0, 1, 0.15)), 3.5, 0.07, 0, rng, segs=6)
    tube_branch(bm, Vector((6.0, 0, 0.6)), Vector((0, -1, 0.05)), 3.0, 0.07, 0, rng, segs=6)
    bm.normal_update()
    mast = to_object(bm, "wreck_mast")
    paint(mast, wc, ao=0.4)
    shade(mast, True, 50)
    export([mast], "wreck_mast", "Structures")

    # anchor
    clear()
    bm = bmesh.new()
    tube_branch(bm, Vector((0, 0, 0)), Vector((0, 0, 1)), 1.6, 0.06, 0, rng, segs=8)
    for s in (-1, 1):
        prev = Vector((0, 0, 0.05))
        for k in range(1, 7):
            a = k / 6 * math.pi * 0.42
            p = Vector((math.sin(a) * 0.7 * s, 0, 0.6 - math.cos(a) * 0.6))
            tube_branch(bm, prev, p - prev, (p - prev).length, 0.055, 0, rng, segs=6)
            prev = p
    tube_branch(bm, Vector((-0.55, 0, 1.45)), Vector((1, 0, 0)), 1.1, 0.05, 0, rng, segs=6)
    bm.normal_update()
    anchor = to_object(bm, "wreck_anchor")
    paint(anchor, lambda co, n: mix(RUST, srgb("#3c2d24"), noise.fractal(co * 4, 0.5, 2, 3) * 0.5 + 0.5), ao=0.5)
    shade(anchor, True, 50)
    export([anchor], "wreck_anchor", "Structures")

    # cargo crate, slightly crushed
    clear()
    bpy.ops.mesh.primitive_cube_add(size=1)
    crate = bpy.context.active_object
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.subdivide(number_cuts=3); bpy.ops.object.mode_set(mode='OBJECT')
    for v in crate.data.vertices:
        v.co.x *= 0.9; v.co.z *= 0.75
        v.co += Vector((noise.noise(v.co * 3), noise.noise(v.co * 3 + Vector((3, 0, 0))), noise.noise(v.co * 3 + Vector((0, 7, 0))))) * 0.035
        if v.co.z > 0.3 and v.co.x > 0.2: v.co.z -= 0.08
    paint(crate, lambda co, n: mix(WOOD, srgb("#6a5640"), (math.sin(co.z * 40) * 0.5 + 0.5) * 0.5), ao=0.5)
    shade(crate, False)
    crate.name = "wreck_crate"
    export([crate], "wreck_crate", "Structures")

# ---------------------------------------------------------------- marine life
def fish_small():
    clear()
    bm = bmesh.new()
    # body: lofted ellipses along +y (exports to unity +z forward with our axis settings)
    prof = [(-0.11, 0.0, 0.0), (-0.08, 0.018, 0.03), (-0.02, 0.026, 0.045), (0.04, 0.022, 0.04), (0.08, 0.012, 0.02), (0.1, 0.004, 0.006)]
    segs = 8; rings = []
    for (y, w, h) in prof:
        rings.append([bm.verts.new((math.cos(math.tau * s / segs) * w, -y, math.sin(math.tau * s / segs) * h)) for s in range(segs)])
    for i in range(len(rings) - 1):
        for s in range(segs):
            bm.faces.new((rings[i][s], rings[i][(s + 1) % segs], rings[i + 1][(s + 1) % segs], rings[i + 1][s]))
    nose = bm.verts.new((0, 0.115, 0)); tail_base = rings[-1]
    for s in range(segs): bm.faces.new((rings[0][(s + 1) % segs], rings[0][s], nose))
    t0 = bm.verts.new((0, -0.1, 0)); t1 = bm.verts.new((0, -0.15, 0.04)); t2 = bm.verts.new((0, -0.15, -0.04))
    bm.faces.new((t0, t1, t2))
    bm.normal_update()
    ob = to_object(bm, "fish_small")
    paint(ob, lambda co, n: mix(srgb("#c8d2d0"), srgb("#3e5d6a"), max(0, n.z)), ao=0.0)
    shade(ob, True, 60)
    export([ob], "fish_small", "MarineLife")

def ray_large():
    clear()
    bm = bmesh.new()
    # manta-like silhouette for the distance, wings as a lofted diamond
    pts = []
    rows = 9
    grid = []
    for i in range(rows):
        t = i / (rows - 1)
        y = lerp(0.9, -0.9, t)
        span = math.sin(t * math.pi) ** 0.7 * 2.0
        row = []
        for k in range(9):
            u = lerp(-1, 1, k / 8)
            x = u * span
            z = (1 - abs(u)) * 0.18 * math.sin(t * math.pi) - abs(u) ** 2 * 0.15
            row.append(bm.verts.new((x, y, z)))
        grid.append(row)
    for i in range(rows - 1):
        for k in range(8):
            bm.faces.new((grid[i][k], grid[i][k + 1], grid[i + 1][k + 1], grid[i + 1][k]))
    tail_a = grid[-1][4]
    tail_b = bm.verts.new((0, -2.4, 0.02)); tail_c = bm.verts.new((0.02, -2.4, 0))
    bm.faces.new((tail_a, tail_b, tail_c))
    bm.normal_update()
    ob = to_object(bm, "ray_large")
    sol = ob.modifiers.new("t", 'SOLIDIFY'); sol.thickness = 0.06
    bpy.context.view_layer.objects.active = ob; bpy.ops.object.modifier_apply(modifier=sol.name)
    paint(ob, lambda co, n: srgb("#27343b") if n.z > 0 else srgb("#c9d1d0"), ao=0.0)
    shade(ob, True, 60)
    export([ob], "ray_large", "MarineLife")

if __name__ == "__main__":
    steps = [terrain, rocks, coral_brain, coral_staghorn, coral_table, sea_fan, sponge_barrel, kelp, seagrass,
             anemone_glow, basalt_columns, tube_worms, giant_clam, ships_bell, info_marker, destination_ring,
             wreck, fish_small, ray_large]
    for s in steps:
        try:
            s()
        except Exception as e:
            import traceback; traceback.print_exc()
            REPORT["_error_" + s.__name__] = str(e)
    json.dump(REPORT, open(os.path.join(OUT, "asset_report.json"), "w"), indent=2)
    print("REPORT", json.dumps(REPORT))
