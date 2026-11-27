# exports the same art the unity build uses as GLB for the web version, plus a sea floor heightmap.
# blender -b -P Tools/blender/export_web.py -- <art_dir> <web_assets_dir>
import bpy, os, sys, json, struct
from mathutils import Vector
from mathutils.bvhtree import BVHTree

argv = sys.argv[sys.argv.index("--") + 1:]
ART, OUT = os.path.abspath(argv[0]), os.path.abspath(argv[1])
os.makedirs(OUT, exist_ok=True)

GENERATED = ["Environment/seafloor_terrain", "Environment/seagrass_clump", "Environment/kelp_strand",
             "Environment/basalt_columns", "Coral/coral_seafan", "Coral/anemone_glow", "Coral/tube_worms",
             "Structures/wreck_anchor", "MarineLife/fish_small", "MarineLife/ray_large",
             "InteractiveObjects/destination_ring", "InteractiveObjects/info_marker"]
SCANS = sorted(d for d in os.listdir(os.path.join(ART, "Scanned")) if os.path.isdir(os.path.join(ART, "Scanned", d)))

def load(rel):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=os.path.join(ART, rel + ".fbx"))
    obs = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    for o in obs: o.select_set(True)
    bpy.context.view_layer.objects.active = obs[0]
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if len(obs) > 1: bpy.ops.object.join()
    return bpy.context.view_layer.objects.active

def vc_material(name):
    m = bpy.data.materials.new(name); m.use_nodes = True
    n = m.node_tree; b = n.nodes["Principled BSDF"]
    vc = n.nodes.new("ShaderNodeVertexColor"); vc.layer_name = "Col"
    n.links.new(vc.outputs[0], b.inputs["Base Color"])
    b.inputs["Roughness"].default_value = 0.85
    return m

def scan_material(name, d):
    m = bpy.data.materials.new(name); m.use_nodes = True
    n = m.node_tree; b = n.nodes["Principled BSDF"]
    t = n.nodes.new("ShaderNodeTexImage"); t.image = bpy.data.images.load(os.path.join(d, name + "_albedo.jpg"))
    n.links.new(t.outputs[0], b.inputs["Base Color"])
    nm_path = os.path.join(d, name + "_normal.jpg")
    if os.path.exists(nm_path):
        nt = n.nodes.new("ShaderNodeTexImage"); nt.image = bpy.data.images.load(nm_path); nt.image.colorspace_settings.name = 'Non-Color'
        nm = n.nodes.new("ShaderNodeNormalMap"); n.links.new(nt.outputs[0], nm.inputs[1]); n.links.new(nm.outputs[0], b.inputs["Normal"])
    b.inputs["Roughness"].default_value = 0.85
    return m

def export(ob, name):
    ob.name = name
    bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True)
    bpy.ops.export_scene.gltf(filepath=os.path.join(OUT, name + ".glb"), export_format='GLB', use_selection=True,
        export_yup=True, export_apply=True, export_vertex_color='ACTIVE', export_all_vertex_colors=False,
        export_image_format='JPEG', export_jpeg_quality=85, export_animations=False, export_materials='EXPORT')
    print("glb", name, os.path.getsize(os.path.join(OUT, name + ".glb")) // 1024, "KB")

report = {}
for rel in GENERATED:
    ob = load(rel)
    ob.data.materials.clear(); ob.data.materials.append(vc_material("vc"))
    name = os.path.basename(rel)
    if name == "seafloor_terrain":
        # heightmap on a 256 grid in three.js space: x = blender x, z = -blender y, y = blender z
        bvh = BVHTree.FromObject(ob, bpy.context.evaluated_depsgraph_get())
        N, half = 256, 75.0
        hs = []
        for j in range(N):
            tz = -half + 2 * half * j / (N - 1)
            for i in range(N):
                tx = -half + 2 * half * i / (N - 1)
                hit = bvh.ray_cast(Vector((tx, -tz, 200)), Vector((0, 0, -1)))
                hs.append(hit[0].z if hit[0] else 0.0)
        with open(os.path.join(OUT, "seafloor_height.bin"), "wb") as f:
            f.write(struct.pack(f"<{len(hs)}f", *hs))
        report["height"] = {"n": N, "half": half, "min": min(hs), "max": max(hs)}
    export(ob, name)

for s in SCANS:
    d = os.path.join(ART, "Scanned", s)
    ob = load(f"Scanned/{s}/{s}")
    ob.data.materials.clear(); ob.data.materials.append(scan_material(s, d))
    export(ob, s)

report["scans"] = SCANS
report["generated"] = [os.path.basename(r) for r in GENERATED]
json.dump(report, open(os.path.join(OUT, "manifest.json"), "w"), indent=1)
print("manifest written")
