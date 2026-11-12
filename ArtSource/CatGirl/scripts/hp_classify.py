# Mark cape and hair on the high-poly mesh using the segmented model (where they were separate parts), then split them off.
import bpy, sys, bmesh, collections
from mathutils.bvhtree import BVHTree
a=sys.argv[sys.argv.index("--")+1:]
hp_blend, seg_blend, out = a
bpy.ops.wm.open_mainfile(filepath=hp_blend)
CAPE={"2","3","4","5","6"}; HAIR={"0","1","44"}
with bpy.data.libraries.load(seg_blend) as (src, dst):
    dst.objects=[n for n in src.objects if n.startswith("tripo_part_")]
groups={"cape":([],[]),"hair":([],[]),"other":([],[])}
for o in dst.objects:
    if o is None or o.type!='MESH': continue
    pid=o.name.replace("tripo_part_","").replace("_R","")
    key="cape" if pid in CAPE and not o.name.endswith("_R") else "hair" if pid in HAIR else "other"
    dg_obj=o  # rest pose == T-pose in that file
    V,P=groups[key]; off=len(V)
    V.extend(o.matrix_world@v.co for v in o.data.vertices); P.extend([[off+i for i in p.vertices] for p in o.data.polygons])
bvh={k:BVHTree.FromPolygons(V,P) for k,(V,P) in groups.items() if V}
print("source sizes",{k:len(v[0]) for k,v in groups.items()})
arm=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]
result=collections.Counter()
for name,targets in (("CG_Body","cape"),("CG_Head","hair")):
    ob=bpy.data.objects[name]; mw=ob.matrix_world
    bm=bmesh.new(); bm.from_mesh(ob.data)
    tag={}
    for v in bm.verts:
        p=mw@v.co
        d_t=bvh[targets].find_nearest(p)[3]; d_o=bvh["other"].find_nearest(p)[3]
        tag[v]= (d_t is not None and d_t<0.012 and (d_o is None or d_t<d_o))
    for f in bm.faces:
        f.select = sum(tag[v] for v in f.verts) >= 2
    n=sum(f.select for f in bm.faces); result[name+"->"+targets]=n
    bm.to_mesh(ob.data); bm.free()
    bpy.ops.object.select_all(action='DESELECT'); bpy.context.view_layer.objects.active=ob; ob.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_mode(type='FACE'); bpy.ops.mesh.separate(type='SELECTED'); bpy.ops.object.mode_set(mode='OBJECT')
    new=[o for o in bpy.context.selected_objects if o is not ob][0]
    new.name="CG_Cape" if targets=="cape" else "CG_Hair"; new.data.name=new.name
for o in list(bpy.data.objects):
    if o.name.startswith("tripo_part_"): bpy.data.objects.remove(o,do_unlink=True)
print("SPLIT",dict(result),{o.name:len(o.data.polygons) for o in bpy.data.objects if o.type=='MESH'})
bpy.ops.wm.save_as_mainfile(filepath=out)
