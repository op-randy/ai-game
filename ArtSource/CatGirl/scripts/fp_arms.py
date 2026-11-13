# First-person arms: a copy of CatGirl.fbx with only the arm meshes, with working fingers.
# The original rig skins each hand only to the Hand bone (rigid fingers), and the left finger bones
# are dummies bunched at the wrist. This script
#   1. moves the left finger bones into the left fingers (right-hand bones mirrored, fitted by ICP),
#   2. re-weights the hand vertices over the Hand bone and the finger bones (nearest finger chain),
#   3. exports only the armature + CG_ArmL / CG_ArmR with the same FBX settings as unity_export.py.
# The original CatGirl.fbx is not changed.
#
# blender -b -P ArtSource/CatGirl/scripts/fp_arms.py -- Assets/Characters/CatGirl/CatGirl.fbx Assets/Characters/CatGirl/CatGirl_FPArms.fbx [preview.png]
import bpy, sys, math
from mathutils import Vector, Matrix, kdtree

a = sys.argv[sys.argv.index("--") + 1:]
SRC, DST = a[0], a[1]
PREVIEW = a[2] if len(a) > 2 else None

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=SRC)
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
arm.name = "CatGirl_Rig"
MW = arm.matrix_world
FINGERS = ["Thumb", "Index", "Middle", "Ring", "Pinky"]
JOINTS = ["Proximal", "Intermediate", "Distal"]

for o in list(bpy.data.objects):
    if o.type == 'MESH' and o.name not in ("CG_ArmL", "CG_ArmR"):
        bpy.data.objects.remove(o, do_unlink=True)


def hand_verts(mesh, side, thresh=0.3):
    g = mesh.vertex_groups[f"{side}_Hand"].index
    out = []
    for v in mesh.data.vertices:
        w = next((x.weight for x in v.groups if x.group == g), 0.0)
        if w > thresh: out.append(mesh.matrix_world @ v.co)
    return out


def best_rigid(src, dst):
    """Least-squares rotation + translation (Kabsch, via power-iteration-free 3x3 SVD substitute: Horn quaternion)."""
    n = len(src)
    cs = sum(src, Vector()) / n; cd = sum(dst, Vector()) / n
    S = [[0.0] * 3 for _ in range(3)]
    for p, q in zip(src, dst):
        p = p - cs; q = q - cd
        for i in range(3):
            for j in range(3): S[i][j] += p[i] * q[j]
    Sxx, Sxy, Sxz = S[0]; Syx, Syy, Syz = S[1]; Szx, Szy, Szz = S[2]
    N = [[Sxx + Syy + Szz, Syz - Szy, Szx - Sxz, Sxy - Syx],
         [Syz - Szy, Sxx - Syy - Szz, Sxy + Syx, Szx + Sxz],
         [Szx - Sxz, Sxy + Syx, -Sxx + Syy - Szz, Syz + Szy],
         [Sxy - Syx, Szx + Sxz, Syz + Szy, -Sxx - Syy + Szz]]
    # largest eigenvector by shifted power iteration
    q = [1.0, 0.0, 0.0, 0.0]
    for _ in range(200):
        r = [sum((N[i][j] + (10.0 if i == j else 0.0)) * q[j] for j in range(4)) for i in range(4)]
        l = math.sqrt(sum(x * x for x in r)); q = [x / l for x in r]
    from mathutils import Quaternion
    R = Quaternion(q).to_matrix()
    return R, cd - R @ cs


# ---------------- 1. left finger bones from the mirrored right hand ----------------
mR, mL = bpy.data.objects["CG_ArmR"], bpy.data.objects["CG_ArmL"]
# fit on the wrist and palm only: the left fingers are malformed in the source mesh
_rh0, _lh0 = MW @ arm.data.bones["Right_Hand"].head_local, MW @ arm.data.bones["Left_Hand"].head_local
R = [p for p in hand_verts(mR, "Right") if (p - _rh0).length < 0.04]
L = [p for p in hand_verts(mL, "Left") if (p - _lh0).length < 0.04]
mirror = Matrix.Diagonal((-1.0, 1.0, 1.0))
Rm = [mirror @ p for p in R]
bones = arm.data.bones
def wpos(name, tail=False):
    b = bones[name]; return MW @ (b.tail_local if tail else b.head_local)
# initial guess: mirrored right hand bone onto the left hand bone
rh, rt = mirror @ wpos("Right_Hand"), mirror @ wpos("Right_Hand", True)
lh, lt = wpos("Left_Hand"), wpos("Left_Hand", True)
rot0 = (rt - rh).rotation_difference(lt - lh).to_matrix()
Rot, T = rot0, lh - rot0 @ rh
kd = kdtree.KDTree(len(L))
for i, p in enumerate(L): kd.insert(p, i)
kd.balance()
for it in range(40):
    cur = [Rot @ p + T for p in Rm]
    pairs = [(p0, kd.find(c)[0]) for p0, c in zip(Rm, cur)]
    dists = sorted((Rot @ p + T - q).length for p, q in pairs)
    cut = dists[int(len(dists) * 0.9)]
    sel = [(p, q) for p, q in pairs if (Rot @ p + T - q).length <= cut]
    Rot, T = best_rigid([p for p, _ in sel], [q for _, q in sel])
rms = math.sqrt(sum((Rot @ p + T - kd.find(Rot @ p + T)[0]).length_squared for p in Rm) / len(Rm))
print(f"ICP left-hand fit rms {rms * 1000:.2f} mm over {len(Rm)} verts")
# the mirrored hand must pivot exactly on the left wrist: compare with the frame of the Hand bones themselves
_R3 = (MW @ bones["Right_Hand"].matrix_local).to_3x3(); _L3 = (MW @ bones["Left_Hand"].matrix_local).to_3x3()
_m3 = Matrix(((-1, 0, 0), (0, 1, 0), (0, 0, 1)))
_Mx = Matrix.Identity(3)
for i in range(3): _Mx.col[i] = _m3 @ _R3.col[i]
_Mx.col[0] = -_Mx.col[0]                      # keep the mirrored frame right-handed
RotB = _L3.normalized() @ _Mx.normalized().inverted()
ang = math.degrees((RotB @ Rot.inverted()).to_quaternion().angle)
off = (Rot @ (mirror @ (MW @ bones["Right_Hand"].head_local)) + T - MW @ bones["Left_Hand"].head_local).length
print(f"wrist fit vs bone frames: {ang:.1f} deg apart, fitted wrist {off * 1000:.1f} mm off the left wrist pivot")
Rot = RotB if ang < 25.0 else Rot
T = MW @ bones["Left_Hand"].head_local - Rot @ (mirror @ (MW @ bones["Right_Hand"].head_local))
fit = lambda p: Rot @ (mirror @ p) + T

bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='EDIT')
eb = arm.data.edit_bones
inv = MW.inverted()
for f in FINGERS:
    for j in JOINTS + ["DistalEnd"]:
        src, dst = eb[f"Right_{f}{j}"], eb[f"Left_{f}{j}"]
        dst.use_connect = False
        dst.head = inv @ fit(MW @ src.head)
        dst.tail = inv @ fit(MW @ src.tail)
        # roll: mirror the right bone's z axis
        z = fit(MW @ (src.head + src.z_axis * 0.01)) - fit(MW @ src.head)
        dst.align_roll(MW.inverted().to_3x3() @ z)
bpy.ops.object.mode_set(mode='OBJECT')


# ---------------- 2. finger weights ----------------
def seg_dist(p, a, b):
    ab = b - a; t = max(0.0, min(1.0, (p - a).dot(ab) / max(ab.length_squared, 1e-12)))
    return (p - (a + ab * t)).length, t


def reweight(mesh, side):
    vg = mesh.vertex_groups
    hand = vg[f"{side}_Hand"]
    for f in FINGERS:
        for j in JOINTS:
            if f"{side}_{f}{j}" not in vg: vg.new(name=f"{side}_{f}{j}")
    segs = {}
    for f in FINGERS:
        for j in JOINTS:
            n = f"{side}_{f}{j}"; b = bones[n]
            segs[n] = (MW @ b.head_local, MW @ b.tail_local)
    hb = bones[f"{side}_Hand"]
    hseg = (MW @ hb.head_local, MW @ hb.tail_local)
    moved = 0
    for v in mesh.data.vertices:
        wh = next((x.weight for x in v.groups if x.group == hand.index), 0.0)
        if wh <= 0.0: continue
        p = mesh.matrix_world @ v.co
        # nearest finger chain
        best, bestd = None, 1e9
        for f in FINGERS:
            d = min(seg_dist(p, *segs[f"{side}_{f}{j}"])[0] for j in JOINTS)
            if d < bestd: bestd, best = d, f
        dh = seg_dist(p, *hseg)[0]
        # only vertices clearly on a finger leave the hand bone (palm and wrist stay rigid)
        prox_head = segs[f"{side}_{best}Proximal"][0]
        prox_dir = (segs[f"{side}_{best}Proximal"][1] - prox_head).normalized()
        along = (p - prox_head).dot(prox_dir)           # metres past the knuckle
        share_f = max(0.0, min(1.0, (along + 0.004) / 0.010))  # blend in over the knuckle
        if bestd > 0.02: share_f = 0.0
        if share_f <= 0.0: continue
        ws = {}
        for j in JOINTS:
            n = f"{side}_{best}{j}"
            d, t = seg_dist(p, *segs[n])
            ws[n] = 1.0 / (max(d, 0.001) ** 4)
        tot = sum(ws.values())
        hand.add([v.index], wh * (1.0 - share_f), 'REPLACE')
        for n, w in ws.items():
            if w / tot * share_f > 0.002:
                vg[n].add([v.index], wh * share_f * w / tot, 'REPLACE')
        moved += 1
    print(f"{side}: {moved} vertices weighted to fingers")


reweight(mR, "Right")


# ---------------- 2b. replace the broken left hand by the mirrored right hand ----------------
import bmesh
HAND_CUT = 0.15     # vertices at least this much on the Hand bone form the hand (the old left hand goes entirely)


def hand_weight(mesh, v, side):
    """Weight on the Hand bone and its fingers."""
    names = {f"{side}_Hand"} | {f"{side}_{f}{j}" for f in FINGERS for j in JOINTS}
    return sum(g.weight for g in v.groups if mesh.vertex_groups[g.group].name in names)


COPY_CUT = 0.5          # the copied hand is cut at the skin, so the stitched wrist carries skin colour
STITCH_RADIUS = 0.045   # forearm opening: open edges this close to the left wrist pivot


def cut_hand(mesh, side, keep_hand, cut):
    """Deletes the hand faces (keep_hand False) or everything but them (True)."""
    hv = {v.index for v in mesh.data.vertices if hand_weight(mesh, v, side) >= cut}
    bm = bmesh.new(); bm.from_mesh(mesh.data)
    bm.faces.ensure_lookup_table()
    doomed = [f for f in bm.faces if all(v.index in hv for v in f.verts) != keep_hand]
    bmesh.ops.delete(bm, geom=doomed, context='FACES')
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context='VERTS')
    bm.to_mesh(mesh.data); bm.free(); mesh.data.update()


nL0 = len(mL.data.vertices)
cut_hand(mL, "Left", keep_hand=False, cut=HAND_CUT)
copy = mR.copy(); copy.data = mR.data.copy(); copy.name = "LeftHandCopy"
bpy.context.scene.collection.objects.link(copy)
cut_hand(copy, "Right", keep_hand=True, cut=COPY_CUT)
# world-space map: mirror, then the wrist fit; baked into the vertices
Mfit = Matrix.Translation(T) @ Rot.to_4x4() @ mirror.to_4x4()
copy.data.transform(mL.matrix_world.inverted() @ Mfit @ copy.matrix_world)
copy.matrix_world = mL.matrix_world
copy.data.flip_normals()                     # the mirror reverses the winding
for g in copy.vertex_groups:
    if g.name.startswith("Right_"): g.name = "Left_" + g.name[6:]
print(f"left hand: {nL0} -> {len(mL.data.vertices)} forearm verts + {len(copy.data.vertices)} mirrored hand verts")
bpy.ops.object.select_all(action='DESELECT')
copy.select_set(True); mL.select_set(True)
bpy.context.view_layer.objects.active = mL
tag = copy.vertex_groups.new(name="FP_HandCopy")
tag.add(list(range(len(copy.data.vertices))), 1.0, 'REPLACE')
bpy.ops.object.join()
mL = bpy.data.objects["CG_ArmL"]

# stitch: the hand's open wrist edge is extruded onto the forearm's opening (those new vertices take the
# forearm's weights, so the skin stretches over the wrist when it bends) - no gap whatever the fit
bm = bmesh.new(); bm.from_mesh(mL.data)
dl = bm.verts.layers.deform.verify()
ti = mL.vertex_groups["FP_HandCopy"].index
pivot = mL.matrix_world.inverted() @ (MW @ arm.data.bones["Left_Hand"].head_local)
open_e = [e for e in bm.edges if len(e.link_faces) == 1]
hand_ring = [e for e in open_e if all(ti in v[dl] for v in e.verts)]
arm_ring = {v for e in open_e if not any(ti in v[dl] for v in e.verts) for v in e.verts if (v.co - pivot).length < STITCH_RADIUS}
kd2 = kdtree.KDTree(len(arm_ring))
arm_list = list(arm_ring)
for i, v in enumerate(arm_list): kd2.insert(v.co, i)
kd2.balance()
ext = bmesh.ops.extrude_edge_only(bm, edges=hand_ring)
for v in [g for g in ext["geom"] if isinstance(g, bmesh.types.BMVert)]:
    co, i, d = kd2.find(v.co)
    src = arm_list[i]
    v.co = co
    v[dl].clear()
    for k, w in src[dl].items(): v[dl][k] = w
for v in bm.verts:
    if ti in v[dl]: del v[dl][ti]
bm.to_mesh(mL.data); bm.free(); mL.data.update()
mL.vertex_groups.remove(mL.vertex_groups["FP_HandCopy"])
print(f"wrist stitched: {len(hand_ring)} hand edges onto {len(arm_ring)} forearm opening verts")


# ---------------- optional preview: curl all fingers, render both hands ----------------
if PREVIEW:
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='POSE')
    for side in ("Right", "Left"):
        for f in FINGERS[1:]:
            for j in JOINTS:
                pb = arm.pose.bones[f"{side}_{f}{j}"]
                pb.rotation_mode = 'XYZ'
                pb.rotation_euler = (math.radians(70), 0, 0)
    bpy.ops.object.mode_set(mode='OBJECT')
    scn = bpy.context.scene
    scn.render.engine = 'BLENDER_EEVEE'
    scn.render.resolution_x, scn.render.resolution_y = 1200, 600
    w = bpy.data.worlds.new("w"); w.color = (0.5, 0.5, 0.5); scn.world = w
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", 'SUN')); scn.collection.objects.link(sun)
    sun.rotation_euler = (0.8, 0.2, 0.6); sun.data.energy = 3
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); scn.collection.objects.link(cam); scn.camera = cam
    cam.data.type = 'ORTHO'; cam.data.ortho_scale = 0.85
    cam.location = (-0.03, -1.5, 0.80); cam.rotation_euler = (math.radians(90), 0, 0)
    scn.render.filepath = PREVIEW
    bpy.ops.render.render(write_still=True)
    bpy.ops.object.mode_set(mode='POSE')
    for pb in arm.pose.bones: pb.rotation_euler = (0, 0, 0); pb.rotation_quaternion = (1, 0, 0, 0)
    bpy.ops.object.mode_set(mode='OBJECT')

# ---------------- 3. export ----------------
for o in bpy.data.objects: o.select_set(o.type in {'ARMATURE', 'MESH'})
bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(filepath=DST, use_selection=True, object_types={'ARMATURE', 'MESH'},
    apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
    bake_space_transform=True, add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X',
    armature_nodetype='NULL', bake_anim=False, mesh_smooth_type='FACE', use_tspace=True,
    path_mode='STRIP', embed_textures=False)
print("EXPORTED", DST)
