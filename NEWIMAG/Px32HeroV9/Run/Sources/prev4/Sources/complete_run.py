from pathlib import Path
from PIL import Image, ImageDraw
import numpy as np, json, hashlib
O=Path(__file__).resolve().parents[1]; S=O/'Sources'
ROOT=O.parents[2]
def read(p): return np.array(Image.open(p).convert('RGBA'))
def save(p,a): Image.fromarray(a.astype('uint8')).save(O/p)
def dump(p,v): (O/p).write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf-8')
def zoom(a,n): return a.repeat(n,0).repeat(n,1)
def poly(points):
 im=Image.new('L',(128,128)); ImageDraw.Draw(im).polygon(points,fill=255); return np.array(im)>0
idle=read(ROOT/'Assets/_Project/ResourcceEX/Sprites/Px32/Player/Hero_Idle.png'); PAL=np.unique(idle[idle[:,:,3]>0,:3],axis=0).astype('int32')
def recover(g,o):
 best=None
 for pitch in [9,10]:
  for oy in range(-30,6):
   for ox in range(-30,6):
    ys=np.arange(26,76)*pitch+pitch//2+oy; xs=np.arange(20,108)*pitch+pitch//2+ox
    a=g[np.clip(ys,0,len(g)-1)[:,None],np.clip(xs,0,g.shape[1]-1)[None,:]]; r=o[26:76,20:108]; u=(a[:,:,3]>127)|(r[:,:,3]>0)
    e=float((((a[:,:,:3].astype(float)-r[:,:,:3])**2).mean(2)*u).sum()+np.sum((a[:,:,3]>127)!=(r[:,:,3]>0))*50000)
    if best is None or e<best[0]: best=(e,pitch,ox,oy)
 e,p,ox,oy=best; ys=np.clip(np.arange(128)*p+p//2+oy,0,len(g)-1); xs=np.clip(np.arange(128)*p+p//2+ox,0,g.shape[1]-1); a=g[ys[:,None],xs[None,:]].copy()
 ids=((a[:,:,:3].astype('int32')[:,:,None,:]-PAL[None,None,:,:])**2).sum(3).argmin(2); a[:,:,:3]=PAL[ids]; a[:,:,3]=(a[:,:,3]>=128)*255; a[a[:,:,3]==0]=0
 return a,dict(pitch=p,origin=[ox,oy],head_registration_error=e)
def put(a,b,x,y):
 v=a[y:y+b.shape[0],x:x+b.shape[1]]; m=b[:,:,3]>0; v[m]=b[m]
bag=read(ROOT/'NEWIMAG/Px32HeroRef/Bag/Bag_Hip_ThreeQuarter.png'); chain=read(ROOT/'NEWIMAG/Px32HeroRef/Sources/R2_chain.png')[:6]
hips=[[63,96],[61,93],[61,93],[62,95],[63,94],[62,92],[63,94],[63,96]]; bounce=[1,0,-1,0,1,0,-1,0]
def layer_at(pos):
 l=np.zeros((128,128,4),dtype='uint8'); x,y=pos; put(l,chain,x+5,y-6);put(l,bag,x,y); return l
def main():
 meta={}; frames=[]
 for i in range(8):
  o=read(S/f'Original_{i}.png'); prev=hips[(i-1)%8]; pos=[prev[0]-11,prev[1]-2+bounce[i]]
  if i in [0,2]:
   a=read(O/f'Run_Key{i}.png'); base=read(S/f'Key{i}_bagless.png'); m=np.array(Image.open(O/f'Masks/Key{i}_generation.png'))>0; rec=None
  else:
   attempt=2 if i in [4,5] else 1
   b,rec=recover(read(S/f'F{i}_generated{attempt}.png'),o);save(f'Sources/F{i}_blocks.png',b)
   bottom={1:112,3:108,4:108,5:106,6:108,7:102}[i]
   m=poly([(48,77),(57,78),(66,82),(66,87),(61,94),(57,99),(52,104),(50,bottom),(27,bottom),(23,88),(34,84),(43,80)])
   # Protect visible legs/shoes below old suitcase and to its right.
   m[99:,57:]=False
   if i==7: m[97:,54:]=False; m[103:]=False
   base=o.copy();base[m]=b[m];save(f'Sources/F{i}_bagless.png',base)
   save(f'Masks/F{i}_generation.png',m.astype('uint8')*255)
   a=base.copy();l=layer_at(pos);bm=l[:,:,3]>0;a[bm]=l[bm];save(f'Run_F{i}.png',a)
  l=layer_at(pos); bm=l[:,:,3]>0;edit=m|bm;save(f'Masks/F{i}_edit.png',edit.astype('uint8')*255);save(f'Sources/F{i}_bag_layer.png',l);save(f'Sources/F{i}_review_3x.png',zoom(a,3))
  meta[i]=dict(hip=hips[i],previous_hip=prev,bounce=bounce[i],bag_position=pos,chain_position=[pos[0]+5,pos[1]-6],recovery=rec,outside_mask_mismatch=int((np.any(a!=o,2)&~edit).sum()),colors=len(np.unique(a[a[:,:,3]>0,:3],axis=0)),head_top=int(np.where(a[:,:,3]>0)[0].min()),floor=int(np.where(a[:,:,3]>0)[0].max()),head_mismatch=int(np.any(a[:77]!=o[:77],2).sum()),front_arm_mismatch=int(np.any(a[80:99,80:104]!=o[80:99,80:104],2).sum()))
  frames.append(a)
 save('Hero_Run_8.png',np.concatenate(frames,1));save('Hero_Run_8_3x.png',zoom(np.concatenate(frames,1),3));save('Waist_8_8x.png',zoom(np.concatenate([a[74:112,20:100] for a in frames],1),8))
 changes=[int(np.any(frames[i]!=frames[(i+1)%8],2).sum()) for i in range(8)];dump('Sources/run8_measurements.json',dict(frames=meta,cyclic_changes=changes,selected_intervals=sorted(sorted(range(8),key=lambda i:changes[i],reverse=True)[:2])))
 print(json.dumps(dict(frames=meta,cyclic_changes=changes),indent=2))
if __name__=='__main__':main()
