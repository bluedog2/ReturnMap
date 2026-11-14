from build import *
import importlib.util
from PIL import ImageSequence
frames={g:[read(O/f'{g}_F{i}.png') for i in range(n)] for g,n in [('BagOut',6),('LongIdle',12),('BagIn',4)]}
idlesheet=read(O.parent/'Idle/Hero_Idle_8.png');idles=[idlesheet[:,i*128:(i+1)*128] for i in range(8)]
assert np.array_equal(idle,idles[0])
gp=np.unique(idlesheet[idlesheet[:,:,3]>0,:3],axis=0)
def indexed(a):
 ids=np.zeros(a.shape[:2],np.uint8)
 for j,c in enumerate(gp,1):ids[np.all(a[:,:,:3]==c,2)&(a[:,:,3]>0)]=j
 im=Image.fromarray(ids,'P');p=[0,0,0]+gp.ravel().tolist();im.putpalette(p+[0]*(768-len(p)));return im
gifs={}
def gif(name,seq,times,loop):
 ims=[indexed(zoom(a)) for a in seq];kwargs=dict(save_all=True,append_images=ims[1:],duration=times,disposal=2,transparency=0,optimize=False)
 if loop:kwargs['loop']=0
 ims[0].save(O/name,**kwargs)
 dec=Image.open(O/name);actual=[];mismatch=[]
 # GIF may merge identical adjacent frames, compare by elapsed timeline.
 elapsed=0;cum=np.cumsum(times)
 for f in ImageSequence.Iterator(dec):
  k=int(np.searchsorted(cum,elapsed,side='right'));a=np.array(f.convert('RGBA'));b=zoom(seq[k]);v=(a[:,:,3]>0)|(b[:,:,3]>0);mismatch.append(int((np.any(a!=b,2)&v).sum()));d=f.info['duration'];actual.append(d);elapsed+=d
 assert not any(mismatch) and elapsed==sum(times)
 gifs[name]=dict(duration_ms=elapsed,durations=actual,decoded_visible_mismatch=mismatch,loop=loop)
gif('Hero_BagOut_3x.gif',frames['BagOut'],[80,90,80,80,90,580],False)
gif('Hero_LongIdle_3x.gif',frames['LongIdle'],[100]*12,True)
gif('Hero_BagIn_3x.gif',frames['BagIn'],[80,90,80,580],False)
gif('Flow_3x.gif',idles+frames['BagOut']+frames['LongIdle']*2+frames['BagIn']+idles,[120,130]*4+[80,90,80,80,90,80]+[100]*24+[80,90,80,80]+[120,130]*4,True)
rows=[]
for g,fs in frames.items():
 for j in range(0,len(fs),6):
  a=[f[72:115,40:88] for f in fs[j:j+6]];a += [np.zeros_like(a[0])]*(6-len(a));rows.append(np.concatenate(a,1))
save('Waist_8x.png',zoom(np.concatenate(rows,0),8))
spec=importlib.util.spec_from_file_location('asf',O.parents[2]/'tools/sprite/asf-to-px32.py');asf=importlib.util.module_from_spec(spec);spec.loader.exec_module(asf)
records=[];asfs={}
for g,fs in frames.items():
 for i,a in enumerate(fs):
  name=f'{g}_F{i}';m=np.array(Image.open(O/f'Masks/{name}.png'))>0;y,x=np.where(a[:,:,3]>0);cols=np.unique(a[a[:,:,3]>0,:3],axis=0)
  r=dict(frame=name,colors=len(cols),alpha=np.unique(a[:,:,3]).tolist(),top=int(y.min()),floor=int(y.max()),outside_mask_mismatch=int((np.any(a!=idle,2)&~m).sum()),head_mismatch=int(np.any(a[:70]!=idle[:70],2).sum()),boots_mismatch=int(np.any(a[117:]!=idle[117:],2).sum()),approved_palette=all(any(np.array_equal(c,p) for p in pal) for c in cols))
  assert r['outside_mask_mismatch']==0 and r['head_mismatch']==0 and r['boots_mismatch']==0 and r['colors']<=24 and r['approved_palette'] and r['floor']==127
  records.append(r)
 fails,metrics=asf.check(Image.fromarray(np.concatenate(fs,1)),128,len(fs),{tuple(map(int,c)) for c in pal},asf.rules_for('Hero_'+g));asfs[g]=dict(failures=fails,metrics=metrics,legacy_spec=asf.sheet_spec('Hero_'+g),expected_warning='기존 규격표에 전환/LongIdle 장수 항목 없음. 요청 규격 6/12/4장으로 ④ 검사. ② 미실행.')
attempt=[]
for g,fs in frames.items():
 for i in range(len(fs)):
  name=f'{g}_F{i}';files=sorted(S.glob(name+'_generated*.png'));nums=[int(p.stem.split('generated')[-1]) for p in files];calls=max(nums,default=0)
  attempt.append(dict(frame=name,calls=calls,limit=3,prior_run_calls=2 if name=='BagOut_F1' else (1 if g=='LongIdle' else 0),generated_files=[p.name for p in files],note='이전 prompt2 존재, 원본 없음: 이전 호출 여부 불명으로 1회 사용을 보수적으로 포함' if name=='BagOut_F1' else ''))
assert max(a['calls'] for a in attempt)<=3
(O/'attempts.json').write_text(json.dumps(dict(tool='built-in image_gen',total_calls_conservative=sum(a['calls'] for a in attempt),saved_generated_images=sum(len(a['generated_files']) for a in attempt),current_run_calls=8,frames=attempt),ensure_ascii=False,indent=2),encoding='utf-8')
report=dict(status='부분 완료: 원본 가방 크기 상충 및 호흡 연기 제한',frames=records,asf_4=asfs,gifs=gifs,joins={ 'Idle0_to_BagOut0':int(np.any(idle!=frames['BagOut'][0],2).sum()),'BagOut5_to_LongIdle0':int(np.any(frames['BagOut'][-1]!=frames['LongIdle'][0],2).sum()),'LongIdle0_to_BagIn0':int(np.any(frames['LongIdle'][0]!=frames['BagIn'][0],2).sum()),'BagIn3_to_Idle0':int(np.any(frames['BagIn'][-1]!=idle,2).sum()),'LongIdle11_to_0_changed_pixels':int(np.any(frames['LongIdle'][-1]!=frames['LongIdle'][0],2).sum())},visual_review=dict(reviewed_files=['Sheet_3x.png','Waist_8x.png','Sources/BagOut_review_3x.png','Sources/BagIn_review_3x.png'],body_orientation='3/4 유지, 머리/다리/신발 승인 픽셀 보존',sleeve='긴 퍼프 소매와 검은 장갑, 맨 팔뚝 없음',bag='승인 Hip/Held/Open 자산만 정수 이동. 자산 사이 크기 자체는 다름',grip='F5~7 장갑을 F0 생성 장갑 정수 이동으로 보완. 검토 필요',breathing='팔/소매 변화만; 몸통 전체 호흡은 구현하지 않음',chain='허리 (56,89)에 R2_chain 상단 6px 유지'),limitations=['Hip18x16/Held25x22/Open28x30으로 원본 규격이 달라 모든 장 동일 크기 조건 미충족','몸통 전체 호흡 부족','꺼내기 F2 바깥 이동 및 F4→F5 손 위치 변화가 커 부드러움 제한'],method='내장 image_gen 원본 재사용, 블록 복원/팔레트 매핑/이진 알파/정수 이동/마스크 합성/조립/④ 검사만')
(O/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(dict(asf={k:v['failures'] for k,v in asfs.items()},joins=report['joins'],calls=sum(a['calls'] for a in attempt),gifs={k:v['duration_ms'] for k,v in gifs.items()}),ensure_ascii=False))
