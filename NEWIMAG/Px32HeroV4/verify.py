"""Independent file checks, GIF LZW decode and exact nearest-3x pixel comparison.
Uses only Python standard library. Does not turn a visually rejected sheet into a pass.
"""
import json,struct,hashlib
from legacy_io import ROOT,readpng

def decode_gif(path):
 b=path.read_bytes();assert b[:6]==b'GIF89a'
 w,h,flags,_,_=struct.unpack('<HHBBB',b[6:13]);p=13
 n=2**((flags&7)+1);pal=[tuple(b[p+i*3:p+i*3+3]) for i in range(n)];p+=n*3;frames=[];delay=0;trans=0;loop=False;disposal=0
 def blocks(p):
  out=b''
  while b[p]:
   sz=b[p];out+=b[p+1:p+1+sz];p+=1+sz
  return out,p+1
 while b[p]!=59:
  marker=b[p];p+=1
  if marker==33:
   typ=b[p];p+=1
   if typ==249:
    assert b[p]==4;gce=b[p+1:p+5];delay=struct.unpack('<H',gce[1:3])[0];trans=gce[3];disposal=(gce[0]>>2)&7;p+=6
   else:
    ext,p=blocks(p)
    if typ==255 and b'NETSCAPE2.0' in ext:loop=True
  elif marker==44:
   x,y,fw,fh,flags=struct.unpack('<HHHHB',b[p:p+9]);p+=9;assert x==y==0 and fw==w and fh==h and flags==0
   minimum=b[p];p+=1;packed,p=blocks(p);clear=1<<minimum;stop=clear+1;bit=0;codesize=minimum+1;table={i:bytes([i]) for i in range(clear)};nextcode=stop+1;prev=None;indices=bytearray()
   while True:
    val=0
    for j in range(codesize):val|=((packed[(bit+j)//8]>>((bit+j)%8))&1)<<j
    bit+=codesize
    if val==clear:table={i:bytes([i]) for i in range(clear)};nextcode=stop+1;codesize=minimum+1;prev=None;continue
    if val==stop:break
    entry=table.get(val)
    if entry is None:
     assert prev is not None and val==nextcode;entry=prev+prev[:1]
    indices.extend(entry)
    if prev is not None:
     table[nextcode]=prev+entry[:1];nextcode+=1
     if nextcode==(1<<codesize) and codesize<12:codesize+=1
    prev=entry
   assert len(indices)==w*h
   rgba=[(0,0,0,0) if v==trans else pal[v]+(255,) for v in indices]
   frames.append({'rgba':rgba,'delay':delay,'disposal':disposal})
  else:raise ValueError(marker)
 return w,h,loop,frames

def main():
 v=json.loads((ROOT/'validation.json').read_text(encoding='utf-8'));checks={}
 for name,m in v['sheets'].items():
  png=readpng(ROOT/'_1x'/f'Hero_{name}.png');assert [len(png[0]),len(png)]==[128*len(m['frames']),128]
  assert {p[3] for r in png for p in r}=={0,255}
  assert len({p[:3] for r in png for p in r if p[3]})<=24
  w,h,loop,frames=decode_gif(ROOT/'Animations'/f'Hero_{name}_3x.gif');assert (w,h)==(384,384) and len(frames)==len(m['frames']) and loop==(name=='Run')
  for i,f in enumerate(frames):
   expected=[p for row in png for _ in range(3) for p in row[i*128:(i+1)*128] for _ in range(3)]
   assert f['rgba']==expected,(name,i,'GIF differs from exact nearest3x')
  assert [f['delay'] for f in frames]==m['gif']['delay_centiseconds']
  if name!='Run':assert frames[-1]['disposal']==1
  checks[name]={'png_geometry_alpha_palette':True,'gif_decode':True,'gif_nearest3x_exact':True,'gif_frame_count':len(frames),'gif_delays_cs':[f['delay'] for f in frames],'gif_loop':loop,'production_ready':m['production_ready']}
 cmp=readpng(ROOT/'Compare_3x.png');assert (len(cmp[0]),len(cmp))==(3456,1536)
 report={'artifact_checks_pass':True,'compare_dimensions':[3456,1536],'sheets':checks}
 (ROOT/'file_checks.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps(report,ensure_ascii=False,indent=2))
if __name__=='__main__':main()
