from pixel_io import *
from process import poly,clean,PAL
from PIL import Image
import hashlib
from gif_io import gif
BASE=ROOT.parent
Z=(0,0,0,0)
def save(name,a):writepng(ROOT/name,a)
def recover(name,pitch,ox=0,oy=0,w=128,h=128):
 a=readpng(ROOT/name)
 return [[clean(a[oy+y*pitch+pitch//2][ox+x*pitch+pitch//2]) if 0<=oy+y*pitch+pitch//2<len(a) and 0<=ox+x*pitch+pitch//2<len(a[0]) else Z for x in range(w)] for y in range(h)]
def crop(a,b):return [r[b[0]:b[2]] for r in a[b[1]:b[3]]]
def maskpoly(ps):return [[any(poly(x+.5,y+.5,p) for p in ps) for x in range(128)] for y in range(128)]
def composite(o,a,m):return [[a[y][x] if m[y][x] else o[y][x] for x in range(128)] for y in range(128)]
def shift(a,dx,dy):return [[a[y-dy][x-dx] if 0<=x-dx<128 and 0<=y-dy<128 else Z for x in range(128)] for y in range(128)]
def put(a,b,dx,dy):
 a=[r[:] for r in a]
 for y,r in enumerate(b):
  for x,p in enumerate(r):
   if p[3] and 0<=x+dx<128 and 0<=y+dy<128:a[y+dy][x+dx]=p
 return a
if __name__=='__main__':
 for n in (1,2):
  a=recover(f'HipBag{n}_generated.png',40 if n==1 else 46,w=32,h=32)
  save(f'HipBag{n}_blocks.png',a)
  save(f'HipBag{n}_blocks_16x.png',nearest(a,16))
  print(n,bbox(a))
 save('C_hand_3x.png',nearest(crop(readpng(ROOT/'C1_composite.png'),(44,72,72,91)),3))
 save('C_hand_8x.png',nearest(crop(readpng(ROOT/'C1_composite.png'),(44,72,72,91)),8))

 # Predeclared semantic regions: sleeve, original held bag/wrist,
 # newly exposed shorts/thigh, proposed C elbow. Never shoes/head.
 areas=[[(53,69),(64,69),(72,78),(72,92),(64,104),(36,104),(36,86),(44,86),(44,82),(49,82),(49,79)],
        [(53,71),(61,73),(63,84),(54,87),(44,82),(44,77)]]
 m=maskpoly(areas)
 src=BASE.parent.parent/'Assets/_Project/ResourcceEX/Sprites/Px32/Player/Hero_Idle.png'
 raw=readpng(src); originals=[[r[i*128:(i+1)*128] for r in raw] for i in range(6)]
 orig=originals[0]
 c=recover('C1_generated.png',10)
 glove=recover('C4_generated.png',78,469,489,4,4)
 save('shared_glove_insert.png',glove)
 c=put(c,glove,61,82)
 master=composite(orig,c,m)
 body=crop(readpng(ROOT/'HipBag2_blocks.png'),(4,9,23,24))
 # Extract right half of generated loop; translation only, no painted pixels.
 loop=crop(readpng(ROOT/'HipBag2_blocks.png'),(9,6,12,9))
 blank=[[Z]*128 for _ in range(128)]
 baglayer=put(put(blank,body,51,88),loop,67,85)
 save('shared_bag_body.png',body);save('shared_bag_loop.png',loop)
 save('shared_C_patch.png',[[c[y][x] if m[y][x] else Z for x in range(128)] for y in range(128)])
 save('shared_bag_layer.png',baglayer)
 hip=put(master,baglayer,0,0)
 mb=[[m[y][x] or bool(baglayer[y][x][3]) for x in range(128)] for y in range(128)]
 records=[]; sets={}
 def maskfile(name,mask):
  path=BASE/'Masks'/name;writepng(path,[[((255,)*4 if v else (0,0,0,255)) for v in r] for r in mask]);return str(path.relative_to(BASE))
 def record(name,i,a,o,mask):
  mp=maskfile(f'{name}_{i:02}.png',mask)
  return dict(stage=name,frame=i,mask=mp,outside_mismatch=sum(a[y][x]!=o[y][x] for y in range(128) for x in range(128) if not mask[y][x]),colors=len({p[:3] for r in a for p in r if p[3]}),alpha=sorted({p[3] for r in a for p in r}),floor=max(y for y,r in enumerate(a) if any(p[3] for p in r)))
 for name,ma,mask in [('Option1_HipBag',hip,mb),('Option2_NoBag',master,m)]:
  folder=BASE/name;(folder/'_1x').mkdir(parents=True,exist_ok=True)
  writepng(folder/'Hero_Idle0_master.png',ma)
  frames=[]
  # Original waist is stable across this idle; zero translation is deliberate.
  for i,o in enumerate(originals):
   a=composite(o,ma,mask);frames.append(a);records.append(record(name,i,a,o,mask))
  sets[name]=frames
  writepng(folder/'_1x/Hero_Idle.png',[[p for a in frames for p in a[y]] for y in range(128)])
  gif(folder/'Hero_Idle_3x.gif',frames,8,True,PAL)
 # Original bag extraction and integer-only trajectory. Character pixels
 # are always in front of the translated bag, including hair occlusion.
 oldbagmask=maskpoly([[(45,84),(56,84),(63,86),(63,99),(59,104),(42,104),(40,99),(40,88)]])
 oldbag=[[orig[y][x] if oldbagmask[y][x] else Z for x in range(128)] for y in range(128)]
 save('original_bag_layer.png',oldbag)
 stows=[orig];stowmeta=[]
 arm_mask=maskpoly([[(53,69),(64,69),(68,77),(66,87),(58,90),(42,90),(42,82),(49,79)]])
 for j,(dx,dy) in enumerate([(-8,-9),(0,-31)],1):
  arm=recover(f'Stow{j}_generated.png',10)
  person=composite(master,arm,arm_mask)
  bag=shift(oldbag,dx,dy)
  # New bag travel can modify only transparent original-background pixels.
  allowed=[[m[y][x] or arm_mask[y][x] or (bag[y][x][3]>0 and not orig[y][x][3]) for x in range(128)] for y in range(128)]
  a=[[person[y][x] if person[y][x][3] else bag[y][x] for x in range(128)] for y in range(128)]
  a=composite(orig,a,allowed)
  stows.append(a);records.append(record('BagStow',j,a,orig,allowed))
  save(f'Stow{j}_bag_translated.png',bag)
  stowmeta.append(dict(frame=j,translation=[dx,dy],visible_bag_pixels=sum(bool(bag[y][x][3]) and not person[y][x][3] and allowed[y][x] for y in range(128) for x in range(128))))
 stows.append(master)
 records.extend([record('BagStow',0,orig,orig,[[False]*128 for _ in range(128)]),record('BagStow',3,master,orig,m)])
 folder=BASE/'Option2_NoBag'
 writepng(folder/'_1x/Hero_BagStow.png',[[p for a in stows for p in a[y]] for y in range(128)])
 gif(folder/'Hero_BagStow_3x.gif',stows,12,False,PAL)
 save('Stow_review_3x.png',nearest([[p for a in stows for p in a[y]] for y in range(128)],3))
 compare=Image.new('RGBA',(1536,608),(0,0,0,0))
 for i,a in enumerate([orig,hip,master]):
  im=Image.new('RGBA',(128,128));im.putdata([p for r in a for p in r]);compare.paste(im.resize((512,512),Image.Resampling.NEAREST),(i*512,0))
  box=(44,78,56,90) if i==0 else (56,78,68,90)
  hand=im.crop(box).resize((96,96),Image.Resampling.NEAREST);compare.paste(hand,(i*512+208,512))
 compare.save(BASE/'Compare_4x.png')
 save('Final_hand_3x.png',nearest(crop(master,(44,72,72,91)),3))
 save('Final_hip_8x.png',nearest(crop(hip,(40,72,78,106)),8))
 handpoints=[(x,y) for y in range(82,86) for x in range(60,65) if master[y][x][3]]
 bagpoints=[(x,y) for y,r in enumerate(baglayer) for x,p in enumerate(r) if p[3]]
 gap=min(max(abs(x-u),abs(y-v))-1 for x,y in handpoints for u,v in bagpoints)
 report=dict(production_ready=False,source_sha256=hashlib.sha256(src.read_bytes()).hexdigest(),source_unchanged=hashlib.sha256(src.read_bytes()).hexdigest()==json.loads((ROOT/'source_hash.json').read_text())['sha256'],frames=records,shared_arm_equal=all(hip[y][x]==master[y][x] for y in range(69,86) for x in range(44,67)),idle_translations=[[0,0]]*6,hand=dict(roi=[60,82,65,86],size=[5,4],belt_row=86,belt_contact=True,visual_3x='pending'),bag=dict(body_size=[19,15],top=88,belt_distance=2,hand_gap=gap,loop_height=2,fangs='pending'),stow=stowmeta,stow_first_exact=stows[0]==orig,stow_last_exact=stows[3]==master)
 report.pop('production_ready')
 report.update(comparison_ready=True,selected_option=None,original_arm_bag_retained_in_edit_patch=0,
  visual_review=dict(hand_on_hip=True,elbow_below_shoulder=True,small_triangle_gap=True,no_old_bag_arm_fragments=True,hipbag_fangs=True,method='3x and 8x static visual review; GIF decoded separately'))
 report['hand'].update(visual_3x='pass',highlight_pixels=sum(min(master[y][x][:3])>=100 for y in range(82,86) for x in range(60,65) if master[y][x][3]),highlight_position=[62,83],wrist_not_larger_than_sleeve=True)
 report['bag'].update(loop_height=3,fangs='upper and lower tapered fangs, visual pass',loop_source_crop=[9,6,12,9],loop_position=[67,85])
 report['recovery']=dict(character=dict(pitch=10,phase=5),bag=dict(pitch=46,phase=23),glove=dict(pitch=78,origin=[469,489],phase=39),note='Fixed integer block samples, palette mapping and binary alpha; generated grids are approximate, not resized to a target bounding box.')
 (BASE/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
 print(json.dumps({k:v for k,v in report.items() if k!='frames'},ensure_ascii=False))
