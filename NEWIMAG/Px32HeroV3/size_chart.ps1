Add-Type -AssemblyName System.Drawing
$rootPath = $PSScriptRoot
$items = @(@('Idle',0),@('Run',0),@('Run',1),@('Run',2),@('Run',3),@('JumpStart',0),@('Fall',0),@('Land',0),@('Dash',0),@('Hit',0),@('Death',0))
$bmp = New-Object Drawing.Bitmap 4368,570
$g = [Drawing.Graphics]::FromImage($bmp)
$g.Clear([Drawing.Color]::FromArgb(39,42,49))
$g.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$g.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::Half
$font = New-Object Drawing.Font 'Consolas',19
$white = [Drawing.Brushes]::White
$gridPen = New-Object Drawing.Pen ([Drawing.Color]::FromArgb(81,86,99)),1
$headPen = New-Object Drawing.Pen ([Drawing.Color]::FromArgb(238,145,183)),2
$headPen.DashStyle = [Drawing.Drawing2D.DashStyle]::Dash
for($level=0;$level -le 128;$level+=32){
 $yy=96+(128-$level)*3
 $g.DrawLine($gridPen,144,$yy,4367,$yy)
 $g.DrawString([string]$level,$font,$white,15,($yy-12))
}
foreach($headY in @(35,75)){$g.DrawLine($headPen,144,(96+$headY*3),4367,(96+$headY*3))}
for($i=0;$i -lt $items.Count;$i++){
 $name=$items[$i][0];$frame=[int]$items[$i][1];$xx=144+$i*384
 $src=[Drawing.Image]::FromFile((Join-Path $rootPath "_1x/Hero_$name.png"))
 $dest=New-Object Drawing.Rectangle $xx,96,384,384
 $crop=New-Object Drawing.Rectangle ($frame*128),0,128,128
 $g.DrawImage($src,$dest,$crop,[Drawing.GraphicsUnit]::Pixel)
 $g.DrawString("$name $($frame+1)",$font,$white,($xx+12),510)
 $src.Dispose()
}
$g.DrawString('SIZE CHECK | 3x NEAREST | ruler: game px | pink: Idle face + bangs top/bottom | all feet aligned',$font,$white,144,24)
$bmp.Save((Join-Path $rootPath 'SizeCheck_3x.png'),[Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$bmp.Dispose();$font.Dispose();$gridPen.Dispose();$headPen.Dispose()
