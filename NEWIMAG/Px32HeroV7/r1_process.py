"""R1: fixed integer block recovery, palette cleanup, masks, translation and assembly only.
Never opens Assets or other NEWIMAG directories. No resizing/rotation or artwork drawing.
Generated grids are imperfect; this is a best candidate, not production approved.
"""
from pixel_io import *
from gif_io import gif
import hashlib
Z=(0,0,0,0)
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def poly(x,y,ps):
 hit=False
 for (a,b),(c,d) in zip(ps,ps[1:]+ps[:1]):
  if (b>y)!=(d>y) and x<(c-a)*(y-b)/(d-b)+a:hit=not hit
 return hit
originals={n:[readpng(ROOT/'Sources'/f'R1_{n}_{i:02}_original.png') for i in range(k)] for n,k in [('Idle',6),('Run',8)]}
PAL=sorted({p[:3] for f in originals['Idle'] for r in f for p in r if p[3]})
def clean(p):return min(PAL,key=lambda c:sum((c[k]-p[k])**2 for k in range(3)))+(255,) if p[3]>=128 else Z
def recover(path,pitch,w,h,ox=0,oy=0):
 a=readpng(ROOT/'Sources'/path)
 return [[clean(a[oy+y*pitch+pitch//2][ox+x*pitch+pitch//2]) if 0<=oy+y*pitch+pitch//2<len(a) and 0<=ox+x*pitch+pitch//2<len(a[0]) else Z for x in range(w)] for y in range(h)]
idle=recover('R1_Idle_attempt3.png',10,128,128)
run=[recover('R1_Run_attempt2.png',3,128,128,(i%4)*384,(i//4)*512) for i in range(8)]
baggrid=recover('R1_Run_attempt3_bag.png',40,32,32)
# Body/loop segmentation follows visible generated contour, not a resized bounding box.
bag=[r[7:24] for r in baggrid[13:27]]
loop=[r[19:22] for r in baggrid[9:13]]
writepng(ROOT/'Sources/R1_pouch_master.png',bag)
writepng(ROOT/'Sources/R1_loop_master.png',loop)
writepng(ROOT/'Sources/R1_pouch_master_16x.png',nearest(bag,16))
report={'revision':'재요청 1','production_ready':False,'source_policy':'Only exact 4x cached original repetition inverted; Assets never opened in R1','processing':{'fixed_grid_pitch':{'Idle':10,'Run':3,'pouch':40},'note':'Generated grid irregularity remains; no fitted resize, no rotation. Integer centers, palette cleanup, alpha threshold, cropped generated components and integer translation only.'},'pouch_body_size':[len(bag[0]),len(bag)],'pouch_master_sha256':sha(ROOT/'Sources/R1_pouch_master.png'),'sheets':{}}
compare=[]
for name,fs in originals.items():
 out=[];rows=[]
 for i,orig in enumerate(fs):
  dy=0 if name=='Idle' else [0,1,-3,-1,0,-2,-1,-4][i]
  belt=86 if name=='Idle' else 92+dy
  bx=51 if name=='Idle' else [49,50,50,48,49,50,51,49][i]
  by=belt+1
  cand=[r[:] for r in orig];mask=[[False]*128 for _ in range(128)]
  if name=='Idle':
   areas=[[(53,69),(65,69),(72,78),(72,92),(64,104),(36,104),(36,86),(49,83),(49,79)]]
   # Erase the old bag and arm using the generated uncovered-body pixels.
   for y in range(128):
    for x in range(128):
     if any(poly(x+.5,y+.5,p) for p in areas):mask[y][x]=True;cand[y][x]=idle[y][x]
   armarea=[(58,70),(65,70),(73,77),(79,83),(79,89),(72,89),(65,82),(60,79)]
   for y in range(70,90):
    for x in range(58,80):
     if poly(x+.5,y+.5,armarea):mask[y][x]=True;cand[y][x]=idle[y+4][x+16]
  else:
   areas=[[(43,79+dy),(57,77+dy),(68,81+dy),(78,84+dy),(79,93+dy),(61,99+dy),(48,99+dy),(39,90+dy)],[(27,91+dy),(49,88+dy),(58,98+dy),(55,112+dy),(27,112+dy)]]
   for y in range(128):
    for x in range(128):
     allowed=any(poly(x+.5,y+.5,p) for p in areas)
     # Lock exposed shoes and upper torso/hair. Only lower sleeve/old bag patches.
     if y<79 or (x>=70 and y<84):allowed=False
     if (i==2 and x<40 and y>=103) or (i==7 and x<42 and y>=101):allowed=False
     if allowed:mask[y][x]=True;cand[y][x]=run[i][y][x]
  # The new loop/body insert extends the allowed mask, never reaches shoes.
  layer=[[Z]*128 for _ in range(128)]
  for component,cx,cy in [(loop,bx+14,by-4),(bag,bx,by)]:
   for yy,r in enumerate(component):
    for xx,p in enumerate(r):
     if p[3]:mask[cy+yy][cx+xx]=True;cand[cy+yy][cx+xx]=p;layer[cy+yy][cx+xx]=p
  writepng(ROOT/'Masks'/f'{name}_{i:02}.png',[[((255,)*4 if p else (0,0,0,255)) for p in r] for r in mask])
  writepng(ROOT/'Sources'/f'R1_{name}_{i:02}_pouch_layer.png',layer)
  writepng(ROOT/'Sources'/f'R1_{name}_{i:02}_final_3x.png',nearest(cand,3))
  outside=sum(cand[y][x]!=orig[y][x] for y in range(128) for x in range(128) if not mask[y][x])
  row={'frame':i,'mask':f'Masks/{name}_{i:02}.png','outside_mask_mismatch_count':outside,'pouch_size':[len(bag[0]),len(bag)],'pouch_xy':[bx,by],'belt_row':belt,'belt_measurement_method':'manual original waistline inspection; row recorded before insertion','belt_reference_roi':[68,belt-1,83,belt+2],'belt_distance_px':by-belt,'loop_length_px':4,'loop_visible':True,'loop_attachment_visual_pass':False,'pouch_identical_under_translation':all(cand[by+y][bx+x]==p for y,r in enumerate(bag) for x,p in enumerate(r) if p[3]),'hand_gap_px':None,'arm_end_x':None,'colors':len({p[:3] for r in cand for p in r if p[3]}),'alpha_values':sorted({p[3] for r in cand for p in r}),'bottom_opaque_row':bbox(cand)[3],'production_ready':False}
  rows.append(row);out.append(cand);compare.extend(nearest([a+b for a,b in zip(orig,cand)],3))
 sheet=[sum([f[y] for f in out],[]) for y in range(128)]
 writepng(ROOT/'_1x'/f'Hero_{name}.png',sheet)
 cols=3 if name=='Idle' else 4
 board=[sum([out[i][y] for i in range(start,start+cols)],[]) for start in range(0,len(out),cols) for y in range(128)]
 writepng(ROOT/'Sources'/f'R1_{name}_final_3x.png',nearest(board,3))
 pal=sorted({p[:3] for r in sheet for p in r if p[3]})
 delays=gif(ROOT/'Animations'/f'Hero_{name}_3x.gif',out,8 if name=='Idle' else 18,True,pal)
 report['sheets'][name]={'production_ready':False,'attempts_this_revision':3,'frames':rows,'colors':len(pal),'gif_delays_cs':delays,'failure_reasons':['generated grid and sleeve/body seams require visual review','belt loop attachment not visually approved','leg counterphase and lag not quantitatively established']}
writepng(ROOT/'Compare_3x.png',compare)
(ROOT/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({n:[(r['outside_mask_mismatch_count'],r['colors'],r['bottom_opaque_row']) for r in s['frames']] for n,s in report['sheets'].items()}))
