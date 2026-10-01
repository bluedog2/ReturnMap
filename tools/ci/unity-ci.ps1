# ═══════════════════════════════════════════════════════════════
#  unity-ci.ps1 — Unity 배치모드 CI 실행 + 결과 요약
# ═══════════════════════════════════════════════════════════════
# 1) 검증할 커밋 결정
#      -Ref 미지정: 메인 작업트리 스냅샷(미커밋·신규 파일 포함, .gitignore 준수)
#                   → 임시 인덱스로 커밋 객체만 만든다. 메인 작업트리/인덱스는 건드리지 않음
#      -Ref 지정  : 해당 브랜치/커밋 (예: spec/SPEC-9)
# 2) CI worktree 를 그 커밋으로 강제 체크아웃 (+ worktree 안의 미추적 파일 정리)
# 3) Unity -batchmode -executeMethod ReTrap.EditorTools.CIRunner.RunAll
# 4) (-Tests) EditMode 테스트 실행
# 5) 로그 + ci-result.json 파싱 → Logs\ci\ci-summary.json 과 콘솔 요약
#
# 종료 코드: 0 PASS · 1 FAIL(검사 오류/테스트 실패) · 2 COMPILE_ERROR · 3 INFRA_ERROR
#
# 사용:
#   powershell -ExecutionPolicy Bypass -File tools\ci\unity-ci.ps1
#   powershell -ExecutionPolicy Bypass -File tools\ci\unity-ci.ps1 -Ref spec/SPEC-9 -Tests

[CmdletBinding()]
param(
    [string]$Ref = '',
    [string]$CiPath = '',
    [string]$UnityPath = '',
    [string]$OutDir = '',
    [int]$TimeoutMinutes = 40,
    [switch]$Tests
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$Utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$sw = [System.Diagnostics.Stopwatch]::StartNew()

# PS 5.1 은 리디렉션된 네이티브 stderr(예: git 의 CRLF 경고)를 예외로 바꾼다.
# git 호출은 이 헬퍼로 감싸 stderr 를 버리고 $LASTEXITCODE 로만 판정한다.
function Invoke-Git {
    $prev = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { & git @args 2>$null } finally { $ErrorActionPreference = $prev }
}

$RepoRoot = (Invoke-Git -C $PSScriptRoot rev-parse --show-toplevel).Trim().Replace('/', '\')
if (-not $CiPath) { $CiPath = Join-Path (Split-Path $RepoRoot -Parent) 'ReturnMap-ci' }
if (-not $OutDir) { $OutDir = Join-Path $RepoRoot 'Logs\ci' }
# Unity 는 상대경로를 자기 프로젝트(CI worktree) 기준으로 해석하므로 반드시 절대경로로 넘긴다
$OutDir = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutDir)
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$summary = [ordered]@{
    result        = 'INFRA_ERROR'
    exitCode      = 3
    source        = $(if ($Ref) { "ref:$Ref" } else { 'working-tree' })
    commit        = ''
    startedAt     = (Get-Date).ToString('o')
    durationSec   = 0
    message       = ''
    compileErrors = @()
    checks        = @()
    errorCount    = 0
    warningCount  = 0
    tests         = $null
    logFile       = ''
}

function Finish([string]$result, [int]$code, [string]$message) {
    $summary.result = $result
    $summary.exitCode = $code
    if ($message) { $summary.message = $message }
    $summary.durationSec = [math]::Round($sw.Elapsed.TotalSeconds, 1)
    $json = $summary | ConvertTo-Json -Depth 8
    [System.IO.File]::WriteAllText((Join-Path $OutDir 'ci-summary.json'), $json, $Utf8NoBom)

    Write-Host ''
    Write-Host '──────── Unity CI 요약 ────────'
    Write-Host ("결과     : {0} (exit {1})" -f $result, $code)
    Write-Host ("대상     : {0} @ {1}" -f $summary.source, $summary.commit)
    Write-Host ("소요     : {0}s" -f $summary.durationSec)
    if ($summary.message) { Write-Host ("메시지   : {0}" -f $summary.message) }
    foreach ($e in $summary.compileErrors) { Write-Host "  [컴파일] $e" }
    foreach ($c in $summary.checks) {
        Write-Host ("  {0,-16} {1,-4} err {2} / warn {3}" -f $c.name, $c.status, @($c.errors).Count, @($c.warnings).Count)
        foreach ($e in @($c.errors) | Select-Object -First 5) { Write-Host "      ✗ $e" }
    }
    if ($summary.tests) {
        $t = $summary.tests
        Write-Host ("  tests(EditMode)  total {0} / passed {1} / failed {2}" -f $t.total, $t.passed, $t.failed)
        foreach ($f in @($t.failures) | Select-Object -First 5) { Write-Host "      ✗ $($f.name): $($f.message)" }
    }
    Write-Host ("요약 파일: {0}" -f (Join-Path $OutDir 'ci-summary.json'))
    Write-Host '───────────────────────────────'
    exit $code
}

try {
    # ── Unity 경로 ────────────────────────────────────────────
    if (-not $UnityPath) {
        $verLine = Get-Content (Join-Path $RepoRoot 'ProjectSettings\ProjectVersion.txt') | Where-Object { $_ -like 'm_EditorVersion:*' } | Select-Object -First 1
        $ver = $verLine.Split(':')[1].Trim()
        $UnityPath = "C:\Program Files\Unity\Hub\Editor\$ver\Editor\Unity.exe"
    }
    if (-not (Test-Path $UnityPath)) { Finish 'INFRA_ERROR' 3 "Unity 실행 파일 없음: $UnityPath" }

    # ── CI worktree 준비 ──────────────────────────────────────
    if (-not (Test-Path (Join-Path $CiPath '.git'))) {
        Write-Host "[ci] CI worktree 없음 → 생성"
        & (Join-Path $PSScriptRoot 'setup-ci-worktree.ps1') -CiPath $CiPath
    }

    # ── 잠금 확인: CI 프로젝트가 다른 Unity 에서 열려 있으면 중단 ──
    $lockFile = Join-Path $CiPath 'Temp\UnityLockfile'
    if (Test-Path $lockFile) {
        try {
            $fs = [System.IO.File]::Open($lockFile, 'Open', 'ReadWrite', 'None'); $fs.Close()
        } catch {
            Finish 'INFRA_ERROR' 3 "CI 프로젝트($CiPath)가 다른 Unity 인스턴스에서 열려 있음"
        }
    }

    # ── 검증할 커밋 결정 ──────────────────────────────────────
    if ($Ref) {
        $sha = (Invoke-Git -C $RepoRoot rev-parse --verify "$Ref^{commit}")
        if ($LASTEXITCODE -ne 0 -or -not $sha) { Finish 'INFRA_ERROR' 3 "Ref 를 찾을 수 없음: $Ref" }
        $sha = $sha.Trim()
    } else {
        # 임시 인덱스에 작업트리 전체를 add → tree → 커밋 객체 (메인 인덱스 불변)
        $realIndex = (Invoke-Git -C $RepoRoot rev-parse --path-format=absolute --git-path index).Trim()
        $tmpIndex = Join-Path ([System.IO.Path]::GetTempPath()) ("retrap-ci-index-" + [guid]::NewGuid().ToString('N'))
        if (Test-Path $realIndex) { Copy-Item $realIndex $tmpIndex }
        $prevIndexEnv = $env:GIT_INDEX_FILE
        try {
            $env:GIT_INDEX_FILE = $tmpIndex
            Invoke-Git -C $RepoRoot add -A | Out-Null
            if ($LASTEXITCODE -ne 0) { throw "git add -A (임시 인덱스) 실패" }
            $tree = (Invoke-Git -C $RepoRoot write-tree).Trim()
        } finally {
            $env:GIT_INDEX_FILE = $prevIndexEnv
            Remove-Item $tmpIndex -ErrorAction SilentlyContinue
        }
        $head = (Invoke-Git -C $RepoRoot rev-parse HEAD).Trim()
        $headTree = (Invoke-Git -C $RepoRoot rev-parse "$head^{tree}").Trim()
        if ($tree -eq $headTree) {
            $sha = $head
        } else {
            $sha = (Invoke-Git -C $RepoRoot commit-tree $tree -p $head -m "ci snapshot $(Get-Date -Format s)").Trim()
        }
    }
    $summary.commit = $sha

    # ── CI worktree 체크아웃 (worktree 전용이므로 강제) ─────────
    Invoke-Git -C $CiPath checkout --force --detach $sha | Out-Null
    if ($LASTEXITCODE -ne 0) { Finish 'INFRA_ERROR' 3 "CI worktree 체크아웃 실패: $sha" }
    Invoke-Git -C $CiPath clean -fd -q | Out-Null   # 미추적 파일만 삭제, .gitignore 대상(Library 등)은 유지

    # ── Unity 실행 헬퍼 ───────────────────────────────────────
    function Invoke-Unity([string[]]$extraArgs, [string]$logPath) {
        $all = @('-batchmode', '-nographics', '-projectPath', $CiPath, '-logFile', $logPath) + $extraArgs
        $argLine = ($all | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } }) -join ' '
        Write-Host "[ci] Unity 실행: $argLine"
        $p = Start-Process -FilePath $UnityPath -ArgumentList $argLine -PassThru -WindowStyle Hidden
        if (-not $p.WaitForExit($TimeoutMinutes * 60 * 1000)) {
            try { $p.Kill() } catch {}
            return -999
        }
        return $p.ExitCode
    }

    function Get-LogIssues([string]$logPath) {
        $r = @{ compile = @(); license = $false }
        if (-not (Test-Path $logPath)) { return $r }
        $lines = Get-Content $logPath -Encoding UTF8
        $r.compile = @($lines | Where-Object { $_ -match 'error CS\d{4}' } | ForEach-Object { $_.Trim() } | Select-Object -Unique | Select-Object -First 30)
        $r.license = [bool]($lines | Where-Object { $_ -match 'No valid Unity Editor license|License is not active|LicensingClient has failed' } | Select-Object -First 1)
        return $r
    }

    # ── 1차: CIRunner ────────────────────────────────────────
    $log = Join-Path $OutDir 'unity-ci.log'
    $resultJson = Join-Path $OutDir 'ci-result.json'
    Remove-Item $resultJson, $log -ErrorAction SilentlyContinue
    $summary.logFile = $log

    $code = Invoke-Unity @('-quit', '-executeMethod', 'ReTrap.EditorTools.CIRunner.RunAll', '-ciOutput', $resultJson) $log
    $issues = Get-LogIssues $log
    $summary.compileErrors = $issues.compile

    if ($code -eq -999) { Finish 'INFRA_ERROR' 3 "Unity 타임아웃 (${TimeoutMinutes}분)" }
    if ($issues.compile.Count -gt 0) { Finish 'COMPILE_ERROR' 2 "컴파일 오류 $($issues.compile.Count)건" }
    if (-not (Test-Path $resultJson)) {
        # 라이선스 문구는 정상 실행 로그에도 섞여 나오므로("...failed validation; ignoring") 결과가 없을 때만 원인으로 본다
        if ($issues.license) { Finish 'INFRA_ERROR' 3 "Unity 라이선스 오류 의심 — Hub 로그인/활성화 확인 (로그: $log)" }
        Finish 'INFRA_ERROR' 3 "ci-result.json 미생성 (Unity exit $code) — 로그 확인: $log"
    }

    $report = Get-Content $resultJson -Raw -Encoding UTF8 | ConvertFrom-Json
    $summary.checks = @($report.checks)
    $summary.errorCount = [int]$report.errorCount
    $summary.warningCount = [int]$report.warningCount
    if ($report.exception) { Finish 'INFRA_ERROR' 3 "CIRunner 예외: $($report.exception)" }

    $failed = -not [bool]$report.success

    # ── 2차(선택): EditMode 테스트 ──────────────────────────────
    if ($Tests) {
        $testLog = Join-Path $OutDir 'unity-tests.log'
        $testXml = Join-Path $OutDir 'editmode-results.xml'
        Remove-Item $testXml, $testLog -ErrorAction SilentlyContinue
        # -runTests 에는 -quit 를 붙이지 않는다 (테스트 전에 종료될 수 있음)
        $tcode = Invoke-Unity @('-runTests', '-testPlatform', 'EditMode', '-testResults', $testXml) $testLog
        if ($tcode -eq -999) { Finish 'INFRA_ERROR' 3 "테스트 타임아웃 (${TimeoutMinutes}분)" }
        if (Test-Path $testXml) {
            [xml]$x = Get-Content $testXml -Raw -Encoding UTF8
            $run = $x.'test-run'
            $failures = @($x.SelectNodes("//test-case[@result='Failed']") | ForEach-Object {
                [ordered]@{ name = $_.fullname; message = ($_.failure.message.'#cdata-section' -as [string]).Trim() }
            } | Select-Object -First 30)
            $summary.tests = [ordered]@{
                total = [int]$run.total; passed = [int]$run.passed; failed = [int]$run.failed
                skipped = [int]$run.skipped; failures = $failures
            }
            if ([int]$run.failed -gt 0) { $failed = $true }
        } else {
            $summary.tests = [ordered]@{ total = 0; passed = 0; failed = 0; skipped = 0; failures = @(); note = "결과 XML 미생성 (exit $tcode) — 테스트 어셈블리가 없을 수 있음" }
        }
    }

    if ($failed) { Finish 'FAIL' 1 '' } else { Finish 'PASS' 0 '' }
}
catch {
    Finish 'INFRA_ERROR' 3 ("스크립트 예외: " + $_.Exception.Message)
}
