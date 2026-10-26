using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.Text;
public class GraffitiAssembler {
static string root, dest; static Color[] pal; static Color bg=Color.FromArgb(255,25,24,31);
static Color Q(Color c) { int d=int.MaxValue; Color best=pal[0]; foreach(var p in pal){int n=(c.R-p.R)*(c.R-p.R)+(c.G-p.G)*(c.G-p.G)+(c.B-p.B)*(c.B-p.B);if(n<d){d=n;best=p;}}return best; }
static Bitmap Blank(int w,int h){var b=new Bitmap(w,h,PixelFormat.Format32bppArgb);using(var g=Graphics.FromImage(b))g.Clear(bg);return b;}
static void Paste(Bitmap d,Bitmap s,int dx,int dy){using(var g=Graphics.FromImage(d))g.DrawImageUnscaled(s,dx,dy);}
static Bitmap Crop(Bitmap s,int x,int y,int w,int h){return s.Clone(new Rectangle(x,y,w,h),PixelFormat.Format32bppArgb);}
static Bitmap Scale(Bitmap b,int k){var o=Blank(b.Width*k,b.Height*k);using(var g=Graphics.FromImage(o)){g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;g.PixelOffsetMode=System.Drawing.Drawing2D.PixelOffsetMode.Half;g.DrawImage(b,new Rectangle(0,0,o.Width,o.Height),0,0,b.Width,b.Height,GraphicsUnit.Pixel);}return o;}
static void Save(Bitmap b,string p){b.Save(Path.Combine(dest,p),ImageFormat.Png);}
static Bitmap[,] Sample(string file){var s=new Bitmap(file);var t=new Bitmap[3,3];for(int a=0;a<3;a++)for(int b=0;b<3;b++){t[a,b]=Blank(32,32);for(int y=0;y<32;y++)for(int x=0;x<32;x++){int sx=(int)((b+(x+0.5)/32)*s.Width/3);int sy=(int)((a+(y+0.5)/32)*s.Height/3);t[a,b].SetPixel(x,y,Q(s.GetPixel(sx,sy)));}}s.Dispose();return t;}
static uint H(int x,int y){unchecked{uint h=(uint)(x*73856093)^(uint)(y*19349663);h^=h>>13;h*=0x5bd1e995;h^=h>>15;return h;}}
static int Kind(int x,int y){int k=(int)(H(x,y)%4);return k<2?0:k-1;}
static Color BuildColor(Color c){var opts=new Color[]{ColorTranslator.FromHtml("#303940"),ColorTranslator.FromHtml("#3B454C"),ColorTranslator.FromHtml("#47525A"),ColorTranslator.FromHtml("#55616A"),ColorTranslator.FromHtml("#65717A"),ColorTranslator.FromHtml("#75818A"),ColorTranslator.FromHtml("#89959D")};double l=.2126*c.R+.7152*c.G+.0722*c.B;double target=55+l*.30;int best=0;double err=999;for(int i=0;i<opts.Length;i++){double v=.2126*opts[i].R+.7152*opts[i].G+.0722*opts[i].B; if(Math.Abs(target-v)<err){err=Math.Abs(target-v);best=i;}}return opts[best];}
static Bitmap[,] Build(Bitmap[,] a){var t=new Bitmap[3,3];for(int i=0;i<3;i++)for(int j=0;j<3;j++){t[i,j]=Blank(32,32);for(int y=0;y<32;y++)for(int x=0;x<32;x++)t[i,j].SetPixel(x,y,BuildColor(a[i,j].GetPixel(x,y)));}return t;}
static bool Paint(Color c){return c.R>c.G*1.25 && c.R>85 || c.R>185 && c.G>175 && c.B>165;}
static void Export(Bitmap[,] t,string kind,string sub){for(int i=0;i<3;i++)for(int j=0;j<3;j++)Save(t[i,j],"_1x/"+sub+kind+(kind=="Floor"?"_L":"_T")+i+(kind=="Floor"?"_R":"_B")+j+".png");}
static void Sheet(Bitmap[,] t,string kind){var s=Blank(580,580);for(int i=0;i<3;i++)for(int j=0;j<3;j++)using(var z=Scale(t[i,j],6))Paste(s,z,j*194,i*194);Save(s,"Sheet_"+kind+"_6x.png");s.Dispose();}
static long Check(Bitmap[,] t,bool wall){long n=0;for(int k=0;k<3;k++)for(int a=0;a<3;a++)for(int b=0;b<3;b++)for(int z=0;z<32;z++)for(int d=0;d<2;d++){var c=wall?t[a,k].GetPixel(z,30+d):t[a,k].GetPixel(30+d,z);var e=wall?t[k,b].GetPixel(z,d):t[k,b].GetPixel(d,z);if(c.ToArgb()==e.ToArgb())n++;}return n;}
static bool Connected(Bitmap b,bool wall){var seen=new bool[32,32];var q=new Queue<Point>();for(int z=0;z<32;z++){int x=wall?z:0,y=wall?0:z;if((wall||y>=4)&&Paint(b.GetPixel(x,y))){seen[x,y]=true;q.Enqueue(new Point(x,y));}}while(q.Count>0){var p=q.Dequeue();if((wall?p.Y:p.X)==31)return true;for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++){int x=p.X+dx,y=p.Y+dy;if(x>=0&&x<32&&y>=(wall?0:4)&&y<32&&!seen[x,y]&&Paint(b.GetPixel(x,y))){seen[x,y]=true;q.Enqueue(new Point(x,y));}}}return false;}
static string Metrics(Bitmap[,] f,Bitmap[,] w,Bitmap[,] fb,Bitmap[,] wb){
var r=new StringBuilder();r.AppendLine("Paint metrics: crimson/ivory mask, Floor excludes y<7 (platform lip and source lip drips). Width coverage = columns containing paint /32; area coverage = paint pixels /1024; span and height = bounding box. Charcoal outline excluded.");
foreach(bool wall in new[]{false,true}){var t=wall?w:f;for(int a=0;a<3;a++)for(int b=0;b<3;b++){int count=0,minX=32,maxX=-1,minY=32,maxY=-1;var cols=new HashSet<int>();for(int y=wall?0:7;y<32;y++)for(int x=0;x<32;x++)if(Paint(t[a,b].GetPixel(x,y))){count++;cols.Add(x);minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minY=Math.Min(minY,y);maxY=Math.Max(maxY,y);}r.AppendLine((wall?"Wall":"Floor")+a+b+": painted columns="+cols.Count+"/32 ("+(cols.Count*100.0/32).ToString("F2")+"%), bbox width="+(maxX<0?0:maxX-minX+1)+", height="+(maxY<0?0:maxY-minY+1)+", area="+(count*100.0/1024).ToString("F2")+"%.");if((a!=0||b!=0)&&cols.Count<23)throw new Exception("Paint width below70%");}}
foreach(string kind in new[]{"Floor","Wall"})foreach(int k in new[]{1,2}){using(var e=new Bitmap(Path.Combine(dest,"Edges/"+kind+"_"+k+".png"))){int run=0,longest=0,start=-1,bestStart=-1;for(int z=kind=="Floor"?7:0;z<32;z++){Color c=kind=="Floor"?e.GetPixel(0,z):e.GetPixel(z,0);bool on=k==1?(c.R>c.G*1.25&&c.R>85):(c.R>185&&c.G>175&&c.B>165);if(on){if(run==0)start=z;run++;if(run>longest){longest=run;bestStart=start;}}else run=0;}r.AppendLine(kind+" type"+k+" primary port thickness="+longest+"px, coordinates="+bestStart+".."+(bestStart+longest-1)+" (crimson/ivory only; fringe/outline excluded).");}}
for(int a=0;a<3;a++)for(int b=0;b<3;b++)for(int y=0;y<32;y++)for(int x=0;x<32;x++){if(fb[a,b].GetPixel(x,y).ToArgb()!=BuildColor(f[a,b].GetPixel(x,y)).ToArgb()||wb[a,b].GetPixel(x,y).ToArgb()!=BuildColor(w[a,b].GetPixel(x,y)).ToArgb())throw new Exception("Build mapping");if(y<3&&f[a,b].GetPixel(x,y).ToArgb()!=f[0,0].GetPixel(x,y).ToArgb())throw new Exception("Floor lip mismatch");}
r.AppendLine("Build exact coordinate-preserving palette substitution PASS. Shared Floor top3 rows PASS.");return r.ToString();}
static Dictionary<string,Bitmap> variants=new Dictionary<string,Bitmap>();
static StringBuilder repairLog=new StringBuilder();
static Bitmap[,] Load(string kind){var a=new Bitmap[3,3];for(int i=0;i<3;i++)for(int j=0;j<3;j++)a[i,j]=new Bitmap(Path.Combine(root,"NEWIMAG/GraffitiTiles2/_1x/"+kind+(kind=="Floor"?"_L":"_T")+i+(kind=="Floor"?"_R":"_B")+j+".png"));return a;}
static double Lum(Color c){return .2126*c.R+.7152*c.G+.0722*c.B;}
static bool Concrete(Color c){return !Paint(c)&&c.R>=80&&Math.Abs(c.R-c.G)<35;}
static Color At(Bitmap b,bool wall,int d,int z){return wall?b.GetPixel(z,d):b.GetPixel(d,z);}
static void Put(Bitmap b,bool wall,int d,int z,Color c){if(wall)b.SetPixel(z,d,c);else b.SetPixel(d,z,c);}
static bool Band(bool wall,int k,int z){return k!=0&&(wall?(k==1?z>=12&&z<=20:z>=13&&z<=18):(k==1?z>=12&&z<=21:z>=14&&z<=21));}
static void Repair(Bitmap[,] a,bool wall){
string kind=wall?"Wall":"Floor";var clean=(Bitmap)a[0,0].Clone();
// Candidate near-edge interior source sections; no synthesized artwork.
int best=3;double bestErr=999;double target=0;int n=0;
foreach(var b in a)for(int d=4;d<28;d++)for(int z=wall?0:7;z<32;z++){var c=At(b,wall,d,z);if(Concrete(c)){target+=Lum(c);n++;}}target/=n;
for(int d=3;d<=9;d++){double sum=0;int count=0;for(int z=wall?0:7;z<32;z++){var c=At(clean,wall,d,z);if(Concrete(c)){sum+=Lum(c);count++;}}double err=Math.Abs(sum/count-target);if(err<bestErr){bestErr=err;best=d;}}
var baseProfile=new Color[32];for(int z=0;z<32;z++){
var c=At(clean,wall,best,z);
// Remove sampled outer frame / deep crack pixels from a repeated standard section.
if((wall||z>=3)&&!Concrete(c)){int near=best;double err=999;for(int d=3;d<=12;d++){var cc=At(clean,wall,d,z);if(Concrete(cc)&&Math.Abs(Lum(cc)-target)<err){near=d;err=Math.Abs(Lum(cc)-target);}}c=At(clean,wall,near,z);if(!Concrete(c))c=At(clean,wall,best,wall?8:8);}
baseProfile[z]=c;}
repairLog.AppendLine(kind+" concrete source: 00 interior "+(wall?"row":"column")+best+"; target concrete luma="+target.ToString("F3")+"; isolated dark source pixels replaced with same-row/column nearby concrete samples.");
for(int k=0;k<3;k++){
var old=new Bitmap(Path.Combine(root,"NEWIMAG/GraffitiTiles2/Edges/"+kind+"_"+k+".png"));var edge=Blank(wall?32:2,wall?2:32);
for(int z=0;z<32;z++){Color c=Band(wall,k,z)?At(old,wall,0,z):baseProfile[z];if(!wall&&z<3)c=At(old,wall,0,z);for(int d=0;d<2;d++)Put(edge,wall,d,z,c);}
Save(edge,"Edges/"+kind+"_"+k+".png");
for(int j=0;j<3;j++)for(int z=0;z<32;z++)for(int d=0;d<2;d++){Put(a[k,j],wall,d,z,At(edge,wall,d,z));Put(a[j,k],wall,30+d,z,At(edge,wall,d,z));}
// Residual outer frame just inside the old 2px overwrite: replace only concrete-region dark pixels.
for(int j=0;j<3;j++)for(int z=wall?0:3;z<32;z++)if(!Band(wall,k,z))for(int d=2;d<4;d++){
if(Lum(At(a[k,j],wall,d,z))<75&&!Paint(At(a[k,j],wall,d,z)))Put(a[k,j],wall,d,z,baseProfile[z]);
if(Lum(At(a[j,k],wall,31-d,z))<75&&!Paint(At(a[j,k],wall,31-d,z)))Put(a[j,k],wall,31-d,z,baseProfile[z]);}
}
}
static Bitmap Pick(Bitmap[,] a,int l,int r,int x,int y,bool wall,bool build){if(l==r&&l>0&&((H(x,y)>>8)&1)==1)return variants[(wall?"Wall":"Floor")+l+(build?"B":"P")];return a[l,r];}
static void Variants(Bitmap[,] f,Bitmap[,] w){foreach(bool wall in new[]{false,true}){var a=wall?w:f;string kind=wall?"Wall":"Floor";
for(int k=1;k<3;k++){var b=(Bitmap)a[0,0].Clone();using(var edge=new Bitmap(Path.Combine(dest,"Edges/"+kind+"_"+k+".png"))){for(int d=0;d<32;d++)for(int z=0;z<32;z++)if(Band(wall,k,z))Put(b,wall,d,z,At(edge,wall,d%2,z));for(int d=0;d<2;d++)for(int z=0;z<32;z++){Put(b,wall,d,z,At(edge,wall,d,z));Put(b,wall,30+d,z,At(edge,wall,d,z));}}
var bb=Blank(32,32);for(int y=0;y<32;y++)for(int x=0;x<32;x++)bb.SetPixel(x,y,BuildColor(b.GetPixel(x,y)));variants[kind+k+"P"]=b;variants[kind+k+"B"]=bb;string name=kind+(wall?"_T":"_L")+k+(wall?"_B":"_R")+k;Save(b,"_1x/"+name+"_b.png");Save(bb,"_1x/Build/"+name+"_b.png");}
var sheet=Blank(580,774);using(var original=new Bitmap(Path.Combine(dest,"Sheet_"+kind+"_6x.png")))Paste(sheet,original,0,0);for(int k=1;k<3;k++)using(var z=Scale(variants[kind+k+"P"],6))Paste(sheet,z,(k-1)*194,582);Save(sheet,"Sheet_"+kind+"_6x.png");}}
static string Audit(Bitmap[,] f,Bitmap[,] w,Bitmap[,] fb,Bitmap[,] wb){var r=new StringBuilder();r.Append(repairLog);int total=0,match=0,maxColors=0;double worst=0;var profiles=new StringBuilder();
foreach(bool wall in new[]{false,true})foreach(bool build in new[]{false,true}){
var a=wall?(build?wb:w):(build?fb:f);string kind=wall?"Wall":"Floor";var tiles=new List<Bitmap>();var ins=new List<int>();var outs=new List<int>();var names=new List<string>();for(int i=0;i<3;i++)for(int j=0;j<3;j++){tiles.Add(a[i,j]);ins.Add(i);outs.Add(j);names.Add(kind+i+j+"a");}for(int k=1;k<3;k++){tiles.Add(variants[kind+k+(build?"B":"P")]);ins.Add(k);outs.Add(k);names.Add(kind+k+k+"b");}
for(int i=0;i<tiles.Count;i++){
var colors=new HashSet<int>();for(int y=0;y<32;y++)for(int x=0;x<32;x++){var c=tiles[i].GetPixel(x,y);colors.Add(c.ToArgb());if(c.A!=255||!Array.Exists(pal,p=>p.ToArgb()==c.ToArgb()))throw new Exception("Alpha/palette");if(build){Bitmap play=i<9?(wall?w:f)[ins[i],outs[i]]:variants[kind+ins[i]+"P"];if(c.ToArgb()!=BuildColor(play.GetPixel(x,y)).ToArgb())throw new Exception("Build substitution mismatch");} }maxColors=Math.Max(maxColors,colors.Count);
for(int j=0;j<tiles.Count;j++)if(outs[i]==ins[j]){for(int d=0;d<2;d++)for(int z=0;z<32;z++){total++;if(At(tiles[i],wall,30+d,z).ToArgb()==At(tiles[j],wall,d,z).ToArgb())match++;}
// Reference concrete: warm neutral concrete colors, excludes paint, charcoal motif outlines, cracks and floor lip.
if(!build){double inner=0;int ni=0;foreach(var b in new[]{tiles[i],tiles[j]})for(int d=4;d<28;d++)for(int z=wall?0:7;z<32;z++)if(Concrete(At(b,wall,d,z))){inner+=Lum(At(b,wall,d,z));ni++;}inner/=ni;double edge=0;int ne=0;for(int z=wall?0:7;z<32;z++)if(!Band(wall,outs[i],z)){for(int d=0;d<2;d++){edge+=Lum(At(tiles[i],wall,30+d,z))+Lum(At(tiles[j],wall,d,z));ne+=2;}}edge/=ne;double diff=(edge/inner-1)*100;worst=Math.Max(worst,Math.Abs(diff));r.AppendLine(names[i]+" -> "+names[j]+": inner="+inner.ToString("F3")+", boundary="+edge.ToString("F3")+", delta="+diff.ToString("F3")+"%");}
}}
}
for(int k=0;k<3;k++)foreach(string kind in new[]{"Floor","Wall"})using(var e=new Bitmap(Path.Combine(dest,"Edges/"+kind+"_"+k+".png")))for(int z=0;z<32;z++)profiles.AppendLine(kind+" "+k+" "+z+": "+At(e,kind=="Wall",0,z).ToArgb().ToString("X8"));File.WriteAllText(Path.Combine(dest,"Edges/profiles.txt"),profiles.ToString());
int dark=0,checkedBg=0;foreach(bool isWall in new[]{false,true})for(int k=0;k<3;k++){string family=isWall?"Wall":"Floor";using(var e=new Bitmap(Path.Combine(dest,"Edges/"+family+"_"+k+".png")))using(var old=new Bitmap(Path.Combine(root,"NEWIMAG/GraffitiTiles2/Edges/"+family+"_"+k+".png"))){double before=0,after=0;int count=0;for(int z=isWall?0:7;z<32;z++)if(!Band(isWall,k,z)){double v=Lum(At(e,isWall,0,z));before+=Lum(At(old,isWall,0,z));after+=v;count++;checkedBg++;if(v<75)dark++;}r.AppendLine(family+" kind"+k+" concrete profile before="+(before/count).ToString("F3")+", after="+(after/count).ToString("F3"));}}
r.AppendLine("Dark charcoal concrete profile pixels (luma<75): "+dark+"/"+checkedBg+"; no repeated charcoal boundary bar. Build coordinate-preserving substitution all44 PASS.");if(dark!=0||worst>6||match!=total||maxColors>24)throw new Exception("Quality gate failed");
int empty=0,zero=0,bvar=0;foreach(int y in new[]{0,7,19})for(int x=0;x<24;x++){int l=x==0?0:Kind(x-1,y),rr=x==23?0:Kind(x,y);if(l==0&&rr==0)empty++;if(x<23&&Kind(x,y)==0)zero++;if(l==rr&&l>0&&((H(x,y)>>8)&1)==1)bvar++;}
r.AppendLine("All variants included boundary match="+match+"/"+total+"; max colors="+maxColors+"; all 44 tiles alpha255/palette PASS.");r.AppendLine("Worst adjacent-pair concrete brightness delta="+worst.ToString("F3")+"%; requirement <=6%.");r.AppendLine("Strip: empty 00="+empty+"/72 ("+(empty*100.0/72).ToString("F3")+"%); zero internal edges="+zero+"/69; b variants="+bvar+"/72.");return r.ToString();}
static void CompareStrip(){using(var old=new Bitmap(Path.Combine(root,"NEWIMAG/GraffitiTiles2/Strip_3x.png")))using(var current=new Bitmap(Path.Combine(dest,"Strip_3x.png"))){var c=Blank(2304,288);using(var a=Crop(old,0,0,2304,96))Paste(c,a,0,48);using(var b=Crop(current,0,0,2304,96))Paste(c,b,0,192);using(var g=Graphics.FromImage(c))using(var font=new Font("Malgun Gothic",23,FontStyle.Bold,GraphicsUnit.Pixel)){g.DrawString("2차",font,Brushes.White,20,9);g.DrawString("3차: 경계 정리·밀도 낮춤",font,Brushes.White,20,153);}Save(c,"Compare_Strip_3x.png");}}

public static void Run(string repo,string floor,string wall,int[] grid,int width,int height,int[] slots,int spawnX,int spawnY){root=repo;dest=Path.Combine(root,"NEWIMAG/GraffitiTiles3");Directory.CreateDirectory(Path.Combine(dest,"_1x/Build"));Directory.CreateDirectory(Path.Combine(dest,"Sources"));Directory.CreateDirectory(Path.Combine(dest,"Edges"));
var colors=new List<Color>();foreach(var file in new[]{"NEWIMAG/palette_v2.txt","NEWIMAG/TileConceptV2/A/palette_add_A.txt"})foreach(string l in File.ReadAllLines(Path.Combine(root,file)))if(System.Text.RegularExpressions.Regex.IsMatch(l.Trim(),"^#[0-9a-fA-F]{6}$"))colors.Add(ColorTranslator.FromHtml(l.Trim()));
// One shared subset prevents seam repair from increasing a tile past 24 colors.
var wanted=new HashSet<string>(new[]{"#19181F","#2A282D","#303940","#3B454C","#47525A","#55616A","#65717A","#75818A","#89959D","#9CA7AC","#3D3940","#555058","#74666A","#8D7B80","#B3A1A0","#D0BEB8","#641F36","#92223F","#B83450","#DC526B","#E7D8CE","#F7EEE4"});colors.RemoveAll(c=>!wanted.Contains("#"+c.R.ToString("X2")+c.G.ToString("X2")+c.B.ToString("X2")));pal=colors.ToArray();
var f=Load("Floor");var w=Load("Wall");
Repair(f,false);Repair(w,true);
var fb=Build(f);var wb=Build(w);Export(f,"Floor","");Export(w,"Wall","");Export(fb,"Floor","Build/");Export(wb,"Wall","Build/");Sheet(f,"Floor");Sheet(w,"Wall");Variants(f,w);
var strip=Blank(768,112);var stripB=Blank(768,112);int[] ys={0,7,19};var seq=new StringBuilder();for(int row=0;row<3;row++){seq.Append("y="+ys[row]+": ");for(int x=0;x<24;x++){int l=x==0?0:Kind(x-1,ys[row]);int r=x==23?0:Kind(x,ys[row]);Paste(strip,Pick(f,l,r,x,ys[row],false,false),x*32,row*40);Paste(stripB,Pick(fb,l,r,x,ys[row],false,true),x*32,row*40);seq.Append(l+""+r+((l==r&&l>0&&((H(x,ys[row])>>8)&1)==1)?"b ":"a "));}seq.AppendLine();}using(var z=Scale(strip,3))Save(z,"Strip_3x.png");using(var z=Scale(stripB,3))Save(z,"Strip_Build_3x.png");File.WriteAllText(Path.Combine(dest,"strip_sequence.txt"),seq.ToString());
var vs=Blank(112,768);for(int col=0;col<3;col++)for(int y=0;y<24;y++){int t=y==23?0:Kind(ys[col],y);int b=y==0?0:Kind(ys[col],y-1);Paste(vs,Pick(w,t,b,ys[col],y,true,false),col*40,(23-y)*32);}using(var z=Scale(vs,3))Save(z,"Strip_Wall_3x.png");
var terrain=(int[])grid.Clone();for(int i=0;i<slots.Length;i+=3)terrain[slots[i+1]*width+slots[i]]=slots[i+2];Func<int,int,int> cell=(x,y)=>x<0||x>=width||y<0||y>=height?0:terrain[y*width+x];
// Camera 640x360, bottom-left world crop; spawn near lower left as in the reference.
{var mockBuild=Blank(640,360);for(int y=0;y<height;y++)for(int x=0;x<width;x++){int v=cell(x,y);if(v==0)continue;Bitmap tile;if(v==2){int t=cell(x,y+1)==v?Kind(x,y):0;int b=cell(x,y-1)==v?Kind(x,y-1):0;tile=Pick(wb,t,b,x,y,true,true);}else{int l=cell(x-1,y)==v?Kind(x-1,y):0;int r=cell(x+1,y)==v?Kind(x,y):0;tile=Pick(fb,l,r,x,y,false,true);}Paste(mockBuild,tile,x*32,360-(y+1)*32);}
var heroSheetBuild=new Bitmap(Path.Combine(root,"NEWIMAG/Px32HeroV2/_1x/Hero_Idle.png"));var heroBuild=Crop(heroSheetBuild,0,0,96,96);int footBuild=95;for(int y=95;y>=0;y--){bool any=false;for(int x=0;x<96;x++)if(heroBuild.GetPixel(x,y).A>20)any=true;if(any){footBuild=y;break;}}int floorYBuild=spawnY-1;while(floorYBuild>=0&&cell(spawnX,floorYBuild)==0)floorYBuild--;int screenFootBuild=360-(floorYBuild+1)*32;Paste(mockBuild,heroBuild,spawnX*32+16-48,screenFootBuild-footBuild-1);var mock3Build=Scale(mockBuild,3);Save(mock3Build,"Mock_Build_3x.png");
}var mock=Blank(640,360);for(int y=0;y<height;y++)for(int x=0;x<width;x++){int v=cell(x,y);if(v==0)continue;Bitmap tile;if(v==2){int t=cell(x,y+1)==v?Kind(x,y):0;int b=cell(x,y-1)==v?Kind(x,y-1):0;tile=Pick(w,t,b,x,y,true,false);}else{int l=cell(x-1,y)==v?Kind(x-1,y):0;int r=cell(x+1,y)==v?Kind(x,y):0;tile=Pick(f,l,r,x,y,false,false);}Paste(mock,tile,x*32,360-(y+1)*32);}
var heroSheet=new Bitmap(Path.Combine(root,"NEWIMAG/Px32HeroV2/_1x/Hero_Idle.png"));var hero=Crop(heroSheet,0,0,96,96);int foot=95;for(int y=95;y>=0;y--){bool any=false;for(int x=0;x<96;x++)if(hero.GetPixel(x,y).A>20)any=true;if(any){foot=y;break;}}int floorY=spawnY-1;while(floorY>=0&&cell(spawnX,floorY)==0)floorY--;int screenFoot=360-(floorY+1)*32;Paste(mock,hero,spawnX*32+16-48,screenFoot-foot-1);var mock3=Scale(mock,3);Save(mock3,"Mock_3x.png");
var old=new Bitmap(Path.Combine(root,"NEWIMAG/GraffitiTiles2/Mock_3x.png"));var comp=Blank(1920,2256);Paste(comp,old,0,48);Paste(comp,mock3,0,1176);using(var g=Graphics.FromImage(comp))using(var font=new Font("Malgun Gothic",23,FontStyle.Bold,GraphicsUnit.Pixel)){g.DrawString("2차",font,Brushes.White,20,9);g.DrawString("3차: 경계 정리·밀도 낮춤",font,Brushes.White,20,1137);}Save(comp,"Compare_3x.png");
var report=new StringBuilder();report.AppendLine("Boundary 2px paired RGBA pixels: Floor "+Check(f,false)+"/1728; Wall "+Check(w,true)+"/1728; Build Floor "+Check(fb,false)+"/1728; Build Wall "+Check(wb,true)+"/1728.");int max=0;foreach(var set in new[]{f,w,fb,wb})foreach(var tile in set){var cs=new HashSet<int>();for(int y=0;y<32;y++)for(int x=0;x<32;x++){var c=tile.GetPixel(x,y);if(c.A!=255)throw new Exception("alpha");if(!colors.Exists(p=>p.ToArgb()==c.ToArgb()))throw new Exception("Palette mismatch: "+c);cs.Add(c.ToArgb());}max=Math.Max(max,cs.Count);}report.AppendLine("All 36 tiles: 32x32, alpha 255. Maximum colors: "+max+".");for(int a=1;a<3;a++)for(int b=1;b<3;b++)report.AppendLine("8-neighbor paint connectivity Floor "+a+b+": "+Connected(f[a,b],false)+"; Wall "+a+b+": "+Connected(w[a,b],true));report.AppendLine("Map dimensions "+width+"x"+height+"; trapSlots "+slots.Length/3+"; spawn "+spawnX+","+spawnY+"; camera world x=[0,640), y=[0,360). Hero visible foot y="+screenFoot+".");report.AppendLine(Audit(f,w,fb,wb));File.WriteAllText(Path.Combine(dest,"validation.txt"),report.ToString());CompareStrip();Console.WriteLine(report);
}
}

