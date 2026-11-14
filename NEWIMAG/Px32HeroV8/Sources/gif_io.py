from pixel_io import *
def gif(path,frames,fps,loop,PAL):
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

