$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$src = @'
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
public static class BgConcept {
 static string root, dest;
 static Color H(string s){return ColorTranslator.FromHtml(s);}
 static Bitmap Read(string p){return new Bitmap(Path.Combine(root,p));}
 static Color Closest(Color c,Color[] pal){int best=int.MaxValue; Color result=pal[0];foreach(var q in pal){int dr=c.R-q.R,dg=c.G-q.G,db=c.B-q.B;int d=dr*dr+dg*dg+db*db;if(d<best){best=d;result=q;}}return result;}
 static Bitmap Scale(Bitmap a,int w,int h){var b=new Bitmap(w,h,PixelFormat.Format32bppArgb);for(int y=0;y<h;y++)for(int x=0;x<w;x++)b.SetPixel(x,y,a.GetPixel(Math.Min(a.Width-1,(int)((x+.5)*a.Width/w)),Math.Min(a.Height-1,(int)((y+.5)*a.Height/h))));return b;}
 static void Save(Bitmap b,string n){b.Save(Path.Combine(dest,n),ImageFormat.Png);}
 static void Paste(Bitmap b,Bitmap a,int x,int y){for(int j=0;j<a.Height;j++)for(int i=0;i<a.Width;i++){int xx=x+i,yy=y+j;if(xx>=0&&yy>=0&&xx<b.Width&&yy<b.Height){var c=a.GetPixel(i,j);if(c.A>=128)b.SetPixel(xx,yy,Color.FromArgb(255,c.R,c.G,c.B));}}}
 static Bitmap Crop(Bitmap a,int w,int h){return a.Clone(new Rectangle(0,0,w,h),PixelFormat.Format32bppArgb);}
 static Bitmap Normalize(string name,bool near,string[] colors){
  var pal=colors.Select(H).ToArray();var raw=Read("NEWIMAG/BgConceptV2/Sources/"+name+"_raw.png");var small=Scale(raw,640,near?256:360);raw.Dispose();
  // Palette cleanup only. No procedural scene geometry or replacement artwork.
  var daySource=new[]{"#29323B","#323D46","#3D4850","#48535A","#535D62","#5E6668","#303940","#3B454C","#47525A","#641F36"}.Select(H).ToArray();
  for(int y=0;y<small.Height;y++)for(int x=0;x<640;x++){var c=small.GetPixel(x,y);if(name=="Verification_Far"){c=Closest(c,daySource);if(!(c.R>c.G*1.5&&c.R>c.B*1.3))c=Color.FromArgb(c.A,(int)(c.R*.67),(int)(c.G*.67),(int)(c.B*.67));}small.SetPixel(x,y,near&&c.A<128?Color.FromArgb(0,0,0,0):Closest(c,pal));}
  // Cross-align only eight columns at either horizontal boundary; quantize blends.
  var before=new Bitmap(small);for(int y=0;y<small.Height;y++){var left=before.GetPixel(0,y);var right=before.GetPixel(639,y);var common=Closest(Color.FromArgb((left.R+right.R)/2,(left.G+right.G)/2,(left.B+right.B)/2),pal);bool opaque=!near||(left.A+right.A>=255);
   for(int k=0;k<8;k++){double t=(8-k)/8.0;foreach(int x in new[]{k,639-k}){var c=before.GetPixel(x,y);bool a=k==0?opaque:c.A>=128;if(!a){small.SetPixel(x,y,Color.FromArgb(0,0,0,0));continue;}var mix=Color.FromArgb((int)Math.Round(c.R*(1-t)+common.R*t),(int)Math.Round(c.G*(1-t)+common.G*t),(int)Math.Round(c.B*(1-t)+common.B*t));small.SetPixel(x,y,Closest(mix,pal));}}
  }before.Dispose();Save(small,"_1x/"+name+".png");return small;
 }
 static void Mock(string phase,string file,Bitmap far,Bitmap near){var b=new Bitmap(far);Paste(b,near,0,104);string tiles="Assets/_Project/ResourcceEX/Sprites/Px32/Tiles/Tile_"+phase+"_";
  for(int row=0;row<2;row++)for(int col=0;col<20;col++){string suffix=row==0?new[]{"Floor_A","Floor_B","Floor_C"}[col%3]:"Wall_A";Paste(b,Read(tiles+suffix+".png"),col*32,296+row*32);}
  foreach(var platform in new[]{new[]{64,200,3},new[]{288,168,3},new[]{480,224,3}})for(int i=0;i<platform[2];i++)Paste(b,Read(tiles+(i==1?"Floor_L1R2":"Floor_A")+".png"),platform[0]+i*32,platform[1]);
  Paste(b,Crop(Read("NEWIMAG/Px32HeroV2/_1x/Hero_Idle.png"),96,96),160,200);
  Paste(b,Crop(Read("NEWIMAG/Px32EnemyV2/_1x/Goblin_Raider_Idle.png"),64,64),464,232);
  Paste(b,Read("NEWIMAG/Px32TrapV2/Parts/spike/Normal/spikes.png"),352,284);
  Paste(b,Read("NEWIMAG/Px32TrapV2/Parts/spike/Normal/base.png"),352,296);
  Save(Scale(b,1280,720),file);b.Dispose();
 }
 static void Label(Bitmap b,string text,int y){using(var g=Graphics.FromImage(b))using(var f=new Font("Consolas",12,FontStyle.Bold,GraphicsUnit.Pixel))using(var brush=new SolidBrush(H("#D0BEB8"))){g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;g.DrawString(text,f,brush,8,y);}}
 public static void Run(string r){root=r;dest=Path.Combine(root,"NEWIMAG/BgConceptV2");
  var pf=Normalize("Play_Far",false,new[]{"#191F2B","#202735","#272D3B","#302B36","#19181F","#641F36"});
  var pn=Normalize("Play_Near",true,new[]{"#19181F","#202735","#272D3B","#302B36","#38313D","#2A282D","#3D3940","#303940","#3B454C","#641F36"});
  var vf=Normalize("Verification_Far",false,new[]{"#202735","#29323B","#323D46","#3D4850","#303940","#3B454C","#641F36"});
  var vn=Normalize("Verification_Near",true,new[]{"#29323B","#323D46","#3D4850","#48535A","#303940","#3B454C","#47525A","#2A282D","#3D3940","#641F36"});
  Mock("Play","Play_Mock_2x.png",pf,pn);Mock("Build","Build_Mock_2x.png",vf,vn);
  var layers=new[]{pf,pn,vf,vn};var names=new[]{"PLAY / FAR","PLAY / NEAR","VERIFICATION / FAR","VERIFICATION / NEAR"};int total=layers.Sum(b=>b.Height+24);var seam=new Bitmap(1280,total);using(var g=Graphics.FromImage(seam))g.Clear(H("#19181F"));int yy=0;for(int i=0;i<4;i++){Label(seam,names[i]+"   |   640 + 640 px   |   join at center",yy+4);Paste(seam,layers[i],0,yy+24);Paste(seam,layers[i],640,yy+24);yy+=layers[i].Height+24;}Save(Scale(seam,2560,total*2),"Seam_2x.png");
  var report=new List<string>();for(int i=0;i<4;i++){var b=layers[i];var unique=new HashSet<int>();var alpha=new HashSet<int>();int mismatch=0,red=0,visible=0;double lum=0;for(int y=0;y<b.Height;y++){if(b.GetPixel(0,y)!=b.GetPixel(639,y))mismatch++;for(int x=0;x<640;x++){var c=b.GetPixel(x,y);alpha.Add(c.A);if(c.A>0){unique.Add(c.ToArgb());visible++;lum+=.2126*c.R+.7152*c.G+.0722*c.B;if(c.R>c.G*1.5&&c.R>c.B*1.3)red++;}}}report.Add(names[i]+": "+b.Width+"x"+b.Height+", opaque colors="+unique.Count+", alpha="+string.Join(",",alpha.OrderBy(v=>v))+", seam mismatches="+mismatch+", mean luma="+(lum/visible).ToString("F2")+", crimson="+(100.0*red/visible).ToString("F3")+"%");if(unique.Count>24||mismatch>0||alpha.Any(a=>a!=0&&a!=255))throw new Exception("Validation failed");}File.WriteAllLines(Path.Combine(dest,"Sources/validation.txt"),report);Console.WriteLine(string.Join(Environment.NewLine,report));
 }
}
'@
Add-Type -TypeDefinition $src -ReferencedAssemblies System.Drawing
[BgConcept]::Run((Resolve-Path "$PSScriptRoot/../../..").Path)
