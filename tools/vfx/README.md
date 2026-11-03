# tools/vfx — 이펙트: pixelfx 레퍼런스 → GPT 그림 → Claude 검수

**게임 이미지는 화풍 통일을 위해 전부 GPT 가 그린다.** pixelfx(MIT, https://github.com/patch-gg/pixelfx)는
이펙트의 **동작·타이밍 레퍼런스**로만 쓴다 — pixelfx 렌더 결과를 게임에 직접 넣지 않는다.
spec-cycle 자동화도 같은 흐름이다(`.claude/skills/spec-cycle/vfx-assets.md`).

```
pixelfx-setup.ps1       ..\pixelfx 를 pixelfx.lock.json 의 고정 커밋으로 클론·체크아웃 (npm install 없음, 수동 1회)
pixelfx-reference.ps1   pixelfx 효과 → <폴더>\motion_reference.png(프레임 번호·시각 목록) + .json(fps·프레임·크기·원점)
../ai/gpt-image.ps1     <폴더>\REQUEST.md + 참고 이미지 → Codex CLI(GPT, 내장 image_gen) → <폴더>\Fx_<Name>.png · Preview_4x · README
                          -ReviewFile REVIEW-k.md 로 검수 지적만 고쳐 다시 그리게 함. 마지막 줄 RESULT=DONE|NO_OUTPUT|FAILED|INFRA
check-sheet.mjs         결과 시트 규격 검사(CI sprites32 와 같은 이펙트 규칙) + --preview: 주인공 | 기존 | 새 시트 같은 배율 비교
png.mjs                 공용 PNG 읽기·쓰기·팔레트
```

## 순서 (사람이 직접 할 때)

```powershell
powershell -ExecutionPolicy Bypass -File tools\vfx\pixelfx-setup.ps1
# 1) 레퍼런스 — 효과는 ..\pixelfx\docs\CATALOG.md 또는 (..\pixelfx 에서) node tools/list.mjs <단어>
powershell -ExecutionPolicy Bypass -File tools\vfx\pixelfx-reference.ps1 -Effect impacts/crit-hit -Out NEWIMAG\FxHitV4 -Fps 20 -Palette "#19181F,#641F36,#92223F,#B83450,#DC526B,#F7EEE4"
# 2) NEWIMAG\FxHitV4 에 style_reference.png · palette_v2.txt 복사, REQUEST.md 작성 (틀: vfx-assets.md 「요청서 틀」)
# 3) GPT 그리기
powershell -ExecutionPolicy Bypass -File tools\ai\gpt-image.ps1 -Dir NEWIMAG\FxHitV4 -Expect Fx_Hit.png -Images "motion_reference.png,style_reference.png"
# 4) 검수
node tools/vfx/check-sheet.mjs NEWIMAG/FxHitV4/Fx_Hit.png NEWIMAG/palette_v2.txt --preview NEWIMAG/FxHitV4/review.png --hero Assets/_Project/ResourcceEX/Sprites/Px32/Player/Hero_Idle.png --old Assets/_Project/ResourcceEX/Sprites/Px32/Effects/Fx_Hit.png
# 5) 통과 → Sprites/Px32/Effects/ 로 복사(교체는 덮어쓰기 — .meta·연결 유지)
```

- 검수는 **처음 + 재요청 최대 2회(총 3장)**. 3장째도 미달이면 가장 나은 회차를 남기고 사람이 판단.
- 크기 기준은 3칸 기준(주인공 키 96px, 32px = 1칸). 이펙트 셀은 보통 32~96px.
- `-Palette` 는 레퍼런스 색을 우리 램프로 바꿔 보기 좋게 할 뿐, 화풍은 GPT 가 `style_reference.png` 를 따라 그린다.

## 비교 테스트 전용 (게임 반영 금지)

`pixelfx-render.ps1` · `pixelfx-convert.mjs` · Unity 메뉴 `ReTrap → Art → pixelfx 이펙트 가져오기`(`PixelfxImporter`)는
pixelfx 결과를 우리 규격(`Pfx_*`, `Sprites/Px32/Effects/Pixelfx/`)으로 바로 변환해 보는 도구다. GPT 버전과 비교할 때만 쓴다.

## 금지

- `..\pixelfx\gallery\` 의 **Patch Pixel 폰트(MIT 아님)** 사용 금지 — gallery 빌드(`build-gallery.mjs`, `npm run gallery`)를 돌리지 않는다.
- `Assets/` 에 JS 를 넣지 않는다. 크레딧은 `THIRD_PARTY_NOTICES.md`.
- 버전 올리기: `pixelfx.lock.json` 의 version·commit 변경 → setup 재실행 → 레퍼런스 렌더 확인 → `THIRD_PARTY_NOTICES.md` 저작권 줄 확인.
