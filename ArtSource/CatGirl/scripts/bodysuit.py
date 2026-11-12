# Adds a fitted black fabric "BodySuit" under the armor so holes (armpits, hips, inner legs) show fabric.
import bpy, sys, math, bmesh
from mathutils import Vector, Matrix
from mathutils.kdtree import KDTree
a=sys.argv[sys.argv.index("--")+1:]
src,dst=a[0],a[1]
bpy.ops.wm.open_mainfile(filepath=src)
arm=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]
EXCL={"0","1","2","3","4","5","6","8","44","16","10","68"}   # hair, cape, whip, hands, tiny bit
pts=[]
for o in bpy.data.objects:
    if o.type=='MESH' and o.name.startswith("tripo_part_") and o.name.replace("tripo_part_","") not in EXCL:
        pts += [o.matrix_world@v.co for v in o.data.vertices]
kd=KDTree(len(pts))
for i,p in enumerate(pts): kd.insert(p,i)
kd.balance()
from mathutils.bvhtree import BVHTree
_bv=[];_bp=[]
for o in bpy.data.objects:
    if o.type=='MESH' and o.name.startswith("tripo_part_"):
        off=len(_bv); _bv+=[o.matrix_world@v.co for v in o.data.vertices]; _bp+=[[off+i for i in pp.vertices] for pp in o.data.polygons]
bvh=BVHTree.FromPolygons(_bv,_bp)
def H(n): return arm.matrix_world@arm.data.bones[n].head_local
def T(n): return arm.matrix_world@arm.data.bones[n].tail_local
NSEC=20; STEP=0.007; SLAB=0.006

def chain(points, bones, rmax, blend=0.015, keep=None, max_shift=0.012):
    """points: polyline joints; bones[i] drives segment i. Returns rings [(center, frame, radii[], weights{})]"""
    # sample along polyline
    seglen=[(points[i+1]-points[i]).length for i in range(len(bones))]
    total=sum(seglen); n=max(2,int(total/STEP)+1)
    samples=[]
    for k in range(n):
        s=total*k/(n-1); acc=0
        for i,L in enumerate(seglen):
            if s<=acc+L or i==len(seglen)-1:
                f=(s-acc)/L if L>0 else 0
                p=points[i].lerp(points[i+1],min(max(f,0),1)); d=(points[i+1]-points[i]).normalized()
                # weights: blend with neighbour bone near joints
                w={bones[i]:1.0}
                if i>0 and s-acc<blend:
                    t=0.5+0.5*(s-acc)/blend; w={bones[i]:t, bones[i-1]:1-t}
                if i<len(bones)-1 and acc+L-s<blend:
                    t=0.5+0.5*(acc+L-s)/blend; w={bones[i]:t, bones[i+1]:1-t}
                samples.append((p,d,w)); break
            acc+=L
    # smooth directions
    dirs=[s[1] for s in samples]
    for _ in range(4):
        dirs=[(dirs[max(i-1,0)]+dirs[i]*2+dirs[min(i+1,len(dirs)-1)]).normalized() for i in range(len(dirs))]
    # parallel-transported frames
    ref=Vector((0,0,1)) if abs(dirs[0].z)<0.9 else Vector((1,0,0))
    u=(ref-dirs[0]*ref.dot(dirs[0])).normalized()
    frames=[]
    for i,d in enumerate(dirs):
        if i>0:
            u=(u-d*u.dot(d)).normalized()
        frames.append((u.copy(), d.cross(u).normalized(), d))
    rings=[]
    for (p,_,w),(u,v,d) in zip(samples,frames):
        near=[pts[idx] for (_,idx,_) in kd.find_range(p,rmax)]
        sl=[q for q in near if abs((q-p).dot(d))<SLAB and (keep is None or keep(q))]
        # recenter on the local geometry, but never drift far from the bone
        if len(sl)>=8:
            c=sum(sl,Vector())/len(sl); c=c-d*(c-p).dot(d)
            off=c-p
            if off.length>max_shift: c=p+off.normalized()*max_shift
        else: c=p.copy()
        rings.append([c,(u,v,d),sl,w])
    # smooth centers along chain
    cs=[r[0] for r in rings]
    for _ in range(6):
        cs=[cs[0]]+[(cs[i-1]+cs[i]*2+cs[i+1])/4 for i in range(1,len(cs)-1)]+[cs[-1]]
    out=[]
    prev=None
    for (c0,(u,v,d),sl,w),c in zip(rings,cs):
        bins=[[] for _ in range(NSEC)]
        for q in sl:
            r=q-c; r=r-d*r.dot(d); dist=r.length
            if dist<1e-5 or dist>rmax: continue
            ang=math.atan2(r.dot(v),r.dot(u)); bins[int(((ang+math.pi)/(2*math.pi))*NSEC)%NSEC].append(dist)
        rad=[None]*NSEC
        for k,b in enumerate(bins):
            if len(b)>=2: b.sort(); rad[k]=b[len(b)//4]
        valid=[x for x in rad if x]
        if len(valid)<4:
            rad=list(prev) if prev else [rmax*0.3]*NSEC
        else:
            med=sorted(valid)[len(valid)//2]
            rad=[min(x,1.15*med) if x else None for x in rad]
            for _ in range(NSEC):   # fill empty sectors from neighbours
                for k in range(NSEC):
                    if rad[k] is None:
                        l=rad[(k-1)%NSEC]; r_=rad[(k+1)%NSEC]
                        if l and r_: rad[k]=(l+r_)/2
                        elif l or r_: rad[k]=l or r_
            rad=[x or med for x in rad]
        prev=rad
        out.append([c,(u,v,d),rad,w])
    # smooth radii around and along
    for _ in range(3):
        for r in out: r[2]=[(r[2][(k-1)%NSEC]+2*r[2][k]+r[2][(k+1)%NSEC])/4 for k in range(NSEC)]
        new=[]
        for i in range(len(out)):
            a_=out[max(i-1,0)][2]; b_=out[i][2]; c_=out[min(i+1,len(out)-1)][2]
            new.append([(a_[k]+2*b_[k]+c_[k])/4 for k in range(NSEC)])
        for r,nr in zip(out,new): r[2]=nr
    return out

chains=[]
hips=H("Hips")
chains.append(chain([hips+Vector((0,0,-0.035)),hips,H("Spine"),H("Chest"),H("UpperChest"),T("UpperChest"),H("Neck")+ (T("Neck")-H("Neck"))*0.5],
                    ["Hips","Hips","Spine","Chest","UpperChest","Neck"],0.12,0.02))
# neck: from the base of the neck straight up to just under the mask (closes the open throat / nape)
chains.append(chain([T("UpperChest"),H("Neck"),H("Head"),H("Head")+Vector((0,0,0.03))],["UpperChest","Neck","Head"],0.05,0.012))
for s in ("Left","Right"):
    chains.append(chain([H(f"{s}_Shoulder"),H(f"{s}_UpperArm"),H(f"{s}_LowerArm"),H(f"{s}_Hand")],
                        [f"{s}_Shoulder",f"{s}_UpperArm",f"{s}_LowerArm"],0.06))
    mid=(H("Left_UpperLeg").x+H("Right_UpperLeg").x)/2
    own=(lambda q,m=mid: q.x>m) if s=="Left" else (lambda q,m=mid: q.x<m)
    chains.append(chain([H(f"{s}_UpperLeg")+Vector((0,0,0.03)),H(f"{s}_UpperLeg"),H(f"{s}_LowerLeg"),H(f"{s}_Foot")],
                        ["Hips",f"{s}_UpperLeg",f"{s}_LowerLeg"],0.065,keep=own))

me=bpy.data.meshes.new("BodySuit"); ob=bpy.data.objects.new("BodySuit",me)
bpy.context.scene.collection.objects.link(ob)
bm=bmesh.new(); uvl=bm.loops.layers.uv.new("UVMap")
groups={}; vw=[]
SHRINK=1.0
def clamp_rings(rings):
    for _ in range(3):
        for r in rings:
            c,(u,v,d),rad,w=r
            med=sorted(rad)[len(rad)//2]
            hitk=[False]*NSEC
            for k in range(NSEC):
                ang=-math.pi+(k+0.5)*2*math.pi/NSEC
                dirk=(u*math.cos(ang)+v*math.sin(ang)).normalized()
                hit=bvh.ray_cast(c,dirk,rad[k]+0.03)
                if hit[0] is not None and hit[3]>0.35*med:
                    rad[k]=min(rad[k], hit[3]-max(0.002,0.06*hit[3])); hitk[k]=True
            # where there is no armor to fit against (gaps), never bulge wider than the fitted part of the ring
            fitted=sorted(rad[k] for k in range(NSEC) if hitk[k])
            if fitted:
                cap=fitted[len(fitted)//2]*0.95
                for k in range(NSEC):
                    if not hitk[k]: rad[k]=min(rad[k],cap)
        # light smoothing, then clamp again on the next pass
        for r in rings:
            rad=r[2]; r[2]=[min(rad[k],(rad[(k-1)%NSEC]+2*rad[k]+rad[(k+1)%NSEC])/4*1.02) for k in range(NSEC)]
for rings in chains: clamp_rings(rings)
for ci,rings in enumerate(chains):
    ring_verts=[]
    for ri,(c,(u,v,d),rad,w) in enumerate(rings):
        vs=[]
        for k in range(NSEC):
            ang=-math.pi+(k+0.5)*2*math.pi/NSEC
            p=c+(u*math.cos(ang)+v*math.sin(ang))*rad[k]*SHRINK
            vs.append(bm.verts.new(p)); vw.append(w)
        ring_verts.append(vs)
    # close both ends so you never look into an open tube
    for end in (ring_verts[0],ring_verts[-1]):
        f=bm.faces.new(end)
        for li in range(len(end)): f.loops[li][uvl].uv=(0.5,0.5)
    for ri in range(len(ring_verts)-1):
        A,B=ring_verts[ri],ring_verts[ri+1]
        for k in range(NSEC):
            f=bm.faces.new((A[k],A[(k+1)%NSEC],B[(k+1)%NSEC],B[k]))
            for li,(kk,rr) in enumerate(((k,ri),(k+1,ri),(k+1,ri+1),(k,ri+1))):
                f.loops[li][uvl].uv=(kk/NSEC, rr/len(ring_verts))
bm.normal_update()
bm.to_mesh(me); bm.free()
for p in me.polygons: p.use_smooth=True
for i,w in enumerate(vw):
    s=sum(w.values())
    for n,x in w.items():
        g=ob.vertex_groups.get(n) or ob.vertex_groups.new(name=n)
        g.add([i],x/s,'REPLACE')
# make normals point outward (rings may be wound either way)
bpy.context.view_layer.objects.active=ob; ob.select_set(True)
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.mesh.normals_make_consistent(inside=False); bpy.ops.object.mode_set(mode='OBJECT')
mat=bpy.data.materials.new("BodySuitFabric"); mat.use_nodes=True
bsdf=mat.node_tree.nodes.get("Principled BSDF")
col=(0.02,0.02,0.024)
try:
    src=next(bpy.data.objects[n] for n in ("tripo_part_20","tripo_part_12","tripo_part_12_R") if n in bpy.data.objects).active_material
    bc=next(n for n in src.node_tree.nodes if n.type=='BSDF_PRINCIPLED').inputs["Base Color"]
    img=bc.links[0].from_node.image
    px=list(img.pixels[:]); vals=[(px[i],px[i+1],px[i+2]) for i in range(0,len(px),4*7)]
    vals.sort(key=lambda c:sum(c)); dark=vals[:len(vals)//2]
    col=tuple(sum(c[j] for c in dark)/len(dark) for j in range(3))
    print("FABRIC color sampled from",img.name,tuple(round(x,3) for x in col))
except Exception as e: print("FABRIC color fallback",e)
col=tuple(x*0.75 for x in col); bsdf.inputs["Base Color"].default_value=(*col,1); bsdf.inputs["Roughness"].default_value=0.45
mat.diffuse_color=(0.8,0.8,0.8,1)   # solid-mode grey like the other parts
me.materials.append(mat)
ob.parent=arm; ob.matrix_parent_inverse=arm.matrix_world.inverted()
m=ob.modifiers.new("Armature",'ARMATURE'); m.object=arm
arm.data.display_type="STICK"
print("BODYSUIT verts",len(me.vertices),"faces",len(me.polygons),"chains",len(chains))
bpy.ops.wm.save_as_mainfile(filepath=dst)
