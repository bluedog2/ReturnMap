import sys,json,pathlib,hashlib
sys.dont_write_bytecode=True
from process import ROOT,SPEC,ANIMS
attempts=json.loads((ROOT/'attempts.json').read_text(encoding='utf-8'))
for path in sorted((ROOT/'Sources').glob('*_attempt2_prompt.json')):
 rec=json.loads(path.read_text(encoding='utf-8'))
 if not any(r['sheet']==rec['sheet'] and r['attempt']==rec['attempt'] for r in attempts['records']):attempts['records'].append(rec)
attempts['records'].sort(key=lambda r:(list(SPEC).index(r['sheet'].rsplit('_',1)[0]),list(ANIMS).index(r['sheet'].rsplit('_',1)[1]),r['attempt']))
attempts['total_calls']=len(attempts['records']);attempts['maximum_per_sheet']=3;attempts['sheets']=[]
for s in SPEC:
 for a in ANIMS:
  name=f'{s}_{a}';n=2 if s=='Skeleton_Archer' and a in ('Run','Death') else 1
  attempts['sheets'].append({'sheet':name,'attempts':n,'selected_attempt':n,'selected_source':f'Sources/{name}_attempt{n}.png','reason':'Second generation selected for cleaner flat shade clusters and less visual noise.' if n==2 else 'First generation selected after grid recovery, visual review and automatic checks.'})
(ROOT/'attempts.json').write_text(json.dumps(attempts,ensure_ascii=False,indent=2),encoding='utf-8')
v=json.loads((ROOT/'validation.json').read_text(encoding='utf-8'))
for r in v['sheets']:
 a=next(a for a in attempts['sheets'] if a['sheet']==r['sheet']);r['attempts']=a['attempts'];r['selected_source']=a['selected_source']
v['gif_checks']=json.loads((ROOT/'Sources/gif_validation.json').read_text(encoding='utf-8-sig'))
v['comparison']={'path':'Compare_3x.png','scale':3,'hero':'NEWIMAG/Px32HeroV3/_1x/Hero_Idle.png','ruler_spacing_native_px':32,'v2_sprites_resampled_for_final_art':False}
v['limitations']=['Generated sources did not obey exact 4x blocks or exact grid/canvas dimensions; new art was recovered onto the requested native grid using palette-mode block sampling, then translated into cells.','Imp Death is exempt from hover-center rule to satisfy requested fall and final prone pose.','Single-pixel charcoal contours and flat 3-4 tone shading were visually reviewed; local contour thickness and tiny clusters may vary. Palette and binary alpha checks alone do not certify all stylistic requirements.','GIF previews use 120ms per frame and loop, including Hit and Death. Engine timing was not specified or modified.']
(ROOT/'validation.json').write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf-8')
lines=['# Re:Trap Enemy V3','', 'image_gen으로 새로 그린 5종 × 4애니메이션, 20시트 / 90프레임. 기존 v2 픽셀은 최종 그림에 사용하지 않고 4배 참조본으로만 사용했습니다.','', '## 파일','', '- `_1x/<종>_<애니>.png`: 1배, 가로 한 줄 시트 20장.','- `Enemies/<종>_<애니>.png`: 정확한 최근접 4배, 가져오기 대상 20장.','- `Animations/<종>_<애니>_3x.gif`: 최근접 3배 GIF 20장, 프레임당 120ms.','- `Compare_3x.png`: v2/v3 Idle 첫 프레임과 Hero V3, 32px 눈금.','- `Sources/*_attempt*.png`: image_gen 원본 22장.','- `Sources/References/*_v2_4x.png`: 승인 v2 참조본 20장.','- `Sources/*_recovery.json`: 선택 원본의 격자 복원 정보.','- `Sources/process.py`, `compare.ps1`, `finalize.py`: 후처리·검사·비교판 생성 도구.','- `attempts.json`: 전체 프롬프트, 원본 경로, 시도 및 선택 기록.','- `validation.json`: 20시트 및 프레임별 검사 수치.','', '## 검사','', '전체 20/20 통과. 모든 시트: 비이진 알파 0, 팔레트 밖 색 0, 금지 가장자리 픽셀 0, 정확한 4배 저장. 지상 전 프레임 바닥 정렬. 임프 Idle/Run/Hit 중앙 오차 0px.','', '| 시트 | 셀 | 프레임 | 색 수 | 중심 드리프트 px | 높이 드리프트 px | 시도 |','|---|---:|---:|---:|---:|---:|---:|']
for r in v['sheets']:lines.append(f"| {r['sheet']} | {r['cell']} | {r['frames']} | {r['colors']} | {r['max_center_drift']} | {r['max_height_drift']} | {r['attempts']} |")
lines+=['', 'Death 높이 드리프트는 쓰러지는 동작의 의도된 변화이며 검사 예외입니다. Idle 첫 키는 고블린 64, 임프 64, 스켈레톤 96, 기사 96, 오크 128px입니다.','', '## 추가 디테일','', '- 고블린: 후드 봉제선, 눈·이빨, 천 주름과 찢김, 벨트·장갑·금속 부품.','- 임프: 두개골 균열·이빨, 날개 뼈대와 막 음영, 꼬리 띠와 발톱.','- 스켈레톤: 두개골·치아·갈비뼈·손가락, 석궁 시위·장치, 찢어진 옷자락.','- 기사: 투구 통풍구, 판금 관절·체인, 방패 리벳·X 페인트 긁힘, 금속 하이라이트.','- 오크: 표정·엄니·손가락, 조끼 봉제선·찢김, 체인 고리, 파이프 칼라·플랜지·볼트.','', '## 시도 및 한계','', '총 image_gen 22회. 스켈레톤 Run/Death 각 2회, 나머지 18시트 각 1회. 첫 스켈레톤 Run/Death의 작은 점과 질감을 줄이기 위해 재생성했습니다.','', '생성 원본은 요청한 정확한 4배 블록·캔버스·균등 격자를 지키지 않았습니다. 새 원본의 투명 간격으로 프레임을 분리하고, 프레임 경계상자 기준 격자 복원과 팔레트 정리·알파 이진화·고립점 제거·평행이동을 수행했습니다. 새 얼굴이나 장비를 코드로 그리거나 기존 v2 그림을 확대하여 최종 그림으로 쓰지 않았습니다. 비교판의 글자와 눈금만 코드로 표시했습니다.','', '임프 Death는 중앙 기준 대신 추락 궤적과 마지막 바닥·누운 자세를 적용했습니다. 외곽선 두께와 3~4단 평면 음영은 육안 검사이며, 모든 국소 경계가 정확히 1px임을 자동 보증하지는 않습니다. 모델이 동작마다 세부 형태를 조금 바꿀 수 있습니다. 게임 엔진에서의 실제 재생·가져오기는 수행하지 않았습니다.','', '지정 폴더 밖 저장소 파일은 수정하지 않았고 git은 사용하지 않았습니다. image_gen 기본 원본은 허용된 CODEX_HOME에 남아 있습니다. 임시 작업 파일은 정리했습니다.','']
(ROOT/'README.md').write_text('\n'.join(lines),encoding='utf-8')
files=[p for p in ROOT.rglob('*') if p.is_file() and '_tmp' not in p.parts and '__pycache__' not in p.parts and p.name!='manifest.json']
manifest=[{'path':str(p.relative_to(ROOT)).replace('\\','/'),'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in sorted(files)]
(ROOT/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
print(json.dumps({'passed':v['passed'],'sheets':len(v['sheets']),'calls':attempts['total_calls'],'manifest_files':len(manifest)},indent=2))
