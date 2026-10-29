$ErrorActionPreference='Stop'
$out=Join-Path (Get-Location) 'NEWIMAG/Px32FxV2'
New-Item -ItemType Directory -Force "$out/Effects","$out/UI","$out/.tmp" | Out-Null
$oldTemp=$env:TEMP; $oldTmp=$env:TMP
$env:TEMP="$out/.tmp"; $env:TMP="$out/.tmp"
Add-Type -AssemblyName System.Drawing
$code=@'
using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;
public static class FinalFx {
static string root,src,dst;
static Color H(string h){return ColorTranslator.FromHtml(h);}
static Bitmap B(int w,int h){return new Bitmap(w,h,PixelFormat.Format32bppArgb);}
static Bitmap Read(string p){using(var b=new Bitmap(p))return new Bitmap(b);}
static void Paste(Bitmap d,Bitmap s,int dx,int dy){for(int y=0;y<s.Height;y++)for(int x=0;x<s.Width;x++)if(x+dx>=0&&x+dx<d.Width&&y+dy>=0&&y+dy<d.Height&&s.GetPixel(x,y).A==255)d.SetPixel(x+dx,y+dy,s.GetPixel(x,y));}
static Rectangle Box(Bitmap b){int l=b.Width,t=b.Height,r=-1,k=-1;for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++)if(b.GetPixel(x,y).A>0){l=Math.Min(l,x);r=Math.Max(r,x);t=Math.Min(t,y);k=Math.Max(k,y);}return Rectangle.FromLTRB(l,t,r+1,k+1);}
static Bitmap Fit(Bitmap b,bool bottom){var q=Box(b);var n=B(b.Width,b.Height);int dx=(b.Width-q.Width)/2-q.X,dy=bottom?b.Height-q.Bottom:(b.Height-q.Height)/2-q.Y;Paste(n,b,dx,dy);for(int y=0;y<n.Height;y++)for(int x=0;x<n.Width;x++)if(x==0||x==n.Width-1||y==0||(!bottom&&y==n.Height-1))n.SetPixel(x,y,Color.Transparent);return n;}
static int Clean(Bitmap b){var singles=new List<Point>();for(int y=1;y<b.Height;y++)for(int x=1;x<b.Width-1;x++)if(b.GetPixel(x,y).A==255){bool neighbor=false;for(int j=-1;j<=1;j++)for(int i=-1;i<=1;i++)if((i!=0||j!=0)&&y+j>=0&&y+j<b.Height&&b.GetPixel(x+i,y+j).A==255)neighbor=true;if(!neighbor)singles.Add(new Point(x,y));}foreach(var p in singles){int nx=p.X<b.Width/2?p.X+1:p.X-1;b.SetPixel(nx,p.Y,b.GetPixel(p.X,p.Y));}return singles.Count;}
static Bitmap Dash(Bitmap b){var n=B(32,32);var box=Box(b);int dx=30-(box.Right-1);Paste(n,b,dx,0);
// Copy only existing horizontal crimson streak fragments towards the left.
int used=0,lastY=-10;for(int y=1;y<31&&used<2;y++){if(y-lastY<3)continue;int left=-1,right=-1;for(int x=1;x<30;x++){var c=n.GetPixel(x,y);if(c.A==255&&c.R>c.G*1.4&&c.R>c.B*1.1){if(left<0)left=x;right=x;}}if(left<0||right-left<1)continue;var fragment=new List<Color>();for(int x=left;x<=right;x++){var c=n.GetPixel(x,y);if(c.A==255&&c.R>c.G*1.4)fragment.Add(c);else break;}if(fragment.Count<2)continue;for(int x=1;x<left;x++)if(n.GetPixel(x,y).A==0)n.SetPixel(x,y,fragment[0]);used++;lastY=y;}return n;}
static List<Bitmap> Ring(int size){using(var sheet=Read(Path.Combine(src,"Gauge_Ring"+size+".png"))){var full=sheet.Clone(new Rectangle(size*8,0,size,size),PixelFormat.Format32bppArgb);if(size==24){var thick=new Bitmap(full);for(int y=1;y<size-1;y++)for(int x=1;x<size-1;x++){var c=full.GetPixel(x,y);if(c.A==0)continue;int dx=x<size/2?1:-1,dy=y<size/2?1:-1;thick.SetPixel(x+dx,y,c);thick.SetPixel(x,y+dy,c);thick.SetPixel(x+dx,y+dy,c);}full=thick;
var light=B(size,size);for(int y=1;y<size-1;y++)for(int x=1;x<size-1;x++)if(full.GetPixel(x,y).A>0){bool edge=full.GetPixel(x-1,y).A==0||full.GetPixel(x+1,y).A==0||full.GetPixel(x,y-1).A==0||full.GetPixel(x,y+1).A==0;var old=full.GetPixel(x,y);light.SetPixel(x,y,edge?H("#19181F"):H(old.R>=180&&y<=3?"#F7EEE4":old.R>=180?"#F18490":"#DC526B"));}full=light;}
var bg=B(size,size);for(int y=0;y<size;y++)for(int x=0;x<size;x++){var c=full.GetPixel(x,y);if(c.A>0)bg.SetPixel(x,y,H(c.R<50?"#19181F":"#3D3940"));}bg.Save(Path.Combine(dst,"UI/Gauge_Ring"+size+"_Background.png"));var frames=new List<Bitmap>();for(int s=0;s<=8;s++){var f=new Bitmap(bg);for(int y=0;y<size;y++)for(int x=0;x<size;x++){double a=(Math.Atan2(x+.5-size/2.0,size/2.0-y-.5)+2*Math.PI)%(2*Math.PI);if(a<s*Math.PI/4&&full.GetPixel(x,y).A>0)f.SetPixel(x,y,full.GetPixel(x,y));}frames.Add(f);}return frames;}}
static void Save(string name,List<Bitmap> frames,string dir){var b=B(frames[0].Width*frames.Count,frames[0].Height);for(int i=0;i<frames.Count;i++)Paste(b,frames[i],i*frames[i].Width,0);b.Save(Path.Combine(dst,dir+"/"+name+".png"),ImageFormat.Png);}
public static void Run(string r){root=r;src=Path.Combine(r,"NEWIMAG/FxUiConceptV2/_1x");dst=Path.Combine(r,"NEWIMAG/Px32FxV2");var names=new[]{"Fx_Dash","Fx_Hit","Fx_Death","Fx_Respawn","Fx_Dust","Fx_EnemyPoof","GoalMarker","Icon_Spike","Icon_ArrowShooter","Icon_DropHammer","Icon_Remove","Particle_Critical","Particle_Dud","Particle_Beneficial"};var sizes=new[]{32,32,48,48,16,48,32,32,32,32,32,8,8,8};var log=new List<string>();for(int i=0;i<names.Length;i++){string name=names[i];int size=sizes[i];bool bottom=name=="Fx_Dash"||name=="Fx_Respawn"||name=="Fx_Dust"||name=="GoalMarker";using(var sheet=Read(Path.Combine(src,name+".png"))){var fs=new List<Bitmap>();for(int j=0;j<sheet.Width/size;j++){var f=sheet.Clone(new Rectangle(j*size,0,size,size),PixelFormat.Format32bppArgb);if(name=="GoalMarker"){var aligned=B(size,size);Paste(aligned,f,0,size-Box(f).Bottom);f=aligned;}else f=Fit(f,bottom);if(name=="Fx_Dash")f=Dash(f);int count=Clean(f);log.Add(name+" frame "+j+": singleton copies "+count);fs.Add(f);}Save(name,fs,i<7?"Effects":"UI");}}Save("Gauge_Ring24",Ring(24),"Effects");Save("Gauge_Ring32",Ring(32),"Effects");File.WriteAllLines(Path.Combine(dst,"pixel_operations.txt"),log.ToArray());Preview();}
static void Label(Bitmap b,string text,int x,int y){using(var g=Graphics.FromImage(b))using(var f=new Font("Consolas",12,FontStyle.Regular,GraphicsUnit.Pixel))using(var brush=new SolidBrush(H("#F7EEE4"))){g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;g.DrawString(text,f,brush,x,y);}}
static void Preview(){var files=Directory.GetFiles(Path.Combine(dst,"Effects"),"*.png").Concat(Directory.GetFiles(Path.Combine(dst,"UI"),"*.png")).ToList();int height=files.Sum(p=>{using(var b=Read(p))return b.Height*4+32;})+24+2*(32*4+32);var canvas=B(1408,height);using(var g=Graphics.FromImage(canvas))g.Clear(H("#1A191F"));int yy=12;foreach(var p in files){using(var b=Read(p)){Label(canvas,Path.GetFileName(p),8,yy+8);for(int y=0;y<b.Height*4;y++)for(int x=0;x<b.Width*4;x++){var c=b.GetPixel(x/4,y/4);if(c.A>0)canvas.SetPixel(248+x,yy+y,c);}yy+=b.Height*4+32;}}using(var tile=Read(Path.Combine(root,"Assets/_Project/ResourcceEX/Sprites/Px32/Tiles/Tile_Play_Floor_A.png"))){foreach(int size in new[]{24,32})using(var b=Read(Path.Combine(dst,"Effects/Gauge_Ring"+size+".png"))){Label(canvas,"Ring"+size+" / concrete",8,yy+8);for(int y=0;y<size*4;y++)for(int x=0;x<b.Width*4;x++){var c=b.GetPixel(x/4,y/4);canvas.SetPixel(248+x,yy+y,c.A>0?c:tile.GetPixel((x/4)%tile.Width,(y/4)%tile.Height));}yy+=size*4+32;}}canvas.Save(Path.Combine(dst,"Preview_4x.png"));}
}
'@
try { Add-Type -TypeDefinition $code -ReferencedAssemblies System.Drawing; [FinalFx]::Run((Get-Location).Path) } finally { $env:TEMP=$oldTemp; $env:TMP=$oldTmp }




