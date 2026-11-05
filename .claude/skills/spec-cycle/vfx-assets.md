# spec-cycle — 이펙트 에셋 단계 (GPT 가 그리고 Claude 가 검수)

설계안에 `이펙트 에셋` 표가 있을 때만 읽는다. 디스패처(SKILL.md)의 보안 규칙이 그대로 적용된다.
**게임 이미지는 화풍 통일을 위해 전부 GPT 가 그린다.** pixelfx 는 동작·타이밍 **레퍼런스로만** 쓰고 게임에 넣지 않는다.
역할: Claude(레퍼런스·요청서·검수·가져오기) → GPT(`tools/ai/gpt-image.ps1`, Codex 내장 image_gen 으로 그림) → Claude.
Astra·code-implementer 는 이미지를 만들거나 고치지 않는다.

## 설계 트랙 (design-track ⑥ 에서 design-reviewer 에게 함께 지시)
시각 이펙트가 필요한 기획이면 설계안에 아래 표를 넣는다:

| 이름 | 용도·재생 위치 | 동작 레퍼런스 (pixelfx id) | 셀 px × 프레임 · fps · 루프 | 피벗 | 화풍 참고 | 교체 |
|---|---|---|---|---|---|---|
| `Fx_<Name>` | 이벤트·위치 | `..\pixelfx\docs\CATALOG.md` 의 `<category>/<name>` | 예: 64 × 6 · 20 · 1회 | 중앙 / 하단 | 비슷한 기존 `Px32/` 스프라이트 경로 | 새 파일 / 기존 `Fx_X` 교체 |

- 크기 기준: 3칸 기준(`docs/scale-3tile.md`) — 주인공 키 96px, 32px = 1칸. 이펙트 셀은 보통 32~96, 128 초과 금지.
- 레퍼런스 id 는 CATALOG 에 실제로 있는 것만(`node tools/list.mjs <단어>` 로 찾기, 렌더해 눈으로 보고 고른다).
- 매핑표(⑤)에 "보이는가" 항목: 화면 px 크기·Map 레이어·order. 수용 기준에 수동 항목 "인스펙터 연결 + 플레이로 크기·타이밍 확인".
- 기존 GPT 이펙트 교체는 기획이 요구할 때만. 교체는 같은 경로 PNG 덮어쓰기(.meta·GUID 유지 → 기존 연결 유지).
- 새 이름이고 피벗이 하단이거나 셀·프레임을 고정해야 하면 `Px32Rules.EffectSpec` 에 한 줄 추가를 설계에 포함(Astra 가 코드로).

## 구현 트랙 ⑪-0 — Astra 구현 **전에**, 표의 행마다
작업 폴더 `<wt>\NEWIMAG\<Name>V<rev>\` (예: `FxHitV4`). 도구 호출은 포그라운드, timeout 600000ms.

1. **레퍼런스**:
   ```
   powershell -ExecutionPolicy Bypass -File tools/vfx/pixelfx-reference.ps1 -Effect <id> -Out <폴더> -Fps <fps> -Palette "<표의 램프, 예: #19181F,#641F36,#92223F,#B83450,#DC526B,#F7EEE4>"
   ```
   고정 커밋 아님 오류 → `보류`, 메모 "pixelfx 고정 커밋 아님 — `tools\vfx\pixelfx-setup.ps1` 수동 1회" (자동화는 setup 을 돌리지 않는다).
   `motion_reference.png` 를 Read 로 보고, 레퍼런스의 몇 번 프레임을 결과의 몇 번 프레임으로 쓸지 정한다(프레임 계획).
2. **재료**: 표의 화풍 참고 스프라이트를 `style_reference.png` 로, `NEWIMAG\palette_v2.txt` 를 폴더에 복사(Copy 도구 대신 Read→Write 불가한 PNG 는 `cp`).
3. **요청서** `REQUEST.md` 를 Write 로 — 아래 「요청서 틀」을 채운다.
4. **GPT 그리기**:
   ```
   powershell -ExecutionPolicy Bypass -File tools/ai/gpt-image.ps1 -Dir <폴더> -Expect Fx_<Name>.png -Images "motion_reference.png,style_reference.png"
   ```
   | RESULT | 처리 |
   |---|---|
   | `DONE` | 5 |
   | `NO_OUTPUT`·`FAILED` | 1회 재실행. 또 실패면 `보류`, 메모 `GPT 이미지 실패: <_gpt\last.md 요약>` |
   | `INFRA` | `보류`, 메모 `GPT 인프라: <메시지>` (codex 로그인 만료 등 — 사람이 `codex login`) |
5. **검수** (Claude 가 직접):
   ```
   node tools/vfx/check-sheet.mjs <폴더>/Fx_<Name>.png NEWIMAG/palette_v2.txt --frames <N> --preview <폴더>/review.png --hero Assets/_Project/ResourcceEX/Sprites/Px32/Player/Hero_Idle.png [--old <교체 대상 기존 PNG>]
   ```
   - `RESULT=FAIL` → 규격 위반 → 그대로 지적.
   - PASS 여도 `review.png`(주인공 옆 실제 크기)와 `Preview_4x.png` 를 **Read 로 보고** 판정: 프레임 계획대로인가 · 첫 프레임 타격감 · 프레임 간 흔들림·조각 · 화풍(외곽선·명암 단계)이 참고와 맞는가 · 주인공 대비 크기.
   - 고칠 게 있으면 `REVIEW-k.md`(무엇이 · 어느 프레임 · 어떻게, 유지할 것 명시)를 쓰고
     `gpt-image.ps1 … -Images "Fx_<Name>.png 의 이전본(round<k>_Fx_<Name>.png 로 복사),motion_reference.png,style_reference.png" -ReviewFile <폴더>/REVIEW-k.md`.
   - **재요청은 최대 2회**(총 3장). 3장째도 미달이면 가장 나은 회차를 남기고 `보류`, 메모에 남은 지적 + `review.png` 경로 — 사람이 판단.
6. **가져오기·커밋**: 통과본을 `<wt>\Assets\_Project\ResourcceEX\Sprites\Px32\Effects\Fx_<Name>.png` 로 복사(교체면 덮어쓰기, `.meta` 는 만들지 않음 — ⑬-1 `-HarvestMetaTo` 가 고정) →
   `spec-worktree.ps1 -Commit`, 메시지 `[SPEC-n] 이펙트 에셋 (GPT)` / 빈 줄 / 행마다 `Fx_<Name> ← 레퍼런스 <id> (셀 px × 프레임 · fps · 검수 k회)` / 빈 줄 / Co-Authored-By. `NEWIMAG\<폴더>`(요청서·레퍼런스·회차별 결과·README)도 함께 커밋.
7. 명세 파일(impl-spec.md)에 추가: "이펙트 시트는 이미 `Sprites/Px32/Effects/Fx_<Name>.png` 에 있다(셀·프레임·fps·피벗). 이미지·.meta 를 만들거나 고치지 말 것. 스프라이트는 `[SerializeField] Sprite[]`(기존 `PlayerEffects` 방식) 등 인스펙터 필드로 받고, 에셋 연결은 하지 않는다."

### 요청서 틀 (REQUEST.md)
```
# 요청: Fx_<Name> (<용도 한 줄>) — 3칸 기준
Re:Trap(2D 픽셀아트 플랫포머) <언제·어디서> 재생되는 이펙트. 이 폴더 안에서만 작업, git 금지.
## 참고 이미지
- motion_reference.png — 동작·타이밍 참고만(pixelfx <id>, <fps>fps 프레임 목록). 화풍은 따르지 말 것. 흐름: <한 줄>.
- style_reference.png — 화풍 참고(<무엇>). 확대해서 쓰지 말고 새로 그릴 것.
- palette_v2.txt — 쓸 수 있는 색 전체.
## 결과물
- Fx_<Name>.png: RGBA, <셀×N>×<셀>, <셀>×<셀> 셀 N개 가로 한 줄(간격 0), 1배. 재생 <fps>fps, <1회/루프>. 피벗 <중앙 (c,c) / 하단 중앙>.
- 프레임 계획: 0. … / 1. … (레퍼런스 프레임 번호와 대응, 크기는 px 로)
- 규칙(엔진 검사기가 그대로 검사): palette_v2 색만·프레임당 16색 이하·순흑 금지 / 알파 0·255 만(안티앨리어싱·그라데이션·디더 금지) /
  각 셀 상·좌·우 1px 비움 / 빈 프레임 금지 / 파편 최소 2픽셀.
- Preview_4x.png(정수 4배), README.md(크기·fps·피벗·사용 색·제작 과정·image_gen 호출 횟수·프롬프트 전문·자체 검사).
## 제작 방식
내장 image_gen 으로 새로 그림(확대 생성 후 논리 격자 복원 가능). 후처리는 격자 복원·최근접 팔레트 매핑·알파 임계 128·평행이동만. 코드로 도형을 그리지 말 것.
```

## 검증·리포트
- ⑬-1 CI `sprites32` 가 최종 판정(check-sheet 는 사전 검사). FAIL 이면 이미지 문제 → Astra 에 넘기지 말고 5의 재요청(재요청 한도와 ⑬ 시도 한도 공유).
- ⑭ 리포트에 "이펙트 에셋" 소절: 행마다 `Fx_<Name>` ← 레퍼런스 id · 셀·프레임·fps · 검수 회차와 회차별 지적 요약 · GPT 토큰·시간(`gpt-image` 출력 `TOKENS`·`DURATION_SEC`) · `review.png` 경로.
  수동 확인 체크리스트: "인스펙터 연결 → 플레이로 크기·타이밍 확인".
- 버그 트랙에서 이펙트가 필요해도 같은 절차(`<wt>` = `..\ReturnMap-spec\BUG-n`).

---

# 캐릭터 시트 — GPT 원본 + ASF 후처리 (설계안에 `캐릭터 시트` 표가 있을 때만)

주인공·적의 **동작 시트**(가로 한 줄, `Px32Rules` 규격 셀·프레임)를 새로 그리거나 교체할 때. 이펙트·함정·타일은 해당 없음(위 절차).
그림은 GPT, 정리(배경 제거·크기 통일·몸통 정렬·팔레트 잠금)와 규격 검사는 Agent Sprite Forge(ASF) — `tools/sprite/README.md`.
ASF 가 그린 그림·`route_media.py` 사용 금지. **이미 승인된 시트는 ASF 에 다시 넣지 않는다**(몸통 기준으로 최대 2px 옮김).

## 설계 트랙 (design-track ⑥)

| 이름 | 동작 | 셀 px × 프레임 · fps · 루프 (`Px32Rules`) | 원본 칸(행×열) | 몸 px | 화풍·정체성 참고 | 교체 |
|---|---|---|---|---|---|---|
| `Hero_<Anim>` / `<Enemy>_<Anim>` | 포즈 흐름 한 줄 | 예: 128 × 8 · 18 · 루프 | 예: 2×4 | 주인공 102 (적은 기존 시트 높이) | 승인 Idle 등 기존 `Px32/` 경로 | 새 파일 / 기존 교체 |

- 새 동작 이름이면 `Px32Rules.CharacterSpec` 한 줄 추가를 설계에 포함(Astra 가 코드로). 몸 크기는 줄이지 않는다(메모리 「주인공 크기 고정」).

## 구현 트랙 ⑪-0 — Astra 구현 **전에**, 표의 행마다
작업 폴더 `<wt>\NEWIMAG\<Name>V<rev>\`. 도구는 **메인 저장소 경로**로 부른다(ASF 체크아웃 `..\agent-sprite-forge` 가 메인 기준) — 경로는 전부 절대 경로. 포그라운드, timeout 600000ms.

1. **재료**: 화풍·정체성 참고 PNG 를 `identity_reference.png` 로, `NEWIMAG\palette_v2.txt` 를 폴더에 복사(`cp`).
2. **요청서** `REQUEST.md` — 아래 「캐릭터 요청서 틀」.
3. **GPT 그리기**: `powershell -ExecutionPolicy Bypass -File C:\Users\Owner\work\ReturnMap\tools\ai\gpt-image.ps1 -Dir <폴더> -Expect <Name>_raw.png -Images "identity_reference.png"` — RESULT 처리는 위 4번 표와 같다.
4. **ASF 후처리 + 규격 검사**:
   ```
   powershell -ExecutionPolicy Bypass -File C:\Users\Owner\work\ReturnMap\tools\sprite\asf-post.ps1 -Raw <폴더>\<Name>_raw.png -Name <Name> -Rows <R> -Cols <C> [-Count <규격 프레임>] -BodyPx <몸 px> -Out <폴더>
   ```
   | RESULT | 처리 |
   |---|---|
   | `PASS` | 5 |
   | `FAIL` | 출력의 `FAIL …` 줄·`크기·정렬 실패` 를 그대로 `REVIEW-k.md` 에 옮겨 3을 `-ReviewFile` 로 재요청(이전 원본은 `round<k>_<Name>_raw.png` 로 복사해 `-Images` 에 추가). `drift: 높이` = 프레임마다 몸 길이가 다름, `leave the canvas` = 칸 여백 부족 |
   | `INFRA` | `보류`, 메모 "ASF 미설치·고정 커밋 아님 — `tools\sprite\asf-setup.ps1` 수동 1회" (자동화는 setup 을 돌리지 않는다) |
5. **눈 검수**: `<Name>_Preview_4x.png` 를 Read 로 보고 — 얼굴·의상·소품이 참고와 같은가 · 프레임마다 소품 크기·모양 유지 · 동작 흐름 · 조각·잘림. 미달이면 4의 FAIL 과 같은 재요청. **재요청은 최대 2회**(총 3장), 3장째도 미달이면 가장 나은 회차를 남기고 `보류`(메모에 지적 + 미리보기 경로).
6. **가져오기·커밋**: `<폴더>\<Name>.png` → `<wt>\Assets\_Project\ResourcceEX\Sprites\Px32\<Player|Enemies>\<Name>.png`(교체는 덮어쓰기, `.meta` 만들지 않음) →
   `spec-worktree.ps1 -Commit`, 메시지 `[SPEC-n] 캐릭터 시트 (GPT + ASF)` / 빈 줄 / 행마다 `<Name> ← 셀 × 프레임 · fps · 검수 k회` / 빈 줄 / Co-Authored-By. `<폴더>` 의 요청서·원본·`<Name>.asf.json`(출처·설정·수치)도 함께 커밋(`ASF\` 중간 산출물은 커밋하지 않음).
7. 명세 파일에: "캐릭터 시트는 이미 `Sprites/Px32/<…>/<Name>.png` 에 있다. 이미지·.meta 를 만들거나 고치지 말 것. 클립·애니메이터 배선은 기존 `Px32HeroWiring`/`Px32EnemyWiring`·`SpriteClipRebinder` 흐름을 따른다."

### 캐릭터 요청서 틀 (REQUEST.md)
```
# 요청: <Name>_raw (<동작 한 줄>) — 원본 시트, 후처리는 Claude 쪽 도구가 함
Re:Trap(2D 픽셀아트 플랫포머) <캐릭터>의 <동작>. 이 폴더 안에서만 작업, git 금지.
## 참고 이미지
- identity_reference.png — 이 캐릭터의 확정 모습. 얼굴·머리·의상·소품(모양·크기)을 모든 프레임에서 그대로.
- palette_v2.txt — 쓸 수 있는 색(앞 20색이 캐릭터용).
## 결과물
- <Name>_raw.png: <R>행 × <C>열 격자, 칸마다 포즈 하나(읽는 순서 = 재생 순서), 오른쪽을 봄.
  배경은 단색 #00FF00(그림자·바닥·글자·테두리 없음). 칸마다 사방 15% 이상 여백 — 머리카락·소품 끝도 칸을 넘지 말 것.
  모든 프레임에서 몸 크기 같게(정수리~발끝 높이 일정), 지면 동작은 발이 같은 높이.
- 포즈 계획: 0. … / 1. … (프레임마다 한 줄)
- 픽셀아트 화풍(외곽선 #19181F, 순흑 금지, 안티앨리어싱 최소). 정확한 픽셀 격자·최종 크기는 맞출 필요 없다(후처리가 맞춤).
- README.md(image_gen 호출 횟수·프롬프트 전문).
## 제작 방식
내장 image_gen 으로 새로 그림. 코드로 도형을 그리지 말 것. 후처리(배경 제거·축소·정렬)는 하지 말고 원본 그대로 둘 것.
```

## 검증·리포트 (캐릭터 시트)
- ⑬-1 CI `sprites32` 가 최종 판정(`asf-post` 의 검사는 같은 규칙의 사전 검사). FAIL 이면 이미지 문제 → 4·5의 재요청(재요청 한도와 ⑬ 시도 한도 공유).
- ⑭ 리포트 「캐릭터 시트」 소절: 행마다 `<Name>` · 셀·프레임·fps · 검수 회차와 회차별 FAIL·지적 요약 · `<Name>.asf.json` 의 ASF 커밋 · GPT 토큰·시간 · 미리보기 경로.
  수동 확인 체크리스트: "플레이로 크기·발 위치·동작 흐름 확인".
