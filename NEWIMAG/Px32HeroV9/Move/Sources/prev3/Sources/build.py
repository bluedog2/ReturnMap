from pathlib import Path
from PIL import Image, ImageSequence
import numpy as np, json, hashlib, importlib.util
O=Path(__file__).resolve().parents[1]
S=O/'Sources'
ROOT=O.parents[2]
def read(p): return np.array(Image.open(p).convert('RGBA'))
def save(p,a): Image.fromarray(a.astype('uint8')).save(O/p)
def dump(p,d): (O/p).write_text(json.dumps(d,ensure_ascii=False,indent=2),encoding='utf-8')
def zoom(a,n=3): return a.repeat(n,0).repeat(n,1)
idle_sheet=read(O.parent/'Idle/Hero_Idle_8.png')
run_sheet=read(O.parent/'Run/Hero_Run_10.png')
idle=[idle_sheet[:,i*128:(i+1)*128].copy() for i in range(8)]
run=[run_sheet[:,i*128:(i+1)*128].copy() for i in range(10)]
PAL=np.unique(idle_sheet[idle_sheet[:,:,3]>0,:3],axis=0).astype('int32')
def shift(a,dx=0,dy=0):
 b=np.zeros_like(a);h,w=a.shape[:2];x0=max(0,dx);x1=min(w,w+dx);y0=max(0,dy);y1=min(h,h+dy)
 b[y0:y1,x0:x1]=a[y0-dy:y1-dy,x0-dx:x1-dx];return b
def recover(name,pitch=10,ox=-17,oy=-11):
 g=read(S/(name+'_generated1.png'))
 yy=np.arange(128)*pitch+pitch//2+oy;xx=np.arange(128)*pitch+pitch//2+ox
 valid=(yy[:,None]>=0)&(yy[:,None]<g.shape[0])&(xx[None,:]>=0)&(xx[None,:]<g.shape[1])
 a=g[np.clip(yy,0,g.shape[0]-1)[:,None],np.clip(xx,0,g.shape[1]-1)[None,:]].copy()
 ids=((a[:,:,:3].astype('int32')[:,:,None,:]-PAL[None,None])**2).sum(3).argmin(2)
 a[:,:,:3]=PAL[ids];a[:,:,3]=((a[:,:,3]>=128)&valid)*255;a[a[:,:,3]==0]=0
 save('Sources/'+name+'_blocks.png',a)
 dump('Sources/'+name+'_recovery.json',dict(method='integer block center recovery',pitch=pitch,origin=[ox,oy],source_size=[g.shape[1],g.shape[0]],resampling=False))
 return a
def bag_layer(pos,kind='ThreeQuarter'):
 b=read(ROOT/'NEWIMAG/Px32HeroRef/Bag'/('Bag_Hip_'+kind+'.png'))
 c=read(ROOT/'NEWIMAG/Px32HeroRef/Sources/R2_chain.png')[:6]
 a=np.zeros((128,128,4),np.uint8)
 for v,(x,y) in [(c,(pos[0]+5,pos[1]-6)),(b,pos)]:
  h,w=v.shape[:2];m=v[:,:,3]>0;region=a[y:y+h,x:x+w];region[m]=v[m]
 return a
if __name__=='__main__':
 for p in S.glob('*_generated1.png'):
  n=p.name.replace('_generated1.png','');a=recover(n);save('Sources/'+n+'_review_3x.png',zoom(a));print(n,Image.fromarray(a).getbbox())
