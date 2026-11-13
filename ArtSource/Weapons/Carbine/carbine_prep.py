# Segmented carbine (Tripo "modern assault rifle 3d segmented") for Unity:
# splits the magazine off the body, removes the floating slivers, decimates the 650k-vertex scan, scales to real size,
# barrel -> Unity +Z, origin on the holo sight's line, bakes the 58 source textures into one 4K atlas (base colour + normal),
# and adds child markers Muzzle / RedDot / GripRight / GripLeft / MagTop / MagBottom / BoltCatch.
#
#   blender -b -P ArtSource/Weapons/Carbine/carbine_prep.py -- ArtSource/Weapons/Carbine/modern_assault_rifle_3d_segmented.fbx \
#       Assets/Weapons/Carbine/Carbine.fbx Assets/Weapons/Carbine/Textures [resolution]
import bpy, sys, math, os
import numpy as np
from mathutils import Vector, Matrix

args = sys.argv[sys.argv.index("--") + 1:]
src, out, texdir = args[:3]
RES = int(args[3]) if len(args) > 3 else 4096
os.makedirs(texdir, exist_ok=True)

# part numbers of the Tripo segmentation (identified from labelled renders of the source)
MAGAZINE = {9, 11, 12, 20, 25, 26, 27, 28, 29, 30, 33, 35, 37, 43, 44, 45, 48, 49, 50, 52, 53, 54}
JUNK = {13, 36}                        # thin slivers floating under the handguard
BOLT_CATCH = 46                        # left side, behind the magazine well
DECIMATE_ABOVE, RATIO = 4000, 0.22

# source coordinates: muzzle at -X, Z up, left side towards -Y, overall length 1.0
AIM = Vector((0.152, 0.0, 0.384))      # rear face of the holo sight, centre of its window
DOT = Vector((0.09, 0.0, 0.384))       # inside the window
MUZZLE = Vector((-0.50, 0.0, 0.2905))
LENGTH = 0.80                          # metres
S = LENGTH / 1.0
M = Matrix.Scale(S, 4) @ Matrix.Rotation(math.radians(90), 4, 'Z') @ Matrix.Translation(-AIM)   # barrel -X -> -Y (Blender) = +Z (Unity)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
sc = bpy.context.scene

def part(o): return int(o.name.replace("tripo_part_", ""))
def sel(objs, active):
    for p in bpy.data.objects: p.select_set(p in objs)
    bpy.context.view_layer.objects.active = active

parts = [o for o in bpy.data.objects if o.type == 'MESH']
for o in parts:
    sel([o], o); bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
for o in [o for o in parts if part(o) in JUNK]: bpy.data.objects.remove(o)
parts = [o for o in bpy.data.objects if o.type == 'MESH']

def verts(objs):
    return np.concatenate([np.array([v.co[:] for v in o.data.vertices]) for o in objs])

# ---------- markers measured on the source geometry ----------
mag_hi = [o for o in parts if part(o) in MAGAZINE]
mv = verts(mag_hi)
def slab_centre(v, z0, z1):
    s = v[(v[:, 2] >= z0) & (v[:, 2] <= z1)]
    return Vector(((s[:, 0].min() + s[:, 0].max()) / 2, 0.0, (z0 + z1) / 2))
MAG_TOP = slab_centre(mv, 0.17, 0.19)
MAG_BOTTOM = slab_centre(mv, 0.0, 0.012); MAG_BOTTOM.z = 0.0
grip = verts([o for o in parts if part(o) == 1])
GRIP_R = slab_centre(grip, 0.17, 0.19)                   # middle of the pistol grip, under the trigger guard
fg = verts([o for o in parts if part(o) == 0])
GRIP_L = Vector((-0.17, 0.0, fg[:, 2].min() + 0.005))      # under the handguard, just ahead of the angled foregrip
bc = verts([o for o in parts if part(o) == BOLT_CATCH])
BOLT = Vector(bc.mean(axis=0))
print("SOURCE MARKERS mag top", MAG_TOP, "bottom", MAG_BOTTOM, "grip", GRIP_R, "fore", GRIP_L, "bolt catch", BOLT)

# ---------- low-poly copies (the textured originals stay as the bake source) ----------
groups = {"Body": [], "Magazine": []}
for o in parts:
    lo = o.copy(); lo.data = o.data.copy(); sc.collection.objects.link(lo)
    lo.name = "LO_" + o.name
    if len(lo.data.vertices) > DECIMATE_ABOVE:
        d = lo.modifiers.new("dec", 'DECIMATE'); d.ratio = RATIO
        sel([lo], lo); bpy.ops.object.modifier_apply(modifier=d.name)
    groups["Magazine" if part(o) in MAGAZINE else "Body"].append(lo)
objs = {}
for name, members in groups.items():
    sel(members, members[0]); bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active
    o.name = o.data.name = name
    for l in list(o.data.uv_layers): o.data.uv_layers.remove(l)
    o.data.uv_layers.new(name="UVMap")
    o.data.materials.clear()
    objs[name] = o
    print("LOWPOLY", name, len(o.data.vertices), "verts", len(o.data.polygons), "faces")

# ---------- UVs: one shared atlas ----------
sel(list(objs.values()), objs["Body"])
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=0.002, area_weight=0.0, scale_to_bounds=False)
bpy.ops.uv.pack_islands(rotate=True, margin=0.002)
bpy.ops.object.mode_set(mode='OBJECT')

# ---------- bake: textured originals -> atlas ----------
sc.render.engine = 'CYCLES'
try:
    prefs = bpy.context.preferences.addons['cycles'].preferences
    for dev in ('OPTIX', 'CUDA'):
        try:
            prefs.compute_device_type = dev; prefs.get_devices()
            if any(d.type == dev for d in prefs.devices): break
        except TypeError: pass
    for d in prefs.devices: d.use = d.type != 'CPU'
    sc.cycles.device = 'GPU'
    print("BAKE DEVICE", prefs.compute_device_type)
except Exception as e:
    print("BAKE DEVICE CPU", e)
sc.render.bake.margin = 16
sc.render.bake.use_selected_to_active = True
sc.render.bake.cage_extrusion = 0.004
sc.render.bake.max_ray_distance = 0.02
sc.world = sc.world or bpy.data.worlds.new("World")

def new_image(name, data):
    im = bpy.data.images.new(name, RES, RES, alpha=False, float_buffer=False)
    im.colorspace_settings.name = 'Non-Color' if data else 'sRGB'
    im.generated_color = (0.5, 0.5, 1.0, 1.0) if data else (0.1, 0.1, 0.1, 1.0)
    return im

def target_material(im):
    m = bpy.data.materials.new("bake_" + im.name); m.use_nodes = True
    n = m.node_tree.nodes.new("ShaderNodeTexImage"); n.image = im
    m.node_tree.nodes.active = n
    return m

def bake(name, kind, data):
    im = new_image(name, data)
    mat = target_material(im)
    first = True
    for gname, lo in objs.items():
        lo.data.materials.clear(); lo.data.materials.append(mat)
        hi = [o for o in parts if (part(o) in MAGAZINE) == (gname == "Magazine")]
        sel(hi + [lo], lo)
        sc.render.bake.use_clear = first; first = False
        if kind == 'DIFFUSE':
            sc.render.bake.use_pass_direct = False; sc.render.bake.use_pass_indirect = False; sc.render.bake.use_pass_color = True
            sc.cycles.samples = 4
        else:
            sc.cycles.samples = 4
        bpy.ops.object.bake(type=kind, normal_space='TANGENT')
    print("BAKED", name)
    return im

# a source part that misses a ray keeps the dark default; normals of the scan are reproduced from the dense original
base = bake("Carbine_BaseColor", 'DIFFUSE', False)
normal = bake("Carbine_Normal", 'NORMAL', True)
for im in (base, normal):
    im.filepath_raw = os.path.join(texdir, im.name + ".png"); im.file_format = 'PNG'; im.save()
    print("SAVED", im.filepath_raw)

# ---------- export: real size, one material, markers ----------
for o in parts: bpy.data.objects.remove(o)
final = bpy.data.materials.new("Carbine")
for o in objs.values():
    o.data.transform(M); o.data.update()
    o.data.materials.clear(); o.data.materials.append(final)
    for poly in o.data.polygons: poly.material_index = 0
root = bpy.data.objects.new("Carbine", None); sc.collection.objects.link(root)
for o in objs.values(): o.parent = root
for name, p in (("Muzzle", MUZZLE), ("RedDot", DOT), ("GripRight", GRIP_R), ("GripLeft", GRIP_L),
                ("MagTop", MAG_TOP), ("MagBottom", MAG_BOTTOM), ("BoltCatch", BOLT)):
    e = bpy.data.objects.new(name, None); sc.collection.objects.link(e)
    e.location = M @ p; e.parent = root
    print("MARKER", name, tuple(round(x, 4) for x in e.location))
for k, o in objs.items():
    pts = np.array([v.co[:] for v in o.data.vertices])
    print("BBOX", k, np.round(pts.min(0), 4), np.round(pts.max(0), 4))
for o in bpy.data.objects: o.select_set(o.type in ('MESH', 'EMPTY'))
bpy.ops.export_scene.fbx(filepath=out, use_selection=True, object_types={'MESH', 'EMPTY'}, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
    axis_forward='-Z', axis_up='Y', bake_space_transform=True, mesh_smooth_type='FACE', path_mode='STRIP', bake_anim=False)
print("EXPORTED", out)
