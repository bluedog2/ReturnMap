"""Dependency-free imagegen normalization. Writes only beside this script."""
from pathlib import Path
import struct,zlib,json,collections,hashlib
ROOT=Path(__file__).resolve().parent
META=json.loads((ROOT/'sources.json').read_text())
MASTER='692820 654138 2B231B 4D3720 554639 B0701C A87B56 CEA362 CAA981 C5AF92 DEC28D 716432 C8A332 E3DBC3 293145 384869 53617E 19142E 27213F 2F2F50 302460 8284A1 040205 47345B 4B3478 2C182D 5F3861 6F2F73 7E2D94 180711 46353A 47141D 8C1D24 902F36 AF1F28 1A151A 2A2B32 6C665E 928982 A0AAA7 F2C14E FFF4D6 3E6B3A 6E9A4A 3FC1C9 8FB8E8'.split()
def color(s):return tuple(bytes.fromhex(s))+(255,)
MP=set(map(color,MASTER)); CLEAR=color('1A151A')[:3]+(0,)
PLAY=list(map(color,'293145 384869 53617E 8284A1 2A2B32 27213F 47345B'.split()))
BUILD=list(map(color,'654138 A87B56 CEA362 FFF4D6 554639 4D3720 B0701C'.split()))
HP=list(map(color,'293145 384869 53617E 8FB8E8 47141D 8C1D24 AF1F28 2B231B 654138 A87B56 DEC28D FFF4D6 B0701C F2C14E 1A151A 2A2B32'.split()))
def readpng(path):
 data=Path(path).read_bytes();p=8;ids=b'';pal=[];tr=b''
 while p<len(data):
  n=struct.unpack('>I',data[p:p+4])[0];typ=data[p+4:p+8];v=data[p+8:p+8+n];p+=n+12
  if typ==b'IHDR':w,h,depth,ct,_,_,inter=struct.unpack('>IIBBBBB',v)
  elif typ==b'IDAT':ids+=v
  elif typ==b'PLTE':pal=[tuple(v[i:i+3]) for i in range(0,len(v),3)]
  elif typ==b'tRNS':tr=v
 assert depth==8 and inter==0,(depth,inter)
 bpp={6:4,2:3,0:1,4:2,3:1}[ct];raw=zlib.decompress(ids);stride=w*bpp;out=[];prev=bytearray(stride);p=0
 for y in range(h):
  f=raw[p];row=bytearray(raw[p+1:p+1+stride]);p+=stride+1
  for x in range(stride):
   a=row[x-bpp] if x>=bpp else 0;b=prev[x];c=prev[x-bpp] if x>=bpp else 0
   if f==1:v=a
   elif f==2:v=b
   elif f==3:v=(a+b)//2
   elif f==4:
    q=a+b-c;ds=[abs(q-a),abs(q-b),abs(q-c)];v=[a,b,c][ds.index(min(ds))]
   else:v=0
   row[x]=(row[x]+v)&255
  line=[]
  for x in range(w):
   v=tuple(row[x*bpp:(x+1)*bpp])
   line.append(v if ct==6 else v+(255,) if ct==2 else pal[v[0]]+(tr[v[0]] if v[0]<len(tr) else 255,) if ct==3 else (v[0],)*3+(v[1] if ct==4 else 255,))
  out.append(line);prev=row
 return out
def savepng(path,a):
 path=ROOT/path;path.parent.mkdir(parents=True,exist_ok=True);h=len(a);w=len(a[0])
 def chunk(t,d):return struct.pack('>I',len(d))+t+d+struct.pack('>I',zlib.crc32(t+d)&0xffffffff)
 raw=b''.join(b'\0'+bytes(c for px in row for c in px) for row in a)
 path.write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',w,h,8,6,0,0,0))+chunk(b'IDAT',zlib.compress(raw,9))+chunk(b'IEND',b''))
def up(a,k):return [[p for p in row for _ in range(k)] for row in a for _ in range(k)]
def quant(p,pal):return min(pal,key=lambda q:sum((p[i]-q[i])**2 for i in range(3))) if p[3]>=128 else CLEAR
def down(a,w,h,pal):
 sh=len(a);sw=len(a[0]);out=[];cache={}
 for y in range(h):
  row=[]
  for x in range(w):
   counts=collections.Counter()
   for sy in range(y*sh//h,max(y*sh//h+1,(y+1)*sh//h)):
    for sx in range(x*sw//w,max(x*sw//w+1,(x+1)*sw//w)):
     p=a[sy][sx]
     if p not in cache:cache[p]=quant(p,pal)
     counts[cache[p]]+=1
   row.append(counts.most_common(1)[0][0])
  out.append(row)
 return out
def bbox(a,pred=lambda p:p[3]>0):
 ps=[(x,y) for y,row in enumerate(a) for x,p in enumerate(row) if pred(p)]
 return [min(x for x,y in ps),min(y for x,y in ps),max(x for x,y in ps),max(y for x,y in ps)]
def crop(a,b):x,y,r,d=b;return [row[x:r+1] for row in a[y:d+1]]
report={};outputs={};records={}
def deliver(name,a,kind,key,steps):
 folder='Player' if kind=='hero' else 'Tiles';p=f'{folder}/{name}.png';one=f'_1x/{name}.png'
 savepng(Path(one),a);savepng(Path(p),up(a,8));outputs[name]=a
 rgb=set(p[:3] for row in a for p in row);allowed={p[:3] for p in MP}
 r={'art_size':[len(a[0]),len(a)],'saved_size':[len(a[0])*8,len(a)*8],'palette_outside_colors':len(rgb-allowed),'opaque_colors':len({p for row in a for p in row if p[3]}),'alpha_values':sorted({p[3] for row in a for p in row})}
 if kind!='hero':r.update(edge_mismatch_lr=sum(row[0]!=row[-1] for row in a),edge_mismatch_tb=sum(x!=y for x,y in zip(a[0],a[-1])))
 report[name]=r;records[name]={'file':p,'one_x':one,**META[key],'postprocess':steps}
 assert r['palette_outside_colors']==0 and r['opaque_colors']<=16
 assert r['alpha_values']==([0,255] if kind=='hero' else [255])
 if kind!='hero':assert r['edge_mismatch_lr']==r['edge_mismatch_tb']==0
for kind in ['Floor','Wall','Ceiling']:
 key=kind.lower();src=readpng(META[key]['source']);a=down(src,32,32,PLAY)
 # Move mortar away from the outside frame. Retain generated clusters.
 a=[[a[(y+5)%32][(x+3)%32] for x in range(32)] for y in range(32)]
 # Close periodic edges inside stone faces; never add a dark perimeter.
 for y in range(32):a[y][0]=a[y][31]=a[y][1]
 for x in range(32):
  a[31][x]=a[0][x] if kind=='Wall' else PLAY[2]
  if kind!='Wall':a[0][x]=PLAY[2]
 if kind=='Floor':
  for x in range(32):a[1][x]=PLAY[3];a[2][x]=PLAY[2] if x%13 in (8,9) else PLAY[3]
 elif kind=='Ceiling':
  for x in range(32):a[28][x]=PLAY[2];a[29][x]=PLAY[1];a[30][x]=PLAY[0]
 for y in range(32):a[y][-1]=a[y][0]
 steps=['Quantized area-mode downsample to 32x32 using nearest RGB palette; no dithering.','Periodic phase offset (+3,+5); opposing edges matched; no dark outer frame.','Floor rows 1-2 highlighted / ceiling rows 28-30 underside shaded as applicable.','Opaque alpha 255; nearest-neighbor 8x export.']
 deliver('Tile_Play_'+kind,a,'tile',key,steps)
 # Process generated warm variant, then repair its structural drift with exact color-index transfer.
 warm=down(readpng(META['build'+kind]['source']),32,32,BUILD)
 b=[[BUILD[PLAY.index(p)] for p in row] for row in a]
 deliver('Tile_Build_'+kind,b,'tile','build'+kind,['Generated warm variant downsampled/nearest-palette quantized.','Exact Play color-index transfer supersedes generated geometry to enforce identical silhouette, mortar and crack pixels.', 'Mapping: '+str(dict(zip([p[:3] for p in PLAY],[p[:3] for p in BUILD]))),'Opaque alpha 255; nearest-neighbor 8x export.'])
 report['Tile_Build_'+kind]['phase_structure_mismatch']=sum(BUILD.index(b[y][x])!=PLAY.index(a[y][x]) for y in range(32) for x in range(32))
src=readpng(META['hero']['source']);cw=len(src[0])//4
first=[row[:cw] for row in src];bounds=bbox(first,lambda p:p[3]>=128);c=crop(first,bounds)
w=min(30,round(len(c[0])*48/len(c)));base=down(c,w,48,HP)
# Force a crisp one-pixel inner silhouette outline with the material's darkest tone.
def outline(a):
 old=[r[:] for r in a]
 for y,row in enumerate(old):
  for x,p in enumerate(row):
   if not p[3]:continue
   if any(xx<0 or yy<0 or yy>=len(a) or xx>=len(row) or old[yy][xx][3]==0 for xx,yy in [(x-1,y),(x+1,y),(x,y-1),(x,y+1)]):
    if p in HP[4:7]:q=HP[4]
    elif p in HP[7:14]:q=HP[7]
    elif p in HP[:4]:q=HP[0]
    else:q=HP[14]
    a[y][x]=q
 return a
base=outline(base)
# Torso is blue armor in local rows 18..31 (cape excluded), measured independently of head/boots.
pts=[x for y in range(18,32) for x,p in enumerate(base[y]) if p in HP[:4]]
center=(min(pts)+max(pts))/2;ox=round(31.5-center)
frames=[]
for i in range(4):
 a=[[CLEAR]*64 for _ in range(64)]
 for y,row in enumerate(base):
  for x,p in enumerate(row):
   if p[3]:a[y+16][x+ox]=p
 # A one-pixel lifted shoulder / cape on middle phases, head and feet fixed.
 if i in (1,2):
  for y in range(33,43):
   for x in range(1,33):
    if a[y][x] in HP[:7]:a[y][x]=a[y+1][x]
 if i in (2,3):
  old=[r[:] for r in a]
  for y in range(43,59):
   for x in range(1,63):
    if old[y][x] in HP[4:7]:a[y][x]=old[y+1][x] if old[y+1][x] in HP[4:7] else old[y][x]
 a=outline(a);frames.append(a)
sheet=[sum([f[y] for f in frames],[]) for y in range(64)]
deliver('Hero_Idle',sheet,'hero','hero',['Alpha >=128 bounds of generated first frame; quantized area-mode downsample to width '+str(w)+' x48.','Canonical first pose reused to eliminate frame identity drift; middle phases lift shoulder 1px; cape fold changes 1px.','Material-specific 1px inner outline. Torso armor centered at cell x31.5; feet aligned PNG y63 = bottom-origin row0.','Hidden RGB uses master color #1A151A; binary alpha; 4x64 cells; nearest-neighbor 8x export.'])
fr=[]
for i,a in enumerate(frames):
 b=bbox(a);pts=[(x,y) for y in range(34,48) for x,p in enumerate(a[y]) if p in HP[:4]]
 tb=[min(x for x,y in pts),min(y for x,y in pts),max(x for x,y in pts),max(y for x,y in pts)]
 r={'frame':i,'opaque_bbox_xyxy':b,'bottom_png_y':b[3],'bottom_origin_row':63-b[3],'height':b[3]-b[1]+1,'width':b[2]-b[0]+1,'torso_bbox_xyxy':tb,'torso_center_x':(tb[0]+tb[2])/2,'top_left_right_edge_opaque':sum(p[3]>0 for p in a[0])+sum(row[0][3]>0 or row[-1][3]>0 for row in a)}
 assert r['height']==48 and r['width']<=32 and r['bottom_origin_row']==0 and abs(r['torso_center_x']-31.5)<=1 and r['top_left_right_edge_opaque']==0,r
 fr.append(r)
report['Hero_Idle']['frames']=fr
# Verify disk roundtrip and all 8x8 blocks, not just the working arrays.
for name,a in outputs.items():
 p=records[name]['file'];disk=readpng(ROOT/p);reread=readpng(ROOT/records[name]['one_x'])
 assert reread==a and disk==up(a,8)
 report[name]['eight_x_block_errors']=0
 report[name]['sha256']=hashlib.sha256((ROOT/p).read_bytes()).hexdigest()
# Palette-only visual contact sheet: six 3x3 tile swatches and four hero cells.
preview=[[color('1A151A')]*304 for _ in range(272)]
for row,phase in enumerate(['Play','Build']):
 for col,kind in enumerate(['Floor','Wall','Ceiling']):
  a=outputs['Tile_'+phase+'_'+kind]
  for y in range(96):
   for x in range(96):preview[4+row*100+y][4+col*100+x]=a[y%32][x%32]
for i,f in enumerate(frames):
 for y in range(64):
  for x in range(64):
   if f[y][x][3]:preview[208+y][24+64*i+x]=f[y][x]
savepng(Path('Preview_4x.png'),up(preview,4))
(ROOT/'validation.json').write_text(json.dumps(report,indent=2),encoding='utf8')
(ROOT/'generation.json').write_text(json.dumps({'tool':'built-in image_gen','spec':'docs/px32-conversion-plan.md section 2','master_palette':['#'+s for s in MASTER],'coordinates':'PNG y down; bottom-origin row0 = PNG y63; cell center x31.5','torso_measurement':'blue armor pixels in cell y34..47; red cape, head and boots excluded','files':records,'validation':report,'limitations':['Pixel metrics and contact sheet checked; no Unity import or in-game animation playtest.','Floor/ceiling directional bands repeat when tiled vertically; edge equality is a necessary technical check, not proof of invisible perceptual repetition.','Build originals structurally normalized with exact Play index mapping.','Idle motion reconstructed from canonical generated first frame to remove model jitter.']},indent=2),encoding='utf8')
print(json.dumps(report,indent=2))
