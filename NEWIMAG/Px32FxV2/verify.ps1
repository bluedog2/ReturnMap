$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=Join-Path (Get-Location) 'NEWIMAG/Px32FxV2'
$palette=@(Get-Content NEWIMAG/palette_v2.txt | Where-Object {$_ -match '^#[0-9a-fA-F]{6}$'} | ForEach-Object {$_.ToUpper()})
$specs=@(@('Fx_Dash',32,5,$true),@('Fx_Hit',32,4,$false),@('Fx_Death',48,6,$false),@('Fx_Respawn',48,6,$true),@('Fx_Dust',16,4,$true),@('Fx_EnemyPoof',48,6,$false),@('GoalMarker',32,4,$true),@('Gauge_Ring24',24,9,$false),@('Gauge_Ring32',32,9,$false),@('Gauge_Ring24_Background',24,1,$false),@('Gauge_Ring32_Background',32,1,$false),@('Icon_Spike',32,1,$false),@('Icon_ArrowShooter',32,1,$false),@('Icon_DropHammer',32,1,$false),@('Icon_Remove',32,1,$false),@('Particle_Critical',8,1,$false),@('Particle_Dud',8,1,$false),@('Particle_Beneficial',8,1,$false))
$reports=@();$ok=$true
foreach($s in $specs){
 $name=$s[0];$size=[int]$s[1];$count=[int]$s[2];$bottom=[bool]$s[3];$dir=if($count -gt 1){'Effects'}else{'UI'}
 $b=[Drawing.Bitmap]::FromFile("$root/$dir/$name.png")
 $colors=@{};$alphas=@{};$outside=0;$black=0;$edges=0;$low=@();$single=0;$hole=0
 for($i=0;$i -lt $count;$i++){$last=-1
 for($y=0;$y -lt $size;$y++){for($x=0;$x -lt $size;$x++){
  $c=$b.GetPixel($i*$size+$x,$y);$alphas[[int]$c.A]=1
  if($c.A -eq 0){continue};$last=$y
  $hex='#{0:X2}{1:X2}{2:X2}' -f $c.R,$c.G,$c.B;$colors[$hex]=1
  if($palette -notcontains $hex){$outside++};if($hex -eq '#000000'){$black++}
  if($x -eq 0 -or $x -eq $size-1 -or $y -eq 0 -or (!$bottom -and $y -eq $size-1)){$edges++}
  if($name -like 'Gauge_Ring32*' -and (($x+0.5-16)*($x+0.5-16)+($y+0.5-16)*($y+0.5-16)) -lt 49){$hole++}
  $neighbors=0
  for($dy=-1;$dy -le 1;$dy++){for($dx=-1;$dx -le 1;$dx++){if(($dx -eq 0 -and $dy -eq 0) -or $x+$dx -lt 0 -or $x+$dx -ge $size -or $y+$dy -lt 0 -or $y+$dy -ge $size){continue};if($b.GetPixel($i*$size+$x+$dx,$y+$dy).A -gt 0){$neighbors++}}}
  if($neighbors -eq 0){$single++}
 }};$low+=,$last}
 $pass=$b.Width -eq $size*$count -and $b.Height -eq $size -and $colors.Count -le 16 -and $outside -eq 0 -and $black -eq 0 -and $edges -eq 0 -and $hole -eq 0 -and $single -eq 0 -and @($alphas.Keys | Where-Object {$_ -notin @(0,255)}).Count -eq 0
 if($bottom -and @($low | Where-Object {$_ -ne $size-1}).Count -gt 0){$pass=$false}
 $r=[ordered]@{file="$dir/$name.png";width=$b.Width;height=$b.Height;cell=@($size,$size);frames=$count;visible_color_count=$colors.Count;colors=@($colors.Keys|Sort-Object);out_of_palette_pixels=$outside;alpha_values=@($alphas.Keys|Sort-Object);pure_black_pixels=$black;edge_violations=$edges;bottom_aligned=$bottom;lowest_opaque_row_per_frame=$low;isolated_1px_components_8_connected=$single;ring32_center_diameter14_violations=$hole;pass=$pass}
 $reports+=$r;$ok=$ok -and $pass;$b.Dispose()
}
$bgMatch=@{}
foreach($size in @(24,32)){$sheet=[Drawing.Bitmap]::FromFile("$root/Effects/Gauge_Ring$size.png");$bg=[Drawing.Bitmap]::FromFile("$root/UI/Gauge_Ring${size}_Background.png");$match=$true;for($y=0;$y -lt $size;$y++){for($x=0;$x -lt $size;$x++){if($sheet.GetPixel($x,$y).ToArgb() -ne $bg.GetPixel($x,$y).ToArgb()){$match=$false}}};$bgMatch["Ring$size"]=$match;$ok=$ok -and $match;$sheet.Dispose();$bg.Dispose()}
[ordered]@{all_pass=$ok;asset_count=$reports.Count;color_count_excludes_transparency=$true;preview_exempt_from_asset_palette_and_1x_rules=$true;background_matches_zero_frame=$bgMatch;files=$reports} | ConvertTo-Json -Depth 8 | Set-Content "$root/validation.json" -Encoding UTF8
$reports | Select-Object file,visible_color_count,edge_violations,isolated_1px_components_8_connected,pass | Format-Table
if(!$ok){throw 'Validation failed'}
