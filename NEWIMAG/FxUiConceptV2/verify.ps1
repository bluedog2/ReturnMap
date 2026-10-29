$ErrorActionPreference='Stop'
$out=Join-Path (Get-Location) 'NEWIMAG/FxUiConceptV2'
Add-Type -AssemblyName System.Drawing
$specs=@{
 Fx_Dash=@(32,5);Fx_Hit=@(32,4);Fx_Death=@(48,6);Fx_Respawn=@(48,6);Fx_Dust=@(16,4);Fx_EnemyPoof=@(48,6)
 Gauge_Ring24=@(24,9);Gauge_Ring32=@(32,9);GoalMarker=@(32,4)
 Icon_Spike=@(32,1);Icon_ArrowShooter=@(32,1);Icon_DropHammer=@(32,1);Icon_Remove=@(32,1)
 Particle_Critical=@(8,1);Particle_Dud=@(8,1);Particle_Beneficial=@(8,1)
}
$palette=@(Get-Content NEWIMAG/palette_v2.txt | Where-Object {$_ -match '^#[0-9A-Fa-f]{6}$'} | ForEach-Object {[System.Drawing.ColorTranslator]::FromHtml($_).ToArgb()})
$results=@()
foreach($name in ($specs.Keys | Sort-Object)){
 $size,$count=$specs[$name]
 $b=[System.Drawing.Bitmap]::new("$out/_1x/$name.png")
 if($b.Width -ne $size*$count -or $b.Height -ne $size){throw "Size: $name"}
 $frameDir="$out/Frames/$name"
 New-Item -ItemType Directory -Force $frameDir | Out-Null
 for($i=0;$i -lt $count;$i++){
  $f=$b.Clone([System.Drawing.Rectangle]::new($i*$size,0,$size,$size),[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $f.Save(('{0}/{1:D2}.png' -f $frameDir,$i),[System.Drawing.Imaging.ImageFormat]::Png)
  if($name -eq 'Gauge_Ring32'){
   for($y=0;$y -lt 32;$y++){for($x=0;$x -lt 32;$x++){if((($x-15.5)*($x-15.5)+($y-15.5)*($y-15.5)) -lt 49 -and $f.GetPixel($x,$y).A -ne 0){throw 'Gauge center hole <14px'}}}
  }
  $f.Dispose()
 }
 $b.Dispose()
 $gif=[System.Drawing.Image]::FromFile("$out/Animations/${name}_4x.gif")
 $dim=[System.Drawing.Imaging.FrameDimension]::new($gif.FrameDimensionsList[0])
 if($gif.GetFrameCount($dim) -ne $count){throw "GIF frames: $name"}
 $gif.Dispose()
 $results+= "$name : cell ${size}x${size}, $count frames; PNG dimensions / GIF decode / count PASS"
}
foreach($path in @((Get-ChildItem "$out/_1x/*.png").FullName)+@("$out/Sheet_4x.png","$out/Scene_3x.png","$out/Hud_3x.png")){
 $b=[System.Drawing.Bitmap]::new($path)
 $colors=[System.Collections.Generic.HashSet[int]]::new()
 # Native-scale inspection is sufficient for the exact nearest-neighbor previews.
 $step=if($path.EndsWith('Sheet_4x.png')){4}elseif($path.EndsWith('Scene_3x.png') -or $path.EndsWith('Hud_3x.png')){3}else{1}
 for($y=0;$y -lt $b.Height;$y+=$step){for($x=0;$x -lt $b.Width;$x+=$step){
  $c=$b.GetPixel($x,$y)
  if($c.A -ne 0 -and $c.A -ne 255){throw "Alpha: $path"}
  if($c.A -eq 255){[void]$colors.Add($c.ToArgb());if($palette -notcontains $c.ToArgb()){throw "Palette: $path"}}
 }}
 if($colors.Count -gt 16){throw "Colors: $path"}
 $results+=([System.IO.Path]::GetFileName($path)+" : $($b.Width)x$($b.Height), $($colors.Count) colors, palette/alpha PASS")
 $b.Dispose()
}
$results | Set-Content "$out/verification.txt" -Encoding utf8

Write-Output 'PASS: dimensions, frame counts, GIF decoding, palette, alpha, 14px center clearance.'
