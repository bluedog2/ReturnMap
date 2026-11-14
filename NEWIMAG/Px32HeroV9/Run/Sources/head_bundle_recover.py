from pathlib import Path
from PIL import Image
import numpy as np,json
R=Path('NEWIMAG/Px32HeroV9/Run');M=R.parent/'Move'
def read(p):return np.array(Image.open(p).convert('RGBA'))
def save(p,a):Image.fromarray(a.astype('uint8')).save(p)
idle=read(R.parent/'Idle/Hero_Idle_8.png');pal=np.unique(idle[idle[:,:,3]>0,:3],axis=0).astype('int32')
meta={}
for tag in [f'{i:02}' for i in range(1,10)]+['Turn']:
 folder=M if tag=='Turn' else R
 a=read(folder/f'Sources/HeadBundle_{tag}_draft.png');g=read(folder/f'Sources/HeadBundle_{tag}_generated1.png')
 protect=np.zeros((128,128),bool);protect[25:78,64:110]=1
 if tag=='Turn':protect[:]=0;protect[22:73,44:88]=1
 best=None
 for pitch in [9,10]:
  for oy in range(-24,5):
   for ox in range(-24,5):
    ys=np.clip(np.arange(128)*pitch+pitch//2+oy,0,len(g)-1);xs=np.clip(np.arange(128)*pitch+pitch//2+ox,0,g.shape[1]-1);b=g[ys[:,None],xs[None,:]]
    s=b[protect].astype(float);t=a[protect].astype(float);u=(s[:,3]>127)|(t[:,3]>0)
    e=(((s[:,:3]-t[:,:3])**2).mean(1)*u).sum()+((s[:,3]>127)!=(t[:,3]>0)).sum()*50000
    if best is None or e<best[0]:best=(e,pitch,ox,oy,b.copy())
 e,p,ox,oy,b=best;idx=((b[:,:,:3].astype('int32')[:,:,None,:]-pal[None,None,:,:])**2).sum(3).argmin(2);b[:,:,:3]=pal[idx];b[:,:,3]=(b[:,:,3]>=128)*255;b[b[:,:,3]==0]=0
 save(folder/f'Sources/HeadBundle_{tag}_blocks.png',b)
 mask=np.array(Image.open(folder/f'Masks/HeadBundle_{tag}_seam.png'))>0;a[mask]=b[mask];save(folder/f'Sources/HeadBundle_{tag}_final.png',a)
 meta[tag]={'pitch':p,'origin':[ox,oy],'registration_error':e,'attempts':1}
save(R/'Sources/HeadBundle_00_final.png',read(R/'Sources/HeadBundle_00_draft.png'))
(R/'Sources/HeadBundle_recovery.json').write_text(json.dumps(meta,indent=2))
old=read(R/'Sources/prev3/Hero_Run_10.png');fs=[read(R/f'Sources/HeadBundle_{i:02}_final.png') for i in range(10)]
strips=[np.concatenate([old[18:68,i*128+45:i*128+96] for i in range(10)],1),np.concatenate([a[18:68,45:96] for a in fs],1)]
save(R/'HeadBack_6x.png',np.concatenate(strips,0).repeat(6,0).repeat(6,1))
save(R/'Sources/HeadBundle_review3x.png',np.concatenate([np.concatenate(fs[:5],1),np.concatenate(fs[5:],1)],0).repeat(3,0).repeat(3,1))
print(meta)
