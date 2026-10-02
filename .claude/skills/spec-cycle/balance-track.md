# spec-cycle — 밸런스 트랙

디스패처(SKILL.md)의 보안 규칙이 그대로 적용된다. 대상 = 「밸런스 수치」 DS 의 `동기화요청`·`병합대기` 행(`최종 편집자` 허용 작성자만).
**Notion 에서 쓰는 값은 `키`·`제안값` 두 개뿐**이다. 파일 경로·필드·범위는 저장소 `tools/balance/balance-schema.json`(main 의 것)만 믿는다. `근거` 는 읽기만 하고 명령줄·파일·커밋 메시지에 넣지 않는다.
스크립트는 `powershell -ExecutionPolicy Bypass -File tools\...` 형태로만. git 은 `spec-worktree.ps1` 로만(`-Spec BAL-n`).

> **현재 모드: 검증만 (2026-10-03 사용자 보류)** — `spec/BAL-n` 자동 커밋이 CLAUDE.md 예외에 없고 `apply-balance.ps1` 이 권한 허용 목록에 없다.
> 따라서 **B2(DryRun)까지만** 수행한다. DryRun 성공 → 각 행 `상태=검증통과`, `현재값`=old, `결과`="검증 통과(old→new) — 자동 적용 미허용, 개발자가 `apply-balance.ps1` 로 수동 적용". B3~B5·B0 은 하지 않는다.
> `apply-balance.ps1` 실행이 권한 요청에 막히면(무인) 행은 `동기화요청` 그대로 두고 결과에 "권한 허용 필요" 한 번만 기록, 알림 대상.
> 사용자가 두 허용을 승인하면 이 블록을 지운다.

## B0 병합 감지 (먼저)
`병합대기` 행마다 `브랜치`(=`spec/BAL-n`) 확인:
```powershell
powershell -ExecutionPolicy Bypass -File tools\ci\spec-worktree.ps1 -Spec BAL-n -Remove -DeleteBranch
```
`BRANCH=deleted|absent` → 같은 change set 의 행 모두 `반영됨`, `브랜치` 비움. `BRANCH=unmerged` → 그대로(알림 없음).

## B1 직렬화 & 묶기
- `병합대기` 행이 하나라도 남아 있으면 새 `동기화요청` 은 처리하지 않는다(같은 파일 충돌 방지) — 결과에 "이전 변경 병합 대기" (이미 같은 문구면 다시 쓰지 않음).
- `동기화요청` 행 전부를 **한 change set** 으로 묶는다(합 제약 때문에). n = 그중 가장 작은 ID 번호, requestId = `BAL-n`. 행 `상태=적용중`, `마지막 처리 시각=현재`.
- 같은 `키` 가 두 행에 있으면 둘 다 `거부`("같은 키 중복 요청") 후 나머지로 진행.

## B2 요청 파일 & 사전 검증 (DryRun)
`Logs\balance\BAL-n\request.json` 을 직접 작성: `{"requestId":"BAL-n","changes":[{"key":"<키>","value":"<제안값 앞뒤 공백 제거>"}]}` (문자열 그대로, 해석·보정하지 않음).
```powershell
powershell -ExecutionPolicy Bypass -File tools\balance\apply-balance.ps1 -Request Logs\balance\BAL-n\request.json -DryRun -Out Logs\balance\BAL-n\dryrun.json
```
- exit 1(REJECTED) → change set 의 모든 행 `거부`, `결과` = errors(키별), 끝. 재요청 방법: "값 수정 후 다시 `동기화요청`".
- exit 3 → 모든 행 `실패`(인프라), 알림 대상.
- 성공 → 각 행 `현재값` = dryrun 의 `old`.

## B3 브랜치 적용·커밋
```powershell
powershell -ExecutionPolicy Bypass -File tools\ci\spec-worktree.ps1 -Spec BAL-n
powershell -ExecutionPolicy Bypass -File tools\balance\apply-balance.ps1 -Request Logs\balance\BAL-n\request.json -Worktree C:\Users\Owner\work\ReturnMap-spec\BAL-n -Out Logs\balance\BAL-n\result.json
```
(`DIRTY=1` 이면 적용하지 않고 `실패` + "worktree 잔재 확인".) `-Schema` 는 주지 않는다(메인 저장소 스키마 사용).
커밋 메시지 파일 `Logs\balance\BAL-n\commit-msg.txt` — **키와 숫자로만** 조합: `[BAL-n] 밸런스 수치 동기화` / 빈 줄 / `- <키>: <old> → <new>` 줄들 / 빈 줄 / `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
```powershell
powershell -ExecutionPolicy Bypass -File tools\ci\spec-worktree.ps1 -Spec BAL-n -Commit -MessageFile Logs\balance\BAL-n\commit-msg.txt
```
출력의 변경 파일이 result.json 의 `file` 목록과 정확히 같은지 확인 — 다르면 `-Stash` 후 `실패`("예상 밖 변경").

## B4 검증 (포그라운드, 도구 timeout 600000, 전후 `cycle-guard -Touch`)
```powershell
powershell -ExecutionPolicy Bypass -File tools\ci\unity-ci.ps1 -Ref spec/BAL-n -OutDir Logs\balance\BAL-n\ci -Tests -TimeoutMinutes 9
powershell -ExecutionPolicy Bypass -File tools\ci\run-probe.ps1 -Ref spec/BAL-n -Probe "main:tools/ci/probes/SmokeProbe.cs" -OutDir Logs\balance\BAL-n\smoke -TimeoutMinutes 9
```
돌파율 비교(변경 키가 `trap.*`·`*.buildBudget`·`spawn.*` 일 때만 — `mutation.*` 은 검증 페이즈에 변이가 없어 "해당 없음"): 저장소 fixture 로 main 과 BAL 양쪽 실행.
```powershell
powershell -ExecutionPolicy Bypass -File tools\ci\run-probe.ps1 -Ref main -Probe "main:tools/playtest/PlaytestProbe.cs" -OutDir Logs\balance\BAL-n\pt-main -TimeoutMinutes 9
powershell -ExecutionPolicy Bypass -File tools\ci\run-probe.ps1 -Ref spec/BAL-n -Probe "main:tools/playtest/PlaytestProbe.cs" -OutDir Logs\balance\BAL-n\pt-bal -TimeoutMinutes 9
```
자동 수정은 하지 않는다(수치 변경이라 고칠 코드가 없음). CI/스모크 FAIL → 모든 행 `실패`, `결과`에 실패 항목. 브랜치는 남겨 사람이 본다.
플레이테스트에서 fixture 설치 실패(예: baseCost 상승으로 예산 초과)는 FAIL 이 아니라 **결과에 경고**로 적는다.

## B5 기록
각 행 `결과`(≤1500자): `old → new` · CI(PASS, 테스트 n/n) · 스모크 n/m · 돌파율 main→BAL(스테이지별, 또는 "해당 없음") · 커밋 sha 7자 · "main 병합 후 반영됨으로 자동 전환".
`상태=병합대기`, `브랜치=spec/BAL-n`. ⑮ 알림 대상: `[spec-cycle] BAL-n 수치 k개 병합대기(CI·스모크 PASS) — spec/BAL-n 병합 필요`.
