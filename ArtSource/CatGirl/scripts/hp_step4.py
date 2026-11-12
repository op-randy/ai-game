# Split the high-poly mesh into regions (head+hair, arms, body) without shading seams; export for Unity.
import bpy, sys, bmesh, collections
a=sys.argv[sys.argv.index("--")+1:]
bpy.ops.wm.open_mainfile(filepath=a[0]); out_blend=a[1]
arm=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]; hp=bpy.data.objects["CatGirlHP"]
FING=("Thumb","Index","Middle","Ring","Pinky")
def region(n):
    if n in ("Head","Neck_Twist_A","Left_Eye","Right_Eye"): return "CG_Head"
    for s,R in (("Left_","CG_ArmL"),("Right_","CG_ArmR")):
        if n.startswith(s) and (n[len(s):] in ("UpperArm","LowerArm","Hand") or any(f in n for f in FING)): return R
    return "CG_Body"
bpy.ops.object.select_all(action='DESELECT'); bpy.context.view_layer.objects.active=hp; hp.select_set(True)
# keep today's smooth normals across the cuts
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.customdata_custom_splitnormals_add()
bpy.ops.object.mode_set(mode='OBJECT')
counts=collections.Counter()
for name in ("CG_Head","CG_ArmL","CG_ArmR"):
    bpy.ops.object.select_all(action='DESELECT'); bpy.context.view_layer.objects.active=hp; hp.select_set(True)
    bm=bmesh.new(); bm.from_mesh(hp.data); n=0
    gn={g.index:g.name for g in hp.vertex_groups}; dl=bm.verts.layers.deform.active
    def vr(v):
        d=collections.Counter()
        for gi,w in v[dl].items(): d[region(gn[gi])]+=w
        return d.most_common(1)[0][0] if d else "CG_Body"
    for f in bm.faces:
        c=collections.Counter(vr(v) for v in f.verts); f.select=(c.most_common(1)[0][0]==name); n+=f.select
    bm.to_mesh(hp.data); bm.free()
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_mode(type='FACE')
    bpy.ops.mesh.separate(type='SELECTED'); bpy.ops.object.mode_set(mode='OBJECT')
    new=[o for o in bpy.context.selected_objects if o is not hp][0]; new.name=name; new.data.name=name
    counts[name]=n
hp.name="CG_Body"; hp.data.name="CG_Body"; counts["CG_Body"]=len(hp.data.polygons)
for o in bpy.data.objects:
    if o.type=='MESH':
        o.parent=arm
        if not any(m.type=='ARMATURE' for m in o.modifiers): m=o.modifiers.new("Armature",'ARMATURE'); m.object=arm
print("REGIONS",dict(counts))
bpy.ops.wm.save_as_mainfile(filepath=out_blend)
