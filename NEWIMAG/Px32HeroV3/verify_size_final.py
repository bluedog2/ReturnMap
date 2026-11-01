import process as p
import json,struct,hashlib
R=p.ROOT
v=json.loads((R/'validation.json').read_text(encoding='utf-8'))
def gifframes(path):
 d=path.read_bytes();assert d[:6]==b'GIF89a'
 w,h=struct.unpack('<HH',d[6:10]);at=13+(3*(2**((d[10]&7)+1)) if d[10]&128 else 0);count=0
 while d[at]!=0x3b:
  block=d[at];at+=1
  if block==0x21:at+=1
  elif block==0x2c:
   packed=d[at+8];at+=9
   if packed&128:at+=3*(2**((packed&7)+1))
   at+=1;count+=1
  else:raise AssertionError((at,block))
  while d[at]:at+=1+d[at]
  at+=1
 return w,h,count
checks={}
for n,count in {'Run':8,'JumpStart':2,'Fall':2,'Land':3,'Dash':4,'Hit':2,'Death':6}.items():
 a=p.readpng(R/'_1x'/f'Hero_{n}.png');b=p.readpng(R/'Player'/f'Hero_{n}.png')
 assert len(a)==128 and len(a[0])==128*count
 assert b==p.nearest(a,4)
 alphas=sorted({px[3] for row in a for px in row});assert alphas==[0,255]
 colors={px[:3] for row in a for px in row if px[3]};assert len(colors)<=24 and colors<=set(p.PAL)
 assert gifframes(R/'Animations'/f'Hero_{n}_3x.gif')==(384,384,count)
 fs=[[row[i*128:(i+1)*128] for row in a] for i in range(count)]
 m=p.metrics(fs,n)
 assert all(f['forbidden_edge_pixels']==0 and f['bottom_row']==127 for f in m['frames'])
 assert [f['height'] for f in m['frames']]==[f['height'] for f in v['sheets'][n]['frames']]
 checks[n]={'files_verified':3,'colors':len(colors),'alpha_values':alphas,'nearest_4x_exact':True,'gif_frames':count,'edge_and_baseline_pass':True}
for path,digest in v['idle_hashes_before'].items():assert hashlib.sha256((R/path).read_bytes()).hexdigest()==digest
chart=p.readpng(R/'SizeCheck_3x.png');assert (len(chart[0]),len(chart))==(4368,570)
v['saved_artifact_verification']={'sheets':checks,'idle_unchanged':True,'size_chart_dimensions':[4368,570],'strict_size_success':False}
(R/'validation.json').write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(v['saved_artifact_verification'],indent=2))
