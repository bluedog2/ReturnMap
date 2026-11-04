"""Standard-library-only source recovery/compositing/assembly. No drawn character pixels.
Run: python NEWIMAG/Px32HeroV4/process.py
All coordinates inclusive. Sampling pitch is recorded per source, never fitted per frame.
"""
from legacy_io import *
import hashlib
ZERO=(0,0,0,0)
# Approved local reference is an exact 4x preview; recover without reading Assets.
IDLE=[r[::4] for r in readpng(ROOT/'Sources/Idle_reference_4x.png')[::4]]
PAL=sorted({p[:3] for r in IDLE for p in r if p[3]})

def clean(p):
 if p[3]<128:return ZERO
 return min(PAL,key=lambda c:sum((c[i]-p[i])**2 for i in range(3)))+(255,)

def recover(name,cols,rows,count,k,attempt=1):
 src=readpng(ROOT/'Sources'/f'{name}_attempt{attempt}.png');h=len(src);w=len(src[0]);fs=[];meta=[]
 for i in range(count):
  x0=(i%cols)*w//cols;y0=(i//cols)*h//rows;x1=(i%cols+1)*w//cols;y1=(i//cols+1)*h//rows
  f=[[clean(src[y][x]) for x in range(x0+k//2,x1,k)] for y in range(y0+k//2,y1,k)]
  old=[r[:] for r in f]
  for y,r in enumerate(old):
   for x,p in enumerate(r):
    if p[3] and not any(old[yy][xx][3] for yy in range(max(0,y-1),min(len(old),y+2)) for xx in range(max(0,x-1),min(len(r),x+2)) if (xx,yy)!=(x,y)):f[y][x]=ZERO
  b=bbox(f); cropped=[r[b[0]:b[2]+1] for r in f[b[1]:b[3]+1]]
  fs.append(cropped);meta.append({'source_cell':[x0,y0,x1,y1],'sample_pitch':k,'sampled_bbox':b,'raw_size':[len(cropped[0]),len(cropped)]})
 return fs,meta

def headmask(x,y):
 # Visually annotated complete head, incl. ponytail/ribbon; excludes jacket/hand.
 return y<=66 or (67<=y<=74 and (x<=51-(y-67)//2 or x>=73+(y-67)//3)) or (75<=y<=83 and x<=47-(y-75)//3)

HEAD={(x,y):p for y,r in enumerate(IDLE) for x,p in enumerate(r) if p[3] and headmask(x,y)}
HB=[min(x for x,y in HEAD),min(y for x,y in HEAD),max(x for x,y in HEAD),max(y for x,y in HEAD)]

def gif(path,frames,fps,loop):
 colors=[(0,0,0)]+PAL;size=1
 while size<len(colors):size*=2
 colors +=[(0,0,0)]*(size-len(colors));bits=int(math.log2(size));lut={c:i+1 for i,c in enumerate(PAL)}
 data=b'GIF89a'+struct.pack('<HHBBB',384,384,0x80|((bits-1)<<4)|(bits-1),0,0)+bytes(v for c in colors for v in c)
 if loop:data+=b'!\xff\x0bNETSCAPE2.0\x03\x01\x00\x00\x00'
 delays=[]
 for fi,frame in enumerate(frames):
  delay=round((fi+1)*100/fps)-round(fi*100/fps);delays.append(delay)
  pixels=[lut[p[:3]] if p[3] else 0 for r in nearest(frame,3) for p in r]
  codes=[]
  for start in range(0,len(pixels),size-4):codes.append(size);codes.extend(pixels[start:start+size-4])
  codes.append(size+1);packed=bytearray();acc=nb=0
  for c in codes:
   acc|=c<<nb;nb+=bits+1
   while nb>=8:packed.append(acc&255);acc>>=8;nb-=8
  if nb:packed.append(acc)
  # Disposal 1 on final non-loop frame preserves the held last pose.
  disposal=1 if not loop and fi==len(frames)-1 else 2
  data+=b'!\xf9\x04'+bytes([(disposal<<2)|1])+struct.pack('<H',delay)+b'\x00\x00'+b','+struct.pack('<HHHHB',0,0,384,384,0)+bytes([bits])
  data+=b''.join(bytes([len(packed[i:i+255])])+packed[i:i+255] for i in range(0,len(packed),255))+b'\x00'
 path.write_bytes(data+b';');return delays

if __name__=='__main__':
 from revision1 import main
 main()
