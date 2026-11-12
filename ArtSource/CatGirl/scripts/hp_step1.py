# High-poly single mesh: decimate to a game budget, then transfer weights from the fixed segmented rig (original pose).
import bpy, sys, time
a=sys.argv[sys.argv.index("--")+1:]
hp_fbx, rig_blend, out, target_tris = a[0], a[1], a[2], int(a[3])
t0=time.time()
bpy.ops.wm.open_mainfile(filepath=rig_blend)                # fixed rig + segmented parts, ORIGINAL pose
arm=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]
parts=[o for o in bpy.data.objects if o.type=='MESH']
# weight source: all parts joined into one temporary mesh
bpy.ops.object.select_all(action='DESELECT')
src_objs=[]
for o in parts:
    c=o.copy(); c.data=o.data.copy(); bpy.context.scene.collection.objects.link(c); c.parent=None; c.matrix_world=o.matrix_world.copy()
    for m in list(c.modifiers): c.modifiers.remove(m)
    src_objs.append(c)
for c in src_objs: c.select_set(True)
bpy.context.view_layer.objects.active=src_objs[0]
bpy.ops.object.join(); src=bpy.context.view_layer.objects.active; src.name="WeightSource"
for o in parts: bpy.data.objects.remove(o,do_unlink=True)
print("source verts",len(src.data.vertices),"groups",len(src.vertex_groups),round(time.time()-t0))
# import high poly
before=set(bpy.data.objects)
bpy.ops.import_scene.fbx(filepath=hp_fbx)
hp=[o for o in bpy.data.objects if o not in before and o.type=='MESH'][0]; hp.name="CatGirlHP"
for o in [o for o in bpy.data.objects if o not in before and o is not hp]: bpy.data.objects.remove(o,do_unlink=True)
bpy.ops.object.select_all(action='DESELECT'); bpy.context.view_layer.objects.active=hp; hp.select_set(True)
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
n0=len(hp.data.polygons)
d=hp.modifiers.new("Decimate",'DECIMATE'); d.decimate_type='COLLAPSE'; d.ratio=target_tris/n0; d.use_collapse_triangulate=True
bpy.ops.object.modifier_apply(modifier=d.name)
print("decimated",n0,"->",len(hp.data.polygons),"tris",round(time.time()-t0))
# transfer weights
for g in src.vertex_groups: hp.vertex_groups.new(name=g.name)
m=hp.modifiers.new("WT",'DATA_TRANSFER'); m.object=src; m.use_vert_data=True; m.data_types_verts={'VGROUP_WEIGHTS'}
m.vert_mapping='POLYINTERP_NEAREST'; m.layers_vgroup_select_src='ALL'; m.layers_vgroup_select_dst='NAME'
bpy.ops.object.modifier_apply(modifier=m.name)
bpy.data.objects.remove(src,do_unlink=True)
hp.parent=arm; hp.matrix_parent_inverse=arm.matrix_world.inverted()
am=hp.modifiers.new("Armature",'ARMATURE'); am.object=arm
print("weights transferred",round(time.time()-t0))
bpy.ops.wm.save_as_mainfile(filepath=out)
