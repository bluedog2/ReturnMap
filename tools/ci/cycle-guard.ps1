# ═══════════════════════════════════════════════════════════════
#  cycle-guard.ps1 — spec-cycle 실행 단위 잠금 (소유 토큰 + heartbeat)
# ═══════════════════════════════════════════════════════════════
# 예약 실행이 겹치면 CI worktree·Unity 를 동시에 쓰게 되므로 실행 하나만 돌게 한다.
#   -Acquire          잠금 획득. 출력 마지막 줄: TOKEN=<토큰> 또는 SKIP=<사유>
#   -Touch  -Token t  heartbeat(마지막 활동 시각 갱신). 토큰이 다르면 LOST (다른 실행이 가져감)
#   -Release -Token t 토큰이 일치할 때만 해제 (남의 잠금은 지우지 않는다)
# 만료: 마지막 heartbeat 로부터 -StaleMinutes(기본 240분) 지나면 버려진 잠금으로 보고 가져간다.
# 종료 코드: 0 성공 · 2 SKIP/LOST/불일치

[CmdletBinding()]
param(
    [switch]$Acquire,
    [switch]$Touch,
    [switch]$Release,
    [string]$Token = '',
    [int]$StaleMinutes = 240
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$RepoRoot = (& git -C $PSScriptRoot rev-parse --show-toplevel 2>$null).Trim().Replace('/', '\')
$LockDir = Join-Path $RepoRoot 'Logs'
$LockFile = Join-Path $LockDir 'spec-cycle.lock'
New-Item -ItemType Directory -Force -Path $LockDir | Out-Null

function Read-Lock {
    if (-not (Test-Path $LockFile)) { return $null }
    $line = (Get-Content $LockFile -TotalCount 1 -ErrorAction SilentlyContinue)
    if (-not $line) { return $null }
    $parts = $line.Split('|')
    return [pscustomobject]@{ Token = $parts[0]; Started = $parts[1]; Age = ((Get-Date) - (Get-Item $LockFile).LastWriteTime) }
}

if ($Acquire) {
    $cur = Read-Lock
    if ($cur -and $cur.Age.TotalMinutes -lt $StaleMinutes) {
        Write-Host ("SKIP=이전 실행 진행 중 (시작 {0}, 마지막 활동 {1:N0}분 전)" -f $cur.Started, $cur.Age.TotalMinutes)
        exit 2
    }
    $new = [guid]::NewGuid().ToString('N').Substring(0, 12)
    # 원자적 생성 시도: 없을 때만 생성(CreateNew). 만료 잠금이면 덮어쓰기.
    try {
        if ($cur) { Remove-Item $LockFile -Force }
        $fs = [System.IO.File]::Open($LockFile, 'CreateNew', 'Write', 'None')
        $bytes = [System.Text.Encoding]::UTF8.GetBytes("$new|$((Get-Date).ToString('s'))")
        $fs.Write($bytes, 0, $bytes.Length); $fs.Close()
    } catch {
        Write-Host "SKIP=다른 실행이 동시에 잠금을 가져감"
        exit 2
    }
    if ($cur) { Write-Host ("[guard] 만료된 잠금(마지막 활동 {0:N0}분 전)을 회수" -f $cur.Age.TotalMinutes) }
    Write-Host "TOKEN=$new"
    exit 0
}

if ($Touch -or $Release) {
    if (-not $Token) { Write-Host "[guard] -Token 필요"; exit 2 }
    $cur = Read-Lock
    if (-not $cur -or $cur.Token -ne $Token) {
        Write-Host "LOST=잠금이 없거나 다른 실행 소유 (내 토큰 $Token)"
        exit 2
    }
    if ($Touch) { (Get-Item $LockFile).LastWriteTime = Get-Date; Write-Host "TOUCHED"; exit 0 }
    Remove-Item $LockFile -Force
    Write-Host "RELEASED"
    exit 0
}

Write-Host "사용: -Acquire | -Touch -Token t | -Release -Token t"
exit 2
