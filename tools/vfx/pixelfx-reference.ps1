# ═══════════════════════════════════════════════════════════════
#  pixelfx-reference.ps1 — pixelfx 이펙트를 GPT 용 "동작 레퍼런스"로 렌더
# ═══════════════════════════════════════════════════════════════
# 게임 이미지는 GPT 가 그린다(화풍 통일). pixelfx 는 동작·타이밍 참고로만 쓴다 — 이 결과물은 게임에 넣지 않는다.
# 고정 커밋(pixelfx.lock.json) 확인 → tools/render.mjs --all --no-actor →
#   <Out>\motion_reference.png   모든 프레임 번호·시각이 찍힌 프레임 목록(어두운 배경)
#   <Out>\motion_reference.json  id · fps · 프레임 수 · 길이 · 원본 크기 · 원점 · loop · 렌더 옵션
#
# 사용:
#   tools\vfx\pixelfx-reference.ps1 -Effect impacts/crit-hit -Out NEWIMAG\FxHitV4
#   tools\vfx\pixelfx-reference.ps1 -Effect impacts/crit-hit -Out NEWIMAG\FxHitV4 -Fps 20 -Palette "#19181F,#641F36,#92223F,#B83450,#DC526B,#F7EEE4"
#
# 출력 마지막 줄: REFERENCE=<png 경로>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidatePattern('^[a-z0-9-]+/[a-z0-9-]+$')][string]$Effect,
    [Parameter(Mandatory = $true)][string]$Out,
    [int]$Fps = 15,
    [int]$Seed = 0,
    [string]$Palette = '',   # "#19181F,#641F36,…" 어두운→밝은 램프. JSON 따옴표 없이 넘기려고 따로 둔다(-Opts 에 합쳐짐)
    [string]$Opts = ''
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$lock     = Get-Content (Join-Path $PSScriptRoot 'pixelfx.lock.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$pfx      = [IO.Path]::GetFullPath((Join-Path $repoRoot $lock.checkout))

& (Join-Path $PSScriptRoot 'pixelfx-setup.ps1') -Check | Out-Null
if ($LASTEXITCODE -ne 0) { throw "pixelfx 가 고정 커밋이 아닙니다 — tools\vfx\pixelfx-setup.ps1 을 먼저 실행하세요." }

if ($Palette) {
    $o = if ($Opts) { $Opts | ConvertFrom-Json } else { New-Object PSObject }
    $o | Add-Member -NotePropertyName palette -NotePropertyValue @($Palette.Split(',') | ForEach-Object { $_.Trim() }) -Force
    $Opts = $o | ConvertTo-Json -Compress
}

$Out = [IO.Path]::GetFullPath($Out)
New-Item -ItemType Directory -Force $Out | Out-Null
$tmp = Join-Path ([IO.Path]::GetTempPath()) ("pixelfx-" + [Guid]::NewGuid().ToString('N'))
$nodeArgs = @('tools/render.mjs', $Effect, '--all', '--sheet', '--no-actor', '--fps', "$Fps", '--out', $tmp)
if ($Seed -ne 0) { $nodeArgs += @('--seed', "$Seed") }
if ($Opts)       { $nodeArgs += @('--opts', $Opts.Replace('"', '\"')) }   # PowerShell 5.1 은 네이티브 인자의 " 를 벗겨낸다

Push-Location $pfx
try { & node @nodeArgs | Out-Null; $code = $LASTEXITCODE } finally { Pop-Location }
if ($code -ne 0) { throw "렌더 실패 (exit $code): $Effect" }

$leaf = $Effect.Split('/')[1]
$src  = Join-Path $tmp $Effect.Replace('/', '\')
Copy-Item (Join-Path $src 'contact.png') (Join-Path $Out 'motion_reference.png') -Force
$meta = (Get-Content (Join-Path $src "$leaf.json") -Raw -Encoding UTF8 | ConvertFrom-Json).meta.pixelfx
Remove-Item $tmp -Recurse -Force

[ordered]@{
    purpose  = '동작·타이밍 참고 전용 (화풍은 따르지 않음, 게임에 넣지 않음)'
    effect   = $Effect
    fps      = $meta.fps
    frames   = $meta.frames
    duration = [math]::Round($meta.frames / $meta.fps, 2)
    size     = $meta.frameSize
    origin   = $meta.pivot
    loop     = $meta.loop
    seed     = $Seed
    opts     = $Opts
    pixelfx  = "$($lock.version) $($lock.commit)"
} | ConvertTo-Json | Set-Content (Join-Path $Out 'motion_reference.json') -Encoding UTF8

Write-Output "FRAMES=$($meta.frames) FPS=$($meta.fps) SIZE=$($meta.frameSize -join 'x')"
Write-Output "REFERENCE=$(Join-Path $Out 'motion_reference.png')"
exit 0
