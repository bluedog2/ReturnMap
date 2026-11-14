from pathlib import Path
from PIL import Image,ImageDraw
import numpy as np,json,hashlib
OUT=Path('NEWIMAG/Px32HeroV9/Run');SRC=OUT/'Sources';REF=Path('NEWIMAG/Px32HeroRef')
def read(p):return np.array(Image.open(p).convert('RGBA'))
def save(p,a):Image.fromarray(a.astype('uint8')).save(OUT/p)
def zoom(a,n):return a.repeat(n,0).repeat(n,1)
def poly(points):
 im=Image.new('L',(128,128));ImageDraw.Draw(im).polygon(points,fill=255);return np.array(im)>0
idle=read('Assets/_Project/ResourcceEX/Sprites/Px32/Player/Hero_Idle.png');pal=np.unique(idle[idle[:,:,3]>0,:3],axis=0).astype('int32')
def mapping(a):
 b=a.copy();ids=((a[:,:,:3].astype('int32')[:,:,None,:]-pal[None,None,:,:])**2).sum(3).argmin(2);b[:,:,:3]=pal[ids];b[:,:,3]=(a[:,:,3]>=128)*255;b[b[:,:,3]==0]=0;return b
meta={};outputs=[];originals=[]
for i in [0,2]:
 o=read(SRC/f'Original_{i}.png');g=read(SRC/f'Key{i}_generated1.png');best=None
 for pitch in [9,10]:
  for oy in range(-24,5):
   for ox in range(-24,5):
    ys=np.arange(128)*pitch+pitch//2+oy;xs=np.arange(128)*pitch+pitch//2+ox
    a=g[np.clip(ys,0,len(g)-1)[:,None],np.clip(xs,0,g.shape[1]-1)[None,:]]
    s=a[26:76,20:108];r=o[26:76,20:108];u=(s[:,:,3]>127)|(r[:,:,3]>0)
    e=float((((s[:,:,:3].astype(float)-r[:,:,:3])**2).mean(2)*u).sum()+np.sum((s[:,:,3]>127)!=(r[:,:,3]>0))*50000)
    if best is None or e<best[0]:best=(e,pitch,ox,oy,a.copy())
 e,p,ox,oy,a=best;a=mapping(a);save(f'Sources/Key{i}_blocks.png',a)
 # Masks select replacement of old rear arm/held bag only, preserving visible hair, torso and legs.
 m=poly([(48,77),(57,78),(66,82),(66,87),(61,94),(57,99),(52,104),(50,109 if i==0 else 103),(29,109 if i==0 else 103),(27,89),(34,86),(39,83),(43,81)])
 if i==2:m[98:,56:]=False
 else:m[103:,57:]=False
 base=o.copy();base[m]=a[m];save(f'Masks/Key{i}_generation.png',m.astype('uint8')*255);save(f'Sources/Key{i}_bagless.png',base);save(f'Sources/Key{i}_bagless_3x.png',zoom(base,3))
 meta[i]={'pitch':p,'origin':[ox,oy],'head_registration_error':e};outputs.append(base);originals.append(o)
save('Sources/Bagless_compare_3x.png',zoom(np.concatenate([originals[0],outputs[0],originals[1],outputs[1]],1),3))
(SRC/'recovery.json').write_text(json.dumps(meta,indent=2))
print(meta)
