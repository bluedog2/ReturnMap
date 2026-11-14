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
