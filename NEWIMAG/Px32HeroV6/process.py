"""Fixed-block recovery, binary alpha, palette cleanup, translation, approved head compositing.
Never resample/rotate/deform/draw character pixels. Only previews repeat pixels 3x.
"""
from pixel_ops import *
import copy
REF=ROOT.parent.parent/'Assets/_Project/ResourcceEX/Sprites/Px32/Player/Hero_Idle.png'
FULL=readpng(REF)
ANN=json.loads((ROOT/'annotations.json').read_text(encoding='utf-8'))
SPECS={'Idle':(3,8,6,3,8),'Run':(2,7,8,4,18)}
def inside(x,y,r):return r[0]<=x<=r[2] and r[1]<=y<=r[3]
def region(f,r):
 pts=[(x,y) for y,row in enumerate(f) for x,p in enumerate(row) if p[3] and inside(x,y,r)]
 if not pts:return None
 xs,ys=zip(*pts);return [min(xs),min(ys),max(xs),max(ys)]
def dims(b):return [b[2]-b[0]+1,b[3]-b[1]+1] if b else [0,0]
def annotations(name,i):
 if name=='Run':return ANN['Run'][i]
 a=copy.deepcopy(ANN['Idle']['base']);dy=ANN['Idle']['y_offsets'][i]
 for key in ('bag','shoulder'):a[key][1]+=dy;a[key][3]+=dy
 for key in ('neck','belt'):a[key][1]+=dy
 for leg in a['legs']:
  for p in leg:p[1]+=dy
 for b in a['shoes']:b[1]+=dy;b[3]+=dy
 return a
def anatomy(f,a,reference=False):
 body=[[ZERO if inside(x,y,a['bag']) or (reference and headmask(x,y)) else p for x,p in enumerate(r)] for y,r in enumerate(f)]
 sb=region(body,a['shoulder']);shoes=[region(body,r) for r in a['shoes']]
 m={'shoulder_width':dims(sb)[0],'shoulder_bbox':sb,'torso_height':a['belt'][1]-a['neck'][1],
 'leg_lengths':[round(sum(math.dist(p[i],p[i+1]) for i in (0,1)),2) for p in a['legs']],
 'shoe_sizes':[dims(b) for b in shoes],'shoe_bboxes':shoes,'bag_size':dims(region(f,a['bag'])),
 'bag_bbox':region(f,a['bag']),'body_without_bag_pixels':sum(bool(p[3]) for r in body for p in r),
 'landmarks':a,'measurement_uncertainty_px':2,'occluded_joints_estimated':not reference}
 return m,[[ (255,255,255,255) if p[3] else ZERO for p in r] for r in body]
def delta(m,r):
 return {'shoulder':m['shoulder_width']-r['shoulder_width'],'torso':m['torso_height']-r['torso_height'],
 'legs':[round(x-y,2) for x,y in zip(m['leg_lengths'],r['leg_lengths'])],
 'shoes':[[x-y for x,y in zip(a,b)] for a,b in zip(m['shoe_sizes'],r['shoe_sizes'])]}
def compose(b,name,i):
 xs=[x for x,p in enumerate(b[1]) if p[3]];nx=round((min(xs)+max(xs))/2)
 si=i if name=='Idle' else 0
 original=[r[si*128:(si+1)*128] for r in FULL]
 head={(x,y):p for y,r in enumerate(original) for x,p in enumerate(r) if p[3] and headmask(x,y)}
 air={3:-4,7:-3}.get(i,0) if name=='Run' else 0
 dx=77-nx;dy=128-len(b)+air;ht=dy+1-73
 f=[[ZERO]*128 for _ in range(128)]
 for y,r in enumerate(b):
  for x,p in enumerate(r):
   if p[3]:
    assert 0<=x+dx<128 and 0<=y+dy<128
    f[y+dy][x+dx]=p
 for (x,y),p in head.items():f[y+ht][x]=p
 bb=bbox(f);sx=round(64-(bb[0]+bb[2])/2)
 f=[[f[y][x-sx] if 0<=x-sx<128 else ZERO for x in range(128)] for y in range(128)]
 bb=bbox(f)
 return f,{'frame':i,'head_source_frame':si,'head_translation':[sx,ht],'body_translation':[dx+sx,dy],
 'neck_source':[nx,1],'neck_reference':[77,73],'airborne_translation_y':air,
 'head_top_y':min(y for x,y in head)+ht,'height':bb[3]-bb[1]+1,'bbox':bb,'center_x':(bb[0]+bb[2])/2,
 'head_pixel_count':len(head),'head_pixel_exact':all(f[y+ht][x+sx]==p for (x,y),p in head.items()),
 'alpha_values':sorted({p[3] for r in f for p in r}),'color_count':len({p[:3] for r in f for p in r if p[3]}),
 'top_side_border_clear':not any(p[3] for p in f[0]) and not any(f[y][x][3] for y in range(128) for x in (0,127)),
 'bottom_border_clear':not any(p[3] for p in f[127]),'clipped_pixels':0}
def main():
 ref,mask=anatomy(IDLE,ANN['reference'],True);writepng(ROOT/'Sources/approved_body_without_bag_mask.png',mask)
 generation=json.loads((ROOT/'Sources/generation.json').read_text(encoding='utf-8'))
 attempts={'generator':'built-in image_gen','max_attempts_per_sheet':3,'sheets':{}}
 v={'production_ready':False,'reference_sha256':hashlib.sha256(REF.read_bytes()).hexdigest(),'reference':ref,
 'measurement_method':ANN['method'],'measurement_limitations':['가방 사각 ROI를 보수적으로 제외. 가려진 몸 픽셀은 복원/측정 불가.','다리 관절은 수동 추정치이며 가림으로 인한 오차 존재. ±2px 제작 통과의 증명으로 사용하지 않음.','외곽선/가방 모양 일치는 시각검수 미달.'],
 'border_policy':'접지127행 우선, 접지 하단만1px투명 예외. 상단/좌우1px투명. 공중3/7은127기준 위로 이동.',
 'recovery_policy':'원본 블록 간격 Idle3=8px, Run2=7px 시트 전체 고정. 크기에 맞춘 프레임별 배율 없음. 불완전 생성 격자의 근사 복원. 미리보기만 픽셀 반복.',
 'sheets':{}}
 allfs={}
 review={'Idle':['가방 축소·반대쪽 허벅지 위치.','반대쪽 가방 위치·크기 축소.','왼쪽 허리 위치·두 팔 확보. 몸과 가방 크기는 미달인 최선 후보.'],
 'Run':['다리 교대 가능하나 생성 머리 포함·격자/크기 불일치.','다리 교대 확인,3번보다 몸 높이 일관성이 나아 선택. 크기/가방 미달.','가방은 커졌으나 공중 몸47px 대 접지54px로 변화 커 탈락.']}
 for name,(selected,pitch,n,cols,fps) in SPECS.items():
  bodies,recovery=recover(name,cols,2,n,pitch,selected);out=[];mm=[]
  for i,b in enumerate(bodies):
   f,m=compose(b,name,i);a,mask=anatomy(b,annotations(name,i));d=delta(a,ref)
   m.update(anatomy=a,size_delta=d,body_size_pass=abs(d['shoulder'])<=2 and abs(d['torso'])<=2 and all(abs(t)<=2 for t in d['legs']) and all(abs(t)<=2 for s in d['shoes'] for t in s),production_ready=False)
   out.append(f);mm.append(m);writepng(ROOT/'Sources'/f'{name}_frame{i}_body_without_bag_mask.png',mask)
  sheet=[sum((f[y] for f in out),[]) for y in range(128)]
  writepng(ROOT/'_1x'/f'Hero_{name}.png',sheet);writepng(ROOT/'Sources'/f'{name}_selected_3x.png',nearest(sheet,3))
  delays=gif(ROOT/'Animations'/f'Hero_{name}_3x.gif',out,fps,True)
  tops=[m['head_top_y'] for m in mm];adj=[abs(tops[i]-tops[(i+1)%n]) for i in range(n)]
  reasons=['몸 크기 ±2px 전체 조건 미달.','가방이 원본보다 작으며 이빨/외곽 모양 일치 미달.','외곽선 정확한1px 미보장.']
  if name=='Idle':reasons+=['머리 위 y26~27 및 키 변화±1px 미달.']
  else:reasons+=['0/4 검은 스타킹/맨다리 교대 확인. 밀기·공중 연결과 반대 팔 스윙 완전 통과는 보류.','가방이 벨트에 대해1~2px 늦게 따라오는 움직임 미확정.']
  v['sheets'][name]={'dimensions':[n*128,128],'attempt_count':3,'selected_attempt':selected,'frames':mm,'head_range':max(tops)-min(tops),
   'max_adjacent_head_delta_including_loop':max(adj),'height_motion_pass':(all(26<=t<=27 for t in tops) and max(tops)-min(tops)<=1) if name=='Idle' else (max(tops)-min(tops)<=3 and max(adj)<=2),
   'color_count':len({p[:3] for r in sheet for p in r if p[3]}),'production_ready':False,'failure_reasons':reasons,
   'bag_shape_consistent':False,'bag_lag_pass':False,'outline_1px_pass':False,'gif':{'requested_fps':fps,'delay_centiseconds':delays,'loop':True}}
  records=[]
  for record in generation[name]:
   src=ROOT/record['source'];raw=readpng(src)
   r=dict(record,sha256=hashlib.sha256(src.read_bytes()).hexdigest(),selected=record['attempt']==selected,source_dimensions=[len(raw[0]),len(raw)],review=review[name][record['attempt']-1])
   if r['selected']:r.update(recovery=recovery,frames=mm)
   records.append(r)
  attempts['sheets'][name]={'attempt_count':len(records),'selected_attempt':selected,'attempts':records};allfs[name]=out
 # Diagnostic grid only; no guide pixel is written to production sprites.
 canvas=[[(38,43,52,255) if x%32==0 or y%128%32==0 else (18,21,28,255) for x in range(128*9)] for y in range(256)]
 for row,name in enumerate(('Idle','Run')):
  for x in range(128*9):canvas[row*128+127][x]=(95,135,155,255)
  for col,f in enumerate([IDLE]+allfs[name]):
   for y,r in enumerate(f):
    for x,p in enumerate(r):
     if p[3]:canvas[row*128+y][col*128+x]=p
 writepng(ROOT/'Compare_3x.png',nearest(canvas,3))
 for filename,data in [('attempts.json',attempts),('validation.json',v)]:(ROOT/filename).write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
 print(json.dumps({'reference':ref,'sheets':{name:[{k:m[k] for k in ('frame','head_top_y','height','size_delta')} for m in s['frames']] for name,s in v['sheets'].items()}},ensure_ascii=False))
if __name__=='__main__':main()
