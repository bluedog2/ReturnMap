"""Source-pixel cutout animation. Standard library only; writes beside this file."""
from pixel_io import *
import math

SOURCE=ROOT.parent/'Px32Pilot2/_1x/Hero_Idle.png'
base=[r[:64] for r in readpng(SOURCE)]
def blank(w=64,h=64): return [[CLEAR]*w for _ in range(h)]
def mask(pred):return [[p if p[3] and pred(x,y,p) else CLEAR for x,p in enumerate(row)] for y,row in enumerate(base)]
cape=mask(lambda x,y,p:y>=34 and p in HP[4:7])
def largest_component(a):
    remaining={(x,y) for y,r in enumerate(a) for x,p in enumerate(r) if p[3]};groups=[]
    while remaining:
        seed=remaining.pop();group={seed};stack=[seed]
        while stack:
            x,y=stack.pop()
            for dx,dy in ((-1,0),(1,0),(0,-1),(0,1),(-1,-1),(1,-1),(-1,1),(1,1)):
                q=(x+dx,y+dy)
                if q in remaining:remaining.remove(q);group.add(q);stack.append(q)
        groups.append(group)
    keep=max(groups,key=len)
    return [[p if (x,y) in keep else CLEAR for x,p in enumerate(r)] for y,r in enumerate(a)]
cape=largest_component(cape)
head=mask(lambda x,y,p:y<35)
torso=mask(lambda x,y,p:35<=y<54 and p not in HP[4:7] and not (26<=x<=31 and 40<=y<=50))
# Reconstruct the armor behind the relocated forearm with adjacent source armor texels.
for y in range(40,51):
    for x in range(28,32):
        if base[y][x][3] and base[y][x] not in HP[4:7]:
            torso[y][x]=base[41+(y-40)%3][33+(x-28)%3]
arm=mask(lambda x,y,p:26<=x<=31 and 40<=y<=50 and p not in HP[4:7])
back_thigh=mask(lambda x,y,p:53<=y<=57 and x<=32 and p not in HP[4:7])
back_boot=mask(lambda x,y,p:y>=57 and x<=32 and p not in HP[4:7])
front_thigh=mask(lambda x,y,p:53<=y<=57 and x>=33 and p not in HP[4:7])
front_boot=mask(lambda x,y,p:y>=57 and x>=33 and p not in HP[4:7])

def affine(dst,src,a=1,b=0,c=0,d=1,tx=0,ty=0):
    det=a*d-b*c
    for y in range(len(dst)):
        for x in range(len(dst[0])):
            sx=round((d*(x-tx)-b*(y-ty))/det);sy=round((-c*(x-tx)+a*(y-ty))/det)
            if 0<=sy<len(src) and 0<=sx<len(src[0]) and src[sy][sx][3]:dst[y][x]=src[sy][sx]

def segment(dst,src,start,end,to_start,to_end,width=1):
    # Map a source limb axis to a posed axis without introducing colors.
    ux,uy=end[0]-start[0],end[1]-start[1];vx,vy=to_end[0]-to_start[0],to_end[1]-to_start[1]
    sl=math.hypot(ux,uy);tl=math.hypot(vx,vy)
    ux/=sl;uy/=sl;nx,ny=-uy,ux;wx,wy=vx/tl,vy/tl;px,py=-wy,wx
    a=wx*(tl/sl)*ux+px*width*nx;b=wx*(tl/sl)*uy+px*width*ny
    c=wy*(tl/sl)*ux+py*width*nx;d=wy*(tl/sl)*uy+py*width*ny
    affine(dst,src,a,b,c,d,to_start[0]-a*start[0]-b*start[1],to_start[1]-c*start[0]-d*start[1])

def edged(a):
    old=[r[:] for r in a]
    for y,row in enumerate(old):
        for x,p in enumerate(row):
            if p[3] and any(xx<0 or xx>=64 or yy<0 or yy>=64 or not old[yy][xx][3] for xx,yy in ((x-1,y),(x+1,y),(x,y-1),(x,y+1))):
                a[y][x]=HP[4] if p in HP[4:7] else HP[7] if p in HP[7:14] else HP[0] if p in HP[:4] else HP[14]
    return a

def normalize(a,height,bottom=63,center=31.5):
    bb=bbox(a);cut=crop(a,bb);w=len(cut[0]);h=len(cut)
    assert w<=60,(w,bb)
    out=blank();left=round(center-(w-1)/2);top=bottom-height+1
    for y in range(height):
        for x in range(w):out[top+y][left+x]=cut[min(h-1,int(y*h/height))][x]
    return edged(out)

def pose(lean=0,dy=0,legs=((28,57,28,63),(37,57,38,63)),arm_end=(29,50),cape_angle=0,flutter=0,cape_scale=1):
    a=blank(96,96)
    # Work on padded canvas, so no transformed body part is clipped by a cell.
    off=16
    theta=math.radians(cape_angle);co=math.cos(theta);si=math.sin(theta)
    cp=blank()
    for y,row in enumerate(cape):
        for x,p in enumerate(row):
            if p[3]:
                xx=x+round(flutter*math.sin((y-34)/25*math.pi))
                if 0<=xx<64:cp[y][xx]=p
    anchor=(26+lean*12,36+dy)
    affine(a,cp,co,-si*cape_scale,si,co*cape_scale,off+anchor[0]-co*26+si*cape_scale*36,off+anchor[1]-si*26-co*cape_scale*36)
    for index,(kx,ky,fx,fy) in enumerate(legs):
        sx=28 if index==0 else 37
        thigh,boot=(back_thigh,back_boot) if index==0 else (front_thigh,front_boot)
        hip=(sx+off,53+dy+off);knee=(kx+off,ky+off);foot=(fx+off,fy+off)
        segment(a,thigh,(sx,53),(sx,57),hip,knee)
        segment(a,boot,(sx,57),(sx,63),knee,foot)
    affine(a,torso,1,-lean,0,1,off+lean*48,off+dy)
    # Head remains an exact translated copy: crown, facial features and light direction preserved.
    affine(a,head,tx=off+round(lean*16),ty=off+dy)
    segment(a,arm,(29,40),(29,50),(29+lean*8+off,40+dy+off),(arm_end[0]+off,arm_end[1]+dy+off))
    return a

animations={}; specs={}
def add(name,frames,fps,notes):animations[name]=frames;specs[name]={'fps':fps,'pose_notes':notes}

run=[]
legs_cycle=[((23,56,21,63),(38,57,42,63)),((25,57,28,63),(40,54,43,60)),((31,55,35,60),(34,55,30,63)),((36,56,41,63),(29,57,25,62)),((37,57,42,63),(24,56,21,63)),((39,54,43,60),(26,57,29,63)),((34,55,30,63),(31,55,35,60)),((28,57,24,62),(36,56,41,63))]
for i,legs in enumerate(legs_cycle):
    run.append(normalize(pose(.24,0,legs,(34+round(5*math.sin(i*math.pi/4)),45),52,(-1,0,1,2,1,0,-1,-2)[i],.68),48))
add('Run',run,12,'Alternating contact, push, flight-shaped legs and landing. Ground anchor retained in flight-shaped frames per import rule.')
add('JumpStart',[
    normalize(pose(.10,1,((24,57,28,63),(40,57,38,63)),(32,47),12,1,.9),47,63),
    normalize(pose(.08,0,((26,56,25,61),(40,56,41,62)),(44,29),42,-1,.7),48,59)],12,'Crouch then extended takeoff with raised arm; airborne second frame.')
add('Fall',[
    normalize(pose(.02,0,((25,56,24,63),(40,56,42,63)),(34,43),137,1,.65),48,60),
    normalize(pose(.02,0,((26,56,24,62),(39,56,41,63)),(35,42),145,-1,.65),48,60)],8,'Upturned cape; separated legs and slight flutter.')
add('Land',[
    normalize(pose(.13,1,((23,56,28,63),(41,56,38,63)),(33,48),27,2,.8),46),
    normalize(pose(.07,0,((26,57,28,63),(39,57,38,63)),(31,49),13,1,.9),47),
    normalize(pose(0,0,((28,57,28,63),(37,57,38,63)),(29,50),2,0,1),48)],12,'Crouch expressed through knees and cape; crown varies by only two rows.')
add('Dash',[normalize(pose(.63,0,legs,(37,44),80,f,.68),41) for legs,f in [(((22,57,19,63),(38,57,43,63)),0),(((24,57,21,63),(40,55,44,62)),1),(((26,56,23,63),(39,57,43,63)),-1),(((23,57,20,63),(38,57,42,63)),0)]],16,'Low forward lean; horizontal cape. Padded composition preserves complete torso before cell fitting.')
add('Hit',[
    normalize(pose(-.32,0,((27,57,27,63),(39,57,40,63)),(24,45),-8,1,.9),48),
    normalize(pose(-.10,0,((28,57,28,63),(38,57,39,63)),(28,48),0,0,1),48)],10,'Recoil backward then recover.')
death=[]
for i,(angle,height) in enumerate([(0,48),(-12,45),(12,37),(38,30),(66,22),(90,19)]):
    p=pose(-.2 if i<2 else .05,0,((24,56,28,62),(40,56,38,62)),(24 if i<2 else 35,47),10 if i<3 else 30,0,.75)
    bb=bbox(p);cut=crop(p,bb)
    if i:
        rotated=blank(128,128);t=math.radians(angle);c=math.cos(t);s=math.sin(t)
        affine(rotated,cut,c,-s,s,c,64-c*len(cut[0])/2+s*len(cut)/2,64-s*len(cut[0])/2-c*len(cut)/2)
        p=rotated
    death.append(normalize(p,height))
add('Death',death,10,'Stagger, buckle to knees, roll onto side, fully prone final frame within cell.')

def chunk(t,d):return struct.pack('>I',len(d))+t+d+struct.pack('>I',zlib.crc32(t+d)&0xffffffff)
def apng(path,frames,fps):
    frames=[up(f,3) for f in frames];w=len(frames[0][0]);h=len(frames[0]);seq=0
    data=b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',w,h,8,6,0,0,0))+chunk(b'acTL',struct.pack('>II',len(frames),0))
    for i,f in enumerate(frames):
        data+=chunk(b'fcTL',struct.pack('>IIIIIHHBB',seq,w,h,0,0,1,fps,0,0));seq+=1
        raw=zlib.compress(b''.join(b'\0'+bytes(c for p in row for c in p) for row in f),9)
        if i==0:data+=chunk(b'IDAT',raw)
        else:data+=chunk(b'fdAT',struct.pack('>I',seq)+raw);seq+=1
    (ROOT/path).write_bytes(data+chunk(b'IEND',b''))

report={};records={}
preview=blank(8*64,7*64)
for row,(name,frames) in enumerate(animations.items()):
    file='Hero_'+name
    sheet=[sum((f[y] for f in frames),[]) for y in range(64)]
    savepng(Path('_1x')/(file+'.png'),sheet);savepng(Path('Player')/(file+'.png'),up(sheet,8))
    (ROOT/'Animations').mkdir(exist_ok=True);apng(Path('Animations')/(file+'_3x.apng'),frames,specs[name]['fps'])
    stats=[]
    for i,f in enumerate(frames):
        b=bbox(f); colors={p[:3] for r in f for p in r if p[3]}
        s={'frame':i,'bbox':b,'bottom_png_y':b[3],'bottom_origin_row':63-b[3],'center_x':(b[0]+b[2])/2,'height':b[3]-b[1]+1,'opaque_colors':len(colors),'palette_outside_colors':len({p[:3] for r in f for p in r}-{p[:3] for p in MP}),'edge_opaque_pixels':sum(f[y][x][3]>0 for y in range(64) for x in range(64) if y==0 or x==0 or x==63),'alpha_values':sorted({p[3] for r in f for p in r})}
        assert s['opaque_colors']<=16 and s['palette_outside_colors']==s['edge_opaque_pixels']==0 and set(s['alpha_values'])<={0,255},s
        if name not in ('JumpStart','Fall'):assert s['bottom_origin_row']==0,s
        stats.append(s)
        for y in range(64):preview[row*64+y][i*64:(i+1)*64]=f[y]
    centers=[s['center_x'] for s in stats];heights=[s['height'] for s in stats]
    cd=max(abs(c-sum(centers)/len(centers)) for c in centers);hd=max(abs(h-sum(heights)/len(heights)) for h in heights)
    if name!='Death':assert cd<=2 and hd<=2,(name,cd,hd)
    colors={p[:3] for r in sheet for p in r if p[3]};assert len(colors)<=16
    assert readpng(ROOT/'Player'/(file+'.png'))==up(sheet,8)
    report[file]={'frames':stats,'sheet_opaque_colors':len(colors),'center_max_deviation':cd,'height_max_deviation':hd,'nearest_neighbor_block_errors':0,'saved_size':[len(frames)*512,512],'passed':True}
    records[file]={'one_x':'_1x/'+file+'.png','eight_x':'Player/'+file+'.png','animation':'Animations/'+file+'_3x.apng','frames':len(frames),**specs[name]}
savepng(Path('Preview_3x.png'),up(preview,3))
(ROOT/'validation.json').write_text(json.dumps({'all_passed':True,'row_convention':'PNG y increases downward. bottom_origin_row = 63 - bottom_png_y.','sheets':report},indent=2),encoding='utf8')
(ROOT/'generation.json').write_text(json.dumps({'source':str(SOURCE.relative_to(ROOT.parent.parent)),'source_sha256':hashlib.sha256(SOURCE.read_bytes()).hexdigest(),'source_frame':0,'pipeline':'Source cutout segmentation; inverse nearest-neighbor affine limb poses; translated original head; cape rotation and row flutter; padded composition; per-sheet height and bbox centering; material-specific 1px inner outline; exact 8x nearest-neighbor. No image generation.','palette':['#'+s for s in MASTER],'files':records,'preview_row_order':list(records),'limitations':['Source cutout editing has no newly drawn occluded anatomy; extreme poses retain source-side lighting.','Run flight-shaped frames retain ground alignment as explicitly required.','Crouch depth is restricted by per-sheet height tolerance.','Death animation preview loops for inspection; gameplay should hold the last frame.','No engine import or gameplay test performed.']},indent=2),encoding='utf8')
print(json.dumps({k:{'bottom':[f['bottom_origin_row'] for f in v['frames']],'centers':[f['center_x'] for f in v['frames']],'heights':[f['height'] for f in v['frames']],'colors':v['sheet_opaque_colors']} for k,v in report.items()},indent=2))
