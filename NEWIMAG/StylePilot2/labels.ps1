Add-Type -AssemblyName System.Drawing
$root = $PSScriptRoot
$bitmap = [System.Drawing.Bitmap]::new((Join-Path $root 'Compare_base.png'))
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::SingleBitPerPixelGridFit
$font = [System.Drawing.Font]::new('Consolas',9,[System.Drawing.FontStyle]::Regular,[System.Drawing.GraphicsUnit]::Pixel)
$brush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255,247,238,228))
$dark = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(235,25,24,31))
$pen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255,247,238,228),1)
$graphics.FillRectangle($dark,0,0,640,20)
$graphics.DrawString('Re:Trap / generated candidates / 32px = 1 cell',$font,$brush,8,5)
$graphics.FillRectangle($dark,0,157,640,35)
$labels = @(@('Current',92),@('A / 64x64',192),@('B / 96x96',303),@('C / 128x128',429),@('Goblin',560))
foreach($entry in $labels){$graphics.DrawString($entry[0],$font,$brush,[single]$entry[1],163)}
$graphics.DrawLine($pen,58,40,58,152)
foreach($entry in @(@('1',32),@('1.5',48),@('2',64),@('3',96))){
  $y=152-[int]$entry[1]
  $graphics.FillRectangle($dark,3,$y-5,43,12)
  $graphics.DrawString(($entry[0]+' cell'),$font,$brush,4,($y-4))
  $graphics.DrawLine($pen,51,$y,65,$y)
}
$graphics.DrawLine($pen,58,152,620,152)
$bitmap.Save((Join-Path $root 'Compare_labeled.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$pen.Dispose();$brush.Dispose();$dark.Dispose();$font.Dispose();$graphics.Dispose();$bitmap.Dispose()
