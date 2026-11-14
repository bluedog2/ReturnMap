from build_v2 import *
import importlib.util,hashlib
from PIL import ImageSequence
frames=[read(O/f'Hit_F{i}.png') for i in range(4)]
idle_sheet=read(O.parent/'Idle/Hero_Idle_8.png');idles=[idle_sheet[:,128*i:128*(i+1)] for i in range(8)]
old=read(S/'prev2/Hero_Hit_4.png');olds=[old[:,128*i:128*(i+1)] for i in range(4)]
sheet=np.concatenate(frames,1);save('Hero_Hit_4.png',sheet)
save('Sheet_3x.png',zoom(np.concatenate([idle]+frames,1)))
save('Waist_8x.png',zoom(np.concatenate([a[70:113,40:102] for a in [idle]+frames],1),8))
gifs={}
allpal=np.unique(np.concatenate([idle_sheet.reshape(-1,4),old.reshape(-1,4),sheet.reshape(-1,4)]),axis=0)
gp=allpal[allpal[:,3]>0,:3]
def indexed(a):
 ids=np.zeros(a.shape[:2],np.uint8)
 for j,c in enumerate(gp,1):ids[np.all(a[:,:,:3]==c,2)&(a[:,:,3]>0)]=j
 im=Image.fromarray(ids,'P');p=[0,0,0]+gp.ravel().tolist();im.putpalette(p+[0]*(768-len(p)));im.info['transparency']=0;return im
def gif(name,seq,times):
 ims=[indexed(zoom(a)) for a in seq]
 ims[0].save(O/name,save_all=True,append_images=ims[1:],duration=times,loop=0,disposal=2,transparency=0,optimize=False)
 dec=Image.open(O/name);mismatches=[];actual=[]
 for j,f in enumerate(ImageSequence.Iterator(dec)):
  a=np.array(f.convert('RGBA'));b=zoom(seq[j]);active=(a[:,:,3]>0)|(b[:,:,3]>0)
  mismatches.append(int((np.any(a!=b,2)&active).sum()));actual.append(f.info['duration'])
 gifs[name]=dict(frames=len(actual),durations_ms=actual,total_ms=sum(actual),visible_pixel_mismatches=mismatches)
 assert len(actual)==len(seq) and not any(mismatches)
gif('Hero_Hit_3x.gif',frames,[60,60,70,60])
gif('Compare_3x.gif',[np.concatenate([a,b],1) for a,b in zip(olds,frames)],[60,60,70,60])
gif('Transition_3x.gif',[idle]+frames+idles,[120,60,60,70,60]+[120,130]*4)
records=[]
for i,a in enumerate(frames):
 mask=np.array(Image.open(O/f'Masks/F{i}_edit.png'))>0
 ys,xs=np.where(a[:,:,3]>0);colors=np.unique(a[a[:,:,3]>0,:3],axis=0)
 bagshift=[2,3,2,0][i];layer=shift(bag,bagshift);lm=layer[:,:,3]>0
 if i==3:lm &= np.all(layer==idle,2)
 protected=np.zeros((128,128),bool);protected[103:]=True;protected&=~vis;protected&=~lm
 r=dict(frame=f'F{i}',colors=len(colors),alpha=np.unique(a[:,:,3]).tolist(),top=int(ys.min()),floor=int(ys.max()),outside_mask_mismatch=int((np.any(a!=idle,2)&~mask).sum()),approved_palette=bool(all(any(np.array_equal(c,p) for p in pal) for c in colors)),bag_offset_from_idle0=[bagshift,0],bag_top_left=[51+bagshift,95],bag_chain_visible_mismatch=int((np.any(a!=layer,2)&lm).sum()),protected_lower_leg_mismatch=int((np.any(a!=idle,2)&protected).sum()),difference_from_idle0=int(np.any(a!=idle,2).sum()))
 records.append(r)
 assert r['outside_mask_mismatch']==0 and r['colors']<=24 and r['approved_palette'] and r['floor']==127 and r['alpha']==[0,255] and r['bag_chain_visible_mismatch']==0 and r['protected_lower_leg_mismatch']==0
spec=importlib.util.spec_from_file_location('asf_check',O.parents[2]/'tools/sprite/asf-to-px32.py');asf=importlib.util.module_from_spec(spec);spec.loader.exec_module(asf)
fails,metrics=asf.check(Image.fromarray(sheet),128,4,{tuple(map(int,c)) for c in pal},asf.rules_for('Hero_Hit'))

for i in [2,3]:
 assert (O/f'Hit_F{i}.png').read_bytes()==(S/f'prev2/Hit_F{i}.png').read_bytes()
report=dict(status='pending visual review',frames=records,frame_spec=dict(count=4,fps=16,duration_seconds=.25,sheet_size=[512,128]),gifs=gifs,asf_4=dict(failures=fails,metrics=metrics,legacy_spec=asf.sheet_spec('Hero_Hit'),legacy_warning='Legacy spec 3 frames; requested 4. Inspection only.'),unchanged_frames=[2,3],method='Built-in image_gen; integer block-center recovery, approved palette mapping, binary alpha, integer translation, masked composition, assembly and validation only.')
report.update(json.loads((S/'review_v3.json').read_text(encoding='utf-8')))
(O/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
attempts=dict(tool='built-in image_gen',per_frame_limit=3,total_calls=4,frames=[dict(frame=f'F{i}',calls=n,selected_attempt=a,generated_files=[f'Sources/V3_F{i}_generated{j}.png' for j in range(1,n+1)],prompts=[f'Sources/V3_F{i}_prompt{j}.txt' for j in range(1,n+1)]) for i,n,a in [(0,3,3),(1,1,1),(2,0,None),(3,0,None)]],selection={'F0':'3회차. 1회차 상체 과도 이동, 2회차 정면화로 미채택.','F1':'1회차. 더 큰 젖힘과 휘날림.','F2':'prev2 원본 바이트 동일, 이음새 검사만.','F3':'prev2 원본 바이트 동일.'},previous_run='Sources/prev2/attempts.json')
(O/'attempts.json').write_text(json.dumps(attempts,ensure_ascii=False,indent=2),encoding='utf-8')
save('Sources/Before_After_F01_3x.png',zoom(np.concatenate([olds[0],frames[0],olds[1],frames[1]],1)))
print(json.dumps(dict(frames=records,asf=fails,gifs=gifs),ensure_ascii=False))
