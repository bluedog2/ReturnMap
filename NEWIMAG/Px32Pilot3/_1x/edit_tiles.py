import struct,zlib, pathlib,collections,json,shutil,random
ROOT=pathlib.Path(__file__).resolve().parents[1]
SRC=ROOT.parent/'Px32Pilot'/'_1x'
def read(p):
 b=p.read_bytes(); i=8; data=b''
 while i<len(b):
  n=struct.unpack('>I',b[i:i+4])[0]; t=b[i+4:i+8]; c=b[i+8:i+8+n]; i+=n+12
  if t==b'IHDR': w,h,depth,ct,*_=struct.unpack('>IIBBBBB',c)
  if t==b'IDAT': data+=c
 assert (depth,ct)==(8,6)
 raw=zlib.decompress(data); rows=[]; prev=[0]*(w*4); k=0
 for y in range(h):
  f=raw[k]; k+=1; row=list(raw[k:k+w*4]); k+=w*4
  for x in range(w*4):
   a=row[x-4] if x>=4 else 0; b=prev[x]; c=prev[x-4] if x>=4 else 0
   p=a+b-c; ds=[abs(p-a),abs(p-b),abs(p-c)]; pa=[a,b,c][ds.index(min(ds))]
   row[x]=(row[x]+[0,a,b,(a+b)//2,pa][f])%256
  rows.append([tuple(row[x:x+4]) for x in range(0,w*4,4)]); prev=row
 return rows
def write(p,rows,scale=1):
 rows=[[c for c in row for _ in range(scale)] for row in rows for _ in range(scale)]
 h=len(rows); w=len(rows[0]); raw=b''.join(b'\0'+bytes(v for c in row for v in c) for row in rows)
 def chunk(t,d): return struct.pack('>I',len(d))+t+d+struct.pack('>I',zlib.crc32(t+d))
 p.write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',w,h,8,6,0,0,0))+chunk(b'IDAT',zlib.compress(raw,9))+chunk(b'IEND',b''))
PALETTE='692820 654138 2B231B 4D3720 554639 B0701C A87B56 CEA362 CAA981 C5AF92 DEC28D 716432 C8A332 E3DBC3 293145 384869 53617E 19142E 27213F 2F2F50 302460 8284A1 040205 47345B 4B3478 2C182D 5F3861 6F2F73 7E2D94 180711 46353A 47141D 8C1D24 902F36 AF1F28 1A151A 2A2B32 6C665E 928982 A0AAA7 F2C14E FFF4D6 3E6B3A 6E9A4A 3FC1C9 8FB8E8'.split()
def color(s): return tuple(bytes.fromhex(s))+(255,)
allowed=set(map(color,PALETTE))
def border(a,b): return sum(a[y][x]!=b[y][x] for y in range(32) for x in range(32) if x<2 or x>=30 or y<2 or y>=30)
def changed(a,b): return sum(a[y][x]!=b[y][x] for y in range(2,30) for x in range(2,30))
def build():
 out={}; report={'tiles':[], 'boundary_pairs':[], 'seed':32703}; layouts={}
 for kind in ['Floor','Wall','Ceiling']:
  a=read(SRC/f'Tile_Play_{kind}.png'); ba=read(SRC/f'Tile_Build_{kind}.png')
  mapping={a[y][x]:ba[y][x] for y in range(32) for x in range(32)}
  mapping.update({color('4B3478'):color('3E6B3A'),color('7E2D94'):color('6E9A4A')})
  variants={'A':a}
  for v in ('BC' if kind!='Ceiling' else 'B'):
   b=[r[:] for r in a]; sign=1 if v=='B' else -1
   bands={'Floor':[(4,10,3),(14,24,3)], 'Wall':[(3,8,4),(10,15,-3),(17,23,3)], 'Ceiling':[(2,6,3),(11,21,-4)]}[kind]
   # Relocate entire existing stone contours, including their lit and shadowed sides.
   for y0,y1,dx in bands:
    for y in range(y0,y1+1):
     for x in range(2,30):
      sx=x-sign*dx
      if 2<=sx<30: b[y][x]=a[y][sx]
   if kind=='Floor':
    # Keep the original border and top cap; reduce interior horizontal mortar contrast.
    for y in [11,26,27]:
     for x in range(2,30):
      if b[y][x]==color('293145'): b[y][x]=color('384869')
   # Short stepped purple mineral cracks / corresponding green moss pixels.
   ox,oy={'Floor':(19,16),'Wall':(8,18),'Ceiling':(19,13)}[kind]
   if v=='C': ox,oy=7,15
   pts=[(0,0),(1,1),(1,2),(2,3),(1,4),(0,5),(-1,5)]
   for i,(dx,dy) in enumerate(pts): b[oy+dy][ox+dx]=color('7E2D94' if i in [1,4] else '4B3478')
   # Bound the edit budget by restoring complete peripheral portions of shifted bands.
   if changed(a,b)>235:
    for y0,y1,dx in reversed(bands):
     for x in list(range(2,ox-2))+list(range(29,ox+4,-1)):
      for y in range(y0,y1+1):
       if changed(a,b)<=225: break
       if b[y][x] not in [color('4B3478'),color('7E2D94')]: b[y][x]=a[y][x]
      if changed(a,b)<=225: break
     if changed(a,b)<=225: break
   assert 118<=changed(a,b)<=235,(kind,v,changed(a,b))
   assert border(a,b)==0
   variants[v]=b
  for mode in ['Play','Build']:
   for v,p in variants.items():
    rows=p if mode=='Play' else [[mapping[c] for c in r] for r in p]
    name=f'Tile_{mode}_{kind}_{v}'; original=a if mode=='Play' else ba
    if v=='A': shutil.copyfile(SRC/f'Tile_{mode}_{kind}.png',ROOT/'_1x'/f'{name}.png')
    else: write(ROOT/'_1x'/f'{name}.png',rows)
    write(ROOT/'Tiles'/f'{name}.png',rows,8); out[(mode,kind,v)]=rows
    cs=set(c for r in rows for c in r); n=changed(original,rows)
    report['tiles'].append({'name':name,'colors':len(cs),'outside_palette':sum(c not in allowed for r in rows for c in r),'interior_changed':n,'interior_total':784,'interior_change_percent':round(100*n/784,3),'border_change':border(original,rows),'A_byte_identical':(ROOT/'_1x'/f'{name}.png').read_bytes()==(SRC/f'Tile_{mode}_{kind}.png').read_bytes() if v=='A' else None})
   for va in variants:
    for vb in variants:
     p=out[(mode,kind,va)]; q=out[(mode,kind,vb)]
     report['boundary_pairs'].append({'mode':mode,'kind':kind,'pair':va+vb,'same_position_2px_border_mismatch':border(p,q),'touching_lr_color_mismatch':sum(p[y][31]!=q[y][0] for y in range(32)),'touching_tb_color_mismatch':sum(p[31][x]!=q[0][x] for x in range(32))})
 rng=random.Random(32703); preview=[]
 for mode in ['Play','Build']:
  rng=random.Random(32703)
  layout=[]
  for kind in ['Floor','Wall','Wall','Wall','Ceiling']:
   vs='AB' if kind=='Ceiling' else 'ABC'; row=[rng.choice(vs) for _ in range(12)]; layout.append({'kind':kind,'variants':''.join(row)})
   for y in range(32): preview.append([c for v in row for c in out[(mode,kind,v)][y]])
  layouts[mode]=layout
 write(ROOT/'Tiles'/'Tile_Random_3x.png',preview,3)
 report['preview']={'size':[1152,960],'layout':layouts}
 # Verify saved PNG decoding, scaling, theme mapping, and exact immutable borders.
 for (mode,k,v),rows in out.items():
  assert read(ROOT/'_1x'/f'Tile_{mode}_{k}_{v}.png')==rows
  large=read(ROOT/'Tiles'/f'Tile_{mode}_{k}_{v}.png')
  assert all(large[y][x]==rows[y//8][x//8] for y in range(256) for x in range(256))
 mismatch=0
 for k in ['Floor','Wall','Ceiling']:
  ap=read(SRC/f'Tile_Play_{k}.png'); ab=read(SRC/f'Tile_Build_{k}.png'); mp={ap[y][x]:ab[y][x] for y in range(32) for x in range(32)}; mp.update({color('4B3478'):color('3E6B3A'),color('7E2D94'):color('6E9A4A')})
  for v in ('ABC' if k!='Ceiling' else 'AB'):
   mismatch+=sum(mp[out[('Play',k,v)][y][x]]!=out[('Build',k,v)][y][x] for y in range(32) for x in range(32))
 report['palette_layout_mismatch']=mismatch
 report['limitations']=['A is byte-for-byte original; its horizontal mortar is unchanged. B/C soften only interior Floor mortar.', 'The fixed 2px border and original brick-course rhythm remain recognizable in long runs, despite interior variations.']
 (ROOT/'_1x'/'Validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
 print('Tiles:',len(report['tiles']),'Theme mismatch:',mismatch,'Boundary mismatch:',sum(p['same_position_2px_border_mismatch']+p['touching_lr_color_mismatch']+p['touching_tb_color_mismatch'] for p in report['boundary_pairs']))
if __name__=='__main__': build()
