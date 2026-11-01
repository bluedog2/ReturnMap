import struct,zlib,pathlib,json,sys,collections,math,shutil
ROOT=pathlib.Path(__file__).resolve().parents[1]
REPO=ROOT.parents[1]
SPEC={'Goblin_Raider':(64,96),'Imp_Flyer':(64,96),'Skeleton_Archer':(96,128),'Elite_Knight':(96,128),'Orc_Brute':(128,160)}
ANIMS={'Idle':4,'Run':6,'Hit':2,'Death':6}
def read(p):
 b=pathlib.Path(p).read_bytes(); pos=8; data=b''; pal=[]; tr=b''
 while pos<len(b):
  n=struct.unpack('>I',b[pos:pos+4])[0]; t=b[pos+4:pos+8]; d=b[pos+8:pos+8+n];pos+=n+12
  if t==b'IHDR': w,h,depth,typ,_,_,inter=struct.unpack('>IIBBBBB',d)
  elif t==b'IDAT': data+=d
  elif t==b'PLTE': pal=[tuple(d[i:i+3]) for i in range(0,len(d),3)]
  elif t==b'tRNS': tr=d
 assert depth==8 and inter==0,(p,depth,inter)
 c={0:1,2:3,3:1,4:2,6:4}[typ]; raw=zlib.decompress(data); prev=bytearray(w*c); rows=[];pos=0
 for y in range(h):
  f=raw[pos];row=bytearray(raw[pos+1:pos+1+w*c]);pos+=1+w*c
  for x in range(w*c):
   a=row[x-c] if x>=c else 0;u=prev[x];ul=prev[x-c] if x>=c else 0
   if f==1:v=a
   elif f==2:v=u
   elif f==3:v=(a+u)//2
   elif f==4:
    q=a+u-ul; ds=[abs(q-a),abs(q-u),abs(q-ul)];v=(a,u,ul)[ds.index(min(ds))]
   else:v=0
   row[x]=(row[x]+v)&255
  rr=[]
  for x in range(w):
   p=tuple(row[x*c:(x+1)*c])
   if typ==6: rgba=p
   elif typ==2:rgba=p+(255,)
   elif typ==3:rgba=pal[p[0]]+(tr[p[0]] if p[0]<len(tr) else 255,)
   elif typ==0:rgba=(p[0],)*3+(255,)
   else:rgba=(p[0],)*3+(p[1],)
   rr.append(rgba)
  rows.append(rr);prev=row
 return rows
def write(p,im):
 def chunk(t,d):return struct.pack('>I',len(d))+t+d+struct.pack('>I',zlib.crc32(t+d)&0xffffffff)
 h=len(im);w=len(im[0]);raw=b''.join(b'\0'+bytes(v for p in r for v in p) for r in im)
 pathlib.Path(p).write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',w,h,8,6,0,0,0))+chunk(b'IDAT',zlib.compress(raw,6))+chunk(b'IEND',b''))
def scale(im,k):return [[p for p in r for _ in range(k)] for r in im for _ in range(k)]
def refs():
 d=ROOT/'Sources'/'References';d.mkdir(exist_ok=True)
 for s in SPEC:
  for a in ANIMS:write(d/f'{s}_{a}_v2_4x.png',scale(read(REPO/'NEWIMAG'/'Px32EnemyV2'/'_1x'/f'{s}_{a}.png'),4))
PALETTE=[tuple(bytes.fromhex(h)) for h in '19181F 2A282D 3D3940 555058 74666A 8D7B80 B3A1A0 D0BEB8 E7D8CE F7EEE4 334629 58783B 88AE43 B8D849 D8EC76 38284D 58377B 8050AD B581D4'.split()]
ZERO=(0,0,0,0)
def bbox(im,threshold=127):
 pts=[(x,y) for y,row in enumerate(im) for x,p in enumerate(row) if p[3]>threshold]
 if not pts:return None
 xs,ys=zip(*pts);return (min(xs),min(ys),max(xs)+1,max(ys)+1)
def blank(w,h):return [[ZERO]*w for _ in range(h)]
def paste(dst,src,x,y):
 for j,row in enumerate(src):
  for i,p in enumerate(row):
   if 0<=y+j<len(dst) and 0<=x+i<len(dst[0]):dst[y+j][x+i]=p
def gif(path,frames):
 # Fixed-width GIF LZW, clear before dictionary growth; lossless and dependency free.
 h=len(frames[0]);w=len(frames[0][0]);pal=[(0,0,0)]+PALETTE+[(0,0,0)]*(31-len(PALETTE));lut={c:i+1 for i,c in enumerate(PALETTE)}
 out=bytearray(b'GIF89a'+struct.pack('<HHBBB',w,h,0xF4,0,0)+bytes(v for c in pal for v in c))
 out+=b'!\xff\x0bNETSCAPE2.0\x03\x01\x00\x00\x00'
 for im in frames:
  out+=b'!\xf9\x04\x09'+struct.pack('<H',12)+b'\x00\x00'+b','+struct.pack('<HHHHB',0,0,w,h,0)
  indices=[lut[p[:3]] if p[3] else 0 for row in im for p in row];codes=[]
  for i in range(0,len(indices),24):codes+=[32]+indices[i:i+24]
  codes.append(33);bits=0;nb=0;encoded=bytearray()
  for c in codes:
   bits|=c<<nb;nb+=6
   while nb>=8:encoded.append(bits&255);bits>>=8;nb-=8
  if nb:encoded.append(bits&255)
  out.append(5)
  for i in range(0,len(encoded),255):part=encoded[i:i+255];out.append(len(part));out+=part
  out.append(0)
 out.append(59);pathlib.Path(path).write_bytes(out)
def process(sheet,attempt=1):
 s,a=sheet.rsplit('_',1);height,cell=SPEC[s];n=ANIMS[a];cols=2 if a in ('Idle','Hit') else 3;rows=1 if a=='Hit' else 2
 path=ROOT/'Sources'/f'{sheet}_attempt{attempt}.png';im=read(path);ih=len(im);iw=len(im[0]);parts=[];boxes=[]
 def gap(counts,expected,radius):
  lo=max(0,round(expected-radius));hi=min(len(counts),round(expected+radius));runs=[];start=None
  for i in range(lo,hi):
   if counts[i]<=1 and start is None:start=i
   if counts[i]>1 and start is not None:runs.append((start,i));start=None
  if start is not None:runs.append((start,hi))
  if not runs:return round(expected)
  a,b=min(runs,key=lambda ab:abs(sum(ab)/2-expected));return (a+b)//2
 yc=[sum(p[3]>191 for p in r) for r in im]
 ycuts=[0]+([gap(yc,ih/2,ih*.22)] if rows==2 else [])+[ih]
 xcuts=[]
 for row in range(rows):
  xc=[sum(im[y][x][3]>191 for y in range(ycuts[row],ycuts[row+1])) for x in range(iw)]
  xcuts.append([0]+[gap(xc,iw*k/cols,iw/cols*.2) for k in range(1,cols)]+[iw])
 for f in range(n):
  row=f//cols;col=f%cols;x0=xcuts[row][col];x1=xcuts[row][col+1];y0=ycuts[row];y1=ycuts[row+1]
  part=[r[x0:x1] for r in im[y0:y1]];box=bbox(part,191);assert box,(sheet,f);parts.append(part);boxes.append(box)
 frames=[];cache={};reports=[]
 def color(p):
  rgb=p[:3]
  if rgb not in cache:cache[rgb]=min(PALETTE,key=lambda c:sum((c[i]-rgb[i])**2 for i in range(3)))+(255,)
  return cache[rgb]
 for f,(part,b) in enumerate(zip(parts,boxes)):
  x0,y0,x1,y1=b;bw=x1-x0;bh=y1-y0
  # Recover the model's approximate block grid from each newly drawn pose.
  step=bh/height if a!='Death' else (boxes[0][3]-boxes[0][1])/height
  th=round(bh/step);tw=round(bw/step)
  if tw>cell-2:
   step=bw/(cell-2);th=round(bh/step);tw=cell-2
  sprite=blank(tw,th)
  for y in range(th):
   for x in range(tw):
    votes=[]
    for dy in (.25,.5,.75):
     for dx in (.25,.5,.75):
      sy=min(y1-1,y0+int((y+dy)*bh/th));sx=min(x1-1,x0+int((x+dx)*bw/tw));p=part[sy][sx]
      votes.append(color(p) if p[3]>=160 else ZERO)
    sprite[y][x]=collections.Counter(votes).most_common(1)[0][0]
  # Remove only truly isolated opaque singletons, never add drawn geometry.
  isolated=[]
  for y,row in enumerate(sprite):
   for x,p in enumerate(row):
    if p[3] and not any(sprite[yy][xx][3] for yy in range(max(0,y-1),min(th,y+2)) for xx in range(max(0,x-1),min(tw,x+2)) if (xx,yy)!=(x,y)):isolated.append((x,y))
  for x,y in isolated:sprite[y][x]=ZERO
  bb=bbox(sprite);assert bb
  sprite=[r[bb[0]:bb[2]] for r in sprite[bb[1]:bb[3]]];th=len(sprite);tw=len(sprite[0]);frame=blank(cell,cell)
  tx=(cell-tw)//2
  if s=='Imp_Flyer' and a!='Death':ty=(cell-th)//2
  elif s=='Imp_Flyer' and a=='Death':ty=round(((cell-th)//2)*(1-f/(n-1))+(cell-th)*(f/(n-1)))
  else:ty=cell-th
  paste(frame,sprite,tx,ty);frames.append(frame)
  reports.append({'frame':f+1,'source_bbox':b,'recovered_grid_step':step,'isolated_removed':len(isolated)})
 sheetim=[sum([frame[y] for frame in frames],[]) for y in range(cell)]
 write(ROOT/'_1x'/f'{sheet}.png',sheetim);write(ROOT/'Enemies'/f'{sheet}.png',scale(sheetim,4));gif(ROOT/'Animations'/f'{sheet}_3x.gif',[scale(fr,3) for fr in frames])
 (ROOT/'Sources'/f'{sheet}_recovery.json').write_text(json.dumps({'source':str(path.relative_to(ROOT)),'method':'New image_gen source only; bbox block-grid recovery by 3x3 palette-mode sampling, binary alpha, isolated-point cleanup, translation. No v2 pixels used in final art.','frames':reports},indent=2))
 print(sheet,[(bbox(f)[3]-bbox(f)[1],bbox(f)[2]-bbox(f)[0]) for f in frames])
def validate():
 import re
 allowed={tuple(bytes.fromhex(h)) for h in re.findall(r'^#([A-Fa-f0-9]{6})\s*$',(REPO/'NEWIMAG/palette_v2.txt').read_text(encoding='utf-8'),re.M)}
 results=[]
 for s,(target,cell) in SPEC.items():
  for a,n in ANIMS.items():
   name=f'{s}_{a}';path=ROOT/'_1x'/f'{name}.png'
   if not path.exists():results.append({'sheet':name,'passed':False,'missing':True});continue
   im=read(path);rgba=[p for r in im for p in r];colors={p[:3] for p in rgba if p[3]};outside=len(colors-allowed);alpha=sum(p[3] not in (0,255) for p in rgba);ms=[]
   for f in range(n):
    fr=[r[f*cell:(f+1)*cell] for r in im];b=bbox(fr);x0,y0,x1,y1=b
    edges=sum(p[3]>0 for p in fr[0])+sum(fr[y][0][3]>0 for y in range(1,cell-1))+sum(fr[y][-1][3]>0 for y in range(1,cell-1))
    ms.append({'frame':f+1,'bbox':[x0,y0,x1-1,y1-1],'height':y1-y0,'width':x1-x0,'bottom_row':y1-1,'horizontal_center':(x0+x1-1)/2,'vertical_center':(y0+y1-1)/2,'edge_pixels':edges})
   center=max(abs(m['horizontal_center']-ms[0]['horizontal_center']) for m in ms);hd=max(abs(m['height']-ms[0]['height']) for m in ms);edges=sum(m['edge_pixels'] for m in ms)
   ground=all(m['bottom_row']==cell-1 for m in ms) if s!='Imp_Flyer' else None
   hover=max(abs(m['vertical_center']-(cell-1)/2) for m in ms) if s=='Imp_Flyer' and a!='Death' else None
   impdeath=(ms[-1]['bottom_row']==cell-1 and ms[-1]['height']<ms[0]['height'] and ms[-1]['width']>ms[-1]['height']) if s=='Imp_Flyer' and a=='Death' else None
   exact=read(ROOT/'Enemies'/f'{name}.png')==scale(im,4)
   passed=(len(im)==cell and len(im[0])==cell*n and not alpha and not outside and len(colors)<=24 and not edges and exact and ground is not False and (hover is None or hover<=2) and impdeath is not False and (a=='Death' or (center<=2 and hd<=2)) and (a!='Idle' or abs(ms[0]['height']-target)<=2))
   results.append({'sheet':name,'cell':cell,'frames':n,'colors':len(colors),'palette_outside':outside,'nonbinary_alpha':alpha,'edge_pixels':edges,'max_center_drift':center,'max_height_drift':hd,'idle_target':target if a=='Idle' else None,'ground_aligned':ground,'max_hover_center_error':hover,'imp_death_floor_and_prone':impdeath,'scale4_exact':exact,'passed':passed,'measurements':ms})
 doc={'passed':all(r['passed'] for r in results),'sheet_count':len(results),'exception':'Imp Death uses falling trajectory and final floor/prone pose; hover-center rule excluded to honor requested animation. Death center/height drift excluded for all species.','visual_checks':'Design consistency, 3-4 shade clusters and 1px contour are visually reviewed, not mathematically certified.','sheets':results}
 (ROOT/'validation.json').write_text(json.dumps(doc,indent=2),encoding='utf-8')
 for r in results:print(r['sheet'],r['passed'],{k:r.get(k) for k in ['colors','max_center_drift','max_height_drift','edge_pixels','max_hover_center_error']})
if __name__=='__main__':
 if sys.argv[1]=='refs':refs()
 elif sys.argv[1]=='process':process(sys.argv[2],int(sys.argv[3]) if len(sys.argv)>3 else 1)
 elif sys.argv[1]=='validate':validate()
