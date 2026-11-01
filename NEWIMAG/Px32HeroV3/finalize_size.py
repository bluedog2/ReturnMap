"""Size revision: fixed-pitch grid recovery, palette cleanup, translations, approved head cutouts only."""
import process as p
import json,hashlib
R=p.ROOT
COUNTS={'Run':8,'JumpStart':2,'Fall':2,'Land':3,'Dash':4,'Hit':2,'Death':6}
SELECT={'Run':1,**{n:4 for n in COUNTS if n!='Run'}}
# Inclusive, visually annotated semantic rectangles on recovered 128px frames.
# Head excludes ponytail. Box excludes handle. Shoe includes cuff; None=occluded.
HEAD={
 'Run':[[58,39,106,82],[57,39,105,82],[58,38,105,81],[57,38,103,81],[58,37,103,79],[58,36,105,78],[58,37,106,80],[58,38,103,80]],
 'JumpStart':[[57,54,101,90],[58,36,100,72]],
 'Fall':[[45,43,88,78],[42,44,83,78]],
 'Land':[[54,66,98,102],[56,45,99,82],[56,35,98,73]],
 'Dash':[[61,44,105,82],[60,44,104,83],[64,46,109,84],[62,46,108,84]],
 'Hit':[[56,35,98,75],[56,45,97,85]],
 'Death':[[55,35,98,74],[53,49,97,87],[54,70,100,105],[68,71,110,108],[77,99,113,127],[81,98,117,127]]}
DEST={
 'Run':[[61,43],[60,43],[60,42],[59,42],[58,40],[60,39],[61,41],[59,41]],
 'JumpStart':[[58,51],[57,33]],'Fall':[[45,39],[41,39]],
 'Land':[[54,63],[56,43],[55,34]],
 'Dash':[[61,43],[60,44],[65,45],[63,45]]}
BOX={
 'Run':[[29,91,53,109],[29,93,51,110],[30,87,53,105],[30,91,54,109],[30,90,55,108],[31,88,55,107],[33,89,56,110],[28,87,52,105]],
 'JumpStart':[[33,94,54,111],[39,83,59,98]],
 'Fall':[[20,80,40,98],[25,88,46,105]],
 'Land':[[35,108,57,124],[36,94,58,109],[40,87,61,102]],
 'Dash':[[35,83,56,100],[34,83,54,99],[36,85,57,103],[35,85,56,103]],
 'Hit':[[43,90,65,106],[38,93,61,111]],
 'Death':[[40,87,61,104],[30,94,53,111],[31,108,54,125],[38,113,60,127],[40,114,62,127],[45,114,67,127]]}
SHOES={
 'Run':[[[38,111,57,127],None],[[54,111,73,127],[73,113,89,127]],[[26,105,41,121],[80,109,104,127]],[[53,113,73,127],None],[[49,107,67,121],[52,113,75,127]],[[49,105,65,121],[57,111,77,127]],[[41,112,62,127],[82,109,107,127]],[[23,108,43,124],[82,110,105,127]]],
 'JumpStart':[[[46,112,63,127],[73,112,90,127]],[[51,110,67,127],[71,97,85,114]]],
 'Fall':[[[66,108,88,127],[89,91,110,112]],[[65,108,87,127],[90,91,111,112]]],
 'Land':[[[46,120,67,127],[77,121,96,127]],[[46,110,64,127],[74,111,93,127]],[[47,110,66,127],[77,111,95,127]]],
 'Dash':[[[28,111,47,127],[62,109,80,125]],[[22,103,40,122],[84,113,107,127]],[[26,110,44,127],[61,108,81,123]],[[25,111,45,127],[84,114,107,127]]],
 'Hit':[[[50,110,72,127],[87,110,104,127]],[[45,110,64,127],[75,112,94,127]]],
 'Death':[[[47,110,67,127],[76,111,94,127]],[[38,112,59,127],[65,112,85,127]],[None,None],[[17,105,35,127],None],[[12,108,31,127],None],[[10,109,29,127],None]]}

def measure(f,roi):
 if roi is None:return None
 x0,y0,x1,y1=roi;b=p.bbox([row[x0:x1+1] for row in f[y0:y1+1]])
 if b is None:return None
 return [b[0]+x0,b[1]+y0,b[2]+x0,b[3]+y0]
def size(b):return [b[2]-b[0]+1,b[3]-b[1]+1] if b else None
def diff(v,ref):return [v[i]-ref[i] for i in range(2)] if v else None
def shifted(b,dx,dy):return [b[0]+dx,b[1]+dy,b[2]+dx,b[3]+dy] if b else None
def within(d):return d is not None and max(abs(x) for x in d)<=1

idle_files=[R/'_1x/Hero_Idle.png',R/'Player/Hero_Idle.png',R/'Animations/Hero_Idle_3x.gif']
hashes={x.relative_to(R).as_posix():hashlib.sha256(x.read_bytes()).hexdigest() for x in idle_files}
master=[r[:128] for r in p.readpng(idle_files[0])]
ref=json.loads((R/'Idle_size_reference.json').read_text())
assert hashes['_1x/Hero_Idle.png']==ref['sha256']
ref['verified_opaque_support']={k:measure(master,ref[k]) for k in ['head_bbox','box_bbox']}
ref['verified_shoe_bboxes']=[measure(master,b) for b in ref['shoe_bboxes']]
patch=[r[55:99] for r in master[35:75]]
review=json.loads((R/'size_review.json').read_text()) if (R/'size_review.json').exists() else json.loads((R/'attempts.json').read_text(encoding='utf-8-sig'))['size_revision']['records']
v=json.loads((R/'validation.json').read_text())
v['idle_reference']=ref;v['idle_hashes_before']=hashes
v['sheets']['Idle']['visual_status']='User approved reference; unchanged.'
v['sheets']['Idle']['user_approved']=True
v['sheets']['Idle']['production_ready']=True
v['sheets']['Idle']['numeric_pass']=True
v['sheets']['Idle']['frames'][0].update({'head_bbox':ref['head_bbox'],'head_size':ref['head_size'],'box_bbox':ref['box_bbox'],'box_size':ref['box_size'],'shoe_bboxes':ref['shoe_bboxes'],'shoe_sizes':ref['shoe_sizes']})
for n,count in COUNTS.items():
 selected=SELECT[n];fs,grid=p.recover(R/'Sources'/f'Hero_{n}_sizefix_attempt{selected}.png',1,1,count,k=4)
 assert fs is not None
 annotations=[]
 for i,f in enumerate(fs):
  before_head=measure(f,HEAD[n][i]);box=measure(f,BOX[n][i]);shoes=[measure(f,b) for b in SHOES[n][i]]
  cutout=n in DEST
  head=before_head
  if cutout:
   tx,ty=DEST[n][i]
   for y,row in enumerate(patch):
    for x,pixel in enumerate(row):
     if pixel[3]:f[ty+y][tx+x]=pixel
   # Preserve generated ponytail/neck pixels behind transparent cutout regions.
   # Remaining generated fringe is included in the measured composite head extent.
   head=[min(before_head[0],tx),min(before_head[1],ty),max(before_head[2],tx+43),max(before_head[3],ty+39)]
  b=p.bbox(f);dx=round(63.5-(b[0]+b[2])/2);dy=127-b[3]
  out=[[p.ZERO]*128 for _ in range(128)]
  for y,row in enumerate(f):
   for x,pixel in enumerate(row):
    if pixel[3]:
     assert 0<=x+dx<128 and 0<=y+dy<128
     out[y+dy][x+dx]=pixel
  fs[i]=out
  ann={'head_bbox':shifted(head,dx,dy),'head_size':size(head),'head_delta_idle':diff(size(head),ref['head_size']),
   'generated_head_bbox':before_head,'generated_head_size':size(before_head),'box_bbox':shifted(box,dx,dy),'box_size':size(box),'box_delta_idle':diff(size(box),ref['box_size']),
   'shoe_bboxes':[shifted(b,dx,dy) for b in shoes],'shoe_sizes':[size(b) for b in shoes],
   'shoe_deltas_idle':[diff(size(b),ref['shoe_sizes'][j]) for j,b in enumerate(shoes)],
   'head_cutout':cutout,'final_translation':[dx,dy],
   'measurement_note':'Opaque approved head pixels composited without scaling; generated fringe remains and is included in composite bbox. Semantic bounds are manually annotated visible-support estimates; rotated/occluded shoes cannot certify +/-1px. Null means occluded.'}
  ann['head_size_pass']=within(ann['head_delta_idle']);ann['box_size_pass']=within(ann['box_delta_idle']);ann['shoe_size_pass']=all(within(d) for d in ann['shoe_deltas_idle'])
  if cutout:
   tx,ty=DEST[n][i];tx+=dx;ty+=dy
   ann['head_cutout_bbox']=[tx,ty,tx+43,ty+39]
   ann['head_cutout_size']=[44,40]
   ann['head_cutout_pixel_exact']=all(out[ty+y][tx+x]==pixel for y,row in enumerate(patch) for x,pixel in enumerate(row) if pixel[3])
   assert ann['head_cutout_pixel_exact']
  annotations.append(ann)
 m=p.metrics(fs,n)
 # Drift is assessed within equivalent posture groups; crouch and death exceptions explicit.
 group=[i for i in range(count) if not(n=='JumpStart' and i==0 or n=='Land' and i<2)]
 groupavg=sum(m['frames'][i]['height'] for i in group)/len(group)
 for i,(f,ann) in enumerate(zip(m['frames'],annotations)):
  f.update(ann);f['height_delta_idle']=f['height']-101
  exception='death pose' if n=='Death' else 'crouch/recovery' if n=='JumpStart' and i==0 or n=='Land' and i<2 else 'dash lean' if n=='Dash' else None
  f['height_exception']=exception
  f['standing_height_pass']=True if exception else abs(f['height_delta_idle'])<=3
  f['drift_height_deviation']=None if n=='Death' or i not in group else round(f['height']-groupavg,3)
  f['drift_pass']=n=='Death' or abs(f['center_deviation'])<=2 and (i not in group or abs(f['height']-groupavg)<=2)
  f['strict_size_pass']=f['head_size_pass'] and f['box_size_pass'] and f['shoe_size_pass'] and f['standing_height_pass']
 m['import_geometry_pass']=all(f['forbidden_edge_pixels']==0 and f['bottom_row']==127 and f['drift_pass'] for f in m['frames'])
 m['strict_size_pass']=all(f['strict_size_pass'] for f in m['frames'])
 m['numeric_pass']=m['strict_size_pass'] and m['import_geometry_pass']
 m.update({'selected_size_attempt':selected,'size_attempt_count':4,'generation_grid':grid,'production_ready':False,'head_cutout_used':n in DEST,'dimensions_1x':[128*count,128],'dimensions_4x':[512*count,512],'nonbinary_alpha_pixels':0,'visual_status':'Best available within 4 size attempts. Strict size rules are not fully met; inspect per-frame failures.'})
 sheet=[sum((f[y] for f in fs),[]) for y in range(128)]
 p.writepng(R/'_1x'/f'Hero_{n}.png',sheet);p.writepng(R/'Player'/f'Hero_{n}.png',p.nearest(sheet,4));p.gif(R/'Animations'/f'Hero_{n}_3x.gif',fs)
 m['nearest_4x_exact']=p.readpng(R/'Player'/f'Hero_{n}.png')==p.nearest(sheet,4)
 v['sheets'][n]=m
 print(n,[f['height'] for f in m['frames']],[(f['head_delta_idle'],f['box_delta_idle']) for f in m['frames']],flush=True)
v['overall_pass']=all(v['sheets'][n]['numeric_pass'] for n in COUNTS)
v['rules_note']='No silhouette resizing. Fixed integer 4x block-center recovery, palette mapping, alpha 128 threshold, isolated-singleton cleanup, translation, and approved Idle face/bangs pixel cutout only. Source grid is approximate. Size failures retained honestly after four generation attempts.'
v['idle_hashes_after']={x.relative_to(R).as_posix():hashlib.sha256(x.read_bytes()).hexdigest() for x in idle_files}
v['idle_unchanged']=hashes==v['idle_hashes_after'];assert v['idle_unchanged']
(R/'validation.json').write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf-8')
a=json.loads((R/'attempts.json').read_text(encoding='utf-8-sig'))
prompts={}
for fn in ['size_revision_new.json','size_attempts3.json','size_attempts4.json']:
 if not (R/fn).exists():continue
 for rec in json.loads((R/fn).read_text()):prompts[(rec['name'],rec['attempt'])]=rec
for rec in review:
 rec.update({k:vv for k,vv in prompts.get((rec['name'],rec['attempt']),{}).items() if k in ['path','prompt']})
 rec['selected']=SELECT[rec['name']]==rec['attempt']
 rec['selection_note']='Fixed-pitch candidate fits cell. Run attempt1 is closest to101px; other sheets attempt4 restores template scale; alternatives oversized or farther from master.'
a['size_revision']={'date':'2026-11-02','tool':'built-in image_gen','max_attempts_per_sheet':4,'prior_attempts_included':2,'additional_attempts':2,'reference':ref,'records':review,'selected':SELECT,'head_cutout_sheets':list(DEST),'overall_pass':v['overall_pass']}
(R/'attempts.json').write_text(json.dumps(a,ensure_ascii=False,indent=2),encoding='utf-8')
(R/'Idle_size_reference.json').write_text(json.dumps(ref,indent=2),encoding='utf-8')
