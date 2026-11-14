from build import *
P=S/'prev1'
def put(a,b,x,y):
 a=a.copy();v=a[y:y+len(b),x:x+b.shape[1]];m=b[:,:,3]>0;v[m]=b[m];return a
def shift(a,dx,dy):
 b=np.zeros_like(a);x0=max(0,dx);x1=min(128,128+dx);y0=max(0,dy);y1=min(128,128+dy);b[y0:y1,x0:x1]=a[y0-dy:y1-dy,x0-dx:x1-dx];return b
hip=read(O.parents[2]/'NEWIMAG/Px32HeroRef/Bag/Bag_Hip_ThreeQuarter.png');opened=read(S/'Bag_Open_Same.png');restore=read(S/'BodyRestore_blocks.png');base=read(P/'Sources/Body_base.png')
meta=json.loads((P/'Sources/composition.json').read_text());records=[]
for r in meta:
 name=r['frame'];old=read(P/(name+'.png'));a=old.copy();layer=read(P/'Sources'/(name+'_bag_layer.png'));hand=np.array(Image.open(P/'Masks'/(name+'_hand.png')))>0;bm=layer[:,:,3]>0
 mask=bm&~hand;a[mask]=restore[mask]
 x,y=r['position'];op=r['bag_kind']=='Held_Open'
 if r['bag_kind'].startswith('Held'):x+=4
 if op:y-=3
 if name=='BagOut_F2':
  x,y=55,91
  arm=shift(read(S/'BagOut_F2_blocks3.png'),-4,3)
  cleanup=np.zeros((128,128),bool);cleanup[73:96,43:66]=True;a[cleanup]=base[cleanup];mask|=cleanup
  arm_mask=np.zeros((128,128),bool);arm_mask[73:94,43:71]=True;a[arm_mask]=arm[arm_mask];mask|=arm_mask
  # Keep only generated arm above the bag; body below restored from original base.
  hand=np.zeros((128,128),bool);hand[87:94,59:68]=arm[87:94,59:68,3]>0
  old=arm
 b=opened if op else hip;newlayer=put(np.zeros_like(a),b,x,y);nm=newlayer[:,:,3]>0;a[nm]=newlayer[nm]
 # Glove occludes handle only, never the chest body or teeth.
 h=hand.copy();h[y+3:]=False;a[h]=old[h]
 mask|=nm|hand
 save(name+'.png',a);save('Sources/'+name+'_bag_layer.png',newlayer);save('Masks/'+name+'_hand.png',h*255)
 previous=np.array(Image.open(P/'Masks'/(name+'.png')))>0;save('Masks/'+name+'.png',(previous|mask)*255)
 save('Masks/'+name+'_revision.png',mask*255)
 records.append(dict(frame=name,position=[x,y],open=op,body_width=18,asset_size=[b.shape[1],len(b)],body_rows_equal_hip=True,hand_overlap=int((nm&h).sum()),outside_revision_mask=int((np.any(a!=read(P/(name+'.png')),2)&~mask).sum())))
for name,src in [('BagOut_F0',idle),('BagOut_F5',read(O/'LongIdle_F0.png')),('BagIn_F0',read(O/'LongIdle_F0.png')),('BagIn_F3',idle)]:
 save(name+'.png',src);same=np.array_equal(src,idle);m=np.zeros((128,128),bool) if same else np.array(Image.open(O/'Masks/LongIdle_F0.png'))>0;save('Masks/'+name+'.png',m*255)
 source='BagOut_F1' if same else 'LongIdle_F0';lay=read(O/('Sources/'+source+'_bag_layer.png'));save('Sources/'+name+'_bag_layer.png',lay)
 records.append(dict(frame=name,position=[51,95] if same else [62,83],open=False,body_width=18,asset_size=[18,16],body_rows_equal_hip=True,endpoint_copy=True))
rows=[]
for group,n in [('BagOut',6),('LongIdle',12),('BagIn',4)]:
 fs=[read(O/f'{group}_F{i}.png') for i in range(n)];save(f'Hero_{group}_{n}.png',np.concatenate(fs,1))
 for j in range(0,n,6):rows.append(np.concatenate(fs[j:j+6]+[np.zeros_like(idle)]*(6-len(fs[j:j+6])),1))
save('Sheet_3x.png',zoom(np.concatenate(rows,0)))
(S/'composition.json').write_text(json.dumps(records,indent=2))
