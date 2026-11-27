# look-dev render: the real ABYSS assets + textures in a quick underwater set, to judge the art.
# NOT a screenshot of the app. cycles volumetrics here vs simple fog on the quest.
# blender -b -P Tools/blender/lookdev.py -- <art_dir> <out.png> <shot: reef|wreck>
import bpy, os, sys, math, random
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
ART, OUT, SHOT = argv[0], argv[1], argv[2] if len(argv) > 2 else "reef"
TEX = os.path.join(ART, "Textures", "Scanned")
random.seed(5)
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'
sc.cycles.samples = 40; sc.cycles.use_denoising = True
sc.cycles.volume_step_rate = 4.0; sc.cycles.max_bounces = 4
sc.render.resolution_x, sc.render.resolution_y = 1280, 720
sc.view_settings.view_transform = 'AgX'; sc.view_settings.look = 'AgX - Medium High Contrast'

# water: blue-green scatter + absorption, a bit of light from above through the world
w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True
nt = w.node_tree; bg = nt.nodes["Background"]
bg.inputs[0].default_value = (0.02, 0.16, 0.2, 1); bg.inputs[1].default_value = 0.6
vol = nt.nodes.new("ShaderNodeVolumePrincipled")
vol.inputs["Color"].default_value = (0.25, 0.75, 0.8, 1)
vol.inputs["Density"].default_value = 0.075
vol.inputs["Absorption Color"].default_value = (0.55, 0.9, 0.95, 1)
vol.inputs["Anisotropy"].default_value = 0.6
# a bounded box of water, not the world volume (that would swallow the sun entirely)
bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 20, -2))
water = bpy.context.active_object; water.name = "water"; water.scale = (160, 160, 30)
wm = bpy.data.materials.new("water"); wm.use_nodes = True; wn = wm.node_tree
wn.nodes.remove(wn.nodes["Principled BSDF"])
wv = wn.nodes.new("ShaderNodeVolumePrincipled")
for k in ("Color", "Density", "Absorption Color", "Anisotropy"): wv.inputs[k].default_value = vol.inputs[k].default_value
wn.links.new(wv.outputs[0], wn.nodes["Material Output"].inputs["Volume"])
water.data.materials.append(wm)
nt.nodes.remove(vol)

sun = bpy.data.lights.new("sun", 'SUN'); sun.energy = 4.0; sun.color = (0.85, 0.97, 1.0); sun.angle = math.radians(2); sun.cycles.cast_shadow = False  # the quest build has no realtime shadows, so neither does this
so = bpy.data.objects.new("sun", sun); sc.collection.objects.link(so)
so.rotation_euler = (math.radians(20), math.radians(12), math.radians(40))

def img(path, non_color=False):
    i = bpy.data.images.load(path); i.colorspace_settings.name = 'Non-Color' if non_color else 'sRGB'; return i

def mat_scan(name):
    m = bpy.data.materials.new(name); m.use_nodes = True; n = m.node_tree; b = n.nodes["Principled BSDF"]
    d = os.path.join(ART, "Scanned", name)
    t = n.nodes.new("ShaderNodeTexImage"); t.image = img(os.path.join(d, name + "_albedo.jpg"))
    vc = n.nodes.new("ShaderNodeVertexColor"); vc.layer_name = "Col"
    mul = n.nodes.new("ShaderNodeMix"); mul.data_type = 'RGBA'; mul.blend_type = 'MULTIPLY'; mul.inputs[0].default_value = 1
    n.links.new(t.outputs[0], mul.inputs[6]); n.links.new(vc.outputs[0], mul.inputs[7]); n.links.new(mul.outputs[2], b.inputs["Base Color"])
    nm = n.nodes.new("ShaderNodeTexImage"); nm.image = img(os.path.join(d, name + "_normal.jpg"), True)
    nmap = n.nodes.new("ShaderNodeNormalMap"); n.links.new(nm.outputs[0], nmap.inputs[1]); n.links.new(nmap.outputs[0], b.inputs["Normal"])
    b.inputs["Roughness"].default_value = 0.75
    return m

def mat_detail(name, tex, scale, strength):
    # mirrors UnderwaterVertexLit detail: scan colour * vertex luma, blended by strength
    m = bpy.data.materials.new(name); m.use_nodes = True; n = m.node_tree; b = n.nodes["Principled BSDF"]
    tc = n.nodes.new("ShaderNodeTexCoord"); mp = n.nodes.new("ShaderNodeMapping"); mp.inputs["Scale"].default_value = (scale,) * 3
    n.links.new(tc.outputs["Object"], mp.inputs[0])
    t = n.nodes.new("ShaderNodeTexImage"); t.image = img(os.path.join(TEX, tex + "_diff_1k.jpg")); t.projection = 'BOX'; t.projection_blend = 0.3
    n.links.new(mp.outputs[0], t.inputs[0])
    vc = n.nodes.new("ShaderNodeVertexColor"); vc.layer_name = "Col"
    bw = n.nodes.new("ShaderNodeRGBToBW"); n.links.new(vc.outputs[0], bw.inputs[0])
    m1 = n.nodes.new("ShaderNodeMath"); m1.operation = 'MULTIPLY'; m1.inputs[1].default_value = 1.6; n.links.new(bw.outputs[0], m1.inputs[0])
    mul = n.nodes.new("ShaderNodeMix"); mul.data_type = 'RGBA'; mul.blend_type = 'MULTIPLY'; mul.inputs[0].default_value = 1
    n.links.new(t.outputs[0], mul.inputs[6]); n.links.new(m1.outputs[0], mul.inputs[7])
    mix = n.nodes.new("ShaderNodeMix"); mix.data_type = 'RGBA'; mix.inputs[0].default_value = strength
    n.links.new(vc.outputs[0], mix.inputs[6]); n.links.new(mul.outputs[2], mix.inputs[7]); n.links.new(mix.outputs[2], b.inputs["Base Color"])
    nm = n.nodes.new("ShaderNodeTexImage"); nm.image = img(os.path.join(TEX, tex + "_nor_gl_1k.jpg"), True); nm.projection = 'BOX'; nm.projection_blend = 0.3
    n.links.new(mp.outputs[0], nm.inputs[0])
    nmap = n.nodes.new("ShaderNodeNormalMap"); n.links.new(nm.outputs[0], nmap.inputs[1]); n.links.new(nmap.outputs[0], b.inputs["Normal"])
    b.inputs["Roughness"].default_value = 0.85
    return m

def mat_vc(name, emit=0.0):
    m = bpy.data.materials.new(name); m.use_nodes = True; n = m.node_tree; b = n.nodes["Principled BSDF"]
    vc = n.nodes.new("ShaderNodeVertexColor"); vc.layer_name = "Col"; n.links.new(vc.outputs[0], b.inputs["Base Color"])
    if emit:
        n.links.new(vc.outputs[0], b.inputs["Emission Color"]); b.inputs["Emission Strength"].default_value = emit
    b.inputs["Roughness"].default_value = 0.8
    return m

cache = {}
def place(rel, mat, loc, rot_z=None, scale=1.0, tilt=(0, 0), sink=0.0):
    path = os.path.join(ART, rel + ".fbx")
    before = set(bpy.context.scene.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    new = [o for o in bpy.context.scene.objects if o not in before]
    # the importer adds a 90 degree axis fix as object rotation, bake it in before we set our own
    bpy.ops.object.select_all(action='DESELECT')
    for o in new: o.select_set(True)
    bpy.context.view_layer.objects.active = new[0]
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    for o in new:
        if o.type != 'MESH': continue
        o.data.materials.clear(); o.data.materials.append(mat)
        o.location = Vector(loc) + Vector((0, 0, -sink))
        o.rotation_euler = (math.radians(tilt[0]), math.radians(tilt[1]), math.radians(rot_z if rot_z is not None else random.uniform(0, 360)))
        o.scale = (scale,) * 3
    return new

# sea floor patch: the real terrain mesh with the scanned sand
sand = mat_detail("sand", "coast_sand_01", 0.32, 0.9)
coral = mat_detail("coral", "coral_ground_02", 2.2, 0.45)
rust = mat_detail("rust", "rusty_metal_02", 1.5, 0.8)
terrain = place("Environment/seafloor_terrain", sand, (0, 0, 0), 0)
def ground(x, y):
    # cast against the terrain only, in its local space
    o = [t for t in terrain if t.type == 'MESH'][0]
    inv = o.matrix_world.inverted()
    org = inv @ Vector((x, y, 80)); d = (inv.to_3x3() @ Vector((0, 0, -1))).normalized()
    hit, loc, *_ = o.ray_cast(org, d)
    return (o.matrix_world @ loc).z if hit else 0.0

scans = {n: mat_scan(n) for n in os.listdir(os.path.join(ART, "Scanned"))}
def S(n): return f"Scanned/{n}/{n}"
vc = mat_vc("vc"); vc2 = mat_vc("vc2")
for m in (vc2,):  # two sided look
    pass

cam = bpy.data.cameras.new("cam"); cam.lens = 22  # ~ the wide feel of a headset
co = bpy.data.objects.new("cam", cam); sc.collection.objects.link(co); sc.camera = co

def scatter(rel, mat, cx, cy, n, rmin, rmax, smin, smax, sink=0.0):
    for _ in range(n):
        a = random.uniform(0, math.tau); r = random.uniform(rmin, rmax)
        x, y = cx + math.cos(a) * r, cy + math.sin(a) * r
        place(rel, mat, (x, y, ground(x, y)), None, random.uniform(smin, smax), (random.uniform(-6, 6), random.uniform(-6, 6)), sink)

if SHOT == "reef":
    # looking from the start point toward the gateway and the reef beyond (+y in blender)
    cx, cy = 0, 22
    place(S("scan_rock_outcrop"), scans["scan_rock_outcrop"], (-5.5, 13, ground(-5.5, 13) - 0.4), 90, 0.9)
    place(S("scan_rock_outcrop"), scans["scan_rock_outcrop"], (5.5, 14, ground(5.5, 14) - 0.6), 250, 0.75)
    # reef = dense patches ("bommies") of mixed species on rock, not evenly sprinkled
    corals = [n for n in scans if n.startswith("scan_coral_")]
    for (px, py, r) in [(-3, 19, 3.2), (3.5, 21, 3.0), (-0.5, 26, 3.5), (6, 16, 2.2), (-6, 15, 2.0)]:
        place(S("scan_boulder_flat"), scans["scan_boulder_flat"], (px, py, ground(px, py) - 0.2), None, 1.1)
        for _ in range(int(r * 9)):
            a = random.uniform(0, math.tau); d = random.uniform(0, r) ** 0.8
            x, y = px + math.cos(a) * d, py + math.sin(a) * d
            n = random.choice(corals)
            place(S(n), scans[n], (x, y, ground(x, y) + random.uniform(-0.15, 0.25)), None, random.uniform(0.7, 1.4), (random.uniform(-12, 12), random.uniform(-12, 12)))
    scatter("Coral/coral_seafan", coral, cx, cy, 6, 3, 10, 0.8, 1.3)
    scatter(S("scan_boulder"), scans["scan_boulder"], cx, cy, 6, 5, 12, 0.7, 1.4, 0.2)
    scatter("Environment/seagrass_clump", vc, 0, 6, 25, 1, 9, 0.8, 1.4)
    place(S("scan_conch"), scans["scan_conch"], (1.0, 14.0, ground(1.0, 14.0)), 30, 2.2)
    co.location = (1.5, 11.0, ground(1.5, 11.0) + 1.6)
    co.rotation_euler = (math.radians(80), 0, math.radians(-8))
else:
    wx, wy = 22, 46
    place(S("scan_shipwreck"), scans["scan_shipwreck"], (wx, wy, ground(wx, wy) - 1.6), 20, 1.0, (4, 24))
    for i in range(4):
        x, y = wx - 6 + random.uniform(-2, 3), wy - 4 + random.uniform(-3, 3)
        place(S("scan_crate"), scans["scan_crate"], (x, y, ground(x, y) - 0.15), None, 1.0, (random.uniform(-10, 10), random.uniform(-15, 15)))
    for i in range(3):
        x, y = wx - 4 + random.uniform(-2, 2), wy - 8 + random.uniform(-2, 2)
        place(S("scan_barrel"), scans["scan_barrel"], (x, y, ground(x, y) + 0.3), None, 1.0, (90, 0))
    place("Structures/wreck_anchor", rust, (wx - 8, wy - 6, ground(wx - 8, wy - 6) + 0.1), 0, 1.0, (72, 0))
    place(S("scan_compass"), scans["scan_compass"], (wx - 5, wy - 10.5, ground(wx - 5, wy - 10.5)), 40, 2.2)
    scatter(S("scan_boulder"), scans["scan_boulder"], wx, wy, 6, 10, 15, 0.8, 1.4, 0.2)
    scatter("Coral/coral_seafan", coral, wx, wy - 5, 5, 4, 9, 0.6, 1.0)
    scatter("Coral/sponge_barrel", coral, wx, wy - 5, 5, 4, 9, 0.5, 0.9, 0.1)
    co.location = (wx - 7, wy - 16, ground(wx - 7, wy - 16) + 1.7)
    co.rotation_euler = (math.radians(86), 0, math.radians(-18))

sc.render.filepath = OUT
bpy.ops.render.render(write_still=True)
print("rendered", OUT)
