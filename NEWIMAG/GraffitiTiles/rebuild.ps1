$ErrorActionPreference = 'Stop'
# Run from repository root. All explicit writes remain inside this folder.
Add-Type -AssemblyName System.Drawing
Add-Type -Path NEWIMAG/GraffitiTiles/assemble.cs -ReferencedAssemblies System.Drawing
$map = Get-Content Assets/StreamingAssets/Maps/stage_01.json -Raw -Encoding UTF8 | ConvertFrom-Json
$slotList = New-Object 'System.Collections.Generic.List[int]'
foreach ($s in $map.trapSlots) {
    $slotList.Add($s.x)
    $slotList.Add($s.y)
    if ($s.anchor -eq 'Floor') { $slotList.Add(1) } else { $slotList.Add(2) }
}
$base = (Get-Location).Path
# Source copies are permanent, so regeneration is independent of CODEX_HOME.
$src = Join-Path $base 'NEWIMAG/GraffitiTiles/Sources'
[GraffitiAssembler]::Run($base, (Join-Path $src 'Floor_selected.png'), (Join-Path $src 'Wall_selected.png'), [int[]]$map.grid, $map.width, $map.height, $slotList.ToArray(), $map.spawnPoint.x, $map.spawnPoint.y)
