from pathlib import Path
import json, hashlib
import numpy as np
from PIL import Image
R=Path(__file__).resolve().parents[1]; S=R/'Sources'
def read(p): return np.array(Image.open(p).convert('RGBA'))
def save(n,a): Image.fromarray(a.astype('uint8')).save(S/n)
def bbox(a):
 y,x=np.where(a[:,:,3]>0); return [int(x.min()),int(y.min()),int(x.max()+1),int(y.max()+1)]
protected=[p for d in ['Turnaround','Arms','Bag'] for p in (R/d).glob('*.png')]
before={str(p.relative_to(R)):hashlib.sha256(p.read_bytes()).hexdigest() for p in protected}
idle=read(S/'Idle0.png'); pal=np.unique(idle[idle[:,:,3]>0,:3],axis=0).astype(int)
g=read(S/'R4_connector_generated3.png')
# Recover centers of the generated coarse square blocks; no resize or rotation.
tile=np.zeros((12,7,4),np.uint8)
for y in range(12):
 for x in range(7):
  p=g[240+y*114,182+x*114]
  if p[3]>=128:
   tile[y,x]=[*pal[np.argmin(((pal-p[:3].astype(int))**2).sum(1))],255]
free=np.zeros((128,128,4),np.uint8); free[81:93,63:70]=tile
save('R4_connector_free.png',free)
old=read(S/'prev2/Turnaround/Hero_ThreeQuarter.png'); restore=read(S/'ThreeQuarter_no_bag.png')
m=read(S/'V9Fix_ThreeQuarter_bag_layer.png')[:,:,3]>0
bagless=old.copy(); bagless[m]=restore[m]
bag=np.zeros_like(free); b=read(R/'Bag/Bag_Hip_ThreeQuarter.png'); bag[92:92+b.shape[0],53:53+b.shape[1]]=b
chain=np.zeros_like(free); c=read(S/'R2_chain.png')[:3]; chain[89:92,58:60]=c
q2=read(S/'prev3/Turnaround/Hero_ThreeQuarter.png')
previews=[]; metrics={}
for name in ['Down','HandOnHip']:
 current=read(R/f'Arms/Arm_{name}.png'); clean=current.copy()
 src=read(S/f'prev3/Arms/Arm_{name}.png'); am=read(S/f'prev3/Masks/Arm_{name}.png')[:,:,0]>0
 armpx=am & np.any(src!=q2,axis=2) & (src[:,:,3]>0)
 # Original Down arm bounds plus existing pale jacket / red lining pixels.
 yy,xx=np.indices((128,128))
 occlusion=armpx if name!='Down' else ((xx>=44)&(xx<56)&(yy>=84)&(yy<102)&(bagless[:,:,3]>0))
 jacket=((bagless[:,:,:3].min(2)>95)|((bagless[:,:,0]>90)&(bagless[:,:,1]<100))) & (xx<=63)&(yy<93)
 occlusion=occlusion|jacket
 oldmask=(chain[:,:,3]>0)&~occlusion&(bag[:,:,3]==0)
 clean[oldmask]=bagless[oldmask]
 # Restore all old chain pixels unless a genuine foreground arm overlays them.
 oldmask=(chain[:,:,3]>0)&~armpx&(bag[:,:,3]==0)
 clean[oldmask]=bagless[oldmask]
 visible=free.copy(); visible[occlusion]=0
 if name=='Down': save('R4_connector_down.png',visible)
 draw=(visible[:,:,3]>0)&(bag[:,:,3]==0)
 result=clean.copy(); result[draw]=visible[draw]
 allowed=(chain[:,:,3]>0)|(free[:,:,3]>0)
 assert not np.any(np.any(result!=current,axis=2)&~allowed)
 assert not np.any(np.any(result!=clean,axis=2)&occlusion)
 assert not np.any(np.any(result!=clean,axis=2)&(bag[:,:,3]>0))
 metrics[name]={'layer_visible_pixels':int((visible[:,:,3]>0).sum()),'composited_pixels':int(draw.sum()),'visible_rows':np.where(draw.any(1))[0].tolist(),'outside_mask_mismatch':0,'bag_mismatch':0,'occluder_mismatch':0}
 previews.append(result[78:111,44:81])
preview=np.concatenate(previews,axis=1)
save('R4_preview_16x.png',preview.repeat(16,0).repeat(16,1))
save('R4_preview_3x.png',preview.repeat(3,0).repeat(3,1))
after={str(p.relative_to(R)):hashlib.sha256(p.read_bytes()).hexdigest() for p in protected}
assert before==after
report={'attempts':3,'selected_attempt':3,'generation':'built-in image_gen','recovery':{'origin_center':[182,240],'pitch':114,'grid':[7,12],'translation':[63,81],'method':'coarse block center recovery, nearest approved palette, binary alpha; no image resize/rotation'},'free_bbox':bbox(free),'free_pixels':int((free[:,:,3]>0).sum()),'colors':np.unique(free[free[:,:,3]>0,:3],axis=0).tolist(),'alpha':np.unique(free[:,:,3]).tolist(),'palette_valid':all(any(np.array_equal(c,p) for p in pal) for c in free[free[:,:,3]>0,:3]),'poses':metrics,'protected_files_unchanged':before==after,'protected_hashes':before,'preview_order':['Down','HandOnHip'],'preview_crop_inclusive':[44,78,80,110],'composition_order':'restore old chain from r3 bagless; connector behind jacket/arms and existing bag; never overwrite production files','self_assessment':'Pending visual review'}
(S/'R4_report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in report.items() if k!='protected_hashes'},ensure_ascii=False))

