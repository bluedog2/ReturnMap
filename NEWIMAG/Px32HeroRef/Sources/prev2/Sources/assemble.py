from build import *

def trim(a):
    b=bbox(a)
    return a[b[1]:b[3],b[0]:b[2]].copy()

def put(a,b,x,y):
    a=a.copy()
    for v in range(b.shape[0]):
        for u in range(b.shape[1]):
            if b[v,u,3] and 0<=x+u<a.shape[1] and 0<=y+v<a.shape[0]:a[y+v,x+u]=b[v,u]
    return a

def grounded(name,attempt,pitch):
    a=recover(SRC/f'{name}_{attempt}_generated.png',pitch)
    b=bbox(a);dy=128-b[3]
    a=np.roll(a,dy,axis=0)
    if dy>0:a[:dy]=0
    elif dy<0:a[dy:]=0
    save(f'Turnaround/Hero_{name}.png',a)
    return a,dict(attempt=attempt,pitch=pitch,origin=[0,0],translation=[0,dy])

ARMS={
'HandOnHip':[[(52,69),(64,69),(67,79),(64,88),(57,92),(48,88),(43,81),(44,76)],[(45,83),(56,83),(58,102),(45,102)]],
'Forward':[[(80,72),(108,72),(108,85),(99,88),(100,102),(86,102),(82,86)]],
'Back':[[(53,69),(65,71),(64,81),(58,88),(42,98),(33,98),(33,88),(44,79)],[(45,83),(57,83),(58,102),(45,102)]]}

def assemble(config):
    recovery={}
    for name,c in config['turnaround'].items():
        _,recovery[name]=grounded(name,*c)
    bags=[]
    for name,c in config['bags'].items():
        attempt,pitch=c
        a=recover(SRC/f'{name}_{attempt}_generated.png',pitch,size=(64,64))
        a=trim(a)
        save('Bag/'+name+'.png',a)
        bags.append(a)
        recovery[name]=dict(attempt=attempt,pitch=pitch,origin=[0,0],bbox_size=[a.shape[1],a.shape[0]])
    # Original sprite is always the base; generated raster is sampled only inside
    # predeclared semantic sleeve/old bag/exposed thigh areas.
    patch=recover(SRC/'ThreeQuarter_1_generated.png',10,(-15,-40))
    m=mask([[(53,69),(64,69),(70,78),(72,92),(64,104),(36,104),(36,86),(44,86),(44,82),(49,82),(49,79)],[(82,78),(91,78),(99,88),(98,99),(88,99),(83,89)]])
    q=composite(BASE,patch,m)
    save('Sources/ThreeQuarter_no_bag.png',q)
    bag=np.array(Image.open(ROOT/'Bag/Bag_Hip_ThreeQuarter.png').convert('RGBA'))
    layer=put(np.zeros_like(BASE),bag,*config['hip_position'])
    q=put(q,bag,*config['hip_position'])
    m|=layer[:,:,3]>0
    mask_save('Hero_ThreeQuarter',m)
    save('Sources/HipBag_layer.png',layer)
    save('Turnaround/Hero_ThreeQuarter.png',q)
    recovery['ThreeQuarter']=dict(attempt=1,pitch=10,origin=[-15,-40],bag_source='Bag/Bag_Hip_ThreeQuarter.png',bag_translation=config['hip_position'])
    save('Arms/Arm_Down.png',q);mask_save('Arm_Down',np.zeros((128,128),bool))
    arms=[q]
    for name,polys in ARMS.items():
        attempt=config['arms'][name]
        a=recover(SRC/f'Arm_{name}_{attempt}_generated.png',10,(-10,-10))
        am=mask(polys)
        # Hip bag pixels are locked; this pass is restricted to the arm.
        am&=layer[:,:,3]==0
        c=composite(q,a,am)
        save(f'Arms/Arm_{name}.png',c);mask_save('Arm_'+name,am)
        save(f'Sources/Arm_{name}_{attempt}_blocks.png',a)
        arms.append(c)
        recovery['Arm_'+name]=dict(attempt=attempt,pitch=10,origin=[-10,-10])
    ts=[np.array(Image.open(ROOT/f'Turnaround/Hero_{n}.png')) for n in ['Front','ThreeQuarter','Side','Back']]
    sheet=np.concatenate(ts,axis=1)
    save('Turnaround/Turnaround_1x.png',sheet);save('Turnaround/Turnaround_4x.png',enlarge(sheet,4));save('Sources/Turnaround_review_3x.png',enlarge(sheet,3))
    ash=np.concatenate(arms,axis=1)
    save('Arms/Arms_4x.png',enlarge(ash,4));save('Sources/Arms_review_3x.png',enlarge(ash,3))
    # Review atlas uses 40px cells and preserves each bag's native pixel grid.
    atlas=np.zeros((80,160,4),np.uint8)
    for i,b in enumerate(bags):atlas=put(atlas,b,(i%4)*40+(40-b.shape[1])//2,(i//4)*40+(40-b.shape[0])//2)
    save('Bag/Bag_8x.png',enlarge(atlas,8));save('Sources/Bag_review_3x.png',enlarge(atlas,3))
    hands=np.zeros((16,64,4),np.uint8)
    for i,(a,roi) in enumerate(zip(arms,config['hand_rois'])):
        x,y,w,h=roi;hands=put(hands,a[y:y+h,x:x+w],i*16+(16-w)//2,(16-h)//2)
    save('Arms/Hands_8x.png',enlarge(hands,8));save('Sources/Hands_review_3x.png',enlarge(hands,3))
    (SRC/'recovery.json').write_text(json.dumps(recovery,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({p.stem:stats(np.array(Image.open(p))) for folder in ['Bag','Turnaround','Arms'] for p in (ROOT/folder).glob('*.png') if not any(t in p.stem for t in ['_8x','_4x','_1x'])},indent=2))

if __name__=='__main__':
    import runpy
    runpy.run_path(str(SRC/'fix_assemble.py'),run_name='__main__')
