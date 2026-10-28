$ErrorActionPreference='Stop'
$out=Join-Path (Get-Location) 'NEWIMAG/Px32TrapV2'
New-Item -ItemType Directory -Force "$out/.tmp" | Out-Null
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
public static class Parts {
static string root,dest,src; static List<Color> pal; static string[] states={"Normal","Dud","Critical","Beneficial"};
static Color H(string h){return ColorTranslator.FromHtml(h);}
static Bitmap B(int w,int h){return new Bitmap(w,h,PixelFormat.Format32bppArgb);}
static Bitmap Read(string p){using(var b=new Bitmap(p))return new Bitmap(b);}
static Bitmap Asset(string n){return Read(Path.Combine(src,n+".png"));}
static Bitmap Crop(Bitmap b,int x,int y,int w,int h){return b.Clone(new Rectangle(x,y,w,h),PixelFormat.Format32bppArgb);}
static void Paste(Bitmap d,Bitmap s,int ox,int oy){for(int y=0;y<s.Height;y++)for(int x=0;x<s.Width;x++)if(x+ox>=0&&y+oy>=0&&x+ox<d.Width&&y+oy<d.Height&&s.GetPixel(x,y).A==255)d.SetPixel(x+ox,y+oy,s.GetPixel(x,y));}
static Color Near(Color a,IEnumerable<Color> p){return p.OrderBy(c=>(c.R-a.R)*(c.R-a.R)+(c.G-a.G)*(c.G-a.G)+(c.B-a.B)*(c.B-a.B)).First();}
static Color NearestPixel(Bitmap b,int x,int y){int best=int.MaxValue;Color c=Color.Transparent;for(int yy=0;yy<b.Height;yy++)for(int xx=0;xx<b.Width;xx++)if(b.GetPixel(xx,yy).A==255){int d=(xx-x)*(xx-x)+(yy-y)*(yy-y);if(d<best){best=d;c=b.GetPixel(xx,yy);}}return c;}
// Alpha registration reuses nearest existing source pixels; never creates geometric artwork.
static Bitmap Register(Bitmap b,Bitmap mask){var n=B(mask.Width,mask.Height);for(int y=0;y<n.Height;y++)for(int x=0;x<n.Width;x++)if(mask.GetPixel(x,y).A==255)n.SetPixel(x,y,b.GetPixel(x,y).A==255?b.GetPixel(x,y):NearestPixel(b,x,y));return n;}
static Bitmap Opaque(Bitmap b){var n=new Bitmap(b);for(int y=0;y<n.Height;y++)for(int x=0;x<n.Width;x++)if(n.GetPixel(x,y).A<255)n.SetPixel(x,y,NearestPixel(b,x,y));return n;}
static Rectangle Bounds(Bitmap b){int l=b.Width,t=b.Height,r=-1,bot=-1;for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++)if(b.GetPixel(x,y).A>=128){l=Math.Min(l,x);t=Math.Min(t,y);r=Math.Max(r,x);bot=Math.Max(bot,y);}return Rectangle.FromLTRB(l,t,r+1,bot+1);}
static Bitmap Sample(Bitmap b,Rectangle r,int w,int h){var n=B(w,h);for(int y=0;y<h;y++)for(int x=0;x<w;x++){Color c=b.GetPixel(r.X+Math.Min(r.Width-1,(int)((x+.5)*r.Width/w)),r.Y+Math.Min(r.Height-1,(int)((y+.5)*r.Height/h)));n.SetPixel(x,y,c.A<128?Color.FromArgb(0,0,0,0):Near(c,pal));}return n;}
static Bitmap Tone(Bitmap b,string state){var n=new Bitmap(b);string[] p=state=="Dud"?new[]{"#19181F","#3D3940","#555058","#845443","#B07853","#8D7B80"}:state=="Beneficial"?new[]{"#19181F","#79502B","#B67C32","#E8B94F","#FFE09A","#F7EEE4"}:state=="Critical"?new[]{"#19181F","#641F36","#92223F","#B83450","#DC526B","#F18490"}:new[]{"#19181F","#3D3940","#74666A","#B3A1A0","#E7D8CE","#F7EEE4"};
for(int y=0;y<n.Height;y++)for(int x=0;x<n.Width;x++){var c=n.GetPixel(x,y);if(c.A==255){double v=(c.R*.299+c.G*.587+c.B*.114)/255;int i=Math.Min(5,(int)(v*6));n.SetPixel(x,y,H(p[i]));}}return n;}
static void Save(Bitmap b,string relative){var counts=new Dictionary<int,int>();for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++){var c=b.GetPixel(x,y);if(c.A<128)b.SetPixel(x,y,Color.FromArgb(0,0,0,0));else{c=Near(c,pal);b.SetPixel(x,y,c);int k=c.ToArgb();if(!counts.ContainsKey(k))counts[k]=0;counts[k]++;}}if(counts.Count>16){var p=counts.OrderByDescending(k=>k.Value).Take(16).Select(k=>Color.FromArgb(k.Key)).ToArray();for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++)if(b.GetPixel(x,y).A==255)b.SetPixel(x,y,Near(b.GetPixel(x,y),p));}string path=Path.Combine(dest,relative);Directory.CreateDirectory(Path.GetDirectoryName(path));b.Save(path,ImageFormat.Png);}
static void Part(Bitmap b,string trap,string state,string name){Save(b,"Parts/"+trap+"/"+state+"/"+name+".png");}
static Bitmap Gen(string name,int w,int h,bool crop){var b=Read(Path.Combine(dest,"Sources/"+name+"_attempt1.png"));return Sample(b,crop?Bounds(b):new Rectangle(0,0,b.Width,b.Height),w,h);}
static void Label(Bitmap b,string s,int x,int y){using(var g=Graphics.FromImage(b))using(var f=new Font("Consolas",8,FontStyle.Regular,GraphicsUnit.Pixel))using(var br=new SolidBrush(H("#E7D8CE"))){g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;g.DrawString(s,f,br,x,y);}}
static void ScaleSave(Bitmap b,int k,string filename){var n=B(b.Width*k,b.Height*k);for(int y=0;y<n.Height;y++)for(int x=0;x<n.Width;x++)n.SetPixel(x,y,b.GetPixel(x/k,y/k));n.Save(Path.Combine(dest,filename),ImageFormat.Png);}
static Bitmap Back(int w,int h){var b=B(w,h);using(var g=Graphics.FromImage(b))g.Clear(H("#1A191F"));return b;}
static Bitmap LoadPart(string trap,string state,string name){return Read(Path.Combine(dest,"Parts",trap,state,name+".png"));}
static void Put(Bitmap b,string trap,string state,string name,int x,int y){Paste(b,LoadPart(trap,state,name),x,y);}
public static void Run(string repo){root=repo;dest=Path.Combine(root,"NEWIMAG/Px32TrapV2");src=Path.Combine(root,"NEWIMAG/TrapConceptV2/_1x");var banned=new[]{"#334629","#58783B","#88AE43","#B8D849","#D8EC76","#38284D","#58377B","#8050AD","#B581D4"};pal=File.ReadAllLines(Path.Combine(root,"NEWIMAG/palette_v2.txt")).Concat(File.ReadAllLines(Path.Combine(root,"NEWIMAG/TrapConceptV2/palette_add_traps.txt"))).Select(s=>s.Trim().ToUpper()).Where(s=>System.Text.RegularExpressions.Regex.IsMatch(s,"^#[0-9A-F]{6}$")&&!banned.Contains(s)).Select(H).Distinct().ToList();
var normalSpike=Crop(Asset("SpikeTrap_Normal"),0,4,32,8);var bodyMask=Asset("ArrowShooter_Normal");var headMask=Crop(Asset("DropHammer_Normal"),0,0,32,28);var arrowMask=Asset("Arrow_Normal");
var flash=B(16,16);Paste(flash,Gen("flash",8,10,true),8,3);
var impact=Gen("impact",32,16,true);
var glow=B(32,32);Paste(glow,Gen("glow",32,14,true),0,0);
var chain=B(8,8);Paste(chain,Gen("chain",4,8,true),2,0);
// Exact periodic seam: repeat existing first pixel row at the bottom, no drawn links.
for(int x=0;x<8;x++)chain.SetPixel(x,7,chain.GetPixel(x,0));
var gold=Opaque(Asset("GoldBlock"));
foreach(string s in states){
var baseSource=Opaque(Crop(Asset("SpikeTrap_"+s),0,16,32,28));var bs=B(32,32);for(int y=0;y<32;y++)for(int x=0;x<32;x++)bs.SetPixel(x,y,baseSource.GetPixel(x,y<24?y:y<28?23:y-4));Part(bs,"spike",s,"base");
var sp=s=="Dud"?Tone(normalSpike,"Dud"):Register(Crop(Asset("SpikeTrap_"+s),0,4,32,8),normalSpike);var spikes=B(32,32);Paste(spikes,sp,0,0);for(int y=8;y<=20;y++)for(int x=0;x<32;x++)spikes.SetPixel(x,y,sp.GetPixel(x,7));Part(spikes,"spike",s,"spikes");var gl=new Bitmap(glow);if(s=="Beneficial")for(int yy=0;yy<32;yy++)for(int xx=0;xx<32;xx++){var c=gl.GetPixel(xx,yy);if(c.A==255)gl.SetPixel(xx,yy,H(c.R>180?"#FFE09A":"#E8B94F"));}Part(gl,"spike",s,"glow");Part(new Bitmap(gold),"spike",s,"platform");
var body=B(32,32);Paste(body,Register(Asset("ArrowShooter_"+s),bodyMask),0,-1);Part(body,"arrow_shooter",s,"body");
var arrow=Register(Asset("Arrow_"+s),arrowMask); // recolor only existing projectile pixels for dark-background readability
for(int y=0;y<8;y++)for(int x=0;x<16;x++){var c=arrow.GetPixel(x,y);if(c.A==255&&c.ToArgb()!=H("#19181F").ToArgb()){if(x<=3)arrow.SetPixel(x,y,H(s=="Dud"?"#B07853":s=="Beneficial"?"#E8B94F":"#B83450"));else if(s=="Normal"&&x>=10)arrow.SetPixel(x,y,H("#E7D8CE"));}}Part(arrow,"arrow_shooter",s,"arrow");Part(s=="Normal"?new Bitmap(flash):Tone(flash,s),"arrow_shooter",s,"muzzle_flash");
var head=B(32,32);Paste(head,Register(Crop(Asset("DropHammer_"+s),0,0,32,28),headMask),0,4);Part(head,"drop_hammer",s,"head");var mount=Asset("DropHammer_Mount_Normal");Part(s=="Normal"?mount:Tone(mount,s),"drop_hammer",s,"mount");Part(s=="Normal"?new Bitmap(chain):Tone(chain,s),"drop_hammer",s,"chain");Part(s=="Normal"?Tone(impact,"Normal"):Tone(impact,s),"drop_hammer",s,"impact");
}
Save(Asset("SlotMarker_Floor"),"Misc/SlotMarker_Floor.png");Save(Asset("SlotMarker_Ceiling"),"Misc/SlotMarker_Ceiling.png");var wall=Asset("SlotMarker_Wall");Save(wall,"Misc/SlotMarker_RightWall.png");wall.RotateFlip(RotateFlipType.RotateNoneFlipX);Save(wall,"Misc/SlotMarker_LeftWall.png");
var floor=Read(Path.Combine(root,"Assets/_Project/ResourcceEX/Sprites/Px32/Tiles/Tile_Play_Floor_A.png"));var seal=new Bitmap(floor);var sealSource=Asset("SlotSeal");for(int y=0;y<32;y++)for(int x=0;x<32;x++){var c=sealSource.GetPixel(x,y);if(c.A==255&&c.R>c.G*1.35&&c.R>c.B*1.1)seal.SetPixel(x,y,c);}Save(seal,"Misc/SlotSeal.png");Preview();
}
static void Preview(){var b=Back(480,360);Label(b,"RE:TRAP / ASSEMBLED PARTS / 1x -> 4x",8,4);for(int i=0;i<4;i++){string s=states[i];int x=i*120;Label(b,s,x+20,19);Label(b,"SPIKE",x+4,33);var floor=Read(Path.Combine(root,"Assets/_Project/ResourcceEX/Sprites/Px32/Tiles/Tile_Play_Floor_A.png"));for(int xx=0;xx<96;xx+=32)Paste(b,floor,x+8+xx,65);if(s=="Critical"||s=="Beneficial"){var gl=LoadPart("spike",s,"glow");for(int yy=0;yy<32;yy++)for(int xx=0;xx<32;xx++){var c=gl.GetPixel(xx,yy);if(c.A==255){var bg=b.GetPixel(x+40+xx,53+yy);b.SetPixel(x+40+xx,53+yy,Color.FromArgb((int)(c.R*.6+bg.R*.4),(int)(c.G*.6+bg.G*.4),(int)(c.B*.6+bg.B*.4)));}}}Put(b,"spike",s,"spikes",x+40,53);Put(b,"spike",s,"base",x+40,65);if(s=="Beneficial")Put(b,"spike",s,"platform",x+72,65);
Label(b,"SHOOTER",x+4,105);Paste(b,Read(Path.Combine(root,"Assets/_Project/ResourcceEX/Sprites/Px32/Tiles/Tile_Play_Wall_A.png")),x+8,120);Put(b,"arrow_shooter",s,"body",x+8,120);Put(b,"arrow_shooter",s,"muzzle_flash",x+30,128);Put(b,"arrow_shooter",s,"arrow",x+80,132);
Label(b,"HAMMER",x+4,160);Paste(b,Read(Path.Combine(root,"Assets/_Project/ResourcceEX/Sprites/Px32/Tiles/Tile_Play_Ceiling_A.png")),x+40,174);Put(b,"drop_hammer",s,"mount",x+40,174);for(int j=0;j<3;j++)Put(b,"drop_hammer",s,"chain",x+52,206+j*8);Put(b,"drop_hammer",s,"head",x+40,226);for(int xx=0;xx<96;xx+=32)Paste(b,floor,x+8+xx,285);Put(b,"drop_hammer",s,"impact",x+40,269);
}string[] names={"Floor","Ceiling","LeftWall","RightWall","Seal"};for(int i=0;i<5;i++){int x=8+i*92;Paste(b,Read(Path.Combine(dest,"Misc/Slot"+(i==4?"Seal":"Marker_"+names[i])+".png")),x,324);Label(b,names[i],x+34,335);}ScaleSave(b,4,"Preview_4x.png");
var tiles=Back(208,104);for(int i=0;i<4;i++){Label(tiles,states[i],i*52+1,2);for(int j=0;j<9;j++)Put(tiles,"drop_hammer",states[i],"chain",i*52+22,16+j*8);}ScaleSave(tiles,4,"Chain_Tiling_4x.png");
var sheet=Back(704,304);string[] traps={"spike","arrow_shooter","drop_hammer"};string[][] parts={new[]{"base","spikes","glow","platform"},new[]{"body","arrow","muzzle_flash"},new[]{"head","mount","chain","impact"}};int col=0;for(int t=0;t<3;t++)foreach(string p in parts[t]){Label(sheet,p,col*64+2,3);for(int i=0;i<4;i++){Paste(sheet,LoadPart(traps[t],states[i],p),col*64+4,20+i*64);Label(sheet,states[i],col*64+1,53+i*64);}col++;}ScaleSave(sheet,4,"Parts_4x.png");}
}
'@
try {
 Add-Type -TypeDefinition $code -ReferencedAssemblies System.Drawing
 [Parts]::Run((Get-Location).Path)
} finally {
 $env:TEMP=$oldTemp; $env:TMP=$oldTmp
 $resolved=[IO.Path]::GetFullPath("$out/.tmp")
 if($resolved.StartsWith([IO.Path]::GetFullPath($out)+[IO.Path]::DirectorySeparatorChar)){Remove-Item -LiteralPath $resolved -Recurse -Force}
}
