from pathlib import Path
import shutil,json
import numpy as np
from PIL import Image
R=Path('NEWIMAG/Px32HeroV9/Run');M=R.parent/'Move'
for folder,tag in [(R,'prev3' if not (R/'Sources/prev3').exists() else 'prev4'),(M,'prev2')]:
 b=folder/'Sources'/tag
 if b.exists():raise RuntimeError(str(b)+' exists')
 b.mkdir()
 for p in folder.iterdir():
  if p.is_file():shutil.copy2(p,b/p.name)
 shutil.copytree(folder/'Masks',b/'Masks')
 shutil.copytree(folder/'Sources',b/'Sources',ignore=shutil.ignore_patterns('prev*'))
(R/'Sources/head_bundle_backup.json').write_text(json.dumps({'run':str(R/'Sources'/('prev4' if (R/'Sources/prev4').exists() else 'prev3')),'move':str(M/'Sources/prev2')}))
def read(p):return np.array(Image.open(p).convert('RGBA'))
def save(p,a):Image.fromarray(a.astype('uint8')).save(p)
def shift(a,dy):
 b=np.zeros_like(a)
 if dy<0:b[:dy]=a[-dy:]
 else:b[dy:]=a[:128-dy]
 return b
mask=np.array(Image.open(R/'Masks/FaceLock_core.png'))>0
mask[20:64,60:111]=1
save(R/'Masks/HeadBundle_core.png',mask*255)
ref=read(R/'Run_Key0.png');sheet=read(R/'Hero_Run_10.png');ds=[0,0,-1,0,-2,-2,-2,-1,-2,-2]
for i,dy in enumerate(ds):
 a=sheet[:,i*128:(i+1)*128].copy();hm=shift(mask,dy);edit=mask|hm;t=shift(ref,dy);a[edit]=t[edit]
 seam=np.zeros((128,128),bool);seam[22+dy:65+dy,51:63]=1;seam &= ~hm
 save(R/f'Masks/HeadBundle_{i:02}_seam.png',seam*255);save(R/f'Masks/HeadBundle_{i:02}_edit.png',(edit|seam)*255)
 save(R/f'Sources/HeadBundle_{i:02}_draft.png',a);save(R/f'Sources/HeadBundle_{i:02}_input8x.png',a.repeat(8,0).repeat(8,1))
