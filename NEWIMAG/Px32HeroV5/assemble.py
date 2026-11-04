"""Translate and composite approved pixels only; never draw/rotate/warp a sprite."""
from process import *

SPECS={'Run':(4,2,8,18,[5,5,5],3,2),'Fall':(2,1,2,8,[10,12],2,3),'Land':(3,1,3,15,[6,8,8],3,3)}

def compose(body,name,i,ny):
 # Neck stump centre annotated from the first opaque row, not a silhouette fit.
 xs=[x for x,p in enumerate(body[0]) if p[3]]
 nx=round((min(xs)+max(xs))/2)
 hx,hy=nx-77,ny-73
 pixels={(x,y):p for y,r in enumerate(body) for x,p in enumerate(r) if p[3]}
 pixels.update({(x+hx,y+hy):p for (x,y),p in HEAD.items()})
 xx=[x for x,y in pixels];yy=[y for x,y in pixels]
 dx=round(63.5-(min(xx)+max(xx))/2)
 grounded=127-max(yy)
 if name=='Fall':
  desired=36-(HB[1]+hy)
  dy=min(desired,127-max(yy))
 elif name=='Run' and i in (3,7):
  desired=26-(HB[1]+hy)
  dy=max(grounded-3,min(grounded-1,desired))
 else:dy=grounded
 f=[[ZERO]*128 for _ in range(128)]
 for (x,y),p in pixels.items():
  assert 1<=x+dx<=126 and 1<=y+dy<=127,(name,i,x+dx,y+dy)
  f[y+dy][x+dx]=p
 ann={'neck_source':[nx,ny],'head_translation':[hx+dx,hy+dy],'frame_translation':[dx,dy],'whole_frame_vertical_delta_from_grounded':dy-grounded,'head_bbox':[HB[0]+hx+dx,HB[1]+hy+dy,HB[2]+hx+dx,HB[3]+hy+dy],'clipped_pixels':0}
 if name=='Land' and i==2:
  f=[r[:] for r in IDLE]
  ann.update({'head_translation':[0,0],'head_bbox':HB,'approved_idle_full_frame':True,'frame_translation':[0,0]})
 return f,ann

def metrics(f,ann):
 b=bbox(f);tx,ty=ann['head_translation'];colors={p[:3] for r in f for p in r if p[3]}
 return {'bbox':b,'head_top_y':ann['head_bbox'][1],'height':b[3]-b[1]+1,'center_x':(b[0]+b[2])/2,'color_count':len(colors),'alpha_values':sorted({p[3] for r in f for p in r}),'head_pixel_exact':all(f[y+ty][x+tx]==p for (x,y),p in HEAD.items()),'top_side_border_clear':not any(p[3] for p in f[0]) and not any(f[y][x][3] for y in range(128) for x in (0,127)),'bottom_border_clear':not any(p[3] for p in f[127]),**ann}

def main():
 generation=json.loads((ROOT/'Sources/generation.json').read_text(encoding='utf-8-sig'))
 all_frames={};attempts={'max_attempts_per_sheet':3,'generator':'built-in image_gen','sheets':{}};validation={'sheets':{}}
 for name,(cols,rows,n,fps,pitches,selected,ny) in SPECS.items():
  records=[]
  for a,k in enumerate(pitches,1):
   bodies,recovery=recover(name,cols,rows,n,k,a)
   pairs=[compose(b,name,i,ny) for i,b in enumerate(bodies)]
   fs=[f for f,ann in pairs];mm=[dict(frame=i,**metrics(f,ann)) for i,(f,ann) in enumerate(pairs)]
   sheet=[sum((f[y] for f in fs),[]) for y in range(128)]
   writepng(ROOT/'Sources'/f'{name}_attempt{a}_composite_3x.png',nearest(sheet,3))
   src=ROOT/'Sources'/f'{name}_attempt{a}.png'
   records.append({**generation[name][a-1],'source':str(src.relative_to(ROOT)),'sha256':hashlib.sha256(src.read_bytes()).hexdigest(),'selected':a==selected,'recovery':recovery,'frames':mm})
   if a==selected:all_frames[name]=fs;chosen=mm;chosen_sheet=sheet
  writepng(ROOT/'_1x'/f'Hero_{name}.png',chosen_sheet)
  delays=gif(ROOT/'Animations'/f'Hero_{name}_3x.gif',all_frames[name],fps,name!='Land')
  tops=[m['head_top_y'] for m in chosen]
  adjacency=[abs(tops[i]-tops[(i+1)%n]) for i in range(n if name!='Land' else n-1)]
  for i,m in enumerate(chosen):
   common=m['head_pixel_exact'] and m['top_side_border_clear'] and m['alpha_values']==[0,255] and m['color_count']<=24 and abs(m['center_x']-64)<=2
   floor=m['bbox'][3]==127 if name=='Land' or (name=='Run' and i not in (3,7)) else (124<=m['bbox'][3]<=126 if name=='Run' else 120<=m['bbox'][3]<=127)
   timing=(max(tops)-min(tops)<=3 and max(adjacency)<=2) if name=='Run' else ((35<=tops[0]<=37 and abs(tops[0]-tops[1])<=2 and abs(chosen[0]['center_x']-chosen[1]['center_x'])<=1.5) if name=='Fall' else (abs(tops[0]-40)<=2 and abs(tops[1]-32)<=2 and tops[2] in (26,27) and max(adjacency)<=10))
   m['checks']={'common':common,'floor':floor,'motion_height':timing};m['numeric_pass']=all(m['checks'].values())
  validation['sheets'][name]={'dimensions':[128*n,128],'frames':chosen,'attempt_count':len(pitches),'selected_attempt':selected,'head_vertical_range_px':max(tops)-min(tops),'max_adjacent_head_delta':max(adjacency),'color_count':len({p[:3] for r in chosen_sheet for p in r if p[3]}),'numeric_pass':all(m['numeric_pass'] for m in chosen),'production_ready':False,'visual_review':'pending','gif':{'fps_requested':fps,'delay_centiseconds':delays,'loop':name!='Land','last_frame_held':name=='Land'}}
  attempts['sheets'][name]={'selected_attempt':selected,'attempts':records}
 # Comparison rows: approved Idle, current game, then V5 frames.
 canvas=[[ZERO]*(128*10) for _ in range(128*3)]
 for row,(name,fs) in enumerate(all_frames.items()):
  current=readpng(ROOT.parent.parent/f'Assets/_Project/ResourcceEX/Sprites/Px32/Player/Hero_{name}.png')
  for col,f in enumerate([IDLE,[r[:128] for r in current]]+fs):
   for y,r in enumerate(f):canvas[row*128+y][col*128:(col+1)*128]=r
 writepng(ROOT/'Compare_3x.png',nearest(canvas,3))
 validation.update({'overall_pass':False,'head_source':'Assets/_Project/ResourcceEX/Sprites/Px32/Player/Hero_Idle.png frame 0','head_pixels':len(HEAD),'head_bbox':HB,'bottom_border_policy':'접지 127행을 우선하며 하단 투명 1px만 예외. 상단·좌우 1px 투명 필수.','recovery_policy':'V4 고정 블록 중심 샘플 복원. 원본 블록의 AA/불균일 때문에 근사 복원이며 프레임별 크기 맞춤·변형 없음.'})
 for filename,obj in [('validation.json',validation),('attempts.json',attempts)]:
  (ROOT/filename).write_text(json.dumps(obj,ensure_ascii=False,indent=2),encoding='utf-8')
 print({n:[(m['head_top_y'],m['height'],m['center_x'],m['bbox'][3]) for m in s['frames']] for n,s in validation['sheets'].items()})
if __name__=='__main__':main()
