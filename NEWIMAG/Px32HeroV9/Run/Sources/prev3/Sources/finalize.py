from pathlib import Path
from PIL import Image,ImageDraw
import numpy as np,json,hashlib
O=Path('NEWIMAG/Px32HeroV9/Run');S=O/'Sources';R=Path('NEWIMAG/Px32HeroRef')
def read(p):return np.array(Image.open(p).convert('RGBA'))
def save(p,a):Image.fromarray(a.astype('uint8')).save(O/p)
def zoom(a,n):return a.repeat(n,0).repeat(n,1)
def poly(points):
 im=Image.new('L',(128,128));ImageDraw.Draw(im).polygon(points,fill=255);return np.array(im)>0
def put(a,b,x,y):
 a=a.copy();v=a[y:y+b.shape[0],x:x+b.shape[1]];m=b[:,:,3]>0;v[m]=b[m];return a
bag=read(R/'Bag/Bag_Hip_ThreeQuarter.png');chain=read(R/'Sources/R2_chain.png')[:6]
idle=read('Assets/_Project/ResourcceEX/Sprites/Px32/Player/Hero_Idle.png');pal={tuple(c) for c in idle[idle[:,:,3]>0,:3]}
settings={0:dict(hip=[63,96],delta=[-1,6],bounce=1),2:dict(hip=[61,93],delta=[-3,3],bounce=-1)}
result={};originals=[];finals=[]
for i in [0,2]:
 orig=read(S/f'Original_{i}.png');base=read(S/f'Key{i}_bagless.png');s=settings[i];dx,dy=s['delta'];dy+=s['bounce'];bx,by=53+dx,95+dy;cx,cy=58+dx,89+dy
 layer=put(np.zeros_like(base),chain,cx,cy);layer=put(layer,bag,bx,by)
 # The camera-near leg and new swinging glove/sleeve are in front of the hip bag.
 leg=poly([(65,98),(82,101),(84,111),(77,119),(64,116),(63,108)]) if i==0 else poly([(68,96),(85,103),(92,118),(78,121),(66,105)])
 arm=poly([(45,82),(59,84),(64,90),(62,96),(54,96),(46,91)]) if i==2 else poly([(29,86),(43,84),(48,88),(41,94),(30,94)])
 occ=(leg|arm)&(base[:,:,3]>0);bm=(layer[:,:,3]>0)&~occ
 a=base.copy();a[bm]=layer[bm]
 m=np.array(Image.open(O/f'Masks/Key{i}_generation.png'))>0;m|=bm
 save(f'Run_Key{i}.png',a);save(f'Masks/Key{i}_edit.png',m.astype('uint8')*255);save(f'Masks/Key{i}_occlusion.png',occ.astype('uint8')*255);save(f'Sources/Key{i}_bag_layer_full.png',layer);save(f'Sources/Key{i}_review_3x.png',zoom(a,3))
 ys=np.where(a[:,:,3]>0)[0];cols={tuple(c) for c in a[a[:,:,3]>0,:3]}
 result[i]={**s,'reference_hip':[64,90],'bag_translation':[bx,by],'chain_translation':[cx,cy],'chain_length':6,'bag_hidden_pixels':int(((layer[:,:,3]>0)&occ).sum()),'size':[128,128],'colors':len(cols),'outside_idle_palette':len(cols-pal),'alpha':list(map(int,np.unique(a[:,:,3]))),'head_top':int(ys.min()),'original_head_top':int(np.where(orig[:,:,3]>0)[0].min()),'bottom':int(ys.max()),'outside_mask_mismatch':int((np.any(a!=orig,2)&~m).sum()),'head_region_mismatch':int(np.any(a[:77]!=orig[:77],2).sum()),'front_arm_region_mismatch':int(np.any(a[80:99,80:104]!=orig[80:99,80:104],2).sum()),'floor_row_mismatch':int(np.any(a[127]!=orig[127],1).sum()),'visible_bag_pixel_mismatch':int(np.any(a[bm]!=layer[bm],1).sum())}
 originals.append(orig);finals.append(a)
# Requested order: original 0, original 2, new 0, new 2.
ordered=originals+finals
save('Keys_3x.png',zoom(np.concatenate(ordered,1),3));save('Keys_8x_hip.png',zoom(np.concatenate([a[78:120,24:96] for a in ordered],1),8))
(S/'placement.json').write_text(json.dumps(settings,ensure_ascii=False,indent=2),encoding='utf-8');(O/'validation.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(result,indent=2))
