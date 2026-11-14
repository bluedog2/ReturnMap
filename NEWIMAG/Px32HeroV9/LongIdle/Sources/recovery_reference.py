from pathlib import Path
import numpy as np,json,sys
from PIL import Image
O=Path(__file__).resolve().parents[1];S=O/'Sources'
def read(p):return np.array(Image.open(p).convert('RGBA'))
def save(p,a):Image.fromarray(a.astype('uint8')).save(O/p)
def zoom(a,n=3):return a.repeat(n,0).repeat(n,1)
idle=read(S/'Idle0.png');pal=np.unique(idle[idle[:,:,3]>0,:3],axis=0).astype('int32')
bag=read(S/'Approved_bag_chain_layer.png');vis=(bag[:,:,3]>0)&np.all(bag==idle,2)
def shift(a,dx,dy=0):
 b=np.zeros_like(a); x0=max(0,dx);x1=min(128,128+dx);y0=max(0,dy);y1=min(128,128+dy)
 b[y0:y1,x0:x1]=a[y0-dy:y1-dy,x0-dx:x1-dx];return b
def recover(g):
 best=None
 # Register immutable boots using integer pixel-block centres only.
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
def build(i,attempt=1):
 a,rec=recover(read(S/f'F{i}_generated{attempt}.png'));save(f'Sources/F{i}_blocks.png',a)
 mask=np.zeros((128,128),bool)
 if i<3:mask[27:98,20:111]=True;mask|=vis
 else:mask[55:70,65:89]=True
 # Hair apex and the lower body remain at approved row positions.
 out=idle.copy();out[mask]=a[mask]
 if i<3:
  layer=shift(bag,[1,2,2][i]);lm=layer[:,:,3]>0;out[lm]=layer[lm];mask|=lm
  save(f'Sources/F{i}_bag_layer.png',layer)
 save(f'Masks/F{i}_edit.png',mask*255);save(f'Hit_F{i}.png',out)
 save(f'Sources/F{i}_review_3x.png',zoom(np.concatenate([idle,out],1)))
 (S/f'F{i}_recovery.json').write_text(json.dumps(rec,indent=2))
 print(i,rec,'bbox',Image.fromarray(out).getbbox())
if __name__=='__main__':
 for i in map(int,sys.argv[1:]):build(i)
