# Comparison annotations only: this script never draws character art.
Add-Type -AssemblyName System.Drawing
$root = Split-Path $PSScriptRoot -Parent
New-Item -ItemType Directory -Force (Join-Path $root '_tmp') | Out-Null
$repo = Split-Path (Split-Path $root -Parent) -Parent
$heroPath = Join-Path $repo 'NEWIMAG/Px32HeroV3/_1x/Hero_Idle.png'
$heroVersion = 'V3'
if (!(Test-Path -LiteralPath $heroPath)) { $heroPath = Join-Path $repo 'NEWIMAG/Px32HeroV2/_1x/Hero_Idle.png'; $heroVersion = 'V2' }
$hero = [System.Drawing.Bitmap]::FromFile($heroPath)
$canvas = [System.Drawing.Bitmap]::new(540,1020)
$g = [System.Drawing.Graphics]::FromImage($canvas)
$g.Clear([System.Drawing.Color]::FromArgb(42,40,45))
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::SingleBitPerPixelGridFit
$font = [System.Drawing.Font]::new('Consolas',9,[System.Drawing.FontStyle]::Regular,[System.Drawing.GraphicsUnit]::Pixel)
$brush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(231,216,206))
$accent = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(184,216,73))
$pen = [System.Drawing.Pen]::new($brush.Color,1)
$g.DrawString('RE:TRAP / V2 -> V3 / NATIVE PIXEL SCALE x3',$font,$brush,12,8)
$species = @('Goblin_Raider','Imp_Flyer','Skeleton_Archer','Elite_Knight','Orc_Brute')
for ($i=0; $i -lt $species.Length; $i++) {
 $s=$species[$i]; $top=28+$i*196; $floor=$top+164
 $g.DrawString($s,$font,$accent,12,$top)
 $g.DrawString('V2',$font,$brush,60,($top+14))
 $g.DrawString('V3',$font,$brush,235,($top+14))
 $g.DrawString(('HERO '+$heroVersion),$font,$brush,380,($top+14))
 $old=[System.Drawing.Bitmap]::FromFile((Join-Path $repo ('NEWIMAG/Px32EnemyV2/_1x/'+$s+'_Idle.png')))
 $new=[System.Drawing.Bitmap]::FromFile((Join-Path $root ('_1x/'+$s+'_Idle.png')))
 $images=@($old,$new,$hero); $centers=@(76,246,404)
 for($j=0;$j -lt 3;$j++) {
  $img=$images[$j]; $c=$img.Height; $x=[int]($centers[$j]-$c/2)
  $g.DrawImage($img,[System.Drawing.Rectangle]::new($x,($floor-$c+1),$c,$c),[System.Drawing.Rectangle]::new(0,0,$c,$c),[System.Drawing.GraphicsUnit]::Pixel)
 }
 $g.DrawLine($pen,14,($floor+2),466,($floor+2))
 $g.DrawLine($pen,478,($floor-160),478,$floor)
 for($tick=0;$tick -le 5;$tick++) { $y=$floor-$tick*32; $g.DrawLine($pen,474,$y,484,$y); $g.DrawString(([string]($tick*32)+'px'),$font,$brush,489,($y-4)) }
 $g.DrawString('32px / tile',$font,$brush,438,($floor+13))
 if($s -eq 'Imp_Flyer') {$g.DrawString('Imp: hover position preserved inside its cell',$font,$brush,12,($floor+14))}
 $old.Dispose();$new.Dispose()
}
$canvas.Save((Join-Path $root '_tmp/Compare_1x.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$canvas.Dispose();$hero.Dispose();$font.Dispose();$brush.Dispose();$accent.Dispose();$pen.Dispose()
