# Unrigged, single-object T-pose export for re-rigging/animating in Tripo.
import bpy, sys
a=sys.argv[sys.argv.index("--")+1:]
src,out_fbx,out_glb=a
bpy.ops.wm.open_mainfile(filepath=src)
arm=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]
meshes=[o for o in bpy.data.objects if o.type=='MESH']
for o in meshes:
    mw=o.matrix_world.copy()
    for m in list(o.modifiers):
        if m.type=='ARMATURE': o.modifiers.remove(m)     # rest pose == T-pose, nothing to bake
    o.parent=None; o.matrix_world=mw
    o.vertex_groups.clear()
bpy.data.objects.remove(arm,do_unlink=True)
for o in list(bpy.data.objects):
    if o.type not in {'MESH'}: bpy.data.objects.remove(o,do_unlink=True)
bpy.ops.object.select_all(action='DESELECT')
for o in meshes: o.select_set(True)
bpy.context.view_layer.objects.active=meshes[0]
bpy.ops.object.join()
ob=bpy.context.view_layer.objects.active; ob.name="CatGirl"; ob.data.name="CatGirl"
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
zs=[v.co.z for v in ob.data.vertices]
print("JOINED verts",len(ob.data.vertices),"faces",len(ob.data.polygons),"materials",len(ob.data.materials),"height",round(max(zs)-min(zs),3),"minz",round(min(zs),4))
bpy.ops.export_scene.fbx(filepath=out_fbx,use_selection=True,object_types={'MESH'},apply_unit_scale=True,
    apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',bake_space_transform=True,
    mesh_smooth_type='FACE',path_mode='COPY',embed_textures=True,bake_anim=False)
bpy.ops.export_scene.gltf(filepath=out_glb,export_format='GLB',use_selection=True,export_skins=False,export_animations=False)
print("EXPORTED")
