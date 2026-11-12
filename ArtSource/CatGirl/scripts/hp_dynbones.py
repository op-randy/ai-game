# Adds physics bones: cape chains, hair chains and jiggle bones (breasts, butt, thighs) + soft weights.
import bpy, sys, math, collections
from mathutils import Vector
a=sys.argv[sys.argv.index("--")+1:]
bpy.ops.wm.open_mainfile(filepath=a[0]); out=a[1]
arm=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]
H=lambda n: arm.matrix_world@arm.data.bones[n].head_local
CX=-0.02
uc=H("UpperChest"); hd=H("Head")
def sector_chain(obj, cx, cy, ang, top_z, nb, width=22):
    P=[obj.matrix_world@v.co for v in obj.data.vertices]
    sel=[p for p in P if abs(((math.degrees(math.atan2(p.x-cx,p.y-cy))-ang+180)%360)-180)<width and p.z<=top_z]
    if len(sel)<50: return None
    zb=min(p.z for p in sel); pts=[]
    for k in range(nb+1):
        z=top_z-(top_z-zb)*k/nb; sl=[p for p in sel if abs(p.z-z)<(top_z-zb)/(nb*2)+0.005]
        if not sl: sl=sorted(sel,key=lambda p:abs(p.z-z))[:30]
        c=sum(sl,Vector())/len(sl); pts.append(Vector((c.x,c.y,z)))
    # sit slightly inside the cloth/hair so the bones follow its middle
    return pts
chains={}
cape=bpy.data.objects["CG_Cape"]; hair=bpy.data.objects["CG_Hair"]
for i,ang in enumerate((-75,-45,-15,15,45,75,105)):
    c=sector_chain(cape,uc.x,uc.y,ang,0.76,4)
    if c: chains[f"Cape_{i}"]=("UpperChest",c,ang)
for i,ang in enumerate((-75,-45,-15,15,45,75)):
    c=sector_chain(hair,hd.x,hd.y,ang,0.84,4)
    if c: chains[f"Hair_{i}"]=("Head",c,ang)
jiggle={  # name: (parent, head, tail, radius, strength, mesh)
 "Jiggle_Breast_L":("UpperChest",Vector((CX+0.045,-0.085,0.695)),Vector((CX+0.05,-0.14,0.69)),0.065,0.85,"CG_Body"),
 "Jiggle_Breast_R":("UpperChest",Vector((CX-0.045,-0.085,0.695)),Vector((CX-0.05,-0.14,0.69)),0.065,0.85,"CG_Body"),
 "Jiggle_Butt_L":("Hips",Vector((CX+0.04,-0.035,0.55)),Vector((CX+0.045,0.01,0.545)),0.065,0.8,"CG_Body"),
 "Jiggle_Butt_R":("Hips",Vector((CX-0.04,-0.035,0.55)),Vector((CX-0.045,0.01,0.545)),0.065,0.8,"CG_Body"),
 "Jiggle_Thigh_L":("Left_UpperLeg",Vector((H("Left_UpperLeg").x,-0.06,0.44)),Vector((H("Left_UpperLeg").x+0.01,-0.02,0.44)),0.07,0.4,"CG_Body"),
 "Jiggle_Thigh_R":("Right_UpperLeg",Vector((H("Right_UpperLeg").x,-0.06,0.44)),Vector((H("Right_UpperLeg").x-0.01,-0.02,0.44)),0.07,0.4,"CG_Body"),
}
# ---- create bones
bpy.ops.object.select_all(action='DESELECT'); bpy.context.view_layer.objects.active=arm; arm.select_set(True)
bpy.ops.object.mode_set(mode='EDIT'); eb=arm.data.edit_bones
inv=arm.matrix_world.inverted()
for name,(parent,pts,ang) in chains.items():
    prev=eb[parent]
    for j in range(len(pts)-1):
        b=eb.new(f"{name}_{j}"); b.head=inv@pts[j]; b.tail=inv@pts[j+1]; b.parent=prev; b.use_connect=j>0; b.use_deform=True; prev=b
for name,(parent,h,t,r,s,m) in jiggle.items():
    b=eb.new(name); b.head=inv@h; b.tail=inv@t; b.parent=eb[parent]; b.use_deform=True
bpy.ops.object.mode_set(mode='OBJECT')
print("BONES chains",{k:len(v[1])-1 for k,v in chains.items()},"jiggle",len(jiggle))
# ---- weights
def setw(o,vi,new):
    for g in list(o.data.vertices[vi].groups): o.vertex_groups[g.group].remove([vi])
    s=sum(new.values())
    for n,x in sorted(new.items(),key=lambda kv:-kv[1])[:4]:
        g=o.vertex_groups.get(n) or o.vertex_groups.new(name=n); g.add([vi],x/s,'REPLACE')
def chain_weights(obj, prefix, parent, top_z, attach):
    cs=sorted([(v[2],k,v[1]) for k,v in chains.items() if k.startswith(prefix)])
    cx,cy=(uc.x,uc.y) if prefix=="Cape" else (hd.x,hd.y)
    n=0
    for v in obj.data.vertices:
        p=obj.matrix_world@v.co
        if p.z>top_z: continue
        ang=math.degrees(math.atan2(p.x-cx,p.y-cy))
        # two neighbouring chains by angle
        ds=sorted(cs,key=lambda c:abs(((ang-c[0]+180)%360)-180))[:2]
        da=[abs(((ang-c[0]+180)%360)-180) for c in ds]
        wa=[1.0,0.0] if da[0]<1e-3 else [da[1]/(da[0]+da[1]),da[0]/(da[0]+da[1])]
        new=collections.Counter()
        for (cang,cname,pts),w in zip(ds,wa):
            if w<1e-3: continue
            zs=[q.z for q in pts]; nb=len(pts)-1
            f=(zs[0]-p.z)/max(1e-4,zs[0]-zs[-1])*nb; f=max(0.0,min(nb-1e-4,f))
            k=int(f); t=f-k
            new[f"{cname}_{k}"]+=w*(1-t*0.5) ; 
            if k+1<nb: new[f"{cname}_{k+1}"]+=w*t*0.5
        # stay attached near the top
        att=max(0.0,min(1.0,(top_z-p.z)/attach))
        for kk in list(new): new[kk]*=att
        new[parent]+=1-att
        setw(obj,v.index,new); n+=1
    return n
nc=chain_weights(cape,"Cape","UpperChest",0.76,0.08)
nh=chain_weights(hair,"Hair","Head",0.84,0.06)
nj=0
for name,(parent,h,t,r,s,m) in jiggle.items():
    o=bpy.data.objects[m]
    for v in o.data.vertices:
        p=o.matrix_world@v.co; d=(p-(h*0.4+t*0.6)).length
        if d>r: continue
        w=s*(1-(d/r))**2
        cur={o.vertex_groups[g.group].name:g.weight for g in v.groups}
        if parent not in cur and not (parent.endswith("UpperLeg") or parent in("Hips","UpperChest","Chest")): continue
        new={k:x*(1-w) for k,x in cur.items()}; new[name]=new.get(name,0)+w
        setw(o,v.index,new); nj+=1
print("WEIGHTS cape verts",nc,"hair verts",nh,"jiggle verts",nj)
bpy.ops.wm.save_as_mainfile(filepath=out)
