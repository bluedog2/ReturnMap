from pathlib import Path
import json
R=Path('NEWIMAG/Px32HeroV9/Run');M=R.parent/'Move'
section='''

## 머리 묶음 고정 (2026-11-14)

최신 결과는 `validation.json.head_bundle_lock`이다. 이전 얼굴 고정 스크립트는 실행하지 않는다.

Key0의 얼굴·앞머리·머리 위·뒤통수·빨강/검정 머리끈·포니테일 뿌리를 한 덩어리로 고정했다. 기존 FaceLock 마스크에 x60~110/y20~63을 합친 `Masks/HeadBundle_core.png`를 사용한다. 원위치와 정수 이동 위치의 합집합을 교체해 옛 머리 조각도 남기지 않았다. 시트 순서는 F0,F1,F2,F3,F4,F5,I5,F6,F7,I7이고 y 이동은 **[0,0,-1,0,-2,-2,-2,-1,-2,-2]**, x 이동은 0이다. 머리 크기와 Key0 원본은 불변이다.

뿌리 왼쪽 x51~62/y22~64(각 y 이동 반영)에서 고정 마스크를 뺀 영역만 꼬리 이음새로 사용했다. 내장 image_gen 결과를 정수 블록 격자로 복원하고 승인 Idle 팔레트 매핑·알파 이진화 후 그 마스크 안에만 합성했다. 먼 꼬리와 몸·팔·다리·가방·체인은 직전 픽셀 그대로다. 리샘플·회전·코드 드로잉은 사용하지 않았다.

| 항목 | 결과 | 검증 |
|---|---|---|
| Run 10장 머리 묶음 | Key0 머리끈·뿌리·뒤통수까지 동일 덩어리 | 정수 이동 후 템플릿 불일치 0, 축소 없음 |
| 꼬리 이음새 | F1~마지막 장 각각 내장 image_gen 1회, Key0 0회 | 6배 직접 눈 검사: 머리끈 부착·연결 확인, 떠 있는 조각·구멍 없음 |
| 원본 보존 | 몸·팔·다리·가방·체인·먼 꼬리 유지 | 마스크 밖·y82 이하·x51 왼쪽 꼬리 불일치 0 |
| Move 연결 | Turn F0=(-20,-4), RunStart F2=Run0, RunStop F0=Run9, Run_L_10 반영 | 머리 템플릿·연결 끝점·마스크 밖 불일치 0 |
| 픽셀 규칙 | 승인 Idle 팔레트, 24색 이하, 알파 0/255, 셀128·바닥127 | ASF ④만 검사, 결과는 HeadBundle_ASF_check.json |
| 미리보기 | Run/Move GIF·Sheet_3x.png·HeadBack_6x.png 갱신 | GIF 재디코딩 보이는 RGBA 불일치 0 |
| 보존·기록 | Run/Sources/prev3, Move/Sources/prev2 | validation·attempts·생성 원본·프롬프트·마스크 저장 |

`HeadBack_6x.png`는 x45~95/y18~67을 6배 픽셀 반복한 띠다. 위 직전 / 아래 새 결과이며 10장 순서다. 전체 띠와 `Sources/HeadBack_first5_6x.png`, `HeadBack_last5_6x.png`, 전신 3배 및 Turn 연결 시트를 직접 확인했다. Key0의 원래 검정/크림 머리 내부 명암은 보존한다.

내장 image_gen 총 10회(Run 9회, Turn F0 1회), 장별 최대 1/3회다. 생성 프롬프트는 `Sources/HeadBundle_prompt1.txt`, 원본은 `HeadBundle_*_generated1.png`, 복원 격자는 `HeadBundle_recovery.json`. Run은 간격10/원점(-18,-11), Turn은 간격10/원점(-14,-10)으로 블록 중심을 복원했다. Move의 다른 장과 미러 시험본은 해당 결과를 재사용했다.

재현은 `python NEWIMAG/Px32HeroV9/Run/Sources/head_bundle_recover.py`, `python NEWIMAG/Px32HeroV9/Run/Sources/head_bundle_finalize.py`, `python NEWIMAG/Px32HeroV9/Run/Sources/head_bundle_document.py` 순서다. prepare 스크립트는 최초 백업 전용이므로 다시 실행하지 않는다. ASF의 기존 Run 8장 규격과 요청 10장 차이는 이전과 동일하다. Assets·다른 NEWIMAG 폴더는 수정하지 않았고 git 명령은 사용하지 않았다.
'''
for folder in [R,M]:
 p=folder/'README.md';s=p.read_text(encoding='utf-8');s=s.split('\n\n## 머리 묶음 고정 (2026-11-14)')[0]
 if folder==R:s= '> 최신 결과: 아래 「머리 묶음 고정」 및 validation.json.head_bundle_lock.\n'+s+section
 else:s+='\n\n## 머리 묶음 고정 (2026-11-14)\n\nRun Key0의 확장 머리 덩어리를 Turn F0(-20,-4), RunStart F2, RunStop F0, Run_L_10 및 Turn F2에 반영했다. Turn F0 꼬리 이음새만 내장 image_gen 1/3회로 수정했다. 몸·가방·체인은 직전 그대로이며 합성 마스크 밖 불일치 0. 연결 GIF·Sheet_3x.png를 재생성했다. 직전 출력·Masks·Sources는 Sources/prev2에 보존했다. 최신 validation.head_bundle_lock과 attempts.head_bundle_lock을 따른다. 상세 프롬프트·재현·검수 표는 ../Run/README.md 「머리 묶음 고정」 참조.\n'
 p.write_text(s,encoding='utf-8')
 v=json.loads((folder/'validation.json').read_text(encoding='utf-8'));v['head_bundle_lock']['visual_review']={'status':'passed','reviewed':['Run/HeadBack_6x.png','Run/Sources/HeadBack_first5_6x.png','Run/Sources/HeadBack_last5_6x.png','Run/Sources/HeadBundle_review3x.png','Move/Sources/Turn_boundary_3x.png'],'findings':'Key0 tie/root/back-skull rigidly attached; flowing tail seam continuous; no detached fragments or seam holes observed; body and accessories preserved.'};(folder/'validation.json').write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf-8')
print('README and visual review updated')
