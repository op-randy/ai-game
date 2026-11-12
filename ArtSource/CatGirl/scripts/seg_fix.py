# Fix Tripo segmented rig: move raised left arm bones, reweight parts, optionally T-pose and apply as rest.
import bpy, sys, collections
from mathutils import Vector, Matrix
from mathutils.geometry import intersect_point_line
a=sys.argv[sys.argv.index("--")+1:]
fbx, out_fixed, out_tpose, out_fbx = a
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx)
arm=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]
parts={o.name.replace("tripo_part_",""):o for o in bpy.data.objects if o.type=='MESH'}
print("parts",len(parts))

# ---------- 1. move left arm bones onto the real arm ----------
E=Vector((0.095,-0.120,0.703)); W=Vector((0.064,-0.086,0.815)); HT=Vector((0.045,-0.068,0.857))
bpy.context.view_layer.objects.active=arm; arm.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
eb=arm.data.edit_bones
def place(b,h,t):
    oz=b.z_axis.copy(); R=(b.tail-b.head).normalized().rotation_difference((t-h).normalized()).to_matrix()
    b.head=h; b.tail=t; b.align_roll(R@oz)
ua,la,hd=eb["Left_UpperArm"],eb["Left_LowerArm"],eb["Left_Hand"]
fingers=list(hd.children_recursive)
old_hand=hd.matrix.copy(); old_len=hd.length
old_f={b.name:(b.head.copy(),b.tail.copy(),b.z_axis.copy()) for b in fingers}
place(ua,ua.head.copy(),E); place(la,E,W); place(hd,W,HT)
T=Matrix.Translation((HT-W).normalized()*(hd.length-old_len))@hd.matrix@old_hand.inverted()
for b in fingers:
    h,t,z=old_f[b.name]; b.use_connect=False; b.head=T@h; b.tail=T@t; b.align_roll(T.to_3x3()@z)
for b in fingers:
    if b.parent in fingers or b.parent==hd: pass
bpy.ops.object.mode_set(mode='OBJECT')
B={b.name:(arm.matrix_world@b.head_local, arm.matrix_world@b.tail_local) for b in arm.data.bones}
def segd(p,n):
    h,t=B[n]; q,f=intersect_point_line(p,h,t); f=max(0,min(1,f)); return (p-(h+(t-h)*f)).length

# ---------- 2. weights ----------
def setw(o,vi,new):
    vg={g.name:g for g in o.vertex_groups}
    for g in list(o.data.vertices[vi].groups):
        o.vertex_groups[g.group].remove([vi])
    s=sum(new.values())
    for n,x in sorted(new.items(),key=lambda kv:-kv[1])[:4]:
        if n not in vg: vg[n]=o.vertex_groups.new(name=n)
        vg[n].add([vi],x/s,'REPLACE')
def rigid(pid,bone):
    o=parts[pid]
    for v in o.data.vertices: setw(o,v.index,{bone:1.0})
def blend(pid,bones,power=2):
    o=parts[pid]
    for v in o.data.vertices:
        p=o.matrix_world@v.co
        setw(o,v.index,{n:1/max(segd(p,n),0.01)**power for n in bones})
TORSO=["UpperChest","Chest","Spine","Hips"]
rigid("16","Left_Hand"); rigid("32","Left_LowerArm"); rigid("39","Left_LowerArm"); rigid("30","Left_UpperArm")
rigid("0","Head"); rigid("1","Head"); rigid("8","Hips")
for c in ["2","3","4","5","6"]: blend(c,TORSO,2)
# generic: strip arm/hand weights that sit far from their bone on non-arm parts
ARM_PARTS={"16","32","39","30","34","10","17","36","18","38"}
def is_armbone(n):
    return any(n.startswith(s+"_") for s in ("Left","Right")) and any(k in n for k in ("UpperArm","LowerArm","Hand","Thumb","Index","Middle","Ring","Pinky"))
BODY=["Head","Neck","Neck_Twist_A","UpperChest","Chest","Spine","Hips","Left_Shoulder","Right_Shoulder"]
moved=collections.Counter()
for pid,o in parts.items():
    if pid in ARM_PARTS or pid in {"0","1","2","3","4","5","6","8"}: continue
    gn={g.index:g.name for g in o.vertex_groups}
    for v in o.data.vertices:
        w={gn[g.group]:g.weight for g in v.groups if g.weight>0}
        p=o.matrix_world@v.co
        bad={n:x for n,x in w.items() if is_armbone(n) and ("UpperArm" not in n or segd(p,n)>0.045)}
        if not bad: continue
        new={n:x for n,x in w.items() if n not in bad}
        inv={n:1/max(segd(p,n),0.01)**4 for n in BODY}; si=sum(inv.values())
        for n,x in inv.items(): new[n]=new.get(n,0)+sum(bad.values())*x/si
        setw(o,v.index,new); moved[pid]+=1
print("stray arm weights moved:",dict(moved))
# ---------- rigid parts: each limb piece follows ONE bone ----------
TORSO_SET={"Hips","Spine","Chest","UpperChest","Neck","Neck_Twist_A","Head","Left_Shoulder","Right_Shoulder"}
SKIP={"0","1","2","3","4","5","6","8","16","32","39","30"}   # already handled above
rig=collections.Counter()
for pid,o in parts.items():
    if pid in SKIP: continue
    gn={g.index:g.name for g in o.vertex_groups}
    tot=collections.Counter()
    for v in o.data.vertices:
        for g in v.groups: tot[gn[g.group]]+=g.weight
    if pid not in ARM_PARTS:
        for n in list(tot):
            if is_armbone(n): del tot[n]
    if not tot: continue
    T=sum(tot.values())
    cands=[n for n,x in tot.items() if x>=0.15*T] or [tot.most_common(1)[0][0]]
    if all(n in TORSO_SET for n in cands):
        # torso piece: keep smooth spine/shoulder blend, drop everything else
        for v in o.data.vertices:
            w={gn[g.group]:g.weight for g in v.groups if gn[g.group] in TORSO_SET and g.weight>0}
            if not w: w={max(cands,key=lambda n:tot[n]):1.0}
            setw(o,v.index,w)
        rig["torso_blend"]+=1
    else:
        c=sum((o.matrix_world@v.co for v in o.data.vertices),Vector())/len(o.data.vertices)
        bone=min(cands,key=lambda n:segd(c,n))
        for v in o.data.vertices: setw(o,v.index,{bone:1.0})
        rig[bone]+=1
print("rigid assignment:",dict(rig))
bpy.ops.wm.save_as_mainfile(filepath=out_fixed)

# ---------- 3. T-pose ----------
bpy.ops.object.select_all(action='DESELECT')
bpy.context.view_layer.objects.active=arm; arm.select_set(True)
bpy.ops.object.mode_set(mode='POSE')
P=arm.pose.bones
def aim(name,target):
    bpy.context.view_layer.update()
    pb=P[name]; M=pb.matrix.copy()
    cur=(M.to_3x3()@Vector((0,1,0))).normalized()
    R=cur.rotation_difference(Vector(target).normalized()).to_matrix().to_4x4()
    h=M.translation
    pb.matrix=Matrix.Translation(h)@R@Matrix.Translation(-h)@M
for side,sx in (("Left",1),("Right",-1)):
    for b in ("UpperArm","LowerArm","Hand"): aim(f"{side}_{b}",(sx,0,0))
    aim(f"{side}_UpperLeg",(0,0,-1)); aim(f"{side}_LowerLeg",(0,0,-1)); aim(f"{side}_Foot",(0,-0.4,-0.92))
    if f"{side}_Toes" in P: aim(f"{side}_Toes",(0,-1,0))
bpy.context.view_layer.update()
bpy.ops.object.mode_set(mode='OBJECT')
# bake pose into meshes, then make it the rest pose
for o in parts.values():
    bpy.context.view_layer.objects.active=o
    mods=[m for m in o.modifiers if m.type=='ARMATURE']
    for m in mods: bpy.ops.object.modifier_apply(modifier=m.name)
    m=o.modifiers.new("Armature",'ARMATURE'); m.object=arm
bpy.context.view_layer.objects.active=arm
bpy.ops.object.mode_set(mode='POSE'); bpy.ops.pose.armature_apply(selected=False); bpy.ops.object.mode_set(mode='OBJECT')
# put feet on the ground
minz=min((o.matrix_world@v.co).z for o in parts.values() for v in o.data.vertices)
arm.location.z-=minz
bpy.ops.object.select_all(action='DESELECT'); arm.select_set(True)
for o in parts.values(): o.select_set(True)
bpy.context.view_layer.objects.active=arm
bpy.ops.object.transform_apply(location=True,rotation=False,scale=False)
print("grounded by",round(-minz,4))
bpy.ops.wm.save_as_mainfile(filepath=out_tpose)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=out_fbx,use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,
    bake_anim=False,path_mode='COPY',embed_textures=True,armature_nodetype='NULL')
print("done")
