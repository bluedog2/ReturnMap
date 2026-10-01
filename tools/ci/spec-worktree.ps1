# ═══════════════════════════════════════════════════════════════
#  spec-worktree.ps1 — 기획(SPEC-n)별 구현용 git worktree 관리
# ═══════════════════════════════════════════════════════════════
# spec-cycle 3단계가 승인된 기획을 구현할 때 쓴다.
# 메인 작업트리(에디터가 열려 있을 수 있음)는 절대 건드리지 않고,
# ..\ReturnMap-spec\SPEC-n 에 spec/SPEC-n 브랜치 worktree 를 만든다.
# 이 worktree 는 Unity 로 열지 않는다(코드 편집 전용). 검증은 unity-ci.ps1 -Ref spec/SPEC-n.
#
# 사용:
#   tools\ci\spec-worktree.ps1 -Spec SPEC-9            # 생성 또는 재사용 → 경로 출력
#   tools\ci\spec-worktree.ps1 -Spec SPEC-9 -Base main # 기준 브랜치 지정 (기본 main)
#   tools\ci\spec-worktree.ps1 -Spec SPEC-9 -Remove    # worktree 만 제거 (브랜치는 유지)
#
# 출력 마지막 줄: WORKTREE=<절대경로>  (자동화가 파싱)

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidatePattern('^SPEC-\d+$')][string]$Spec,
    [string]$Base = 'main',
    [string]$Root = '',
    [switch]$Remove
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

function Invoke-Git {
    $prev = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { & git @args 2>$null } finally { $ErrorActionPreference = $prev }
}

$RepoRoot = (Invoke-Git -C $PSScriptRoot rev-parse --show-toplevel).Trim().Replace('/', '\')
if (-not $Root) { $Root = Join-Path (Split-Path $RepoRoot -Parent) 'ReturnMap-spec' }
$Path = [System.IO.Path]::GetFullPath((Join-Path $Root $Spec))
$Branch = "spec/$Spec"

$registered = @(Invoke-Git -C $RepoRoot worktree list --porcelain | Where-Object { $_ -like 'worktree *' } |
    ForEach-Object { [System.IO.Path]::GetFullPath(($_.Substring(9)).Replace('/', '\')) })

if ($Remove) {
    if ($registered -contains $Path) {
        $dirty = Invoke-Git -C $Path status --porcelain
        if ($dirty) { throw "[spec] $Path 에 커밋 안 된 변경이 있어 제거 중단. 확인 후 커밋하거나 직접 정리할 것." }
        Invoke-Git -C $RepoRoot worktree remove $Path | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "[spec] worktree 제거 실패: $Path" }
        Write-Host "[spec] worktree 제거: $Path (브랜치 $Branch 는 유지)"
    } else {
        Write-Host "[spec] 등록된 worktree 없음: $Path"
    }
    exit 0
}

if ($registered -contains $Path) {
    Write-Host "[spec] 기존 worktree 재사용: $Path"
} else {
    if (Test-Path $Path) { throw "[spec] $Path 가 이미 있지만 worktree 가 아니다." }
    New-Item -ItemType Directory -Force -Path $Root | Out-Null
    Invoke-Git -C $RepoRoot rev-parse --verify --quiet "refs/heads/$Branch" | Out-Null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "[spec] 기존 브랜치 $Branch 로 worktree 생성"
        Invoke-Git -C $RepoRoot worktree add $Path $Branch | Out-Null
    } else {
        Write-Host "[spec] $Base 에서 새 브랜치 $Branch 생성"
        Invoke-Git -C $RepoRoot worktree add -b $Branch $Path $Base | Out-Null
    }
    if ($LASTEXITCODE -ne 0) { throw "[spec] git worktree add 실패" }
}

# CI 진입점이 기준 브랜치에 있는지 확인 (없으면 CI 가 INFRA_ERROR 로 끝남)
if (-not (Test-Path (Join-Path $Path 'Assets\_Project\Editor\CI\CIRunner.cs'))) {
    Write-Warning "[spec] 이 브랜치에 CIRunner.cs 가 없다. CI 인프라를 $Base 에 커밋한 뒤 브랜치를 다시 만들거나 $Base 를 병합할 것."
}

Write-Host "WORKTREE=$Path"
