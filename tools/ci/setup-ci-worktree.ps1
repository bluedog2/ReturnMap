# ═══════════════════════════════════════════════════════════════
#  setup-ci-worktree.ps1 — CI 전용 git worktree 생성
# ═══════════════════════════════════════════════════════════════
# 메인 프로젝트를 에디터로 열어둔 채 배치모드 Unity를 돌리기 위해
# 같은 저장소의 별도 worktree(기본: ..\ReturnMap-ci)를 만든다.
# Library 는 worktree 마다 따로 생기므로 첫 실행은 임포트로 오래 걸린다.
# -CopyLibrary 를 주면 메인 프로젝트의 Library 를 복사해 첫 임포트를 단축한다.
#
# 사용:
#   powershell -ExecutionPolicy Bypass -File tools\ci\setup-ci-worktree.ps1 [-CopyLibrary]

[CmdletBinding()]
param(
    [string]$CiPath = '',
    [switch]$CopyLibrary
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$RepoRoot = (git -C $PSScriptRoot rev-parse --show-toplevel).Trim()
if (-not $CiPath) { $CiPath = Join-Path (Split-Path $RepoRoot -Parent) 'ReturnMap-ci' }
$CiPath = [System.IO.Path]::GetFullPath($CiPath)

# 이미 등록된 worktree 인지 확인
$registered = git -C $RepoRoot worktree list --porcelain | Where-Object { $_ -like 'worktree *' } |
    ForEach-Object { [System.IO.Path]::GetFullPath(($_.Substring(9)).Replace('/', '\')) }

if ($registered -contains $CiPath) {
    Write-Host "[setup] CI worktree 이미 존재: $CiPath"
} else {
    if (Test-Path $CiPath) {
        throw "[setup] $CiPath 가 이미 있지만 이 저장소의 worktree 가 아니다. 경로를 비우거나 -CiPath 로 다른 경로를 지정할 것."
    }
    Write-Host "[setup] worktree 생성: $CiPath"
    git -C $RepoRoot worktree add --detach $CiPath HEAD
    if ($LASTEXITCODE -ne 0) { throw "[setup] git worktree add 실패" }
}

if ($CopyLibrary) {
    $src = Join-Path $RepoRoot 'Library'
    $dst = Join-Path $CiPath 'Library'
    if (-not (Test-Path $src)) {
        Write-Warning "[setup] 메인 Library 가 없어 복사 생략"
    } elseif (Test-Path $dst) {
        Write-Host "[setup] CI Library 가 이미 있어 복사 생략 (다시 복사하려면 $dst 삭제 후 재실행)"
    } else {
        Write-Host "[setup] Library 복사 중... (메인 에디터는 닫아두는 편이 안전)"
        robocopy $src $dst /E /MT:8 /NFL /NDL /NJH /NJS /NP | Out-Null
        # robocopy 는 8 미만이 성공
        if ($LASTEXITCODE -ge 8) { throw "[setup] Library 복사 실패 (robocopy $LASTEXITCODE)" }
        Write-Host "[setup] Library 복사 완료"
    }
}

Write-Host "[setup] 완료. 다음: powershell -ExecutionPolicy Bypass -File tools\ci\unity-ci.ps1"
