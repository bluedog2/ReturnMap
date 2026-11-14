from build import *
from compose import put,base,vis,R

def shift(a,dx,dy):
 b=np.zeros_like(a);x0=max(0,dx);x1=min(128,128+dx);y0=max(0,dy);y1=min(128,128+dy);b[y0:y1,x0:x1]=a[y0-dy:y1-dy,x0-dx:x1-dx];return b
meta=[]
def compose(name,a,rects,bagkind,x,y,handrect=None):
 out=base.copy();mask=vis.copy();mask[89:95,56:58]=True
 if name.startswith('LongIdle') and int(name.split('_F')[1]) in [2,3,4,8,9,10]:
  torso=shift(base,0,-1);out[73:89,65:85]=torso[73:89,65:85];mask[73:89,65:85]=True
 for x0,y0,x1,y1 in rects:out[y0:y1,x0:x1]=a[y0:y1,x0:x1];mask[y0:y1,x0:x1]=True
 b=read(R/f'Bag/Bag_{bagkind}.png');layer=np.zeros_like(out);layer=put(layer,b,x,y);bm=layer[:,:,3]>0;out[bm]=layer[bm];mask|=bm
 hand=np.zeros((128,128),bool)
 if handrect:
  x0,y0,x1,y1=handrect;hand[y0:y1,x0:x1]=True;hand&=a[:,:,3]>0;out[hand]=a[hand];mask|=hand
 save(name+'.png',out);save('Masks/'+name+'.png',mask*255);save('Sources/'+name+'_bag_layer.png',layer);save('Masks/'+name+'_hand.png',hand*255)
 meta.append(dict(frame=name,bag_kind=bagkind,position=[x,y],arm_rects=rects,hand_rect=handrect,bag_visible_mismatch=int((np.any(out!=layer,2)&bm&~hand).sum())))
 return out
# Remove original C forearm with the corresponding generated arm/body patch.
coords=[(58,83,0),(58,83,0),(58,82,0),(58,81,1),(58,82,1),(59,84,0),(59,85,0),(59,84,0),(58,83,1),(57,82,1),(57,83,1),(58,83,0)]
for i,(x,y,op) in enumerate(coords):
 a=read(S/f'LongIdle_F{i}_blocks.png')
 if i in [5,6,7]:
  donor=shift(read(S/'LongIdle_F0_blocks.png'),x-58,y-83)
  a[77+y-83:86+y-83,63+x-58:75+x-58]=donor[77+y-83:86+y-83,63+x-58:75+x-58]
 compose(f'LongIdle_F{i}',a,[(43,73,65,95),(63+x-58,77+y-83,75+x-58,86+y-83)],'Held_Open' if op else 'Held_ThreeQuarter',x,y,(65+x-58,79+y-83,74+x-58,85+y-83))
# Reach down, swing slightly outward, then lift toward the held endpoint.
a=shift(read(S/'BagOut_F1_selected_blocks.png'),0,-3)
compose('BagOut_F1',a,[(43,73,65,98)],'Hip_ThreeQuarter',51,95,(52,91,63,98))
a=shift(read(S/'BagOut_F2_selected_blocks.png'),5,-5)
compose('BagOut_F2',a,[(43,73,66,96)],'Held_ThreeQuarter',45,92,(48,88,61,96))
a=read(S/'BagOut_F3_selected_blocks.png')
compose('BagOut_F3',a,[(43,73,65,95),(65,80,72,90)],'Held_ThreeQuarter',55,87,(60,83,71,90))
a=shift(read(S/'BagOut_F4_selected_blocks.png'),-2,0)
compose('BagOut_F4',a,[(43,73,65,95),(63,78,75,87)],'Held_ThreeQuarter',58,84,(64,79,75,87))
a=shift(read(S/'BagIn_F1_selected_blocks.png'),0,-7)
compose('BagIn_F1',a,[(43,73,65,96)],'Held_ThreeQuarter',47,93,(52,89,62,96))
a=read(S/'BagIn_F2_selected_blocks.png')
compose('BagIn_F2',a,[(43,73,65,94)],'Hip_ThreeQuarter',51,95)
for name,src in [('BagOut_F0',idle),('BagOut_F5',read(O/'LongIdle_F0.png')),('BagIn_F0',read(O/'LongIdle_F0.png')),('BagIn_F3',idle)]:
 save(name+'.png',src);mask=np.zeros((128,128),bool) if np.array_equal(src,idle) else np.array(Image.open(O/'Masks/LongIdle_F0.png'))>0;save('Masks/'+name+'.png',mask*255)
(S/'composition.json').write_text(json.dumps(meta,indent=2))
rows=[]
for group,n in [('BagOut',6),('LongIdle',12),('BagIn',4)]:
 fs=[read(O/f'{group}_F{i}.png') for i in range(n)]
 save(f'Hero_{group}_{n}.png',np.concatenate(fs,1))
 for j in range(0,n,6):rows.append(np.concatenate(fs[j:j+6]+[np.zeros_like(idle)]*(6-len(fs[j:j+6])),1))
keys=[read(O/f'LongIdle_F{i}.png') for i in [0,3,6,9]]
rows.append(np.concatenate(keys+[idle,np.zeros_like(idle)],1))
save('Sheet_3x.png',zoom(np.concatenate(rows,0)))
for group,n in [('BagOut',6),('LongIdle',12),('BagIn',4)]:
 save(f'Sources/{group}_review_3x.png',zoom(np.concatenate([read(O/f'{group}_F{i}.png') for i in range(n)],1)))
