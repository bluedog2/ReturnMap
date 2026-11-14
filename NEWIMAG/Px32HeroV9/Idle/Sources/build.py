from prepare import *
def put(a,b,x,y):
 a=a.copy(); v=a[y:y+b.shape[0],x:x+b.shape[1]];m=b[:,:,3]>0;v[m]=b[m];return a
bag=put(np.zeros_like(master),read(REF/'Sources/R2_chain.png')[:6],58,89)
bag=put(bag,read(REF/'Bag/Bag_Hip_ThreeQuarter.png'),53,95)
visible=(bag[:,:,3]>0)&np.all(bag==master,axis=2)
body=master.copy();under=read(REF/'Sources/ThreeQuarter_no_bag.png');body[visible]=under[visible]
bodymask=edit|visible
bodymask[83:105,36:64]=True  # Original held-bag removal footprint, including frame-specific edge pixels.
save('Sources/Body_patch.png',body);save('Sources/Bag_layer.png',bag)
frames=[]; masks=[]
for i,o in enumerate(orig):
 dx,dy=moves[i]; bx,by=moves[(i-1)%6]
 a=o.copy();m=bodymask|shift(bodymask,dx,dy);p=shift(body,dx,dy);a[m]=p[m]
 # Visible bag pixels only: preserve approved arm occlusion at its shifted location.
 layer=shift(bag,bx,by);bm=layer[:,:,3]>0
 arm=(bag[:,:,3]>0)&~visible
 bm &= ~shift(arm,dx,dy)
 a[bm]=layer[bm]; m|=bm
 frames.append(a);masks.append(m)
 save(f'Sources/Idle6_{i}.png',a);save(f'Sources/Idle6_{i}_3x.png',zoom(a));save(f'Masks/Idle6_{i}.png',m.astype('uint8')*255)
save('Hero_Idle_6.png',np.concatenate(frames,1))
save('Sources/Idle6_review_3x.png',zoom(np.concatenate(frames,1)))
changes=[int(np.any(frames[i]!=frames[(i+1)%6],axis=2).sum()) for i in range(6)]
edges=sorted(range(6),key=lambda i:(-changes[i],i))[:2]
(OUT/'Sources/transitions.json').write_text(json.dumps(dict(changes=changes,selected=edges),indent=2))
print('changes',changes,'selected',edges)
