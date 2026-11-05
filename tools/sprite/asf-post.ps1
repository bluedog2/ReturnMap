# ═══════════════════════════════════════════════════════════════
#  asf-post.ps1 — GPT 원본 캐릭터 시트 → ASF 후처리 → Px32 규격 시트 (한 줄 실행)
# ═══════════════════════════════════════════════════════════════
# 순서: ① sheet_qc spill(칸 넘침) → ② scale_frames(크기 통일·몸통 X 고정·발 고정)
#       → ③ palette_tool apply(팔레트 잠금·이진 알파) → ④ asf-to-px32.py(배치·시트·CI 규칙 미리 검사)
# 중간 산출물은 ASF\<이름>-<시각>\ (gitignore). 시트는 -Out 폴더(보통 NEWIMAG\<묶음>)에만 쓴다 — Assets 는 건드리지 않는다.
# 숫자 통과 ≠ 승인: 결과 미리보기(<이름>_Preview_4x.png)를 눈으로 본 뒤에 Sprites/Px32 로 옮긴다.
#
# 사용:
#   tools\sprite\asf-post.ps1 -Raw NEWIMAG\Px32HeroV2\Sources\Hero_Run_attempt2.png -Name Hero_Run -Rows 2 -Cols 4 -Out ASF\out
#   (-Count 5: 칸 중 앞 5개만 / -Key green|magenta|blue|none / -BodyPx 102 / -Place ground-center|ground|asf)
# 마지막 줄 RESULT=PASS|FAIL|INFRA, exit 0 / 1 / 3
#   FAIL 의 흔한 원인: 높이 드리프트(GPT 가 프레임마다 몸 길이를 다르게 그림) · 셀 밖 → GPT 재요청 근거로 REVIEW 에 그대로 옮긴다.

[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$Raw,
    [Parameter(Mandatory)] [string]$Name,
    [Parameter(Mandatory)] [int]$Rows,
    [Parameter(Mandatory)] [int]$Cols,
    [Parameter(Mandatory)] [string]$Out,
    [int]$Count = 0,
    [string]$Key = 'green',
    [int]$BodyPx = 102,
    [int]$Cell = 128,
    [ValidateSet('ground-center', 'ground', 'asf')] [string]$Place = 'ground-center',
    [string]$Palette = 'ASF\palette_hero20.hex',
    [string]$CheckPalette = 'NEWIMAG\palette_v2.txt'
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $repoRoot
$env:PYTHONUTF8 = '1'

function Done([string]$result, [int]$code, [string]$msg) {
    if ($msg) { Write-Output $msg }
    Write-Output "RESULT=$result"; exit $code
}

# ── 준비 확인 ──
$lockPath = Join-Path $PSScriptRoot 'asf.lock.json'
$lock = Get-Content $lockPath -Raw -Encoding UTF8 | ConvertFrom-Json
& powershell -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'asf-setup.ps1') -Check | Out-Null
if ($LASTEXITCODE -ne 0) { Done 'INFRA' 3 'ASF 체크아웃이 없거나 고정 커밋이 아님 → tools\sprite\asf-setup.ps1 실행' }
if (-not (Test-Path $Palette)) { Done 'INFRA' 3 "팔레트 없음: $Palette → tools\sprite\asf-setup.ps1 실행" }
if (-not (Test-Path $Raw)) { Done 'FAIL' 1 "원본 없음: $Raw" }

$py      = "-$($lock.python)"
$scripts = Join-Path ([IO.Path]::GetFullPath((Join-Path $repoRoot $lock.checkout))) 'skills\generate2dsprite\scripts'
$frames  = if ($Count -gt 0) { $Count } else { $Rows * $Cols }
$work    = Join-Path $repoRoot ("ASF\{0}-{1}" -f $Name, (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Force $work | Out-Null
Write-Output "WORK=$work"

# Python 한 단계 실행: stdout → <label>.log, stderr → <label>.err (UTF-8), 종료 코드만 반환
function Step([string]$label, [string[]]$pyArgs) {
    $log = Join-Path $work "$label.log"; $err = Join-Path $work "$label.err"
    # py 런처는 따옴표 친 버전 옵션("-3.12")을 못 읽으므로 앞 두 개는 그대로
    $argLine = "$py -I " + (($pyArgs | ForEach-Object { '"' + ($_ -replace '"', '\"') + '"' }) -join ' ')
    $p = Start-Process py -ArgumentList $argLine -NoNewWindow -Wait -PassThru `
            -RedirectStandardOutput $log -RedirectStandardError $err
    # ASF 는 임시 폴더를 만든 뒤 이름을 바꿔 게시한다 — Windows 가 순간 잠그면 WinError 5. 한 번만 다시 시도
    if ($p.ExitCode -ne 0 -and (Select-String -Path $log, $err -Pattern 'WinError 5' -Quiet)) {
        Get-ChildItem $work -Directory -Filter ".$label.stage-*" | Remove-Item -Recurse -Force
        Start-Sleep -Seconds 2
        $p = Start-Process py -ArgumentList $argLine -NoNewWindow -Wait -PassThru `
                -RedirectStandardOutput $log -RedirectStandardError $err
    }
    $last = @(Get-Content $log, $err -Encoding UTF8 | Where-Object { $_ }) | Select-Object -Last 1
    Write-Host "[$label] exit $($p.ExitCode)  $last"
    return $p.ExitCode
}

$anchorX = [int]($Cell / 2); $anchorY = $Cell - 1
$keyArgs = if ($Key -eq 'none') { @() } else { @('--key', $Key) }
$countArgs = if ($Count -gt 0) { @('--count', "$Count") } else { @() }

# ① 칸 넘침 (warn 은 진행, error 는 중단)
$c = Step 'spill' (@("$scripts\sheet_qc.py", 'spill', '--input', $Raw, '--rows', "$Rows", '--cols', "$Cols") + $keyArgs + @('--output-dir', "$work\spill"))
if ($c -ne 0) { Done 'FAIL' 1 "칸 넘침 검사 실패 — $work\spill.log (칸끼리 겹치면 GPT 재요청: 여백 더)" }

# ② 크기·정렬 (몸통 X 고정 + 발 고정, 셀 밖이면 ASF 가 실패 — 자르지 않음)
$c = Step 'scale' (@("$scripts\scale_frames.py", '--sheet', $Raw, '--rows', "$Rows", '--cols', "$Cols") + $countArgs + $keyArgs + @(
    '--target-body-px', "$BodyPx", '--resampler', 'box', '--anchor', 'feet', '--root-lock', 'torso-x', '--lock', 'feet',
    '--canvas', "$Cell,$Cell", '--anchor-px', "$anchorX,$anchorY", '--output-dir', "$work\scale"))
if ($c -ne 0) { Done 'FAIL' 1 "크기·정렬 실패 — $work\scale.log (셀 밖이면 -BodyPx 를 줄이지 말고 GPT 재요청 또는 칸 여백 확인)" }

# ③ 팔레트 잠금
$c = Step 'palette' @("$scripts\palette_tool.py", 'apply', '--palette', $Palette, '--input', "$work\scale\frames", '--orphans', '1', '--output-dir', "$work\palette")
if ($c -ne 0) { Done 'FAIL' 1 "팔레트 잠금 실패 — $work\palette.log" }

# ④ 규격 시트 + CI 규칙 미리 검사
$settings = [ordered]@{ raw = $Raw; rows = $Rows; cols = $Cols; count = $Count; key = $Key; bodyPx = $BodyPx; cell = $Cell
                        anchorPx = "$anchorX,$anchorY"; rootLock = 'torso-x'; lock = 'feet'; palette = $Palette; place = $Place; work = $work }
$settingsPath = Join-Path $work 'settings.json'
[IO.File]::WriteAllText($settingsPath, ($settings | ConvertTo-Json), (New-Object Text.UTF8Encoding $false))
$c = Step 'adapter' @((Join-Path $PSScriptRoot 'asf-to-px32.py'), '--frames', "$work\palette", '--name', $Name, '--expect-frames', "$frames",
    '--palette', $CheckPalette, '--out', $Out, '--cell', "$Cell", '--place', $Place, '--source', $Raw, '--lock-file', $lockPath, '--settings', $settingsPath)
Get-Content (Join-Path $work 'adapter.log'), (Join-Path $work 'adapter.err') -Encoding UTF8 | Where-Object { $_ -and $_ -notmatch '^RESULT=' }
if ($c -eq 0) { Done 'PASS' 0 "검수: $Out\${Name}_Preview_4x.png 를 눈으로 확인 후 Sprites/Px32 로" }
Done 'FAIL' 1 "규칙 미달 — 위 FAIL 줄을 GPT 재요청 REVIEW 에 옮긴다 (출처·수치: $Out\$Name.asf.json)"
