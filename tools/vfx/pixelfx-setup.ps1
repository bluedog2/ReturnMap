# ═══════════════════════════════════════════════════════════════
#  pixelfx-setup.ps1 — pixelfx 를 고정 커밋으로 받기
# ═══════════════════════════════════════════════════════════════
# tools\vfx\pixelfx.lock.json 의 commit 으로 저장소 밖(..\pixelfx)에 클론·체크아웃한다(detached).
# npm install 은 하지 않는다 — 렌더(tools/render.mjs)는 의존성이 없다.
# gallery/ (Patch Pixel 폰트 포함, MIT 아님)는 쓰지 않는다.
#
# 사용:
#   tools\vfx\pixelfx-setup.ps1           # 없으면 클론, 있으면 고정 커밋인지 확인·맞춤
#   tools\vfx\pixelfx-setup.ps1 -Check    # 확인만 (exit 0 일치 / 1 불일치·없음)
#
# 버전 올리기: lock.json 의 version·commit 을 바꾸고 이 스크립트를 다시 실행 → 샘플 렌더로 확인 → 커밋.

[CmdletBinding()]
param([switch]$Check)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$lock     = Get-Content (Join-Path $PSScriptRoot 'pixelfx.lock.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$dir      = [IO.Path]::GetFullPath((Join-Path $repoRoot $lock.checkout))

function Head { (git -C $dir rev-parse HEAD).Trim() }

if (-not (Test-Path (Join-Path $dir '.git'))) {
    if ($Check) { Write-Output "MISSING=$dir"; exit 1 }
    git clone --quiet $lock.repo $dir
    if ($LASTEXITCODE -ne 0) { throw "클론 실패: $($lock.repo)" }
}

if ((Head) -ne $lock.commit) {
    if ($Check) { Write-Output "MISMATCH HEAD=$(Head) WANT=$($lock.commit)"; exit 1 }
    if (git -C $dir status --porcelain) { throw "$dir 에 커밋 안 된 변경이 있어 체크아웃하지 않습니다." }
    git -C $dir cat-file -e "$($lock.commit)^{commit}" 2>$null
    if ($LASTEXITCODE -ne 0) { git -C $dir fetch --quiet origin }
    git -C $dir checkout --quiet --detach $lock.commit
    if ((Head) -ne $lock.commit) { throw "체크아웃 실패: $($lock.commit)" }
}

Write-Output "PIXELFX=$dir"
Write-Output "VERSION=$($lock.version) COMMIT=$($lock.commit)"
exit 0
