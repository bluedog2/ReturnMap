# CLAUDE.md — Re:Trap (리:트랩)

Unity 6 (6000.3.10f1) URP 2D 게임. 2D 샌드박스 퍼즐 플랫포머 + 로그라이트.
핵심 루프: **빌드 페이즈**(함정 배치) → **검증 페이즈**(AI 돌파 시도) → **플레이 페이즈**(변이 함정 직접 돌파).

상세 설계·진행 상황은 작성자의 메모리(`retrap-project-overview`, `retrap-progress-2026-06`)와 GDD(작성자 로컬 문서 `리트랩.docx`, 저장소 미포함) 참조.

---

## 모델 라우팅 정책 (정확도↑ / 토큰↓)

이 저장소에서 작업할 때 메인 세션(Opus)은 **오케스트레이터**로 동작한다. 직접 대량 파일을 읽지 말고 아래 기준으로 서브에이전트에 위임한다.

| 작업 | 에이전트 | 모델 | 이유 |
|------|----------|------|------|
| 코드/파일 **탐색**, "어디에 있나", 영향 범위 조사 | `Explore` 또는 `code-explorer` | Sonnet | 넓게 훑는 작업은 싼 모델로 |
| 코드 **구현·수정** (정의된 작업) | `code-implementer` | Sonnet | 명세가 분명한 변경은 Sonnet으로 충분 |
| **설계 리뷰**, 아키텍처 판단, 트레이드오프, 변경 계획 승인 | `design-reviewer` | Opus | 판단·정확도가 중요한 곳에만 비싼 모델 |

운영 원칙:
- 메인 세션은 탐색 결과/구현 diff를 **결론만** 받아 종합한다. 전체 파일 덤프를 컨텍스트로 끌어오지 않는다.
- 구현 전, 비자명한 설계 결정은 `design-reviewer`(Opus)에게 먼저 검토받는다. 자명한 수정은 바로 `code-implementer`(Sonnet)로 보낸다.
- 한 작업에 여러 각도가 필요하면 서브에이전트를 병렬로 띄운다.

## 작업 원칙 (Karpathy)

- 가정 명시: 불확실하면 보고의 `가정:` 섹션에 적고, 판단이 갈리면 구현 전 멈추고 보고.
- 최소 구현: 요청·승인되지 않은 기능·추상화 금지.
- 국소 변경: 인접 코드 "개선" 금지, 기존 스타일 유지, 데드 코드/잠재 버그는 언급만.
- 목표 우선: 시작 전 성공 기준(컴파일 + 확인 시나리오)을 정의하고, 완료 보고 때 충족 여부를 명시.

---

## 기획 → 개발 사이클

Notion 기획안 DB의 `개발 상태`(개발요청/답변완료)를 감지해 설계·질문을 수행하는 스킬: `/spec-cycle` (`.claude/skills/spec-cycle/SKILL.md`). 운영 규칙은 Notion「🔁 개발 사이클 운영 가이드」. 1단계(설계·질문) + 2단계(Unity CLI CI) + 3단계(승인된 설계 → spec 브랜치 구현 → CI → 자동 수정 → Notion 리포트) + 4단계(데스크톱 앱 예약 작업 `spec-cycle-auto` 주기 실행 · 실행 가드 `Logs/spec-cycle.lock` · 사람 할 일 생길 때만 푸시 알림 · 완료+병합된 spec 브랜치 자동 정리) 구축됨. 스킬은 디스패처 `SKILL.md` + `design-track.md` · `impl-track.md`(대상 있을 때만 읽음). 검증 = CI(정적·소팅 레이어) + 기획별 Play 프로브(구현과 분리된 작성자) + 공통 `SmokeProbe`(전 스테이지 회귀). 보조 스크립트·권한 경계는 `tools/ci/README.md`. 5단계(2026-10-03): EditMode 테스트(`-Tests` 기본) · `BalanceConfig` SO(`Assets/_Project/Settings`) · Notion「밸런스 수치」→ `balance-track.md`(`tools/balance/apply-balance.ps1`, 스키마 화이트리스트, `spec/BAL-n`) · Notion「플레이테스트」→ `playtest-track.md`(`tools/playtest/PlaytestProbe.cs`). 버그 사이클(2026-10-06): Notion「버그」→ `bug-track.md`(수정 전 재현 검증 커밋 → `spec/BUG-n` 수정 → CI·재현·스모크).

---

## 프로젝트 구조

- `Assets/_Project/Scripts/` — 런타임 게임 코드 (네임스페이스 `ReTrap`)
  - `Trap/` — `ITrap`, `TrapBase`(변이 추상화), `TrapMutationManager`, `Traps/`(SpikeTrap·ArrowShooter·Arrow·DropHammer)
  - `Player/` — `PlayerController`(코요테/점프버퍼/대시), `PlayerHealth`, `PlayerAnimationFSM`
  - `Map/` — `MapLoader`, `MapData`, `TilePaletteConfig`, `TrapSlotMarker/Registry`, `AddressableLoader`
  - `Camera/` — `BuildCameraController`, `CameraConfinerSync`
  - `GamePhaseManager.cs`, `BuildPhaseController.cs`, `BuildHudController.cs`, `RespawnManager.cs`
- `Assets/_Project/Editor/` — 에디터 전용 (`MapEditorWindow.*`, `TrapPrefabBuilder`, `MapAuthoringValidator`, Addressables/SpriteAtlas 빌드)
- `Assets/_Project/ResourcceEX/` — **모든 프리팹·스프라이트·씬·애니메이션의 단일 위치** (오타 폴더명 그대로 사용). 프리팹은 여기 `Prefabs/`에만 둔다.
- `Assets/StreamingAssets/Maps/` — JSON 맵 데이터 (`stage_01.json`, `stage_02.json`, `stage_03.json`)
- asmdef: 런타임 `ReTrap.Runtime`(`Scripts/`, autoReferenced) · EditMode 테스트 `ReTrap.Tests.EditMode`(`Assets/_Project/Tests/EditMode`, `InternalsVisibleTo`로 internal 접근). 에디터 코드는 asmdef 없이 `Assembly-CSharp-Editor`. 런타임 `.cs`는 `Scripts/`, 에디터는 `Editor/`, 테스트는 `Tests/`에만(CIRunner `scriptFolders` 검사). `Scripts/`에서 `using UnityEditor`는 `#if UNITY_EDITOR` 안에서만.

## 코드 컨벤션 (기존 코드와 일치시킬 것)

- 네임스페이스 `ReTrap`로 감쌀 것.
- 주석·Tooltip·XML 문서는 **한글**. 클래스 헤더는 `// ═══...═══` 박스 구분선 + `<summary>` 역할 설명.
- 인스펙터 노출 필드는 `[SerializeField] private` + `[Header(...)]` 그룹 + `[Tooltip("...")]`.
- 매니저류 싱글톤: `public static X Instance { get; private set; }`, `Awake`에서 중복 파괴.
- 페이즈 전환 등 전역 이벤트는 `static event Action<...>` 발행/구독.

## 검증 (Unity)

- 코드 변경은 컴파일 가능해야 한다. Unity 에디터에서 컴파일/플레이로 확인하는 흐름.
- **무인 검증 = Unity CLI**: `powershell -ExecutionPolicy Bypass -File tools\ci\unity-ci.ps1 [-Ref <branch>] [-Tests]` → `Logs/ci/ci-summary.json` (exit 0 PASS / 1 FAIL / 2 COMPILE_ERROR / 3 INFRA). 별도 worktree `..\ReturnMap-ci`에서 돌아 에디터를 열어둬도 된다. 상세는 `tools/ci/README.md`.
- **대화형 확인 = Unity MCP** (에디터 켜져 있을 때). 에디터 안에서는 `ReTrap → Dev → CI 검증 실행 (에디터)`로 같은 검사 가능.
- Unity MCP 연결은 컴파일/도메인 리로드/승인 만료로 자주 끊김 → Project Settings→AI→Unity MCP 승인 상시 허용. MCP 검증 시 `ReTrap/Dev/Fast Play (Reload Domain 끄기)` 사용 권장(static 오염 주의).
- 함정/HUD/타일 배선은 메뉴 `ReTrap → Setup → 함정 프리팹+Build UI 세팅`(TrapPrefabBuilder)가 자동 처리.

## 주의사항

- **소팅 레이어**: Default(v0) < Map(v1). 모든 게임 비주얼은 **Map 레이어**. order: 배경 -10 < 타일 0 < 함정/화살 10 < 황금블록 15 < 캐릭터 20. 새 엔티티도 Map 레이어 사용.
- **슬롯=지형 불변식**: 설치된 함정 칸 = 솔리드 지형(밟고 섬), 빈 슬롯은 Play에서 봉인. Dud=발사정지(0칸), Beneficial=바깥 +1칸 황금블록. DropHammer는 이동형이라 opt-out.
- **어드레서블 코루틴**: `AsyncOperationHandle`를 코루틴에서 직접 yield 금지 → `AddressableLoader.WaitAll`(IsDone 폴링) 사용.
- 커밋/푸시는 사용자가 요청할 때만. 커밋 메시지·UI 텍스트는 한글 톤 유지.
  - **예외 (2026-10-02 사용자 승인)**: `/spec-cycle` 구현 트랙은 `spec/SPEC-n` 브랜치(별도 worktree `..\ReturnMap-spec\SPEC-n`)에 한해 **로컬 커밋만** 자동으로 한다. push·main 병합·리베이스는 하지 않는다 — 병합은 사람이. 브랜치 삭제는 `완료` + main 에 완전히 병합된 `spec/SPEC-n` 만 `spec-worktree.ps1 -Remove -DeleteBranch`로 (2026-10-03 승인).
  - **버그 트랙 (2026-10-06 사용자 승인)**: `/spec-cycle` 버그 트랙은 `spec/BUG-n` 브랜치(worktree `..\ReturnMap-spec\BUG-n`)에 위와 같은 규칙(로컬 커밋만, push·병합 금지, `완료`+병합된 것만 삭제)으로 자동 커밋한다. 재현이 확인된 버그는 수정 전 승인 없이 수정까지 진행한다.
