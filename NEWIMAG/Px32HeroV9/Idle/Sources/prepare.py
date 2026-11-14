from pathlib import Path
import numpy as np
from PIL import Image
import json, shutil
ROOT=Path(__file__).resolve().parents[4]
OUT=Path(__file__).resolve().parents[1]
REF=ROOT/'NEWIMAG/Px32HeroRef'
def read(p): return np.array(Image.open(p).convert('RGBA'))
def save(p,a): Image.fromarray(a.astype('uint8')).save(OUT/p)
def zoom(a,n=3): return np.repeat(np.repeat(a,n,0),n,1)
def shift(a,dx,dy):
 b=np.zeros_like(a); b[max(0,dy):min(128,128+dy),max(0,dx):min(128,128+dx)]=a[max(0,-dy):min(128,128-dy),max(0,-dx):min(128,128-dx)]; return b
raw=read(ROOT/'Assets/_Project/ResourcceEX/Sprites/Px32/Player/Hero_Idle.png')
orig=list(np.split(raw,6,axis=1)); master=read(REF/'Arms/Arm_HandOnHip.png')
shutil.copyfile(REF/'Arms/Arm_HandOnHip.png',OUT/'Hero_Idle_Master.png')
edit=np.any(master!=orig[0],axis=2)
save('Masks/Master_edit.png',edit.astype('uint8')*255)
scores=[]; moves=[]
for i,o in enumerate(orig):
 candidates=[]
 for dy in range(-3,4):
  for dx in range(-3,4):
   a=shift(orig[0],dx,dy)
   # Visible torso/hip belt, excluding held bag and front arm.
   mask=np.zeros((128,128),bool); mask[78:98,66:87]=True
   mask &= (a[:,:,3]>0)|(o[:,:,3]>0)
   err=int(np.any(a!=o,axis=2)[mask].sum())
   candidates.append((err,abs(dx)+abs(dy),dx,dy))
 candidates.sort(); _,_,dx,dy=candidates[0]; moves.append([dx,dy]);scores.append(candidates[:8])
for i,o in enumerate(orig): save(f'Sources/Original_{i}.png',o)
save('Sources/Original_review_3x.png',zoom(raw))
(OUT/'Sources/measurements.json').write_text(json.dumps(dict(moves=moves,scores=scores),indent=2))
print('moves',moves,'best', [s[0] for s in scores])
print('diff bbox',np.where(edit)[1].min(),np.where(edit)[0].min(),np.where(edit)[1].max(),np.where(edit)[0].max())
