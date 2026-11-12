# Rifle for Unity: barrel -> Unity +Z, origin on the sight line, child markers Muzzle and RedDot.
import bpy, sys, math
from mathutils import Vector, Matrix
src,out=sys.argv[sys.argv.index("--")+1:][:2]
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.fbx(filepath=src)
gun=[o for o in bpy.data.objects if o.type=='MESH'][0]; gun.name="AssaultRifle"
bpy.context.view_layer.objects.active=gun; gun.select_set(True)
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
AIM=Vector((-0.16,-0.005,0.317))       # rear of the holo sight, on the sight line
MUZZLE=Vector((0.50,0.0155,0.2395))
DOT=Vector((-0.08,-0.005,0.317))       # inside the sight window
M=Matrix.Rotation(math.radians(-90),4,'Z')@Matrix.Translation(-AIM)   # barrel +X -> -Y (Blender) = +Z (Unity)
gun.data.transform(M); gun.data.update()
GRIP_R=Vector((-0.235,0.015,0.135)); GRIP_L=Vector((0.171,0.015,0.12))
for name,p in (("Muzzle",MUZZLE),("RedDot",DOT),("GripRight",GRIP_R),("GripLeft",GRIP_L)):
    e=bpy.data.objects.new(name,None); bpy.context.scene.collection.objects.link(e)
    e.location=M@p; e.parent=gun
    print("MARKER",name,tuple(round(x,4) for x in e.location))
pts=[v.co for v in gun.data.vertices]
print("BBOX",tuple(round(min(q[i] for q in pts),3) for i in range(3)),tuple(round(max(q[i] for q in pts),3) for i in range(3)))
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=out,use_selection=True,object_types={'MESH','EMPTY'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',
    axis_forward='-Z',axis_up='Y',bake_space_transform=True,mesh_smooth_type='FACE',path_mode='COPY',embed_textures=True,bake_anim=False)
print("EXPORTED")
