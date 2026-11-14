from build import *
R=O.parents[2]/'NEWIMAG/Px32HeroRef'
def put(a,b,x,y):
 a=a.copy();v=a[y:y+len(b),x:x+b.shape[1]];m=b[:,:,3]>0;v[m]=b[m];return a
base=idle.copy();old=read(O.parent/'Idle/Sources/Bag_layer.png');old=np.roll(old,-2,axis=1);vis=(old[:,:,3]>0)&np.all(old==idle,2)
patch=read(O.parent/'Idle/Sources/Body_patch.png');base[vis]=patch[vis]
# Six original chain pixels stay attached to the belt.
chain=read(R/'Sources/R2_chain.png')[:6];base=put(base,chain,56,89)
save('Sources/Body_base.png',base)
for i,(x,y,op) in zip([0,3,6,9],[(58,83,False),(58,81,True),(59,85,False),(57,82,True)]):
 a=read(S/f'LongIdle_F{i}_blocks.png');mask=np.zeros((128,128),bool);mask[73:85,43:75]=True
 out=base.copy();out[mask]=a[mask]
 b=read(R/('Bag/Bag_Held_Open.png' if op else 'Bag/Bag_Held_ThreeQuarter.png'))
 out=put(out,b,x,y)
 # Generated glove remains in front of the top handle only.
 hand=np.zeros((128,128),bool);hand[79:85,65:74]=True;hand &= a[:,:,3]>0
 out[hand]=a[hand];mask |= vis;mask[89:95,56:58]=True;mask[y:y+len(b),x:x+b.shape[1]]=True
 save(f'LongIdle_F{i}.png',out);save(f'Masks/LongIdle_F{i}.png',mask*255);save(f'Sources/LongIdle_F{i}_ref8x.png',zoom(out,8))
keys=[read(O/f'LongIdle_F{i}.png') for i in [0,3,6,9]];save('Sources/Keys_composed_3x.png',zoom(np.concatenate(keys,1)))
