Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$manifest = Get-Content -LiteralPath (Join-Path $root 'generation.json') -Raw | ConvertFrom-Json
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
public class BackgroundProcess {
 public static void Run(string src,string dst,bool near,bool warm) {
  using(var s=new Bitmap(src)) using(var o=new Bitmap(1536,1024,PixelFormat.Format32bppArgb)) {
   int top=1024,bottom=0;
   if(near) for(int y=0;y<1024;y+=4) for(int x=0;x<1536;x+=4) if(s.GetPixel(x,y).A>=192) {top=Math.Min(top,y);bottom=Math.Max(bottom,y+4);break;}
   Color[,] p=new Color[384,256];
   for(int y=0;y<256;y++) for(int x=0;x<384;x++) {
    if(near && y<141){p[x,y]=Color.Transparent;continue;}
    int sy=near?top+(int)((y-141+0.5)*(bottom-top)/115.0):y*4+2;
    var c=s.GetPixel(x*4+2,Math.Min(1023,sy));
    if(near && c.A<192){p[x,y]=Color.Transparent;continue;}
    double gray=c.R*.299+c.G*.587+c.B*.114;
    double sat=warm?.72:.85;
    double contrast=(!near&&warm)?.72:1.0;
    double anchor=140;
    int r=Q(((gray+(c.R-gray)*sat)-anchor)*contrast+anchor);
    int g=Q(((gray+(c.G-gray)*sat)-anchor)*contrast+anchor);
    int b=Q(((gray+(c.B-gray)*sat)-anchor)*contrast+anchor);
    p[x,y]=Color.FromArgb(255,r,g,b);
   }
   // Discrete palette transition within 128 pixels of either edge.
   for(int y=0;y<256;y++) {
    for(int i=0;i<32;i++) { var a=p[i,y];var b=p[383-i,y];
    var mid=Color.FromArgb((a.A+b.A)>=255?255:0,Q((a.R+b.R)/2.0),Q((a.G+b.G)/2.0),Q((a.B+b.B)/2.0));
     double t=(32-i)/32.0;
     p[i,y]=Mix(p[i,y],mid,t);p[383-i,y]=Mix(p[383-i,y],mid,t);
    }
   }
   for(int y=0;y<256;y++) for(int x=0;x<384;x++) for(int yy=0;yy<4;yy++) for(int xx=0;xx<4;xx++)o.SetPixel(x*4+xx,y*4+yy,p[x,y]);
   o.Save(dst,ImageFormat.Png);
  }
 }
 static int Q(double v){return Math.Max(0,Math.Min(255,(int)Math.Round(v/12)*12));}
 static Color Mix(Color a,Color b,double t){int alpha=a.A*(1-t)+b.A*t>=127.5?255:0;return alpha==0?Color.FromArgb(0,0,0,0):Color.FromArgb(alpha,Q(a.R*(1-t)+b.R*t),Q(a.G*(1-t)+b.G*t),Q(a.B*(1-t)+b.B*t));}
 public static string Check(string path,bool near) {
  using(var b=new Bitmap(path)) {
   int diff=0,top=0,partial=0,min=255,max=0,first=1024;bool grid=true;
   for(int y=0;y<b.Height;y++) for(int x=0;x<b.Width;x++) {
    var c=b.GetPixel(x,y);min=Math.Min(min,c.A);max=Math.Max(max,c.A);
    if(c.A>0)first=Math.Min(first,y);
    if(y<564&&c.A!=0)top++;
    if(c.A!=0&&c.A!=255)partial++;
    if(c.ToArgb()!=b.GetPixel(x-x%4,y-y%4).ToArgb())grid=false;
   }
   for(int y=0;y<b.Height;y++)if(b.GetPixel(0,y).ToArgb()!=b.GetPixel(b.Width-1,y).ToArgb())diff++;
   if(b.Width!=1536||b.Height!=1024||diff!=0||partial!=0||!grid||(near&&top!=0)||(!near&&min!=255))throw new Exception("Validation failed: "+path);
   return "{\"width\":1536,\"height\":1024,\"alpha_min\":"+min+",\"alpha_max\":"+max+",\"partial_alpha_pixels\":"+partial+",\"top_564_nontransparent_pixels\":"+top+",\"first_visible_row\":"+first+",\"left_right_different_rows\":"+diff+",\"uniform_4x4_blocks\":true}";
  }
 }
}
'@
foreach($j in $manifest.images) {
 $dst=Join-Path $root $j.file
 [void][System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($dst))
 [BackgroundProcess]::Run($j.source,$dst,$j.transparent,$j.file.Contains('Verification'))
 $check=[BackgroundProcess]::Check($dst,$j.transparent) | ConvertFrom-Json
 $j | Add-Member -Force NoteProperty postprocessing '4x4 nearest point sampling; RGB saturation reduction (Verification 0.72, Play 0.85); Verification far contrast 0.72 around 140; RGB channels quantized to multiples of 12; near alpha threshold 192, visible vertical bounds (top and bottom) fitted into rows 564..1023; 128px edge transition to paired edge colors, quantized with binary alpha; transparent RGB zeroed.'
 $j | Add-Member -Force NoteProperty validation $check
 Write-Output ($j.file + ': ' + ($check | ConvertTo-Json -Compress))
}
$manifest | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $root 'generation.json') -Encoding UTF8

