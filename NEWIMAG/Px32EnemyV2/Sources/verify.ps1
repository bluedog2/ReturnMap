$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=Split-Path $PSScriptRoot -Parent
$palette=@{}
Get-Content (Join-Path $root '../palette_v2.txt') | ForEach-Object { if($_ -match '^#([0-9A-Fa-f]{6})$'){$palette[$matches[1].ToUpper()]=1} }
$specs=@{Goblin_Raider=@(64,48);Imp_Flyer=@(64,44);Skeleton_Archer=@(96,72);Elite_Knight=@(96,84);Orc_Brute=@(128,96)}
$anims=@{Idle=4;Run=6;Hit=2;Death=6}
$results=@()
foreach($name in @('Goblin_Raider','Imp_Flyer','Skeleton_Archer','Elite_Knight','Orc_Brute')){
 foreach($anim in @('Idle','Run','Hit','Death')){
  $key=$name+'_'+$anim;$cell=$specs[$name][0];$n=$anims[$anim]
  $one=[Drawing.Bitmap]::new((Join-Path $root ('_1x/'+$key+'.png')))
  $large=[Drawing.Bitmap]::new((Join-Path $root ('Enemies/'+$key+'.png')))
  $gif=[Drawing.Image]::FromFile((Join-Path $root ('Animations/'+$key+'_3x.gif')))
  $dim=[Drawing.Imaging.FrameDimension]::new($gif.FrameDimensionsList[0])
  $errors=@();$colors=@{};$frames=@();$badAlpha=0;$outside=0;$edges=0
  if($one.Width-ne($cell*$n)-or$one.Height-ne$cell){$errors+='1x dimensions'}
  if($large.Width-ne($cell*$n*8)-or$large.Height-ne($cell*8)){$errors+='8x dimensions'}
  if($gif.Width-ne($cell*3)-or$gif.Height-ne($cell*3)-or$gif.GetFrameCount($dim)-ne$n){$errors+='gif dimensions/count'}
  $delays=@();$delayProp=$gif.GetPropertyItem(0x5100)
  for($f=0;$f-lt$n;$f++){
   [void]$gif.SelectActiveFrame($dim,$f)
   $delays += [BitConverter]::ToInt32($delayProp.Value,$f*4)
   $l=$cell;$r=-1;$t=$cell;$b=-1;$fc=@{};$fe=0
   for($y=0;$y-lt$cell;$y++){for($x=0;$x-lt$cell;$x++){
    $c=$one.GetPixel($f*$cell+$x,$y)
    if($c.A-ne0-and$c.A-ne255){$badAlpha++}
    if($c.A-gt0){
     $hex='{0:X2}{1:X2}{2:X2}'-f$c.R,$c.G,$c.B;$colors[$hex]=1;$fc[$hex]=1
     if(-not$palette.ContainsKey($hex)){$outside++}
     if($y-eq0-or(($x-eq0-or$x-eq($cell-1))-and$y-lt($cell-1))){$edges++;$fe++}
     $l=[Math]::Min($l,$x);$r=[Math]::Max($r,$x);$t=[Math]::Min($t,$y);$b=[Math]::Max($b,$y)
    }
   }}
   $frames += [pscustomobject]@{frame=$f+1;bottom_row=$b;horizontal_center=($l+$r)/2;vertical_center=($t+$b)/2;height=$b-$t+1;colors=$fc.Count;edge_pixels=$fe}
  }
  if($badAlpha-gt0){$errors+='alpha'};if($outside-gt0-or$colors.Count-gt20-or$colors.ContainsKey('000000')){$errors+='palette'};if($edges-gt0){$errors+='edge'}
  if($name-ne'Imp_Flyer'-and@($frames|Where-Object {$_.bottom_row-ne($cell-1)}).Count-gt0){$errors+='baseline'}
  $meanX=($frames|Measure-Object horizontal_center -Average).Average;$meanH=($frames|Measure-Object height -Average).Average
  if($anim-ne'Death'){
   if(@($frames|Where-Object {[Math]::Abs($_.horizontal_center-$meanX)-gt2-or[Math]::Abs($_.height-$meanH)-gt2}).Count-gt0){$errors+='drift'}
   if($name-eq'Imp_Flyer'-and@($frames|Where-Object {[Math]::Abs($_.vertical_center-$cell/2)-gt2}).Count-gt0){$errors+='flight center'}
  }
  if($anim-eq'Idle'-and[Math]::Abs($frames[0].height-$specs[$name][1])-gt2){$errors+='idle height'}
  $expectedDelay=if($anim-eq'Idle'){[Math]::Round($n*100/6)}else{$n*10}
  if(($delays|Measure-Object -Sum).Sum-ne$expectedDelay){$errors+='gif timing'}
  $results += [pscustomobject]@{sheet=$key;passed=($errors.Count-eq0);errors=$errors;colors=$colors.Count;palette_outside_pixels=$outside;nonbinary_alpha_pixels=$badAlpha;edge_pixels=$edges;gif_delays_centiseconds=$delays;frames=$frames}
  $one.Dispose();$large.Dispose();$gif.Dispose()
 }
}
$output=[pscustomobject]@{checked_from_saved_files=$true;sheet_count=$results.Count;all_passed=(@($results|Where-Object {-not$_.passed}).Count-eq0);sheets=$results}
$output|ConvertTo-Json -Depth 12|Set-Content (Join-Path $root 'Sources/independent_validation.json') -Encoding UTF8
$results|Select-Object sheet,passed,colors,errors|Format-Table -AutoSize
if(-not$output.all_passed){throw 'Independent saved-file validation failed'}
