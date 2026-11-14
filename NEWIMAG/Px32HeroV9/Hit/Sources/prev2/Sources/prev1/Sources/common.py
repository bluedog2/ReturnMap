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

