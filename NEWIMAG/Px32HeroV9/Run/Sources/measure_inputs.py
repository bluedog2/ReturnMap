from PIL import Image
import numpy as np
from pathlib import Path
p=Path('NEWIMAG/Px32HeroV9/Run/Sources')
for name,path in [('Reference','NEWIMAG/Px32HeroRef/Turnaround/Hero_ThreeQuarter.png')]:
 a=np.array(Image.open(path).convert('RGBA'));Image.fromarray(a.repeat(6,0).repeat(6,1)).save(p/f'{name}_6x.png')
for i in [0,2]:
 a=np.array(Image.open(p/f'Original_{i}.png'));print(i,'top',np.where(a[:,:,3]>0)[0].min(),'bottom',np.where(a[:,:,3]>0)[0].max(),'colors',len(np.unique(a[a[:,:,3]>0,:3],axis=0)))
