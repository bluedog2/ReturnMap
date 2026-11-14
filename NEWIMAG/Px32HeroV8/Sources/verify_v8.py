from PIL import Image,ImageChops
from pathlib import Path
import json,hashlib
B=Path(__file__).resolve().parent.parent
src=B.parent.parent/'Assets/_Project/ResourcceEX/Sprites/Px32/Player/Hero_Idle.png'
original=Image.open(src).convert('RGBA')
v=json.loads((B/'validation.json').read_text(encoding='utf-8'))
checks=[]
for r in v['frames']:
 name=r['stage'];i=r['frame']
 path=B/'Option2_NoBag/_1x/Hero_BagStow.png' if name=='BagStow' else B/name/'_1x/Hero_Idle.png'
 sheet=Image.open(path).convert('RGBA');frame=sheet.crop((i*128,0,(i+1)*128,128))
 oi=0 if name=='BagStow' else i;o=original.crop((oi*128,0,(oi+1)*128,128))
 mask=Image.open(B/r['mask']).convert('L')
 mismatch=sum(a!=b and m==0 for a,b,m in zip(frame.getdata(),o.getdata(),mask.getdata()))
 assert mismatch==0
 assert {p[3] for p in frame.getdata()}=={0,255}
 assert len({p[:3] for p in frame.getdata() if p[3]})<=24
 assert frame.getbbox()[3]==128
 checks.append(dict(stage=name,frame=i,outside_mismatch=mismatch))
for name in ['Option1_HipBag','Option2_NoBag']:
 s=Image.open(B/name/'_1x/Hero_Idle.png').convert('RGBA')
 ma=Image.open(B/name/'Hero_Idle0_master.png').convert('RGBA')
 assert s.size==(768,128) and ma.size==(128,128)
 assert list(s.crop((0,0,128,128)).getdata())==list(ma.getdata())
 for i in range(6):
  f=s.crop((128*i,0,128*(i+1),128));mask=Image.open(B/f'Masks/{name}_{i:02}.png').convert('L')
  assert all(a==b for a,b,m in zip(f.getdata(),ma.getdata(),mask.getdata()) if m)
stow=Image.open(B/'Option2_NoBag/_1x/Hero_BagStow.png').convert('RGBA')
assert stow.size==(512,128)
assert list(stow.crop((0,0,128,128)).getdata())==list(original.crop((0,0,128,128)).getdata())
assert list(stow.crop((384,0,512,128)).getdata())==list(Image.open(B/'Option2_NoBag/Hero_Idle0_master.png').convert('RGBA').getdata())
gifs=[]
for p in B.glob('Option*/*.gif'):
 g=Image.open(p);is_stow='BagStow' in p.name;expected=4 if is_stow else 6
 assert g.size==(384,384) and g.n_frames==expected
 s=Image.open(p.parent/'_1x'/p.name.replace('_3x.gif','.png')).convert('RGBA')
 ds=[]
 for i in range(g.n_frames):
  g.seek(i);ds.append(g.info['duration']);a=g.convert('RGBA');b=s.crop((i*128,0,(i+1)*128,128)).resize((384,384),Image.Resampling.NEAREST)
  assert all((pa==pb if pb[3] else pa[3]==0) for pa,pb in zip(a.getdata(),b.getdata()))
 gifs.append(dict(path=str(p.relative_to(B)),frames=g.n_frames,durations_ms=ds,decoded_pixels_match=True))
assert v['source_sha256']==hashlib.sha256(src.read_bytes()).hexdigest()
v['independent_checks']=dict(pass_all=True,frames=checks,gifs=gifs,translation_reuse=True,endpoint_equality=True)
(B/'validation.json').write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(dict(pass_all=True,frames=len(checks),gifs=gifs),ensure_ascii=False))
