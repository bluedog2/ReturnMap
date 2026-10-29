$ErrorActionPreference='Stop'
$out=Join-Path (Get-Location) 'NEWIMAG/FxUiConceptV2'
$oldTemp=$env:TEMP; $oldTmp=$env:TMP
$env:TEMP="$out/.tmp"; $env:TMP="$out/.tmp"
New-Item -ItemType Directory -Force "$out/.tmp" | Out-Null
Add-Type -AssemblyName System.Drawing
$code=@'
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;
public static class FxBuild {
static string root,dest; static Color[] palette;
static Dictionary<string,List<Bitmap>> assets=new Dictionary<string,List<Bitmap>>();
static List<string> report=new List<string>();
static Color H(string s){return ColorTranslator.FromHtml(s);}
static Bitmap B(int w,int h){return new Bitmap(w,h,PixelFormat.Format32bppArgb);}
static Bitmap Read(string p){using(var b=new Bitmap(p))return new Bitmap(b);}
static Color Near(Color c,Color[] p){return p.OrderBy(t=>(t.R-c.R)*(t.R-c.R)+(t.G-c.G)*(t.G-c.G)+(t.B-c.B)*(t.B-c.B)).First();}
static Bitmap Crop(Bitmap b,int x,int y,int w,int h){return b.Clone(new Rectangle(x,y,w,h),PixelFormat.Format32bppArgb);}
static void Paste(Bitmap d,Bitmap s,int x,int y){for(int j=0;j<s.Height;j++)for(int i=0;i<s.Width;i++)if(x+i>=0&&x+i<d.Width&&y+j>=0&&y+j<d.Height&&s.GetPixel(i,j).A>=128)d.SetPixel(x+i,y+j,s.GetPixel(i,j));}
static Bitmap Scale(Bitmap b,int k){var n=B(b.Width*k,b.Height*k);for(int y=0;y<n.Height;y++)for(int x=0;x<n.Width;x++)n.SetPixel(x,y,b.GetPixel(x/k,y/k));return n;}
static Rectangle Bounds(Bitmap b,int left,int right){int l=right,t=b.Height,r=left-1,bot=-1;for(int y=0;y<b.Height;y++)for(int x=left;x<right;x++)if(b.GetPixel(x,y).A>=160){l=Math.Min(l,x);r=Math.Max(r,x);t=Math.Min(t,y);bot=Math.Max(bot,y);}return r<l?new Rectangle(left,0,1,1):Rectangle.FromLTRB(l,t,r+1,bot+1);}
static Bitmap Sample(Bitmap b,Rectangle r,int w,int h,Color[] p){var n=B(w,h);for(int y=0;y<h;y++)for(int x=0;x<w;x++){var c=b.GetPixel(r.X+Math.Min(r.Width-1,(int)((x+.5)*r.Width/w)),r.Y+Math.Min(r.Height-1,(int)((y+.5)*r.Height/h)));if(c.A>=160)n.SetPixel(x,y,Near(c,p));}return n;}
static Color[] Colors(string name){string s=name.Contains("Enemy")?"38284D 58377B 8050AD B581D4 334629 58783B 88AE43 B8D849 D8EC76":name.Contains("Dust")||name.Contains("Dud")?"555058 74666A 8D7B80 B3A1A0 D0BEB8":name.Contains("Beneficial")?"79502B B67C32 E8B94F FFE09A":"19181F 3D3940 74666A B3A1A0 D0BEB8 F7EEE4 641F36 92223F B83450 DC526B F18490";return s.Split(' ').Select(v=>H("#"+v)).ToArray();}
static int[] Cuts(Bitmap b,int n,string name){
 if(name=="Fx_Dash")return new[]{0,310,710,1245,1695,b.Width};
 if(name=="Fx_Death")return new[]{0,285,665,1138,1580,1925,b.Width};
 var cuts=new int[n+1];cuts[n]=b.Width;int[] proj=new int[b.Width];for(int x=0;x<b.Width;x++)for(int y=0;y<b.Height;y++)if(b.GetPixel(x,y).A>=160)proj[x]++;
 for(int i=1;i<n;i++){int center=i*b.Width/n,rad=b.Width/n/3;int best=center;double score=1e10;for(int x=center-rad;x<center+rad;x++){double v=proj[x]*1000+Math.Abs(center-x);if(v<score){score=v;best=x;}}cuts[i]=best;}
 return cuts;
}
static void Gen(string name,int size,int count){var src=Read(Path.Combine(dest,"Sources/"+name+(name=="Gauge_Ring24"||name=="Particle_Beneficial"?"_attempt2.png":"_attempt1.png")));var p=Colors(name);var cuts=Cuts(src,count,name); if(name=="Particle_Critical") cuts=new[]{690,970}; if(name=="Particle_Dud") cuts=new[]{830,1080};  if(name=="Icon_Remove") cuts=new[]{700,1020}; if(name=="Icon_Spike") cuts=new[]{1120,1420}; if(name=="Icon_ArrowShooter"||name=="Icon_DropHammer") cuts=new[]{0,320};var bounds=new List<Rectangle>();for(int i=0;i<count;i++)bounds.Add(Bounds(src,cuts[i],cuts[i+1]));double scale=(size-(name.StartsWith("Particle")?1.0:2.0))/bounds.Max(r=>Math.Max(r.Width,r.Height));var frames=new List<Bitmap>();
 for(int i=0;i<count;i++){var r=bounds[i];int w=Math.Max(1,(int)Math.Round(r.Width*scale)),h=Math.Max(1,(int)Math.Round(r.Height*scale));var frame=B(size,size);bool bottom=name=="Fx_Dash"||name=="Fx_Dust"||name=="Fx_Respawn"||name=="GoalMarker";Paste(frame,Sample(src,r,w,h,p),(name=="GoalMarker"?5:(size-w)/2),bottom?size-h:(size-h)/2);frames.Add(frame);}
 if(name.StartsWith("Gauge")){var original=frames[0];var full=B(size,size);for(int y=0;y<size;y++)for(int x=0;x<size;x++)full.SetPixel(x,y,original.GetPixel(x<size/2?x:size-1-x,y<size/2?y:size-1-y));var bg=B(size,size);for(int y=0;y<size;y++)for(int x=0;x<size;x++){var c=full.GetPixel(x,y);if(c.A==255)bg.SetPixel(x,y,H(c.R<70?"#19181F":"#3D3940"));}Save(bg,"_1x/"+name+"_Background.png");assets[name+"_Background"]=new List<Bitmap>{bg};frames.Clear();for(int step=0;step<=8;step++){var f=new Bitmap(bg);for(int y=0;y<size;y++)for(int x=0;x<size;x++){double a=(Math.Atan2(x+.5-size/2.0,size/2.0-y-.5)+2*Math.PI)%(2*Math.PI);if(a<step*Math.PI/4&&full.GetPixel(x,y).A==255)f.SetPixel(x,y,full.GetPixel(x,y));}frames.Add(f);}}
 assets[name]=frames;SaveFrames(name,frames);
 if(name.StartsWith("Particle")){var f=frames[0];var four=B(4,4);Paste(four,Sample(src,bounds[0],3,3,p),0,0);assets[name+"_4"]=new List<Bitmap>{four};SaveFrames(name+"_4",new List<Bitmap>{four});Save(f,"_1x/"+name+"_8.png");}
}
static void Save(Bitmap b,string rel){b.Save(Path.Combine(dest,rel),ImageFormat.Png);}
static Bitmap Index(Bitmap b){var list=new List<Color>();for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++){var c=b.GetPixel(x,y);if(!list.Any(v=>v.ToArgb()==c.ToArgb()))list.Add(c);}var n=new Bitmap(b.Width,b.Height,PixelFormat.Format8bppIndexed);var pal=n.Palette;for(int i=0;i<256;i++)pal.Entries[i]=i<list.Count?list[i]:list[0];n.Palette=pal;var bits=n.LockBits(new Rectangle(0,0,n.Width,n.Height),ImageLockMode.WriteOnly,PixelFormat.Format8bppIndexed);var data=new byte[bits.Stride*n.Height];for(int y=0;y<n.Height;y++)for(int x=0;x<n.Width;x++)data[y*bits.Stride+x]=(byte)list.FindIndex(c=>c.ToArgb()==b.GetPixel(x,y).ToArgb());System.Runtime.InteropServices.Marshal.Copy(data,0,bits.Scan0,data.Length);n.UnlockBits(bits);return n;}
static void SaveFrames(string name,List<Bitmap> f){var sheet=B(f[0].Width*f.Count,f[0].Height);for(int i=0;i<f.Count;i++)Paste(sheet,f[i],i*f[i].Width,0);Save(sheet,"_1x/"+name+".png");Gif(f.Select(x=>Scale(x,4)).ToList(),Path.Combine(dest,"Animations/"+name+"_4x.gif"),name.StartsWith("Gauge")||name=="GoalMarker"?new[]{17,17,16}:new[]{6});}
// GIF frames are lossless indexed copies of the composited preview, with local color tables.
static void Gif(List<Bitmap> frames,string path,int[] delays){using(var output=new FileStream(path,FileMode.Create))using(var bw=new BinaryWriter(output)){bool first=true;int frameIndex=0;foreach(var frame in frames){var opaque=B(frame.Width,frame.Height);using(var g=Graphics.FromImage(opaque))g.Clear(H("#2A282D"));Paste(opaque,frame,0,0);byte[] data;using(var ms=new MemoryStream()){using(var indexed=Index(opaque))indexed.Save(ms,ImageFormat.Gif);data=ms.ToArray();}int gct=3*(1<<((data[10]&7)+1));if(first){bw.Write(Encoding.ASCII.GetBytes("GIF89a"));bw.Write(data,6,7+gct);bw.Write(new byte[]{0x21,0xff,0x0b});bw.Write(Encoding.ASCII.GetBytes("NETSCAPE2.0"));bw.Write(new byte[]{3,1,0,0,0});first=false;}int p=13+gct;while(data[p]!=0x2c){if(data[p]==0x21){p+=2;while(data[p]!=0){p+=1+data[p];}p++;}else throw new Exception("GIF parse");}bw.Write(new byte[]{0x21,0xf9,4,4,(byte)delays[frameIndex%delays.Length],0,0,0});bw.Write(data,p,9);byte packed=(byte)(0x80|(data[10]&7));bw.Write(packed);bw.Write(data,13,gct);int q=p+10;if((data[p+9]&128)!=0)q+=3*(1<<((data[p+9]&7)+1));int end=q+1;while(data[end]!=0)end+=1+data[end];end++;bw.Write(data,q,end-q);frameIndex++;}bw.Write((byte)0x3b);}}
static void Label(Bitmap b,string s,int x,int y,int size=8){using(var g=Graphics.FromImage(b))using(var f=new Font("Consolas",size,FontStyle.Regular,GraphicsUnit.Pixel))using(var br=new SolidBrush(H("#F7EEE4"))){g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;g.DrawString(s,f,br,x,y);}}
static Bitmap Canvas(int w,int h){var b=B(w,h);using(var g=Graphics.FromImage(b))g.Clear(H("#2A282D"));return b;}
static void Reduce(Bitmap b){Color[] p="19181F 2A282D 3D3940 74666A B3A1A0 F7EEE4 641F36 92223F DC526B F18490 58377B 8050AD 88AE43 B8D849 E8B94F FFE09A".Split(' ').Select(v=>H("#"+v)).ToArray();for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++)if(b.GetPixel(x,y).A>0)b.SetPixel(x,y,Near(b.GetPixel(x,y),p));}
static void Board(){int h=assets.Sum(a=>Math.Max(a.Value[0].Height,12)+22)+28;var b=Canvas(480,h);Label(b,"RE:TRAP / FX + UI V2 / APPROVAL CONCEPT",6,5);int y=28;foreach(var a in assets){Label(b,a.Key,6,y+1);int x=150;for(int i=0;i<a.Value.Count;i++){Paste(b,a.Value[i],x,y);Label(b,i.ToString(),x,y+a.Value[i].Height+1);x+=a.Value[i].Width+2;}y+=Math.Max(a.Value[0].Height,12)+22;}Reduce(b);Save(Scale(b,4),"Sheet_4x.png");}
static Bitmap A(string name,int i){return assets[name][i];}
static Bitmap Asset(string rel){return Read(Path.Combine(root,rel));}
static void Scene(){var b=Canvas(640,360);var tile=Asset("Assets/_Project/ResourcceEX/Sprites/Px32/Tiles/Tile_Play_Floor_A.png");for(int y=288;y<360;y+=32)for(int x=0;x<640;x+=32)Paste(b,tile,x,y);Label(b,"RE:TRAP / FX + WORLD UI / CONCEPT V2",16,15,10);Label(b,"DASH + HIT",140,160);Label(b,"CRITICAL / 4:8",318,190);Label(b,"ENEMY POOF",450,205);Label(b,"GOAL",584,231);
 var hero=Asset("NEWIMAG/Px32HeroV2/_1x/Hero_Dash.png");Paste(b,A("Fx_Dash",2),132,256);Paste(b,Crop(hero,192,0,96,96),155,192);Paste(b,A("Fx_Hit",1),226,217);Paste(b,A("Gauge_Ring32",5),188,164);Label(b,"2",200,175,10);
 var sp=Asset("NEWIMAG/Px32TrapV2/Parts/spike/Critical/spikes.png");var bas=Asset("NEWIMAG/Px32TrapV2/Parts/spike/Critical/base.png");Paste(b,bas,330,288);Paste(b,sp,330,268);Paste(b,A("Gauge_Ring24",4),335,227);Paste(b,A("Particle_Critical_4",0),329,259);Paste(b,A("Particle_Critical_4",0),348,253);Paste(b,A("Particle_Critical_4",0),360,264);
 Paste(b,A("Fx_EnemyPoof",3),460,238);Paste(b,A("GoalMarker",1),588,256);Reduce(b);Save(Scale(b,3),"Scene_3x.png");
 var hud=Canvas(640,84);Label(hud,"TRAPS / SELECT",16,10,10);string[] names={"Icon_Spike","Icon_ArrowShooter","Icon_DropHammer","Icon_Remove"};string[] labels={"1 SPIKE","2 SHOOTER","3 HAMMER","4 REMOVE"};for(int i=0;i<4;i++){int x=190+i*72;var panel=Canvas(56,62);if(i==0){using(var g=Graphics.FromImage(panel))g.Clear(H("#641F36"));}Paste(panel,A(names[i],0),12,7);Label(panel,labels[i],3,46);Paste(hud,panel,x,10);}Label(hud,"SELECTED: SPIKE",16,36);Reduce(hud);Save(Scale(hud,3),"Hud_3x.png");}
static void Validate(){foreach(var file in Directory.GetFiles(Path.Combine(dest,"_1x"),"*.png")){var b=Read(file);var colors=new HashSet<int>();int bad=0;for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++){var c=b.GetPixel(x,y);if(c.A!=0&&c.A!=255)bad++;if(c.A==255){colors.Add(c.ToArgb());if(!palette.Any(t=>t.ToArgb()==c.ToArgb()))bad++;}}if(colors.Count>16||bad>0)throw new Exception(file+" invalid");report.Add(Path.GetFileName(file)+" | "+b.Width+"x"+b.Height+" | colors="+colors.Count+" | alpha/palette PASS");}File.WriteAllLines(Path.Combine(dest,"validation.txt"),report.ToArray());}
public static void Run(string r){root=r;dest=Path.Combine(r,"NEWIMAG/FxUiConceptV2");palette=File.ReadAllLines(Path.Combine(r,"NEWIMAG/palette_v2.txt")).Where(x=>System.Text.RegularExpressions.Regex.IsMatch(x,"^#[0-9A-Fa-f]{6}$")).Select(H).ToArray();
 Gen("Fx_Dash",32,5);Gen("Fx_Hit",32,4);Gen("Fx_Death",48,6);Gen("Fx_Respawn",48,6);Gen("Fx_Dust",16,4);Gen("Fx_EnemyPoof",48,6);Gen("Particle_Critical",8,1);Gen("Particle_Dud",8,1);Gen("Particle_Beneficial",8,1);Gen("Gauge_Ring24",24,1);Gen("Gauge_Ring32",32,1);Gen("GoalMarker",32,4);Gen("Icon_Spike",32,1);Gen("Icon_ArrowShooter",32,1);Gen("Icon_DropHammer",32,1);Gen("Icon_Remove",32,1);Board();Scene();Validate();}
}
'@
try { Add-Type -TypeDefinition $code -ReferencedAssemblies System.Drawing; [FxBuild]::Run((Get-Location).Path) } finally { $env:TEMP=$oldTemp; $env:TMP=$oldTmp }







