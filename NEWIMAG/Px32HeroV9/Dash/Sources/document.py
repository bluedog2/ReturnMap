import sys
sys.dont_write_bytecode=True
import build as b
from build import json,np,Image
def main(kind):
 O=b.ROOT/'NEWIMAG/Px32HeroV9'/kind;S=O/'Sources';v=json.loads((O/'validation.json').read_text(encoding='utf-8'));p=v['placement'];order=p['order'];n=len(order);fps=20 if kind=='Land' else 28
 land=kind=='Land';j=0 if land else 3
 keys=[b.read(S/f'F{i}_bagless.png') for i in range(3 if land else 5)]
 changes=[int(np.any(keys[i]!=keys[i+1],2).sum()) for i in range(len(keys)-1)]
 visual={
 '머리':'백금발 하이 포니테일·검정/빨강 밴드·Z핀 유지. 머리 꼭대기 원본 동일.',
 '얼굴':'빨간 눈·웃음 원본 유지. 작은 송곳니·피어싱은 원본 보존도 근거로 사용.',
 '외투':'어깨 노출, 흰 긴 퍼프 소매·빨간 안감 확인.',
 '손':'소매 끝 검은 핑거리스 장갑·빈손 확인, 손가방 없음.',
 '상의':'검정 크롭 탑·초커 유지. 작은 왕관 무늬는 원본 표현 한계 포함.',
 '하의':'반바지·벨트·가터 유지, 옛 가방에 가려진 부분은 생성 복원.',
 '다리':'원본 비대칭 검정/빨강 양말 유지. 복원 마스크 안의 가려졌던 허벅지만 생성.',
 '신발':'두툼한 흰/검정 하이탑·빨간 스트랩 유지. 최하단 y127.',
 '가방':'승인 Bag_Hip_ThreeQuarter 픽셀 그대로. 상자·빨간 띠·손잡이·리벳·맞물린 이빨 유지.',
 '위치':'3배 시트·허리 8배 직접 확인. 손과 분리, 재킷 밑단 옆 뒤 허벅지 앞. 높이 보정과 엄밀한 지연 조건의 차이는 별도 미통과 기록.',
 '긴 대기':'해당 없음.',
 '크기':'리샘플·회전·축소 없음. 생성본 전체를 사용하지 않고 사전 지정 마스크만 합성.'}
 v['visual_3x']=dict(reviewed=True,files=['Sheet_3x.png','Waist_8x.png','Sources/Transition_3x.png'],settings=visual,handheld_bag_frames=[],strict_one_frame_lag_pass=False)
 v['inbetween']=dict(interval=[j,j+1],selection='가방 없는 수정 키의 인접 RGBA 변화량 최대',adjacent_changes=changes,scope='앞 장 머리·몸·다리 유지; 팔·소매·자락 부분 보간')
 v['unmet_requirements']=([
  'Land F0 진입은 낙하 F3의 허리 (62,88) 대신 착지 접촉 기준 (62,102)으로 초기화함. 엄밀한 전환 1장 지연 아님.',
  'Land F1은 이전 I0 허리 기준 수식의 (51,101)에서 위로 6px 이동해 (51,95). 엄밀한 고정 오프셋 1장 지연 아님.'
 ] if land else ['Dash F4는 이전 I3 허리 기준 수식의 (52,94)에서 위로 6px 이동해 (52,88). 엄밀한 고정 오프셋 1장 지연 아님.'])
 (O/'validation.json').write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf-8')
 rows=[]
 for name in order:
  f=p['frames'][name];q=v['frames'][name]
  rows.append(f'| {name} | {tuple(f["hip"])} | {tuple(f["previous_hip"])} | {tuple(f["bag_position"])} | {tuple(f["inertia"])} | {tuple(f["review_translation"])} | {q["head_top"]} / {q["bottom"]} | 1/3 | 0 |')
 summary=[
 '| 항목 | 결과 | 검증 |','|---|---|---|',
 f'| 최종 시트 | {n}장 @{fps} · {n*128}×128 | '+('0.200초' if land else '0.214286초(약 0.21초)')+' |',
 '| 손가방·팔 | 전 장 빈손·긴 소매·장갑 | 내장 image_gen 장별 1/3회 |',
 '| 허리 가방 | 승인 가방 정수 이동·높이 보정 | '+('F1 위로 6px' if land else 'F4 위로 6px·뒤로 2~3px·기울어진 체인')+' |',
 '| 픽셀·ASF ④ | 통과 | 20색·알파 0/255·마스크 밖 0·바닥 127·② 미실행 |',
 '| 미리보기 | 3배 GIF·Compare·Transition·3배/허리 8배 시트 | GIF 재디코딩 불일치 0 |',
 '| 전체 상태 | **부분 완료: 엄밀한 1장 지연 미통과** | '+('진입 기준 초기화·F1 높이 보정' if land else 'F4 높이 보정')+' |']
 text=f'# Re:Trap V9 — {kind} 3단계\n\n2026-11-09. 요청 산출물 작성·3배 눈 검사 완료. **모든 승인 조건을 충족한 완성본은 아니다.** 높이 보정 때문에 엄밀한 이전 장 기준 고정 오프셋 규칙에 예외가 남았다. 이를 통과로 기록하지 않는다.\n\n'+ '\n'.join(summary)+'\n\n## 장별 배치·검사\n\n좌표는 0 기반 셀 좌표, 가방 PNG 좌상단이다. 기본 오프셋은 승인 Run의 (-11,-2).\n\n| 장 | 현재 허리 | 이전 허리 | 최종 가방 | 관성 (x,y) | 눈 검사 후 이동 | 머리/바닥 | 생성 | 마스크 밖 |\n|---|---|---|---|---|---|---|---|---|\n'+'\n'.join(rows)+'\n\n## 지연·높이 보정과 남은 제한\n\n'
 if land:
  text+='Land 가방은 F0→I0에서 아래 2px 눌리고 이후 원위 높이로 돌아오는 착지 표현이다. 같은 웅크림 몸인 I0를 양끝 가방 평균으로 올리지 않았다. Land F1은 이전 I0 허리를 그대로 따르면 가방 (51,101)이 무릎 쪽으로 내려가므로 **정수 이동 (0,-6)**으로 (51,95)에 올렸다. F2는 이전 F1 기준 (51,92)이다.\n\n낙하 마지막 허리 y88을 Land F0에 그대로 대입하면 가방 y86으로 웅크린 몸의 얼굴/가슴 높이가 된다. F0는 접촉 자세 허리 y102에서 시작해 가방 y100을 사용했다. 따라서 전환 진입과 F1은 엄밀한 1장 지연 미통과다. 3배 및 허리 8배에서 F0/I0는 깊게 접힌 뒤 허벅지 앞, F1/F2는 재킷 밑단 아래를 확인했다. 무릎이 올라온 웅크림에서는 서 있는 자세의 무릎 행과 단순 비교하지 않았다.\n\n'
 else:
  text+='가방 x는 이전 장 허리 +(-11,-2)에 뒤쪽 관성 -2/-3/-3/-3/-3/-2px를 적용했다. F0 이전 몸 기준은 승인 Run 마지막 I7의 기준 몸 F7 (63,96)이다. F4에서 y94를 그대로 쓰면 가방이 낮아져 **정수 이동 (0,-6)**으로 y88에 올렸다. 이 장은 엄밀한 고정 오프셋 1장 지연 미통과다.\n\n체인은 승인 R2_chain 위 6행을 16×16 작업 캔버스에 합성해 64배 반복한 참조를 내장 image_gen으로 수정했다. 코드로 기울기를 그리지 않았다. 생성본을 정수 pitch78·원점(0,0) 블록 중심 복원, 팔레트 매핑·알파 이진화 후 (5,5)~(10,11)만 추출한 **5×6px** 자산이다. 아래가 왼쪽으로 3px 기울어진 자산을 전 장 정수 이동했다. `Sources/Chain_*`에 프롬프트·생성본·복원 기록이 있다.\n\n'
 text+=f'## 사이 장과 원본 보존\n\n가방 없는 수정 키의 인접 변화량: {changes} 픽셀. 가장 큰 F{j}→F{j+1}에 I{j}를 넣었다. 순서: '+ ' → '.join(order)+'. 승인 Run·JumpStart 방식처럼 **팔·소매·자락의 부분 보간이며 전신/다리 보간은 아니다.** 머리·다리는 앞 장을 유지한다.\n\n`Sources/build.py`의 사전 지정 해부학적 폴리곤이 생성 허용 마스크다. 결과 차이에서 역산하지 않았다. 최종 edit 마스크는 생성 마스크와 가방·체인 레이어의 합집합이다. 옛 가방에 가려졌던 영역만 생성본으로 복원하며 마스크 밖 원본 RGBA 불일치 0. 몸 크기·머리 꼭대기 원본 유지. 원본의 마스크 밖 잔상·외곽 픽셀도 유지했다.\n\n생성 원본은 1254×1254, 정수 간격 9/10과 원점을 보호된 머리 구간으로 대조하여 블록 중심 복원했다. 장별 결과는 `Sources/*_recovery.json`. 승인 Idle 20색 최근접 매핑·알파 128 임계 이진화·마스크 합성만 수행했다. 리샘플·회전·코드 드로잉 없음. 확대는 정수 픽셀 반복이다.\n\n## 설정표 3배 눈 검사\n\n| 설정표 항목 | 관찰 |\n|---|---|\n'+ '\n'.join(f'| {k} | {val} |' for k,val in visual.items())+'\n\n## ASF ④와 미리보기\n\n`tools/sprite/asf-to-px32.py`의 check/rules_for/sheet_spec만 호출했다. scale_frames(②)·place는 실행하지 않았다. 검사 실패 0. 규격표 '+('Land 3→4' if land else 'Dash 5→6')+'장 차이는 예상 경고로 따로 기록했다. bbox 높이는 '+str([q['height'] for q in v['asf']['metrics']])+'px. 원본 '+('착지 웅크림에서 일어서기' if land else '몸 기울기·포니테일·다리 벌림')+'에 따른 변화이며 축소로 맞추지 않았다.\n\n'
 if land:text+='Land GIF는 각 50ms, 4장 200ms. Transition은 승인 Fall_F3 → Land 4장 → 승인 Idle 8장 시트의 첫 장, 각 50ms로 6장 300ms.\n'
 else:text+='Dash 게임 타이밍은 6/28=0.214286초. GIF는 10ms 단위 한계로 30/40ms를 분배한다. 애니메이션 7주기 42장=1500ms, Transition은 승인 Run 마지막 → Dash 6장 → Run 첫 장의 8장 흐름을 7회 배치해 56장=2000ms, 평균 정확히 28fps.\n'
 text+='Compare는 왼쪽 원본 / 오른쪽 수정본. 삽입 시점의 왼쪽은 해당 기준 원본 장을 유지하고 최종 동작 fps로 동기화한 비교다(원본 고유 fps 비교가 아님). 모든 GIF를 다시 디코딩해 보이는 RGBA 불일치 0·장수·누적 시간 확인.\n\n## 파일·재현\n\n장별 PNG, `Hero_'+kind+f'_{n}.png`, `Hero_{kind}_{n}_3x.gif`, `Compare_3x.gif`, `Transition_3x.gif`, `Sheet_3x.png`, `Waist_8x.png`, `Masks/`, `Sources/`, `validation.json`, `attempts.json` 저장. 정확한 프롬프트는 `Sources/*_prompt1.txt`, 원본 생성 결과는 `*_generated1.png`. 그림은 **내장 image_gen만** 사용했다.\n\n루트에서:\n\n```powershell\n'+f'python NEWIMAG/Px32HeroV9/{kind}/Sources/build.py {kind}\npython NEWIMAG/Px32HeroV9/{kind}/Sources/assemble.py {kind}\npython NEWIMAG/Px32HeroV9/{kind}/Sources/finish.py {kind}\npython NEWIMAG/Px32HeroV9/{kind}/Sources/document.py {kind}\n'+'```\n\n프로젝트 쓰기는 Land·Dash 두 폴더에만 수행했다. Assets·다른 NEWIMAG·git은 수정/실행하지 않았다.\n'
 (O/'README.md').write_text(text,encoding='utf-8')
 print(kind+' documented')
if __name__=='__main__':main(sys.argv[1])
