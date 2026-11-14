from build import *
from assemble import put
prev=SRC/'prev'; ps=prev/'Sources'
def read(p):return np.array(Image.open(p).convert('RGBA'))
def backup_review(name,a):
    p=ps/name
    if not p.exists():Image.fromarray(a).save(p)
backup_review('Turnaround_review_3x.png',enlarge(read(prev/'Turnaround/Turnaround_1x.png'),3))
backup_review('Arms_review_3x.png',enlarge(np.concatenate([read(prev/f'Arms/Arm_{n}.png') for n in ['Down','HandOnHip','Forward','Back']],axis=1),3))
config=json.loads((ps/'config.json').read_text(encoding='utf-8'))
atlas=np.zeros((80,160,4),np.uint8)
for i,n in enumerate(config['bags']):
    a=read(prev/f'Bag/{n}.png');atlas=put(atlas,a,(i%4)*40+(40-a.shape[1])//2,(i//4)*40+(40-a.shape[0])//2)
backup_review('Bag_review_3x.png',enlarge(atlas,3))
recovery=json.loads((SRC/'recovery.json').read_text(encoding='utf-8'))
recovery['v9_fix']={'placement_file':'Sources/V9Fix_placement.json','entrypoint':'Sources/fix_assemble.py','previous_recovery':'Sources/prev/Sources/recovery.json','generation_calls':9,'mouth_recovery':{'hip':{'source':'V9Fix_Bag_Hip_Front_2_generated.png','pitch':114,'origin':[64,24],'size':[15,7],'assembly':'upper 13 columns; lower source columns 3..14 translated to columns 0..11; retain generated column 12'},'held':{'source':'V9Fix_Bag_Held_Front_2_generated.png','pitch':106,'origin':[0,0],'size':[20,7],'assembly':'upper 18 columns; lower source columns 3..19 translated to columns 0..16; retain generated column 17'}},'open_recovery':{'hip':{'pitch':52,'size':[20,23]},'held':{'pitch':40,'size':[28,30]}},'restoration_recovery':{'pitch':10,'origin':[-5,-5],'local_masks':'Sources/fix_assemble.py restore_boxes'},'note':'Top-level entries describe inherited generation; use v9_fix entrypoint for current outputs.'}
(SRC/'recovery.json').write_text(json.dumps(recovery,ensure_ascii=False,indent=2),encoding='utf-8')
manifest={p.relative_to(prev).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in prev.rglob('*') if p.is_file()}
(SRC/'V9Fix_previous_manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
print('Previous version files:',len(manifest))
