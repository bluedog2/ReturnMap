from build import *
pal=np.unique(raw[raw[:,:,3]>0,:3],axis=0).astype('int32')
def mapping(a):
 b=a.copy();rgb=a[:,:,:3].astype('int32'); ids=((rgb[:,:,None,:]-pal[None,None,:,:])**2).sum(3).argmin(2)
 b[:,:,:3]=pal[ids];b[:,:,3]=(a[:,:,3]>=128)*255;b[b[:,:,3]==0]=0;return b
meta={}
for i in [1,2]:
 gen=read(OUT/f'Sources/Between_{i}_generated1.png')
 # Fixed integer block restoration only. Locate grid using unchanged head as registration reference.
 best=None
 for pitch in [9,10]:
  for oy in range(-30,11,2):
   for ox in range(-30,11,2):
    ys=np.arange(128)*pitch+pitch//2+oy;xs=np.arange(128)*pitch+pitch//2+ox
    a=gen[np.clip(ys,0,len(gen)-1)[:,None],np.clip(xs,0,gen.shape[1]-1)[None,:]]
    region=a[27:69,30:100]; ref=frames[i][27:69,30:100]
    rgb=((region[:,:,:3].astype(float)-ref[:,:,:3])**2).mean(2)
    union=(region[:,:,3]>127)|(ref[:,:,3]>0)
    err=float((rgb*union).sum()+np.sum((region[:,:,3]>127)!=(ref[:,:,3]>0))*50000)
    if best is None or err<best[0]:best=(err,pitch,ox,oy,a.copy())
 _,pitch,ox,oy,a=best;a=mapping(a)
 save(f'Sources/Between_{i}_blocks.png',a)
 # Edit permission fixed before compositing. Protect head, legs, bag and chain.
 m=np.zeros((128,128),bool);m[72:98,40:97]=True
 # Preserve existing hip bag/chain exactly; only torso, sleeve and hem may change.
 m &= ~(shift(bag,*moves[(i-1)%6])[:,:,3]>0)
 c=frames[i].copy();c[m]=a[m]
 save(f'Sources/Between_{i}.png',c);save(f'Sources/Between_{i}_3x.png',zoom(c))
 save(f'Masks/Between_{i}.png',m.astype('uint8')*255)
 meta[i]=dict(pitch=pitch,origin=[ox,oy],head_registration_error=best[0],changed=int(np.any(c!=frames[i],2).sum()))
print(meta)
(OUT/'Sources/recovery.json').write_text(json.dumps(meta,indent=2))
