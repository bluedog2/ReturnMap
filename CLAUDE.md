# CLAUDE.md — Re:Trap (리:트랩)

Unity 6 (6000.3.10f1) URP 2D 게임. 2D 샌드박스 퍼즐 플랫포머 + 로그라이트.
핵심 루프: **빌드 페이즈**(함정 배치) → **검증 페이즈**(AI 돌파 시도) → **플레이 페이즈**(변이 함정 직접 돌파).

상세 설계·진행 상황은 작성자의 메모리(`retrap-project-overview`, `retrap-progress-2026-06`)와 GDD(`C:\Users\Owner\Desktop\리트랩.docx`) 참조.

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
- `Assets/StreamingAssets/Maps/` — JSON 맵 데이터 (`stage_01.json`, `Stage_2.json`)
- asmdef 없음 → 전부 `Assembly-CSharp` / `Assembly-CSharp-Editor`.

## 코드 컨벤션 (기존 코드와 일치시킬 것)

- 네임스페이스 `ReTrap`로 감쌀 것.
- 주석·Tooltip·XML 문서는 **한글**. 클래스 헤더는 `// ═══...═══` 박스 구분선 + `<summary>` 역할 설명.
- 인스펙터 노출 필드는 `[SerializeField] private` + `[Header(...)]` 그룹 + `[Tooltip("...")]`.
- 매니저류 싱글톤: `public static X Instance { get; private set; }`, `Awake`에서 중복 파괴.
- 페이즈 전환 등 전역 이벤트는 `static event Action<...>` 발행/구독.

## 검증 (Unity)

- 코드 변경은 컴파일 가능해야 한다. Unity 에디터에서 컴파일/플레이로 확인하는 흐름.
- Unity MCP 연결은 컴파일/도메인 리로드/승인 만료로 자주 끊김 → Project Settings→AI→Unity MCP 승인 상시 허용. MCP 검증 시 `ReTrap/Dev/Fast Play (Reload Domain 끄기)` 사용 권장(static 오염 주의).
- 함정/HUD/타일 배선은 메뉴 `ReTrap → Setup → 함정 프리팹+Build UI 세팅`(TrapPrefabBuilder)가 자동 처리.

## 주의사항

- **소팅 레이어**: Default(v0) < Map(v1). 모든 게임 비주얼은 **Map 레이어**. order: 배경 -10 < 타일 0 < 함정/화살 10 < 황금블록 15 < 캐릭터 20. 새 엔티티도 Map 레이어 사용.
- **슬롯=지형 불변식**: 설치된 함정 칸 = 솔리드 지형(밟고 섬), 빈 슬롯은 Play에서 봉인. Dud=발사정지(0칸), Beneficial=바깥 +1칸 황금블록. DropHammer는 이동형이라 opt-out.
- **어드레서블 코루틴**: `AsyncOperationHandle`를 코루틴에서 직접 yield 금지 → `AddressableLoader.WaitAll`(IsDone 폴링) 사용.
- 커밋/푸시는 사용자가 요청할 때만. 커밋 메시지·UI 텍스트는 한글 톤 유지.
