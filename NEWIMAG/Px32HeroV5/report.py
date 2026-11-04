"""Persist explicit visual review separately from measurable constraints."""
from legacy_io import *

REVIEWS={
 'Run':{'pass':False,'reason':'머리 높이 범위2px·인접 최대1px·루프1px로 떨림 수치는 개선. 그러나 0/4 접지에서 선행 다리 교대가 불명확하고 눌림·밀기 포즈 차이가 작아 요구한 좌우 4단계 달리기로 승인하지 않음.','selection':'3차는 공중 몸 길이가 회복되어 공중 발125/124행과 머리26을 동시에 만족. 1·2차보다 접지/공중 전환이 안정적.'},
 'Fall':{'pass':True,'reason':'두 장 모두 머리36·하단121행, 중심 차1px. 다리는 아래로 향하고 팔·옷자락에 작은 상향 변화가 있어 낙하 루프 연결을 통과. 머리카락은 전체 머리 고정 규칙에 따라 Idle 그대로이며 별도 휘날림 없음.','selection':'1차는 몸이 길어 머리36에 두면 셀을 넘음. 2차는 무릎을 굽힌 짧은 낙하 몸으로 목표 범위와 목 연결을 만족.'},
 'Land':{'pass':False,'reason':'머리40→32→27, 발127, 마지막 장은 Idle0과 RGBA 완전 일치. 그러나 생성 몸/상자가 Idle보다 가늘고 작아 1→2에서 어깨·상자가 갑자기 커짐. 마지막 장의 일치만으로 전체 회복 동작을 승인하지 않음.','selection':'3차만 0/1번 목표 머리40/32와 접지를 동시에 만족. 1차는 너무 길고 2차는 너무 짧음. 2번은 승인 Idle0 전체 합성.'}
}

def main():
 v=json.loads((ROOT/'validation.json').read_text(encoding='utf-8'));a=json.loads((ROOT/'attempts.json').read_text(encoding='utf-8'))
 lines=['# Re:Trap 주인공 V5 — Run · Fall · Land','', '내장 image_gen으로 Run 3회, Fall 2회, Land 3회(총 8회) 생성. 시트별 3회 제한을 지켰습니다. 수치 검증은 모두 통과했지만 시각 검수 결과 **Fall만 production_ready=true**, Run·Land는 최선 후보 보존입니다.','', '## 요약 표','', '프레임 번호는 0부터, 모든 좌표·키는 1x px입니다. 키는 전체 불투명 bbox의 높이, 중심은 bbox 가로 중심입니다. 통과는 수치/시각을 구분합니다.','', '|시트|프레임|머리 위 y|키|중심 x|색 수|시도/선택|통과(수치/제작)|','|---|---:|---:|---:|---:|---:|---|---|']
 for name,s in v['sheets'].items():
  rv=REVIEWS[name];s['visual_review']=rv;s['production_ready']=s['numeric_pass'] and rv['pass'];s['selection_reason']=rv['selection']
  for f in s['frames']:
   f['visual_pass']=rv['pass'];f['pass']=f['numeric_pass'] and rv['pass']
   lines.append(f"|{name}|{f['frame']}|{f['head_top_y']}|{f['height']}|{f['center_x']:g}|{f['color_count']}|{s['attempt_count']}/{s['selected_attempt']}|{'통과' if f['numeric_pass'] else '미달'}/{'통과' if f['pass'] else '미달'}|")
  a['sheets'][name]['selection_reason']=rv['selection']
  for item in a['sheets'][name]['attempts']:
   item['production_ready']=item['selected'] and s['production_ready']
   item['selection_reason']=rv['selection']
 v['overall_pass']=all(s['production_ready'] for s in v['sheets'].values())
 lines+=['','## 검수 결과','']+[f"- **{n}**: {r['reason']}" for n,r in REVIEWS.items()]
 lines+=['','## 파일과 재현','', '- `_1x/Hero_Run.png` 1024×128 / `Hero_Fall.png` 256×128 / `Hero_Land.png` 384×128. 모든 셀 128×128, 가로 한 줄.', '- `Animations/Hero_*_3x.gif`: 384×384. Run18fps, Fall8fps 루프, Land15fps 1회 후 마지막 장 유지. GIF 1/100초 제약으로 지연은 각각 6/5/6/5/6/5/6/5, 12/13, 7/6/7cs. 실제 게임 설정 fps는 요청값을 사용.', '- `Compare_3x.png`: 3840×1152. 위부터 Run/Fall/Land. 각 행의 첫 셀은 Idle0, 둘째는 현재 게임 해당 시트0, 이후는 V5 순서. 남는 셀은 투명.', '- `Sources/*_attemptN.png`: image_gen 원본 8개. `*_composite_3x.png`: 모든 시도의 후처리 비교. `Sources/generation.json`: 정확한 생성 프롬프트와 원본 경로.', '- `attempts.json`: 후보별 복원 블록 간격, 원본 SHA-256, 목 앵커/평행이동, 측정과 선택 이유. `validation.json`: 시트별 production_ready 및 프레임별 수치. `file_checks.json`: 독립 GIF 디코드·PNG 비교 결과.', '', '```powershell', 'python NEWIMAG/Px32HeroV5/process.py', 'python NEWIMAG/Px32HeroV5/report.py', 'python NEWIMAG/Px32HeroV5/verify.py', '```', '', '## 처리 규칙과 한계', '', '- 실제 Assets의 Idle0에서 V4 전체 머리 마스크(얼굴·앞머리·포니테일·머리핀)를 추출해 2180 불투명 픽셀을 합성. 저장 PNG의 모든 머리 픽셀 일치를 별도 검사. 머리 bbox 69×57. Land2는 Idle0 전체 RGBA 그대로 합성.', '- 몸은 내장 image_gen만 사용. 후처리는 V4 방식의 고정 블록 중심 복원, alpha128 기준 이진화, Idle 20색 팔레트 매핑, 고립1px 정리, 정수 평행이동, 승인 픽셀 합성·조립뿐. 프레임별 리샘플·회전·변형·새 캐릭터 픽셀 그리기 없음. 3x 미리보기는 픽셀 반복 확대만 사용.', '- 고정 복원 간격: Run1/2/3=5px, Fall1=10px·2=12px, Land1=6px·2/3=8px. 원본 확대 블록/외곽선 간격을 기준으로 정했으며 목표 bbox에 맞춘 프레임별 배율은 사용하지 않음. image_gen 원본은 완전한 정수 격자가 아니므로 복원은 근사이며 일부 외곽선 접합부 두께의 완벽한 1px는 보장하지 못함.', '- Run3 전체 세로 이동은 접지 기준 대비 [0,0,0,-2,0,0,0,-3]px. 목표 예시 곡선과 동일하지는 않지만 접지와 공중 높이를 지키면서 머리 범위2px·인접1px·루프1px를 만족.', '- 접지127행과 하단1px 투명은 서로 양립 불가하므로 V4처럼 접지 프레임의 하단만 예외. 상단/좌우1px는 모두 투명. Fall과 Run 공중 프레임 하단은 투명.', '- 머리카락 휘날림/처짐은 Idle 전체 머리 고정 지시와 양립하지 않아 머리 고정을 우선. 낙하의 팔·옷자락과 착지의 몸 포즈로 동작 표현.', '- Assets 및 다른 NEWIMAG 폴더는 참조로만 읽었고 git 작업은 하지 않음. Unity 가져오기·게임 플레이 검증은 이번 산출물 범위에 포함하지 않음.', '']
 for path,obj in [('validation.json',v),('attempts.json',a)]: (ROOT/path).write_text(json.dumps(obj,ensure_ascii=False,indent=2),encoding='utf-8')
 (ROOT/'README.md').write_text('\n'.join(lines),encoding='utf-8')
 print('\n'.join(lines[6:24]))
if __name__=='__main__':main()
