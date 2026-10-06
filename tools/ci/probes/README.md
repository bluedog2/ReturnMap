# Play 검증 프로브

CI(컴파일·데이터 검사)는 **실제 플레이 동작**을 보지 못한다. 프로브는 기획별 임시 에디터 스크립트로,
Unity 배치모드에서 Play 모드에 들어가 수용 기준을 **프레임 단위로 측정**하고 결과를 남긴 뒤 종료한다.

- 이 폴더는 `Assets` 밖이라 게임/에디터 빌드에 포함되지 않는다.
- 실행은 [`run-probe.ps1`](../run-probe.ps1)이 프로브를 검증 대상 커밋 위에 **임시로만** 얹어 CI worktree에서 돌린다(어느 브랜치에도 커밋되지 않음).
- 참고 구현: [`SpecProbe9b.cs`](SpecProbe9b.cs) — SPEC-9 함정 쿨다운 게이지, 27항목(로직 + 픽셀 가시성).

```powershell
powershell -ExecutionPolicy Bypass -File tools\ci\run-probe.ps1 -Ref spec/SPEC-12 -Probe tools\ci\probes\SpecProbe12.cs
```
종료 코드 `0` PASS · `1` FAIL · `3` INFRA. 결과는 `Logs/ci-spec/SPEC-12/SpecProbe12.txt` (+ PNG).

## 새 프로브 작성 규칙

**구조** — `SpecProbe9b.cs`를 복사해서 시작한다.
1. 파일명 = 클래스명(`SpecProbeN.cs` / `SpecProbeN`), 네임스페이스 `ReTrap.EditorTools`, `public static void Run()`.
2. `Run()`: `-probeOutput` 인자 경로 확보 → `EnterPlayModeOptions.DisableDomainReload | DisableSceneReload` → 씬 열기 → `EditorApplication.update += Tick` → `EnterPlaymode()`.
   - 도메인 리로드를 끄지 않으면 Play 진입 시 static 상태(단계·측정값)가 날아간다.
3. `Tick()`은 **단계 상태머신**: 각 단계는 조건을 걸고 일정 시간 측정한 뒤 `Check(이름, 통과?, 상세)`를 남기고 다음 단계로.
4. 끝나면 `ExitPlaymode()` → Play 가 끝난 뒤 결과 파일 첫 줄에 `RESULT: PASS|FAIL (fail n / total m)`, 이어서 항목별 `PASS|FAIL | 이름 | 상세`, 마지막에 콘솔 오류 목록 → `EditorApplication.Exit(0|1)`.
5. **전체 타임아웃**(예: 150초)을 두고 넘으면 FAIL 로 종료한다 — 배치모드가 영원히 떠 있지 않게.

**측정 원칙** (SPEC-9 에서 배운 것)
- 테스트 대상은 프리팹을 직접 `Instantiate` 해서 만든다. 변이·페이즈는 `Mutate()`·`GamePhaseManager.SetPhase(…, true)`로 **강제**한다(랜덤 굴림에 기대지 않는다).
- **주기·타이밍은 자연 발생 이벤트만 센다.** 진행도가 가득 찬 뒤(≥0.85) 0 으로 떨어질 때만 한 주기. 중간에서 떨어지면(페이즈 종료 등으로 루틴 재시작) 그때까지 측정을 버리고, 표본이 충분해질 때까지 상한 시간 안에서 더 잰다.
- 같은 프로브가 실행마다 다른 항목에서 실패하면 **제품 버그보다 측정 오염을 먼저 의심**하고 프로브를 고친다. 재실행으로 통과시키지 않는다.
- **`Time.timeScale`의 주인은 `VerificationSpeedController`** — 페이즈 이벤트마다 기본 배속으로 되돌린다. 배속 측정은 매 프레임 원하는 값인지 확인하고, 덮어써졌으면 다시 걸고 그 구간 측정을 버린다(`HoldScale()`). 끝나면 반드시 1 로 복원.
- 검증 페이즈는 웨이브가 끝나면 스스로 다른 페이즈로 넘어갈 수 있다 — 페이즈 의존 측정은 매 샘플에 현재 페이즈를 같이 기록한다.

**"보이는가" 픽셀 판정** (시각 요소가 있는 기획은 필수)
- 메인 카메라를 1920×1080 `RenderTexture`로 **대상 on / off 두 번** 렌더해 차이 픽셀로 대상 영역을 찾는다(같은 프레임이라 다른 움직임은 섞이지 않는다).
- 판정: 화면상 크기(px), 색(채움 색 픽셀 수), 배경 대비(밝기 차 — **차이가 큰 픽셀만** 써서 안티앨리어싱 가장자리 제외), 위치(겹침).
- Play 카메라는 플레이어를 따라가므로 대상을 **카메라 화면 안으로 옮긴 뒤** 찍는다. 관전(검증) 카메라는 맵 전체라 대상이 더 작게 나온다 — 두 화면 모두 판정한다.
- on 렌더를 `PNG`로 `-probeOutput` 폴더에 저장해 리포트에 쓴다.
- 기준값(최소 px·대비 등)은 **설계안에 적힌 값**을 쓴다. 설계안에 없으면 설계 단계에서 가정으로 먼저 적게 한다.

**콘솔 오류 = FAIL**: Play 중 Error·Exception 이 하나라도 나면 프로브는 FAIL 이다(`OnLog` + 마지막 `콘솔 오류 0개` 항목 — 템플릿 그대로 유지).
- 알려진 무해 오류만 스택으로 제외한다: `UnityEditor.Search.*` (SearchInit.IndexationOnStartup 의 배치모드 `ArgumentOutOfRangeException`). 새로 무해 판정을 추가하려면 근거를 이 README 에 적는다.

**자기채점 금지**
- 프로브는 **구현 diff 를 보지 않은 작성자**가 설계안의 수용 기준·수치만 보고 쓴다(spec-cycle 규칙). 코드 상수를 다시 읽어 비교하는 항목(예: `diameter == 0.8`)은 동어반복이라 넣지 않는다 — 대신 화면 px·주기·상태 같은 **관찰 가능한 결과**를 잰다.
- 판정 기준값은 설계안에 적힌 값만 쓴다. 통과시키려고 기준을 바꾸지 않는다.

**공통 스모크 프로브**: [`SmokeProbe.cs`](SmokeProbe.cs) — 기획과 무관하게 모든 구현 뒤에 돌리는 회귀 검사(전 스테이지 로드 · 페이즈 순환 · 예외 0 · 플레이어 스폰 · 설치 칸 솔리드).

## 버그 재현 프로브 (BugProbe{n})

버그 트랙(`.claude/skills/spec-cycle/bug-track.md`)이 수정 **전에** 커밋하는 재현 검증. 기대 동작을 단언하므로 수정 전 main 에서는 실패, 수정 후에는 통과해야 한다.
- 버그를 판정하는 항목 이름은 `[BUG]` 로 시작. 그 외 항목(맵 로드·페이즈 도달 등)은 준비 항목 — 준비 항목이 실패하면 "재현"이 아니라 프로브 결함.
- 작성자는 수정 담당과 다른 인스턴스, 수정 담당은 이 파일을 고치지 않는다. 기대값 완화 금지.
- 병합 후 main 에 남아 회귀 검증으로 재사용할 수 있다.
