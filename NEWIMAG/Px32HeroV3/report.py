import json
from pathlib import Path
root=Path(__file__).resolve().parent
v=json.loads((root/'validation.json').read_text())
a=json.loads((root/'attempts.json').read_text())
lines=['# Re:Trap Hero V3 생성 결과 — 승인 규격 미달','',
'8종 후보 파일을 저장했지만, 전체 승인 조건을 충족하지 못했습니다. `validation.json`의 `overall_pass`와 각 시트의 `production_ready`는 false입니다. Player 폴더의 파일 역시 승인된 가져오기용 완성품이 아닙니다.','',
'## 파일','',
'- `_1x/Hero_{Idle,Run,JumpStart,Fall,Land,Dash,Hit,Death}.png`: 셀 128×128, 가로 한 줄.',
'- `Player/Hero_*.png`: 위 PNG의 정확한 최근접 4배.',
'- `Animations/Hero_*_3x.gif`: 3배 애니메이션 미리보기.',
'- `Compare_3x.png`: 위 동작 순서로 8행. 각 행 왼쪽은 기존 첫 프레임의 원래 픽셀, 오른쪽은 새 후보 첫 프레임. 원래 크기로 나란히 배치한 전체를 최근접 3배 표시.',
'- `Sources/`: image_gen 원본 모든 시도와 승인 시트의 참조용 4배 사본.',
'- `attempts.json`: 시도별 전체 프롬프트, 원본 경로, 추출 결과, 선택 시도.',
'- `validation.json`: 저장된 PNG 재검사 결과 및 프레임별 상세 수치.',
'- `process.py`, `verify.py`, `report.py`: 후처리·검증·보고서 재현 코드. 외부 패키지 없음.','',
'## 수치','',
'|시트|프레임 높이 px|가로 중심 px|높이 최대 편차 px|색 수|시도/선택|수치 통과|',
'|---|---|---|---|---|---|---|']
for name,s in v['sheets'].items():
    fs=s.get('frames',[])
    if not fs:continue
    lines.append('|'+name+'|'+','.join(str(f['height']) for f in fs)+'|'+str(min(f['center_x'] for f in fs))+'–'+str(max(f['center_x'] for f in fs))+'|'+str(max(abs(f['height_deviation']) for f in fs))+'|'+str(s['color_count'])+'|'+str(len(a['sheets'][name]['attempts']))+'/'+str(s['selected_attempt'])+'|'+('통과' if s['numeric_pass'] else '미달')+'|')
lines+=['','모든 프레임 바닥 행은 127, 금지 가장자리 불투명 픽셀은 0입니다. 알파는 0/255, 팔레트 밖 픽셀은 0이며, 4배 결과는 1배의 최근접 확대와 픽셀 단위로 일치합니다. Death는 높이/가로 중심 드리프트 검사 예외입니다. 수치 통과와 전체 제작 승인 여부는 별개입니다.','',
'## 디테일 및 한계','',
'생성 후보에서 눈동자 하이라이트와 표정, 머리카락 덩어리/가닥, 재킷 주름과 테두리, 상자 이빨·리벳, 신발 끈·밑창 디테일을 추가했습니다. 지퍼·재봉선의 일관된 유지와 모든 프레임의 동일 디자인은 보장하지 못했습니다.','',
'Idle은 4회 생성 후에도 96±1px에 맞지 않습니다. 일부 동작은 높이 편차 검사도 실패합니다. image_gen이 요청된 블록 격자, 캔버스/셀 배치, 체격, 얼굴·머리핀·상자 형태를 일관되게 유지하지 못했습니다. 원본 블록 내부 일치율을 validation.json에 기록했습니다. 따라서 전체 결과는 실패로 남깁니다.','',
'기존 캐릭터 확대본을 새 그림으로 사용하지 않았습니다. 새 그림은 전부 내장 image_gen으로 생성했습니다. 코드는 정수 k배 블록 중앙 샘플 복원, 배경/알파 이진화, 팔레트 정리, 고립점 정리, 프레임 평행이동과 시트/미리보기 조립만 수행합니다. 실루엣을 목표 높이에 맞춰 리샘플링하거나 일부를 잘라내지 않았습니다. 셀에 들어오지 않는 후보는 배제했습니다.','',
'저장소의 다른 리소스와 Assets는 수정하지 않았고 git은 사용하지 않았습니다.']
(root/'README.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
print('\n'.join(lines[17:30]))
