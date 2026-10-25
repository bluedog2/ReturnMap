"""Deterministic source-texel animation; all writes are confined beside this file."""
from pixel_io import *
import math

CONFIG={
 'Elite_Knight':dict(cell=64,height=48,source='knight',palette='1A151A 2A2B32 293145 53617E 928982 2B231B B0701C CEA362 19142E 302460 4B3478 7E2D94 8284A1 FFF4D6'),
 'Orc_Brute':dict(cell=96,height=64,source='orc',palette='1A151A 2B231B 3E6B3A 6E9A4A 716432 DEC28D FFF4D6 654138 A87B56 4D3720 2A2B32 6C665E 928982 A0AAA7 AF1F28')}
OUT=color('1A151A')
def blank(w,h=None): return [[CLEAR]*w for _ in range(h or w)]
def edge(a):
 old=[r[:] for r in a];h=len(a);w=len(a[0])
 for y,r in enumerate(old):
  for x,p in enumerate(r):
   if p[3] and any(xx<0 or yy<0 or xx>=w or yy>=h or not old[yy][xx][3] for xx,yy in ((x-1,y),(x+1,y),(x,y-1),(x,y+1))):a[y][x]=OUT
 return a
def affine(dst,src,a=1,b=0,c=0,d=1,tx=0,ty=0):
 det=a*d-b*c
 for y in range(len(dst)):
  for x in range(len(dst[0])):
   sx=round((d*(x-tx)-b*(y-ty))/det);sy=round((-c*(x-tx)+a*(y-ty))/det)
   if 0<=sy<len(src) and 0<=sx<len(src[0]) and src[sy][sx][3]:dst[y][x]=src[sy][sx]
def fit(a,cell,height=None):
 cut=crop(a,bbox(a));h=len(cut);w=len(cut[0])
 if height is not None and height!=h:
  cut=[cut[min(h-1,y*h//height)][:] for y in range(height)];h=height
 assert w<=cell-2,(w,cell)
 out=blank(cell);left=(cell-w)//2;top=cell-h
 assert top>=1
 for y,r in enumerate(cut):out[top+y][left:left+w]=r
 return edge(out)
def seed(name,cfg):
 src=readpng(ROOT/'sources'/(name+'.png'))
 cut=crop(src,bbox(src,lambda p:p[3]>=128))
 h=cfg['height'];w=round(len(cut[0])*h/len(cut))
 # Decode the newly generated enlarged pixel drawing into the native grid.
 a=down(cut,w,h,list(map(color,cfg['palette'].split())))
 out=fit(a,cfg['cell'],h)
 savepng(Path('bases')/(name+'_Idle_0.png'),out)
 savepng(Path('bases')/(name+'_Idle_0_8x.png'),up(out,8))
 return out
def animate(base,cfg):
 n=cfg['cell'];bb=bbox(base);x0,y0,x1,y1=bb;w=x1-x0+1;h=y1-y0+1
 # Partition the SAME native drawing into disjoint articulated cutouts.
 # Rear drapery, head, body, weapon/forearm, and two leg regions.
 layers={k:blank(n) for k in ('cape','head','body','weapon','rear','front')}
 split=x0+round(w*.53);hip=y0+round(h*.75)
 for y,r in enumerate(base):
  for x,p in enumerate(r):
   if not p[3]:continue
   if x>x0+w*.73 and y>y0+h*.4:key='weapon'
   elif cfg['source']=='knight' and x<x0+w*.30 and y>y0+h*.30:key='cape'
   elif y>=hip:key='rear' if x<split else 'front'
   elif y<y0+h*.32:key='head'
   else:key='body'
   layers[key][y][x]=p
 def pose(lean=0,breathe=0,stride=0,lift=0,weapon=0,flutter=0):
  out=blank(n*3);off=n
  for k in ('cape','rear','front','body','head','weapon'):
   src=layers[k];b=0;tx=off;ty=off;d=1
   if k in ('head','body','cape','weapon'):
    b=-lean;tx+=lean*(y0+h*.8);ty+=breathe
   if k=='body' and breathe:
    neck=y0+h*.32
    d=1-breathe/(hip-neck)
    ty=off+breathe-(d-1)*neck
   if k=='cape':b+=flutter/h;tx-=flutter*y0/h
   if k in ('rear','front'):
    sign=-1 if k=='rear' else 1
    b=sign*stride/max(1,y1-hip);tx-=b*hip
    if (stride>0 and k=='rear') or (stride<0 and k=='front'):
     d=1-lift/max(1,y1-hip);ty+=hip*(1-d)
   if k=='weapon':ty+=weapon
   affine(out,src,1,b,0,d,tx,ty)
  return out
 idle=[base,fit(pose(breathe=1,weapon=1,flutter=1),n,h),fit(pose(breathe=0,weapon=-1,flutter=2),n,h),fit(pose(breathe=-1,weapon=0,flutter=1),n,h)]
 run=[]
 for i,s in enumerate((-4,-2,2,4,2,-2)):
  run.append(fit(pose(.16,(0,1,0,0,1,0)[i],s*(1.4 if n==96 else 1), (0,3,2,0,3,2)[i],(1,0,-2,-1,0,2)[i],3),n,h))
 hit=[fit(pose(-.22,0,-2,0,-3,-2),n,h),fit(pose(-.08,0,1,0,-1,-1),n,h)]
 death=[]
 for i,angle in enumerate((0,-12,18,42,70,90)):
  p=pose(-.18,0,-2,0,-2,-1);cut=crop(p,bbox(p));cw=len(cut[0]);ch=len(cut)
  out=blank(n*3);t=math.radians(angle);co=math.cos(t);si=math.sin(t)
  affine(out,cut,co,-si,si,co,n-co*cw/2+si*ch/2,n-si*cw/2-co*ch/2)
  target=round(h*(1,.94,.77,.60,.46,.38)[i])
  death.append(fit(out,n,target))
 return {'Idle':idle,'Run':run,'Hit':hit,'Death':death}

def main():
 records={};allframes={}
 for name,cfg in CONFIG.items():
  base=seed(name,cfg)
  for anim,frames in animate(base,cfg).items():
   stem=name+'_'+anim;n=cfg['cell']
   sheet=[sum((f[y] for f in frames),[]) for y in range(n)]
   savepng(Path('_1x')/(stem+'.png'),sheet);savepng(Path('Enemies')/(stem+'.png'),up(sheet,8))
   allframes[stem]=frames
   records[stem]={'one_x':'_1x/'+stem+'.png','eight_x':'Enemies/'+stem+'.png','cell':n,'frames':len(frames),'fps':6 if anim=='Idle' else 10,'base':'bases/'+name+'_Idle_0.png'}
 # One row per sheet, with unchanged Hero Idle frame zero at the same baseline.
 hero=[r[:64] for r in readpng(ROOT.parent/'Px32Pilot2/_1x/Hero_Idle.png')]
 preview=blank(64+12+6*96,8*104)
 for row,(stem,frames) in enumerate(allframes.items()):
  bottom=(row+1)*104-1;n=len(frames[0])
  for y,r in enumerate(hero):preview[bottom-63+y][:64]=r
  for i,f in enumerate(frames):
   for y,r in enumerate(f):preview[bottom-n+1+y][76+i*n:76+(i+1)*n]=r
 savepng(Path('Preview_3x.png'),up(preview,3))
 sources={name:{'original_reference':'NEWIMAG/AnimationV2/'+cfg['source']+'/Idle_00.png','generated_source':'sources/'+name+'.png','generated_sha256':hashlib.sha256((ROOT/'sources'/(name+'.png')).read_bytes()).hexdigest()} for name,cfg in CONFIG.items()}
 manifest={'pipeline':'Built-in image_gen redraw from original references, decode enlarged new drawing to native pixel grid, fixed palette, native cutout articulation, exact nearest-neighbor 8x. Original high-resolution enemy art is never resized into a delivery.','palette':['#'+s for s in MASTER],'sources':sources,'files':records,'preview_row_order':list(records),'preview_hero':'NEWIMAG/Px32Pilot2/_1x/Hero_Idle.png frame 0','limitations':['Cutout animation retains source-side lighting; some joints use overlapping native texels.','Grounded run cycle has no airborne frames due to mandatory bottom-row anchoring.','Automated validation does not replace engine import or human motion review.']}
 promptfile=ROOT/'prompts.json'
 if promptfile.exists():manifest['image_gen_prompts']=json.loads(promptfile.read_text(encoding='utf8'))
 (ROOT/'generation.json').write_text(json.dumps(manifest,indent=2),encoding='utf8')
 print('Generated',len(records),'sheets')
if __name__=='__main__':main()
