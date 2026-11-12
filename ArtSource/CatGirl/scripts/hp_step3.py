# Same T-pose as the segmented model (seg_fix.py), applied as rest pose, feet on the ground.
import bpy, sys
from mathutils import Vector, Matrix
a=sys.argv[sys.argv.index("--")+1:]
bpy.ops.wm.open_mainfile(filepath=a[0]); out=a[1]
arm=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]
meshes=[o for o in bpy.data.objects if o.type=='MESH']
bpy.ops.object.select_all(action='DESELECT'); bpy.context.view_layer.objects.active=arm; arm.select_set(True)
bpy.ops.object.mode_set(mode='POSE')
P=arm.pose.bones
def aim(name,target):
    bpy.context.view_layer.update()
    pb=P[name]; M=pb.matrix.copy()
    cur=(M.to_3x3()@Vector((0,1,0))).normalized()
    R=cur.rotation_difference(Vector(target).normalized()).to_matrix().to_4x4()
    h=M.translation; pb.matrix=Matrix.Translation(h)@R@Matrix.Translation(-h)@M
for side,sx in (("Left",1),("Right",-1)):
    for b in ("UpperArm","LowerArm","Hand"): aim(f"{side}_{b}",(sx,0,0))
    aim(f"{side}_UpperLeg",(0,0,-1)); aim(f"{side}_LowerLeg",(0,0,-1)); aim(f"{side}_Foot",(0,-0.4,-0.92))
    if f"{side}_Toes" in P: aim(f"{side}_Toes",(0,-1,0))
bpy.context.view_layer.update(); bpy.ops.object.mode_set(mode='OBJECT')
for o in meshes:
    bpy.context.view_layer.objects.active=o
    for m in [m for m in o.modifiers if m.type=='ARMATURE']: bpy.ops.object.modifier_apply(modifier=m.name)
    m=o.modifiers.new("Armature",'ARMATURE'); m.object=arm
bpy.context.view_layer.objects.active=arm
bpy.ops.object.mode_set(mode='POSE'); bpy.ops.pose.armature_apply(selected=False); bpy.ops.object.mode_set(mode='OBJECT')
minz=min((o.matrix_world@v.co).z for o in meshes for v in o.data.vertices)
arm.location.z-=minz
bpy.ops.object.select_all(action='DESELECT'); arm.select_set(True)
for o in meshes: o.select_set(True)
bpy.context.view_layer.objects.active=arm
bpy.ops.object.transform_apply(location=True,rotation=False,scale=False)
arm.data.display_type='STICK'
print("TPOSE done, grounded by",round(-minz,4))
bpy.ops.wm.save_as_mainfile(filepath=out)
