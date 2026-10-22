# ═══════════════════════════════════════════════════════════════
#  astra-implement.ps1 — GPT-6 Astra(OpenAI Codex CLI)로 spec worktree 구현
# ═══════════════════════════════════════════════════════════════
# 역할 분담: Opus(설계·명세·리뷰) → Astra(구현, 이 스크립트) → Claude(CI·커밋·리포트).
# Astra 는 spec worktree 안의 파일만 고친다. git 커밋·푸시는 하지 않는다
# (커밋은 리뷰 후 spec-worktree.ps1 -Commit 으로만).
#
# 사용:
#   tools\ai\astra-implement.ps1 -Spec SPEC-9 -SpecFile spec.md
#   tools\ai\astra-implement.ps1 -Spec SPEC-9 -SpecFile spec.md -CiLog Logs\ci\ci-summary.json   # CI 실패 후 재시도
#   tools\ai\astra-implement.ps1 -Spec SPEC-9 -SpecFile spec.md -ReviewFile review.md -AllowDirty # Opus 리뷰 지적 반영(미커밋 구현 위에서)
#   tools\ai\astra-implement.ps1 -Spec SPEC-9 -SpecFile spec.md -DryRun                          # 프롬프트만 출력
#
# 전제: Codex CLI 설치·로그인(`npm i -g @openai/codex` → `codex login`), worktree 는 spec-worktree.ps1 로 먼저 생성.
# 결과: Logs\ai\astra-<Spec>.json + 콘솔 마지막 줄 RESULT=CHANGED|NO_CHANGE|DIRTY|FAILED|INFRA
# exit: 0 변경 있음/DryRun, 1 변경 없음·DIRTY·FAILED, 3 인프라(codex 없음·worktree 없음 등)

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidatePattern('^(SPEC|BUG)-\d+$')][string]$Spec,
    [Parameter(Mandatory = $true)][string]$SpecFile,
    [string]$CiLog = '',
    [string]$ReviewFile = '',
    [switch]$AllowDirty,
    [string]$Model = 'gpt-6-astra',
    [string]$Worktree = '',
    [int]$TimeoutMinutes = 30,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$Utf8NoBom = New-Object System.Text.UTF8Encoding $false

function Invoke-Git {
    $prev = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { & git @args 2>$null } finally { $ErrorActionPreference = $prev }
}

$RepoRoot = (Invoke-Git -C $PSScriptRoot rev-parse --show-toplevel).Trim().Replace('/', '\')
$OutDir = Join-Path $RepoRoot 'Logs\ai'
$OutJson = Join-Path $OutDir "astra-$Spec.json"
$LastMsg = Join-Path $OutDir "astra-$Spec-last.md"
New-Item -ItemType Directory -Force $OutDir | Out-Null

function Finish([string]$result, [int]$code, [hashtable]$extra = @{}) {
    $o = [ordered]@{ spec = $Spec; model = $Model; result = $result; time = (Get-Date).ToString('s') }
    foreach ($k in $extra.Keys) { $o[$k] = $extra[$k] }
    [System.IO.File]::WriteAllText($OutJson, ($o | ConvertTo-Json -Depth 5), $Utf8NoBom)
    Write-Output "RESULT=$result"
    exit $code
}

# ── 입력·환경 확인 ──
if (-not $Worktree) { $Worktree = Join-Path (Join-Path (Split-Path $RepoRoot -Parent) 'ReturnMap-spec') $Spec }
$Worktree = [System.IO.Path]::GetFullPath($Worktree)
if (-not (Test-Path (Join-Path $Worktree '.git'))) { Write-Output "worktree 없음: $Worktree (spec-worktree.ps1 -Spec $Spec 먼저)"; Finish 'INFRA' 3 }
$branch = (Invoke-Git -C $Worktree rev-parse --abbrev-ref HEAD).Trim()
if ($branch -ne "spec/$Spec") { Write-Output "브랜치 불일치: $branch (기대 spec/$Spec)"; Finish 'INFRA' 3 }
if (-not (Test-Path $SpecFile)) { Write-Output "명세 파일 없음: $SpecFile"; Finish 'INFRA' 3 }
if ($CiLog -and -not (Test-Path $CiLog)) { Write-Output "CI 로그 없음: $CiLog"; Finish 'INFRA' 3 }
if ($ReviewFile -and -not (Test-Path $ReviewFile)) { Write-Output "리뷰 파일 없음: $ReviewFile"; Finish 'INFRA' 3 }

$dirty = @(Invoke-Git -C $Worktree status --porcelain)
if ($dirty.Count -gt 0 -and -not $AllowDirty) { Write-Output "커밋 안 된 변경이 있음 → 먼저 리뷰·커밋 또는 stash"; Finish 'DIRTY' 1 @{ dirty = $dirty } }

# ── 프롬프트 조립 (Astra 는 CLAUDE.md 를 자동으로 읽지 않으므로 직접 넣는다) ──
$claudeMd = Get-Content (Join-Path $Worktree 'CLAUDE.md') -Raw -Encoding UTF8
$specText = Get-Content $SpecFile -Raw -Encoding UTF8
$ciText = ''
if ($CiLog) {
    $ciLines = Get-Content $CiLog -Encoding UTF8
    $ciText = ($ciLines | Select-Object -Last 200) -join "`n"
}

$prompt = @"
너는 Unity 6 URP 2D 게임 Re:Trap 저장소의 구현 담당이다. 현재 작업 디렉터리가 spec/$Spec 브랜치 worktree 다.

## 절대 규칙
- 아래 [명세]에 적힌 것만 구현한다. 명세에 없는 기능·추상화·리팩터링·인접 코드 개선 금지(최소 구현, 국소 변경).
- git commit / push / branch / reset / stash 등 git 상태를 바꾸는 명령 금지. 파일 편집만 한다.
- 이 worktree 밖의 파일은 읽기만, 수정 금지. Unity 에디터를 실행하지 않는다(검증은 별도 CI 가 한다).
- 새 .cs 의 .meta 파일은 만들지 않는다(CI 가 생성). 기존 .meta·.unity·.prefab·.asset 은 명세가 요구할 때만 수정.
- 코드 컨벤션은 아래 [프로젝트 규칙]을 따른다: 네임스페이스 ReTrap, 한글 주석, [SerializeField] private + Header/Tooltip 등.
- 명세가 모호하면 추측으로 확장하지 말고, 가장 작은 해석으로 구현한 뒤 마지막 보고에 "가정:"으로 적는다.
- 명세대로는 구현할 수 없거나 새 프리팹·씬·ScriptableObject 에셋이 필요하면, 아무 파일도 고치지 말고 마지막 응답 첫 줄을 "설계로 해결 불가: <사유>" 로 쓴다.

## 마지막 응답 형식 (한국어)
변경 파일 목록 / 무엇을 왜 바꿨는지 요약 / 가정: / 확인 필요:

## 프로젝트 규칙 (CLAUDE.md)
$claudeMd

## 명세
$specText
"@
if ($ciText) {
    $prompt += @"

## 직전 CI 실패 로그 (마지막 200줄) — 이 실패를 고치는 것만 한다
$ciText
"@
}
if ($ReviewFile) {
    $reviewText = Get-Content $ReviewFile -Raw -Encoding UTF8
    $prompt += @"

## 리뷰 지적 — 현재 worktree 의 미커밋 구현 위에서 이 지적만 고친다
$reviewText
"@
}

if ($DryRun) { Write-Output $prompt; Finish 'DRYRUN' 0 }

$codex = Get-Command codex -ErrorAction SilentlyContinue
if (-not $codex) { Write-Output 'codex CLI 없음 (npm i -g @openai/codex → codex login)'; Finish 'INFRA' 3 }

# ── 실행: 프롬프트는 stdin 으로 (PS 5.1 기본 ASCII 파이프 → UTF-8 로 고정) ──
$promptFile = Join-Path $OutDir "astra-$Spec-prompt.md"
[System.IO.File]::WriteAllText($promptFile, $prompt, $Utf8NoBom)
if (Test-Path $LastMsg) { Remove-Item $LastMsg -Force }

$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = 'cmd.exe'
$psi.Arguments = "/c codex exec -m `"$Model`" -C `"$Worktree`" --sandbox workspace-write -o `"$LastMsg`" - < `"$promptFile`""
$psi.UseShellExecute = $false
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$psi.StandardOutputEncoding = [System.Text.Encoding]::UTF8
$psi.StandardErrorEncoding = [System.Text.Encoding]::UTF8
$proc = [System.Diagnostics.Process]::Start($psi)
$stdoutTask = $proc.StandardOutput.ReadToEndAsync()
$stderrTask = $proc.StandardError.ReadToEndAsync()
if (-not $proc.WaitForExit($TimeoutMinutes * 60 * 1000)) {
    & taskkill /PID $proc.Id /T /F 2>$null | Out-Null
    Finish 'FAILED' 1 @{ reason = "timeout ${TimeoutMinutes}m" }
}
$runLog = Join-Path $OutDir "astra-$Spec-run.log"
[System.IO.File]::WriteAllText($runLog, $stdoutTask.Result + "`n--- stderr ---`n" + $stderrTask.Result, $Utf8NoBom)

# ── 결과 수집: 변경 파일 + 허용 범위 밖 변경(리뷰어가 판단, 자동 원복은 안 함) ──
$changed = @(Invoke-Git -C $Worktree status --porcelain --untracked-files=all | ForEach-Object { $_.Substring(3).Trim('"') })
$allowed = '^(Assets/_Project/(Scripts|Editor|Tests|Settings)/|Assets/_Project/ResourcceEX/|Assets/StreamingAssets/Maps/|tools/ci/probes/)'
$outside = @($changed | Where-Object { $_ -notmatch $allowed })
$lastText = if (Test-Path $LastMsg) { Get-Content $LastMsg -Raw -Encoding UTF8 } else { '' }

$extra = @{ exitCode = $proc.ExitCode; changed = $changed; outsideAllowed = $outside; lastMessage = $LastMsg; runLog = $runLog }
Write-Output "CHANGED=$($changed.Count)"
if ($outside.Count -gt 0) { Write-Output "OUTSIDE_ALLOWED=$($outside -join ',')" }
if ($proc.ExitCode -ne 0) { Finish 'FAILED' 1 $extra }
if ($changed.Count -eq 0) { Finish 'NO_CHANGE' 1 $extra }
Finish 'CHANGED' 0 $extra
