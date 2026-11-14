from assemble import put,trim
from build import *
import shutil
PREV=SRC/'prev'
def read(p):return np.array(Image.open(p).convert('RGBA'))
def old(p):return read(PREV/p)
def rect(box):
    m=np.zeros((128,128),bool);x0,y0,x1,y1=box;m[y0:y1,x0:x1]=True;return m

# Only generated mouth regions are transferred; original hardware is locked.
hipstrip=recover(SRC/'V9Fix_Bag_Hip_Front_2_generated.png',114,(64,24),size=(15,7))
hipmouth=hipstrip[:,:13].copy();hipmouth[4:,:12]=hipstrip[4:,3:15]
heldstrip=recover(SRC/'V9Fix_Bag_Held_Front_2_generated.png',106,(0,0),size=(20,7))
heldmouth=heldstrip[:,:18].copy();heldmouth[4:,:17]=heldstrip[4:,3:20]
# Retain only generated deep-crimson interior accents where the new mouth is black.
for mouth,first in [(hipmouth,recover(SRC/'V9Fix_Bag_Hip_Front_1_generated.png',36,(365,570),size=(13,7))), (heldmouth,recover(SRC/'V9Fix_Bag_Held_Front_1_generated.png',54,(150,611),size=(18,7)))]:
    accent=np.all(first[:,:,:3]==[100,31,54],axis=2)&np.all(mouth[:,:,:3]==[25,24,31],axis=2)
    mouth[accent]=first[accent]
for n,patch,xy in [('Hip_Front',hipmouth,(2,6)),('Hip_ThreeQuarter',hipmouth,(2,7)),('Held_Front',heldmouth,(2,10)),('Held_ThreeQuarter',heldmouth,(2,11))]:
    a=old(f'Bag/Bag_{n}.png');x,y=xy
    a[y:y+patch.shape[0],x:x+patch.shape[1]]=patch
    save(f'Bag/Bag_{n}.png',a)
for n in ['Hip_Open','Held_Open']:
    save(f'Bag/Bag_{n}.png',read(SRC/f'V9Fix_Bag_{n}_recovered.png'))

restore_boxes={'Front':(51,90,63,104),'Side':(62,92,72,104),'Back':(65,90,71,99)}
bases={};edit_masks={}
for n,box in restore_boxes.items():
    a=old(f'Turnaround/Hero_{n}.png');p=read(SRC/f'V9Fix_Restore_{n}_recovered.png')
    m=rect(box);bases[n]=composite(a,p,m);edit_masks[n]=m
bases['ThreeQuarter']=read(SRC/'ThreeQuarter_no_bag.png')
edit_masks['ThreeQuarter']=old('Masks/Hero_ThreeQuarter.png')[:,:,0]>0

# Anatomy-specific occluders protect every approved sleeve/glove pixel.
protect={
 'Front':mask([[(78,80),(92,88),(92,104),(79,104),(77,96)]]),
 'ThreeQuarter':mask([[(84,78),(92,80),(98,88),(98,100),(88,100),(86,89)]]),
 'Back':mask([[(32,82),(42,82),(43,91),(43,100),(33,100)]]),
 'Side':np.zeros((128,128),bool)}
positions={'Front':('Bag_Hip_Front',70,86),'ThreeQuarter':('Bag_Hip_ThreeQuarter',78,85),'Side':('Bag_Hip_ThreeQuarter',64,85),'Back':('Bag_Hip_Side',40,85)}
meta={};layers={}
for n,(bagname,x,y) in positions.items():
    bag=read(ROOT/f'Bag/{bagname}.png');layer=put(np.zeros_like(BASE),bag,x,y)
    visible=layer[:,:,3]>0
    if n=='Side':
        # Far-side bag: only the two foremost columns peek past the front pelvis.
        visible &= np.indices(visible.shape)[1]>=80
    visible &= ~protect[n]
    layer[~visible]=0
    a=composite(bases[n],layer,visible)
    edit_masks[n]|=visible
    save(f'Turnaround/Hero_{n}.png',a)
    save(f'Sources/V9Fix_{n}_bag_layer.png',layer)
    mask_save('V9Fix_'+n,edit_masks[n])
    layers[n]=layer
    belt=86 if n in ['Side','ThreeQuarter'] else 87
    top=y+(4 if n=='Back' else 3)
    meta[n]=dict(asset='Bag/'+bagname+'.png',translation=[x,y],visible_bbox=bbox(layer),visible_pixels=int(visible.sum()),belt_row=belt,attachment_top=y,box_top=top,box_top_belt_gap=top-belt,attachment_span=4 if n=='Back' else 3)
q=read(ROOT/'Turnaround/Hero_ThreeQuarter.png');qm=edit_masks['ThreeQuarter']
mask_save('Hero_ThreeQuarter',qm)
save('Sources/HipBag_layer.png',layers['ThreeQuarter'])
arms=[]
for n in ['Down','HandOnHip','Forward','Back']:
    am=old(f'Masks/Arm_{n}.png')[:,:,0]>0
    a=composite(q,old(f'Arms/Arm_{n}.png'),am)
    if n=='Forward':
        visible=(layers['ThreeQuarter'][:,:,3]>0)&am&(np.indices(am.shape)[0]>=88)
        a=composite(a,layers['ThreeQuarter'],visible)
    save(f'Arms/Arm_{n}.png',a);arms.append(a)
    mask_save('Arm_'+n,am)
    mask_save('Idle0_Arm_'+n,qm|am)
ts=[read(ROOT/f'Turnaround/Hero_{n}.png') for n in ['Front','ThreeQuarter','Side','Back']]
sheet=np.concatenate(ts,axis=1)
save('Turnaround/Turnaround_1x.png',sheet);save('Turnaround/Turnaround_4x.png',enlarge(sheet,4));save('Sources/Turnaround_review_3x.png',enlarge(sheet,3))
ash=np.concatenate(arms,axis=1)
save('Arms/Arms_4x.png',enlarge(ash,4));save('Sources/Arms_review_3x.png',enlarge(ash,3))
atlas=np.zeros((80,160,4),np.uint8)
config=json.loads((SRC/'config.json').read_text(encoding='utf-8'))
for i,n in enumerate(config['bags']):
    a=read(ROOT/f'Bag/{n}.png');atlas=put(atlas,a,(i%4)*40+(40-a.shape[1])//2,(i//4)*40+(40-a.shape[0])//2)
save('Bag/Bag_8x.png',enlarge(atlas,8));save('Sources/Bag_review_3x.png',enlarge(atlas,3))
teeth=np.zeros((24,48,4),np.uint8)
teeth=put(teeth,read(ROOT/'Bag/Bag_Hip_Front.png'),2,3)
teeth=put(teeth,read(ROOT/'Bag/Bag_Held_Front.png'),24,2)
save('Bag/BagTeeth_16x.png',enlarge(teeth,16))
config['hip_position']=[78,85];config['v9_fix_positions']=meta
config['v9_fix_entrypoint']='fix_assemble.py'
(SRC/'config.json').write_text(json.dumps(config,ensure_ascii=False,indent=2),encoding='utf-8')
(SRC/'V9Fix_placement.json').write_text(json.dumps(meta,indent=2),encoding='utf-8')
