from build_v2 import *
import importlib.util,hashlib
from PIL import ImageSequence
frames=[read(O/f'Hit_F{i}.png') for i in range(4)]
idle_sheet=read(O.parent/'Idle/Hero_Idle_8.png');idles=[idle_sheet[:,128*i:128*(i+1)] for i in range(8)]
old=read(S/'prev1/Hero_Hit_4.png');olds=[old[:,128*i:128*(i+1)] for i in range(4)]
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
 bagshift=[1,2,2,0][i];layer=shift(bag,bagshift);lm=layer[:,:,3]>0
 if i==3:lm &= np.all(layer==idle,2)
 protected=np.zeros((128,128),bool);protected[103:]=True;protected&=~vis;protected&=~lm
 r=dict(frame=f'F{i}',colors=len(colors),alpha=np.unique(a[:,:,3]).tolist(),top=int(ys.min()),floor=int(ys.max()),outside_mask_mismatch=int((np.any(a!=idle,2)&~mask).sum()),approved_palette=bool(all(any(np.array_equal(c,p) for p in pal) for c in colors)),bag_offset_from_idle0=[bagshift,0],bag_top_left=[51+bagshift,95],bag_chain_visible_mismatch=int((np.any(a!=layer,2)&lm).sum()),protected_lower_leg_mismatch=int((np.any(a!=idle,2)&protected).sum()),head_translation=[[-3,0],[-4,0],[-1,0],[0,0]][i],difference_from_idle0=int(np.any(a!=idle,2).sum()))
 records.append(r)
 assert r['outside_mask_mismatch']==0 and r['colors']<=24 and r['approved_palette'] and r['top']==27 and r['floor']==127 and r['alpha']==[0,255] and r['bag_chain_visible_mismatch']==0 and r['protected_lower_leg_mismatch']==0
spec=importlib.util.spec_from_file_location('asf_check',O.parents[2]/'tools/sprite/asf-to-px32.py');asf=importlib.util.module_from_spec(spec);spec.loader.exec_module(asf)
fails,metrics=asf.check(Image.fromarray(sheet),128,4,{tuple(map(int,c)) for c in pal},asf.rules_for('Hero_Hit'))
report=dict(status='부분 완료 — 동작 표현 제한',source_body='승인 Hero_Idle_8.png 0번만 사용; 옛 Hit 몸 사용 없음',frames=records,frame_spec=dict(count=4,fps=16,duration_seconds=.25,sheet_size=[512,128]),gifs=gifs,asf_4=dict(failures=fails,metrics=metrics,legacy_spec=asf.sheet_spec('Hero_Hit'),legacy_warning='기존 ASF Hit 3장 규격; 요청은 4장. 검사만 실행, ②/배치/스케일링 미실행'),bag_coordinate_note='승인 시트 0번의 실제 가방은 (51,95). README의 (53,95)는 별도 마스터 좌표. F3는 시트 0번과 정확히 동일한 가방 위치로 복귀.',visual_review=dict(reference='Sheet_3x.png, Sources/F0~3_review_3x.png, Waist_8x.png',direction='승인 머리 외곽·몸통 핵심 원본 보존, 팔 교차/뒤틀림 없음',scale='머리 외곽 정수 이동만, 하체 원본, 머리 꼭대기 27·바닥127',neck='옛 Hit의 목 왼쪽 네모 결함 없음',hands='F0/F1 빈손 양팔 가드, 긴 소매·검은 장갑; F2 낮춤; F3 C 복귀',bag='승인 가방·체인 정수 이동만, 손에 든 가방 없음',limitation='고개가 추가로 꺾이는 변형 및 머리카락·재킷 자락의 독립적인 앞쪽 휘날림을 충분히 만들지 못함. F0/F1 소매는 같은 생성 픽셀의 1px 이동으로 차이를 표현.'),source_sha256={str(p.relative_to(O.parents[2])):hashlib.sha256(p.read_bytes()).hexdigest() for p in [O.parent/'Idle/Hero_Idle_8.png',O.parent/'Idle/Sources/Body_patch.png',O.parent/'Idle/Sources/Bag_layer.png']},method='내장 image_gen + 정수 블록 중심 복원/승인 팔레트 매핑/알파 이진화/정수 이동/마스크 합성/조립. 리샘플·회전·코드 그림 없음.')
(O/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
attempts=dict(tool='built-in image_gen',per_frame_limit=3,frames=[dict(frame=f'F{i}',calls=n,generated_files=[f'Sources/F{i}_generated{j}.png' for j in range(1,n+1)],prompts=[f'Sources/F{i}_prompt{j}.txt' for j in range(1,n+1)]) for i,n in enumerate([3,3,1,1])],selection={'F0':'3회차 표정 + F1 3회차 소매 공유. 머리·핵심 몸통 Idle 정수 이동 보존. 1·2회차 전신 미채택.', 'F1':'3회차 표정·소매. 1·2회차 미채택. 머리·핵심 몸통 Idle 정수 이동 보존.','F2':'1회차 표정·소매 부분 사용, 머리·핵심 몸통 Idle 보존.','F3':'1회차 얼굴 내부만; 나머지 Idle0 그대로.'},total_calls=8)
(O/'attempts.json').write_text(json.dumps(attempts,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(dict(frames=records,asf=fails,gifs=gifs),ensure_ascii=False))
