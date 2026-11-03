# ═══════════════════════════════════════════════════════════════
#  gpt-image.ps1 — GPT(OpenAI Codex CLI, 내장 image_gen)로 리소스 이미지 그리기
# ═══════════════════════════════════════════════════════════════
# 역할 분담: Claude(레퍼런스·요청서·검수) → GPT(그림, 이 스크립트) → Claude(규격 검사·가져오기).
# GPT 는 -Dir 폴더 안에서만 작업한다(요청서 REQUEST.md 를 읽고 결과 PNG·README 를 남김). git 은 쓰지 않는다.
#
# 사용:
#   tools\ai\gpt-image.ps1 -Dir NEWIMAG\FxHitV4 -Expect Fx_Hit.png -Images motion_reference.png,style_reference.png
#   tools\ai\gpt-image.ps1 -Dir NEWIMAG\FxHitV4 -Expect Fx_Hit.png -Images motion_reference.png -ReviewFile NEWIMAG\FxHitV4\REVIEW-1.md   # 검수 지적 반영
#
# 전제: Codex CLI 설치·로그인, image_generation 기능 켜짐(`codex features list`). -Dir 에 REQUEST.md.
# 결과: <Dir>\_gpt\run.log · last.md + 콘솔 마지막 줄 RESULT=DONE|NO_OUTPUT|FAILED|INFRA
# exit: 0 DONE(-Expect 파일이 새로 생기거나 바뀜), 1 NO_OUTPUT·FAILED, 3 INFRA

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Dir,
    [Parameter(Mandatory = $true)][ValidatePattern('^[A-Za-z0-9_]+\.png$')][string]$Expect,
    [string[]]$Images = @(),
    [string]$ReviewFile = '',
    [string]$Model = '',
    [int]$TimeoutMinutes = 20
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$Utf8NoBom = New-Object System.Text.UTF8Encoding $false

function Finish([string]$result, [int]$code, [string]$msg = '') {
    if ($msg) { Write-Output $msg }
    Write-Output "RESULT=$result"
    exit $code
}

# ── 입력·환경 확인 ──
$Dir = [System.IO.Path]::GetFullPath($Dir)
if (-not (Test-Path (Join-Path $Dir 'REQUEST.md'))) { Finish 'INFRA' 3 "요청서 없음: $Dir\REQUEST.md" }
if ($Dir -notmatch '\\NEWIMAG\\[^\\]+$') { Finish 'INFRA' 3 "작업 폴더는 NEWIMAG\<묶음> 이어야 합니다: $Dir" }
# PS 5.1 은 -Images a,b 를 한 문자열로 넘기기도 한다
$Images = @($Images | ForEach-Object { $_ -split ',' } | Where-Object { $_ } | ForEach-Object { Join-Path $Dir $_.Trim() })
foreach ($img in $Images) { if (-not (Test-Path $img)) { Finish 'INFRA' 3 "참고 이미지 없음: $img" } }

$codexPath = (Get-Command codex.cmd -ErrorAction SilentlyContinue).Source
if (-not $codexPath) { $codexPath = Join-Path $env:APPDATA 'npm\codex.cmd' }
if (-not (Test-Path $codexPath)) { Finish 'INFRA' 3 'codex CLI 없음 (npm i -g @openai/codex → codex login)' }

$work = Join-Path $Dir '_gpt'
New-Item -ItemType Directory -Force $work | Out-Null
$LastMsg = Join-Path $work 'last.md'
if (Test-Path $LastMsg) { Remove-Item $LastMsg -Force }
$expectPath = Join-Path $Dir $Expect
$before = if (Test-Path $expectPath) { (Get-FileHash $expectPath).Hash } else { '' }

# ── 프롬프트 ──
$names = ($Images | ForEach-Object { Split-Path $_ -Leaf }) -join ', '
$reviewText = ''
if ($ReviewFile) {
    $reviewText = "`n## 검수 지적 (이것만 고쳐서 다시 만드세요 — 지적 밖의 좋은 부분은 유지)`n" + (Get-Content $ReviewFile -Raw -Encoding UTF8)
}
$prompt = @"
REQUEST.md 를 읽고 그 요청을 그대로 수행하세요. 결과 시트 파일 이름은 $Expect 입니다.
첨부 이미지는 이 폴더의 파일입니다: $names (각 파일의 용도는 REQUEST.md 에 적혀 있음).
규칙: 이 폴더 밖의 파일을 만들거나 고치지 마세요. git 을 쓰지 마세요. 그림은 내장 image_gen 으로 새로 그리고,
후처리는 REQUEST.md 가 허용한 것만 하세요. 작업 파일은 _gpt\ 아래에 두세요. 끝나면 README.md 에 제작 과정과 자체 검사 결과를 적으세요.
$reviewText
"@
$promptFile = Join-Path $work 'prompt.md'
[System.IO.File]::WriteAllText($promptFile, $prompt, $Utf8NoBom)

# ── 실행: 프롬프트는 stdin 으로 ──
$imgArgs = ($Images | ForEach-Object { "-i `"$_`"" }) -join ' '
$modelArg = if ($Model) { "-m `"$Model`" " } else { '' }
$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = 'cmd.exe'
$psi.Arguments = "/s /c `"`"$codexPath`" exec $modelArg-C `"$Dir`" --sandbox workspace-write --skip-git-repo-check $imgArgs -o `"$LastMsg`" - < `"$promptFile`"`""
$psi.UseShellExecute = $false
# codex.cmd 는 node 를 PATH 에서 찾는다 → 레지스트리의 최신 PATH 를 앞에 붙여 넘긴다
$psi.EnvironmentVariables['PATH'] = @(
    [Environment]::GetEnvironmentVariable('Path', 'Machine'),
    [Environment]::GetEnvironmentVariable('Path', 'User'),
    $env:Path) -join ';'
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$psi.StandardOutputEncoding = [System.Text.Encoding]::UTF8
$psi.StandardErrorEncoding = [System.Text.Encoding]::UTF8
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$proc = [System.Diagnostics.Process]::Start($psi)
$stdoutTask = $proc.StandardOutput.ReadToEndAsync()
$stderrTask = $proc.StandardError.ReadToEndAsync()
if (-not $proc.WaitForExit($TimeoutMinutes * 60 * 1000)) {
    & taskkill /PID $proc.Id /T /F 2>$null | Out-Null
    Finish 'FAILED' 1 "timeout ${TimeoutMinutes}m"
}
$all = $stdoutTask.Result + "`n--- stderr ---`n" + $stderrTask.Result
[System.IO.File]::WriteAllText((Join-Path $work 'run.log'), $all, $Utf8NoBom)

$tokens = -1
$m = [regex]::Match($all, 'tokens used\s+([\d,]+)')
if ($m.Success) { $tokens = [int]($m.Groups[1].Value -replace ',', '') }
Write-Output "TOKENS=$tokens"
Write-Output "DURATION_SEC=$([int]$sw.Elapsed.TotalSeconds)"

if ($proc.ExitCode -ne 0) { Finish 'FAILED' 1 "codex exit $($proc.ExitCode) — $work\run.log" }
$after = if (Test-Path $expectPath) { (Get-FileHash $expectPath).Hash } else { '' }
if (-not $after -or $after -eq $before) { Finish 'NO_OUTPUT' 1 "$Expect 가 만들어지거나 바뀌지 않음 — $LastMsg" }
Write-Output "OUTPUT=$expectPath"
Finish 'DONE' 0
