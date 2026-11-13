# Glock 19 for Unity: splits the Tripo parts into Frame / Slide / Barrel / Magazine (slide and reload are animated in Unity),
# scales to real size, barrel -> Unity +Z, origin on the sight line, child markers Muzzle / GripRight / GripLeft / MagTop / MagBottom,
# UV-unwraps and bakes 4K PBR textures (black nitride slide, black polymer frame with a stippled grip).
#
#   blender -b -P ArtSource/Weapons/Glock19/glock_prep.py -- ArtSource/Weapons/Glock19/glock19_segmented.fbx \
#       Assets/Weapons/Glock19/Glock19.fbx Assets/Weapons/Glock19/Textures [resolution]
import bpy, sys, math, os
import numpy as np
from mathutils import Vector, Matrix

args = sys.argv[sys.argv.index("--") + 1:]
src, out, texdir = args[:3]
RES = int(args[3]) if len(args) > 3 else 4096
os.makedirs(texdir, exist_ok=True)

# part numbers of the Tripo segmentation (identified from renders of the source model)
SLIDE = {1, 3, 5, 8, 9, 19, 24, 29, 31, 32, 35, 36, 38, 43, 44, 48, 49, 50, 52, 54, 55, 60, 61, 64, 67, 68, 71, 72, 73, 74}
BARREL = {10, 22, 53}
GRIP_PARTS = {0, 4, 11, 37, 45}
MAGAZINE = {16}                       # floor plate; the body inside the grip is generated below
# magazine body: centre line measured from grip cross-sections (back strap / front strap), source units
MAG_Z0, MAG_Z1 = 0.056, 0.50
MAG_X0, MAG_SLOPE = 0.352, -0.37
MAG_DEPTH, MAG_WIDTH = 0.17, 0.10          # frame shells that carry the grip stippling

# source coordinates (after import): muzzle at -X, Z up, overall length ~1.0
AIM = Vector((0.383, 0.007, 0.700))          # rear sight notch, on the sight line
MUZZLE = Vector((-0.50, 0.007, 0.6285))
GRIP_R = Vector((0.335, 0.007, 0.33))        # middle of the grip, where the shooting hand holds
GRIP_L = Vector((0.26, 0.007, 0.27))         # support hand, wrapped around the front of the grip
LENGTH = 0.185                               # Glock 19 overall length in metres
S = LENGTH / 0.9996
M = Matrix.Scale(S, 4) @ Matrix.Rotation(math.radians(90), 4, 'Z') @ Matrix.Translation(-AIM)   # barrel -X -> -Y (Blender) = +Z (Unity)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
# ---------- procedural materials ----------
def material(name, kind):
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; N = nt.nodes; L = nt.links
    N.clear()
    def node(t, **kw):
        n = N.new(t)
        for k, v in kw.items(): setattr(n, k, v)
        return n
    def math_(op, a, b=None, clamp=False):
        n = node("ShaderNodeMath", operation=op, use_clamp=clamp)
        for i, v in enumerate((a, b)):
            if v is None: continue
            if isinstance(v, (int, float)): n.inputs[i].default_value = v
            else: L.new(v, n.inputs[i])
        return n.outputs[0]
    def ramp(v, lo, hi):
        n = node("ShaderNodeMapRange", clamp=True); L.new(v, n.inputs["Value"])
        n.inputs["From Min"].default_value = lo; n.inputs["From Max"].default_value = hi
        return n.outputs[0]
    def mix(fac, a, b):
        n = node("ShaderNodeMix", data_type='RGBA'); L.new(fac, n.inputs["Factor"])
        for sock, v in ((n.inputs[6], a), (n.inputs[7], b)):
            if isinstance(v, tuple): sock.default_value = v
            else: L.new(v, sock)
        return n.outputs[2]
    def mixf(fac, a, b):
        n = node("ShaderNodeMix", data_type='FLOAT'); L.new(fac, n.inputs["Factor"])
        for sock, v in ((n.inputs[2], a), (n.inputs[3], b)):
            if isinstance(v, (int, float)): sock.default_value = v
            else: L.new(v, sock)
        return n.outputs[0]
    def noise(vec, scale, detail=4.0, rough=0.55):
        n = node("ShaderNodeTexNoise"); L.new(vec, n.inputs["Vector"])
        n.inputs["Scale"].default_value = scale; n.inputs["Detail"].default_value = detail; n.inputs["Roughness"].default_value = rough
        return n.outputs["Fac"]

    p0 = node("ShaderNodeAttribute", attribute_name="P0").outputs["Vector"]
    sep = node("ShaderNodeSeparateXYZ"); L.new(p0, sep.inputs[0])
    edgeAO = node("ShaderNodeAmbientOcclusion", inside=True, samples=16); edgeAO.inputs["Distance"].default_value = 0.0015
    edge = ramp(math_('SUBTRACT', 1.0, edgeAO.outputs["AO"]), 0.35, 0.8)          # convex edges -> 1
    grain = noise(p0, 900.0, 2.0)
    blotch = noise(p0, 18.0, 5.0)
    height = None

    if kind == "slide":         # black nitride: very dark, slightly metallic, worn bright on the edges
        wear = math_('MULTIPLY', edge, ramp(noise(p0, 60.0, 6.0), 0.42, 0.62), clamp=True)
        base = mix(ramp(blotch, 0.35, 0.7), (0.018, 0.018, 0.02, 1), (0.03, 0.03, 0.032, 1))
        color = mix(wear, base, (0.16, 0.16, 0.165, 1))
        metal = mixf(wear, 0.55, 0.95)
        rough = mixf(wear, math_('ADD', 0.42, math_('MULTIPLY', grain, 0.12)), 0.3)
        height = math_('MULTIPLY', grain, 0.15)
    elif kind == "barrel":      # bare steel, darkened
        color = mix(ramp(blotch, 0.3, 0.7), (0.07, 0.07, 0.072, 1), (0.11, 0.11, 0.115, 1))
        metal = 1.0
        rough = math_('ADD', 0.3, math_('MULTIPLY', grain, 0.1))
        height = math_('MULTIPLY', grain, 0.1)
    elif kind == "mag":         # black coated steel magazine body
        color = mix(ramp(blotch, 0.3, 0.7), (0.02, 0.02, 0.022, 1), (0.035, 0.035, 0.037, 1))
        metal = 0.6
        rough = math_('ADD', 0.38, math_('MULTIPLY', grain, 0.12))
        height = math_('MULTIPLY', grain, 0.1)
    elif kind == "brass":       # 9 mm round
        color = mix(ramp(blotch, 0.3, 0.75), (0.55, 0.36, 0.12, 1), (0.75, 0.52, 0.22, 1))
        metal = 1.0
        rough = math_('ADD', 0.22, math_('MULTIPLY', grain, 0.12))
        height = math_('MULTIPLY', grain, 0.05)
    else:                       # polymer frame, stippled grip panels
        gripAttr = node("ShaderNodeAttribute", attribute_name="GRIP").outputs["Fac"]
        x, z = sep.outputs[0], sep.outputs[2]
        region = math_('MULTIPLY', ramp(x, 0.03, 0.08), math_('SUBTRACT', 1.0, ramp(z, 0.36, 0.40)))
        region = math_('MULTIPLY', region, ramp(z, 0.07, 0.10))
        mask = math_('MULTIPLY', region, gripAttr, clamp=True)
        vor = node("ShaderNodeTexVoronoi", feature='F1', distance='EUCLIDEAN'); L.new(p0, vor.inputs["Vector"])
        vor.inputs["Scale"].default_value = 230.0; vor.inputs["Randomness"].default_value = 0.35
        bumps = math_('SUBTRACT', 1.0, ramp(vor.outputs["Distance"], 0.18, 0.42))      # raised pyramids of the stippling
        stip = math_('MULTIPLY', bumps, mask)
        base = mix(ramp(blotch, 0.3, 0.7), (0.022, 0.022, 0.024, 1), (0.034, 0.033, 0.033, 1))
        color = mix(math_('MULTIPLY', edge, 0.6), base, (0.06, 0.06, 0.062, 1))           # polymer edges catch a little grey
        metal = 0.0
        rough = mixf(mask, math_('ADD', 0.55, math_('MULTIPLY', grain, 0.1)), mixf(bumps, 0.78, 0.6))
        height = math_('ADD', math_('MULTIPLY', stip, 1.0), math_('MULTIPLY', grain, 0.12))

    bsdf = node("ShaderNodeBsdfPrincipled")
    L.new(color, bsdf.inputs["Base Color"])
    for sock, v in (("Metallic", metal), ("Roughness", rough)):
        if isinstance(v, (int, float)): bsdf.inputs[sock].default_value = v
        else: L.new(v, bsdf.inputs[sock])
    bump = node("ShaderNodeBump"); bump.inputs["Strength"].default_value = 1.0
    bump.inputs["Distance"].default_value = 0.00018
    L.new(height, bump.inputs["Height"]); L.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    outn = node("ShaderNodeOutputMaterial"); L.new(bsdf.outputs[0], outn.inputs["Surface"])
    emit = node("ShaderNodeEmission")
    img = node("ShaderNodeTexImage"); N.active = img
    m_sockets[name] = {"color": color, "metal": metal, "rough": rough, "bsdf": bsdf, "out": outn, "emit": emit, "img": img, "nt": nt}
    return m

m_sockets = {}
mats = {"Frame": material("Frame_bake", "frame"), "Slide": material("Slide_bake", "slide"), "Barrel": material("Barrel_bake", "barrel"),
        "Mag": material("Mag_bake", "mag"), "Brass": material("Brass_bake", "brass")}

parts = [o for o in bpy.data.objects if o.type == 'MESH']
groups = {"Frame": [], "Slide": [], "Barrel": [], "Magazine": [], "MagRound": []}

def prepare(o, n, mat):
    bpy.context.view_layer.objects.active = o
    for p in bpy.data.objects: p.select_set(p == o)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    # original position (source units) and a grip flag, used by the procedural material
    co = np.empty(len(o.data.vertices) * 3, dtype=np.float32); o.data.vertices.foreach_get("co", co)
    a = o.data.attributes.new("P0", 'FLOAT_VECTOR', 'POINT'); a.data.foreach_set("vector", co)
    g = o.data.attributes.new("GRIP", 'FLOAT', 'POINT'); g.data.foreach_set("value", [1.0 if n in GRIP_PARTS else 0.0] * len(o.data.vertices))
    o.data.materials.clear(); o.data.materials.append(mat)

for o in parts:
    n = int(o.name.replace("tripo_part_", ""))
    prepare(o, n, mats["Slide" if n in SLIDE else "Barrel" if n in BARREL else "Frame"])
    groups["Slide" if n in SLIDE else "Barrel" if n in BARREL else "Magazine" if n in MAGAZINE else "Frame"].append(o)

# ---------- generated magazine body (hidden in the grip, follows the ~20 deg grip angle) and its top round ----------
def mag_x(z): return MAG_X0 + MAG_SLOPE * (z - MAG_Z0)
body = bpy.data.meshes.new("MagBody")
verts = []
for z in (MAG_Z0, MAG_Z1):
    cx = mag_x(z)
    for dx, dy in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
        verts.append((cx + dx * MAG_DEPTH / 2, 0.007 + dy * MAG_WIDTH / 2, z))
body.from_pydata(verts, [], [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)])
import bmesh
bm = bmesh.new(); bm.from_mesh(body); bmesh.ops.recalc_face_normals(bm, faces=bm.faces); bm.to_mesh(body); bm.free()
body.update()
bo = bpy.data.objects.new("MagBody", body); bpy.context.scene.collection.objects.link(bo)
prepare(bo, -1, mats["Mag"]); groups["Magazine"].append(bo)
r = 0.0245                                   # 9 mm round, source units
rear = mag_x(MAG_Z1) + MAG_DEPTH / 2 - 0.01
bpy.ops.mesh.primitive_cylinder_add(vertices=20, radius=r, depth=0.105, location=(rear - 0.0525, 0.007, MAG_Z1 + r), rotation=(0, -math.pi / 2, 0))
case = bpy.context.active_object; prepare(case, -1, mats["Brass"]); groups["MagRound"].append(case)
bpy.ops.mesh.primitive_cone_add(vertices=20, radius1=r, radius2=r * 0.45, depth=0.05, location=(rear - 0.105 - 0.025, 0.007, MAG_Z1 + r), rotation=(0, -math.pi / 2, 0))
nose = bpy.context.active_object; prepare(nose, -1, mats["Brass"]); groups["MagRound"].append(nose)

objs = {}
for name, members in groups.items():
    for p in bpy.data.objects: p.select_set(p in members)
    bpy.context.view_layer.objects.active = members[0]
    bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active
    o.name = o.data.name = name
    o.data.transform(M); o.data.update()
    objs[name] = o

# ---------- UVs: one shared atlas for all meshes ----------
for o in bpy.data.objects: o.select_set(o.name in objs)
bpy.context.view_layer.objects.active = objs["Frame"]
for o in objs.values():
    if not o.data.uv_layers: o.data.uv_layers.new(name="UVMap")
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=0.002, area_weight=0.0, scale_to_bounds=False)
bpy.ops.uv.pack_islands(rotate=True, margin=0.003)
bpy.ops.object.mode_set(mode='OBJECT')


# ---------- bake ----------
sc = bpy.context.scene
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
sc.world = sc.world or bpy.data.worlds.new("World")
sc.render.bake.use_clear = True
for o in bpy.data.objects: o.select_set(o.name in objs)
bpy.context.view_layer.objects.active = objs["Frame"]

def new_image(name, data):
    im = bpy.data.images.new(name, RES, RES, alpha=True, float_buffer=False)
    im.colorspace_settings.name = 'Non-Color' if data else 'sRGB'
    return im

def bake(name, kind, data, value=None, samples=32):
    im = new_image(name, data)
    for s in m_sockets.values():
        s["img"].image = im
        nt = s["nt"]; L = nt.links
        for l in list(s["out"].inputs["Surface"].links): L.remove(l)
        if kind == 'EMIT':
            v = s[value]
            for l in list(s["emit"].inputs["Color"].links): L.remove(l)
            if isinstance(v, (int, float)): s["emit"].inputs["Color"].default_value = (v, v, v, 1)
            else: L.new(v, s["emit"].inputs["Color"])
            L.new(s["emit"].outputs[0], s["out"].inputs["Surface"])
        else:
            L.new(s["bsdf"].outputs[0], s["out"].inputs["Surface"])
    sc.cycles.samples = samples
    bpy.ops.object.bake(type=kind, normal_space='TANGENT' if kind == 'NORMAL' else 'OBJECT')
    print("BAKED", name)
    return im

def pixels(im):
    a = np.empty(RES * RES * 4, dtype=np.float32); im.pixels.foreach_get(a); return a.reshape(-1, 4)

base = bake("Glock19_BaseColor", 'EMIT', False, "color")
metal = bake("metal", 'EMIT', True, "metal")
rough = bake("rough", 'EMIT', True, "rough")
normal = bake("Glock19_Normal", 'NORMAL', True, samples=8)
sc.world.light_settings.distance = 0.02
ao = bake("Glock19_AO", 'AO', True, samples=96)

# URP Lit: metallic in R, smoothness in A
ms = new_image("Glock19_MetallicSmoothness", True)
pm, pr = pixels(metal), pixels(rough)
o = np.zeros_like(pm); o[:, 0] = o[:, 1] = o[:, 2] = pm[:, 0]; o[:, 3] = 1.0 - pr[:, 0]
ms.pixels.foreach_set(o.ravel())
for im in (base, normal, ao, ms):
    im.filepath_raw = os.path.join(texdir, im.name + ".png"); im.file_format = 'PNG'; im.save()
    print("SAVED", im.filepath_raw)

# ---------- export: one material for the atlas, markers, root ----------
final = bpy.data.materials.new("Glock19")
for o in objs.values():
    o.data.materials.clear(); o.data.materials.append(final)
    for poly in o.data.polygons: poly.material_index = 0
root = bpy.data.objects.new("Glock19", None); sc.collection.objects.link(root)
for k, o in objs.items(): o.parent = objs["Magazine"] if k == "MagRound" else root   # the top round moves with the magazine
MAG_TOP = Vector((MAG_X0 + MAG_SLOPE * (MAG_Z1 - MAG_Z0), 0.007, MAG_Z1))
MAG_BOTTOM = Vector((MAG_X0 - 0.02, 0.007, 0.0))        # under the floor plate, where the support hand pushes
for name, p in (("Muzzle", MUZZLE), ("GripRight", GRIP_R), ("GripLeft", GRIP_L), ("MagTop", MAG_TOP), ("MagBottom", MAG_BOTTOM)):
    e = bpy.data.objects.new(name, None); sc.collection.objects.link(e)
    e.location = M @ p; e.parent = root
    print("MARKER", name, tuple(round(x, 4) for x in e.location))
for k, o in objs.items():
    pts = [v.co for v in o.data.vertices]
    print("BBOX", k, tuple(round(min(q[i] for q in pts), 4) for i in range(3)), tuple(round(max(q[i] for q in pts), 4) for i in range(3)))
for o in bpy.data.objects: o.select_set(True)
bpy.ops.export_scene.fbx(filepath=out, use_selection=True, object_types={'MESH', 'EMPTY'}, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
    axis_forward='-Z', axis_up='Y', bake_space_transform=True, mesh_smooth_type='FACE', path_mode='STRIP', bake_anim=False)
print("EXPORTED", out)
