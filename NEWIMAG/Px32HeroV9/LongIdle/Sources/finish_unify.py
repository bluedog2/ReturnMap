# Finalize the resumed size-unification revision without calling image generation.
from unify_finalize import *
import hashlib
P=S/'prev1'
meta=json.loads((S/'composition.json').read_text())
hip=read(ROOT/'NEWIMAG/Px32HeroRef/Bag/Bag_Hip_ThreeQuarter.png');opened=read(S/'Bag_Open_Same.png')
assert hip.shape==(16,18,4) and opened.shape==(21,18,4)
assert np.array_equal(opened[15:],hip[10:])
size=[];tiles=[]
for r in meta:
 name=r['frame'];a=read(O/(name+'.png'));lay=read(S/(name+'_bag_layer.png'));ys,xs=np.where(lay[:,:,3]>0)
 x,y=r['position'];asset=opened if r['open'] else hip
 expected=np.zeros_like(lay);expected[y:y+len(asset),x:x+18]=asset
 assert np.array_equal(lay,expected)
 diff=np.any(a!=read(P/(name+'.png')),2)
 if not r.get('endpoint_copy'):
  rev=np.array(Image.open(O/'Masks'/(name+'_revision.png')))>0
  assert not np.any(diff&~rev)
 tile=np.zeros((24,24,4),np.uint8);tile[22-len(asset):22,3:21]=lay[y:y+len(asset),x:x+18];tiles.append(tile)
 size.append(dict(frame=name,position=[x,y],body_width_px=int(xs.max()-xs.min()+1),asset_height_px=int(ys.max()-ys.min()+1),asset_exact_translation=True,lower_body_pixel_mismatch=0,lower_body_rows='Hip 10:16 == Open 15:21',hand_overlap=r.get('hand_overlap'),revision_mask_mismatch=r.get('outside_revision_mask',0)))
# Display order matches the 3x sheet: BagOut, LongIdle, BagIn.
lookup={r['frame']:t for r,t in zip(meta,tiles)}
rows=[]
for g,fs in frames.items():
 for j in range(0,len(fs),6):
  row=[lookup[f'{g}_F{i}'] for i in range(j,min(j+6,len(fs)))];row += [np.zeros_like(row[0])]*(6-len(row));rows.append(np.concatenate(row,1))
save('BagSize_8x.png',zoom(np.concatenate(rows,0),8))
prev={g:[read(P/f'{g}_F{i}.png') for i in range(len(fs))] for g,fs in frames.items()}
seq=idles+frames['BagOut']+frames['LongIdle']*2+frames['BagIn']+idles
oldseq=idles+prev['BagOut']+prev['LongIdle']*2+prev['BagIn']+idles
times=[120,130]*4+[80,90,80,80,90,80]+[100]*24+[80,90,80,80]+[120,130]*4
gif('Compare_3x.gif',[np.concatenate([a,b],1) for a,b in zip(oldseq,seq)],times,True)
# Read the actual Flow GIF back for visual review, retaining all decoded frames.
dec=[np.array(f.convert('RGBA')) for f in ImageSequence.Iterator(Image.open(O/'Flow_3x.gif'))]
for i,a in enumerate(dec):Image.fromarray(a).save(S/f'Flow_review_{i:02}.png')
joins={name:int(np.any(a!=b,2).sum()) for name,a,b in [('Idle0_to_BagOut0',idle,frames['BagOut'][0]),('BagOut5_to_LongIdle0',frames['BagOut'][-1],frames['LongIdle'][0]),('LongIdle0_to_BagIn0',frames['LongIdle'][0],frames['BagIn'][0]),('BagIn3_to_Idle0',frames['BagIn'][-1],idle),('LongIdle11_to_0',frames['LongIdle'][-1],frames['LongIdle'][0])]}
assert all(v==0 for k,v in joins.items() if k!='LongIdle11_to_0')
path=[next(r['position'] for r in meta if r['frame']==f'BagOut_F{i}') for i in range(6)]
assert path[2]==[(path[1][0]+path[3][0])//2,(path[1][1]+path[3][1])//2]
attempt=json.loads((O/'attempts.json').read_text(encoding='utf-8'))
attempt.update(total_calls_conservative=25,saved_generated_images=24,current_run_calls=0,resumed_session_calls=0,previous_frame_calls=23,shared_assets=[dict(asset='Bag_Open_Same',calls=1,limit=3,source='Bag_Open_generated1.png',reused=True),dict(asset='BodyRestore',calls=1,source='BodyRestore_generated1.png',reused=True)],note='이전 장별 호출 23회(원본 없는 F1 2회차 포함) + 열린 가방 1회 + 공용 몸 복원 1회 = 누적 25회. 이번 재개는 생성 0회. F1/F2는 이미 3회 사용하여 추가 생성하지 않음.')
(O/'attempts.json').write_text(json.dumps(attempt,ensure_ascii=False,indent=2),encoding='utf-8')
report.update(status='가방 크기 통일 완료',bag_size=size,joins=joins,bagout_path=path,open_asset=dict(size=[18,21],height_extension=5,unchanged_lower_rows=6,lower_body_mismatch=0),attempts=attempt,method='기존 내장 image_gen 원본 재사용; 정수 이동, 마스크 합성, 조립, 검증. 확대는 정수 픽셀 반복. ASF ④만.',visual_review=dict(status='pending',files=['Sheet_3x.png','Flow_3x.gif','Waist_8x.png','BagSize_8x.png','Sources/Bag_Compare_3x.png','Sources/Bag_Compare_8x.png']),backup=dict(path='Sources/prev1',files=sum(f.is_file() for f in P.rglob('*')),sha256={str(f.relative_to(P)):hashlib.sha256(f.read_bytes()).hexdigest() for f in P.rglob('*') if f.is_file()}))
(O/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(dict(asf={k:v['failures'] for k,v in asfs.items()},joins=joins,colors=max(r['colors'] for r in records),path=path,frames=len(size),gifs=list(gifs)),ensure_ascii=False))

review_path=S/'unify_visual_review.json'
if review_path.exists():
 report['visual_review']=json.loads(review_path.read_text(encoding='utf-8'))
 report['status']='?? ?? ?? ??? ??; ??? GIF ?? ?? ???'
 (O/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
