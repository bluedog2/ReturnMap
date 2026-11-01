import struct,zlib,json,math,re,collections
from pathlib import Path
ROOT=Path(__file__).resolve().parent

def readpng(path):
 b=Path(path).read_bytes(); pos=8; dat=b''; pal=None; trans=b''
 while pos<len(b):
  n=struct.unpack('>I',b[pos:pos+4])[0]; typ=b[pos+4:pos+8]; d=b[pos+8:pos+8+n]; pos+=12+n
  if typ==b'IHDR': w,h,depth,ct,_,_,inter=struct.unpack('>IIBBBBB',d)
  elif typ==b'IDAT': dat+=d
  elif typ==b'PLTE': pal=[tuple(d[i:i+3]) for i in range(0,len(d),3)]
  elif typ==b'tRNS': trans=d
 assert depth==8 and inter==0,(path,depth,inter)
 channels={2:3,6:4,3:1,0:1,4:2}[ct]; stride=w*channels; raw=zlib.decompress(dat); prev=bytearray(stride); rows=[]
 for y in range(h):
  f=raw[y*(stride+1)]; r=bytearray(raw[y*(stride+1)+1:(y+1)*(stride+1)])
  for i in range(stride):
   a=r[i-channels] if i>=channels else 0; c=prev[i-channels] if i>=channels else 0; v=prev[i]
   if f==1: r[i]=(r[i]+a)&255
   elif f==2: r[i]=(r[i]+v)&255
   elif f==3: r[i]=(r[i]+((a+v)//2))&255
   elif f==4:
    p=a+v-c; pa=abs(p-a); pb=abs(p-v); pc=abs(p-c); r[i]=(r[i]+(a if pa<=pb and pa<=pc else v if pb<=pc else c))&255
  row=[]
  for x in range(w):
   t=tuple(r[x*channels:(x+1)*channels])
   if ct==6: row.append(t)
   elif ct==2: row.append(t+(255,))
   elif ct==3: row.append(pal[t[0]]+(trans[t[0]] if t[0]<len(trans) else 255,))
   elif ct==0: row.append((t[0],)*3+(255,))
   else: row.append((t[0],)*3+(t[1],))
  rows.append(row); prev=r
 return rows

def writepng(path,rows):
 h=len(rows);w=len(rows[0])
 def chunk(t,d): return struct.pack('>I',len(d))+t+d+struct.pack('>I',zlib.crc32(t+d)&0xffffffff)
 data=b''.join(b'\0'+bytes(v for p in r for v in p) for r in rows)
 Path(path).write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',w,h,8,6,0,0,0))+chunk(b'IDAT',zlib.compress(data,9))+chunk(b'IEND',b''))

def nearest(rows,k): return [[p for p in row for _ in range(k)] for row in rows for _ in range(k)]
def bbox(rows):
 pts=[(x,y) for y,r in enumerate(rows) for x,p in enumerate(r) if p[3]]
 if not pts:return None
 xs,ys=zip(*pts);return [min(xs),min(ys),max(xs),max(ys)]
PAL=[tuple(bytes.fromhex(s)) for s in re.findall(r'^#([0-9A-Fa-f]{6})\s*$',(ROOT.parent/'palette_v2.txt').read_text(encoding='utf-8'),re.M)][:20]
ZERO=(0,0,0,0)

def recover(path,cols,rows,count,k=4,canvas=None):
 src=readpng(path); h=len(src);w=len(src[0])
 # Detect occupied row/column bands instead of assuming the generator respected cell boundaries.
 frames=[]; grid=[]
 cache={}
 def clean(p):
  r,g,b,a=p
  if a<128 or (r>150 and b>150 and g<100 and r-g>90 and b-g>90):return ZERO
  if p not in cache:cache[p]=min(PAL,key=lambda c:sum((c[i]-p[i])**2 for i in range(3)))+(255,)
  return cache[p]
 def foreground(p):
  r,g,b,a=p
  return a>=128 and not (r>150 and b>150 and g<100 and r-g>90 and b-g>90)
 def bands(flags,gap=2):
  idx=[i for i,v in enumerate(flags) if v]
  if not idx:return []
  out=[];start=prev=idx[0]
  for i in idx[1:]:
   if i-prev>gap+1:out.append((start,prev));start=i
   prev=i
  out.append((start,prev));return out
 # Sample only fixed integer-k block centers. Never fit or resize a silhouette.
 sw=w//k;sh=h//k
 mask=[[foreground(src[y*k+k//2][x*k+k//2]) for x in range(sw)] for y in range(sh)]
 ybands=bands([any(r) for r in mask],2)
 boxes=[]
 for y0,y1 in ybands:
  xbands=bands([any(mask[y][x] for y in range(y0,y1+1)) for x in range(sw)],2)
  for x0,x1 in xbands:
   pts=[(x,y) for y in range(y0,y1+1) for x in range(x0,x1+1) if mask[y][x]]
   if len(pts)<10:continue
   xx,yy=zip(*pts);boxes.append((min(xx),min(yy),max(xx),max(yy)))
 if len(boxes)!=count:return None,{'error':'detected frame count mismatch','size':[w,h],'detected_boxes':boxes}
 if any(x1-x0+1>126 or y1-y0+1>127 for x0,y0,x1,y1 in boxes):return None,{'error':'native sampled silhouette exceeds cell; resizing forbidden','size':[w,h],'detected_boxes':boxes}
 for f,(x0,y0,x1,y1) in enumerate(boxes):
  ox=x0*k;oy=y0*k
  fw=x1-x0+1;fh=y1-y0+1
  frame=[[clean(src[oy+y*k+k//2][ox+x*k+k//2]) if x<fw and y<fh else ZERO for x in range(128)] for y in range(128)]
  # Remove isolated opaque singletons only. Retain multi-pixel dash trails.
  old=[r[:] for r in frame]
  for y in range(128):
   for x in range(128):
    if old[y][x][3] and not any(old[yy][xx][3] for yy in range(max(0,y-1),min(128,y+2)) for xx in range(max(0,x-1),min(128,x+2)) if (xx,yy)!=(x,y)):frame[y][x]=ZERO
  bb=bbox(frame)
  if not bb:return None,{'error':'empty frame'}
  dx=round(63.5-(bb[0]+bb[2])/2);dy=127-bb[3]
  out=[[ZERO]*128 for _ in range(128)];clipped=0
  for y in range(128):
   for x in range(128):
    if frame[y][x][3]:
     xx=x+dx; yy=y+dy
     if 0<=xx<128 and 0<=yy<128:out[yy][xx]=frame[y][x]
     else:clipped+=1
  frames.append(out)
  # Quantized foreground 4x4 block agreement: detects nonuniform generated grid.
  match=total=0
  for y in range(0,128,2):
   for x in range(0,128,2):
    center=frame[y][x]
    if not center[3]:continue
    for sy,sx in [(0,0),(0,k-1),(k-1,0),(k-1,k-1)]:
     match+=clean(src[oy+y*k+sy][ox+x*k+sx])==center;total+=1
  grid.append({'block_corner_agreement':round(match/max(1,total),4),'translation':[dx,dy],'clipped_pixels':clipped,'raw_bbox':bb,'detected_source_box':boxes[f],'sample_pitch':k})
 return frames,{'size':[w,h],'grid':grid}

def metrics(frames,name):
 result=[]
 for i,f in enumerate(frames):
  b=bbox(f); colors=set(p[:3] for r in f for p in r if p[3]);edge=sum(p[3]>0 for p in f[0])+sum(f[y][x][3]>0 for y in range(127) for x in [0,127])
  result.append({'frame':i,'bbox':b,'bottom_row':b[3],'center_x':(b[0]+b[2])/2,'height':b[3]-b[1]+1,'color_count':len(colors),'forbidden_edge_pixels':edge})
 avgx=sum(f['center_x'] for f in result)/len(result);avgh=sum(f['height'] for f in result)/len(result)
 for f in result:f['center_deviation']=round(f['center_x']-avgx,3);f['height_deviation']=round(f['height']-avgh,3)
 colors=set(p[:3] for f in frames for r in f for p in r if p[3]);bad=sum(c not in PAL for c in colors)
 passed=all(f['bottom_row']==127 and f['forbidden_edge_pixels']==0 and (name=='Death' or abs(f['height_deviation'])<=2 and abs(f['center_deviation'])<=2) for f in result) and (name!='Idle' or all(abs(f['height']-96)<=1 for f in result))
 return {'frames':result,'color_count':len(colors),'palette_outside':bad,'alpha_values':[0,255],'numeric_pass':passed}

def gif(path,frames,k=3):
 # Literal LZW with regular clear codes keeps the 6-bit width constant.
 colors=[(0,0,0)]+PAL; colors+= [(0,0,0)]*(32-len(colors)); lut={c:i+1 for i,c in enumerate(PAL)}
 w=h=128*k;data=b'GIF89a'+struct.pack('<HHBBB',w,h,0xF4,0,0)+bytes(v for c in colors for v in c)+b'!\xff\x0bNETSCAPE2.0\x03\x01\x00\x00\x00'
 for frame in frames:
  pixels=[lut[p[:3]] if p[3] else 0 for row in nearest(frame,k) for p in row]
  codes=[]
  for start in range(0,len(pixels),20):codes.append(32);codes.extend(pixels[start:start+20])
  codes.append(33);packed=bytearray();acc=bits=0
  for c in codes:
   acc|=c<<bits;bits+=6
   while bits>=8:packed.append(acc&255);acc>>=8;bits-=8
  if bits:packed.append(acc)
  data+=b'!\xf9\x04\x09'+struct.pack('<H',12)+b'\x00\x00'+b','+struct.pack('<HHHHB',0,0,w,h,0)+b'\x05'
  data+=b''.join(bytes([len(packed[i:i+255])])+packed[i:i+255] for i in range(0,len(packed),255))+b'\x00'
 Path(path).write_bytes(data+b';')

if __name__=='__main__':
 import sys
 manifest=json.loads((ROOT/'attempts.json').read_text(encoding='utf-8-sig'))
 for sub in ['_1x','Player','Animations']: (ROOT/sub).mkdir(exist_ok=True)
 validation={};compare=[[ZERO]*256 for _ in range(128*8)]
 for ni,(name,spec) in enumerate(manifest['sheets'].items()):
  candidates=[]
  for attempt in spec['attempts']:
   if not attempt.get('source'):continue
   frames,meta=recover(ROOT/attempt['source'],attempt.get('cols',spec['cols']),attempt.get('rows',spec['rows']),spec['count'],attempt.get('k',4),attempt.get('canvas'))
   attempt['recovery']=meta
   if frames:
    m=metrics(frames,name);m['numeric_pass']=m['numeric_pass'] and all(g['clipped_pixels']==0 for g in meta['grid']);attempt['metrics']=m
    score=(1000 if m['numeric_pass'] else 0)+sum(g['block_corner_agreement'] for g in meta['grid'])/len(frames)*100
    if name=='Idle':score-=sum(abs(f['height']-96) for f in m['frames'])*2
    candidates.append((score,attempt,frames,m))
  if not candidates:validation[name]={'error':'no recoverable candidate'};continue
  _,selected,frames,m=max(candidates,key=lambda c:c[0]);spec['selected_attempt']=selected['attempt'];m['selected_attempt']=selected['attempt'];m['generation_grid']=selected['recovery'];m['production_ready']=False;m['grid_clean']=all(g['block_corner_agreement']>=0.98 for g in selected['recovery']['grid']);m['visual_status']='Rejected for production: irregular source block grid and inter-frame/cross-sheet face, hairpin, box and scale variation.';validation[name]=m
  sheet=[sum((f[y] for f in frames),[]) for y in range(128)]
  writepng(ROOT/'_1x'/('Hero_'+name+'.png'),sheet);writepng(ROOT/'Player'/('Hero_'+name+'.png'),nearest(sheet,4));gif(ROOT/'Animations'/('Hero_'+name+'_3x.gif'),frames)
  old=readpng(ROOT.parent/'Px32HeroV2'/'_1x'/('Hero_'+name+'.png')); old=[r[:96] for r in old];bb=bbox(old);dx=round(63.5-(bb[0]+bb[2])/2);dy=127-bb[3]
  for y,row in enumerate(old):
   for x,p in enumerate(row):
    if p[3]:compare[ni*128+y+dy][x+dx]=p
  for y,row in enumerate(frames[0]):compare[ni*128+y][128:]=row
 writepng(ROOT/'Compare_3x.png',nearest(compare,3))
 (ROOT/'validation.json').write_text(json.dumps({'overall_pass':False,'rules_note':'Height cannot be corrected by translation. No silhouette resizing used. Integer-k source block-center sampling (Run attempts2-4 k3, otherwise k4); detected row/column bands; palette mapping; binary alpha; singleton removal; rigid translation. Generated candidates are NOT approved production assets.','sheets':validation},indent=2),encoding='utf-8')
 (ROOT/'attempts.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
 print(json.dumps({n:{'heights':[f['height'] for f in m.get('frames',[])],'colors':m.get('color_count'),'pass':m.get('numeric_pass'),'selected':m.get('selected_attempt')} for n,m in validation.items()},indent=2))
