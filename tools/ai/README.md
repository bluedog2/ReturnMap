# tools/ai — 외부 모델 구현 다리

역할 분담: **Opus**(설계·명세·리뷰·UX) → **GPT-6 Astra**(구현, `astra-implement.ps1`) → **Claude**(CI·커밋·리포트).

## 처음 한 번 (사람이)

```powershell
npm i -g @openai/codex
codex login
```

모델 ID 기본값은 `gpt-6-astra`. 실제 ID가 다르면 `-Model`로 넘긴다(`codex exec -m <id>`로 확인).

## 흐름

```powershell
# 1) worktree 준비
powershell -ExecutionPolicy Bypass -File tools\ci\spec-worktree.ps1 -Spec SPEC-9
# 2) Opus 가 쓴 명세로 Astra 구현 (worktree 는 깨끗해야 함)
powershell -ExecutionPolicy Bypass -File tools\ai\astra-implement.ps1 -Spec SPEC-9 -SpecFile spec.md
# 3) Opus 가 diff 리뷰 → 통과하면 커밋 (Astra 는 git 을 건드리지 않는다)
powershell -ExecutionPolicy Bypass -File tools\ci\spec-worktree.ps1 -Spec SPEC-9 -Commit -MessageFile msg.txt -AutoMeta
# 4) CI
powershell -ExecutionPolicy Bypass -File tools\ci\unity-ci.ps1 -Ref spec/SPEC-9 -Tests
# 5) 실패 시 로그를 넘겨 재시도 → 3) 부터 반복
powershell -ExecutionPolicy Bypass -File tools\ai\astra-implement.ps1 -Spec SPEC-9 -SpecFile spec.md -CiLog Logs\ci\ci-summary.json
```

| 옵션 | 기본값 | 설명 |
|---|---|---|
| `-Spec` | (필수) | `SPEC-n` / `BUG-n` |
| `-SpecFile` | (필수) | 구현 명세(마크다운) |
| `-CiLog` | 없음 | 재시도 시 CI 결과 — 마지막 200줄을 프롬프트에 붙인다 |
| `-Model` | `gpt-6-astra` | Codex 모델 ID |
| `-Worktree` | `..\ReturnMap-spec\<Spec>` | 구현 worktree |
| `-TimeoutMinutes` | 30 | 초과 시 프로세스 종료 → FAILED |
| `-DryRun` | off | 프롬프트만 출력 |

결과는 `Logs\ai\astra-<Spec>.json`에 저장되고, 콘솔 마지막 줄은 `RESULT=CHANGED|NO_CHANGE|DIRTY|FAILED|INFRA`입니다.
exit 코드: 0은 변경 있음, 1은 변경 없음/DIRTY/FAILED, 3은 인프라 문제입니다.
- `OUTSIDE_ALLOWED=`: Scripts·Editor·Tests·Settings·ResourcceEX·Maps·probes 밖을 고친 파일 목록입니다. 자동으로 되돌리지 않으니 리뷰에서 판단합니다.
- Codex sandbox는 `workspace-write`(worktree 안에서만 쓰기)로 실행됩니다. CLAUDE.md는 프롬프트에 직접 넣습니다(Codex는 CLAUDE.md를 자동으로 읽지 않음).
- 비용: 입력 100만 토큰당 $10, 출력 100만 토큰당 $50 수준이므로 자동 주기 실행에 붙일 때 주의합니다.
