# Agent Sprite Forge(ASF) 통합 설계안 — 2026-11-06 (설계만, 미착수)

ASF(MIT, https://github.com/0x0funky/agent-sprite-forge, v0.4.0 `5324342f`)는
"그림은 이미지 모델이, 정리는 결정적 Python 도구가" 하는 2D 스프라이트 파이프라인이다.
우리 규칙(**게임 이미지는 전부 GPT가 그린다**)과 맞추면 ASF의 자리는 pixelfx 와 같다 —
**ASF가 그린 그림은 게임에 넣지 않고, GPT 시트를 정리·검사하는 후처리기로만 쓴다.**

## 설치 상태 (완료)

| 항목 | 위치 |
|---|---|
| Claude Code 플러그인 `agent-sprite-forge@agent-sprite-forge` (user scope) | 스킬 5개: generate2dsprite · video2dsprite · generate2dmap · generate2dmedia · codeart2d |
| 고정 체크아웃 (pixelfx 와 같은 자리) | `..\agent-sprite-forge` (v0.4.0, `5324342fdfab72430f4e9604f3fe427009f8596d`) |
| Python 의존성 | Python 3.12 + numpy 2.5.4 · Pillow 13.0.0 · scipy 1.18.1 |
| 없음 | ffmpeg (영상 경로 전용), API 키, Grok CLI |

## 왜 필요한가 — 지금 아픈 곳과 ASF 도구 대응

| 지금 문제 (메모리·V2~V7 기록) | ASF 도구 |
|---|---|
| 주인공 시트 프레임마다 위치가 흘러다님(드리프트), 왕관 기준 피벗 수동 정렬 | `generate2dsprite.py process --scale-strategy registered` + `scale_frames.py --root-lock torso-x --row-baseline` |
| 동작마다 몸 크기가 달라짐 | `process --write-scale-profile` (Idle 기준) → 다른 동작 `--scale-profile --max-profile-scale-drift 0.08` |
| 조각(떨어진 픽셀)·대시 잘림 | `sheet_qc.py spill`(칸 넘침을 슬라이스 전에 잡음) + matte QA |
| GPT가 프레임마다 모습을 바꿈 (가방 문제 자체는 **다른 세션이 V6/V7 로 정리 중 — 이 계획 범위 밖**) | `sheet_qc.py frames` identity_residual · torso_drift 수치로 재요청 근거만 제공 |
| 색 수가 늘고 알파가 반투명 | `palette_tool.py apply/lock` (palette_v2 공유 팔레트, 이진 알파, 프레임 간 색 떨림 억제) |
| 손으로 짠 `NEWIMAG/Px32HeroV7/prepare.py` 류 일회성 스크립트 | 고정 도구 + 고정 플래그로 대체 |

## 흐름 (pixelfx 흐름과 짝)

```
                 ┌──────────── GPT(Codex image_gen) — 유일한 그림 출처 ────────────┐
① 마스터         gpt-image.ps1 → still.png ──► master_still.py approve --still ... --route codex-gpt
                                                └► master.json (정체성·방향·프레이밍 고정)
② 동작 시트      plan_guide.py(칸·발 라인 guide.png) + master_rgba.png ──► gpt-image.ps1 ──► raw sheet
③ ASF 후처리     sheet_qc spill → process(키·슬라이스·정렬, scale-profile) → sheet_qc frames
                 → scale_frames(몸통 X 고정·발 기준선, 96px 몸 / 128 셀) → palette_tool(palette_v2 잠금)
④ 우리 규격      asf-to-px32 어댑터 → Hero_<State>.png 아틀라스 + manifest  (NEWIMAG/<묶음>/)
⑤ 검수           check-sheet.mjs(기존 CI 규칙) + 눈 비교 → 재요청 최대 2회 (기존 규칙 그대로)
⑥ 반영           Sprites/Px32/... 덮어쓰기 → Px32HeroWiring / SpriteClipRebinder (변경 없음)
```

- ASF 의 `route_media.py`(API 키 → Codex CLI → codeart 자동 선택)는 **쓰지 않는다.** 생성은 계속 `tools\ai\gpt-image.ps1` 하나로 —
  키가 생기면 ASF가 몰래 유료 API로 바꿔 타는 일, codeart(코드 그림)로 떨어지는 일을 막기 위해.
- ASF `export_engine.py`는 Unity 임포트 미검증(공식 known-limitations) → **안 쓴다.** Unity 쪽은 기존 Px32 배선이 담당.

## 저장소에 생길 것 (pixelfx 대응표)

| pixelfx (있음) | ASF (새로) | 역할 |
|---|---|---|
| `tools/vfx/pixelfx.lock.json` | `tools/sprite/asf.lock.json` | repo·version·commit·checkout `..\agent-sprite-forge` |
| `tools/vfx/pixelfx-setup.ps1` | `tools/sprite/asf-setup.ps1` | 고정 커밋 클론·체크아웃 + `py -3.12 -m pip install -r requirements.txt` + `forge_doctor` |
| `tools/vfx/pixelfx-reference.ps1` | `tools/sprite/asf-post.ps1` | ③ 후처리 한 줄 래퍼 (단계별 실패 시 멈춤, `RESULT=PASS\|WARN\|FAIL\|INFRA`) |
| `tools/vfx/pixelfx-convert.mjs` | `tools/sprite/asf-to-px32.py` | ④ ASF 프레임 → 우리 셀·아틀라스·manifest (우리 코드, ASF import 안 함) |
| `tools/vfx/README.md` | `tools/sprite/README.md` | 순서·금지·버전 올리기 |
| `PIXELFX/` (gitignore) | `ASF/` (gitignore) | ASF 중간 산출물(output-dir 이 매번 새 폴더라 쌓임) |
| `.claude/skills/spec-cycle/vfx-assets.md` | 같은 파일에 "캐릭터·적 시트" 절 추가 | spec-cycle 자동화도 같은 흐름 |
| — | `THIRD_PARTY_NOTICES.md` 에 ASF MIT 줄 | 크레딧 |
| — | `CLAUDE.md` 프로젝트 구조에 한 줄 | 규칙 노출 |

C#·Unity 쪽 변경은 **없음**(어댑터가 지금 규격을 그대로 맞춰 줌).

## 금지 (README 에 박을 것)

- ASF 가 그린 그림(codeart2d, video2dsprite 결과, route_media 생성물)을 게임에 넣지 않는다 — 비교 테스트만.
- `Assets/` 에 Python 을 넣지 않는다. ASF 스크립트는 `..\agent-sprite-forge` 에서 `py -3.12 -I` 로 실행.
- 숫자 QC 통과 ≠ 승인. ASF 스스로 "anatomy·identity·motion 은 증명 안 함"이라고 적어 둠 → 눈 검수 유지.
- 버전 올리기: lock 의 version·commit 변경 → setup 재실행 → 기존 승인 시트로 회귀 비교 → NOTICES 확인.

## 진행 (2026-11-06)

- 1단계 완료: `tools/sprite/`(lock·setup·README), `.gitignore /ASF/`, NOTICES, CLAUDE.md.
- 2단계 완료: V2 GPT 원본 Run — `process` 기본은 몸통 28px 흔들림(탈락), `scale_frames --root-lock torso-x` 는 5px·20색(손 정리본 동급).
- 3단계 완료: `asf-post.ps1`(한 줄 실행) + `asf-to-px32.py`(배치·시트·CI 규칙 미리 검사).
  CI 기준(발 행 일치·가로 bbox 중심 ±2)에 맞추려 정렬은 ASF(`--lock feet`) + 어댑터 정수 이동(발=127행, bbox 중심=64)으로 마무리.
  결과: V2 Dash PASS · V2 Run 높이 드리프트 2프레임 FAIL(그림 문제 → GPT 재요청 근거). 승인 Idle·Run 은 이동 0 · PASS.
- 5단계 완료: spec-cycle `vfx-assets.md` 「캐릭터 시트」 절(설계안 `캐릭터 시트` 표 → GPT 원본 → `asf-post.ps1` → 눈 검수 → 재요청 최대 2회), SKILL·design-track·impl-track 진입 조건에 추가.
- 남음: 4단계(가방 세션 끝난 뒤 새 시트에 실제 적용 — 첫 실제 CI `sprites32` 확인도 여기서).

## 단계 (승인 후)

1. 뼈대: lock · setup · README · gitignore · NOTICES · CLAUDE.md 줄.
2. **시험**: 지금 승인된 `Hero_Idle`·`Hero_Run` **복사본**을 `ASF/` 에서 후처리해 기존 결과와 나란히 비교(드리프트·색 수·알파 수치 + 눈).
   `Sprites/Px32/Player`·`NEWIMAG/Px32HeroV6·V7` 은 읽기만 한다(가방 세션이 작업 중). 나아지지 않으면 통합 중단.
3. 어댑터 `asf-to-px32.py` + 래퍼 `asf-post.ps1`.
4. 실제 적용은 **가방(V6/V7) 세션이 끝난 뒤** 다음 새 시트(적 캐릭터 등)부터. 주인공 시트에 넣을지는 그때 그 결과를 보고 판단.
5. spec-cycle `vfx-assets.md` 에 절 추가.

> 겹침 주의: 주인공 가방 정리는 다른 세션 담당. 이 계획은 그 세션의 파일(`NEWIMAG/Px32HeroV6·V7`, `Hero_*.png`, `PlayerAnimationFSM`, `docs/animation-plan-v6.md`)을 수정하지 않는다.

## 가정 (확인 필요)

- `palette_tool.py` 가 `NEWIMAG/palette_v2.txt` 형식을 그대로 읽는지 미확인 — 안 되면 어댑터에서 변환.
- ASF 픽셀 마무리 기본 높이(80px) 대신 몸 96px / 셀 128 로 지정 가능하다고 봄(`scale_frames` 목표 높이 플래그).
- ASF 의 실사 검증은 시트 1장(여우)뿐 — 우리 GPT 픽셀 시트에서 임계값이 맞는지는 ②단계 시험에서 확인.

## 결정이 필요한 것

1. **video2dsprite(영상→스프라이트)** — 마스터 1장에서 Grok/Kling 영상으로 동작을 뽑으면 동작 일관성은 가장 좋지만,
   프레임을 그리는 건 GPT 가 아닌 영상 모델(규칙 위반) + xAI/fal 키(유료) + ffmpeg 필요. **권장: 보류**(비교 실험만 허용).
2. **적용 범위** — 권장: 주인공·적 캐릭터 시트부터. 이펙트 시트는 지금 pixelfx 흐름 뒤에 ③ 후처리만 선택적으로.
3. **generate2dmap** — 우리 맵은 JSON 타일맵이라 당장 쓸 곳 없음. 배경 판(parallax) 만들 때 다시 판단.
