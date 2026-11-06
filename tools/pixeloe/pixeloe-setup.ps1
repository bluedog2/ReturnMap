# ═══════════════════════════════════════════════════════════════
#  pixeloe-setup.ps1 — PixelOE 를 고정 버전·CPU 로 저장소 밖 venv 에 설치
# ═══════════════════════════════════════════════════════════════
# tools\pixeloe\pixeloe.lock.json 의 버전으로 ..\pixeloe-venv 를 만들고 설치한다(저장소·Assets 에 Python 없음).
# torch·torchvision 은 CPU 빌드(lock 의 torchIndex). GPU 로 바꾸려면 lock 의 torch·torchvision·torchIndex 를 CUDA 빌드로 바꾸고 다시 실행.
# ASF 가 쓰는 시스템 Python(py -3.12)과 섞이지 않게 venv 를 따로 둔다.
#
# 사용:
#   tools\pixeloe\pixeloe-setup.ps1          # 없으면 만들고, 버전이 다르면 맞춤
#   tools\pixeloe\pixeloe-setup.ps1 -Check   # 확인만 (exit 0 일치 / 1 없음·불일치)
#
# 출력: PYTHON=<venv python 경로> · VERSION=… 줄

[CmdletBinding()]
param([switch]$Check)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$lock     = Get-Content (Join-Path $PSScriptRoot 'pixeloe.lock.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$venv     = [IO.Path]::GetFullPath((Join-Path $repoRoot $lock.venv))
$py       = Join-Path $venv 'Scripts\python.exe'
$pins     = @($lock.packages.PSObject.Properties | ForEach-Object { "$($_.Name)==$($_.Value)" })

# 설치된 버전이 lock 과 같은지: 다른 패키지 이름 목록을 돌려준다(비었으면 일치)
function Mismatch {
    if (-not (Test-Path $py)) { return @('venv') }
    $names = ($lock.packages.PSObject.Properties | ForEach-Object { $_.Name }) -join ' '
    $have = & $py -I -c "import importlib.metadata as m,sys
for n in sys.argv[1:]:
    try: print(n+'=='+m.version(n))
    except m.PackageNotFoundError: print(n+'==NONE')" $names.Split(' ')
    return @($pins | Where-Object { $have -notcontains $_ })
}

$bad = Mismatch
if ($Check) {
    if ($bad.Count) { Write-Output "MISMATCH=$($bad -join ',') VENV=$venv"; exit 1 }
} elseif ($bad.Count) {
    if (-not (Test-Path $py)) {
        & py "-$($lock.python)" -m venv $venv
        if ($LASTEXITCODE -ne 0) { throw "venv 생성 실패 (py -$($lock.python))" }
    }
    & $py -m pip install --quiet --disable-pip-version-check --upgrade pip
    $torch = @($pins | Where-Object { $_ -match '^torch(vision)?==' })
    & $py -m pip install --quiet --disable-pip-version-check @torch --index-url $lock.torchIndex
    if ($LASTEXITCODE -ne 0) { throw "torch 설치 실패 ($($torch -join ' '))" }
    & $py -m pip install --quiet --disable-pip-version-check @pins --extra-index-url $lock.torchIndex
    if ($LASTEXITCODE -ne 0) { throw "pixeloe 설치 실패" }
    $bad = Mismatch
    if ($bad.Count) { throw "설치 후에도 버전 불일치: $($bad -join ',')" }
}

# 불러오기 확인 (CPU 로 도는지)
$probe = & $py -I -c "import torch, pixeloe.torch.pixelize as p; print('TORCH='+torch.__version__+' CUDA='+str(torch.cuda.is_available()))"
if ($LASTEXITCODE -ne 0) { Write-Output 'IMPORT_FAILED'; exit 1 }
Write-Output "PYTHON=$py"
Write-Output $probe
Write-Output "VERSION=pixeloe $($lock.packages.pixeloe)"
exit 0
