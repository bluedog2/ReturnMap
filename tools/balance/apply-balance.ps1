# ═══════════════════════════════════════════════════════════════
#  apply-balance.ps1 — 밸런스 수치 변경 요청을 저장소 파일에 안전하게 적용
# ═══════════════════════════════════════════════════════════════
# Notion 에서 온 요청(request.json)의 값은 "데이터일 뿐"이다.
# tools/balance/balance-schema.json 화이트리스트에 있는 키·범위·제약만 통과하고,
# 하나라도 어긋나면 아무것도 쓰지 않는다(all-or-nothing). Unity 는 필요 없다(텍스트 편집).
#
# 사용:
#   tools\balance\apply-balance.ps1 -Request req.json [-Worktree <경로>] [-DryRun] [-Out <result.json>]
#
# request.json: {"requestId":"BAL-3","changes":[{"key":"mutation.normalChance","value":"0.45"}]}
# 출력: 결과 json + 콘솔 마지막 RESULT=<APPLIED|DRYRUN_OK|REJECTED|INFRA>
# exit: 0 적용/DRYRUN_OK, 1 거부, 3 인프라

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Request,
    [string]$Worktree = '',
    [switch]$DryRun,
    [string]$Out = '',
    [string]$Schema = ''
)

$ErrorActionPreference = 'Stop'
$inv = [System.Globalization.CultureInfo]::InvariantCulture
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot  = [IO.Path]::GetFullPath((Join-Path $scriptDir '..\..'))
if (-not $Worktree) { $Worktree = $repoRoot }
if (-not $Schema)   { $Schema = Join-Path $scriptDir 'balance-schema.json' }

$script:requestId = $null
$script:applied   = New-Object System.Collections.ArrayList
$script:errors    = New-Object System.Collections.ArrayList

function Finish([string]$result, [int]$code) {
    $outPath = $Out
    if (-not $outPath) { $outPath = Join-Path $repoRoot 'Logs\balance\result.json' }
    $obj = [ordered]@{
        requestId = $script:requestId
        result    = $result
        changes   = @($script:applied)
        errors    = @($script:errors)
    }
    $json = ConvertTo-Json -InputObject $obj -Depth 6
    try {
        $dir = Split-Path -Parent $outPath
        if ($dir -and -not (Test-Path -LiteralPath $dir)) { [void](New-Item -ItemType Directory -Path $dir -Force) }
        [IO.File]::WriteAllText($outPath, $json, (New-Object System.Text.UTF8Encoding($false)))
    } catch {
        Write-Host "결과 파일 쓰기 실패: $($_.Exception.Message)"
    }
    foreach ($e in $script:errors) { Write-Host "ERROR: $e" }
    Write-Host "RESULT=$result"
    exit $code
}

function Reject([string]$msg) { [void]$script:errors.Add($msg) }

# ── 스키마 로드 ──────────────────────────────────────────────────
try {
    if (-not (Test-Path -LiteralPath $Schema)) { throw "스키마 없음: $Schema" }
    $schemaObj = [IO.File]::ReadAllText($Schema) | ConvertFrom-Json
    if (-not (Test-Path -LiteralPath $Worktree)) { throw "Worktree 없음: $Worktree" }
    $wtFull = [IO.Path]::GetFullPath($Worktree).TrimEnd('\', '/')
} catch {
    [void]$script:errors.Add("INFRA: $($_.Exception.Message)")
    Finish 'INFRA' 3
}

$keyMap = @{}
foreach ($k in $schemaObj.keys) { $keyMap[[string]$k.key] = $k }

# ── 요청 로드 ────────────────────────────────────────────────────
try {
    if (-not (Test-Path -LiteralPath $Request)) { throw "요청 파일 없음: $Request" }
    $reqText = [IO.File]::ReadAllText($Request)
} catch {
    [void]$script:errors.Add("INFRA: $($_.Exception.Message)")
    Finish 'INFRA' 3
}
try { $req = $reqText | ConvertFrom-Json } catch { $req = $null; Reject "요청 JSON 파싱 실패" }
if ($null -eq $req) { if ($script:errors.Count -eq 0) { Reject "요청이 비어 있음" }; Finish 'REJECTED' 1 }

if ($req.requestId -is [string]) { $script:requestId = $req.requestId }
if ($req.requestId -isnot [string] -or $req.requestId -cnotmatch '^BAL-\d+$') { Reject "requestId 형식 오류 (^BAL-\d+$ 필요)" }
$changes = @($req.changes)
if ($changes.Count -eq 0 -or $null -eq $req.changes) { Reject "changes 가 비어 있음" }
if ($script:errors.Count -gt 0) { Finish 'REJECTED' 1 }

# ── 변경 항목 검증 ───────────────────────────────────────────────
$plan  = New-Object System.Collections.ArrayList   # key, value(double), text, def
$seen  = @{}
foreach ($ch in $changes) {
    $key = $ch.key
    if ($key -isnot [string] -or -not $keyMap.ContainsKey($key)) { Reject "미등록 키: $key"; continue }
    if ($seen.ContainsKey($key)) { Reject "중복 키: $key"; continue }
    $seen[$key] = $true
    $def = $keyMap[$key]
    $val = $ch.value
    if ($val -isnot [string] -or $val -cnotmatch '^-?\d+(\.\d+)?$') { Reject "${key}: 숫자 형식이 아님 (값은 문자열 ^-?\d+(\.\d+)?$)"; continue }
    if ($def.type -eq 'int' -and $val.Contains('.')) { Reject "${key}: int 키에 소수 값 '$val'"; continue }
    $num = 0.0
    if (-not [double]::TryParse($val, [Globalization.NumberStyles]::Float, $inv, [ref]$num)) { Reject "${key}: 파싱 실패 '$val'"; continue }
    if ($num -lt [double]$def.min -or $num -gt [double]$def.max) { Reject "${key}: $val 가 범위 [$($def.min), $($def.max)] 밖"; continue }
    if ($def.type -eq 'int') { $text = ([long]$num).ToString($inv) }
    else {
        $text = $num.ToString('R', $inv)
        if ($text -match '[eE]') { Reject "${key}: 지수 표기가 필요한 값 '$val' 거부"; continue }
    }
    [void]$plan.Add([pscustomobject]@{ key = $key; num = $num; text = $text; def = $def })
}
if ($script:errors.Count -gt 0) { Finish 'REJECTED' 1 }

# ── 파일 읽기 / 현재 값 ──────────────────────────────────────────
function Get-FieldRegex($def) {
    $f = [regex]::Escape([string]$def.field)
    if ($def.kind -eq 'unityYaml') { return "(?m)^  ${f}: ([^\r\n]+)$" }
    elseif ($def.kind -eq 'json')  { return "`"${f}`"\s*:\s*(-?\d+(\.\d+)?)" }
    return $null
}

$files = @{}   # full path -> @{ text; bom; rel }
function Get-FileState($def) {
    $rel = [string]$def.file
    $full = [IO.Path]::GetFullPath((Join-Path $wtFull $rel))
    if (-not $full.StartsWith($wtFull + '\', [StringComparison]::OrdinalIgnoreCase)) { throw "REJECT:대상 경로가 Worktree 밖: $rel" }
    if (-not $files.ContainsKey($full)) {
        if (-not (Test-Path -LiteralPath $full)) { throw "INFRA:대상 파일 없음: $rel" }
        $bytes = [IO.File]::ReadAllBytes($full)
        $bom = ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)
        $txt = (New-Object System.Text.UTF8Encoding($false)).GetString($bytes, $(if ($bom) { 3 } else { 0 }), $bytes.Length - $(if ($bom) { 3 } else { 0 }))
        $files[$full] = @{ text = $txt; bom = $bom; rel = $rel; full = $full }
    }
    return $files[$full]
}

function Read-Current($def, $state) {
    if ($def.kind -eq 'unityYaml' -and -not $state.text.StartsWith('%YAML')) { throw "REJECT:Unity YAML 이 아님(%YAML 헤더 없음): $($def.file)" }
    $rx = Get-FieldRegex $def
    if (-not $rx) { throw "REJECT:알 수 없는 kind '$($def.kind)': $($def.key)" }
    $m = [regex]::Matches($state.text, $rx)
    if ($m.Count -ne 1) { throw "REJECT:$($def.key): 필드 '$($def.field)' 매치 $($m.Count)회 (정확히 1회 필요) — $($def.file)" }
    return $m[0]
}

$infra = $false
$cur = @{}       # key -> double (현재 파일값)
foreach ($p in $plan) {
    try {
        $st = Get-FileState $p.def
        $m = Read-Current $p.def $st
        $oldText = $m.Groups[1].Value
        $oldNum = 0.0
        if (-not [double]::TryParse($oldText, [Globalization.NumberStyles]::Float, $inv, [ref]$oldNum)) { throw "REJECT:$($p.key): 현재 값 '$oldText' 숫자 파싱 실패" }
        $cur[$p.key] = $oldNum
        $p | Add-Member -NotePropertyName old -NotePropertyValue $oldText
    } catch {
        $msg = $_.Exception.Message
        if ($msg.StartsWith('INFRA:')) { $infra = $true; Reject $msg.Substring(6) }
        elseif ($msg.StartsWith('REJECT:')) { Reject $msg.Substring(7) }
        else { Reject $msg }
    }
}
if ($infra) { Finish 'INFRA' 3 }
if ($script:errors.Count -gt 0) { Finish 'REJECTED' 1 }

# ── 제약(constraints): 현재 파일값 + 요청값 병합 평가 ─────────────
$reqVals = @{}
foreach ($p in $plan) { $reqVals[$p.key] = $p.num }
foreach ($c in @($schemaObj.constraints)) {
    if ($null -eq $c) { continue }
    $touched = $false
    foreach ($k in @($c.sumOf)) { if ($reqVals.ContainsKey($k)) { $touched = $true } }
    if (-not $touched) { continue }
    $sum = 0.0
    $bad = $false
    foreach ($k in @($c.sumOf)) {
        if ($reqVals.ContainsKey($k)) { $sum += $reqVals[$k]; continue }
        if (-not $keyMap.ContainsKey($k)) { Reject "제약 $($c.id): 스키마에 없는 키 $k"; $bad = $true; continue }
        try {
            $st = Get-FileState $keyMap[$k]
            $m = Read-Current $keyMap[$k] $st
            $v = 0.0
            if (-not [double]::TryParse($m.Groups[1].Value, [Globalization.NumberStyles]::Float, $inv, [ref]$v)) { throw "REJECT:값 파싱 실패 $k" }
            $sum += $v
        } catch { Reject ($_.Exception.Message -replace '^(REJECT|INFRA):', ''); $bad = $true }
    }
    if (-not $bad -and $sum -gt ([double]$c.max + 1e-4)) { Reject "제약 $($c.id) 위반: 합 $($sum.ToString('R', $inv)) > $($c.max)" }
}
if ($script:errors.Count -gt 0) { Finish 'REJECTED' 1 }

# ── 메모리에서 치환 → 전부 성공하면 쓰기 ─────────────────────────
foreach ($p in $plan) {
    $st = Get-FileState $p.def
    $rx = Get-FieldRegex $p.def
    $m = [regex]::Match($st.text, $rx)
    $g = $m.Groups[1]
    $st.text = $st.text.Substring(0, $g.Index) + $p.text + $st.text.Substring($g.Index + $g.Length)
    [void]$script:applied.Add([ordered]@{ key = $p.key; old = $p.old; new = $p.text; file = [string]$p.def.file })
}

if ($DryRun) { Finish 'DRYRUN_OK' 0 }

try {
    foreach ($st in $files.Values) {
        [IO.File]::WriteAllText($st.full, $st.text, (New-Object System.Text.UTF8Encoding([bool]$st.bom)))
    }
} catch {
    [void]$script:errors.Add("INFRA: 쓰기 실패 $($_.Exception.Message)")
    Finish 'INFRA' 3
}
Finish 'APPLIED' 0
