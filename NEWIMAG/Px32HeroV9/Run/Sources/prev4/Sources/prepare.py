from PIL import Image
import numpy as np
from pathlib import Path
p=Path('NEWIMAG/Px32HeroV9/Run/Sources')
a=np.array(Image.open('Assets/_Project/ResourcceEX/Sprites/Px32/Player/Hero_Run.png').convert('RGBA'))
for i in [0,2]:
 b=a[:,i*128:(i+1)*128];Image.fromarray(b).save(p/f'Original_{i}.png');Image.fromarray(b.repeat(8,0).repeat(8,1)).save(p/f'Input_{i}_8x.png')
