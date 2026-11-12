# Replace the (incomplete) right leg parts with a mirrored copy of the left leg parts.
import bpy, sys, collections
from mathutils import Matrix, Vector
a=sys.argv[sys.argv.index("--")+1:]
bpy.ops.wm.open_mainfile(filepath=a[0])
arm=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]
H=lambda n: arm.matrix_world@arm.data.bones[n].head_local
c=(H("Left_UpperLeg").x+H("Right_UpperLeg").x)/2
print("MIRROR plane x =",round(c,4))
LEG=("UpperLeg","LowerLeg","Foot","Toes","ToesEnd")
def side_of(o):
    gn={g.index:g.name for g in o.vertex_groups}; tot=collections.Counter()
    for v in o.data.vertices:
        for g in v.groups:
            if g.weight>0: tot[gn[g.group]]+=g.weight
    if not tot: return None
    names=set(tot)
    if all(n.startswith("Left_") and n[5:] in LEG for n in names): return "L"
    if all(n.startswith("Right_") and n[6:] in LEG for n in names): return "R"
    return None
parts=[o for o in bpy.data.objects if o.type=='MESH' and o.name.startswith("tripo_part_")]
left=[o for o in parts if side_of(o)=="L"]; right=[o for o in parts if side_of(o)=="R"]
print("LEFT leg parts",sorted(o.name[11:] for o in left)); print("RIGHT leg parts (removed)",sorted(o.name[11:] for o in right))
for o in right: bpy.data.objects.remove(o,do_unlink=True)
M=Matrix.Translation((c,0,0))@Matrix.Scale(-1,4,(1,0,0))@Matrix.Translation((-c,0,0))
bpy.ops.object.select_all(action='DESELECT')
new=[]
for o in left:
    n=o.copy(); n.data=o.data.copy(); n.name=o.name+"_R"
    for col in o.users_collection: col.objects.link(n)
    n.matrix_world=M@o.matrix_world
    new.append(n)
for n in new: n.select_set(True)
bpy.context.view_layer.objects.active=new[0]
bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)   # bakes the mirror, fixes normals
for n in new:
    gn={g.index:g.name for g in n.vertex_groups}
    w=[{gn[g.group]:g.weight for g in v.groups if g.weight>0} for v in n.data.vertices]
    n.vertex_groups.clear()
    for i,d in enumerate(w):
        for name,x in d.items():
            name="Right_"+name[5:] if name.startswith("Left_") else name
            g=n.vertex_groups.get(name) or n.vertex_groups.new(name=name)
            g.add([i],x,'REPLACE')
    n.parent=arm; n.matrix_parent_inverse=arm.matrix_world.inverted()
    for m in n.modifiers:
        if m.type=='ARMATURE': m.object=arm
print("MIRRORED",len(new))
bpy.ops.wm.save_as_mainfile(filepath=a[1])
