# Tripo walk clip -> faces forward (-Y in Blender = +Z in Unity), walks in place, loops. Exports skeleton + animation only.
import bpy, sys, math
from mathutils import Matrix, Vector
src,out=sys.argv[sys.argv.index("--")+1:][:2]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
arm=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]
sc=bpy.context.scene; act=arm.animation_data.action
f0,f1=int(act.frame_range[0]),int(act.frame_range[1]); sc.frame_start,sc.frame_end=f0,f1
hb=arm.pose.bones["Hips"]
# travel direction -> yaw needed to face -Y
sc.frame_set(f0); a=arm.matrix_world@hb.head; sc.frame_set(f1); b=arm.matrix_world@hb.head
travel=(b-a); travel.z=0
yaw=math.atan2(travel.x,-travel.y)          # angle from -Y to travel dir
R=Matrix.Rotation(-yaw,4,'Z')
print("TRAVEL",tuple(round(x,3) for x in travel),"rotating by",round(math.degrees(-yaw),1),"deg")
mats={}
for f in range(f0,f1+1):
    sc.frame_set(f); mats[f]=R@(arm.matrix_world@hb.matrix)
drift=mats[f1].translation-mats[f0].translation; drift.z=0
for f in range(f0,f1+1):
    m=mats[f].copy(); t=(f-f0)/(f1-f0); m.translation-=drift*t
    # keep the hips centred over the start position
    sc.frame_set(f); hb.matrix=arm.matrix_world.inverted()@m
    hb.keyframe_insert("location",frame=f); hb.keyframe_insert("rotation_quaternion" if hb.rotation_mode=='QUATERNION' else "rotation_euler",frame=f)
# loop check
def pose(f):
    sc.frame_set(f); return {b.name:(arm.matrix_world@b.head).copy() for b in arm.pose.bones}
p0,p1=pose(f0),pose(f1); err=max((p0[n]-p1[n]).length for n in p0)
sc.frame_set(f0); h0=arm.matrix_world@hb.head; sc.frame_set((f0+f1)//2); hm=arm.matrix_world@hb.head
print("LOOP max bone gap first/last frame %.4f m; hips start %s mid %s"%(err,tuple(round(x,3) for x in h0),tuple(round(x,3) for x in hm)))
# cut to the cleanest loop (one full step cycle, at least 16 frames long)
names=[b.name for b in arm.pose.bones if not any(k in b.name for k in ("Thumb","Index","Middle","Ring","Pinky","Eye","ToesEnd"))]
P={}
for f in range(f0,f1+1):
    sc.frame_set(f); P[f]={n:(arm.matrix_world@arm.pose.bones[n].head).copy() for n in names}
gap,la,lb=min((max((P[x][n]-P[y][n]).length for n in names),x,y) for x in range(f0,f1+1) for y in range(x+16,f1+1))
sc.frame_start,sc.frame_end=la,lb
print("LOOPCUT frames %d-%d (%d frames, %.2f s), seam gap %.3f m"%(la,lb,lb-la,(lb-la)/sc.render.fps,gap))
act.name="WalkRifle"
for o in list(bpy.data.objects):
    if o is not arm: bpy.data.objects.remove(o,do_unlink=True)
for a2 in list(bpy.data.actions):
    if a2 is not act: bpy.data.actions.remove(a2)
arm.select_set(True); bpy.context.view_layer.objects.active=arm
bpy.ops.export_scene.fbx(filepath=out,use_selection=True,object_types={'ARMATURE'},add_leaf_bones=False,
    apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',bake_space_transform=True,armature_nodetype='NULL',
    bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_force_startend_keying=True,bake_anim_simplify_factor=0.0)
print("EXPORTED",out)
