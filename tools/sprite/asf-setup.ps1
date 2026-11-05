# ═══════════════════════════════════════════════════════════════
#  asf-setup.ps1 — Agent Sprite Forge(ASF) 를 고정 커밋으로 받기
# ═══════════════════════════════════════════════════════════════
# tools\sprite\asf.lock.json 의 commit 으로 저장소 밖(..\agent-sprite-forge)에 클론·체크아웃한다(detached).
# Python 의존성(numpy·Pillow·scipy)을 lock 의 Python 버전에 설치하고,
# NEWIMAG\palette_v2.txt 를 ASF 가 읽는 .hex 형식으로 변환해 ASF\palette_v2.hex 에 둔다(원본은 그대로).
# ASF 는 GPT 시트 후처리 전용 — ASF 가 그린 그림은 게임에 넣지 않는다(tools\sprite\README.md).
#
# 사용:
#   tools\sprite\asf-setup.ps1           # 없으면 클론, 있으면 고정 커밋인지 확인·맞춤 + 의존성 + 팔레트
#   tools\sprite\asf-setup.ps1 -Check    # 확인만 (exit 0 일치 / 1 불일치·없음)
#
# 버전 올리기: lock.json 의 version·commit 을 바꾸고 다시 실행 → 기존 시험 시트로 회귀 비교 → 커밋.

[CmdletBinding()]
param([switch]$Check)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$lock     = Get-Content (Join-Path $PSScriptRoot 'asf.lock.json') -Raw -Encoding UTF8 | ConvertFrom-Json
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

if (-not $Check) {
    # 의존성 — 의도적으로 ASF requirements.txt(jsonschema·PyYAML 포함) 전체 설치
    & py "-$($lock.python)" -m pip install --quiet --disable-pip-version-check -r (Join-Path $dir 'requirements.txt')
    if ($LASTEXITCODE -ne 0) { throw "pip 설치 실패 (py -$($lock.python))" }

    # 팔레트 변환: '#' 주석 줄과 색 줄이 섞인 txt → 색만 한 줄씩(.hex)
    $outDir = Join-Path $repoRoot 'ASF'
    New-Item -ItemType Directory -Force $outDir | Out-Null
    $src    = Join-Path $repoRoot 'NEWIMAG\palette_v2.txt'
    $colors = Get-Content $src -Encoding UTF8 | ForEach-Object { $_.Trim() } |
              Where-Object { $_ -match '^#[0-9A-Fa-f]{6}$' } | ForEach-Object { $_.Substring(1).ToUpper() }
    if ($colors.Count -eq 0) { throw "팔레트 색을 못 찾음: $src" }
    [IO.File]::WriteAllLines((Join-Path $outDir 'palette_v2.hex'), [string[]]$colors)
    # 캐릭터용: 화풍 v2 기본 20색(파일 앞 20색). 60색 전체로 잡으면 다른 진영 색이 섞인다(시험 2026-11-06: 46색)
    [IO.File]::WriteAllLines((Join-Path $outDir 'palette_hero20.hex'), [string[]]($colors | Select-Object -First 20))
    Write-Output "PALETTE=$(Join-Path $outDir 'palette_v2.hex') COLORS=$($colors.Count) (+ palette_hero20.hex)"
}

Write-Output "ASF=$dir"
Write-Output "VERSION=$($lock.version) COMMIT=$($lock.commit)"
exit 0
