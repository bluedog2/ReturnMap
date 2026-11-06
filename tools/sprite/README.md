# tools/sprite — GPT 캐릭터 시트 후처리: Agent Sprite Forge(ASF)

**게임 이미지는 화풍 통일을 위해 전부 GPT 가 그린다.** ASF(MIT, https://github.com/0x0funky/agent-sprite-forge)는
GPT 가 그린 원본 시트를 **정리(배경 제거·크기 통일·정렬·팔레트 잠금)하고 수치로 검사하는 후처리기**로만 쓴다.
ASF 가 생성한 그림(route_media · codeart2d · video2dsprite 결과)은 게임에 넣지 않는다. 설계: `docs/sprite-forge-plan.md`.

```
asf.lock.json     repo · version · commit · checkout(..\agent-sprite-forge) · python
asf-setup.ps1     고정 커밋 클론·체크아웃 + pip 의존성 + NEWIMAG\palette_v2.txt → ASF\palette_v2.hex · palette_hero20.hex
asf-post.ps1      GPT 원본 → ① 칸 넘침 → ② 크기·정렬 → ③ 팔레트 잠금 → ④ 규격 시트 + CI 규칙 검사. 마지막 줄 RESULT=PASS|FAIL|INFRA
asf-to-px32.py    ④ 단계: ASF 프레임 → 발=맨 아래 행·가로 bbox 중심=셀 중앙 정수 이동 → <이름>.png(가로 한 줄)
                  + _Preview_4x.png + .asf.json(출처·설정·수치) + CI sprites32 캐릭터 규칙 미리 검사
```

중간 산출물은 `ASF\<이름>-<시각>\`(gitignore, 단계별 .log/.err). 시트는 `-Out` 폴더에만 쓴다(Assets 는 안 건드림).

## 순서

```powershell
powershell -ExecutionPolicy Bypass -File tools\sprite\asf-setup.ps1          # 처음 1회·버전 올린 뒤
# GPT 원본(초록 배경 4×2 예) → 후처리
powershell -ExecutionPolicy Bypass -File tools\sprite\asf-post.ps1 -Raw NEWIMAG\<묶음>\Sources\Hero_Run_raw.png -Name Hero_Run -Rows 2 -Cols 4 -Out NEWIMAG\<묶음>
#   -Count N(앞 N칸만) · -Key green|magenta|blue|none · -BodyPx 102 · -Place ground-center|ground|asf
#   -Downscale box|pixeloe(기본 box) · -OeThickness 1 · -OePixel 4
```

### 축소 방식 `-Downscale` (선택, 2026-11-07)

- **box**(기본): ASF `scale_frames` 가 원본을 box 로 줄인다.
- **pixeloe**: box 로 한 번 돌려 배율만 잰 뒤 → `tools\pixeloe\oe-sheet.py` 가 같은 크기로 PixelOE 픽셀화(`oe_sheet.png`) → ASF 는 배율 1·nearest 로 같은 정렬만 → ③④ 는 같음.
  준비 `tools\pixeloe\pixeloe-setup.ps1`(venv 없으면 INFRA). 크로마키 배경 원본 전용(`-Key none` 은 FAIL로 막음). `.asf.json` settings 에 `downscale`·`pixeloe` 기록.
- V2 비교(Run·Dash): pixeloe 외곽선 1 은 외톨이 픽셀 약 −65%·실루엣 또렷, 대신 얼굴이 어둡고 1~2px 가는 선(대시 속도선)이 사라지고 몸이 ~1.5px 커짐. 외곽선 2 는 더 거칢.
  **기본값은 다음 새 GPT 시트(V9 이후)를 두 방식으로 뽑아 눈으로 비교한 뒤 정한다.**

- **PASS** → `<이름>_Preview_4x.png` 를 눈으로 본다(얼굴·의상·소품·동작) → 통과면 `Sprites/Px32/...` 로 덮어쓰기(.meta 유지) → `tools\ci\unity-ci.ps1`.
- **FAIL** → 출력의 `FAIL ...` 줄을 그대로 GPT 재요청 REVIEW 에 옮긴다(`tools\ai\gpt-image.ps1 -ReviewFile`). 처음 + 재요청 최대 2회.
  - `drift: 높이` = GPT 가 프레임마다 몸 길이를 다르게 그림 → ASF 로 못 고침, 재요청.
  - `크기·정렬 실패(leave the canvas)` = 셀 밖 → 칸 여백 더 달라고 재요청. ASF 는 자르지 않는다.
  - `dimensions: 규격 … 프레임인데` = `-Rows×-Cols`(`-Count`)가 `Px32Rules` 규격과 다름 → 인자를 잘못 줬으면 고쳐 다시, GPT 가 칸 수를 다르게 그렸으면 재요청.
    셀도 대조한다(주인공 128 · 고블린·임프 96 · 스켈레톤·엘리트 128 · 오크 160 → `-Cell`). 규격표에 없는 이름은 `WARN` 만 내고 진행.
- **INFRA** → 설치 문제이거나 Windows 파일 잠금(`WinError 5`, 백신·색인이 ASF 임시 폴더를 잡음 — 3번 시도 후에도 잠기면). 그림 문제가 아니니 재요청하지 말고 잠시 후 다시 실행.
- 프레임 수는 `Px32Rules` 규격과 같아야 한다(예: Hero_Dash 5). `-Rows×-Cols` 또는 `-Count` 가 곧 규격 프레임 수 — 어댑터가 이름으로 규격을 대조한다.

## 시험에서 확인한 것 (2026-11-06, V2 GPT 원본)

| | 몸통 흔들림 | 색 | 결과 |
|---|---|---|---|
| `generate2dsprite.py process` 기본(feet 앵커) | 28px | 46(60색 팔레트) | 쓰지 않음 |
| `scale_frames --root-lock torso-x --lock feet` + 20색 + 배치 | 5px | 20 | Dash 정렬·색 통과(V2 원본은 4장이라 규격 5장 대조로 FAIL) · Run 은 높이 드리프트 2프레임 FAIL(그림 자체 문제) |

- 승인된 `Hero_Idle`·`Hero_Run` 을 ④ 만 통과시키면 이동량 0 · PASS(배치가 기존 기준과 같음).
- **이미 승인된 시트에 ②(scale_frames)를 다시 돌리지 않는다** — 몸통 기준으로 최대 2px 옮긴다. 새 GPT 원본에만.

## 금지

- ASF 생성 그림을 게임에 넣지 않는다. `route_media.py`(API 키가 있으면 유료 API 자동 사용)를 쓰지 않는다 — 생성은 `tools\ai\gpt-image.ps1` 하나.
- `Assets/` 에 Python 을 넣지 않는다. 크레딧은 `THIRD_PARTY_NOTICES.md`.
- 버전 올리기: `asf.lock.json` 의 version·commit 변경 → setup 재실행 → V2 Run·Dash 원본으로 같은 결과인지 회귀 비교 → NOTICES 확인.
- `asf-to-px32.py` 의 규칙·규격표(`HERO_FRAMES`·`ENEMY_FRAMES`·`ENEMY_CELL`)는 `PixelArtSheetValidator.cs`·`Px32Rules.cs` 복사본 — 그쪽이 바뀌면 같이 고친다. .ps1 은 UTF-8 BOM 유지(PowerShell 5.1).
