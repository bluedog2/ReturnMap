$ErrorActionPreference='Stop'
$out=Join-Path (Get-Location) 'NEWIMAG/Px32TrapV3'
New-Item -ItemType Directory -Force "$out/.tmp" | Out-Null
$oldTemp=$env:TEMP; $oldTmp=$env:TMP
$env:TEMP="$out/.tmp"; $env:TMP="$out/.tmp"
Add-Type -AssemblyName System.Drawing
$code=@'
using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
public static class PreviewV3 {
static string root,dest;
static Bitmap Read(string p){return new Bitmap(Path.Combine(root,p));}
static void Paste(Bitmap d,Bitmap s,int ox,int oy){for(int y=0;y<s.Height;y++)for(int x=0;x<s.Width;x++)if(x+ox>=0&&y+oy>=0&&x+ox<d.Width&&y+oy<d.Height&&s.GetPixel(x,y).A==255)d.SetPixel(x+ox,y+oy,s.GetPixel(x,y));}
static void Put(Bitmap b,string trap,string state,string part,int x,int y){using(var p=new Bitmap(Path.Combine(dest,"Parts",trap,state,part+".png")))Paste(b,p,x,y);}
static void Label(Bitmap b,string s,int x,int y){using(var g=Graphics.FromImage(b))using(var f=new Font("Consolas",10,FontStyle.Regular,GraphicsUnit.Pixel))using(var br=new SolidBrush(ColorTranslator.FromHtml("#E7D8CE"))){g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;g.DrawString(s,f,br,x,y);}}
static void Tile(Bitmap b,Bitmap t,int x,int y,int cols,int rows){for(int j=0;j<rows;j++)for(int i=0;i<cols;i++)Paste(b,t,x+32*i,y+32*j);}
public static void Run(string repo){root=repo;dest=Path.Combine(repo,"NEWIMAG/Px32TrapV3");
var b=new Bitmap(1152,928,PixelFormat.Format32bppArgb);using(var g=Graphics.FromImage(b)){g.Clear(ColorTranslator.FromHtml("#19181F"));using(var p=new Pen(ColorTranslator.FromHtml("#2A282D"))){for(int x=0;x<b.Width;x+=32)g.DrawLine(p,x,32,x,b.Height-1);for(int y=32;y<b.Height;y+=32)g.DrawLine(p,0,y,b.Width-1,y);}}
Label(b,"RE:TRAP V3 | 1 TILE=32px | TRAPS=64px | HERO: ORIGINAL IDLE FRAME 0 | DISPLAY 3x",8,8);
var floor=Read("Assets/_Project/ResourcceEX/Sprites/Px32/Tiles/Tile_Play_Floor_A.png");var wall=Read("Assets/_Project/ResourcceEX/Sprites/Px32/Tiles/Tile_Play_Wall_A.png");var ceil=Read("Assets/_Project/ResourcceEX/Sprites/Px32/Tiles/Tile_Play_Ceiling_A.png");
var hs=Read("NEWIMAG/Px32HeroV3/_1x/Hero_Idle.png");var hero=hs.Clone(new Rectangle(0,0,128,128),PixelFormat.Format32bppArgb);int bottom=-1;for(int y=0;y<128;y++)for(int x=0;x<128;x++)if(hero.GetPixel(x,y).A==255)bottom=y;
string[] states={"Normal","Dud","Critical","Beneficial"};for(int i=0;i<4;i++){int x=i*288;string s=states[i];Label(b,s,x+8,40);Label(b,"SPIKE / +24px",x+8,57);
Tile(b,floor,x,160,9,2);Paste(b,hero,x+148,159-bottom);if(i>=2)Put(b,"spike",s,"glow",x+32,136);Put(b,"spike",s,"spikes",x+32,136);Put(b,"spike",s,"base",x+32,160);if(i==3)Put(b,"spike",s,"platform",x+96,160);
Label(b,"ARROW SHOOTER",x+8,248);Tile(b,wall,x+16,304,2,2);Tile(b,floor,x,400,9,2);Put(b,"arrow_shooter",s,"body",x+16,304);Put(b,"arrow_shooter",s,"muzzle_flash",x+63,320);Put(b,"arrow_shooter",s,"arrow",x+112,328);Paste(b,hero,x+152,399-bottom);
Label(b,"DROP HAMMER / 3 CHAIN TILES",x+8,472);Tile(b,ceil,x+32,496,2,2);Put(b,"drop_hammer",s,"mount",x+32,496);for(int j=0;j<3;j++)Put(b,"drop_hammer",s,"chain",x+56,560+j*16);Put(b,"drop_hammer",s,"head",x+32,608);Tile(b,floor,x,736,9,2);Put(b,"drop_hammer",s,"impact",x+32,704);Paste(b,hero,x+152,735-bottom);
}
string[] names={"Floor","Ceiling","LeftWall","RightWall","Seal"};for(int i=0;i<5;i++){int x=32+i*208;using(var a=new Bitmap(Path.Combine(dest,"Misc","Slot"+(i==4?"Seal":"Marker_"+names[i])+".png")))Paste(b,a,x,832);Label(b,names[i],x+72,853);}
Label(b,"32px grid | SlotSeal remains 32px | Preview only: 3x nearest-neighbor",8,912);
var big=new Bitmap(b.Width*3,b.Height*3,PixelFormat.Format32bppArgb);for(int y=0;y<big.Height;y++)for(int x=0;x<big.Width;x++)big.SetPixel(x,y,b.GetPixel(x/3,y/3));big.Save(Path.Combine(dest,"Preview_3x.png"),ImageFormat.Png);
var sheet=new Bitmap(768,352,PixelFormat.Format32bppArgb);using(var g=Graphics.FromImage(sheet))g.Clear(ColorTranslator.FromHtml("#2A282D"));string[] traps={"spike","spike","spike","spike","arrow_shooter","arrow_shooter","arrow_shooter","drop_hammer","drop_hammer","drop_hammer","drop_hammer"};string[] parts={"base","spikes","glow","platform","body","arrow","muzzle_flash","head","mount","chain","impact"};for(int c=0;c<11;c++){Label(sheet,parts[c],c*69,0);for(int i=0;i<4;i++)Put(sheet,traps[c],states[i],parts[c],c*69,20+i*82);}sheet.Save(Path.Combine(dest,"Parts_1x.png"),ImageFormat.Png);
}
}
'@
try {Add-Type -TypeDefinition $code -ReferencedAssemblies System.Drawing; [PreviewV3]::Run((Get-Location).Path)} finally {
 $env:TEMP=$oldTemp; $env:TMP=$oldTmp
 $resolved=[IO.Path]::GetFullPath("$out/.tmp")
 if($resolved.StartsWith([IO.Path]::GetFullPath($out)+[IO.Path]::DirectorySeparatorChar)){Remove-Item -LiteralPath $resolved -Recurse -Force}
}
