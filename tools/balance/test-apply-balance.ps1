# ═══════════════════════════════════════════════════════════════
#  test-apply-balance.ps1 — apply-balance.ps1 시나리오 테스트
# ═══════════════════════════════════════════════════════════════
# 저장소를 건드리지 않는다: 스키마 대상 파일을 $env:TEMP 아래 scratch 에 같은 상대 경로로 복사해
# -Worktree 로 지정하고 실행한다. 끝나면 scratch 삭제. 실패가 있으면 exit 1.

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$repo = [IO.Path]::GetFullPath((Join-Path $here '..\..'))
$apply = Join-Path $here 'apply-balance.ps1'
$schemaPath = Join-Path $here 'balance-schema.json'
$schema = [IO.File]::ReadAllText($schemaPath) | ConvertFrom-Json

$scratch = Join-Path $env:TEMP ("balance-test-" + [guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $scratch)
$wt = Join-Path $scratch 'wt'
foreach ($rel in ($schema.keys | ForEach-Object { $_.file } | Select-Object -Unique)) {
    $dst = Join-Path $wt $rel
    [void](New-Item -ItemType Directory -Path (Split-Path -Parent $dst) -Force)
    Copy-Item -LiteralPath (Join-Path $repo $rel) -Destination $dst
}

$script:fail = 0
$script:n = 0

function Run([string]$reqJson, [switch]$DryRun, [string]$Wt = $wt, [string]$SchemaArg = '') {
    $script:n++
    $req = Join-Path $scratch "req$($script:n).json"
    $out = Join-Path $scratch "res$($script:n).json"
    [IO.File]::WriteAllText($req, $reqJson, (New-Object System.Text.UTF8Encoding($false)))
    $a = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $apply, '-Request', $req, '-Worktree', $Wt, '-Out', $out)
    if ($DryRun) { $a += '-DryRun' }
    if ($SchemaArg) { $a += @('-Schema', $SchemaArg) }
    $console = & powershell @a 2>&1 | Out-String
    $code = $LASTEXITCODE
    $res = $null
    if (Test-Path $out) { $res = [IO.File]::ReadAllText($out) | ConvertFrom-Json }
    return [pscustomobject]@{ code = $code; console = $console; res = $res }
}

function Check([string]$name, [bool]$ok, [string]$detail = '') {
    if ($ok) { Write-Host "PASS  $name" }
    else { Write-Host "FAIL  $name  $detail"; $script:fail++ }
}

function Expect([string]$name, $r, [string]$result, [int]$code) {
    $ok = ($r.code -eq $code) -and ($r.res.result -eq $result) -and ($r.console -match "RESULT=$result")
    Check $name $ok "exit=$($r.code) result=$($r.res.result) errors=$($r.res.errors -join '|')"
}

function Snapshot { $h = @{}; Get-ChildItem $wt -Recurse -File | ForEach-Object { $h[$_.FullName] = [IO.File]::ReadAllBytes($_.FullName) }; return $h }
function Unchanged($snap) {
    foreach ($k in $snap.Keys) {
        $now = [IO.File]::ReadAllBytes($k)
        if ([Convert]::ToBase64String($now) -ne [Convert]::ToBase64String($snap[$k])) { return $false }
    }
    return $true
}
function Chg([string]$k, [string]$v) { return "{`"key`":`"$k`",`"value`":`"$v`"}" }
function Req([string[]]$c) { return '{"requestId":"BAL-1","changes":[' + ($c -join ',') + '],"note":"무시되는 근거 텍스트"}' }

try {
    $assetRel = 'Assets/_Project/Settings/BalanceConfig.asset'
    $mapRel = 'Assets/StreamingAssets/Maps/stage_01.json'
    $assetPath = Join-Path $wt $assetRel
    $mapPath = Join-Path $wt $mapRel
    $assetBefore = [IO.File]::ReadAllBytes($assetPath)
    $mapBefore = [IO.File]::ReadAllBytes($mapPath)
    $mapHadBom = ($mapBefore[0] -eq 0xEF -and $mapBefore[1] -eq 0xBB -and $mapBefore[2] -eq 0xBF)

    # 1) 정상: DryRun 은 파일 불변, 이후 실제 적용
    $snap = Snapshot
    $good = Req @((Chg 'mutation.normalChance' '0.45'), (Chg 'stage_01.buildBudget' '150'), (Chg 'stage.clearReward' '20'))
    $r = Run $good -DryRun
    Expect '정상 DryRun' $r 'DRYRUN_OK' 0
    Check '정상 DryRun 파일 불변' (Unchanged $snap)

    $r = Run $good
    Expect '정상 적용' $r 'APPLIED' 0
    $assetAfter = [IO.File]::ReadAllText($assetPath); $assetOld = [IO.File]::ReadAllText((Join-Path $repo $assetRel))
    $diffA = @(); $la = $assetAfter -split "`n"; $lo = $assetOld -split "`n"
    for ($i = 0; $i -lt $lo.Count; $i++) { if ($la[$i] -ne $lo[$i]) { $diffA += $la[$i] } }
    Check '에셋: 대상 라인만 변경' ($la.Count -eq $lo.Count -and $diffA.Count -eq 2 -and ($diffA -contains '  normalChance: 0.45') -and ($diffA -contains '  stageClearReward: 20')) ($diffA -join '|')
    $mapAfter = [IO.File]::ReadAllBytes($mapPath)
    $mapBomNow = ($mapAfter[0] -eq 0xEF -and $mapAfter[1] -eq 0xBB -and $mapAfter[2] -eq 0xBF)
    Check '맵 JSON BOM 보존' ($mapBomNow -eq $mapHadBom) "before=$mapHadBom after=$mapBomNow"
    $mt = [IO.File]::ReadAllText($mapPath); $mo = [IO.File]::ReadAllText((Join-Path $repo $mapRel))
    Check '맵 JSON: buildBudget 만 변경' ($mt -eq $mo.Replace('"buildBudget": 100', '"buildBudget": 150'))
    $crBefore = ([Text.Encoding]::UTF8.GetString($mapBefore).ToCharArray() | Where-Object { $_ -eq "`r" }).Count
    $crAfter = ([Text.Encoding]::UTF8.GetString($mapAfter).ToCharArray() | Where-Object { $_ -eq "`r" }).Count
    Check '개행(CR 수) 보존' ($crBefore -eq $crAfter)
    Check '에셋 BOM 유무 보존(없음)' (-not ($assetAfter.Length -gt 0 -and [IO.File]::ReadAllBytes($assetPath)[0] -eq 0xEF))
    Check '저장소 원본 불변' (([IO.File]::ReadAllText((Join-Path $repo $assetRel)) -match 'normalChance: 0\.5\r?\n'))

    # 이후 시나리오는 적용된 상태(normal 0.45, dud 0.2, critical 0.2) 기준
    $snap = Snapshot

    # 2) 범위 밖
    $r = Run (Req @((Chg 'verification.timeLimit' '5')))
    Expect '범위 밖(min)' $r 'REJECTED' 1
    $r = Run (Req @((Chg 'trap.Spike.dangerLevel' '11')))
    Expect '범위 밖(max)' $r 'REJECTED' 1

    # 3) 미등록 키
    $r = Run (Req @((Chg 'player.moveSpeed' '5')))
    Expect '미등록 키' $r 'REJECTED' 1

    # 4) 비숫자
    $r = Run (Req @((Chg 'mutation.dudChance' '0.3; rm')))
    Expect '비숫자 0.3; rm' $r 'REJECTED' 1
    $r = Run (Req @((Chg 'mutation.dudChance' '1e-3')))
    Expect '지수 표기 입력' $r 'REJECTED' 1

    # 5) 합 > 1 (요청값 + 현재 파일값 병합)
    $r = Run (Req @((Chg 'mutation.dudChance' '0.45')))
    Expect '합>1 (요청 1개 + 파일값)' $r 'REJECTED' 1
    $r = Run (Req @((Chg 'mutation.normalChance' '0.6'), (Chg 'mutation.dudChance' '0.3'), (Chg 'mutation.criticalChance' '0.3')))
    Expect '합>1 (요청 3개)' $r 'REJECTED' 1

    # 6) int 자리에 소수
    $r = Run (Req @((Chg 'stage.clearReward' '10.5')))
    Expect 'int 에 소수' $r 'REJECTED' 1

    # 7) 경로 탈출 key / 스키마 file 경로 탈출
    $r = Run (Req @((Chg '..\..\evil.txt' '1')))
    Expect '경로 탈출 key' $r 'REJECTED' 1
    $badSchema = Join-Path $scratch 'bad-schema.json'
    [IO.File]::WriteAllText($badSchema, '{"version":1,"keys":[{"key":"x.y","file":"../outside.asset","kind":"unityYaml","field":"a","type":"int","min":0,"max":9}],"constraints":[]}', (New-Object System.Text.UTF8Encoding($false)))
    $r = Run (Req @((Chg 'x.y' '1'))) -SchemaArg $badSchema
    Expect '스키마 file 경로 탈출' $r 'REJECTED' 1

    # 8) 기타: 중복 키, requestId 형식, 한 건이라도 나쁘면 전부 미적용
    $r = Run (Req @((Chg 'stage.clearReward' '30'), (Chg 'stage.clearReward' '31')))
    Expect '중복 키' $r 'REJECTED' 1
    $r = Run '{"requestId":"X-1","changes":[{"key":"stage.clearReward","value":"30"}]}'
    Expect 'requestId 형식 오류' $r 'REJECTED' 1
    $r = Run (Req @((Chg 'stage.clearReward' '30'), (Chg 'stage.clearReward2' '1')))
    Expect '혼합 요청(일부 불량)' $r 'REJECTED' 1
    Check '거부된 요청들은 파일 미변경(all-or-nothing)' (Unchanged $snap)

    # 9) 인프라: Worktree 에 대상 파일 없음
    $empty = Join-Path $scratch 'empty'; [void](New-Item -ItemType Directory -Path $empty)
    $r = Run (Req @((Chg 'stage.clearReward' '30'))) -Wt $empty
    Expect '대상 파일 없음 → INFRA' $r 'INFRA' 3
}
finally {
    Remove-Item -LiteralPath $scratch -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host ("scratch 삭제: " + (-not (Test-Path $scratch)))
}

if ($script:fail -gt 0) { Write-Host "TEST RESULT=FAIL ($($script:fail))"; exit 1 }
Write-Host "TEST RESULT=PASS"
exit 0
