from build import *
import re

config=json.loads((SRC/'config.json').read_text(encoding='utf-8'))
settings={
'hair':'긴 백금발 하이 포니테일, 검정·빨강 곱창 밴드, 앞머리 삐침, 빨간 Z 핀',
'face':'빨간 눈, 송곳니, 장난스러운 웃음, 귀 피어싱',
'jacket':'어깨에서 흘러내린 오버사이즈 흰 재킷, 빨간 안감·옷깃, 손목까지 긴 퍼프 소매',
'hand':'소매 끝에 바로 검은 핑거리스 장갑, 맨 팔뚝 없음, 명확한 손',
'top':'빨간 왕관 무늬 검은 크롭 탑, 검은 초커',
'bottom':'검은 반바지, 은색 버클 벨트, O링, 늘어진 끈, 허벅지 가터',
'legs':'해부학적 오른다리 검은 니하이, 왼다리 빨간 무릎 양말',
'shoes':'두툼한 흰·검정 하이탑, 빨간 스트랩',
'bag':'진회색·검정 금속 상자, 빨간 뚜껑 가로띠, 손잡이, 리벳, 크고 뾰족한 흰 상하 지그재그 이빨, 어두운 빨강 입',
'hip_position':'옆 허리 벨트 옆에 짧은 끈/고리로 매달림, 몸 중심선보다 살짝 앞',
'held_position':'긴 대기에는 허리에서 떼어 손에 듦',
'size':'셀 128, 승인 Idle 머리·어깨·벨트·발바닥 행 유지, 축소 금지'}

# Shoulder/belt are manually identified anatomical landmarks in the final
# three-times review, not automatically inferred from the alpha bbox.
landmarks={
'Front':{'head_top':27,'shoulder':73,'belt':87,'sole':127},
'ThreeQuarter':{'head_top':27,'shoulder':70,'belt':86,'sole':127},
'Side':{'head_top':26,'shoulder':73,'belt':86,'sole':127},
'Back':{'head_top':28,'shoulder':70,'belt':87,'sole':127}}
reference={'head_top':27,'shoulder':70,'belt':86,'sole':127}
artifacts=[]

def checklist(fails=(),na=()):
    return {k:dict(status='not_applicable' if k in na else 'fail' if k in fails else 'pass',criterion=v) for k,v in settings.items()}

def record(path,checks,reasons,extra=None):
    a=np.array(Image.open(ROOT/path).convert('RGBA'));s=stats(a)
    assert s['colors']<=24 and set(s['alpha_values'])<={0,255}
    s.update(path=path,production_ready=False,setting_checks=checks,remaining_failures=reasons,visual_review_scale=3,visual_review='直接 눈 검사 / 3배 PNG, 손·가방은 8배 추가 확인')
    s.update(extra or {})
    artifacts.append(s)
    return s

for name in ['Front','ThreeQuarter','Side','Back']:
    rows=landmarks[name];d={k:rows[k]-reference[k] for k in rows}
    fails=['hand']
    reasons=['손의 3~5px·1px 강조점 규격을 모두 충족하지 못함']
    if name!='Back':fails+=['bag'];reasons+=['최종 저해상도 가방 이빨/리벳의 선명도 미달']
    if any(abs(v)>1 for v in d.values()):fails+=['size'];reasons+=['어깨 행이 승인 기준보다 3px 아래']
    if name=='ThreeQuarter':fails+=['hip_position'];reasons+=['가방 고리와 벨트 연결 및 옆구리보다 정면으로 읽히는 위치를 재검토해야 함']
    na=['held_position']+(['face','top','bag'] if name=='Back' else [])
    extra=dict(landmark_rows=rows,landmark_delta=d,row_alignment_pass=all(abs(v)<=1 for v in d.values()),landmark_method='머리·발: 알파 경계 자동 측정; 어깨·벨트: 3배 확대에서 의미 부위를 수동 지정',long_sleeves_to_wrist=True,bare_forearm_visible=False,anatomical_leg_consistency=True)
    if name=='ThreeQuarter':
        a=np.array(Image.open(ROOT/f'Turnaround/Hero_{name}.png'));m=np.array(Image.open(ROOT/'Masks/Hero_ThreeQuarter.png'))[:,:,0]>0
        extra['outside_mask_mismatch']=int(np.any(a!=BASE,axis=2)[~m].sum())
        extra['mask']='Masks/Hero_ThreeQuarter.png'
        assert extra['outside_mask_mismatch']==0
    record(f'Turnaround/Hero_{name}.png',checklist(fails,na),reasons,extra)

bag_notes={
'Bag_Hip_Front':['T자·네모처럼 읽히는 이빨, 고리가 상단이 아닌 하단에 남음'],
'Bag_Hip_Side':['폭 11·높이 20으로 허리 크기 범위 밖'],
'Bag_Hip_ThreeQuarter':['상하 이빨의 큰 삼각 지그재그 식별성 미달'],
'Bag_Hip_Open':['높이 19로 허리 크기 범위 밖, 이빨이 작은 점으로 단순화됨'],
'Bag_Held_Front':['하단 모서리 리벳 및 이빨 외곽선 분리 불충분'],
'Bag_Held_Side':['높이 19이나 폭 15로 목표 23보다 좁음; 크기 일치 미달'],
'Bag_Held_ThreeQuarter':['25×22로 목표 23×19보다 큼; 하단 이빨 일부가 합쳐짐'],
'Bag_Held_Open':['18×22로 목표 23×19와 다름; 일부 이빨이 점/짧은 막대로 단순화됨']}
for name in config['bags']:
    a=np.array(Image.open(ROOT/f'Bag/{name}.png'));h,w=a.shape[:2];hip='_Hip_' in name
    size_ok=(17<=w<=19 and 14<=h<=16) if hip else (22<=w<=24 and 18<=h<=20)
    side=name.endswith('Side')
    checks=checklist(['bag'],[k for k in settings if k!='bag'])
    if side:checks['bag']['status']='pass';checks['bag']['note']='측면에는 앞면 이빨 전체가 보이지 않는 것이 정상'
    details=dict(metal_box=True,red_lid_band=True,top_handle=True,dark_red_mouth='not_applicable' if side else True,sharp_interlocking_teeth='not_applicable' if side or name.endswith('Open') else False,sharp_open_teeth=False if name.endswith('Open') else 'not_applicable',corner_rivets=name not in ['Bag_Held_Front','Bag_Hip_Front'],one_pixel_outline=False if name.endswith('Open') else True,hip_hanger_at_top=(False if name in ['Bag_Hip_Front','Bag_Hip_Open'] else True) if hip else 'not_applicable')
    record(f'Bag/{name}.png',checks,bag_notes[name],dict(target_size=[18,15] if hip else [23,19],size_pass=size_ok,bag_detail_checks=details))

q=np.array(Image.open(ROOT/'Turnaround/Hero_ThreeQuarter.png'))
for i,name in enumerate(['Down','HandOnHip','Forward','Back']):
    a=np.array(Image.open(ROOT/f'Arms/Arm_{name}.png'));m=np.array(Image.open(ROOT/f'Masks/Arm_{name}.png'))[:,:,0]>0
    mismatch=int(np.any(a!=q,axis=2)[~m].sum());assert mismatch==0
    roi=config['hand_rois'][i]
    reasons=['긴 소매는 통과; 손 크기/명확한 장갑 실루엣/정확히 1px 하이라이트 전체 조건은 미달','3/4 기준면의 가방 이빨·걸이 위치 미달을 공유']
    extra=dict(mask=f'Masks/Arm_{name}.png',outside_mask_mismatch=mismatch,baseline='Turnaround/Hero_ThreeQuarter.png',landmark_rows=reference,hand_roi=roi,long_sleeves_to_wrist=True,bare_forearm_visible=False,hand_3_to_5_px_pass=False,one_highlight_pixel_pass=False,pose_pass=True,elbow_backward_triangle=(True if name=='HandOnHip' else 'not_applicable'))
    record(f'Arms/Arm_{name}.png',checklist(['hand','bag','hip_position'],['held_position']),reasons,extra)

source=ROOT.parents[1]/'Assets/_Project/ResourcceEX/Sprites/Px32/Player/Hero_Idle.png'
oldhash=json.loads((ROOT.parent/'Px32HeroV8/Sources/source_hash.json').read_text(encoding='utf-8'))['sha256']
newhash=hashlib.sha256(source.read_bytes()).hexdigest()
assert oldhash==newhash
assert np.array_equal(BASE,np.array(Image.open(source).convert('RGBA'))[:,:128])
sheet=np.array(Image.open(ROOT/'Turnaround/Turnaround_1x.png'))
assert sheet.shape==(128,512,4)
assert all(a['floor_row']==127 for a in artifacts if not a['path'].startswith('Bag/'))

attempts=[]
recovery=json.loads((SRC/'recovery.json').read_text(encoding='utf-8'))
for p in sorted(SRC.glob('*_generated.png')):
    m=re.match(r'(.+)_(\d+)_generated$',p.stem);name,num=m[1],int(m[2])
    selected=recovery.get(name,{}).get('attempt')==num
    if name=='ThreeQuarter':reason='1차 소매·팔/가림 복구 영역만 마스크 합성; 나머지 원본 보존' if selected else '가방이 반대편, T자 이빨; 1차 부분 패치보다 불리'
    elif name in ['Front','Side']:reason='3차: 왕관 빨강·버클·가방 위치 보정, 최선 후보지만 어깨 행 미달' if selected else '1차 세로 비율 과대; 2차 왕관색/가방 위치/버클 오류'
    elif name=='Back':reason='뒷면 방향·다리 비대칭 유지; 손 세부 미달'
    elif name.startswith('Arm_'):reason='2차 손 형태/강조점 개선; 규격 전체 통과는 아님' if selected else '1차 손 강조점/크기 미달'
    elif 'Open' in name:reason='2차 실제 뚜껑 벌림 채택; 크기/작은 이빨 미달' if selected else '입 벌림이 닫힌 직사각형처럼 생성됨'
    elif name=='Bag_Held_Side':reason='2차 측면 채택; 폭 미달' if selected else '측면 요청인데 앞면이 생성됨'
    else:reason='최선 후보 보존; 세부 미달은 validation.json 참조' if selected else '치아/손잡이 또는 비율 개선 부족'
    attempts.append(dict(item=name,attempt=num,selected=selected,production_ready=False,source=str(p.relative_to(ROOT)).replace('\\','/'),prompt=str(p.with_name(p.stem.replace('_generated','_prompt')+'.txt').relative_to(ROOT)).replace('\\','/'),reason=reason,tool='built-in image_gen'))
counts={n:sum(a['item']==n for a in attempts) for n in set(a['item'] for a in attempts)}
assert max(counts.values())<=3
for a in attempts:assert (ROOT/a['prompt']).is_file(),a['prompt']
(ROOT/'attempts.json').write_text(json.dumps(dict(production_ready=False,max_per_item=3,total_calls=len(attempts),counts=counts,items=attempts,Arm_Down='ThreeQuarter와 동일 이미지 재사용; 추가 생성 0회'),ensure_ascii=False,indent=2),encoding='utf-8')
report=dict(production_ready=False,stage=1,status='best_candidates_with_failed_checks',reference=dict(source=str(source),sha256=newhash,sha256_matches_v8_record=True,idle0_crop_exact=True,nominal_height=96,actual_alpha_height=101,landmark_rows=reference,height_discrepancy_resolution='축소 없이 승인본의 실제 y27..127 우선'),settings_table=settings,artifacts=artifacts,all_native_files_palette_pass=True,all_native_files_binary_alpha=True,all_character_floor_rows_127=True,all_mask_outside_mismatch_zero=True,recovery=json.loads((SRC/'recovery.json').read_text(encoding='utf-8')),processing='고정 정수 간격 블록 중심 표본·승인 20색 매핑·알파 128 이진화·마스크 합성·정수 평행이동·조립. 생성 격자는 근사값이며 정확한 격자 복원/디자인 성공으로 보증하지 않음. 생산 이미지 리사이즈/회전/코드 드로잉 없음. 확대본만 정수 픽셀 반복.',visual_review_files=['Sources/Turnaround_review_3x.png','Sources/Bag_review_3x.png','Sources/Arms_review_3x.png','Sources/Hands_review_3x.png'])
(ROOT/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')

labels={'Hero_Front':'턴어라운드 앞','Hero_ThreeQuarter':'턴어라운드 3/4','Hero_Side':'턴어라운드 옆','Hero_Back':'턴어라운드 뒤','Bag_Hip_Front':'허리 가방 앞','Bag_Hip_Side':'허리 가방 옆','Bag_Hip_ThreeQuarter':'허리 가방 3/4','Bag_Hip_Open':'허리 가방 입 벌림','Bag_Held_Front':'든 가방 앞','Bag_Held_Side':'든 가방 옆','Bag_Held_ThreeQuarter':'든 가방 3/4','Bag_Held_Open':'든 가방 입 벌림','Arm_Down':'팔 내림','Arm_HandOnHip':'허리에 손 C','Arm_Forward':'앞으로 뻗음','Arm_Back':'뒤로 흔듦'}
summary=['| 항목 | 결과 | 설정표 통과 | 시도 | 자체 평가 |','|---|---|---|---|---|']
for a in artifacts:
    stem=Path(a['path']).stem;key=stem.removeprefix('Hero_');n=counts.get(key,0)
    valid=[v for v in a['setting_checks'].values() if v['status']!='not_applicable'];passed=sum(v['status']=='pass' for v in valid)
    dim='×'.join(map(str,a['size']))
    short=a['remaining_failures'][0]
    if stem=='Arm_Down':n='0 (3/4 재사용)'
    summary.append(f"| {labels[stem]} | {dim} 후보 저장 | {passed}/{len(valid)} | {n} | 미달 — {short} |")
rows=['| 면 | 머리 | 어깨 | 벨트 | 발바닥 | 행 정렬 |','|---|---:|---:|---:|---:|---|']
for name,r in landmarks.items():rows.append('| '+name+' | '+' | '.join(str(r[k]) for k in reference)+' | '+('통과' if all(abs(r[k]-reference[k])<=1 for k in reference) else '실패')+' |')
md='# Re:Trap V9 1단계 모델 시트 검수\n\n모든 요청 파일을 후보로 저장했습니다. **전체 production_ready: false**. 긴 소매/맨 팔뚝 제거는 확인했지만, 가방 치아·크기·연결, 손의 3~5px/1px 강조점, 일부 면의 어깨 정렬이 미달입니다. 이 결과를 승인본으로 사용하면 안 됩니다.\n\n'+ '\n'.join(summary)+'\n\n## 행 측정\n\n기준 Idle 0은 명목 96px와 달리 실제 알파 경계가 y=27..127(101행)입니다. 승인본을 축소하지 않고 실제 행을 우선했습니다. 어깨·벨트는 3배 의미 부위 육안 지정이며, 머리·발바닥은 자동 측정입니다.\n\n'+'\n'.join(rows)+'\n\n## 검증과 한계\n\n- 원본 전체 SHA256은 V8 기록과 동일하며 Idle 0 복사본도 원본 셀과 RGBA가 같습니다.\n- 3/4는 승인 Idle 0 기준, 팔 4종은 최종 3/4 기준 마스크 밖 RGBA 불일치가 모두 0입니다. 팔 내림은 3/4와 같습니다.\n- 모든 생산 PNG는 승인 팔레트 내 20색 이하, 알파 0/255입니다. 캐릭터 8장은 모두 바닥 행 127입니다. 가방에는 캐릭터 바닥 행 규칙을 적용하지 않았습니다.\n- 가방은 앞·옆·3/4·입 벌림 순서, 확대 아틀라스 위쪽이 허리 크기, 아래쪽이 든 크기입니다. 폭/높이를 맞추기 위한 리사이즈는 하지 않았으며 미달 치수를 그대로 기록했습니다.\n- 3배 확대를 직접 검토했고 손·가방은 8배도 확인했습니다. 설정표 항목별 pass/fail/not_applicable은 validation.json에 있습니다. 가려진 뒤쪽 얼굴/상의와 별도 가방의 인체 항목은 N/A입니다. 설정표 통과 수와 추가 치수·손 세부 규격 통과는 별개입니다.\n- 생성 격자가 근사여서 고정 정수 블록 표본 복원 과정에서 이빨/리벳/손 세부가 손실됐습니다. 이를 성공한 픽셀 디자인으로 판정하지 않았습니다.\n- 내장 image_gen '+str(len(attempts))+'회. 항목당 최대 3회 이내. 정확한 프롬프트와 모든 생성 후보는 Sources/*_prompt.txt 및 *_generated.png, 채택 근거는 attempts.json입니다.\n- Assets, 다른 NEWIMAG 폴더를 수정하지 않았고 git 명령을 실행하지 않았습니다. 산출물은 이 폴더에만 기록했습니다.\n\n## 파일과 재현\n\nTurnaround/ 4면과 1x·4x 시트, Bag/ 8종과 8x 시트, Arms/ 4자세와 4x·손 8x 시트, Masks/ 흰색 편집 허용 마스크, Sources/ 생성 원본·프롬프트·3배 검사본·조립 코드입니다.\n\n`python NEWIMAG/Px32HeroRef/Sources/assemble.py` 후 `python NEWIMAG/Px32HeroRef/Sources/report.py`. config.json의 복원 간격과 선택 후보를 사용합니다. 코드는 블록 복원·팔레트 매핑·알파 이진화·마스크·평행이동·조립·검증만 수행합니다.\n'
(ROOT/'README.md').write_text(md,encoding='utf-8')
print(json.dumps(dict(artifacts=len(artifacts),imagegen_calls=len(attempts),max_attempts=max(counts.values()),original_unchanged=oldhash==newhash,outside_mask_mismatches=0,production_ready=False),ensure_ascii=False))
