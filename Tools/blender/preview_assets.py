# renders a quick look at every exported fbx so we can actually see what we made.
# blender -b -P Tools/blender/preview_assets.py -- <art_dir> <out_dir>
import bpy, sys, os, glob, math
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
ART, OUT = argv[0], argv[1]
os.makedirs(OUT, exist_ok=True)

def setup():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.device = 'CPU'
    sc.cycles.samples = 24
    sc.cycles.use_denoising = True
    sc.render.resolution_x = 420; sc.render.resolution_y = 320
    sc.view_settings.view_transform = 'Standard'
    w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (0.05, 0.22, 0.28, 1)
    w.node_tree.nodes["Background"].inputs[1].default_value = 0.8
    sun = bpy.data.lights.new("sun", 'SUN'); sun.energy = 3.0; sun.color = (0.85, 0.97, 1.0)
    so = bpy.data.objects.new("sun", sun); sc.collection.objects.link(so)
    so.rotation_euler = (math.radians(35), math.radians(10), math.radians(30))
    mat = bpy.data.materials.new("vc"); mat.use_nodes = True
    nt = mat.node_tree; bsdf = nt.nodes["Principled BSDF"]
    attr = nt.nodes.new("ShaderNodeVertexColor"); attr.layer_name = "Col"
    nt.links.new(attr.outputs[0], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.8
    return mat

for f in sorted(glob.glob(os.path.join(ART, "**/*.fbx"), recursive=True)):
    name = os.path.splitext(os.path.basename(f))[0]
    mat = setup()
    bpy.ops.import_scene.fbx(filepath=f)
    obs = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    if not obs: continue
    mn = Vector((1e9,) * 3); mx = Vector((-1e9,) * 3)
    for o in obs:
        o.data.materials.clear(); o.data.materials.append(mat)
        for c in o.bound_box:
            w = o.matrix_world @ Vector(c)
            mn = Vector(map(min, mn, w)); mx = Vector(map(max, mx, w))
    center = (mn + mx) / 2; size = max((mx - mn).length, 0.01)
    cam = bpy.data.cameras.new("c"); cam.lens = 50
    co = bpy.data.objects.new("c", cam); bpy.context.scene.collection.objects.link(co)
    bpy.context.scene.camera = co
    d = Vector((1.0, -1.25, 0.75)).normalized() * size * 1.35
    if name == "seafloor_terrain": d = Vector((0.0, -1.0, 0.9)).normalized() * size * 0.75
    co.location = center + d
    co.rotation_euler = (center - co.location).to_track_quat('-Z', 'Y').to_euler()
    bpy.context.scene.render.filepath = os.path.join(OUT, name + ".png")
    bpy.ops.render.render(write_still=True)
    print("rendered", name)
