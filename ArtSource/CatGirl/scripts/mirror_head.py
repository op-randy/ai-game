# Rebuild the damaged left half of the mask (21) and head cap (23) by mirroring their intact right halves.
import bpy, sys, bmesh
from mathutils import Vector, Matrix
from mathutils.kdtree import KDTree
a=sys.argv[sys.argv.index("--")+1:]
bpy.ops.wm.open_mainfile(filepath=a[0])
def wpts(n):
    o=bpy.data.objects[n]; return [o.matrix_world@v.co for v in o.data.vertices]
# --- find the symmetry plane x=c from intact symmetric parts (forehead plate + horn pairs)
ref=wpts("tripo_part_52")+wpts("tripo_part_61")+wpts("tripo_part_63")+wpts("tripo_part_58")+wpts("tripo_part_65")
kd=KDTree(len(ref))
for i,p in enumerate(ref): kd.insert(p,i)
kd.balance()
best=None
for k in range(-400,1):
    c=-0.04+k*0.0001+0.04  # scan -0.04..0
    c=k*0.0001
    err=sum(kd.find(Vector((2*c-p.x,p.y,p.z)))[2] for p in ref[::3])/len(ref[::3])
    if best is None or err<best[1]: best=(c,err)
c=best[0]; print("PLANE x=%.4f mean mirror error %.4f m"%best)
for name in ("tripo_part_21","tripo_part_23"):
    o=bpy.data.objects[name]
    mw=o.matrix_world; inv=mw.inverted()
    bm=bmesh.new(); bm.from_mesh(o.data)
    co_l=inv@Vector((c,0,0)); no_l=(inv.to_3x3()@Vector((1,0,0))).normalized()
    geom=bm.verts[:]+bm.edges[:]+bm.faces[:]
    bmesh.ops.bisect_plane(bm,geom=geom,plane_co=co_l,plane_no=no_l,clear_outer=True,clear_inner=False,dist=0.0002)
    n_before=len(bm.verts); bm.to_mesh(o.data); bm.free()
    # mirror object at the plane, mirror modifier first in the stack, then apply it
    emp=bpy.data.objects.get("MirrorPlane") or bpy.data.objects.new("MirrorPlane",None)
    if emp.name not in bpy.context.scene.collection.objects: bpy.context.scene.collection.objects.link(emp)
    emp.location=(c,0,0)
    m=o.modifiers.new("MirrorFix",'MIRROR'); m.use_axis[0]=True; m.mirror_object=emp; m.use_clip=True; m.use_mirror_merge=True; m.merge_threshold=0.0004
    bpy.context.view_layer.objects.active=o
    bpy.ops.object.modifier_move_to_index(modifier=m.name,index=0)
    bpy.ops.object.modifier_apply(modifier=m.name)
    xs=[(mw@v.co).x for v in o.data.vertices]
    print("MIRRORED",name,"half verts",n_before,"-> verts",len(o.data.vertices),"x[%.3f,%.3f]"%(min(xs),max(xs)))
bpy.data.objects.remove(bpy.data.objects["MirrorPlane"],do_unlink=True)
bpy.ops.wm.save_as_mainfile(filepath=a[1])
