"""Rebuild Korean report and generation audit from saved results (stdlib only)."""
from legacy_io import *
import hashlib
from process import ASSETS

REASONS={
 'Run':['포즈 8개 확보. 공중 높이 흔들림이 크며 머리 합성 시 어깨 수평 절단.','원본 머리가 상단에 닿고 실제 캔버스/픽셀 격자 불일치. 체격과 포즈 일관성이 개선되지 않음.','선택: 머리 없는 몸이 생성되어 어깨 절단 없이 Idle 전체 머리 합성 가능. 상하 흔들림 및 몸 방향 전환 결함은 남음.'],
 'JumpStart':['선택: 웅크림·박차기·무릎 상승 포즈 구분이 가장 명확하고 다른 시도보다 체격이 기준에 가까움. 머리 합성은 어깨 잘림 때문에 포기하고 생성 머리 예외.','실제 박차기 몸/다리가 길어 체격 편차가 커짐.','머리 없는 몸은 있으나 흰 배경과 그림자 포함. 4px 격자에서 목-발 몸 길이가 약94px로 기준54px를 크게 초과; 리샘플링 없이 셀 수용 불가.'],
 'Dash':['다섯 포즈 확보했으나 몸/상자 크기와 몸 기울기가 불안정하고 반투명 후광 발생.','선택: 준비·돌진3·감속 구분과 몸 비율이 상대적으로 양호. 후광은 알파128 이진화로 제거. 목/어깨 합성 부조화 때문에 생성 머리 예외.','지시와 달리 머리·문구·패널 배경이 생성되고 상자가 누락되어 즉시 탈락.'],
 'Hit':['선택: 눈 감은 움찔·뒤젖힘·복귀가 가장 명확. 목/어깨 합성선과 표정 소실 때문에 생성 머리 예외.','몸이 더 커졌으며 뒤젖힘과 왼쪽 반동이 1차보다 약함.','머리 없음 요청에도 머리카락이 남음. 뒤로 젖힌 몸 동작이 약해지고 표정 복원 불가; 1차보다 동작 전달이 떨어져 탈락.']}

def span(values,signed=False):
 lo,hi=min(values),max(values)
 def fmt(x):return f'{x:+g}' if signed else f'{x:g}'
 return fmt(lo) if lo==hi else fmt(lo)+'…'+fmt(hi)

def main():
 v=json.loads((ROOT/'validation.json').read_text(encoding='utf-8'));a=json.loads((ROOT/'attempts.json').read_text(encoding='utf-8'));lines=[]
 for n,s in a['sheets'].items():
  for rec in s['attempts']:
   p=ROOT/rec['source'];b=p.read_bytes();rec['sha256']=hashlib.sha256(b).hexdigest();rec['source_dimensions']=list(struct.unpack('>II',b[16:24]));rec['selection_reason']=REASONS[n][rec['attempt']-1]
   if rec['selected']:
    rec['recovery']=v['sheets'][n]['recovery'];rec['production_ready']=False
  m=v['sheets'][n];fs=m['frames'];heights=', '.join(str(f['height']) for f in fs);dw=span([f['head_delta_idle'][0] for f in fs],True);dh=span([f['head_delta_idle'][1] for f in fs],True);cx=span([f['center_x'] for f in fs]);lines.append(f"|{n}|{heights}|{dw} / {dh}|{cx}|{m['color_count']}|3 / {s['selected_attempt']}|미달|")
 a['sampling_note']='Run/Dash selected sources use fixed k=4; JumpStart/Hit selected large2172x724 sources use fixed k=6 based on visible repeated pixel-step spacing. Source grid itself is approximate, not certified exact; no per-frame fitting or silhouette scaling. Head composition uses only integer translation.'
 a['head_fallback_note']='JumpStart frames1–3, Dash frames1–5, Hit frames1–3 use generated heads after failed complete Idle-head composition. Sources/Composite_before_bodyonly_3x.png preserves the rejected trial. Run frames1–8 use exact complete Idle head.'
 (ROOT/'attempts.json').write_text(json.dumps(a,ensure_ascii=False,indent=2),encoding='utf-8')
 v['read_only_reference_hashes']={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in [ASSETS/f'Hero_{n}.png' for n in ['Idle','Run','JumpStart','Dash','Hit']]}
 v['file_checks']=json.loads((ROOT/'file_checks.json').read_text(encoding='utf-8'))
 (ROOT/'validation.json').write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf-8')
 table='|시트|프레임 키 px|머리 bbox Δ폭 / Δ높이 px|가로 중심 px|색 수|시도 / 선택|통과|\n|---|---|---|---|---|---|---|\n'+'\n'.join(lines)
 readme='''# Re:Trap 주인공 V4 — 후보 저장 완료, 제작 승인 미달

시트별 내장 image_gen 3회(총12회)를 사용했다. 가장 나은 후보 4종을 저장했으나 모두 `production_ready: false`다. 게임 리소스로 교체할 승인본이 아니다. Assets, 다른 NEWIMAG 폴더 및 git은 수정하지 않았다.

'''+table+'''

키는 불투명 bbox의 포함 높이이며 Idle 첫 프레임은101px이다. 머리 차이는 V3의 얼굴만44×40px가 아니라 **얼굴·앞머리·포니테일·리본·머리핀을 포함한 Idle 전체69×57px** 대비 값이다. 가로 중심은 전체 불투명 bbox 중심으로 Idle64px 대비 검사한다. 생성 머리의 bbox는 수동 의미 영역의 불투명 경계 측정이므로 어깨와 겹친 부분의 분할에는 한계가 있다. 자세별 전체 좌표와 판정은 validation.json에 있다.

## 머리 합성 및 실패 이유

- Run 1–8: Idle 전체 머리2180개 불투명 픽셀을 그대로 합성했다. 회전·리샘플·재색칠 없이 평행이동만 사용했고 픽셀 일치를 검사했다. 3차 머리 없는 몸에 붙여 초기 합성의 어깨 절단선을 줄였다. 머리 상하 범위6px로1~2px 조건을 실패하며, 4→5 몸 방향과 팔 형태가 바뀌고 8→1 연결이 충분히 자연스럽지 않다.
- JumpStart 1–3: Idle 머리 합성 시 목보다 높은 어깨 부분이 지워져 수평 잘림이 생겼다. 사용자 허용 예외에 따라 생성 머리를 사용했다. 웅크림→박차기→상승은 구분되지만 박차기 키103px로 Idle+2px이며 머리 크기가 다르다.
- Dash 1–5: 목선 위 제거 후 Idle 직립 머리를 얹으면 높은 어깨가 잘리고 수평 몸과 목의 연결이 어색했다. 생성 머리를 사용했다. 움직이는 포니테일과 옷은 유지했지만 머리 크기, 상자 형태, 돌진의 수평 기울기에는 결함이 남았다.
- Hit 1–3: 합성 어깨선이 어색하고, Idle 머리를 쓰면 눈 감기와 뒤로 젖힌 표정이 사라졌다. 생성 머리를 사용했다. 움찔/복귀는107px로 Idle+6px이며 머리 크기와 체격도 불일치한다.

생성 머리 예외를 통과로 간주하지 않았다. 초기 합성본은 Sources/Composite_before_bodyonly_3x.png에 보존했다. 전체 원본 12개와 시도별 전체 프롬프트·선택 이유·원본 경로·SHA-256은 Sources/ 및 attempts.json에 있다. 최종 프롬프트는 선택된 Run3, JumpStart1, Dash2, Hit1 기록이다. 3회 한도 후 추가 생성하지 않았다.

## 파일과 재현

- `_1x/Hero_Run.png`1024×128, `Hero_JumpStart.png`384×128, `Hero_Dash.png`640×128, `Hero_Hit.png`384×128. 셀128×128, 가로 한 줄.
- `Animations/Hero_*_3x.gif`:384×384, 정확한 최근접3배. Run만 루프한다. JumpStart는 마지막 프레임을 계속 유지한다. Dash/Hit도 한 번 재생 후 마지막 장을 남긴다.
- GIF는1/100초 단위라18/15/24/12fps를 프레임별 누적 반올림으로 근사한다. 지연(cs)은 Run6,5,6,5,6,5,6,5; JumpStart7,6,7; Dash4,4,4,5,4; Hit8,9,8이다. 정확한 게임 fps 설정은 요청값18/15/24/12를 사용해야 한다.
- `Compare_3x.png`:3456×1536. 위에서 Run, JumpStart, Dash, Hit 순. 각 행 첫 셀은 승인 Idle1, 이후 새 프레임 전부. 32px 눈금, 청록색 Idle 전체 머리 상·하단(y27/83), 황토색 바닥127 가이드. 확대는 정확한 최근접3배다.
- `validation.json`:프레임 키·머리 bbox·중심·색 수·알파·가장자리·개별 검사·실패 원인·production_ready. `file_checks.json`:별도 GIF LZW 디코드 및 PNG와 픽셀 단위3배 일치 검증.
- `process.py`, `legacy_io.py`, `layout.json`:복원·후처리·검증·조립. `verify.py`:독립 파일 검사. `report.py`:이 보고서 및 생성 이력 갱신. 모두 Python 표준 라이브러리만 사용한다.

```powershell
python NEWIMAG/Px32HeroV4/process.py
python NEWIMAG/Px32HeroV4/verify.py
python NEWIMAG/Px32HeroV4/report.py
```

## 검사 해석과 한계

모든 최종 시트는20색의 승인 Idle 팔레트, 알파0/255, 바닥127, 상단·좌우1px 투명, 중심64±2px 조건을 만족한다. 다만 **바닥127 접지와 하단1px 투명은 동시에 만족할 수 없다.** V3 방식대로 하단 접지를 허용하고 상단·좌우를 금지 가장자리로 검사했다. 네 변 전체 투명이라는 문자 그대로의 조건은 미충족이다.

Run/Dash는 고정4px, 큰 원본(2172×724)인 JumpStart/Hit는 반복 픽셀 계단 간격에 근거한 고정6px 블록 중앙 샘플을 사용했다. 원본은 엄밀한 정수 블록을 지키지 않으므로 이 복원은 근사이며, 외곽선1px와 세부 선의 일관성을 보증하지 않는다. 알파128 이진화, Idle 팔레트 매핑, 고립1픽셀 제거, 평행이동, 승인 머리 마스크 합성만 수행했다. 목표 키에 맞추기 위한 실루엣 리샘플, 회전, 몸 픽셀 그리기, 팔다리 변형은 하지 않았다. 격자·가이드는 비교 이미지에만 그렸다.

최종 파일 규격 및 GIF 디코드 검사는 통과했지만, 애니메이션 키포즈/루프의 자연스러움·디자인 일관성·완전한1px 외곽선은 통과하지 못했다. 수치와 눈 검수를 구분해 모든 프레임의 최종 pass 및 모든 시트 production_ready를 false로 남겼다. 승인 Idle과 게임의 V3 시트는 읽기만 했으며 Unity 가져오기·클립·fps 변경은 수행하지 않았다.
'''
 (ROOT/'README.md').write_text(readme,encoding='utf-8');print(table)

if __name__=='__main__':main()
