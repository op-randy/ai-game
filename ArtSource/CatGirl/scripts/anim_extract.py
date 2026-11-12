# Extract selected Tripo clips: face forward, in place, trimmed/looped; one FBX per clip (skeleton + animation).
import bpy, sys, math
from mathutils import Vector, Matrix
src,outdir=sys.argv[sys.argv.index("--")+1:][:2]
JOBS=[  # (tripo action, output name, mode, range, how to find "forward")
 ("wait.001","Idle","loop",None,"avg"),("walk.001","Walk","loop",None,"avg"),("run.001","Run","loop",None,"avg"),
 ("box_01.001","Punch1","range",(5,34),"reach:Left_Hand,Right_Hand"),("box_03.001","Punch2","range",(8,42),"reach:Left_Hand,Right_Hand"),
 ("front_kick_01.001","Kick","range",(13,46),"reach:Left_Foot,Right_Foot"),("climb.001","Climb","range",None,"start")]
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.fbx(filepath=src)
arm=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]; sc=bpy.context.scene; P=arm.pose.bones; hb=P["Hips"]
for o in list(bpy.data.objects):
    if o is not arm: bpy.data.objects.remove(o,do_unlink=True)
W=lambda n: arm.matrix_world@P[n].head
def facing():
    lat=W("Left_UpperLeg")-W("Right_UpperLeg"); f=lat.cross(Vector((0,0,1))); return math.atan2(f.x,-f.y)
for act_name,out,mode,rng,how in JOBS:
    a=bpy.data.actions["Armature|"+act_name].copy(); a.name=out
    arm.animation_data.action=a
    f0,f1=int(a.frame_range[0]),int(a.frame_range[1])
    ra,rb=rng if rng else (f0,f1)
    if how=="start":
        sc.frame_set(ra); yaw=facing()
    elif how=="avg":          # average body direction over the clip (vector mean, robust to hip twist)
        acc=Vector((0,0,0))
        for f in range(ra,rb+1):
            sc.frame_set(f); lat=W("Left_UpperLeg")-W("Right_UpperLeg")+W("Left_UpperArm")-W("Right_UpperArm"); acc+=lat.cross(Vector((0,0,1))).normalized()
        yaw=math.atan2(acc.x,-acc.y)
    else:                     # direction of the strike: where the hand/foot reaches farthest from the hips
        effs=how.split(":")[1].split(","); best=None
        for f in range(ra,rb+1):
            sc.frame_set(f); h=W("Hips")
            for e in effs:
                d=W(e)-h; d.z=0
                if best is None or d.length>best[0]: best=(d.length,d.copy(),f,e)
        yaw=math.atan2(best[1].x,-best[1].y)
        print(f"   strike of {out}: {best[3]} at frame {best[2]}, reach {best[0]:.2f}")
    R=Matrix.Rotation(-yaw,4,'Z')
    mats={}
    for f in range(f0,f1+1): sc.frame_set(f); mats[f]=R@(arm.matrix_world@hb.matrix)
    if mode=="loop":
        drift=mats[f1].translation-mats[f0].translation; drift.z=0
    else: drift=Vector((0,0,0))
    base=mats[f0].translation.copy(); base.z=0
    for f in range(f0,f1+1):
        m=mats[f].copy(); m.translation-=drift*((f-f0)/(f1-f0)) + Vector((base.x,base.y,0))
        sc.frame_set(f); hb.matrix=arm.matrix_world.inverted()@m
        hb.keyframe_insert("location",frame=f); hb.keyframe_insert("rotation_quaternion" if hb.rotation_mode=='QUATERNION' else "rotation_euler",frame=f)
    if mode=="loop":
        names=[b.name for b in P if not any(k in b.name for k in ("Thumb","Index","Middle","Ring","Pinky","Eye","ToesEnd"))]
        Q={}
        for f in range(f0,f1+1): sc.frame_set(f); Q[f]={n:W(n).copy() for n in names}
        minlen=max(12,int((f1-f0)*0.5))
        gap,la,lb=min((max((Q[x][n]-Q[y][n]).length for n in names),x,y) for x in range(f0,f1+1) for y in range(x+minlen,f1+1))
    else:
        la,lb=rng if rng else (f0,f1); gap=0
    sc.frame_start,sc.frame_end=la,lb
    info=f"CLIP {out:7s} from {act_name:18s} frames {la}-{lb} ({(lb-la)/sc.render.fps:.2f}s) rotated {math.degrees(-yaw):+.0f} deg ({how.split(':')[0]})"
    if mode=="loop": info+=f" loop seam {gap:.3f}m"
    if out in ("Walk","Run"):
        # ground speed: horizontal speed of the lower (planted) foot relative to the hips
        sp=[]
        for f in range(la,lb):
            sc.frame_set(f); l1,r1,h1=W("Left_Foot"),W("Right_Foot"),W("Hips"); sc.frame_set(f+1); l2,r2,h2=W("Left_Foot"),W("Right_Foot"),W("Hips")
            foot=(l1,l2) if l1.z<r1.z else (r1,r2)
            d=(foot[1]-foot[0])-(h2-h1); d.z=0; sp.append(d.length*sc.render.fps)
        sp.sort(); info+=f" ground speed {sp[len(sp)//2]:.3f} model-m/s"
    if out=="Climb":
        zs=[]
        for f in range(la,lb+1): sc.frame_set(f); zs.append(W("Hips").z)
        z0,z1=zs[0],zs[-1]; curve=[round((zs[int(i*(len(zs)-1)/16)]-z0)/(z1-z0),3) for i in range(17)]
        info+=f" rise {z1-z0:.3f} model-m curve {curve}"
    print(info)
    arm.select_set(True); bpy.context.view_layer.objects.active=arm
    bpy.ops.export_scene.fbx(filepath=f"{outdir}/CatGirl_{out}.fbx",use_selection=True,object_types={'ARMATURE'},add_leaf_bones=False,
        apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',bake_space_transform=True,armature_nodetype='NULL',
        bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_force_startend_keying=True,bake_anim_simplify_factor=0.0)
print("DONE")
