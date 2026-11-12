# Cut fused contacts on the single mesh (only real joints stay connected), then smooth weights.
import bpy, sys, bmesh, collections, time
from mathutils import Vector
a=sys.argv[sys.argv.index("--")+1:]
bpy.ops.wm.open_mainfile(filepath=a[0]); out=a[1]
t0=time.time()
arm=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]; hp=bpy.data.objects["CatGirlHP"]
H=lambda n: arm.matrix_world@arm.data.bones[n].head_local
FING=("Thumb","Index","Middle","Ring","Pinky")
def chain(n):
    for s,S in (("Left_","L"),("Right_","R")):
        if n.startswith(s):
            r=n[len(s):]
            if r=="UpperArm": return S+"ArmU"
            if r in("LowerArm","Hand") or any(f in r for f in FING): return S+"ArmL"
            if r in("UpperLeg","LowerLeg","Foot","Toes","ToesEnd"): return S+"Leg"
            if r=="Shoulder": return "Torso"
            if r=="Eye": return "Head"
    if n in("Head","Neck","Neck_Twist_A"): return "Head"
    return "Torso"
JOINT={frozenset(("Head","Torso")):(None,0),
       frozenset(("Torso","LArmU")):(H("Left_UpperArm"),0.08), frozenset(("Torso","RArmU")):(H("Right_UpperArm"),0.08),
       frozenset(("LArmU","LArmL")):(H("Left_LowerArm"),0.055), frozenset(("RArmU","RArmL")):(H("Right_LowerArm"),0.055),
       frozenset(("Torso","LLeg")):(H("Left_UpperLeg"),0.12), frozenset(("Torso","RLeg")):(H("Right_UpperLeg"),0.12)}
def allowed(a,b,p):
    if a==b: return True
    j=JOINT.get(frozenset((a,b)))
    if j is None: return False
    c,r=j
    return c is None or (p-c).length<=r
me=hp.data; gn={g.index:g.name for g in hp.vertex_groups}; gch={i:chain(n) for i,n in gn.items()}
bm=bmesh.new(); bm.from_mesh(me); dl=bm.verts.layers.deform.active
def vacc(v):
    d=collections.Counter()
    for gi,w in v[dl].items(): d[gch[gi]]+=w
    return d
vc={v:(vacc(v).most_common(1) or [("Torso",0)])[0][0] for v in bm.verts}
side={}
for f in bm.faces:
    d=collections.Counter()
    for v in f.verts:
        for k,w in vacc(v).items(): d[k]+=w
    side[f]=(d.most_common(1) or [("Torso",0)])[0][0]
cut=[e for e in bm.edges if len(e.link_faces)==2 and side[e.link_faces[0]]!=side[e.link_faces[1]]
     and not allowed(side[e.link_faces[0]],side[e.link_faces[1]],(e.verts[0].co+e.verts[1].co)/2)]
print("cut edges",len(cut),collections.Counter(tuple(sorted((side[e.link_faces[0]],side[e.link_faces[1]]))) for e in cut).most_common(8))
bmesh.ops.split_edges(bm,edges=cut)
# boundary verts that now belong to one side only: take that side's weights from neighbours
fixed=0
for it in range(4):
    ch=0
    for v in bm.verts:
        ss={side[f] for f in v.link_faces}
        if len(ss)!=1: continue
        s=next(iter(ss)); c=vc.get(v) or (vacc(v).most_common(1) or [("Torso",0)])[0][0]
        if c==s or allowed(c,s,v.co): continue
        good=[e.other_vert(v) for e in v.link_edges if vc.get(e.other_vert(v))==s]
        if not good: continue
        tot=collections.Counter()
        for u in good:
            for gi,w in u[dl].items(): tot[gi]+=w/len(good)
        d=v[dl]
        for gi in list(d.keys()): del d[gi]
        for gi,w in tot.items(): d[gi]=w
        vc[v]=s; ch+=1
    fixed+=ch
    if not ch: break
wire=[e for e in bm.edges if not e.link_faces]
if wire: bmesh.ops.delete(bm,geom=wire,context='EDGES')
bm.to_mesh(me); me.update(); bm.free()
print("reweighted boundary verts",fixed,round(time.time()-t0))
# smooth weights across the real joints (cuts are not crossed), then tidy
bpy.ops.object.select_all(action='DESELECT'); bpy.context.view_layer.objects.active=hp; hp.select_set(True)
bpy.ops.object.mode_set(mode='WEIGHT_PAINT')
bpy.ops.object.vertex_group_smooth(group_select_mode='ALL',factor=0.5,repeat=8,expand=0.0)
bpy.ops.object.mode_set(mode='OBJECT')
bpy.ops.object.vertex_group_limit_total(group_select_mode='ALL',limit=4)
bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL',lock_active=False)
print("smoothed",round(time.time()-t0))
bpy.ops.wm.save_as_mainfile(filepath=out)
