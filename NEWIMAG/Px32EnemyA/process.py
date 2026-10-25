"""Reproducible source-pixel animation. Writes only beneath this directory."""
from pixel_io import *
import math, shutil

SOURCE=Path('C:/Users/Owner/.codex/generated_images/01a16f6d-535d-7cb3-aae6-5a8e635ce2cc')
CONFIG=[('Goblin_Raider','goblin',48,32,'exec-568efcc1-0ba4-4d99-a750-2f3cf3d900d0.png','1A151A 2B231B 4D3720 654138 A87B56 CEA362 B0701C C8A332 DEC28D 3E6B3A 6E9A4A 293145 53617E A0AAA7 F2C14E FFF4D6'),('Imp_Flyer','imp',48,32,'exec-25dbc849-cee4-43a9-b95d-52b4f41c6d51.png','180711 47141D 8C1D24 902F36 AF1F28 692820 CEA362 DEC28D FFF4D6 F2C14E 19142E 27213F 47345B 4B3478 6F2F73 7E2D94'),('Skeleton_Archer','skeleton',64,48,'exec-68b124b6-b67a-4c92-893e-65e42532d77c.png','19142E 27213F 47345B 4B3478 6F2F73 8284A1 2B231B 4D3720 654138 A87B56 CEA362 C5AF92 DEC28D E3DBC3 FFF4D6 1A151A')]
def blank(w,h):return [[CLEAR]*w for _ in range(h)]
def paste(dst,src,dx,dy):
 for y,row in enumerate(src):
  for x,p in enumerate(row):
   if p[3] and 0<=y+dy<len(dst) and 0<=x+dx<len(dst[0]):dst[y+dy][x+dx]=p
def outline(a,dark):
 old=[r[:] for r in a];h=len(a);w=len(a[0])
 for y,row in enumerate(old):
  for x,p in enumerate(row):
   if p[3] and any(xx<0 or yy<0 or xx>=w or yy>=h or not old[yy][xx][3] for xx,yy in ((x-1,y),(x+1,y),(x,y-1),(x,y+1))):a[y][x]=dark
 return a
def fit(a,n,height,bottom,dark):
 cut=crop(a,bbox(a));h=len(cut);w=len(cut[0]);width=min(n-4,w)
 resized=[[cut[min(h-1,int(y*h/height))][min(w-1,int(x*w/width))] for x in range(width)] for y in range(height)]
 out=blank(n,n);paste(out,resized,(n-width)//2,bottom-height+1)
 return outline(out,dark)
def rotate(a,deg):
 cut=crop(a,bbox(a));h=len(cut);w=len(cut[0]);out=blank(128,128);t=math.radians(deg);c=math.cos(t);s=math.sin(t)
 for y in range(128):
  for x in range(128):
   sx=round(c*(x-64)+s*(y-64)+(w-1)/2);sy=round(-s*(x-64)+c*(y-64)+(h-1)/2)
   if 0<=sx<w and 0<=sy<h:out[y][x]=cut[sy][sx]
 return out
def deform(base,kind,mode,i):
 h=len(base);w=len(base[0]);out=blank(128,128)
 # All poses reuse the established 1x pixels. Head remains rigid.
 for y,row in enumerate(base):
  for x,p in enumerate(row):
   if not p[3]:continue
   dx=dy=0
   if mode=='Idle':
    if y<h*.62:dy=(0,-1,0,1)[i]
    if x>w*.76 and y>h*.45:dx=(0,1,0,-1)[i]
    if x<w*.33 and y>h*.4:dx=(0,0,-1,1)[i]
   elif mode=='Run':
    phase=i*math.pi/3
    if kind=='imp':
     if x<w*.58 and y<h*.62:
      dy=round(math.sin(phase)*7*(1-x/(w*.65)));dx=round(math.cos(phase)*2)
     else:dy=round(math.sin(phase)*1)
     if y>h*.7:dx=round(math.sin(phase+1)*2)
    else:
     if y>h*.70:
      side=-1 if x<w*.50 else 1
      dx=round(side*math.sin(phase)*5*(y/h-.65)/.35)
      dy=-round(max(0,side*math.cos(phase))*3*(y/h-.65)/.35)
     else:
      dx=round((h*.7-y)*.07);dy=(0,-1,-1,0,1,1)[i]
     if x<w*.28 and y>h*.35:dx-=2+(i%3)
   elif mode=='Hit':
    dx=-round((h-y)*(.23 if i==0 else .09))
    if x>w*.67 and y>h*.4:dy=-3 if i==0 else -1
   out[y+dy+32][x+dx+32]=p
 return out

records={};sources=[];allframes={}
for name,kind,n,height,filename,palette in CONFIG:
 pal=list(map(color,palette.split()));dark=pal[0]
 (ROOT/'Sources').mkdir(exist_ok=True);shutil.copyfile(SOURCE/filename,ROOT/'Sources'/f'{name}_imagegen.png')
 raw=readpng(ROOT/'Sources'/f'{name}_imagegen.png')
 raw=[[p if p[3]>=128 else CLEAR for p in row] for row in raw];cut=crop(raw,bbox(raw))
 width=round(len(cut[0])*height/len(cut));base=down(cut,width,height,pal)
 base=outline(base,dark)
 savepng(Path('_bases')/(name+'_Idle_0.png'),base)
 bottom=n-1 if kind!='imp' else 39
 idle0=fit(base,n,height,bottom,dark)
 for mode,count in [('Idle',4),('Run',6),('Hit',2),('Death',6)]:
  frames=[]
  for i in range(count):
   if mode=='Death':
    a=deform(base,kind,'Hit',0)
    a=rotate(a,[0,-12,-30,-55,-78,-90][i])
    hh=round(height*[1,.94,.77,.58,.39,.30][i])
    f=fit(a,n,hh,bottom,dark)
   elif mode=='Idle' and i==0:f=idle0
   else:f=fit(deform(base,kind,mode,i),n,height,bottom+((0,-1,0,1)[i%4] if kind=='imp' else 0),dark)
   frames.append(f)
  key=name+'_'+mode;sheet=[sum((f[y] for f in frames),[]) for y in range(n)]
  savepng(Path('_1x')/(key+'.png'),sheet);savepng(Path('Enemies')/(key+'.png'),up(sheet,8))
  allframes[key]=frames
  records[key]={'one_x':'_1x/'+key+'.png','eight_x':'Enemies/'+key+'.png','frames':count,'cell':[n,n],'fps':6 if mode=='Idle' else 10,'flying':kind=='imp','outline':palette.split()[0]}
 sources.append({'character':name,'design_reference':'NEWIMAG/AnimationV2/'+kind+'/Idle_00.png','generated_source':'Sources/'+name+'_imagegen.png','base':'_bases/'+name+'_Idle_0.png','palette':palette.split(),'method':'Built-in image_gen newly redrew reference; generated art sampled to target grid, palette quantized and outlined. All later frames edit that 1x base only.'})

hero_path=ROOT.parent/'Px32Hero/_1x/Hero_Idle.png'
if not hero_path.exists():hero_path=ROOT.parent/'Px32Pilot2/_1x/Hero_Idle.png'
hero=[r[:64] for r in readpng(hero_path)]
# Each row: full-size hero frame 0, then a complete animation strip. Shared baseline y=63.
preview=blank(64+6*64,12*72)
for row,(key,frames) in enumerate(allframes.items()):
 paste(preview,hero,0,row*72)
 n=len(frames[0])
 for i,f in enumerate(frames):paste(preview,f,64+i*n,row*72+64-n)
savepng(Path('Preview_3x.png'),up(preview,3))
manifest={'tool':'built-in image_gen','palette':['#'+x for x in MASTER],'sources':sources,'files':records,'preview_row_order':list(records),'preview_hero':str(hero_path),'pipeline':'New pixel-art redraw from high-resolution design reference, grid sampling and exact palette normalization; 1x pixel cutout/row articulation; padded rotation for death; exact nearest neighbor 8x. No original high-resolution animation frame was scaled into an output sheet.','prompt_summary':['Goblin: green skin, pointed ears, ochre cape, leather clothes, curved dagger, right facing 32px.','Imp: red body, tan horns, purple bat wings, yellow eye, curled tail, right facing 32px including wings.','Skeleton: purple hood/cape, ivory bones, quiver, wooden bow, right facing 48px.','All: transparent, chunky native pixel clusters, dark 1px outline, supplied 16-color subset, hero style reference.'],'limitations':['Generated artwork needs grid normalization; image_gen does not directly emit exact native pixel dimensions.','Pixel cutout animation reuses visible anatomy and lighting; no newly revealed reverse-side anatomy.','Grounded run poses retain the mandatory baseline and height; no true airborne run frame.','Imp death settles into a horizontal floating fallen pose rather than disappearing completely.','No Unity import or gameplay test.']}
(ROOT/'generation.json').write_text(json.dumps(manifest,indent=2),encoding='utf8')
print('Created 12 sheets / 54 frames and preview.')
