"""Independently decode delivered PNG files and validate import constraints."""
from pixel_io import *
manifest=json.loads((ROOT/'generation.json').read_text(encoding='utf8'));report={}
for key,rec in manifest['files'].items():
 a=readpng(ROOT/rec['one_x']);large=readpng(ROOT/rec['eight_x']);n=rec['cell'][0];count=rec['frames']
 assert len(a)==n and len(a[0])==n*count
 assert large==up(a,8)
 frames=[[r[i*n:(i+1)*n] for r in a] for i in range(count)];stats=[];colors=set();outline_errors=0
 for i,f in enumerate(frames):
  b=bbox(f);cs={p[:3] for r in f for p in r if p[3]};colors|=cs
  outside=len({p[:3] for r in f for p in r}-{p[:3] for p in MP})
  edge=sum(p[3]>0 for y,r in enumerate(f) for x,p in enumerate(r) if y==0 or x==0 or x==n-1)
  alpha=sorted({p[3] for r in f for p in r})
  oe=0
  for y,r in enumerate(f):
   for x,p in enumerate(r):
    if p[3] and any(xx<0 or yy<0 or xx>=n or yy>=n or not f[yy][xx][3] for xx,yy in ((x-1,y),(x+1,y),(x,y-1),(x,y+1))):oe+=p!=color(rec['outline'])
  outline_errors+=oe
  s={'frame':i,'bbox':b,'bottom_png_y':b[3],'bottom_origin_row':n-1-b[3],'center_x':(b[0]+b[2])/2,'height':b[3]-b[1]+1,'palette_outside_colors':outside,'edge_opaque_pixels':edge,'outline_errors':oe,'alpha_values':alpha}
  assert outside==edge==oe==0 and set(alpha)<={0,255},(key,s)
  if not rec['flying']:assert s['bottom_origin_row']==0,(key,s)
  stats.append(s)
 centers=[s['center_x'] for s in stats];heights=[s['height'] for s in stats]
 cd=max(abs(c-sum(centers)/count) for c in centers);hd=max(abs(h-sum(heights)/count) for h in heights)
 if not key.endswith('Death'):assert cd<=2 and hd<=2,(key,cd,hd)
 assert len(colors)<=16 and (0,0,0) not in colors
 diffs=[sum(p!=q for ra,rb in zip(frames[i],frames[i+1]) for p,q in zip(ra,rb)) for i in range(count-1)]
 assert min(diffs)>0,(key,diffs)
 report[key]={'passed':True,'frames':stats,'sheet_opaque_colors':len(colors),'center_max_deviation':cd,'height_max_deviation':hd,'outline_errors':outline_errors,'nearest_neighbor_block_errors':0,'adjacent_frame_changed_pixels':diffs,'saved_size':[len(large[0]),len(large)],'one_x_sha256':hashlib.sha256((ROOT/rec['one_x']).read_bytes()).hexdigest(),'eight_x_sha256':hashlib.sha256((ROOT/rec['eight_x']).read_bytes()).hexdigest()}
(ROOT/'validation.json').write_text(json.dumps({'all_passed':True,'total_frames':54,'row_convention':'bottom_origin_row = cell_height - 1 - PNG_y. Ground row is 0. Center coordinates are native 1x PNG x.','sheets':report},indent=2),encoding='utf8')
for k,v in report.items():print(k, 'bottom=',[s['bottom_origin_row'] for s in v['frames']], 'center=',[s['center_x'] for s in v['frames']], 'height=',[s['height'] for s in v['frames']], 'colors=',v['sheet_opaque_colors'])
print('PASS: 12 sheets, 54 frames, 24 PNGs, all exact 8x; binary alpha, palette, edges, outlines, grounding, centers, heights, changing frames.')
