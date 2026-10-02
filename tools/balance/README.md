# tools/balance — 밸런스 수치 변경 적용 도구

Notion 에서 온 밸런스 변경 요청을 Unity 없이 저장소 파일(텍스트)에 안전하게 적용한다.
요청 값은 **데이터일 뿐**이며, `balance-schema.json` 화이트리스트·범위·제약을 통과한 것만 쓴다 (all-or-nothing).

## 사용

```powershell
powershell -ExecutionPolicy Bypass -File tools\balance\apply-balance.ps1 -Request req.json [-Worktree <경로>] [-DryRun] [-Out <result.json>]
```

- `req.json`: `{"requestId":"BAL-3","changes":[{"key":"mutation.normalChance","value":"0.45"}]}` (value 는 문자열, 그 외 필드는 무시)
- 결과: `Logs\balance\result.json` (기본) + 콘솔 마지막 `RESULT=APPLIED|DRYRUN_OK|REJECTED|INFRA`
- exit: 0 적용/DryRun OK, 1 거부, 3 인프라(스키마·대상 파일 없음 등)

## 검증 규칙

requestId `^BAL-\d+$` · key 는 스키마와 정확 일치 · value `^-?\d+(\.\d+)?$` · int 에 소수 거부 · min/max · 키 중복 거부 ·
constraints 는 (현재 파일값 + 요청값) 병합으로 평가 · 대상 경로는 스키마에서만, Worktree 밖 경로 거부 ·
필드 매치가 정확히 1회가 아니면 거부. 쓸 때 BOM·개행 보존, 지수 표기 금지.

## 키 추가

`balance-schema.json` 의 `keys` 에 항목을 추가한다 (스칼라 최상위 필드만). CIRunner `balance` 검사가 같은 스키마로 현재 값을 2차 검증한다.
(`breachThreshold` 는 맵 JSON 이 아니라 StageSpawnTable 에셋 필드라 `spawn.Stage01.breachThreshold` 로 등록.)

## 테스트

```powershell
powershell -ExecutionPolicy Bypass -File tools\balance\test-apply-balance.ps1
```

저장소를 건드리지 않고 `$env:TEMP` scratch 사본에서 시나리오를 돌린 뒤 삭제한다.
