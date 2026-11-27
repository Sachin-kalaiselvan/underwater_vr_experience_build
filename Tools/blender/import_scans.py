# takes CC0 photoscans from poly haven (1k gltf) and makes them quest ready:
# drop parts we don't want, decimate to a budget, put the origin at the bottom,
# bake a soft weathering tint into vertex colour, export fbx + copy the albedo/normal maps.
# blender -b -P Tools/blender/import_scans.py -- <polyhaven_download_dir> <out_dir>
import bpy, bmesh, sys, os, json, shutil, math
from mathutils import Vector, noise

argv = sys.argv[sys.argv.index("--") + 1:]
SRC, OUT = os.path.abspath(argv[0]), os.path.abspath(argv[1])
SI = os.path.abspath(argv[2]) if len(argv) > 2 else None  # smithsonian coral glbs (CC0)
REPORT = {}

# name in poly haven, our name, target tris, keep materials containing, tint mode
SPECS = [
    ("coast_rocks_05", "scan_rock_coast", 3000, None, "rock"),
    ("coast_land_rocks_03", "scan_rock_outcrop", 6000, None, "rock"),
    ("boulder_01", "scan_boulder", 2000, None, "rock"),
    ("namaqualand_boulder_02", "scan_boulder_flat", 2000, None, "rock"),
    ("lambis_shell", "scan_conch", 2500, None, "clean"),
    ("seadogs_compass", "scan_compass", 3000, None, "metal"),
    ("wooden_crate_01", "scan_crate", 1200, None, "wood"),
    ("wooden_barrels_01", "scan_barrel", 1500, "barrel01", "wood"),
    ("dutch_ship_medium", "scan_shipwreck", 14000, "hull", "wreck"),
]

# smithsonian corals: museum skeletons are bleached white, so the living colour goes in
# vertex colour. (file, our name, target tris, size in metres, base colour, tip colour)
CORALS = [
    ("si_staghorn", "scan_coral_staghorn", 4000, 1.1, "#8a6a45", "#d8c49a"),
    ("si_acropora_bush", "scan_coral_acropora", 3500, 0.8, "#7a6440", "#c9b07a"),
    ("si_cauliflower", "scan_coral_cauliflower", 3500, 0.55, "#9a6a5e", "#d9a99a"),
    ("si_pocillopora", "scan_coral_pocillopora", 3500, 0.6, "#6f5a3e", "#b59a6a"),
    ("si_montipora", "scan_coral_montipora", 3000, 0.7, "#5e4660", "#9a7ea0"),
    ("si_brain_meander", "scan_coral_brain_a", 3000, 0.75, "#6e6a3e", "#b0a46a"),
    ("si_brain_grooved", "scan_coral_brain_b", 3000, 0.9, "#7a6a48", "#a8b07a"),
    ("si_finger", "scan_coral_finger", 3500, 0.7, "#8a7a8e", "#c6b8c8"),
    ("si_madrepora", "scan_coral_madrepora", 3500, 0.6, "#a0664a", "#e0a27a"),
]

def hexrgb(h):
    h = h.lstrip("#"); return tuple(int(h[i:i + 2], 16) / 255 for i in (0, 2, 4))

def tint(mode, co, n, zmin, zmax):
    h = 0.0 if zmax - zmin < 1e-5 else (co.z - zmin) / (zmax - zmin)
    up = max(0.0, n.z)
    nz = noise.fractal(co * 1.7, 0.5, 2.0, 3) * 0.5 + 0.5
    if mode == "clean":
        return (1, 1, 1)
    if mode == "metal":  # a bit of verdigris in crevices, mostly clean so it reads as a find
        g = max(0.0, nz - 0.55) * 1.2
        return (1 - g * 0.5, 1 - g * 0.15, 1 - g * 0.3)
    if mode == "rock":  # algae film on top faces, darker near the sand line
        a = up ** 2 * (0.35 + 0.4 * nz)
        base = 0.78 + 0.22 * min(1.0, h * 3)
        return (base * (1 - a * 0.45), base * (1 - a * 0.15), base * (1 - a * 0.5))
    if mode in ("wood", "wreck"):  # silt + algae on top, dark waterlogged lower half
        a = up ** 1.5 * (0.45 + 0.4 * nz)
        base = 0.55 + 0.35 * min(1.0, h * 1.4)
        s = 0.3 * up  # silt lightens upward faces
        r, g, b = base * (1 - a * 0.5), base * (1 - a * 0.2), base * (1 - a * 0.55)
        return (r + s * 0.2, g + s * 0.22, b + s * 0.15)
    return (1, 1, 1)

def run(src_name, out_name, target, keep, mode, size=None, base=None, tip=None):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if mode == "coral":
        bpy.ops.import_scene.gltf(filepath=os.path.join(SI, src_name + ".glb"))
    else:
        gltf = [f for f in os.listdir(os.path.join(SRC, src_name)) if f.endswith(".gltf")][0]
        bpy.ops.import_scene.gltf(filepath=os.path.join(SRC, src_name, gltf))
    obs = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    if keep:
        for o in list(obs):
            if not any(keep in (m.name if m else "") for m in o.data.materials):
                bpy.data.objects.remove(o); obs.remove(o)
        if keep == "barrel01":  # several barrels share the material, take the first one
            for o in obs[1:]: bpy.data.objects.remove(o)
            obs = obs[:1]
    # apply parent transforms so everything is in metres, upright
    for o in obs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = obs[0]
    bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if len(obs) > 1:
        bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active
    ob.name = out_name
    if size:  # specimens are hand sized, scale to a believable colony
        big = max(ob.dimensions)
        if big > 0:
            for v in ob.data.vertices: v.co *= size / big

    if mode == "rock":
        # rock scans often include a skirt of the ground they were scanned on. strip the
        # flat low faces so the rocks sit in our sand instead of on a floating slab
        bm = bmesh.new(); bm.from_mesh(ob.data)
        zmin0 = min(v.co.z for v in bm.verts); zmax0 = max(v.co.z for v in bm.verts)
        cut = zmin0 + (zmax0 - zmin0) * 0.12
        dead = [f for f in bm.faces if f.calc_center_median().z < cut and f.normal.z > 0.55]
        bmesh.ops.delete(bm, geom=dead, context='FACES')
        # drop little islands left behind
        bm.to_mesh(ob.data); bm.free()
    tris = sum(len(p.vertices) - 2 for p in ob.data.polygons)
    # some scans come with every triangle split apart, collapse can't do anything until they're welded
    bm = bmesh.new(); bm.from_mesh(ob.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0005)
    bm.to_mesh(ob.data); bm.free()
    for _ in range(3):
        cur = sum(len(p.vertices) - 2 for p in ob.data.polygons)
        if cur <= target * 1.1: break
        m = ob.modifiers.new("dec", 'DECIMATE'); m.ratio = target / cur; m.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier=m.name)

    # origin at bottom centre so it sits on the sea floor when placed
    me = ob.data
    zs = [v.co.z for v in me.vertices]
    xs = [v.co.x for v in me.vertices]; ys = [v.co.y for v in me.vertices]
    off = Vector(((min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2, min(zs)))
    for v in me.vertices: v.co -= off
    zmin, zmax = 0.0, max(zs) - min(zs)

    if "Col" not in me.color_attributes: me.color_attributes.new("Col", 'BYTE_COLOR', 'POINT')
    col = me.color_attributes["Col"]
    for i, v in enumerate(me.vertices):
        if mode == "coral":
            # darker living tissue at the base, paler growing tips, a little noise so no two look painted
            h = v.co.z / zmax if zmax > 0 else 0
            k = min(1.0, max(0.0, h ** 0.8 + (noise.noise(v.co * 6) * 0.15)))
            b, t = hexrgb(base), hexrgb(tip)
            c = tuple(b[j] + (t[j] - b[j]) * k for j in range(3))
            col.data[i].color = (c[0], c[1], c[2], 1); continue
        c = tint(mode, v.co, v.normal, zmin, zmax)
        col.data[i].color = (min(1, c[0]), min(1, c[1]), min(1, c[2]), 1)

    # textures: albedo + opengl normal straight from the scan
    folder = os.path.join(OUT, out_name); os.makedirs(folder, exist_ok=True)
    mat = me.materials[0]
    found = {}
    for node in mat.node_tree.nodes:
        if node.type == 'TEX_IMAGE' and node.image:
            p = bpy.path.abspath(node.image.filepath)
            low = os.path.basename(p).lower()
            if "_diff" in low: found["albedo"] = p
            elif "_nor_gl" in low: found["normal"] = p
    if mode == "coral":
        for node in mat.node_tree.nodes:
            if node.type == 'TEX_IMAGE' and node.image:
                nm = node.image.name.lower()
                links = [l.to_socket.name for l in node.outputs[0].links]
                kind = "normal" if ("normal" in nm or any("Color" == x for x in []) or node.image.colorspace_settings.name == 'Non-Color' and any(l.to_node.type == 'NORMAL_MAP' for l in node.outputs[0].links)) else ("albedo" if any(l.to_node.type == 'BSDF_PRINCIPLED' and l.to_socket.name == "Base Color" for l in node.outputs[0].links) else None)
                if kind:
                    img = node.image.copy(); img.filepath_raw = os.path.join(folder, f"{out_name}_{kind}.jpg"); img.file_format = 'JPEG'
                    img.save(); found[kind] = img.filepath_raw
    for k, p in list(found.items()):
        dst = os.path.join(folder, f"{out_name}_{k}.jpg")
        if os.path.abspath(p) != dst: shutil.copy(p, dst)
    me.materials.clear()

    bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(folder, out_name + ".fbx"), use_selection=True,
        apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y', bake_space_transform=True,
        mesh_smooth_type='FACE', add_leaf_bones=False, colors_type='SRGB', object_types={'MESH'})
    t = sum(len(p.vertices) - 2 for p in ob.data.polygons)
    d = ob.dimensions
    REPORT[out_name] = {"source": f"Poly Haven: {src_name}", "licence": "CC0", "tris": t, "source_tris": tris,
                        "size_m": [round(d.x, 2), round(d.y, 2), round(d.z, 2)], "maps": sorted(found.keys()), "file": f"Scanned/{out_name}/{out_name}.fbx"}
    print("done", out_name, tris, "->", t, "tris", sorted(found))

jobs = list(SPECS)
if SI:
    jobs += [(f, o, t, None, "coral", sz, b, tp) for (f, o, t, sz, b, tp) in CORALS]
for s in jobs:
    try:
        run(*s)
    except Exception as e:
        import traceback; traceback.print_exc(); REPORT["_error_" + s[1]] = str(e)
json.dump(REPORT, open(os.path.join(os.path.dirname(OUT.rstrip("/")), "..", "..", "..", "Tools", "blender", "scan_report.json") if False else "/tmp/scan_report.json", "w"), indent=2)
