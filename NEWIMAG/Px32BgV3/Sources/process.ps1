$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$src = @'
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
public static class BgV3 {
 static Color Clear=Color.FromArgb(0,0,0,0);
 static Color Closest(Color c,Color[] p){ int best=int.MaxValue; Color ans=p[0]; foreach(var q in p){int r=c.R-q.R,g=c.G-q.G,b=c.B-q.B,d=r*r+g*g+b*b;if(d<best){best=d;ans=q;}}return ans; }
 static Bitmap Sample(Bitmap a,int w,int h){if(a.Width<w||a.Height<h)throw new Exception("Upsampling forbidden");var b=new Bitmap(w,h);for(int y=0;y<h;y++)for(int x=0;x<w;x++)b.SetPixel(x,y,a.GetPixel(Math.Min(a.Width-1,(int)((x+.5)*a.Width/w)),Math.Min(a.Height-1,(int)((y+.5)*a.Height/h))));return b;}
 static void Paste(Bitmap b,Bitmap a,int ox,int oy,int zoom){for(int y=0;y<a.Height;y++)for(int x=0;x<a.Width;x++){var c=a.GetPixel(x,y);if(c.A==0)continue;for(int j=0;j<zoom;j++)for(int i=0;i<zoom;i++)b.SetPixel(ox+x*zoom+i,oy+y*zoom+j,c);}}
 static void Label(Bitmap b,string s,int x,int y){using(var g=Graphics.FromImage(b))using(var f=new Font("Consolas",22,FontStyle.Bold,GraphicsUnit.Pixel))using(var brush=new SolidBrush(Color.White)){g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;g.DrawString(s,f,brush,x,y);}}
 static Bitmap Process(string path,bool near,bool warm,List<string> report,string name){
 var p=(warm?new[]{"#705E5B","#89716B","#A08078","#B58B7D","#C39486","#D6A49A","#DEB49E","#E5C2AF"}:new[]{"#191F2B","#202735","#272D3B","#302B36","#38313D","#19181F","#2A282D","#3D3940","#555058","#641F36"}).Select(ColorTranslator.FromHtml).ToArray();
 if(!near)p=(warm?new[]{"#705E5B","#89716B","#A08078","#C39486","#DEB49E"}:new[]{"#191F2B","#272D3B","#302B36","#19181F","#555058","#641F36"}).Select(ColorTranslator.FromHtml).ToArray();
 Bitmap b;using(var raw=new Bitmap(path)){report.Add(name+" source="+raw.Width+"x"+raw.Height);b=Sample(raw,960,near?384:540);}
 for(int y=0;y<b.Height;y++)for(int x=0;x<960;x++){var c=b.GetPixel(x,y);b.SetPixel(x,y,near&&c.A<128?Clear:Closest(c,p));}
 // Remove isolated quantization specks, preserving authored silhouette and alpha.
 for(int pass=0;pass<2;pass++)using(var a=new Bitmap(b)){for(int y=1;y<b.Height-1;y++)for(int x=0;x<960;x++){var c=a.GetPixel(x,y);if(c.A==0)continue;var count=new Dictionary<int,int>();for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++){var q=a.GetPixel((x+dx+960)%960,y+dy);if(q.A==0)continue;int k=q.ToArgb();if(!count.ContainsKey(k))count[k]=0;count[k]++;}var win=count.OrderByDescending(t=>t.Value).First();if(count[c.ToArgb()]<=2&&win.Value>=6)b.SetPixel(x,y,Color.FromArgb(win.Key));}}
 // Connected isolated-color cleanup: replace tiny opaque islands by adjacent majority.
 using(var a=new Bitmap(b)){int h=b.Height;var seen=new bool[960*h];for(int sy=0;sy<h;sy++)for(int sx=0;sx<960;sx++){int start=sy*960+sx;if(seen[start])continue;var c=a.GetPixel(sx,sy);if(c.A==0){seen[start]=true;continue;}var todo=new Queue<int>();var component=new List<int>();var border=new Dictionary<int,int>();todo.Enqueue(start);seen[start]=true;while(todo.Count>0){int idx=todo.Dequeue();component.Add(idx);int x=idx%960,y=idx/960;foreach(int d in new[]{-1,1,-960,960}){int nx=d==-1?(x+959)%960:d==1?(x+1)%960:x;int ny=d==-960?y-1:d==960?y+1:y;if(ny<0||ny>=h)continue;int j=ny*960+nx;var q=a.GetPixel(nx,ny);if(q.ToArgb()==c.ToArgb()){if(!seen[j]){seen[j]=true;todo.Enqueue(j);}}else if(q.A==255){int key=q.ToArgb();if(!border.ContainsKey(key))border[key]=0;border[key]++;}}}if(component.Count<=5&&border.Count>0){var fill=Color.FromArgb(border.OrderByDescending(kv=>kv.Value).First().Key);foreach(int j in component)b.SetPixel(j%960,j/960,fill);}}}
 // Approved v2 seam method: align eight edge columns, palette-remap blends.
 using(var a=new Bitmap(b)){for(int y=0;y<b.Height;y++){var l=a.GetPixel(0,y);var r=a.GetPixel(959,y);var common=l.A==0?r:r.A==0?l:Closest(Color.FromArgb((l.R+r.R)/2,(l.G+r.G)/2,(l.B+r.B)/2),p);bool opaque=!near||(l.A+r.A>=255);for(int k=0;k<8;k++){double t=(8-k)/8.0;foreach(int x in new[]{k,959-k}){var c=a.GetPixel(x,y);if(!(k==0?opaque:c.A>=128)){b.SetPixel(x,y,Clear);continue;}b.SetPixel(x,y,Closest(Color.FromArgb((int)Math.Round(c.R*(1-t)+common.R*t),(int)Math.Round(c.G*(1-t)+common.G*t),(int)Math.Round(c.B*(1-t)+common.B*t)),p));}}}}
 for(int y=0;y<b.Height;y++)b.SetPixel(959,y,b.GetPixel(0,y));
 if(!near){var sky=Enumerable.Range(0,960).Select(x=>b.GetPixel(x,0).ToArgb()).GroupBy(k=>k).OrderByDescending(g=>g.Count()).First().Key;for(int x=0;x<960;x++)b.SetPixel(x,0,Color.FromArgb(sky));}
 return b;
 }
 public static void Run(string root){
 var names=new[]{"Play_Far","Play_Near","Verification_Far","Verification_Near"};var layers=new List<Bitmap>();var notes=new List<string>();var rows=new List<string>();
 var allowed=new HashSet<int>(File.ReadAllLines(Path.Combine(root,"../palette_v2.txt")).Where(s=>System.Text.RegularExpressions.Regex.IsMatch(s.Trim(),"^#[0-9A-Fa-f]{6}$")).Select(s=>ColorTranslator.FromHtml(s.Trim()).ToArgb()));
 for(int i=0;i<4;i++){bool near=i%2==1;var b=Process(Path.Combine(root,"Sources/"+names[i]+"_raw.png"),near,i>=2,notes,names[i]);layers.Add(b);var colors=new HashSet<int>();var alpha=new HashSet<int>();var top=new HashSet<int>();int edges=0,off=0,bottom=0,topOpaque=0,maxDiff=0;for(int y=0;y<b.Height;y++){var l=b.GetPixel(0,y);var r=b.GetPixel(959,y);if(l.ToArgb()!=r.ToArgb())edges++;maxDiff=Math.Max(maxDiff,Math.Max(Math.Abs(l.A-r.A),Math.Max(Math.Abs(l.R-r.R),Math.Max(Math.Abs(l.G-r.G),Math.Abs(l.B-r.B)))));for(int x=0;x<960;x++){var c=b.GetPixel(x,y);alpha.Add(c.A);if(c.A>0){colors.Add(c.ToArgb());if(!allowed.Contains(c.ToArgb()))off++;}if(y==0){top.Add(c.ToArgb());if(c.A>0)topOpaque++;}if(y==b.Height-1&&c.A!=255)bottom++;}}
 if(colors.Count>24||off>0||edges>0||alpha.Any(a=>a!=0&&a!=255)||bottom>0||(!near&&alpha.Contains(0))||(near&&topOpaque>0)||(!near&&top.Count!=1))throw new Exception("Validation failed: "+names[i]+" bottom="+bottom+" top="+topOpaque);
 b.Save(Path.Combine(root,"Backgrounds/"+names[i]+".png"),ImageFormat.Png);
 rows.Add("| "+names[i]+".png | 960×"+b.Height+" | "+colors.Count+" | "+(colors.Count+(alpha.Contains(0)?1:0))+" | "+string.Join("/",alpha.OrderBy(a=>a))+" | "+edges+" | "+maxDiff+" | "+off+" | "+bottom+" |");
 }
 // Contact sheet: two 2x screens, then each phase repeated twice at 2x.
 using(var preview=new Bitmap(3840,3360)){using(var g=Graphics.FromImage(preview))g.Clear(Color.FromArgb(25,24,31));
 for(int phase=0;phase<2;phase++)using(var composite=new Bitmap(layers[phase*2])){Paste(composite,layers[phase*2+1],0,156,1);Label(preview,names[phase*2].Split('_')[0]+" / 960x540 / 2x",phase*1920+12,8);Paste(preview,composite,phase*1920,40,2);int sy=1120+phase*1120;Label(preview,names[phase*2].Split('_')[0]+" / horizontal repeat / seam x=1920",12,sy+8);Paste(preview,composite,0,sy+40,2);Paste(preview,composite,1920,sy+40,2);}
 preview.Save(Path.Combine(root,"Preview_2x.png"),ImageFormat.Png);}
 File.WriteAllLines(Path.Combine(root,"Sources/validation.txt"),notes.Concat(rows));Console.WriteLine(string.Join(Environment.NewLine,notes.Concat(rows)));
 foreach(var b in layers)b.Dispose();
 }
}
'@
Add-Type -TypeDefinition $src -ReferencedAssemblies System.Drawing
[BgV3]::Run((Resolve-Path "$PSScriptRoot/..").Path)

