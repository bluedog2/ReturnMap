$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=Split-Path $PSScriptRoot -Parent
$code=@'
using System;
using System.IO;
using System.Drawing;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
public class VerifyEnemyConcepts {
 public static void Run(string root){
 string repo=Directory.GetParent(Directory.GetParent(root).FullName).FullName;
 HashSet<int> allowed=new HashSet<int>();foreach(string p in new[]{Path.Combine(repo,"NEWIMAG","palette_v2.txt"),Path.Combine(root,"palette_add_enemies.txt")})foreach(string line in File.ReadAllLines(p))if(Regex.IsMatch(line.Trim(),"^#[0-9A-Fa-f]{6}$"))allowed.Add(ColorTranslator.FromHtml(line.Trim()).ToArgb());
 string[] names={"Goblin_Raider","Imp_Flyer","Skeleton_Archer","Elite_Knight","Orc_Brute"};int[] sizes={64,64,96,96,128},heights={48,44,72,84,96};StringBuilder log=new StringBuilder();
 for(int i=0;i<5;i++)using(Bitmap b=new Bitmap(Path.Combine(root,"_1x",names[i]+"_Concept.png")))using(Bitmap z=new Bitmap(Path.Combine(root,names[i]+"_Concept_6x.png"))){int t=b.Height,bt=-1;HashSet<int> colors=new HashSet<int>();if(b.Width!=sizes[i]||b.Height!=sizes[i])throw new Exception("Cell mismatch");for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++){Color c=b.GetPixel(x,y);if(c.A!=0&&c.A!=255)throw new Exception("Nonbinary alpha");if(c.A==255){if(!allowed.Contains(c.ToArgb()))throw new Exception("Color outside allowed palette");colors.Add(c.ToArgb());t=Math.Min(t,y);bt=Math.Max(bt,y);}}
 if(bt-t+1!=heights[i]||colors.Count>20)throw new Exception("Height/color failure");if(i==1){if(t!=10||bt!=53)throw new Exception("Imp not centered");}else if(bt!=sizes[i]-1)throw new Exception("Ground contact failure");if(z.Width!=b.Width*6||z.Height!=b.Height*6)throw new Exception("6x size failure");for(int y=0;y<z.Height;y++)for(int x=0;x<z.Width;x++)if(z.GetPixel(x,y).ToArgb()!=b.GetPixel(x/6,y/6).ToArgb())throw new Exception("6x not nearest neighbor");log.AppendLine(names[i]+": PASS cell="+sizes[i]+", height="+heights[i]+", colors="+colors.Count+", alpha=0/255, 6x exact; occupied rows="+t+".."+bt);
 }
 foreach(string n in new[]{"Lineup_3x.png","Screen_3x.png"})using(Bitmap b=new Bitmap(Path.Combine(root,n))){int ew=n.StartsWith("Screen")?1920:2304,eh=n.StartsWith("Screen")?1080:720;if(b.Width!=ew||b.Height!=eh)throw new Exception("Composite dimensions");for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++)if(b.GetPixel(x,y).ToArgb()!=b.GetPixel(x/3*3,y/3*3).ToArgb())throw new Exception("Composite not exact 3x");log.AppendLine(n+": PASS "+ew+"x"+eh+", exact 3x blocks");}
 log.AppendLine("Original imagegen sources: grid compliance NOT certified. Review README limitations.");File.WriteAllText(Path.Combine(root,"Sources","verification.txt"),log.ToString(),new UTF8Encoding(false));Console.Write(log.ToString());
 }
}
'@
Add-Type -TypeDefinition $code -ReferencedAssemblies System.Drawing
[VerifyEnemyConcepts]::Run($root)
