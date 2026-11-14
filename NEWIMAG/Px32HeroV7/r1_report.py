from pixel_io import *
import hashlib
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
v=json.loads((ROOT/'validation.json').read_text(encoding='utf-8'))
# Conservative manually annotated glove ROIs from the final 3x review, inclusive.
boxes={'Idle':[[70,81,78,88]]*6,'Run':[[43,80,56,91],[52,83,64,93],[59,79,71,90],[61,83,73,93],[60,82,74,93],[51,80,64,91],[46,81,59,92],[37,80,50,91]]}
checks={};table=['|시트|프레임|마스크 밖 불일치|파우치 크기|벨트 거리|손 틈|팔 끝 x|통과|','|---|---:|---:|---|---:|---:|---:|---|']
for name,s in v['sheets'].items():
 a=readpng(ROOT/'_1x'/f'Hero_{name}.png');cs=[]
 for row in s['frames']:
  i=row['frame'];f=[r[i*128:(i+1)*128] for r in a];o=readpng(ROOT/'Sources'/f'R1_{name}_{i:02}_original.png');m=readpng(ROOT/row['mask']);layer=readpng(ROOT/'Sources'/f'R1_{name}_{i:02}_pouch_layer.png')
  x0,y0,x1,y1=boxes[name][i]
  hands=[(x,y) for y in range(y0,y1+1) for x in range(x0,x1+1) if f[y][x][3] and max(f[y][x][:3])<95 and not layer[y][x][3]]
  pouch=[(x,y) for y,r in enumerate(layer) for x,p in enumerate(r) if p[3]]
  gap=max(0,min(max(abs(x-a),abs(y-b))-1 for x,y in hands for a,b in pouch))
  armx=round((min(x for x,y in hands)+max(x for x,y in hands))/2,1)
  row.update({'hand_annotation_roi':boxes[name][i],'hand_measurement_method':'Conservative manually annotated black-glove ROI; dark pixels <95, excluding inserted pouch. Chebyshev empty-pixel clearance to body AND loop; glove center x is distal-arm landmark. Occluded pixels cannot be certified.','hand_gap_px':gap,'arm_end_x':armx,'hand_gap_pass':gap>=2,'loop_attachment_visual_pass':False,'mask_sha256':sha(ROOT/row['mask'])})
  outside=sum(f[y][x]!=o[y][x] for y in range(128) for x in range(128) if m[y][x][0]==0)
  assert outside==row['outside_mask_mismatch_count']==0
  assert {p[3] for r in f for p in r}<={0,255}
  assert len({p[:3] for r in f for p in r if p[3]})<=24 and bbox(f)[3]==127
  # Exact cached source inversion, independently checked again.
  assert nearest(o,4)==readpng(ROOT/'Sources'/f'{name}_{i:02}_original_4x.png')
  cs.append({'frame':i,'outside_mask_mismatch_count':outside,'cached_original_exact':True,'bottom_row':127,'alpha_binary':True})
  table.append(f"|{name}|{i}|0|17×14|{row['belt_distance_px']}px|{gap}px|{armx:g}|미달|")
 xs=[r['arm_end_x'] for r in s['frames']];s['arm_end_x_range_px']=max(xs)-min(xs)
 s['arm_swing_range_pass']=s['arm_end_x_range_px']>=8 if name=='Run' else s['arm_end_x_range_px']<=1
 s['visual_review_3x']={'completed':True,'belt_hung_pouch_unambiguous':False,'sleeve_matches_original_shading':False,'old_bag_region_restored':True,'stocking_seam_quality_pass':False,'leg_counterphase_pass':False if name=='Run' else None,'hip_lag_1_2px_verified':False if name=='Run' else None}
 s['failure_reasons']=(['Idle 팔이 자연스럽게 내려오지 않고 앞쪽으로 굽음','고리와 원본 벨트의 접합이 불명확','생성 소매와 원본 어깨·몸통의 음영 접합 미달'] if name=='Idle' else ['일부 프레임 손-파우치 고리 간격 2px 미달','원본 어깨와 생성 소매 연결 및 드러난 스타킹 경계에 각진 잔여부','팔 왕복 폭은 확보했지만 다리 반대 위상·엉덩이 지연 1~2px 검증 미달','벨트 접합이 모든 프레임에서 명확하지 않음'])
 checks[name]={'frames':cs,'sheet_sha256':sha(ROOT/'_1x'/f'Hero_{name}.png'),'gif_sha256':sha(ROOT/'Animations'/f'Hero_{name}_3x.gif'),'size':[len(a[0]),len(a)]}
v['measurement_limitations']=['belt rows and glove ROIs are manual semantic annotations','loop is present but its connection to native belt is not visually approved','no original Assets read; original equality is to cached exact4x sources from previous run','fixed grid recovery is imperfect; generated art was never fitted to a bounding box']
(ROOT/'validation.json').write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf-8')
(ROOT/'file_checks.json').write_text(json.dumps({'revision':1,'production_ready':False,'sheets':checks},ensure_ascii=False,indent=2),encoding='utf-8')
attempts=json.loads((ROOT/'attempts.json').read_text(encoding='utf-8'))
for name,entries in {'Idle':[('R1_bag_attempt1.png','initial pouch; rejected body17x13'),('R1_Idle_attempt2.png','rejected low hand overlaps pouch region'),('R1_Idle_attempt3.png','selected uncovered body + sleeve patch translated -16,-4')],'Run':[('R1_Run_attempt1.png','rejected1774x887 irregular tile layout'),('R1_Run_attempt2.png','selected eight masked arm/restoration patches'),('R1_Run_attempt3_bag.png','selected common17x14 pouch and4px loop for BOTH sheets')]}.items():
 attempts[name]=[e for e in attempts[name] if e.get('revision')!=1]+[{'revision':1,'attempt':i+1,'source':'Sources/'+src,'result':reason} for i,(src,reason) in enumerate(entries)]
attempts.update({'total_calls':12,'revision_1_calls':6,'revision_1_limit_per_sheet':3,'revision_1_production_ready':False})
(ROOT/'attempts.json').write_text(json.dumps(attempts,ensure_ascii=False,indent=2),encoding='utf-8')
readme=(ROOT/'README.md').read_text(encoding='utf-8').split('\n## 재요청 1')[0]
if not readme.startswith('> 최신'):readme='> 최신 결과는 아래 「재요청 1」 절입니다. 앞의 내용과 기존 process.py는 이전 결과의 이력입니다.\n\n'+readme
section='''
## 재요청 1

**Idle·Run 모두 production_ready: false.** 내장 image_gen 시트당 3회(이번 요청 총 6회)를 사용했고, 최선 후보로 `_1x`, `Masks`, GIF, `Compare_3x.png`를 갱신했습니다. 공통 파우치 몸체는 **17×14px**, 고리는 **4px**입니다. 몸체와 고리를 생성 픽셀에서 분리한 뒤 동일 구성으로 모든 프레임에 평행이동 합성했습니다. 위아래 이빨 줄은 유지됐습니다.

14프레임 모두 마스크 밖 불일치 0개, 20색, 알파 0/255, 바닥 행 127입니다. 원본은 이 폴더에 보관된 4배 확대본의 정확한 반복 픽셀을 역복원했습니다. 이번 요청에서는 Assets·다른 NEWIMAG 폴더·git에 접근하거나 변경하지 않았습니다.

벨트 행은 원본 허리선 육안 측정값(Idle 86; Run 92,93,89,91,92,90,91,88)이며 각 행과 참조 ROI를 validation.json에 기록했습니다. 파우치 윗변은 모두 벨트+1입니다. 손 틈은 최종 3배 검수에서 지정한 검은 장갑 ROI와 파우치 몸체·고리 픽셀 사이의 보수적 빈 픽셀 거리입니다. 팔 끝 x는 장갑 중심 랜드마크이며, 가림 때문에 손 전체 의미 분할을 보장하지는 않습니다.

**외형 미달:** Idle 팔은 요청한 자연스러운 내린 자세보다 앞쪽으로 굽었습니다. Run에는 고리와 손 간격이 부족한 프레임, 소매·스타킹 경계의 각진 접합부가 남습니다. 벨트 연결이 모든 프레임에서 명확하지 않으며, 다리 반대 위상과 엉덩이 대비 1~2px 지연은 검증하지 못했습니다. 생성 횟수 제한에 도달해 추가 생성하지 않았습니다. 숫자상 크기·높이 통과를 외형 통과로 취급하지 않았습니다.

생성본은 요청한 격자와 정확히 일치하지 않았습니다. 정수 블록 중심 복원(Idle10, Run3, 파우치40), 기존 20색 팔레트 정리, 알파 임계값, 생성 픽셀 자르기·평행이동·마스크 합성만 했습니다. 목표 경계에 맞춘 리샘플·회전·코드로 그림 그리기는 하지 않았으며, 불완전한 생성 격자는 품질 미달 사유에 포함됩니다. 파우치 후보 두 장 중 최종 한 장만 공통 자산으로 채택했습니다.

재현: `python NEWIMAG/Px32HeroV7/r1_process.py` 다음 `python NEWIMAG/Px32HeroV7/r1_report.py`. 생성 원본·프롬프트·공통 파우치·프레임별 합성 레이어와 3배 검수본은 Sources/R1_*에 있습니다. 이전 validation.json은 Sources/pre_R1_validation.json에 보존했습니다.

'''
section+=f"Run 팔 끝 x 왕복 범위: **{v['sheets']['Run']['arm_end_x_range_px']:g}px**. 아래 통과는 전체 요구사항 판정입니다.\n\n"+'\n'.join(table)+'\n'
(ROOT/'README.md').write_text(readme+section,encoding='utf-8')
(ROOT/'Sources/R1_summary_table.md').write_text('\n'.join(table)+'\n',encoding='utf-8')
print('\n'.join(table));print('Run range',v['sheets']['Run']['arm_end_x_range_px'])
