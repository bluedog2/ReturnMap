# 자동 플레이테스트

기획자가 쓴 스테이지별 함정 배치(fixture)로 검증 페이즈(AI 돌파)를 N 시드 돌려 결과를 JSON 으로 남긴다.
판정(PASS/FAIL)은 "실행 성공"(설치·타임아웃·콘솔 오류·결정성)만이고, 돌파율은 **데이터**다.

## fixture 형식 (`tools/playtest/fixtures/<mapId>.json`)

```json
{"mapId":"stage_01","seedCount":5,"seedBase":1001,"placements":[{"trap":"TrapDefinition_Spike","x":10,"y":3}]}
```
- `mapId`: `stage_\d+` 이고 StageCatalog 에 존재. `seedCount` 1~20, `seedBase` ≥ 1(0 은 랜덤 시드), 시드 = `seedBase + i`.
- `trap`: TrapDefinition 에셋 이름(`TrapDefinition_Spike` / `_ArrowShooter` / `_DropHammer`). `x,y`: 맵 JSON `trapSlots` 좌표.
  슬롯 앵커 호환·`buildBudget` 안이어야 설치된다(위반 시 해당 fixture FAIL). 최대 64개, 슬롯 0개 맵은 빈 배열.
- fixture 위치: `<probeOutput 폴더>/fixtures/` 가 있으면 우선, 없으면 `<project>/tools/playtest/fixtures/`
  (후자는 검증 ref 에 fixture 가 커밋돼 있어야 CI worktree 에서 보인다).

## 실행

```powershell
powershell -ExecutionPolicy Bypass -File tools\ci\run-probe.ps1 -Ref <ref> -Probe tools\playtest\PlaytestProbe.cs -TimeoutMinutes 30
```
결과: `Logs/ci-spec/<tag>/PlaytestProbe.txt`(PASS/FAIL 항목) + 같은 폴더 `Playtest.json`(시드별 breached/reached/killed/simSec, 돌파율).

## 주의
- 검증 페이즈는 변이를 적용하지 않는다 → 변이 확률은 돌파율에 영향 없음.
- 스폰 테이블 에셋은 현재 `SpawnTable_Stage01` 하나뿐 — 모든 스테이지가 이 테이블로 돈다(`Playtest.json` 의 `spawnTable` 로 확인). 스테이지별 밸런스를 보려면 스테이지별 테이블이 필요.
- 결정성(첫 fixture 첫 시드 재실행 비교)은 같은 머신·같은 Unity 버전에서만 보장된다. `Time.captureDeltaTime=1/60` 고정.
