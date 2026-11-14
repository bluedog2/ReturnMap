# Re:Trap V9 — 이동 전환

2026-11-14 수정. 직전 출력·문서·검증·Masks는 `Sources/prev1/` 보존. 이전 생성 과정과 체형 검증 제한은 그 README를 참고한다. validation.json의 최신 결과는 `screen_side_bag`이며 나머지 기존 항목은 이력이다.

## 턴 가방 고정

**왼쪽 보기는 flipX 대신 _L 시트가 필요 — 5단계에서 전 시트 _L 생성·FSM 방향별 클립**. 이번 작업은 Turn_F2_L과 Run_L 시험본까지다. FSM·Assets는 수정하지 않았다.

| 턴 | 시선/원본 | 가방 좌상단 | 이전 장 대비 |
|---|---|---|---|
| F0 | 브레이크 몸 + Run Key0 머리 | (50,94) | Run9 (52,94)에서 -2px |
| F1 | 기존 정면 몸·얼굴 | (48,94) | -2px |
| F2 | 가방 없는 Run0 몸을 미러한 왼쪽 완성본 | (50,94) | +2px |

세 장 모두 화면 왼쪽 허벅지 앞에 같은 승인 3/4 가방·6행 체인을 얹었다. F1도 중앙에서 화면 왼쪽으로 옮겼다. 가방·체인을 한 덩어리로 정수 이동하고 다리보다 앞에 합성한다. 턴 내부 이동은 유클리드 최대2px. F2→Run_L F0 전체 불일치0.

`Turn_F2.png`와 `Turn_F2_L.png`는 동일한 왼쪽 완성본. `Hero_Turn_3.png`는 오른쪽 F0 / 정면 F1 / 왼쪽 F2 순서이며 다시 flipX 하지 않는다. `Turn_3x.gif`는 오른쪽 Run10 → 턴3 → Run_L10, 비교용20fps. `Hero_Turn_3_3x.gif`는 턴3장만20fps다.

### 왼쪽 Run 시험본

각 오른쪽 Run에서 가방이 가린 픽셀만 기존 Sources의 가방 없는 몸으로 복원했다. 사이 장은 기존 blocks와 generation 마스크 합성을 재사용했다. 몸을 픽셀 그대로 미러하고 가방·체인을 화면 왼쪽에 합성했다. 가방 밖은 이 미러 몸과 불일치0.

10장 시간축의 바로 이전 장 허리 기준점을 사용한다. 오른쪽 x를 127-x로 미러한 뒤 가방 오프셋 (-14,-2)을 적용했다. I5 허리는 인접 장의 정수 중간값 (62,93), I7은 (63,96)이다. 가방은 몸보다 정확히1장 늦게 따라간다. 오른쪽 승인 Run 가방 위치는 그대로다. 장별 좌표와 근거는 validation.json.screen_side_bag.left_frames에 있다.

가방은 완전 좌우 대칭이 아니다. 승인18×16 이미지와 미러의 RGBA 불일치는100px이며 측면·명암·리벳이 비대칭이다. 따라서 가방 자체는 미러하지 않고 동일한 그림을 사용했다. 크기·빨간 띠·맞물린 흰 이빨·체인을 보존했다.

### 얼굴 연결·출력·검증

RunStart F2=수정 Run0, RunStop F0=수정 Run9. Turn F0도 Key0 얼굴·앞머리·머리통을 목 위치에 맞춰 (-20,-4) 이동했다. 정면 F1 얼굴은 유지한다. Turn F2=Run_L F0이며 얼굴도 함께 미러된다. 목·뿌리 이음새와 가방 위치를 확대 확인했다.

출력: 각 RunStart/RunStop/Turn 장, Hero_RunStart_3/RunStop_4/Turn_3 시트·GIF, Turn_F2_L.png, Run_L_10.png, Run_L_F0~9.png, Run_L_10_3x.gif, Start/Stop/Turn 연결GIF, Sheet_3x·Waist_8x·Sources/Turn_boundary_3x.png.

검증: 20색, 승인 Idle 팔레트 밖0, 알파0/255·셀128·바닥127, 연결 장·GIF 재디코딩 불일치0. ASF ④만 수행. RunStart·Run_L 실패0. 기존 가로 중심 편차3건(멈춤F1, 턴F1/F2)은 남아 있으며 통과로 처리하지 않았다. 정확한 수치는 Sources/ScreenSide_ASF_check.json에 있다.

신규 image_gen 0회. 기존 생성 결과 재사용, 새 선·색의 코드 드로잉 없음. 리샘플·회전·축소 없음. 확대는 픽셀 반복. 재현: `python NEWIMAG/Px32HeroV9/Run/Sources/face_lock.py`. 옛 finish.py/report.py는 수정 전 상태로 되돌리므로 이번 재현용으로 실행하지 않는다.


## 머리 묶음 고정 (2026-11-14)

Run Key0의 확장 머리 덩어리를 Turn F0(-20,-4), RunStart F2, RunStop F0, Run_L_10 및 Turn F2에 반영했다. Turn F0 꼬리 이음새만 내장 image_gen 1/3회로 수정했다. 몸·가방·체인은 직전 그대로이며 합성 마스크 밖 불일치 0. 연결 GIF·Sheet_3x.png를 재생성했다. 직전 출력·Masks·Sources는 Sources/prev2에 보존했다. 최신 validation.head_bundle_lock과 attempts.head_bundle_lock을 따른다. 상세 프롬프트·재현·검수 표는 ../Run/README.md 「머리 묶음 고정」 참조.

ASF ? ??: Run?Run_L?RunStart ?? 0. Move RunStop F1 ?? ??, Turn F1/F2 ?? ??? ???? ?? ???. Turn F0? ?? (-20,-4) ????? ???????? ????? ?? 101?105px? ?? ?? ?? ?2px ?? ??? ????. ?? ??? ? ?? ?? ??? ?? ???? ? ???? ????. ??????? ?? ??? ??.
