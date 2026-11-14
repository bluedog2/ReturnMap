from pathlib import Path
from PIL import Image,ImageDraw
import numpy as np,json,hashlib,sys
O=Path(__file__).resolve().parents[1]; S=O/'Sources'; ROOT=O.parents[2]
def read(p): return np.array(Image.open(p).convert('RGBA'))
def save(p,a): Image.fromarray(a.astype('uint8')).save(O/p)
def dump(p,a): (O/p).write_text(json.dumps(a,ensure_ascii=False,indent=2),encoding='utf-8')
def zoom(a,n):return a.repeat(n,0).repeat(n,1)
def poly(points):
 im=Image.new('L',(128,128)); ImageDraw.Draw(im).polygon(points,fill=255);return np.array(im)>0
idle=read(ROOT/'NEWIMAG/Px32HeroV9/Idle/Hero_Idle_8.png');PAL=np.unique(idle[idle[:,:,3]>0,:3],axis=0).astype('int32')
def recover(g,o,top,bottom):
 best=None
 for pitch in [9,10]:
  for oy in range(-30,6):
   for ox in range(-30,6):
    ys=np.arange(top,bottom)*pitch+pitch//2+oy;xs=np.arange(20,105)*pitch+pitch//2+ox
    a=g[np.clip(ys,0,len(g)-1)[:,None],np.clip(xs,0,g.shape[1]-1)[None,:]];r=o[top:bottom,20:105];u=(a[:,:,3]>127)|(r[:,:,3]>0)
    e=float((((a[:,:,:3].astype(float)-r[:,:,:3])**2).mean(2)*u).sum()+np.sum((a[:,:,3]>127)!=(r[:,:,3]>0))*50000)
    if best is None or e<best[0]:best=(e,pitch,ox,oy)
 e,p,x,y=best;ys=np.clip(np.arange(128)*p+p//2+y,0,len(g)-1);xs=np.clip(np.arange(128)*p+p//2+x,0,g.shape[1]-1);a=g[ys[:,None],xs[None,:]].copy()
 ids=((a[:,:,:3].astype('int32')[:,:,None,:]-PAL[None,None,:,:])**2).sum(3).argmin(2);a[:,:,:3]=PAL[ids];a[:,:,3]=(a[:,:,3]>=128)*255;a[a[:,:,3]==0]=0
 return a,dict(pitch=p,origin=[x,y],head_registration_error=e)
bag=read(ROOT/'NEWIMAG/Px32HeroRef/Bag/Bag_Hip_ThreeQuarter.png');chain=read(ROOT/'NEWIMAG/Px32HeroRef/Sources/R2_chain.png')[:6]
def layer(pos):
 a=np.zeros((128,128,4),dtype='uint8');x,y=pos
 for b,xx,yy in [(chain,x+5,y-6),(bag,x,y)]:
  v=a[yy:yy+len(b),xx:xx+b.shape[1]];m=b[:,:,3]>0;v[m]=b[m]
 return a

# Anatomical regions specified before compositing; head/legs remain original.
LAND=[
 [[(18,94),(45,94),(55,98),(68,98),(69,108),(63,116),(54,124),(33,124),(30,112)],[(78,104),(93,104),(105,111),(105,125),(94,127),(78,121)]],
 [[(23,78),(50,77),(57,76),(69,79),(69,91),(61,99),(55,109),(33,109)],[(78,87),(91,87),(105,93),(105,104),(88,106),(79,99)]],
 [[(38,75),(53,73),(56,70),(68,72),(71,80),(67,91),(60,100),(59,105),(38,105)],[(79,79),(91,82),(98,91),(98,103),(85,104),(79,95)]]]
DASH=[
 [[(29,84),(40,84),(45,76),(56,75),(66,78),(67,90),(60,97),(54,106),(34,106)]],
 [[(30,83),(44,81),(48,76),(58,74),(70,76),(73,86),(65,94),(57,104),(35,106)]],
 [[(35,89),(48,87),(52,82),(63,81),(75,82),(79,90),(69,98),(62,110),(42,110)]],
 [[(34,87),(48,85),(53,80),(63,79),(75,80),(79,89),(69,97),(61,109),(42,109)]],
 [[(36,79),(46,77),(51,71),(60,70),(69,74),(71,83),(64,91),(59,102),(40,102)]]]
def run(kind):
 global O,S
 O=ROOT/'NEWIMAG/Px32HeroV9'/kind;S=O/'Sources'
 ps=LAND if kind=='Land' else DASH
 for i,polys in enumerate(ps):
  if not (S/f'F{i}_generated1.png').exists():continue
  orig=read(S/f'Original_{i}.png');top=int(np.where(orig[:,:,3]>0)[0].min())
  b,r=recover(read(S/f'F{i}_generated1.png'),orig,top,min(top+35,74))
  m=np.logical_or.reduce([poly(p) for p in polys]);base=orig.copy();base[m]=b[m]
  for name,a in [(f'Sources/F{i}_blocks.png',b),(f'Sources/F{i}_bagless.png',base),(f'Sources/F{i}_bagless_10x.png',zoom(base,10)),(f'Masks/F{i}_generation.png',m*255)]:save(name,a)
  dump(f'Sources/F{i}_recovery.json',r)
 fs=[read(S/f'F{i}_bagless.png') for i in range(len(ps)) if (S/f'F{i}_bagless.png').exists()]
 if fs:
  save('Sources/Bagless_3x.png',zoom(np.concatenate(fs,1),3))
  print(kind,[int(np.any(fs[i]!=fs[i+1],2).sum()) for i in range(len(fs)-1)])
if __name__=='__main__':run(sys.argv[1])
