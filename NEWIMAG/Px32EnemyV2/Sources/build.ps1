param([string]$Only='')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -Path (Join-Path $PSScriptRoot 'Processor.cs') -ReferencedAssemblies System.Drawing
$root=Split-Path $PSScriptRoot -Parent
$rows=[EnemyProcessor]::Build($root,$Only)
$rows | ConvertTo-Json -Depth 12 | Set-Content (Join-Path $root 'validation.json') -Encoding UTF8
