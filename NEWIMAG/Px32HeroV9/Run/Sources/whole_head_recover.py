from pathlib import Path
from PIL import Image
import numpy as np,json,sys
R=Path(__file__).resolve().parents[1]
def read(p):return np.array(Image.open(p).convert('RGBA'))
def save(p,a):Image.fromarray(a.astype('uint8')).save(p)
palim=read(R.parent/'Idle/Hero_Idle_8.png');pal=np.unique(palim[palim[:,:,3]>0,:3],axis=0).astype('int32')
ds=[0,0,-1,0,-2,-2,-2,-1,-2,-2]
meta={}
mp=R/'Sources/WholeHead_recovery.json'
if mp.exists():meta=json.loads(mp.read_text())
for arg in sys.argv[1:]:
 i=int(arg);a=read(R/'Sources/prev4/Hero_Run_10.png')[:,128*i:128*(i+1)].copy();gp=sorted((R/'Sources').glob(f'WholeHead_{i:02}_generated*.png'))[-1];g=read(gp)
 protect=np.zeros((128,128),bool);protect[88:128,:]=True
 best=None
 for pitch in [9,10]:
  for oy in range(-25,6):
   for ox in range(-25,6):
    ys=np.clip(np.arange(128)*pitch+pitch//2+oy,0,len(g)-1);xs=np.clip(np.arange(128)*pitch+pitch//2+ox,0,g.shape[1]-1);b=g[ys[:,None],xs[None,:]]
    s=b[protect].astype(float);t=a[protect].astype(float);u=(s[:,3]>127)|(t[:,3]>0)
    e=(((s[:,:3]-t[:,:3])**2).mean(1)*u).sum()+((s[:,3]>127)!=(t[:,3]>0)).sum()*50000
    if best is None or e<best[0]:best=(e,pitch,ox,oy,b.copy())
 e,p,ox,oy,b=best;idx=((b[:,:,:3].astype('int32')[:,:,None,:]-pal[None,None,:,:])**2).sum(3).argmin(2);b[:,:,:3]=pal[idx];b[:,:,3]=(b[:,:,3]>=128)*255;b[b[:,:,3]==0]=0
 # One continuous whole-head mask, defined from the existing collar boundary before compositing.
 mask=np.zeros((128,128),bool)
 for x0,x1,y in [(0,30,87),(30,38,82),(38,48,79),(48,60,78),(60,68,80),(68,76,80),(76,82,82),(82,94,84),(94,104,82),(104,128,81)]:mask[:y+ds[i],x0:x1]=True
 # Register the complete generated head by integer translation only; never resize or splice head components.
 top_old=int(np.where(a[:,:,3]>0)[0].min());top_gen=int(np.where(b[:75,:,3]>0)[0].min());dy=top_old-top_gen
 moved=np.zeros_like(b)
 if dy<0:moved[:dy]=b[-dy:]
 elif dy>0:moved[dy:]=b[:-dy]
 else:moved=b.copy()
 b=moved
 out=a.copy();out[mask]=b[mask]
 save(R/f'Masks/WholeHead_{i:02}_edit.png',mask*255);save(R/f'Sources/WholeHead_{i:02}_blocks.png',b);save(R/f'Sources/WholeHead_{i:02}_final.png',out)
 save(R/f'Sources/WholeHead_{i:02}_review6x.png',np.concatenate([a[18:88,15:112],out[18:88,15:112]],1).repeat(6,0).repeat(6,1))
 meta[arg]={'source':gp.name,'pitch':p,'origin':[ox,oy],'whole_head_translation':[0,dy],'error':float(e),'old_top':top_old,'new_top':int(np.where(out[:,:,3]>0)[0].min()),'outside_mask_mismatch':int((np.any(out!=a,2)&~mask).sum())}
mp.write_text(json.dumps(meta,indent=2));print(meta)
