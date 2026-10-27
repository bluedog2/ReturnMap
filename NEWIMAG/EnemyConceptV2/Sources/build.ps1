param([switch]$InspectOnly)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=Split-Path $PSScriptRoot -Parent
$code=@'
using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.Text;
public class EnemyConceptBuild {
 static string[] names={"Goblin_Raider","Imp_Flyer","Skeleton_Archer","Elite_Knight","Orc_Brute"};
 static int[] cells={64,64,96,96,128}, heights={48,44,72,84,96};
 static string[] hex={"19181F","2A282D","3D3940","555058","74666A","8D7B80","B3A1A0","D0BEB8","E7D8CE","47525A","75818A","334629","58783B","88AE43","B8D849","D8EC76","38284D","58377B","8050AD","B581D4"};
 static Color[] pal;
 static Rectangle Bounds(Bitmap b) {int l=b.Width,t=b.Height,r=-1,bt=-1;for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++)if(b.GetPixel(x,y).A>=128){l=Math.Min(l,x);t=Math.Min(t,y);r=Math.Max(r,x);bt=Math.Max(bt,y);}return Rectangle.FromLTRB(l,t,r+1,bt+1);}
 static Color Quant(Color c){if(c.A<128)return Color.FromArgb(0,0,0,0); int best=0; double d0=double.MaxValue;for(int i=0;i<pal.Length;i++){double r=c.R-pal[i].R,g=c.G-pal[i].G,b=c.B-pal[i].B;double d=r*r+g*g+b*b;if(d<d0){d0=d;best=i;}}return pal[best];}
 static Bitmap Scale(Bitmap b,int k){Bitmap z=new Bitmap(b.Width*k,b.Height*k);for(int y=0;y<z.Height;y++)for(int x=0;x<z.Width;x++)z.SetPixel(x,y,b.GetPixel(x/k,y/k));return z;}
 static void Paste(Bitmap target,Bitmap src,int x,int y){using(Graphics g=Graphics.FromImage(target))g.DrawImageUnscaled(src,x,y);}
 static void Label(Bitmap b,string text,int x,int y,int size,Color color){using(Graphics g=Graphics.FromImage(b))using(Font f=new Font("Consolas",size,FontStyle.Regular,GraphicsUnit.Pixel))using(Brush br=new SolidBrush(color)){g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;g.DrawString(text,f,br,x,y);}}
 static Bitmap Canvas(int w,int h){Bitmap b=new Bitmap(w,h);using(Graphics g=Graphics.FromImage(b))g.Clear(ColorTranslator.FromHtml("#19181F"));return b;}
 static void Floor(Bitmap b,Bitmap tile,int y,int rows){for(int r=0;r<rows;r++)for(int x=0;x<b.Width;x+=32)Paste(b,tile,x,y+r*32);}
 static void SaveZoom(Bitmap b,string path,int k){using(Bitmap z=Scale(b,k))z.Save(path,ImageFormat.Png);}
 public static void Run(string root,bool inspect){pal=new Color[hex.Length];for(int i=0;i<hex.Length;i++)pal[i]=ColorTranslator.FromHtml("#"+hex[i]);StringBuilder report=new StringBuilder();report.AppendLine("name,source_width,source_height,source_bbox,cell,height,colors,alpha,bottom_row,grid_note");Bitmap[] sprites=new Bitmap[5];
 for(int i=0;i<5;i++){string source=Path.Combine(root,"Sources",names[i]+"_Selected.png");if(!File.Exists(source))continue;using(Bitmap raw=new Bitmap(source)){Rectangle box=Bounds(raw);Console.WriteLine(names[i]+" source "+raw.Width+"x"+raw.Height+" bbox "+box);if(inspect)continue;
 // Block-center sampling only. No new contours or painted character pixels.
 // Normalize occupied silhouette to the approved height; retain aspect ratio.
 int h=heights[i],w=Math.Min(cells[i]-2,(int)Math.Round(box.Width*(double)h/box.Height));Bitmap sprite=new Bitmap(cells[i],cells[i]);int ox=(cells[i]-w)/2,oy=i==1?(cells[i]-h)/2:cells[i]-h;
 for(int y=0;y<h;y++)for(int x=0;x<w;x++){int sx=box.Left+Math.Min(box.Width-1,(int)((x+0.5)*box.Width/w));int sy=box.Top+Math.Min(box.Height-1,(int)((y+0.5)*box.Height/h));sprite.SetPixel(ox+x,oy+y,Quant(raw.GetPixel(sx,sy)));}
 Rectangle final=Bounds(sprite); if(final.Height!=h)throw new Exception(names[i]+" sampled height mismatch: "+final.Height);
 HashSet<int> colors=new HashSet<int>();HashSet<int> alpha=new HashSet<int>();for(int y=0;y<sprite.Height;y++)for(int x=0;x<sprite.Width;x++){Color c=sprite.GetPixel(x,y);alpha.Add(c.A);if(c.A>0)colors.Add(c.ToArgb());}
 if(colors.Count>20||alpha.Count>2||(i!=1&&final.Bottom!=cells[i]))throw new Exception("Validation failed");
 sprite.Save(Path.Combine(root,"_1x",names[i]+"_Concept.png"),ImageFormat.Png);SaveZoom(sprite,Path.Combine(root,names[i]+"_Concept_6x.png"),6);sprites[i]=sprite;
 report.AppendLine(String.Format("{0},{1},{2},\"{3}\",{4},{5},{6},0/255,{7},source grid approximate; center sampled",names[i],raw.Width,raw.Height,box,cells[i],final.Height,colors.Count,final.Bottom-1));
 }}
 if(inspect)return;
 foreach(Bitmap s in sprites)if(s==null)throw new Exception("Missing selected source");
 using(Bitmap hero=new Bitmap(Path.Combine(root,"Sources","Hero_Idle_Frame01.png")))using(Bitmap tile=new Bitmap(Path.Combine(root,"Sources","Tile_Play_Floor_A.png"))){
 Bitmap tile32=tile;if(tile.Width!=32||tile.Height!=32)throw new Exception("Floor reference must be 32x32");
 using(Bitmap lineup=Canvas(768,240)){int floor=160;Floor(lineup,tile32,floor,1);Rectangle hb=Bounds(hero);Paste(lineup,hero,61,floor-hb.Bottom);int[] xs={181,279,379,490,613};for(int i=0;i<5;i++)Paste(lineup,sprites[i],xs[i],floor-cells[i]);
 Label(lineup,"RE:TRAP / ENEMY CONCEPT V2",12,10,12,Color.FromArgb(231,216,206));Label(lineup,"HERO",86,201,10,Color.FromArgb(231,216,206));string[] shortNames={"GOBLIN","IMP","SKELETON","KNIGHT","ORC"};for(int i=0;i<5;i++){Label(lineup,shortNames[i],xs[i]+4,201,10,Color.FromArgb(216,236,118));Label(lineup,heights[i]+"px",xs[i]+4,216,9,Color.FromArgb(231,216,206));}
 for(int n=0;n<=3;n++)Label(lineup,n+" |--",9,floor-n*32-6,10,Color.FromArgb(231,216,206));Label(lineup,"32px",8,218,9,Color.FromArgb(231,216,206));SaveZoom(lineup,Path.Combine(root,"Lineup_3x.png"),3);}
 using(Bitmap screen=Canvas(640,360)){int floor=296;Floor(screen,tile32,floor,2);Rectangle hb=Bounds(hero);Paste(screen,hero,58,floor-hb.Bottom);Paste(screen,sprites[0],192,floor-64);Paste(screen,sprites[1],302,156);Paste(screen,sprites[2],378,floor-96);Paste(screen,sprites[4],499,floor-128);Label(screen,"RE:TRAP  /  FACTION READABILITY",20,18,12,Color.FromArgb(231,216,206));Label(screen,"HERO: CRIMSON + IVORY",20,38,10,Color.FromArgb(231,216,206));Label(screen,"INTRUDERS: TOXIC GREEN + PURPLE",20,53,10,Color.FromArgb(184,216,73));SaveZoom(screen,Path.Combine(root,"Screen_3x.png"),3);}
 }
 File.WriteAllText(Path.Combine(root,"Sources","validation.csv"),report.ToString(),new UTF8Encoding(false));foreach(Bitmap s in sprites)s.Dispose();Console.WriteLine(report.ToString());
 }
}
'@
Add-Type -TypeDefinition $code -ReferencedAssemblies System.Drawing
[EnemyConceptBuild]::Run($root,$InspectOnly.IsPresent)
