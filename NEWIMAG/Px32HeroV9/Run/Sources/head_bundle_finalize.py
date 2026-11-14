from pathlib import Path
from PIL import Image,ImageSequence
import numpy as np,json,ast,importlib.util
R=Path(__file__).resolve().parents[1];M=R.parent/'Move';ROOT=R.parents[2];B=R/'Sources/prev3';MB=M/'Sources/prev2'
# Reuse pure assembly/validation helpers, never execute the previous revision workflow.
tree=ast.parse((R/'Sources/face_lock.py').read_text(encoding='utf-8-sig'))
exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in ['read','save','dump','zoom','shift','indexed','gif','pixel','check']],type_ignores=[]),'<helpers>','exec'))
idle_sheet=read(R.parent/'Idle/Hero_Idle_8.png');idle=[idle_sheet[:,128*i:128*(i+1)] for i in range(8)];pal=np.unique(idle_sheet[idle_sheet[:,:,3]>0,:3],axis=0)
fs=[read(R/f'Sources/HeadBundle_{i:02}_final.png') for i in range(10)];oldsheet=read(B/'Hero_Run_10.png');old=[oldsheet[:,128*i:128*(i+1)] for i in range(10)]
names=['Run_Key0','Run_F1','Run_Key2','Run_F3','Run_F4','Run_F5','Run_I5-6','Run_F6','Run_F7','Run_I7-0'];ds=[0,0,-1,0,-2,-2,-2,-1,-2,-2];core=np.array(Image.open(R/'Masks/HeadBundle_core.png'))>0;ref=old[0];rv={};left=[]
for i,(a,n) in enumerate(zip(fs,names)):
 e=np.array(Image.open(R/f'Masks/HeadBundle_{i:02}_edit.png'))>0;hm=shift(core,dy=ds[i]);d=np.any(a!=old[i],2)
 rv[n]={**pixel(a),'offset':[0,ds[i]],'outside_mask_mismatch':int((d&~e).sum()),'head_template_mismatch':int(np.any(a[hm]!=shift(ref,dy=ds[i])[hm],1).sum()),'body_y82_mismatch':int(d[82:].sum()),'distant_tail_mismatch':int(d[:82,:51].sum())}
 assert rv[n]['outside_mask_mismatch']==rv[n]['head_template_mismatch']==rv[n]['body_y82_mismatch']==rv[n]['distant_tail_mismatch']==0
 save(R/(n+'.png'),a)
 l=read(MB/f'Run_L_F{i}.png');em=e[:,::-1];l[em]=a[:,::-1][em];left.append(l);save(M/f'Run_L_F{i}.png',l)
 body=read(MB/f'Sources/Run_L_{i:02}_body_R.png');body[e]=a[e];save(M/f'Sources/Run_L_{i:02}_body_R.png',body)
 save(M/f'Masks/HeadBundle_Run_L_{i:02}_edit.png',em*255)
start=[read(MB/f'RunStart_F{i}.png') for i in range(3)];stop=[read(MB/f'RunStop_F{i}.png') for i in range(4)];start[2]=fs[0];stop[0]=fs[-1]
turn=[read(M/'Sources/HeadBundle_Turn_final.png'),read(MB/'Turn_F1.png'),left[0]];save(M/'Turn_F2_L.png',left[0]);groups={'RunStart':start,'RunStop':stop,'Turn':turn}
sheet=np.concatenate(fs,1);eight=[fs[i] for i in [0,1,2,3,4,5,7,8]]
for nn,g in [('10',fs),('8',eight)]:
 s=np.concatenate(g,1);save(R/f'Hero_Run_{nn}.png',s);save(R/f'Hero_Run_{nn}_3x.png',zoom(s))
save(R/'Sheet_3x.png',zoom(np.concatenate([oldsheet,sheet],0)));save(M/'Run_L_10.png',np.concatenate(left,1));save(M/'Run_L_10_3x.png',zoom(np.concatenate(left,1)))
rows=[];mv={}
for n,g in groups.items():
 for i,a in enumerate(g):
  save(M/f'{n}_F{i}.png',a);before=read(MB/f'{n}_F{i}.png');e=np.zeros((128,128),bool)
  if n=='RunStart' and i==2:e=np.array(Image.open(R/'Masks/HeadBundle_00_edit.png'))>0
  if n=='RunStop' and i==0:e=np.array(Image.open(R/'Masks/HeadBundle_09_edit.png'))>0
  if n=='Turn' and i==0:e=np.array(Image.open(M/'Masks/HeadBundle_Turn_edit.png'))>0
  if n=='Turn' and i==2:e=np.array(Image.open(R/'Masks/HeadBundle_00_edit.png'))[:,::-1]>0
  mv[f'{n}_F{i}']={**pixel(a),'outside_mask_mismatch':int((np.any(a!=before,2)&~e).sum())};assert mv[f'{n}_F{i}']['outside_mask_mismatch']==0
 s=np.concatenate(g,1);save(M/f'Hero_{n}_{len(g)}.png',s);row=np.zeros((128,512,4),np.uint8);row[:,:s.shape[1]]=s;rows.append(row)
save(M/'Sheet_3x.png',zoom(np.concatenate(rows,0)));save(M/'Sources/Turn_boundary_3x.png',zoom(np.concatenate([fs[-1]]+turn+[left[0]],1)))
rpre={'run10':gif(R/'Hero_Run_10_3x.gif',fs,22,11),'run8':gif(R/'Hero_Run_8_3x.gif',eight,18,9),'compare':gif(R/'Compare_3x.gif',[np.concatenate([a,b],1) for a,b in zip(old,fs)],22,11)}
mpre={n:gif(M/f'Hero_{n}_{len(g)}_3x.gif',g) for n,g in groups.items()};mpre.update(Start=gif(M/'Start_3x.gif',idle[:4]+start+fs*2),Stop=gif(M/'Stop_3x.gif',fs+stop+idle),Turn=gif(M/'Turn_3x.gif',fs+turn+left),Run_L=gif(M/'Run_L_10_3x.gif',left,22,11))
lv={}
for i,a in enumerate(left):
 e=np.array(Image.open(M/f'Masks/HeadBundle_Run_L_{i:02}_edit.png'))>0;lv[str(i)]={**pixel(a),'outside_mask_mismatch':int((np.any(a!=read(MB/f'Run_L_F{i}.png'),2)&~e).sum())};assert lv[str(i)]['outside_mask_mismatch']==0
for v in list(rv.values())+list(mv.values())+list(lv.values()):assert v['colors']<=24 and v['outside_idle_palette']==0 and v['alpha']==[0,255] and v['floor']==127
spec=importlib.util.spec_from_file_location('asf',ROOT/'tools/sprite/asf-to-px32.py');mod=importlib.util.module_from_spec(spec);spec.loader.exec_module(mod)
ra=check(fs,'Hero_Run');ma={n:check(g,'Hero_'+n) for n,g in groups.items()};ma['Run_L']=check(left,'Hero_Run');dump(R/'Sources/HeadBundle_ASF_check.json',ra);dump(M/'Sources/HeadBundle_ASF_check.json',ma)
th=shift(core,-20,-4);turn_mismatch=int(np.any(turn[0][th]!=shift(ref,-20,-4)[th],1).sum());assert turn_mismatch==0
for folder,data in [(R,{'frames':rv,'previews':rpre,'asf':ra,'backup':'Sources/prev3','imagegen_calls':9}),(M,{'frames':mv,'left_frames':lv,'previews':mpre,'asf':ma,'backup':'Sources/prev2','imagegen_calls':1,'turn_template_mismatch':turn_mismatch,'endpoints_mismatch':{'start':int(np.any(start[2]!=fs[0],2).sum()),'stop':int(np.any(stop[0]!=fs[-1],2).sum()),'turn_left':int(np.any(turn[2]!=left[0],2).sum())}})]:
 data.update(date='2026-11-14',visual_review='pending',head_source='Run/Sources/prev3/Run_Key0.png',core_mask='Run/Masks/HeadBundle_core.png',offsets_y=ds)
 v=json.loads((folder/'validation.json').read_text(encoding='utf-8'));v['current_revision']='head_bundle_lock';v['head_bundle_lock']=data;dump(folder/'validation.json',v)
 v=json.loads((folder/'attempts.json').read_text(encoding='utf-8'));v['head_bundle_lock']={'built_in_imagegen':True,'calls':data['imagegen_calls'],'limit_per_frame':3,'per_frame':({n:0 if i==0 else 1 for i,n in enumerate(names)} if folder==R else {'Turn_F0':1,'other_frames':0}),'prompt':'Run/Sources/HeadBundle_prompt1.txt','method':'integer block recovery, approved palette mapping, binary alpha, integer shift, mirror and mask composite'};dump(folder/'attempts.json',v)
print(json.dumps({'run':rv,'asf_run':ra['failures'],'asf_move':{n:v['failures'] for n,v in ma.items()},'turn_template_mismatch':turn_mismatch},indent=2))
