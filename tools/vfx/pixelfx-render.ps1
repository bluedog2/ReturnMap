# ═══════════════════════════════════════════════════════════════
#  pixelfx-render.ps1 — pixelfx 이펙트 → Px32 스프라이트 시트(PNG+JSON) → PIXELFX 수신함 (Unity 불필요)
# ═══════════════════════════════════════════════════════════════
# 고정 커밋(pixelfx.lock.json)인지 확인 → tools/render.mjs --sheet(캐릭터 없음·투명 배경) →
# pixelfx-convert.mjs 로 화풍 v2 규격 변환. 저장소 루트 PIXELFX\<Name>\ 에:
#   Pfx_<Name>.png/.json  변환된 시트(가로 1줄 정사각 셀, 팔레트 양자화) + fps·loop·cell
#   raw.png/raw.json     pixelfx 원본 시트 · contact.png 검수용 · recipe.json 재현용
# -InstallTo <프로젝트 루트> 이면 수신함을 그 프로젝트의 PIXELFX\ 로 바꾸고 Pfx_<Name>.png 를 Sprites/Px32/Effects/Pixelfx/ 에도 넣는다
# (spec-cycle 자동화가 worktree 에 쓰는 경로. .meta 는 만들지 않음 — CI -HarvestMetaTo 가 고정).
# 클립(.anim)은 Unity 메뉴 ReTrap → Art → pixelfx 이펙트 가져오기 가 만든다.
#
# 사용:
#   tools\vfx\pixelfx-render.ps1 -Effect impacts/hit-spark -Name HitSpark
#   tools\vfx\pixelfx-render.ps1 -Effect fire/torch -Name Torch -Fps 12 -Seed 3 -Opts '{"palette":["#19181F","#8A1C2B","#E0475B"]}'
#   tools\vfx\pixelfx-render.ps1 -Effect impacts/hit-spark -Name HitSpark -Force -InstallTo C:\Users\Owner\work\ReturnMap-spec\SPEC-12
#
# 출력: CELL=… 줄, INSTALLED=<경로>(지정 시), 마지막 줄 INBOX=<폴더>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidatePattern('^[a-z0-9-]+/[a-z0-9-]+$')][string]$Effect,
    [Parameter(Mandatory = $true)][ValidatePattern('^[A-Z][A-Za-z0-9]*$')][string]$Name,
    [int]$Fps = 15,
    [int]$Seed = 0,
    [string]$Opts = '',
    [string]$InstallTo = '',
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$lock     = Get-Content (Join-Path $PSScriptRoot 'pixelfx.lock.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$pfx      = [IO.Path]::GetFullPath((Join-Path $repoRoot $lock.checkout))

& (Join-Path $PSScriptRoot 'pixelfx-setup.ps1') -Check | Out-Null
if ($LASTEXITCODE -ne 0) { throw "pixelfx 가 고정 커밋이 아닙니다 — tools\vfx\pixelfx-setup.ps1 을 먼저 실행하세요." }

$inbox = Join-Path $(if ($InstallTo) { $InstallTo } else { $repoRoot }) "PIXELFX\$Name"   # 자동화: 수신함도 worktree 쪽(메인은 읽기 전용)
if ((Test-Path $inbox) -and -not $Force) { throw "이미 있음: $inbox (덮어쓰려면 -Force)" }
$installPath = ''
if ($InstallTo) {
    $installPath = Join-Path $InstallTo "Assets\_Project\ResourcceEX\Sprites\Px32\Effects\Pixelfx\Pfx_$Name.png"
    if ((Test-Path $installPath) -and -not $Force) { throw "이미 있음: $installPath (덮어쓰려면 -Force)" }
}

# 1) pixelfx 렌더 (임시 폴더)
$tmp = Join-Path ([IO.Path]::GetTempPath()) ("pixelfx-" + [Guid]::NewGuid().ToString('N'))
$nodeArgs = @('tools/render.mjs', $Effect, '--sheet', '--no-actor', '--bg', 'none', '--fps', "$Fps", '--out', $tmp)
if ($Seed -ne 0) { $nodeArgs += @('--seed', "$Seed") }
if ($Opts)       { $nodeArgs += @('--opts', $Opts.Replace('"', '\"')) }   # PowerShell 5.1 은 네이티브 인자의 " 를 벗겨낸다

Push-Location $pfx
try { & node @nodeArgs | Out-Null; $code = $LASTEXITCODE } finally { Pop-Location }
if ($code -ne 0) { throw "렌더 실패 (exit $code): $Effect" }

$leaf = $Effect.Split('/')[1]
$src  = Join-Path $tmp $Effect.Replace('/', '\')
if (Test-Path $inbox) { Remove-Item $inbox -Recurse -Force }
New-Item -ItemType Directory -Force $inbox | Out-Null
Copy-Item (Join-Path $src "$leaf.png")   (Join-Path $inbox 'raw.png')
Copy-Item (Join-Path $src "$leaf.json")  (Join-Path $inbox 'raw.json')
Copy-Item (Join-Path $src 'contact.png') (Join-Path $inbox 'contact.png')
Remove-Item $tmp -Recurse -Force

# 2) 화풍 v2 변환
& node (Join-Path $PSScriptRoot 'pixelfx-convert.mjs') (Join-Path $inbox 'raw.json') (Join-Path $inbox 'raw.png') `
    (Join-Path $repoRoot 'NEWIMAG\palette_v2.txt') (Join-Path $inbox "Pfx_$Name.png") (Join-Path $inbox "Pfx_$Name.json")
if ($LASTEXITCODE -ne 0) { throw "변환 실패: $Effect" }

[ordered]@{
    effect  = $Effect
    name    = $Name
    fps     = $Fps
    seed    = $Seed
    opts    = $Opts
    version = $lock.version
    commit  = $lock.commit
    command = "tools\vfx\pixelfx-render.ps1 -Effect $Effect -Name $Name -Fps $Fps" + $(if ($Seed) { " -Seed $Seed" } else { '' }) + $(if ($Opts) { " -Opts '$Opts'" } else { '' })
} | ConvertTo-Json | Set-Content (Join-Path $inbox 'recipe.json') -Encoding UTF8

# 3) (선택) 프로젝트에 설치
if ($installPath) {
    New-Item -ItemType Directory -Force (Split-Path $installPath) | Out-Null
    Copy-Item (Join-Path $inbox "Pfx_$Name.png") $installPath -Force
    Write-Output "INSTALLED=$installPath"
}

Write-Output "INBOX=$inbox"
exit 0
