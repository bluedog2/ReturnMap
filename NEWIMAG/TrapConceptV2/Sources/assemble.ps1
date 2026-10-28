Add-Type -AssemblyName System.Drawing
$src = @'
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
public static class TrapConcept {
 static string root, dest; static List<Color> pal; static List<string> report=new List<string>();
 static string[] states={"Normal","Dud","Critical","Beneficial"};
 static Bitmap Read(string p){return new Bitmap(p);}
 static Color Hex(string s){return ColorTranslator.FromHtml(s);}
 static Rectangle Bounds(Bitmap b,Rectangle r) {
  int x0=r.Right,y0=r.Bottom,x1=r.Left,y1=r.Top;
  for(int y=r.Top;y<r.Bottom;y++)for(int x=r.Left;x<r.Right;x++)if(b.GetPixel(x,y).A>=200){x0=Math.Min(x0,x);y0=Math.Min(y0,y);x1=Math.Max(x1,x);y1=Math.Max(y1,y);}
  return Rectangle.FromLTRB(x0,y0,x1+1,y1+1);
 }
 static int Dist(Color a,Color b){int r=a.R-b.R,g=a.G-b.G,bl=a.B-b.B;return r*r+g*g+bl*bl;}
 static Color Nearest(Color a,List<Color> p){return p.OrderBy(c=>Dist(a,c)).First();}
 // Region-center sampling only: no new outlines, shapes, pixels, or inferred details are drawn.
 static Bitmap Sample(Bitmap b,Rectangle r,int w,int h,string state="Normal"){
  var p=state=="Dud"?new[]{"#19181F","#2A282D","#3D3940","#555058","#74666A","#8D7B80","#845443","#B07853"}.Select(Hex).ToList():pal;
  Bitmap n=new Bitmap(w,h,PixelFormat.Format32bppArgb);
  for(int y=0;y<h;y++)for(int x=0;x<w;x++){
   var c=b.GetPixel(Math.Min(r.Right-1,r.X+(int)((x+.5)*r.Width/w)),Math.Min(r.Bottom-1,r.Y+(int)((y+.5)*r.Height/h)));
   n.SetPixel(x,y,c.A<128?Color.FromArgb(0,0,0,0):Nearest(c,p));
  }
  return n;
 }
 static Bitmap Canvas(int w,int h){return new Bitmap(w,h,PixelFormat.Format32bppArgb);}
 static void Paste(Bitmap dst,Bitmap im,int x,int y){using(var g=Graphics.FromImage(dst)){g.CompositingMode=System.Drawing.Drawing2D.CompositingMode.SourceOver;g.DrawImageUnscaled(im,x,y);}}
 static void Save(Bitmap b,string name){
  var count=new Dictionary<int,int>();for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++){var c=b.GetPixel(x,y);if(c.A==255){if(!count.ContainsKey(c.ToArgb()))count[c.ToArgb()]=0;count[c.ToArgb()]++;}}
  if(count.Count>16){var p=count.OrderByDescending(k=>k.Value).Take(16).Select(k=>Color.FromArgb(k.Key)).ToList();for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++)if(b.GetPixel(x,y).A==255)b.SetPixel(x,y,Nearest(b.GetPixel(x,y),p));}
  b.Save(Path.Combine(dest,"_1x",name+".png"),ImageFormat.Png);
 }
 static Bitmap LoadAsset(string n){return Read(Path.Combine(dest,"_1x",n+".png"));}
 static void Place(Bitmap b,string n,int x,int y){using(var a=LoadAsset(n))Paste(b,a,x,y);}
 static void Label(Bitmap b,string s,int x,int y,int size=9){using(var g=Graphics.FromImage(b))using(var f=new Font("Consolas",size,FontStyle.Regular,GraphicsUnit.Pixel))using(var brush=new SolidBrush(Hex("#E7D8CE"))){g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;g.DrawString(s,f,brush,x,y);}}
 static Bitmap Scale(Bitmap b,int k){var n=Canvas(b.Width*k,b.Height*k);for(int y=0;y<n.Height;y++)for(int x=0;x<n.Width;x++)n.SetPixel(x,y,b.GetPixel(x/k,y/k));return n;}
 static void ExportScale(Bitmap b,int k,string filename){using(var n=Scale(b,k))n.Save(Path.Combine(dest,filename),ImageFormat.Png);}
 public static void Run(string repo){root=repo;dest=Path.Combine(root,"NEWIMAG/TrapConceptV2");pal=File.ReadAllLines(Path.Combine(root,"NEWIMAG/palette_v2.txt")).Where(s=>System.Text.RegularExpressions.Regex.IsMatch(s.Trim(),"^#[0-9a-fA-F]{6}$")).Take(32).Select(s=>Hex(s.Trim())).ToList();pal.AddRange(new[]{"#79502B","#B67C32","#E8B94F","#FFE09A","#845443","#B07853"}.Select(Hex));
  using(var b=Read(Path.Combine(dest,"Sources/SpikeTrap_attempt2.png"))){for(int i=0;i<4;i++){
   int ox=i%2*627,oy=i/2*627;var r=Bounds(b,new Rectangle(ox,oy,627,627));
   // Same cassette split across states; compressed upper hazard region retains 12px gameplay extent.
   int split=i<2?306:850;var baseR=new Rectangle(r.X,split,r.Width,r.Bottom-split);var spikeR=new Rectangle(r.X,r.Y,r.Width,split-r.Y);
   using(var n=Canvas(32,48))using(var bs=Sample(b,baseR,32,32,states[i]))using(var sp=Sample(b,spikeR,32,12,states[i])){Paste(n,bs,0,16);if(i==2){using(var original=Read(Path.Combine(dest,"Sources/SpikeTrap_attempt1.png")))using(var barbs=Sample(original,new Rectangle(152,668,399,210),32,12,"Critical"))Paste(n,barbs,0,4);}else Paste(n,sp,0,4);Save(n,"SpikeTrap_"+states[i]);}
   report.Add("Spike "+states[i]+" source bounds "+r+" base split y="+split);
  }}
  using(var b=Read(Path.Combine(dest,"Sources/ArrowShooter_attempt1.png"))){for(int i=0;i<4;i++){
   int ox=i%2*627,oy=i/2*627;int split=i<2?445:418;var r=Bounds(b,new Rectangle(ox,oy,627,split));var ar=Bounds(b,new Rectangle(ox,oy+split,627,627-split));
   using(var n=Canvas(32,32))using(var body=Sample(b,r,32,24,states[i])){Paste(n,body,0,4);Save(n,"ArrowShooter_"+states[i]);}
   using(var n=Canvas(16,8))using(var a=Sample(b,ar,16,5,states[i])){Paste(n,a,0,1);Save(n,"Arrow_"+states[i]);}
   report.Add("ArrowShooter "+states[i]+" body "+r+" projectile "+ar);
  }}
  using(var b=Read(Path.Combine(dest,"Sources/DropHammer_attempt1.png"))){for(int i=0;i<4;i++){
   var r=Bounds(b,new Rectangle(i%2*627,i/2*627,627,627));using(var n=Canvas(32,32))using(var a=Sample(b,r,32,28,states[i])){Paste(n,a,0,0);Save(n,"DropHammer_"+states[i]);}report.Add("Hammer "+states[i]+" head "+r);
  }}
  string[] names={"GoldBlock","SlotMarker_Floor","SlotMarker_Wall","SlotMarker_Ceiling","SlotSeal","DropHammer_Mount_Normal","DropHammer_Chain_Normal","DropHammer_Chain_Dud","DropHammer_Chain_Critical"};
  using(var b=Read(Path.Combine(dest,"Sources/Accessories_attempt1.png"))){for(int i=0;i<9;i++){
   var cell=new Rectangle(i%3*418,i/3*418,418,418);if(i==0)cell=new Rectangle(30,40,410,370);if(i==1)cell=new Rectangle(455,40,350,370);if(i==4)cell=new Rectangle(430,465,390,350);var r=Bounds(b,cell);
   if(i>=6){using(var n=Canvas(8,8))using(var a=Sample(b,r,4,8,i==7?"Dud":"Normal")){Paste(n,a,2,0);Save(n,names[i]);}}
   else using(var n=Sample(b,r,32,32))Save(n,names[i]);report.Add(names[i]+" source "+r);
  }}
  // A scene assembly preview is deliberately separate from the 32px moving head cell.
  for(int i=0;i<4;i++){using(var n=Canvas(32,88)){
   Place(n,"DropHammer_Mount_Normal",0,0);string cs=i==1?"Dud":i==2?"Critical":"Normal";
   for(int y=32;y<56;y+=8)Place(n,"DropHammer_Chain_"+cs,12,y);Place(n,"DropHammer_"+states[i],0,56);
   n.Save(Path.Combine(dest,"Sources/DropHammer_Assembly_"+states[i]+".png"),ImageFormat.Png);
  }}
  using(var board=Canvas(320,246)){
   using(var g=Graphics.FromImage(board))g.Clear(Hex("#2A282D"));Label(board,"Re:Trap / TRAP CONCEPT V2",8,5,11);
   for(int i=0;i<4;i++)Label(board,states[i],94+i*56,24,8);
   string[] traps={"SpikeTrap","ArrowShooter","DropHammer"};
   for(int row=0;row<3;row++){int y=42+row*65;Label(board,traps[row],5,y+20,9);for(int i=0;i<4;i++){Place(board,traps[row]+"_"+states[i],100+i*56,y);if(row==1)Place(board,"Arrow_"+states[i],108+i*56,y+38);}}
   Label(board,"APPROVAL CONCEPT / 1x cells shown at 4x",8,234,8);ExportScale(board,4,"States_4x.png");
  }
  Scene(false);Scene(true);Validate();File.WriteAllLines(Path.Combine(dest,"Sources/sampling_report.txt"),report);
 }
 static void Scene(bool build){using(var b=Canvas(640,360)){
  using(var g=Graphics.FromImage(b))g.Clear(Hex("#2A282D"));string mode=build?"Build":"Play";
  using(var floor=Read(Path.Combine(root,"Assets/_Project/ResourcceEX/Sprites/Px32/Tiles/Tile_"+mode+"_Floor_A.png")))
  using(var wall=Read(Path.Combine(root,"Assets/_Project/ResourcceEX/Sprites/Px32/Tiles/Tile_"+mode+"_Wall_A.png")))
  using(var ceil=Read(Path.Combine(root,"Assets/_Project/ResourcceEX/Sprites/Px32/Tiles/Tile_"+mode+"_Ceiling_A.png"))){
   for(int x=32;x<608;x+=32){Paste(b,ceil,x,32);Paste(b,floor,x,288);Paste(b,wall,x,320);}
   for(int y=32;y<352;y+=32){Paste(b,wall,0,y);Paste(b,wall,608,y);}
   // Shallow wall recess at firing height; supports shooter without floating.
   Paste(b,wall,32,224);Paste(b,wall,32,256);
  }
  Place(b,"SpikeTrap_Beneficial",256,272);Place(b,"GoldBlock",288,256);
  Place(b,"ArrowShooter_Normal",32,224);Place(b,"Arrow_Normal",128,237);
  Place(b,"DropHammer_Mount_Normal",416,32);for(int y=64;y<88;y+=8)Place(b,"DropHammer_Chain_Normal",428,y);Place(b,"DropHammer_Critical",416,88);
  Place(b,"SlotSeal",512,288);
  using(var hero=Read(Path.Combine(root,"NEWIMAG/Px32HeroV2/_1x/Hero_Idle.png")))using(var frame=hero.Clone(new Rectangle(0,0,96,96),PixelFormat.Format32bppArgb))Paste(b,frame,160,192);
  using(var gob=Read(Path.Combine(root,"NEWIMAG/Px32EnemyV2/_1x/Goblin_Raider_Idle.png")))using(var frame=gob.Clone(new Rectangle(0,0,64,64),PixelFormat.Format32bppArgb))Paste(b,frame,480,224);
  if(build){Place(b,"SlotMarker_Floor",352,288);Place(b,"SlotMarker_Wall",0,160);Place(b,"SlotMarker_Ceiling",320,32);}
  Label(b,build?"Re:Trap / BUILD - empty slot directions":"Re:Trap / PLAY - faction and silhouette check",16,12,11);
  Label(b,"GOLD PLATFORM",277,246,8);Label(b,"3 x 8px CHAIN",457,77,8);Label(b,"SEALED",511,327,8);
  if(build){Label(b,"FLOOR",346,327,8);Label(b,"WALL",36,171,8);Label(b,"CEILING",309,70,8);}
  b.Save(Path.Combine(dest,"Sources",build?"Build_1x.png":"Scene_1x.png"),ImageFormat.Png);ExportScale(b,3,build?"Build_3x.png":"Scene_3x.png");
 }}
 static void Validate(){var lines=new List<string>();foreach(string path in Directory.GetFiles(Path.Combine(dest,"_1x"),"*.png"))using(var b=Read(path)){
  var colors=new HashSet<int>();int invalidAlpha=0,outside=0,black=0;for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++){var c=b.GetPixel(x,y);if(c.A!=0&&c.A!=255)invalidAlpha++;if(c.A==255){colors.Add(c.ToArgb());if(!pal.Any(p=>p.ToArgb()==c.ToArgb()))outside++;if(c.R==0&&c.G==0&&c.B==0)black++;}}
  if(colors.Count>16||invalidAlpha>0||outside>0||black>0)throw new Exception("Validation failed "+path);
  lines.Add(Path.GetFileName(path)+" | "+b.Width+"x"+b.Height+" | colors="+colors.Count+" | alpha=0/255 | outside=0 | pure-black=0");
 }File.WriteAllLines(Path.Combine(dest,"Sources/validation.txt"),lines);}
}
'@
Add-Type -TypeDefinition $src -ReferencedAssemblies System.Drawing
[TrapConcept]::Run((Get-Location).Path)
