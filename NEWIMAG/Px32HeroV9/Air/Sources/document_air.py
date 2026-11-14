from build_air import *

def main():
 v=json.loads((O/'validation.json').read_text(encoding='utf-8'))
 checklist={
 '머리':'백금발 하이 포니테일·검정/빨강 밴드·Z핀 유지. 상승 끝머리 아래, 꼭대기 옆으로 뜸, 낙하 원본 위 방향.',
 '얼굴':'빨간 눈·표정 원본 보존. 작은 송곳니·피어싱은 원본 픽셀 보존 근거 병행.',
 '외투':'어깨 노출·흰 긴 퍼프 소매·빨강 안감 확인.',
 '손':'소매에서 검은 장갑으로 연결. 손은 빈손, 손가방 없음.',
 '상의':'검정 크롭 탑·초커 유지.',
 '하의':'검정 반바지·벨트·가터 유지. 작은 고리는 가방에 일부 가림.',
 '다리':'검정/빨강 비대칭 유지. Apex F1만 다리 중간 자세 생성.',
 '신발':'흰/검정 두툼한 하이탑·빨강 스트랩 확인.',
 '가방':'승인 Bag_Hip_ThreeQuarter·R2_chain 위 6행 그대로, 이빨·띠·상자 유지.',
 '가방 위치':'재킷 밑단 아래 뒤 허벅지 앞 허리춤. 가슴/무릎 아래 배치 없음.',
 '긴 대기':'해당 없음.',
 '크기':'축소 없음. 머리 꼭대기 y29~31, 기준 장과 동일. 꼭대기는 다리 펴기 때문에 bbox 높이가 달라짐.'}
 lag_data=json.loads((S/'bag_lag.json').read_text(encoding='utf-8'))
 for name,m in v['frames'].items():
  m['visual_review']='3배 시트 및 허리 8배 확인: 빈손·허리춤 가방 통과'
  m['visual_3x']=checklist.copy();m['bag_vertical_lag_px']=lag_data[name]['relative_current_hip_y']
  m['hip_landmark']=lag_data[name]['current_hip'];m['temporal_bag_lag']=lag_data[name]
 v['status']='artifacts_complete_with_asf_apex_height_findings'
 v['review_images']=['Sheet_3x.png','Waist_8x.png']
 v['bag_lag_scope']='이전 장 허리 좌표 + (-11,-2) + 방향별 세로 관성. 현재 허리 중립 대비 상승 +1/+2/+1, 낙하 -1/-1/-2/-1, 꼭대기 0. Sources/bag_lag.json에 반복 경계와 전체 흐름 진입 경계 포함.'
 v['asf']['Apex']['interpretation']='높이 드리프트 FAIL 3건 유지. 승인 상승(바닥120)과 낙하(바닥127)의 다리 자세 연결. 규격표 부재 경고와는 별개이며 전체 ASF 통과로 간주하지 않음.'
 v['prohibited_operations']={'scale_frames':False,'resample':False,'rotate':False,'code_drawing_art':False,'assets_modified':False,'other_newimag_modified':False,'git_used':False}
 dump('validation.json',v);dump('Sources/ASF_check.json',v['asf'])
 summary='''| 항목 | 결과 | 검사 |
|---|---|---|
| 낙하 | 4장 @10 · 512×128 | 빈손·허리춤, 기존 F0 해시 동일 |
| 상승 | 3장 @10 · 384×128 | 머리카락·자락 아래 방향, 가방 1~2px 아래 |
| 꼭대기 | 3장 @12 · 384×128 · 1회 | 뜨는 자락·다리 연결, 가방 중립 높이 |
| 픽셀 | 전 장 20색·알파 0/255 | 마스크 밖·가방 레이어 불일치 0 |
| ASF ④ | 상승·낙하 통과 / 꼭대기 높이 드리프트 3건 | ② 미실행, 규격표 경고 별도 기록 |
| 미리보기·기록 | 개별 GIF·전체 흐름·3배·허리 8배·Masks·Sources | GIF 재디코딩 불일치 0 |
'''
 lines=['# Re:Trap V9 — 공중 상승·꼭대기·낙하','', '2026-11-09. 산출물 제작 완료, 사용자 검수용. **전체 ASF 통과는 아님**: 꼭대기 높이 드리프트 3건을 숨기지 않고 기록했다. 기존 `Fall_F0.png`는 다시 저장하지 않았다. 그림은 내장 image_gen만 사용했다.','',summary,'## 장별 좌표·시도·검사','', '좌표는 0 기반 가방 PNG 좌상단. 체인 좌상단은 가방 +(5,-6). 낙하 순서는 원본0 수정 F0 → 사이 F1 → 원본1 수정 F2 → 사이 F3이다.','', '| 동작 | 장 | 가방 좌표 | image_gen 시도 | 검사 |','|---|---|---|---|---|']
 for kind,ko,n in [('Fall','낙하',4),('Rise','상승',3),('Apex','꼭대기',3)]:
  for i in range(n):
   name=f'{kind}_F{i}';m=v['frames'][name];p=m['bag_position'];a=f"{m['attempt']}/3"
   if name=='Fall_F0':a+=' · 기존 보존'
   if name=='Fall_F2':a+=' · 이전 1회 보수 예약'
   lines.append(f'| {ko} | F{i} | ({p[0]}, {p[1]}) | {a} | 빈손·허리춤 / 20색 / 마스크 밖 0 |')
 lines+=['','## 가방 높이와 지연','',
 'Run·JumpStart README의 **Claude 조정 이후** 높이를 기준으로 했다. 가방은 몸체보다 앞, 뒤 다리 허벅지 앞에 합성했다. 승인 가방과 체인 원본을 정수 이동했고 가방 레이어의 보이는 RGBA 불일치는 전 장 0이다.',
 '가방 위치 = **이전 장의 허리 기준점 + (-11,-2) + 세로 관성**. 현재 허리 중립 대비 상승은 아래 +1/+2/+1px, 낙하는 위 -1/-1/-2/-1px, 꼭대기는 높이 0px이다. 같은 몸을 유지하는 부분 사이 프레임에서도 이전 장 기준점을 사용한다. 꼭대기 가방 x54/54/53은 몸 x65/64/62의 이동을 한 장 늦게 따라간다. 허리 기준점은 반바지 뒤쪽 상단을 8배로 읽은 육안 랜드마크다.',
 '상승 허리 (65,87)×3; 꼭대기 (65,87)/(64,87)/(62,87); 낙하 (62,87)/(62,87)/(62,88)/(62,88). 세로 관성은 상승 +1/+2/+1, 꼭대기 0, 낙하 반복 -2/-1/-1/-1. 낙하 F0로 처음 진입할 때는 이전 꼭대기의 허리 y87과 -1px를 사용하고, 반복에서는 이전 낙하 F3의 y88과 -2px를 사용해 동일한 승인 F0 좌표 (51,84)를 유지한다. 수식·전후 장·현재 몸 대비 오프셋은 `Sources/bag_lag.json`에 기록했다.',
 '', '## 부분 수정·크기 보존','',
 '- 낙하 F0/F2: 기존 든 가방·든 팔 영역 제거, 가려진 재킷·몸·배경 복원. F0는 직전 실행 결과 그대로다.',
 '- 낙하 F1/F3: 앞 키의 머리·얼굴·몸·다리·신발 유지, 뒤팔·소매·자락만 사이 자세 생성. 전신 보간이 아니다.',
 '- 상승: 승인 JumpStart F2의 가방 없는 몸을 바탕으로 포니테일 끝·소매·빈손·자락 마스크 수정. 얼굴·머리 꼭대기·다리·신발 유지.',
 '- 꼭대기 F0: 상승 F2 부분 수정. F1: 같은 기준에서 허리 아래 다리 영역까지 마스크를 확장해 낙하 자세 사이 다리를 생성. F2: 낙하 F0의 끝머리·뒤팔·자락 부분 수정. 전신 새 그림은 사용하지 않았다.',
 '- 생성 마스크는 `build_air.py`·`continue_air.py`의 사전 지정 폴리곤/영역이다. 최종 edit 마스크는 생성 영역과 이전/새 가방 레이어 합집합이다. 결과 차이로 역산하지 않았다.',
 '- 생성본은 정수 간격 10의 블록 중심 복원 후 승인 Idle 팔레트 최근접 매핑, 알파 128 이진화, 마스크 합성. 보간 리샘플·회전·코드 드로잉 없음. 확대는 픽셀 반복이다.',
 '- 머리 꼭대기는 낙하 29/29/31/31, 상승 29/29/29, 꼭대기 29/29/29. 승인 기준의 같은 위치·크기를 보존했다. 머리 꼭대기 보호 띠 y0..44 불일치도 전 장 0.',
 '', '## 설정표 3배 눈 검사','', '| 설정표 | 결과 |','|---|---|']
 for k,val in checklist.items():lines.append(f'| {k} | {val} |')
 lines+=['','## ASF ④ 검사','',
 '`tools/sprite/asf-to-px32.py`의 check/rules_for/sheet_spec만 사용했다. 세 동작 모두 공중이므로 Hero_Fall의 airborne 규칙을 적용했으며 기준선 배치·② scale_frames는 실행하지 않았다.',
 '상승·낙하 실패 0. 꼭대기는 bbox 높이 92/92/99px, 평균 94.3px에서 ±2px를 벗어나 **높이 drift FAIL 3건**이다. 승인 도약과 낙하의 최하단 행 120/127 차이를 유지한 결과다. 이를 지우거나 통과로 바꾸지 않았다. 가로 중심·색·알파·클립 실패는 없다.',
 '예상 규격표 경고: Hero_Fall 기존 2장→4장, Hero_Rise/Hero_Apex 기존 표에 없음. 이 경고와 위 실제 검사 실패는 구분했다. 원문은 `Sources/ASF_check.json`.',
 '', '## 미리보기·시도 이력','',
 '`Hero_Fall_4_3x.gif`: 4×100ms 반복. `Hero_Rise_3_3x.gif`: 3×100ms 반복. `Hero_Apex_3_3x.gif`: 80/90/80ms, 총 250ms, 반복 확장 없음. GIF의 10ms 시간 단위 때문에 12fps를 이 배분으로 표현했다.',
 '`Jump_Flow_3x.gif`: 승인 JumpStart 4×50ms → Rise 3장×2회×100ms → Apex 80/90/80ms → Fall 4장×2회×100ms. 21장·1850ms. GIF 재디코딩의 보이는 RGBA 불일치 0, 장수·시간 검증 완료.',
 '`Sheet_3x.png`·`Waist_8x.png`: 위부터 낙하 / 상승 / 꼭대기, 각 행 왼쪽부터 F0 순서. 개별 동작 3배 PNG 및 허리 8배 PNG도 제공한다.',
 '이전 `Fall_F2_prompt1.txt`에는 undefined만 있고 생성본이 없었다. 과거 호출 여부를 확정할 수 없어 1회를 보수적으로 예약하고 이번 호출을 2번으로 기록했다. F0는 기존 1회, 다른 8장은 각 1회. 이번 실제 성공 호출 9회, 보존 원본 포함 성공 이미지 10개, 보수 예약 합계 11회. 장별 최대 3회 이내.',
 '', '## 파일·재현','',
 '최종 프롬프트는 `Sources/*_prompt*.txt`, 내장 생성 원본은 `Sources/*_generated*.png`, 블록 복원·가방 없는 몸·가방 레이어·측정은 같은 폴더에 있다. 검사 결과는 `validation.json`, 시도 이력은 `attempts.json`이다.',
 '기존 F0 SHA256: `'+v['approved_Fall_F0_sha256']+'`.',
 '가방 지연 재현: `python NEWIMAG/Px32HeroV9/Air/Sources/lag_air.py` (기존 F0는 검사만). 조립/검증 재현: `python NEWIMAG/Px32HeroV9/Air/Sources/finish_air.py` 다음 `python NEWIMAG/Px32HeroV9/Air/Sources/document_air.py`. 뒤 두 스크립트는 장별 최종 PNG를 다시 쓰지 않는다. `build_air.py fall0`는 기존 F0를 덮어쓰므로 실행하지 않았다.',
 '프로젝트 쓰기는 Air 안으로만 제한했다. Assets·다른 NEWIMAG 폴더·git은 건드리지 않았다.']
 (O/'README.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
 print('README and final review records saved')
if __name__=='__main__':main()
