import bpy, sys
a=sys.argv[sys.argv.index("--")+1:]
bpy.ops.wm.open_mainfile(filepath=a[0])
arm=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]
arm.name="CatGirl_Rig"
for o in bpy.data.objects: o.select_set(o.type in {'ARMATURE','MESH'})
bpy.context.view_layer.objects.active=arm
bpy.ops.export_scene.fbx(filepath=a[1], use_selection=True, object_types={'ARMATURE','MESH'},
    apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
    bake_space_transform=True, add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X',
    armature_nodetype='NULL', bake_anim=False, mesh_smooth_type='FACE', use_tspace=True,
    path_mode='COPY', embed_textures=True)
print("EXPORTED")
