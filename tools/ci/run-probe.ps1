# ═══════════════════════════════════════════════════════════════
#  run-probe.ps1 — 기획별 Play 검증 프로브를 CI worktree 에서 실행
# ═══════════════════════════════════════════════════════════════
# spec-cycle 구현 트랙 ⑬ 에서 CI PASS 후 사용한다.
# 1) 검증할 ref(예: spec/SPEC-9) 의 트리에 프로브 .cs 하나만 얹은 임시 커밋 객체를 만든다
#    (어느 브랜치에도 커밋하지 않음 — 메인/spec 작업트리·인덱스 불변)
# 2) CI worktree(..\ReturnMap-ci) 를 그 커밋으로 강제 체크아웃
# 3) Unity 배치모드로 -executeMethod <프로브>.Run 실행 → 프로브가 Play 모드 진입·측정 후 결과 txt 기록·종료
#    (-nographics 를 쓰지 않는다: UI Canvas·카메라 렌더 PNG 가 필요)
# 4) 결과 txt 첫 줄 "RESULT: PASS|FAIL" 로 판정
#
# 종료 코드: 0 PASS · 1 FAIL · 3 INFRA_ERROR(잠금·타임아웃·결과 미생성)
#
# 사용:
#   powershell -ExecutionPolicy Bypass -File tools\ci\run-probe.ps1 -Ref spec/SPEC-12 -Probe tools\ci\probes\SpecProbe12.cs
#   powershell ... run-probe.ps1 -Ref spec/SPEC-12 -Probe "spec/SPEC-12:tools/ci/probes/SpecProbe12.cs"
#       ↑ 권장: 작업 파일이 아니라 **커밋된** 프로브(git 객체)를 실행 — 검증한 내용과 리포트가 일치
#   (클래스명은 파일명과 같아야 하고, ReTrap.EditorTools 네임스페이스의 public static Run() 을 가져야 한다)
#   결과 txt 의 "CONSOLE ERRORS" 가 비어 있지 않으면 프로브 자체가 FAIL 을 내야 한다(probes/README 규칙).

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Ref,
    [Parameter(Mandatory = $true)][string]$Probe,
    [string]$CiPath = '',
    [string]$OutDir = '',
    [string]$UnityPath = '',
    [int]$TimeoutMinutes = 10
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

function Invoke-Git {
    $prev = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { & git @args 2>$null } finally { $ErrorActionPreference = $prev }
}

$RepoRoot = (Invoke-Git -C $PSScriptRoot rev-parse --show-toplevel).Trim().Replace('/', '\')
if (-not $CiPath) { $CiPath = Join-Path (Split-Path $RepoRoot -Parent) 'ReturnMap-ci' }
# -Probe 가 "<ref>:<경로>" 면 커밋된 git 객체를 쓴다 (드라이브 문자 C:\ 는 제외)
$probeBlob = $null
if ($Probe -match '^(?![A-Za-z]:[\\/])[^:]+:.+$') {
    $probeBlob = (Invoke-Git -C $RepoRoot rev-parse --verify --quiet "$Probe")
    if ($LASTEXITCODE -ne 0 -or -not $probeBlob) { Write-Host "[probe] git 객체 없음: $Probe (프로브를 먼저 커밋할 것)"; exit 3 }
    $probeBlob = $probeBlob.Trim()
    $Class = [System.IO.Path]::GetFileNameWithoutExtension(($Probe -split ':', 2)[1])
} else {
    $Probe = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Probe)
    if (-not (Test-Path $Probe)) { Write-Host "[probe] 프로브 파일 없음: $Probe"; exit 3 }
    $Class = [System.IO.Path]::GetFileNameWithoutExtension($Probe)
}
$specTag = ($Ref -replace '^spec/', '')
if (-not $OutDir) { $OutDir = Join-Path $RepoRoot "Logs\ci-spec\$specTag" }
$OutDir = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutDir)
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$resultTxt = Join-Path $OutDir "$Class.txt"
$logFile = Join-Path $OutDir "$Class.log"
Remove-Item $resultTxt, $logFile -ErrorAction SilentlyContinue

if (-not $UnityPath) {
    $verLine = Get-Content (Join-Path $RepoRoot 'ProjectSettings\ProjectVersion.txt') | Where-Object { $_ -like 'm_EditorVersion:*' } | Select-Object -First 1
    $UnityPath = "C:\Program Files\Unity\Hub\Editor\$($verLine.Split(':')[1].Trim())\Editor\Unity.exe"
}
if (-not (Test-Path $UnityPath)) { Write-Host "[probe] Unity 없음: $UnityPath"; exit 3 }
if (-not (Test-Path (Join-Path $CiPath '.git'))) { Write-Host "[probe] CI worktree 없음 — 먼저 unity-ci.ps1 을 한 번 실행할 것"; exit 3 }

$lockFile = Join-Path $CiPath 'Temp\UnityLockfile'
if (Test-Path $lockFile) {
    try { $fs = [System.IO.File]::Open($lockFile, 'Open', 'ReadWrite', 'None'); $fs.Close() }
    catch { Write-Host "[probe] CI 프로젝트가 다른 Unity 에서 열려 있음"; exit 3 }
}

# ── 임시 커밋: ref 트리 + 프로브 1개 (임시 인덱스, 실제 인덱스 불변) ──
$base = (Invoke-Git -C $RepoRoot rev-parse --verify "$Ref^{commit}")
if ($LASTEXITCODE -ne 0 -or -not $base) { Write-Host "[probe] ref 없음: $Ref"; exit 3 }
$base = $base.Trim()
$tmpIndex = Join-Path ([System.IO.Path]::GetTempPath()) ("retrap-probe-index-" + [guid]::NewGuid().ToString('N'))
$prevIdx = $env:GIT_INDEX_FILE
try {
    $env:GIT_INDEX_FILE = $tmpIndex
    Invoke-Git -C $RepoRoot read-tree $base | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "read-tree 실패" }
    if ($probeBlob) { $blob = $probeBlob } else {
        $blob = (Invoke-Git -C $RepoRoot hash-object -w $Probe)
        if ($LASTEXITCODE -ne 0 -or -not $blob) { throw "hash-object 실패" }
        $blob = $blob.Trim()
    }
    Invoke-Git -C $RepoRoot update-index --add --cacheinfo "100644,$blob,Assets/_Project/Editor/CI/$Class.cs" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "update-index 실패" }
    $tree = (Invoke-Git -C $RepoRoot write-tree)
    if ($LASTEXITCODE -ne 0 -or -not $tree) { throw "write-tree 실패" }
    $tree = $tree.Trim()
} catch {
    Write-Host "[probe] 임시 커밋 생성 실패: $($_.Exception.Message)"; exit 3
} finally {
    $env:GIT_INDEX_FILE = $prevIdx
    Remove-Item $tmpIndex -ErrorAction SilentlyContinue
}
$sha = (Invoke-Git -C $RepoRoot commit-tree $tree -p $base -m "probe $Class on $Ref (temp)")
if ($LASTEXITCODE -ne 0 -or -not $sha) { Write-Host "[probe] commit-tree 실패"; exit 3 }
$sha = $sha.Trim()

Invoke-Git -C $CiPath checkout --force --detach $sha | Out-Null
if ($LASTEXITCODE -ne 0) { Write-Host "[probe] CI worktree 체크아웃 실패"; exit 3 }
Invoke-Git -C $CiPath clean -fd -q | Out-Null

# ── Unity 실행 ────────────────────────────────────────────────
$argLine = "-batchmode -projectPath `"$CiPath`" -logFile `"$logFile`" -executeMethod ReTrap.EditorTools.$Class.Run -probeOutput `"$resultTxt`""
Write-Host "[probe] $Class @ $Ref ($($sha.Substring(0,7)))"
$p = Start-Process -FilePath $UnityPath -ArgumentList $argLine -PassThru -WindowStyle Hidden
if (-not $p.WaitForExit($TimeoutMinutes * 60 * 1000)) {
    & taskkill /T /F /PID $p.Id 2>$null | Out-Null   # 하위 Unity 워커까지 종료
    Write-Host "[probe] 타임아웃 (${TimeoutMinutes}분)"; exit 3
}

if (-not (Test-Path $resultTxt)) {
    Write-Host "[probe] 결과 미생성 (Unity exit $($p.ExitCode)) — 로그: $logFile"
    Select-String -Path $logFile -Pattern 'error CS\d{4}' | Select-Object -First 10 | ForEach-Object { Write-Host "  $($_.Line.Trim())" }
    exit 3
}
Get-Content $resultTxt -Encoding UTF8 | ForEach-Object { Write-Host $_ }
Write-Host "[probe] 결과: $resultTxt (스크린샷 PNG 는 같은 폴더)"
$first = (Get-Content $resultTxt -Encoding UTF8 -TotalCount 1)
if ($first -like 'RESULT: PASS*') { exit 0 } else { exit 1 }
