# Claude 조정(2026-11-07): I0 가방이 웅크린 몸의 가슴~허리 높이라 미통과 → F0 가방 자리(같은 웅크린 몸)로 옮김.
# 몸보다 1장 늦게 따라가는 규칙: F0→I0 에서 가방은 아직 F0 높이에 머무른다. 새 픽셀 없음(복원은 F0_bagless, 가방은 F0 레이어 평행이동 0).
import numpy as np
from PIL import Image
from pathlib import Path
R=Path(__file__).resolve().parents[1]
def rd(p):return np.array(Image.open(R/p).convert('RGBA'))
i0=rd('Sources/prev1/JumpStart_I0-1.png');old=rd('Sources/I0_bag_layer.png')[:,:,3]>0
f0b=rd('Sources/F0_bagless.png');f0l=rd('Sources/F0_bag_layer.png');new=f0l[:,:,3]>0
a=i0.copy();a[old]=f0b[old];a[new]=f0l[new]
Image.fromarray(a).save(R/'JumpStart_I0-1.png')
order=['JumpStart_F0.png','JumpStart_I0-1.png','JumpStart_F1.png','JumpStart_F2.png']
fr=[rd(o) for o in order];sheet=np.concatenate(fr,1)
Image.fromarray(sheet).save(R/'Hero_JumpStart_4.png')
wb=lambda x:np.where(x[:,:,3:]>0,x,255).astype(np.uint8)
Image.fromarray(wb(sheet).repeat(3,0).repeat(3,1)).save(R/'Hero_JumpStart_4_3x.png')
Image.fromarray(np.concatenate([wb(f)[60:120,20:110] for f in fr],1).repeat(8,0).repeat(8,1)).save(R/'Waist_4_8x.png')
ims=[Image.fromarray(wb(f).repeat(3,0).repeat(3,1)).convert('P',palette=Image.ADAPTIVE,colors=255) for f in fr]
ims[0].save(R/'Hero_JumpStart_4_3x.gif',save_all=True,append_images=ims[1:],duration=50,loop=0,disposal=2)
print('colors',len(np.unique(a[a[:,:,3]>0,:3],axis=0)),'floor',np.where(a[:,:,3]>0)[0].max(),'changed',int(np.any(a!=i0,axis=2).sum()))
