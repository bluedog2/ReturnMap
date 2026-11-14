from pathlib import Path
from PIL import Image,ImageDraw
import numpy as np,json,hashlib,sys
O=Path(__file__).resolve().parents[1]; S=O/'Sources'; ROOT=O.parents[2]
def read(p): return np.array(Image.open(p).convert('RGBA'))
def save(p,a): Image.fromarray(a.astype('uint8')).save(O/p)
def dump(p,a): (O/p).write_text(json.dumps(a,ensure_ascii=False,indent=2),encoding='utf-8')
def zoom(a,n):return a.repeat(n,0).repeat(n,1)
def poly(points):
 im=Image.new('L',(128,128)); ImageDraw.Draw(im).polygon(points,fill=255);return np.array(im)>0
idle=read(ROOT/'NEWIMAG/Px32HeroV9/Idle/Hero_Idle_8.png');PAL=np.unique(idle[idle[:,:,3]>0,:3],axis=0).astype('int32')
def recover(g,o,top,bottom):
 best=None
 for pitch in [9,10]:
  for oy in range(-30,6):
   for ox in range(-30,6):
    ys=np.arange(top,bottom)*pitch+pitch//2+oy;xs=np.arange(20,105)*pitch+pitch//2+ox
    a=g[np.clip(ys,0,len(g)-1)[:,None],np.clip(xs,0,g.shape[1]-1)[None,:]];r=o[top:bottom,20:105];u=(a[:,:,3]>127)|(r[:,:,3]>0)
    e=float((((a[:,:,:3].astype(float)-r[:,:,:3])**2).mean(2)*u).sum()+np.sum((a[:,:,3]>127)!=(r[:,:,3]>0))*50000)
    if best is None or e<best[0]:best=(e,pitch,ox,oy)
 e,p,x,y=best;ys=np.clip(np.arange(128)*p+p//2+y,0,len(g)-1);xs=np.clip(np.arange(128)*p+p//2+x,0,g.shape[1]-1);a=g[ys[:,None],xs[None,:]].copy()
 ids=((a[:,:,:3].astype('int32')[:,:,None,:]-PAL[None,None,:,:])**2).sum(3).argmin(2);a[:,:,:3]=PAL[ids];a[:,:,3]=(a[:,:,3]>=128)*255;a[a[:,:,3]==0]=0
 return a,dict(pitch=p,origin=[x,y],head_registration_error=e)
bag=read(ROOT/'NEWIMAG/Px32HeroRef/Bag/Bag_Hip_ThreeQuarter.png');chain=read(ROOT/'NEWIMAG/Px32HeroRef/Sources/R2_chain.png')[:6]
def layer(pos):
 a=np.zeros((128,128,4),dtype='uint8');x,y=pos
 for b,xx,yy in [(chain,x+5,y-6),(bag,x,y)]:
  v=a[yy:yy+len(b),xx:xx+b.shape[1]];m=b[:,:,3]>0;v[m]=b[m]
 return a
# Predefined anatomical masks; never derived from result differences.
polygons=[[(49,92),(57,91),(66,94),(69,99),(67,108),(61,113),(57,116),(49,119),(35,119),(30,106),(33,99),(43,96)],[(51,68),(58,65),(65,67),(70,73),(76,75),(76,82),(67,88),(59,91),(58,97),(35,97),(34,82),(45,78),(48,74)],[(51,70),(59,67),(66,69),(72,73),(78,74),(78,81),(68,88),(60,92),(59,100),(35,100),(34,85),(43,81),(48,77)]]
hips=[[62,100],[65,86],[65,87]]
# Same body offset (-11,-2) as final Run; bounded one-key lag: down -1, up +2, down -1.
lags=[-1,2,-1];positions=[[hips[i][0]-11,hips[i][1]-2+lags[i]] for i in range(3)]
def main():
 fs=[];meta={}
 for i in range(3):
  o=read(S/f'Original_{i}.png');b,r=recover(read(S/f'F{i}_generated1.png'),o,50 if i==0 else 25,91 if i==0 else 65)
  m=poly(polygons[i])
  if i==0:m[117:,49:]=False;m[114:,59:]=False
  else:m[88:,61:]=False;m[94:,58:]=False
  base=o.copy();base[m]=b[m];l=layer(positions[i]);bm=l[:,:,3]>0;a=base.copy();a[bm]=l[bm];edit=m|bm
  for name,arr in [(f'Sources/F{i}_blocks.png',b),(f'Sources/F{i}_bagless.png',base),(f'Sources/F{i}_bagless_10x.png',zoom(base,10)),(f'Sources/F{i}_bag_layer.png',l),(f'JumpStart_F{i}.png',a),(f'Sources/F{i}_review_3x.png',zoom(a,3)),(f'Masks/F{i}_generation.png',m*255),(f'Masks/F{i}_edit.png',edit*255)]:save(name,arr)
  meta[f'F{i}']=dict(recovery=r,hip=hips[i],lag=lags[i],bag_position=positions[i],outside_mask_mismatch=int((np.any(a!=o,2)&~edit).sum()))
  fs.append(a)
 changes=[int(np.any(fs[i]!=fs[i+1],2).sum()) for i in range(2)]
 dump('Sources/measurements.json',dict(frames=meta,changes=changes,selected_interval=int(np.argmax(changes))))
 save('Hero_JumpStart_3.png',np.concatenate(fs,1));save('Sources/Review_6x.png',zoom(np.concatenate(fs,1),6));print(json.dumps(dict(frames=meta,changes=changes),indent=2))
if __name__=='__main__':main()
