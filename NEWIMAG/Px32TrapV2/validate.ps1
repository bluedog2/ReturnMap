$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=(Get-Location).Path
$dest=Join-Path $root 'NEWIMAG/Px32TrapV2'
$palette=@((Get-Content NEWIMAG/palette_v2.txt),(Get-Content NEWIMAG/TrapConceptV2/palette_add_traps.txt)) | ForEach-Object {$_} | Where-Object {$_ -match '^#[0-9a-fA-F]{6}$'} | ForEach-Object {$_.ToUpper()}
$banned=@('#334629','#58783B','#88AE43','#B8D849','#D8EC76','#38284D','#58377B','#8050AD','#B581D4')
$files=@(); $masks=@{}; $chains=@(); $failures=@()
foreach($f in (Get-ChildItem "$dest/Parts","$dest/Misc" -Filter *.png -Recurse | Sort-Object FullName)) {
 $b=[Drawing.Bitmap]::new($f.FullName); $rel=$f.FullName.Substring($dest.Length+1).Replace('\','/')
 $colors=@{}; $alphas=@{}; $rows=@(); $columns=@{}; $mask=''; $outside=@{}; $bad=@{}; $black=0; $opaque=0
 for($y=0;$y -lt $b.Height;$y++){ $row=0; for($x=0;$x -lt $b.Width;$x++){ $c=$b.GetPixel($x,$y); $alphas[[int]$c.A]=1; $mask+=([int]($c.A -eq 255)).ToString(); if($c.A -gt 0){$hex='#{0:X2}{1:X2}{2:X2}' -f $c.R,$c.G,$c.B; $colors[$hex]=1; if($palette -notcontains $hex){$outside[$hex]=1}; if($banned -contains $hex){$bad[$hex]=1}; if($hex -eq '#000000'){$black++}; $row++;$opaque++;$columns[$x]=1} }; $rows+=,$row }
 $ew=32;$eh=32; if($f.BaseName -eq 'arrow'){$ew=16;$eh=8}; if($f.BaseName -eq 'muzzle_flash'){$ew=16;$eh=16}; if($f.BaseName -eq 'chain'){$ew=8;$eh=8}; if($f.BaseName -eq 'impact'){$ew=32;$eh=16}
 $occupied=@(0..($b.Height-1)|Where-Object {$rows[$_] -gt 0})
 $entry=[ordered]@{file=$rel;width=$b.Width;height=$b.Height;expected_size=@($ew,$eh);size_ok=($b.Width -eq $ew -and $b.Height -eq $eh);color_count=$colors.Count;outside_palette_color_count=$outside.Count;forbidden_faction_color_count=$bad.Count;pure_black_pixels=$black;alpha_values=@($alphas.Keys|Sort-Object);top_opaque_row=$occupied[0];bottom_opaque_row=$occupied[-1];opaque_pixels=$opaque;fully_opaque=($opaque -eq $b.Width*$b.Height);top_row_opaque_pixels=$rows[0];bottom_row_opaque_pixels=$rows[-1]}
 if(!$entry.size_ok -or $colors.Count -gt 16 -or $outside.Count -gt 0 -or $bad.Count -gt 0 -or $black -gt 0 -or @($alphas.Keys|Where-Object {$_ -notin @(0,255)}).Count -gt 0){$failures+=$rel}
 $files+=,[pscustomobject]$entry
 if($rel.StartsWith('Parts/')){$segments=$rel.Split('/');$group=$segments[1]+'/'+$segments[3];if(!$masks.ContainsKey($group)){$masks[$group]=@{}};$masks[$group][$segments[2]]=$mask}
 if($f.BaseName -eq 'chain'){$same=$true;$connection=$false;for($x=0;$x -lt 8;$x++){if($b.GetPixel($x,0).ToArgb() -ne $b.GetPixel($x,7).ToArgb()){$same=$false};if($b.GetPixel($x,0).A -eq 255 -and $b.GetPixel($x,7).A -eq 255){$connection=$true}};$cs=@($columns.Keys|Sort-Object);$chains+=,[ordered]@{file=$rel;top_has_opaque=($rows[0] -gt 0);bottom_has_opaque=($rows[7] -gt 0);rgba_seam_identical=$same;vertical_connection=$connection;link_width=($cs[-1]-$cs[0]+1);centered=($cs[0]+$cs[-1] -eq 7)};if(!$same -or !$connection){$failures+=$rel+': seam'}}
 $b.Dispose()
}
$silhouettes=@();foreach($k in ($masks.Keys|Sort-Object)){$match=(@($masks[$k].Values|Select-Object -Unique).Count -eq 1);$silhouettes+=,[ordered]@{part=$k;state_count=$masks[$k].Count;identical_alpha_and_position=$match};if(!$match){$failures+=$k+': state silhouettes'}}
$tips=@($files|Where-Object {$_.file -match '/spikes.png$'}|ForEach-Object {$_.top_opaque_row})
$checks=[ordered]@{all_spike_tips_at_row_zero=(@($tips|Where-Object {$_ -ne 0}).Count -eq 0);spike_rods_end_at_row_20=(@($files|Where-Object {$_.file -match '/spikes.png$' -and $_.bottom_opaque_row -ne 20}).Count -eq 0);bases_fully_opaque=(@($files|Where-Object {$_.file -match '/base.png$' -and !$_.fully_opaque}).Count -eq 0);platforms_fully_opaque=(@($files|Where-Object {$_.file -match '/platform.png$' -and !$_.fully_opaque}).Count -eq 0);heads_bottom_aligned=(@($files|Where-Object {$_.file -match '/head.png$' -and $_.bottom_opaque_row -ne 31}).Count -eq 0);mounts_top_aligned=(@($files|Where-Object {$_.file -match '/mount.png$' -and $_.top_opaque_row -ne 0}).Count -eq 0);glow_within_top_16_rows=(@($files|Where-Object {$_.file -match '/glow.png$' -and $_.bottom_opaque_row -gt 15}).Count -eq 0);impacts_bottom_aligned=(@($files|Where-Object {$_.file -match '/impact.png$' -and $_.bottom_opaque_row -ne 15}).Count -eq 0);seal_fully_opaque=($files|Where-Object {$_.file -eq 'Misc/SlotSeal.png'}).fully_opaque}
foreach($k in $checks.Keys){if(!$checks[$k]){$failures+=$k}}
$shooters=@();foreach($s in @('Normal','Dud','Critical','Beneficial')){$b=[Drawing.Bitmap]::new("$dest/Parts/arrow_shooter/$s/body.png");$ys=@(0..31|Where-Object {$b.GetPixel(31,$_).A -eq 255});$shooters+=,[ordered]@{state=$s;right_edge_opaque_rows=$ys;muzzle_mid_y=(($ys[0]+$ys[-1])/2);touches_right_edge=($ys.Count -gt 0)};$b.Dispose()}
$report=[ordered]@{scope='Parts/**/*.png + Misc/*.png only. Preview and retained generation sources excluded.';file_count=$files.Count;all_pass=($failures.Count -eq 0 -and $files.Count -eq 49);max_color_count=($files.color_count|Measure-Object -Maximum).Maximum;failures=$failures;placement_checks=$checks;state_silhouettes=$silhouettes;chain_checks=$chains;shooter_checks=$shooters;files=$files}
$report|ConvertTo-Json -Depth 12|Set-Content "$dest/validation.json" -Encoding UTF8
[pscustomobject]$report|Select-Object file_count,all_pass,max_color_count,failures|ConvertTo-Json -Depth 4
