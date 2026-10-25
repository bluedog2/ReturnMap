"""Reopen deliverables and independently verify each native pixel and 8x block."""
from pixel_io import *

manifest=json.loads((ROOT/'generation.json').read_text(encoding='utf8'))
reports={}
allowed={p[:3] for p in MP};outline=color('1A151A')
for name,rec in manifest['files'].items():
 n=rec['cell'];count=rec['frames'];a=readpng(ROOT/rec['one_x']);large=readpng(ROOT/rec['eight_x'])
 assert len(a)==n and len(a[0])==n*count
 assert large==up(a,8)
 frames=[[r[i*n:(i+1)*n] for r in a] for i in range(count)]
 stats=[];colors={p[:3] for r in a for p in r if p[3]}
 assert len(colors)<=16
 for i,f in enumerate(frames):
  bb=bbox(f);outside={p[:3] for r in f for p in r if p[:3] not in allowed}
  edges=sum(bool(f[y][x][3]) for y in range(n) for x in range(n) if y==0 or x==0 or x==n-1)
  outlines=0
  for y,r in enumerate(f):
   for x,p in enumerate(r):
    assert p[3] in (0,255)
    if p[3] and any(xx<0 or yy<0 or xx>=n or yy>=n or not f[yy][xx][3] for xx,yy in ((x-1,y),(x+1,y),(x,y-1),(x,y+1))):outlines+=p!=outline
  stat={'frame':i,'bbox':bb,'bottom_png_y':bb[3],'bottom_origin_row':n-1-bb[3],'center_x':(bb[0]+bb[2])/2,'height':bb[3]-bb[1]+1,'palette_outside_colors':len(outside),'edge_opaque_pixels':edges,'outline_errors':outlines}
  assert not outside and edges==outlines==stat['bottom_origin_row']==0,stat
  stats.append(stat)
 centers=[s['center_x'] for s in stats];heights=[s['height'] for s in stats]
 cd=max(abs(v-sum(centers)/count) for v in centers);hd=max(abs(v-sum(heights)/count) for v in heights)
 if not name.endswith('Death'):assert cd<=2 and hd<=2
 changes=[sum(frames[i][y][x]!=frames[i+1][y][x] for y in range(n) for x in range(n)) for i in range(count-1)]
 assert min(changes)>0
 reports[name]={'passed':True,'frames':stats,'sheet_opaque_colors':len(colors),'alpha_values':sorted({p[3] for r in a for p in r}),'center_max_deviation':cd,'height_max_deviation':hd,'nearest_neighbor_block_errors':0,'adjacent_frame_changed_pixels':changes,'saved_size':[n*count*8,n*8],'one_x_sha256':hashlib.sha256((ROOT/rec['one_x']).read_bytes()).hexdigest(),'eight_x_sha256':hashlib.sha256((ROOT/rec['eight_x']).read_bytes()).hexdigest()}
result={'all_passed':True,'total_frames':sum(r['frames'] for r in manifest['files'].values()),'row_convention':'PNG y downwards. bottom_origin_row = cell_height - 1 - bottom_png_y.','outline_rule':'One-pixel inner boundary uses darkest neutral material ramp #1A151A.','sheets':reports}
(ROOT/'validation.json').write_text(json.dumps(result,indent=2),encoding='utf8')
print(json.dumps({name:{'bottom':[s['bottom_origin_row'] for s in r['frames']],'centers':[s['center_x'] for s in r['frames']],'heights':[s['height'] for s in r['frames']],'colors':r['sheet_opaque_colors'],'changes':r['adjacent_frame_changed_pixels']} for name,r in reports.items()},indent=2))
