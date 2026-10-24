"""Dependency-free imagegen normalization. Writes only beside this script."""
from pathlib import Path
import struct,zlib,json,collections,hashlib
ROOT=Path(__file__).resolve().parent
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