import sys
sys.dont_write_bytecode=True
from pngio import readpng,writepng,bbox
from pathlib import Path
import re,json,shutil,collections,hashlib
ROOT=Path(__file__).resolve().parent
REPO=ROOT.parent.parent
Z=(0,0,0,0)
STATES=['Normal','Dud','Critical','Beneficial']
BANNED=set('334629 58783B 88AE43 B8D849 D8EC76 38284D 58377B 8050AD B581D4'.split())
PAL=[tuple(bytes.fromhex(h)) for h in re.findall(r'^#([0-9A-Fa-f]{6})\s*$',(ROOT.parent/'palette_v2.txt').read_text(encoding='utf-8'),re.M) if h.upper() not in BANNED]
cache={}
def clean(p):
 if p[3]<192:return Z
 if p not in cache:cache[p]=min(PAL,key=lambda c:sum((c[i]-p[i])**2 for i in range(3)))+(255,)
 return cache[p]
def blank(w,h):return [[Z]*w for _ in range(h)]
def paste(d,s,x,y):
 for yy,row in enumerate(s):
  for xx,p in enumerate(row):
   if p[3] and 0<=y+yy<len(d) and 0<=x+xx<len(d[0]):d[y+yy][x+xx]=p
def crop(a,r):
 x0,y0,x1,y1=r
 return [row[x0:x1] for row in a[y0:y1]]
def bounds(a):return bbox([[p if p[3]>=192 else Z for p in r] for r in a])
def sample(a,w,h):
 # Restore the generated sprite's requested logical grid by cell-center sampling.
 return [[clean(a[min(len(a)-1,int((y+.5)*len(a)/h))][min(len(a[0])-1,int((x+.5)*len(a[0])/w))]) for x in range(w)] for y in range(h)]
def isolated(a):
 b=[r[:] for r in a]
 for y,r in enumerate(a):
  for x,p in enumerate(r):
   if p[3] and not any(a[v][u][3] for v in range(max(0,y-1),min(len(a),y+2)) for u in range(max(0,x-1),min(len(r),x+2)) if (u,v)!=(x,y)):b[y][x]=Z
 return b
audit=[]
def extract(src,rect,w,h,solid=False):
 a=crop(src,rect);b=bounds(a);x0,y0,x1,y1=b
 if solid:
  # Cut away rounded outer corners: no opaque pixels are invented or extended.
  while any(a[y][x][3]<192 for y in range(y0,y1+1) for x in range(x0,x1+1)):
   x0+=1;y0+=1;x1-=1;y1-=1
   if x1<=x0 or y1<=y0:raise ValueError('no solid interior')
 a=crop(a,(x0,y0,x1+1,y1+1))
 audit.append({'source_rect':rect,'opaque_crop':[x0,y0,x1+1,y1+1],'logical_size':[w,h],'sample_pitch':[len(a[0])/w,len(a)/h]})
 return isolated(sample(a,w,h))
def common(frames):
 for y in range(len(frames[0])):
  for x in range(len(frames[0][0])):
   if not all(a[y][x][3] for a in frames):
    for a in frames:a[y][x]=Z
 return frames
def save(a,path):
 counts=collections.Counter(p for r in a for p in r if p[3]); colors=[c for c,n in counts.most_common(24)]
 if len(counts)>24:
  remap={p:min(colors,key=lambda c:sum((c[i]-p[i])**2 for i in range(3))) for p in counts}
  a=[[remap[p] if p[3] else Z for p in row] for row in a]
 path=ROOT/path;path.parent.mkdir(parents=True,exist_ok=True);writepng(path,a)
def group(trap,name,frames):
 for s,a in zip(STATES,frames):save(a,Path('Parts')/trap/s/(name+'.png'))
def putcanvas(a,w,h,x,y):
 out=blank(w,h);paste(out,a,x,y);return out
sp=readpng(ROOT/'Sources/spike_attempt1.png')
cols=[(0,313),(314,627),(628,941),(942,1254)]
for name,yr,size,offset,solid in [('base',(0,310),(64,64),(0,0),True),('spikes',(315,582),(60,52),(2,0),False),('glow',(620,800),(60,28),(2,0),False),('platform',(940,1254),(64,64),(0,0),True)]:
 frames=[putcanvas(extract(sp,(l,yr[0],r,yr[1]),*size,solid),64,64,*offset) for l,r in cols]
 if name=='platform':frames=[frames[3]]*4
 elif name=='glow':frames=common([frames[2],frames[2],frames[2],frames[3]])
 else:frames=common(frames)
 group('spike',name,frames)
ar=readpng(ROOT/'Sources/arrow_attempt1.png');aw=len(ar[0]);cols=[(round(i*aw/4),round((i+1)*aw/4)) for i in range(4)]
for name,yr,size,canvas,offset in [('body',(75,410),(64,58),(64,64),(0,3)),('arrow',(480,650),(32,16),(32,16),(0,0)),('muzzle_flash',(780,1030),(16,28),(32,32),(16,2))]:
 frames=[putcanvas(extract(ar,(l,yr[0],r,yr[1]),*size),*canvas,*offset) for l,r in cols]
 frames=common(frames)
 if name=='body':
  ys=[y for y in range(64) if frames[0][y][63][3]]
  dy=32-round((min(ys)+max(ys))/2)
  frames=[putcanvas(a,64,64,0,dy) for a in frames]
 group('arrow_shooter',name,frames)
ha=readpng(ROOT/'Sources/hammer_attempt1.png');cols=[(0,313),(314,627),(628,941),(942,1254)]
for name,yr,size,canvas,offset in [('head',(0,350),(64,64),(64,64),(0,0)),('mount',(380,644),(64,64),(64,64),(0,0)),('chain',(645,930),(10,16),(16,16),(3,0)),('impact',(985,1240),(64,32),(64,32),(0,0))]:
 frames=[putcanvas(extract(ha,(l,yr[0],r,yr[1]),*size),*canvas,*offset) for l,r in cols]
 frames=common(frames)
 if name=='chain':
  # Seam cutout and palette cleanup of existing opaque endpoint pixels only.
  for a in frames:
   for x in range(16):
    if a[0][x][3] and a[-1][x][3]:a[0][x]=a[-1][x]=(25,24,31,255)
    else:a[0][x]=a[-1][x]=Z
 group('drop_hammer',name,frames)
ma=readpng(ROOT/'Sources/marker_attempt1.png')
for i,n in enumerate(['Floor','Ceiling','LeftWall','RightWall']):
 x=(i%2)*627;y=(i//2)*627
 a=extract(ma,(x,y,x+627,y+627),64,64)
 save(a,Path('Misc')/('SlotMarker_'+n+'.png'))
shutil.copyfile(ROOT.parent/'Px32TrapV2/Misc/SlotSeal.png',ROOT/'Misc/SlotSeal.png')
(ROOT/'Sources/extraction.json').write_text(json.dumps(audit,indent=2),encoding='utf-8')
print('Saved 44 parts and 5 misc sprites.')
