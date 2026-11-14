from pathlib import Path
import numpy as np,json,sys
from PIL import Image
O=Path(__file__).resolve().parents[1];S=O/'Sources';ROOT=O.parents[2]
def read(p):return np.array(Image.open(p).convert('RGBA'))
def save(p,a):Image.fromarray(a.astype('uint8')).save(O/p)
def zoom(a,n=3):return a.repeat(n,0).repeat(n,1)
idle=read(S/'Idle0.png');pal=np.unique(idle[idle[:,:,3]>0,:3],axis=0).astype('int32')
def recover(g):
 best=None
 for pitch in [8,9,10]:
  for oy in range(-32,9):
   for ox in range(-32,9):
    yy=np.clip(np.arange(113,128)*pitch+pitch//2+oy,0,len(g)-1);xx=np.clip(np.arange(40,100)*pitch+pitch//2+ox,0,g.shape[1]-1)
    a=g[yy[:,None],xx[None,:]];r=idle[113:128,40:100];u=(a[:,:,3]>=128)|(r[:,:,3]>0)
    e=float((((a[:,:,:3].astype(float)-r[:,:,:3])**2).mean(2)*u).sum()+np.sum((a[:,:,3]>=128)!=(r[:,:,3]>0))*50000)
    if best is None or e<best[0]:best=(e,pitch,ox,oy)
 e,p,x,y=best;yy=np.clip(np.arange(128)*p+p//2+y,0,len(g)-1);xx=np.clip(np.arange(128)*p+p//2+x,0,g.shape[1]-1)
 a=g[yy[:,None],xx[None,:]].copy();ids=((a[:,:,:3].astype('int32')[:,:,None,:]-pal[None,None])**2).sum(3).argmin(2)
 a[:,:,:3]=pal[ids];a[:,:,3]=(a[:,:,3]>=128)*255;a[a[:,:,3]==0]=0
 return a,dict(pitch=p,origin=[x,y],boot_registration_error=e)
if __name__=='__main__':
 name=sys.argv[1];a,r=recover(read(S/f'{name}_generated1.png'));save(f'Sources/{name}_blocks.png',a);save(f'Sources/{name}_blocks_3x.png',zoom(a));(S/f'{name}_recovery.json').write_text(json.dumps(r,indent=2));print(r)
