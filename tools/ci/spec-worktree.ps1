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
#   tools\ci\spec-worktree.ps1 -Spec SPEC-9 -Remove -DeleteBranch
#                                                       # + 브랜치 삭제 — 기준 브랜치에 완전히 병합된 경우에만
#                                                       #   (git branch -d, 미병합이면 유지하고 경고)
#   tools\ci\spec-worktree.ps1 -Spec SPEC-9 -Status    # main 대비 거리·병합 충돌 여부만 출력(읽기 전용)
#   tools\ci\spec-worktree.ps1 -Spec SPEC-9 -Commit -MessageFile <txt> [-IncludeMeta "a.cs.meta,b.meta"]
#                                                       # spec 브랜치에 커밋 (자동화는 git 을 직접 쓰지 않고 이것만 쓴다)
#                                                       # .meta 는 -IncludeMeta 로 지정한 것만 들어가고 나머지 미추적 .meta 는 제외
#                                                       # -AutoMeta: 이 브랜치가 추가한 파일·폴더의 .meta 만 자동 선택, 나머지는 삭제(META_SKIPPED=)
#   tools\ci\spec-worktree.ps1 -Spec SPEC-9 -Stash -Message "rev 2 중단: 사유"
#                                                       # 커밋 안 된 변경을 stash 로 치움(중단 시)
#
# 생성/재사용 출력: 상태 줄들 + 마지막 줄 WORKTREE=<절대경로>  (자동화가 파싱)
#   DIRTY=1 이 나오면 커밋 안 된 잔재가 있다는 뜻 → 자동화는 구현하지 말고 보류할 것
#   PROBES=0 이면 이 브랜치에 tools/ci/probes 가 없다(인프라 미커밋)

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidatePattern('^(SPEC|BAL)-\d+$')][string]$Spec,
    [string]$Base = 'main',
    [string]$Root = '',
    [switch]$Remove,
    [switch]$DeleteBranch,
    [switch]$Status,
    [switch]$Stash,
    [string]$Message = '',
    [switch]$Commit,
    [string]$MessageFile = '',
    [string]$IncludeMeta = '',
    [switch]$AutoMeta
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

# ── 상태 조회 (읽기 전용): main 대비 뒤처진 커밋 수 + 병합 충돌 여부 ──
if ($Status) {
    Invoke-Git -C $RepoRoot rev-parse --verify --quiet "refs/heads/$Branch" | Out-Null
    if ($LASTEXITCODE -ne 0) { Write-Host "BRANCH=absent"; exit 0 }
    $behind = (Invoke-Git -C $RepoRoot rev-list --count "$Branch..$Base").Trim()
    $ahead = (Invoke-Git -C $RepoRoot rev-list --count "$Base..$Branch").Trim()
    $files = @(Invoke-Git -C $RepoRoot merge-tree --write-tree --name-only $Base $Branch)
    $conflict = if ($LASTEXITCODE -eq 1) { 1 } elseif ($LASTEXITCODE -eq 0) { 0 } else { -1 }
    Write-Host "AHEAD=$ahead"
    Write-Host "BEHIND=$behind"
    Write-Host "CONFLICT=$conflict"
    if ($conflict -eq 1) {
        # 첫 줄은 트리 sha, 이후 충돌 파일 목록(빈 줄 전까지)
        $files | Select-Object -Skip 1 | Where-Object { $_ } | Select-Object -First 10 | ForEach-Object { Write-Host "CONFLICT_FILE=$_" }
    }
    exit 0
}

# ── 커밋 (자동화 전용 경로: 브랜치 확인 · .meta 선별 · 메시지 파일) ──
if ($Commit) {
    if (-not ($registered -contains $Path)) { throw "[spec] worktree 없음: $Path" }
    $cur = (Invoke-Git -C $Path branch --show-current).Trim()
    if ($cur -ne $Branch) { throw "[spec] 현재 브랜치가 $Branch 가 아니라 '$cur' — 커밋 중단" }
    if (-not $MessageFile -or -not (Test-Path $MessageFile)) { throw "[spec] -MessageFile 필요" }
    Invoke-Git -C $Path add -A -- . ':(exclude)*.meta' | Out-Null
    # 추적 중인 .meta 의 수정·삭제는 그대로 반영, 새 .meta 는 지정한 것만
    Invoke-Git -C $Path add -u -- '*.meta' | Out-Null
    if ($AutoMeta) {
        # 이 브랜치가 새로 추가한 파일(또는 그 상위 폴더)의 .meta 만 포함, 나머지 미추적 .meta 는 지운다
        # (main 에 있어야 할 .meta 를 spec 브랜치가 따로 갖지 않게 — 병합 시 GUID 충돌 방지)
        $added = @(Invoke-Git -C $Path diff --name-only --diff-filter=A "$Base...HEAD" | Where-Object { $_ })
        $owners = New-Object System.Collections.Generic.HashSet[string]
        foreach ($a in $added) {
            $p = $a
            while ($p) { [void]$owners.Add($p); $i = $p.LastIndexOf('/'); $p = if ($i -gt 0) { $p.Substring(0, $i) } else { '' } }
        }
        $metas = @(Invoke-Git -C $Path ls-files --others --exclude-standard -- '*.meta' | Where-Object { $_ })
        $picked = @(); $skipped = @()
        foreach ($m in $metas) {
            $owner = $m.Substring(0, $m.Length - 5)
            if ($owners.Contains($owner) -and $owner -notmatch '^Assets$') { $picked += $m } else { $skipped += $m }
        }
        foreach ($m in $skipped) { Remove-Item (Join-Path $Path $m) -Force }
        if ($skipped.Count -gt 0) { Write-Host "META_SKIPPED=$($skipped -join ',')" }
        $IncludeMeta = ($picked -join ',')
    }
    foreach ($m in ($IncludeMeta -split ',' | Where-Object { $_.Trim() })) {
        Invoke-Git -C $Path add -- $m.Trim() | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "[spec] .meta 추가 실패: $m" }
    }
    $staged = Invoke-Git -C $Path diff --cached --name-status
    if (-not $staged) { Write-Host "COMMIT=nothing"; exit 0 }
    $staged | ForEach-Object { Write-Host "  $_" }
    Invoke-Git -C $Path commit -q -F (Resolve-Path $MessageFile) | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "[spec] git commit 실패" }
    $left = @(Invoke-Git -C $Path ls-files --others --exclude-standard | Where-Object { $_ })
    Write-Host "COMMIT=$((Invoke-Git -C $Path rev-parse --short HEAD).Trim())"
    if ($left.Count -gt 0) { Write-Host "UNTRACKED_LEFT=$($left -join ',')" }
    exit 0
}

# ── 중단 시 잔재 치우기 ──
if ($Stash) {
    if (-not ($registered -contains $Path)) { Write-Host "[spec] worktree 없음: $Path"; exit 0 }
    $dirty = Invoke-Git -C $Path status --porcelain
    if (-not $dirty) { Write-Host "STASH=clean"; exit 0 }
    $msg = if ($Message) { "$Spec $Message" } else { "$Spec 중단 $(Get-Date -Format s)" }
    Invoke-Git -C $Path stash push -u -m $msg | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "[spec] stash 실패" }
    Write-Host "STASH=saved ($msg) — 복구: git -C $Path stash list / stash pop"
    exit 0
}

if ($Remove) {
    if ($registered -contains $Path) {
        $dirty = Invoke-Git -C $Path status --porcelain
        if ($dirty) { throw "[spec] $Path 에 커밋 안 된 변경이 있어 제거 중단. 확인 후 커밋하거나 직접 정리할 것." }
        Invoke-Git -C $RepoRoot worktree remove $Path | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "[spec] worktree 제거 실패: $Path" }
        Write-Host "[spec] worktree 제거: $Path"
    } else {
        Write-Host "[spec] 등록된 worktree 없음: $Path"
    }

    Invoke-Git -C $RepoRoot rev-parse --verify --quiet "refs/heads/$Branch" | Out-Null
    $branchExists = ($LASTEXITCODE -eq 0)
    if (-not $DeleteBranch -or -not $branchExists) {
        if ($branchExists) { Write-Host "[spec] 브랜치 $Branch 유지" } else { Write-Host "[spec] 브랜치 $Branch 없음" }
        Write-Host "BRANCH=$(if ($branchExists) { 'kept' } else { 'absent' })"
        exit 0
    }

    # 안전 삭제: 기준 브랜치에 완전히 포함된 경우에만 (-d, 절대 -D 아님)
    Invoke-Git -C $RepoRoot merge-base --is-ancestor $Branch $Base | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "[spec] $Branch 가 $Base 에 병합되지 않아 삭제하지 않음 (병합 후 다시 실행)"
        Write-Host "BRANCH=unmerged"
        exit 0
    }
    Invoke-Git -C $RepoRoot branch -d $Branch | Out-Null
    if ($LASTEXITCODE -ne 0) {
        # -d 는 "현재 HEAD 에 병합됐나"로 판단해서, 메인 체크아웃이 다른 커밋(detached 등)이면 거부한다.
        # 위에서 $Base 포함을 이미 확인했으므로 그 sha 를 조건으로 ref 만 지운다 (그 사이 바뀌었으면 실패).
        $sha = (Invoke-Git -C $RepoRoot rev-parse "refs/heads/$Branch").Trim()
        Invoke-Git -C $RepoRoot update-ref -d "refs/heads/$Branch" $sha | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "[spec] 브랜치 $Branch 삭제 실패" }
    }
    Write-Host "[spec] 브랜치 삭제: $Branch ($Base 에 병합됨)"
    Write-Host "BRANCH=deleted"
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
$hasProbes = Test-Path (Join-Path $Path 'tools\ci\probes\README.md')
Write-Host "PROBES=$([int]$hasProbes)"
if (-not $hasProbes) { Write-Warning "[spec] 이 브랜치에 tools/ci/probes 가 없다(인프라 미커밋). 프로브 템플릿은 메인 저장소 경로에서 읽을 것." }

# 커밋 안 된 잔재 감지 — 이전 구현이 중단되며 남긴 변경이 다음 커밋에 섞이지 않게
$dirty = Invoke-Git -C $Path status --porcelain
Write-Host "DIRTY=$([int][bool]$dirty)"
if ($dirty) { $dirty | Select-Object -First 10 | ForEach-Object { Write-Host "  $_" } }

# 이미 있는 이 기획의 구현 커밋 (재개 판단용)
Invoke-Git -C $Path log --oneline "$Base..HEAD" | Select-Object -First 15 | ForEach-Object { Write-Host "COMMIT=$_" }

Write-Host "WORKTREE=$Path"
