"""Pilot 2: deterministic, dependency-free normalization. Run python -B process.py.
All writes are contained in this directory. pixel_io.py is copied from Pilot 1.
"""
from pixel_io import *
import random, math, shutil
OLD=ROOT.parent/'Px32Pilot'
outputs={}; records={}; report={}
sources=json.loads((ROOT/'sources.json').read_text(encoding='utf8'))

def deliver(name,a,folder,scale,source,steps):
    p=Path(folder)/(name+'.png'); one=Path('_1x')/(name+'.png')
    savepng(one,a);savepng(p,up(a,scale));outputs[name]=a
    records[name]={'file':str(p),'one_x':str(one),'source':source,'postprocess':steps}
    rgb={p[:3] for row in a for p in row}
    r={'art_size':[len(a[0]),len(a)],'saved_size':[len(a[0])*scale,len(a)*scale],
       'palette_outside_colors':len(rgb-{p[:3] for p in MP}),
       'opaque_colors':len({p for row in a for p in row if p[3]}),
       'alpha_values':sorted({p[3] for row in a for p in row}),
       'nearest_neighbor_block_errors':0}
    assert readpng(ROOT/p)==up(a,scale) and readpng(ROOT/one)==a
    assert r['palette_outside_colors']==0
    assert set(r['alpha_values'])<={0,255}
    assert r['opaque_colors']<= (12 if folder=='Tiles' else 16 if folder=='Player' else 24)
    r['sha256']=hashlib.sha256((ROOT/p).read_bytes()).hexdigest();report[name]=r

def blit(dest,src,x,y):
    for j,row in enumerate(src):
        for i,p in enumerate(row):
            if p[3]:dest[y+j][x+i]=p

# Keep original imagegen pixel clusters; warp only their interior distribution.
for ki,(kind,n) in enumerate([('Floor',3),('Wall',3),('Ceiling',2)]):
    src=readpng(OLD/'_1x'/('Tile_Play_'+kind+'.png'))
    common=readpng(OLD/'_1x'/'Tile_Play_Wall.png')
    for v in range(n):
        a=[]
        for y in range(32):
            row=[]
            for x in range(32):
                sx=(x+v*9+(y//7)*(v+1))%32
                sy=3+(y+v*5+x//(9+v))%25
                p=src[sy][sx]
                # Compress the bright and dark mortar ramps, preserving broad stone faces.
                if p in (PLAY[2],PLAY[3]):p=PLAY[1]
                if p in (PLAY[4],PLAY[5]):p=PLAY[0]
                row.append(p)
            a.append(row)
        # Short upper-left chips and asymmetric fissures, interior only.
        for x,y in [(5+v*4,5),(21-v*3,17),(10+v*3,25)]:
            for dx in range(3):a[y][x+dx]=PLAY[2]
            a[y+1][x]=PLAY[1]
        for t in range(5):a[10+t][8+v*5+t//2]=PLAY[6]
        # A shared two-pixel periodic collar, stone color rather than dark outline.
        for y in range(32):
            for x in range(32):
                if x<2 or x>=30 or y<2 or y>=30:
                    xx=x if x<30 else 31-x; yy=y if y<30 else 31-y
                    p=PLAY[1]
                    if x<2 or x>=30:
                        if yy in (7,8,9,21,22):p=PLAY[0]
                    if y<2 or y>=30:
                        if xx in (15,16,17,25):p=PLAY[0]
                    if kind=='Floor' and y in (0,31) and 4<=x<=9:p=PLAY[2]
                    a[y][x]=p
        name='Tile_Play_'+kind+'_'+'ABC'[v]
        deliver(name,a,'Tiles',8,str(OLD/'_1x'/('Tile_Play_'+kind+'.png')),
                ['Reuse imagegen Pilot1 clusters; deterministic interior warp and localized chips.',
                 'Reduce horizontal mortar contrast; shared two-pixel border; floor highlight one row.',
                 'Binary alpha and master palette; nearest-neighbor 8x.'])
        b=[[BUILD[PLAY.index(p)] for p in row] for row in a]
        bn=name.replace('Play','Build')
        deliver(bn,b,'Tiles',8,name,['Exact Play-to-Build palette index replacement; no geometry changes.'])
        report[bn]['phase_structure_mismatch']=sum(BUILD.index(b[y][x])!=PLAY.index(a[y][x]) for y in range(32) for x in range(32))

# Every ordered pair, including self pairs, in both directions, plus 2px collar identity.
pairs=[]
for phase in ('Play','Build'):
    for kind,n in [('Floor',3),('Wall',3),('Ceiling',2)]:
        names=['Tile_'+phase+'_'+kind+'_'+'ABC'[i] for i in range(n)]
        for na in names:
            for nb in names:
                a,b=outputs[na],outputs[nb]
                lr=sum(a[y][-1]!=b[y][0] for y in range(32))
                tb=sum(a[-1][x]!=b[0][x] for x in range(32))
                collar=sum(a[y][x]!=b[y][x] for y in range(32) for x in range(32) if x<2 or x>=30 or y<2 or y>=30)
                assert lr==tb==collar==0
                pairs.append({'a':na,'b':nb,'lr_mismatch':lr,'tb_mismatch':tb,'collar_mismatch':collar})

base=[r[:64] for r in readpng(OLD/'_1x'/'Hero_Idle.png')]
def outline(a):
    old=[r[:] for r in a]
    for y,row in enumerate(old):
        for x,p in enumerate(row):
            if not p[3]:continue
            if any(xx<0 or xx>=64 or yy<0 or yy>=64 or not old[yy][xx][3] for xx,yy in [(x-1,y),(x+1,y),(x,y-1),(x,y+1)]):
                a[y][x]=HP[4] if p in HP[4:7] else HP[7] if p in HP[7:14] else HP[0] if p in HP[:4] else HP[14]
    return a
frames=[];fr=[]
for i,(dy,sway) in enumerate(zip([0,-1,-1,0,1,1],[0,1,2,1,0,-1])):
    a=[[CLEAR]*64 for _ in range(64)]
    # Inverse integer row warp prevents holes at the waist; boots are fixed.
    for y in range(64):
        sy=y-dy if y<49 else y-(dy if y<53 else 0)
        if 0<=sy<64:
            for x,p in enumerate(base[sy]):
                if p not in HP[4:7]:a[y][x]=p
    for y in range(64):
        sy=y-dy if y<53 else y
        if not 0<=sy<64:continue
        for x,p in enumerate(base[sy]):
            if p in HP[4:7]:
                dx=round(sway*max(0,min(1,(y-39)/17)))
                a[y][x+dx]=p
    a=outline(a)
    for y in range(60,64):a[y]=base[y][:]
    frames.append(a)
    b=bbox(a);pts=[x for y in range(34,48) for x,p in enumerate(a[y]) if p in HP[:4]]
    center=(min(pts)+max(pts))/2
    r={'frame':i,'bottom_png_y':b[3],'bottom_origin_row':63-b[3],
       'height':b[3]-b[1]+1,'torso_center_x':center,'bbox':b,
       'top_left_right_edge_opaque':sum(p[3]>0 for p in a[0])+sum(row[0][3]>0 or row[-1][3]>0 for row in a),
       'boot_difference_pixels':sum(a[y][x]!=base[y][x] for y in range(60,64) for x in range(64))}
    assert r['bottom_png_y']==63 and 47<=r['height']<=49 and abs(center-31.5)<=1
    assert r['top_left_right_edge_opaque']==0 and r['boot_difference_pixels']==0 and b[2]-b[0]+1<=32,r
    fr.append(r)
for i in range(6):
    fr[i]['difference_to_next_pixels']=sum(frames[i][y][x]!=frames[(i+1)%6][y][x] for y in range(64) for x in range(64))
    assert fr[i]['difference_to_next_pixels']>0
sheet=[sum([f[y] for f in frames],[]) for y in range(64)]
deliver('Hero_Idle',sheet,'Player',8,str(OLD/'_1x'/'Hero_Idle.png'),
        ['Use first 64x64 Pilot1 frame, no new character drawing.',
         'Integer breathing shifts 0,-1,-1,0,+1,+1; cape tip shifts 0,+1,+2,+1,0,-1.',
         'Boots fixed; material-specific one-pixel inner outline; same palette; 8fps loop.'])
report['Hero_Idle']['frames']=fr

def smooth_blocks(a):
    # 2x2 logical clusters: removes generated micro-AA after area-mode quantization.
    h=len(a);w=len(a[0]);out=[[CLEAR]*w for _ in range(h)]
    for y in range(0,h,2):
        for x in range(0,w,2):
            p=collections.Counter(a[yy][xx] for yy in range(y,min(y+2,h)) for xx in range(x,min(x+2,w))).most_common(1)[0][0]
            for yy in range(y,min(y+2,h)):
                for xx in range(x,min(x+2,w)):out[yy][xx]=p
    return out

def periodic(a):
    # Symmetric 24px transition, discrete 2x2 checker selection, then identical end columns.
    w=len(a[0]);old=[r[:] for r in a]
    for y in range(len(a)):
        for x in range(24):
            if x<4 or ((x//2+y//2)%2==0 and x<16):
                a[y][w-1-x]=old[y][x]
    for row in a:row[-1]=row[0]
    return a

BG=list(map(color,'19142E 27213F 2F2F50 293145 384869 53617E 47345B 302460 2A2B32 4B3478 5F3861 1A151A'.split()))
WARM=list(map(color,'2B231B 4D3720 554639 654138 A87B56 CEA362 CAA981 DEC28D 716432 3E6B3A 6E9A4A C5AF92'.split()))
for s in sources:
    key=s['key'];warm=key=='warm_near';near='near' in key
    name='Verification_Near' if warm else 'Play_Near' if near else 'Play_Far'
    local=ROOT/'_sources'/(key+'.png');local.parent.mkdir(exist_ok=True)
    if not local.exists():shutil.copyfile(s['source'],local)
    raw=readpng(local);h=256 if near else 360
    a=down(raw,640,h,WARM if warm else BG)
    if not near:a=[[p[:3]+(255,) for p in row] for row in a]
    else:
        # Place scenery in lower 48%; original generated layer can intrude too high.
        bb=bbox(a);cut=crop(a,[0,bb[1],639,255]);cut=down(cut,640,122,WARM if warm else BG)
        a=[[CLEAR]*640 for _ in range(134)]+cut
    a=periodic(smooth_blocks(a))
    deliver(name,a,'Backgrounds',2,s['source'],['Imagegen reference-guided generation.',
       'Pilot1 nearest-palette area-mode downsample, alpha threshold 128; 2x2 cluster cleanup.',
       '24px discrete periodic transition; exact matching edge columns; nearest-neighbor 2x.'])
    report[name]['horizontal_edge_mismatch']=sum(row[0]!=row[-1] for row in a)
    report[name]['top_128_rows_opaque']=sum(p[3]>0 for row in a[:128] for p in row) if near else None
    if name=='Play_Far':
        mapping=dict(zip(BG,list(map(color,'A87B56 CAA981 CEA362 C5AF92 C5AF92 DEC28D CEA362 A87B56 554639 CAA981 CEA362 4D3720'.split()))))
        b=[[mapping[p] for p in row] for row in a]
        # Lightning is the highest night-sky ramp: absorb it into sunset clouds.
        for y in range(235):
            for x in range(640):
                if a[y][x]==color('53617E'):b[y][x]=color('C5AF92')
        # Ochre distant castle and vegetated foreground hills retain the original silhouettes.
        for y in range(240,282):
            for x in range(640):
                if a[y][x] in (color('19142E'),color('293145')):b[y][x]=color('A87B56')
        for y in range(282,360):
            for x in range(640):
                if a[y][x] in (color('19142E'),color('293145')):b[y][x]=color('3E6B3A') if y>320 else color('716432')
        deliver('Verification_Far',b,'Backgrounds',2,'Play_Far',
                ['Exact geometry reuse; region-aware sunset/ochre/green palette mapping; dim lightning absorbed into clouds.'])
        report['Verification_Far']['horizontal_edge_mismatch']=sum(row[0]!=row[-1] for row in b)

# Preview: two 8x4 mixed maps, six hero frames, two far/near composites.
preview=[[color('1A151A')]*656 for _ in range(438)]
rng=random.Random(20261025);layouts={}
for pi,phase in enumerate(['Play','Build']):
    layout=[]
    for ty in range(4):
        names=[]
        for tx in range(8):
            kind='Ceiling' if ty==0 else 'Floor' if ty==3 else 'Wall'
            v=rng.randrange(2 if kind=='Ceiling' else 3)
            name='Tile_'+phase+'_'+kind+'_'+'ABC'[v];names.append(name)
            blit(preview,outputs[name],8+pi*328+tx*32,8+ty*32)
        layout.append(names)
    layouts[phase]=layout
for i,f in enumerate(frames):blit(preview,f,136+i*64,144)
for pi,phase in enumerate(['Play','Verification']):
    far=outputs[phase+'_Far'];near=outputs[phase+'_Near'];comp=[r[:] for r in far]
    blit(comp,near,0,104)
    thumb=[[comp[y*2][x*2] for x in range(320)] for y in range(180)]
    blit(preview,thumb,4+pi*328,220)
savepng(Path('Preview_3x.png'),up(preview,3))
# Explicit 3x3 random tests for each type, both phases.
swatch=[[color('1A151A')]*296 for _ in range(196)]
for pi,phase in enumerate(['Play','Build']):
    for ki,(kind,n) in enumerate([('Floor',3),('Wall',3),('Ceiling',2)]):
        for y in range(3):
            for x in range(3):blit(swatch,outputs['Tile_'+phase+'_'+kind+'_'+'ABC'[rng.randrange(n)]],ki*100+x*32,pi*100+y*32)
savepng(Path('Tile_Random_3x3.png'),up(swatch,3))
report['tile_pair_tests']=pairs
def stripe_score(a):
    lum=lambda p:0.2126*p[0]+0.7152*p[1]+0.0722*p[2]
    means=[sum(map(lum,row))/len(row) for row in a]
    return round(max(means)-min(means),4)
report['floor_horizontal_row_luminance_range']={
    'pilot1':stripe_score(readpng(OLD/'_1x'/'Tile_Play_Floor.png')),
    'pilot2':{v:stripe_score(outputs['Tile_Play_Floor_'+v]) for v in 'ABC'},
    'note':'Lower row-mean range reduces horizontal band dominance; not a perceptual guarantee.'}
report['summary']={'asset_count':len(outputs),'palette_outside_colors':sum(r['palette_outside_colors'] for k,r in report.items() if k in outputs),
   'ordered_tile_pairs':len(pairs),'tile_boundary_checks':len(pairs)*2,'tile_seam_mismatch_pixels':sum(r['lr_mismatch']+r['tb_mismatch'] for r in pairs),
   'background_horizontal_mismatch_pixels':sum(report[k].get('horizontal_edge_mismatch',0) for k in outputs)}
(ROOT/'validation.json').write_text(json.dumps(report,indent=2),encoding='utf8')
(ROOT/'generation.json').write_text(json.dumps({'spec':'docs/px32-conversion-plan.md section 2','master_palette':['#'+s for s in MASTER],
 'pipeline':'Pilot1 PNG IO, palette nearest RGB, area-mode downsample, binary alpha, deterministic repair, nearest-neighbor export',
 'sources':sources,'files':records,'preview_layouts':layouts,'hero_fps':8,
 'limitations':['No Unity import or in-game camera/parallax playtest.',
 'Exact edge equality is a numerical condition, not a guarantee against all perceived repetition.',
 'Tiles derive from Pilot1 generated clusters via deterministic interior edits; hero derives exclusively from Pilot1 first frame.',
 'Background generation grid was normalized with 2x2 clusters; no smooth gradients survive quantization.',
 'Verification Near retains general composition but adds vegetation; not pixel-identical geometry to Play Near.']},indent=2),encoding='utf8')
print(json.dumps({'summary':report['summary'],'hero_frames':fr},indent=2))
