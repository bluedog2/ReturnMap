# 32px 규격 전환 계획 (640×360 · 32px 타일 · PPU 32 · Pixel Perfect Camera)

작성 2026-10-24 · 영향 조사(Sonnet) + 설계(Opus) 종합. 시안: `Assets/_Project/ResourcceEX/Scenes/ScaleTest_32px.unity` (`ReTrap → Dev → 32px 규격 시안 씬 생성`).

## 0. 가정
- A1 적(AI 에이전트)은 검증 페이즈에만 나온다 → 적 개체 스케일(`AgentContext` `_stats.Scale`)은 PPC 정합 대상 아님.
- A2 Cinemachine 3 사용 → `CinemachinePixelPerfect`, `PixelPerfectCamera.CorrectCinemachineOrthoSize()` 사용 가능.
- A3 기준 모니터 1920×1080 → PPC 3배, Play 렌즈 ortho 5.625.
- A4 이동·물리 수치는 유닛 기반, 플레이어 캡슐(0.7×1.49)이 이미 1.5칸 → 물리 재튜닝 불필요(시야 체감만 변화).
- 확인된 현존 결함: 검증 뷰가 cover 맞춤(`BuildCameraController.cs:236-237`)이라 **stage_02(24×16)는 지금도 16줄 중 13.5줄만 보임**. `VerificationAgent.cs:106` 에이전트 interpolation None 강제(검증 뷰 떨림 후보).

## 1. 결정 사항

| 위험 지점 | 추천 · 근거 |
|---|---|
| 빌드·검증 뷰 | **PPC 끄고 맵 전체 맞춤(비정수 배율 허용)**. 32×18은 1080p 1.875배라 정수 배율로 전체를 못 보여줌. 정적 관전 뷰라 흔들림 적음. (선택) 줌 단계를 맞춤→2·3·4배로 스냅 |
| PPC ↔ BuildCameraController | **직접 제어 중 `ppc.enabled=false`, Play 진입 시 켬**. 글라이드 목표는 `CorrectCinemachineOrthoSize`, 끝에 `ForceCameraPosition` 직후 PPC·Brain 켜서 튐 방지 |
| PPC 모드 | gridSnapping **None**, cropFrame **None**(사용자 결정 U1). 카메라는 부드럽게, 픽셀 깨짐은 소스 규칙(정수 스케일·90° 회전)으로 방지 |
| 개체 스케일 | 플레이어·함정 Visual 스케일 **1 필수**. 적 `_stats.Scale` 은 검증 뷰 전용이라 허용 |
| 화살 회전 | 4방향만 쓰므로 **회전 유지**(90° 배수는 픽셀 정확). 90° 스냅 방어 코드(선택) |
| 서브픽셀 이동 | **스냅 없음 + 플레이어 Rigidbody2D Interpolate**(확인 필요). DropHammer·Spike 연속 이동은 유지 |
| 월드 UI | 스크린 HUD(1920×1080) 유지. 월드 게이지 정수 px: `TrapCooldownGauge` 0.8→0.75(24px), `DashGaugeUI` 0.9→1.0(32px), `SpriteUtil` PPU 32. 월드 Text 흐리면 그때 비트맵 폰트 |
| 콜라이더 1/32 정렬 | 보이는 접촉면만 정렬(CI 강제 안 함). Spike extendHeight 0.4→**0.375**, Hammer 0.9/0.7→0.875/0.6875, Arrow 0.5×0.15→0.5×0.15625, Shooter 0.6→0.625, 플레이어 캡슐 높이 1.49→1.4375 |
| 애니메이션 참조 단절 | 새 시트 + **기존 .anim GUID 유지·커브만 교체하는 도구**(SpriteClipRebinder) → Animator·Controller 무수정 |
| 시야 12→11.25칸 | 렌즈 5.625 확정, 레벨 무수정. 9단계 플레이테스트에서 블라인드 점프만 확인(필요 시 vcam 오프셋·룩어헤드) |
| 점프 체감 | 선제 변경 금지 → 플레이테스트 → `BalanceConfig`(BAL 트랙) |

### 사용자 결정 (2026-10-25 확정)
- U1 = **None** (gridSnapping None, cropFrame None)
- U2 = **빌드·검증 뷰는 PPC 끄고 맵 전체 맞춤**
- U3 = **(a) 비율 규칙** `ceil(h×16/9) ≤ w ≤ ceil(h×16/9)+1`, h ∈ {14, 18}, 프리셋 25×14·32×18 — **stage_02 는 25×14 로 축소**
- U4 = **Codex 생성 + 사람이 Aseprite 로 최종 보정 가능**

(아래는 결정 전 선택지 기록)
- **U1 Play 렌더 방식**: None(추천 — 부드러운 카메라, 월드 텍스트 선명) / UpscaleRT(완전한 픽셀, 카메라가 3px 단위로 걷고 월드 UI 저해상도)
- **U2 빌드·검증 뷰**: PPC 끄고 맵 전체 맞춤(추천) / 정수 배율 + 스크롤(전체 조망 기획과 충돌)
- **U3 맵 너비 규칙**: cover 맞춤이라 `w ≈ ceil(h×16/9)` 일 때만 맵 전체가 보임 → (a, 추천) 비율 [16/9, 16/9+1칸], 16칸 단위 폐기, 프리셋 25×14·32×18 / (b) 2배 폭까지 허용 + 검증 뷰 contain 맞춤(카메라 정책 변경, M 추가). + **stage_02 를 25×14 축소 / 32×18 확장 / 29×16(높이 16 허용)** 중 선택
- **U4 아트 보정 주체**: 전부 Codex 생성 + 자동 정규화 / 사람이 Aseprite 로 최종 보정 가능(주인공·타일 품질 위험 크게 줄어듦)

## 2. 아트 규격서 초안

### 2.1 공통 임포트 (`ResourcceEX/Sprites/Px32/` 하위 자동 적용)
PPU 32 · Point · 무압축 · FullRect · 밉맵 끔 · Clamp · Alpha is Transparency · Multiple(그리드, 간격·오프셋 0). 아틀라스: padding 2, 회전·타이트 패킹 금지.

### 2.2 크기·피벗·애니메이션
| 카테고리 | 보이는 크기(px) | 셀(px) | 피벗 | 애니(프레임@fps) |
|---|---|---|---|---|
| 타일 | 32×32 (페이즈 변형은 같은 실루엣·다른 팔레트). **종류당 변형 2~3장**(바닥·벽 A/B/C, 천장 A/B)을 위치 해시로 섞어 반복 무늬 방지 | 32 | 중앙 | — |
| 주인공 | 높이 48, 폭 ≤32 | 64×64 | 하단 중앙(발=0행) | Idle 6@8(호흡 1px + 망토 흔들림, 2026-10-25 파일럿 후 4@6 에서 상향) · Run 8@12 · JumpStart 2@12 · Fall 2@8 루프 · Land 3@12 · **Dash 4@20 전용 시트**(대시 0.2s 와 길이 일치) · Hit 2@10 · Death 6@10 |
| 적 소형 Goblin·Imp | 높이 32 | 48×48 | 하단 중앙(Imp 중앙) | Idle 4@6 · Run 6@10 · Hit 2@10 · Death 6@10 (Imp Death 는 눕는 바닥선을 프레임 하단 +8px = 피벗 아래 16px = 바닥면에) |
| 적 중형 Skeleton·Elite Knight | 높이 48 | 64×64 | 하단 중앙 | 동일 |
| 적 대형 Orc | 높이 64 | 96×96 | 하단 중앙 | 동일 |
| Spike | 받침 32×32 + 가시 32×12 | 32×48 | 받침 중앙 | 상태 4종, 상태당 1~2프레임 |
| ArrowShooter | 32×32(총구 오른쪽) | 32 | 중앙 | 상태 4종 |
| DropHammer | 머리 32×28, 체인 4×8(타일링) | 32 / 8 | 머리 상단 중앙 | 상태 4종 |
| 화살 | 16×5(오른쪽) | 16×8 | 중앙 | 1 |
| 황금블록 | 32×32 | 32 | 중앙 | 반짝임 4@8(선택) |
| 슬롯 마커 / 봉인 | 32×32 | 32 | 중앙 | — |
| 이펙트 | 먼지 16 · 피격 32 · 사망 48 | 같음 | 중앙(먼지 하단) | 4~6@15~20 |
| 배경 원경 | 640×360, 가로 이음매 없음 | — | 중앙, 카메라 Y 고정 | — |
| 배경 근경 | 640×256 띠, 위쪽 투명 | — | 하단 중앙, 맵 바닥 고정 | — |
| UI 아이콘(HUD) | 32×32 → 96px(3배) | 32 | 중앙 | — |

### 2.3 시트 레이아웃
애니메이션당 PNG 1장, 가로 1줄, 셀 간격 0. `<대상>_<애니>.png` / 스프라이트 `<대상>_<애니>_<i>`. 셀 상·좌·우 1px 비움(바닥 기준선 행 예외).

### 2.4 팔레트·외곽선
마스터 팔레트 32~48색(`NEWIMAG/palette_32px.txt`), 스프라이트당 16색 이하. 캐릭터·함정 1px 외곽선(램프의 가장 어두운 색, 순흑 금지). **타일 바깥 외곽선 금지**. 광원 좌상단. 알파 0/255만, AA·그라데이션 금지, 디더는 2×2 체커만.

### 2.5 Codex 프롬프트 필수 제약
- image_gen 은 정확한 저해상도를 못 냄 → **k배 격자 업스케일 출력** 요구 후 가져올 때 다운샘플(예: 주인공 64×64×4프레임 → 2048×512, 아트 1px = 8×8).
- 투명 배경, 측면, 오른쪽 향함. 모든 프레임 발이 같은 기준선, 몸통 중심 셀 가로 중앙 ±1px, 키 차이 ±1px.
- 1px 외곽선 지정 hex, 팔레트 hex 목록만 사용. 셀 가장자리 1px 침범 금지.
- 기준 이미지 첨부: 같은 대상의 승인 프레임 + 타일 1장(스케일 비교).

### 2.6 수용 검사 (`ArtInboxImporter` Px32 모드 + 신규 `PixelArtSheetValidator`, CI 연동)
1 격자 검출·최빈색 다운샘플(재업스케일 오차 초과 = 가짜 픽셀 거부) · 2 치수·프레임 수 · 3 알파 0/255 · 4 팔레트 밖 색 0 · 5 기준선 드리프트 ±0(공중 애니 예외) · 6 수평 ±2px·높이 ±2px(Death 예외) · 7 셀 테두리 1px 불투명 0 · 8 타일 32×32·이음 연속성 · 9 비교 시트 PNG 사람 승인.

## 3. 단계별 로드맵

**병행 전략**: 새 에셋은 `Sprites/Px32/` 별도 폴더. `FitToCell`·Visual 보정 스케일이 크기를 맞춰주므로 카테고리별 참조 교체 중에도 게임이 돈다. PPC 는 `BuildCameraController` 의 `pixelPerfectPlay` 플래그로 켜고 끔. 단계마다 짧은 SPEC 브랜치, 되돌리기 = 그 커밋 revert. 보정 스케일은 해당 카테고리 교체 후에만 1로. 구 에셋 삭제는 9단계에서만.

| 단계 | 목표 | 작업 | 담당 | 완료 기준 | 규모 |
|---|---|---|---|---|---|
| 0 사전정리 | 깨끗한 기준점 | 미커밋 가시 매립·애니메이션 보정 플레이 확인 후 커밋, 팔레트 확정, U1~U4 결정 | 사람 | main 클린, CI PASS | S |
| 1 카메라·PPC | Play PPC, Build/Verif 직접 제어 | `BuildCameraController` PPC 토글·글라이드 보정·플래그, SampleScene PPC(640×360/32), vcam `CinemachinePixelPerfect`·Lens 5.625 | Astra→Opus | 1080p/720p/1440p 에서 Build 줌·팬·Verif 맞춤 기존과 동일, Play 진입 튐 0, SmokeProbe PASS | M |
| 2 툴 규격화 | 32px 파이프라인·자동 검사 | ArtInboxImporter Px32 모드, `Px32ImportPostprocessor`, `PixelArtSheetValidator` + CI `sprites32`, `SpriteClipRebinder`, BackgroundTileImporter PPU 명시, TrapStateVisualSetup extendHeight 데이터화·PPU 폴백 32 | Astra | 결함 샘플 3종(드리프트·잘림·가짜 픽셀)만 거부, EditMode PASS | M |
| 3 타일 | 지형 32px | Codex 타일 세트 → `Px32/Tiles`, 새 TilePaletteConfig·아틀라스, TrapPrefabBuilder TileSprite | Codex / Sonnet / 사람 승인 | 전 스테이지 3페이즈 이음매·빈칸 0, CI PASS | 아트 L / 코드 S |
| 4 주인공 | 플레이어 32px | 시트 9종 + Dash → `Px32/Player`, Rebinder 로 클립 교체, Visual 0.42→1, 캡슐 0.6875×1.4375, effectScale→1, 보간 확인 | Codex / Astra / 사람 | 검증기 PASS, 발-바닥 틈 0px, 대시 전용 시트, SmokeProbe PASS | L |
| 5 적 | 5종 32px | 시트 20장 → `Px32/Enemies`, 클립 교체, Visual 1, 콜라이더 정리 | Codex / Sonnet | 크기 비율 규격표 일치, 검증 돌파율 ±10% | L |
| 6 함정·이펙트·월드 UI | | TrapBase 1셀 강제 스케일 제거, 화살 Visual 1, 콜라이더 표 값, TrapStateVisualSetup 재실행, SpriteUtil PPU 32, 게이지 크기, 이펙트 교체 | Codex / Astra | 슬롯=지형 유지(설치 칸 솔리드·빈 슬롯 봉인·Dud 0칸·Beneficial +1·DropHammer opt-out), CI 소팅 PASS | L |
| 7 배경 | 2층 1:1 | ParallaxBackground: Play 스케일 1·원경 Y 고정·근경 바닥 고정, Build/Verif 뷰 높이 맞춤 | Codex / Sonnet | 빈 하늘 노출 0, Play 배경 텍셀 3px 정수 | M |
| 8 맵 규격 | 프리셋·검사 | 4절 | Astra | 4절 기준 | M |
| 9 튜닝·정리 | 마감 | 플레이테스트(시야·점프) → BAL, 구 에셋·보정 코드 삭제, SpriteSheetPivotAligner 폐기 검토 | 사람 / Sonnet | 플레이테스트 승인, 전체 CI·SmokeProbe PASS, 구 PPU 참조 0 | M |

의존: 0 → 1 → 2 → (3 → 4 → 6), 5·7 은 2 이후 병렬, 8 은 1 이후 언제든, 9 마지막. 처음엔 **타일 1세트 + 주인공 Idle 1개로 파이프라인 파일럿**.

## 4. 맵 에디터 규격 (U3 = a 기준)
- 프리셋: S 25×14, L 32×18 (+ U3 에서 높이 16 허용 시 M 29×16).
- 규칙: h ∈ {14, 18}(+16), `ceil(h×16/9) ≤ w ≤ ceil(h×16/9)+1`. 최소 25×14, 최대 33×18. 16칸 단위 규칙 폐기(배경은 640px=20칸 단위 타일링이라 맵 폭과 무관).
- 기존 맵: stage_01(25×14)·03·04(32×18) 유지, stage_02(24×16) 는 U3 선택 크기로 재설계 후 Validator·도달성·스폰 테이블·SmokeProbe 재실행.
- 수정 지점: `MapEditorWindow.IO.cs:164-165,183-184`(프리셋·규격 밖 경고), `MapEditorWindow.Sidebar.cs:167-188`(프리셋 드롭다운·라벨), `MapAuthoringValidator.cs:42-45`(`CheckViewportSpec`), `CIRunner.cs:225-235`(규격 위반 **error** 승격).
- 배경: Play 뷰 20×11.25칸 고정, 원경 카메라 고정(맵 크기 무관), 근경 맵 폭만큼 반복, Build/Verif(최대 33×18칸) 는 뷰 높이에 맞춰 확대.

## 5. 위험 Top 5
1. 생성형 도트 품질(가짜 픽셀·드리프트·잘림·팔레트 이탈) → 격자 업스케일+다운샘플, 자동 검사 9항목, 사람 승인, U4, 파일럿.
2. 카메라 페이즈 전환 회귀 → 1단계 단독 SPEC, 해상도 매트릭스 확인, 플래그 롤백.
3. 애니메이션·프리팹 참조 단절 → GUID 유지 Rebinder, CI 누락 참조 스캔, 카테고리별 커밋.
4. 장기 전환 중 충돌 → 0단계 정리, `Px32/` 병행 폴더, 짧은 브랜치, 구 에셋은 9단계에서만 삭제.
5. 레벨·체감 변화 → BalanceConfig 로만 수치 변경, 검증 돌파율 전후 비교(±10%), 플레이테스트 트랙.

## 6. SPEC 분할 (spec-cycle)
- PX1 카메라 · PX2 임포트·검사 툴 · PX3 클립 재배선 도구 · PX4 타일 배선 · PX5 주인공 배선 · PX6 적 배선 · PX7 함정·화살·황금블록·마커 · PX8 이펙트·월드 UI · PX9 배경 · PX10 맵 규격 · PX11 정리(튜닝은 BAL 트랙).
- 아트 생성(Codex image_gen)은 SPEC 밖 "아트 입고" 작업 — 각 배선 SPEC 의 선행 조건으로 기획안에 적는다.
