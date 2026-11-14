from build import *
from assemble import put

PREV=SRC/'prev2'
def read(p):return np.array(Image.open(p).convert('RGBA'))
def rect(box):
    m=np.zeros((128,128),bool);x0,y0,x1,y1=box;m[y0:y1,x0:x1]=True;return m

# Generated raster recovery only. No chain pixels are drawn in code.
chain=recover(SRC/'R2_chain_generated.png',68,(560,270),size=(2,12))
save('Sources/R2_chain.png',chain)
save('Sources/R2_chain_32x.png',enlarge(chain,32))
bases={}
for n,box in {'Front':(51,90,63,104),'Side':(62,92,72,104),'Back':(65,90,71,99)}.items():
    bases[n]=composite(read(SRC/'prev'/f'Turnaround/Hero_{n}.png'),read(SRC/f'V9Fix_Restore_{n}_recovered.png'),rect(box))
bases['ThreeQuarter']=read(SRC/'ThreeQuarter_no_bag.png')
for n,a in bases.items():save(f'Sources/R2_{n}_base.png',a)

# Locked asset identity; integer translations only, including the existing handle.
positions={'Front':('Front',70,101),'ThreeQuarter':('ThreeQuarter',75,101),'Side':('ThreeQuarter',66,101),'Back':('Side',40,100)}
chain_positions={'Front':(76,89),'ThreeQuarter':(80,89),'Side':(80,89),'Back':(45,89)}
protect={
 'Front':mask([[(78,80),(92,88),(92,104),(79,104),(77,96)]]),
 'ThreeQuarter':mask([[(84,78),(92,80),(98,88),(98,100),(88,100),(86,89)]]),
 'Back':mask([[(32,82),(42,82),(43,91),(43,100),(33,100)]]),
 'Side':np.zeros((128,128),bool)}
for n,end in [('Front',104),('ThreeQuarter',100),('Back',100)]:protect[n]&=np.indices((128,128))[0]<end
layers={};masks={};meta={};results={}
for n,(asset,x,y) in positions.items():
    original=read(PREV/f'Turnaround/Hero_{n}.png')
    oldlayer=read(SRC/f'V9Fix_{n}_bag_layer.png')
    oldmask=oldlayer[:,:,3]>0
    base=composite(original,bases[n],oldmask)
    bag=read(ROOT/f'Bag/Bag_Hip_{asset}.png')
    layer=put(np.zeros_like(BASE),bag,x,y)
    full=layer.copy()
    if n=='Side':layer[:,:79]=0
    layer[protect[n]]=0
    strap=put(np.zeros_like(BASE),chain,*chain_positions[n])
    strap[protect[n]]=0
    a=composite(base,strap,strap[:,:,3]>0)
    a=composite(a,layer,layer[:,:,3]>0)
    m=oldmask|(layer[:,:,3]>0)|(strap[:,:,3]>0)
    save(f'Turnaround/Hero_{n}.png',a)
    save(f'Sources/R2_{n}_bag_layer.png',layer)
    save(f'Sources/R2_{n}_full_bag_layer.png',full)
    save(f'Sources/R2_{n}_chain_layer.png',strap)
    mask_save('R2_'+n,m)
    masks[n]=m;layers[n]=layer;results[n]=a
    meta[n]={'asset':f'Bag/Bag_Hip_{asset}.png','translation':[x,y],'box_top':y+(4 if n=='Back' else 3),'bottom':bbox(full)[3]-1,'chain_translation':list(chain_positions[n]),'chain_length':12,'chain_width':2,'visible_bbox':bbox(layer)}

q=results['ThreeQuarter'];qm=read(PREV/'Masks/Hero_ThreeQuarter.png')[:,:,0]>0
save('Sources/HipBag_layer.png',layers['ThreeQuarter'])
qm|=masks['ThreeQuarter'];mask_save('Hero_ThreeQuarter',qm)
arms=[]
for n in ['Down','HandOnHip','Forward','Back']:
    original=read(PREV/f'Arms/Arm_{n}.png')
    am=read(PREV/f'Masks/Arm_{n}.png')[:,:,0]>0
    a=composite(q,original,am)
    if n=='Forward':
        # This lower region is generated background restoration, not arm pixels.
        region=am&(np.indices(am.shape)[0]>=88)
        oldbag=read(SRC/'V9Fix_ThreeQuarter_bag_layer.png')[:,:,3]>0
        a=composite(a,bases['ThreeQuarter'],region&oldbag)
        a=composite(a,read(SRC/'R2_ThreeQuarter_chain_layer.png'),region&(read(SRC/'R2_ThreeQuarter_chain_layer.png')[:,:,3]>0))
        a=composite(a,layers['ThreeQuarter'],region&(layers['ThreeQuarter'][:,:,3]>0))
    save(f'Arms/Arm_{n}.png',a);arms.append(a)
    mask_save('R2_Arm_'+n,masks['ThreeQuarter'])
    mask_save('Idle0_Arm_'+n,qm|am)
sheet=np.concatenate([results[n] for n in positions],axis=1)
save('Turnaround/Turnaround_1x.png',sheet);save('Turnaround/Turnaround_4x.png',enlarge(sheet,4))
save('Sources/Turnaround_review_3x.png',enlarge(sheet,3))
hips=np.concatenate([results[n][82:118,32:104] for n in positions],axis=1)
save('Turnaround/Hips_8x.png',enlarge(hips,8))
ash=np.concatenate(arms,axis=1)
save('Arms/Arms_4x.png',enlarge(ash,4));save('Sources/Arms_review_3x.png',enlarge(ash,3))
(SRC/'R2_placement.json').write_text(json.dumps(meta,indent=2),encoding='utf-8')
config=json.loads((SRC/'config.json').read_text(encoding='utf-8'))
config['hip_position']=[75,101];config['r2_positions']=meta;config['current_entrypoint']='r2_assemble.py'
(SRC/'config.json').write_text(json.dumps(config,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(meta))
