from pathlib import Path
import numpy as np
from PIL import Image
R=Path('NEWIMAG/Px32HeroV9/Run');M=R.parent/'Move'
def read(p):return np.array(Image.open(p).convert('RGBA'))
def shift(a,dx,dy):
 b=np.zeros_like(a);x0=max(0,dx);x1=min(128,128+dx);y0=max(0,dy);y1=min(128,128+dy);b[y0:y1,x0:x1]=a[y0-dy:y1-dy,x0-dx:x1-dx];return b
mask=np.array(Image.open(R/'Masks/HeadBundle_core.png'))>0;hm=shift(mask,-20,-4);a=read(M/'Sources/prev2/Turn_F0.png');ref=shift(read(R/'Run_Key0.png'),-20,-4);a[hm]=ref[hm]
seam=shift(np.array(Image.open(R/'Masks/HeadBundle_00_seam.png'))>0,-20,-4)
for p,v in [(M/'Sources/HeadBundle_Turn_draft.png',a),(M/'Sources/HeadBundle_Turn_input8x.png',a.repeat(8,0).repeat(8,1)),(M/'Masks/HeadBundle_Turn_seam.png',seam*255),(M/'Masks/HeadBundle_Turn_edit.png',(seam|hm)*255)]:Image.fromarray(v.astype('uint8')).save(p)
