from pathlib import Path
import shutil,json,ast,importlib.util,sys
import numpy as np
from PIL import Image,ImageSequence
sys.dont_write_bytecode=True
M=Path('NEWIMAG/Px32HeroV9/Move').resolve(); R=M.parent/'Run';ROOT=M.parents[2];S=M/'Sources';B=S/'prev4'
if not B.exists():
 B.mkdir()
 for p in M.iterdir():
  if p.is_file():shutil.copy2(p,B/p.name)
 shutil.copytree(M/'Masks',B/'Masks')
 (B/'Sources').mkdir()
 for p in S.iterdir():
  if p.is_file() and p.name!='flipx_turn.py':shutil.copy2(p,B/'Sources'/p.name)
# Only pure helpers are imported, with no legacy output side effects.
tree=ast.parse((R/'Sources/face_lock.py').read_text(encoding='utf-8-sig'))
exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in ['read','save','dump','zoom','shift','indexed','gif','pixel','check','layer','add']],type_ignores=[]),'<pure helpers>','exec'))
idle=read(M.parent/'Idle/Hero_Idle_8.png');pal=np.unique(idle[idle[:,:,3]>0,:3],axis=0)
bag=read(ROOT/'NEWIMAG/Px32HeroRef/Bag/Bag_Hip_ThreeQuarter.png');chain=read(ROOT/'NEWIMAG/Px32HeroRef/Sources/R2_chain.png')[:6]
sheet=read(R/'Hero_Run_10.png');run=[sheet[:,128*i:128*(i+1)].copy() for i in range(10)]
f0=read(B/'Turn_F0.png');body=read(S/'Turn_F1_aligned.png');full=layer([54,94]);occ=np.zeros((128,128),bool)
# Selection of the existing foreground fist and dark thigh, not new sprite pixels.
for y0,y1,x0 in [(94,102,62),(102,105,63),(105,110,64)]:occ[y0:y1,x0:80]=body[y0:y1,x0:80,3]>0
visible=full.copy();visible[occ]=0
f1=add(body,visible);turn=[f0,f1,run[0].copy()];preview=[f0,f1,turn[2][:,::-1].copy()]
save(S/'FlipX_F1_bag_full.png',full);save(S/'FlipX_F1_bag_visible.png',visible);save(M/'Masks/FlipX_F1_occlusion.png',occ*255)
for i,a in enumerate(turn):save(M/f'Turn_F{i}.png',a)
save(M/'Hero_Turn_3.png',np.concatenate(turn,1))
pre={'turn':gif(M/'Hero_Turn_3_3x.gif',preview),'transition':gif(M/'Turn_3x.gif',run+preview+[a[:,::-1].copy() for a in run])}
boundary=[run[-1]]+preview+[run[0][:,::-1].copy(),run[1][:,::-1].copy()]
save(S/'Turn_boundary_3x.png',zoom(np.concatenate(boundary,1)))
save(S/'Turn_flip_boundary_3x.png',zoom(np.concatenate(boundary,1)))
save(S/'FlipX_waist_8x.png',zoom(np.concatenate([a[84:115,38:88] for a in boundary],1),8))
groups={'RunStart':[read(M/f'RunStart_F{i}.png') for i in range(3)],'RunStop':[read(M/f'RunStop_F{i}.png') for i in range(4)],'Turn':turn}
rows=[];waists=[]
for g in groups.values():
 row=np.zeros((128,512,4),np.uint8);row[:,:128*len(g)]=np.concatenate(g,1);rows.append(row)
 row=np.zeros((40,320,4),np.uint8)
 for i,a in enumerate(g):row[:,80*i:80*(i+1)]=a[76:116,20:100]
 waists.append(row)
save(M/'Sheet_3x.png',zoom(np.concatenate(rows,0)));save(M/'Waist_8x.png',zoom(np.concatenate(waists,0),8))
obsolete=S/'obsolete_L';obsolete.mkdir(exist_ok=True)
for p in list(M.glob('Run_L_*'))+list(M.glob('Turn_F2_L.png')):
 assert p.resolve().parent==M and not (obsolete/p.name).exists()
 shutil.move(str(p),str(obsolete/p.name))
spec=importlib.util.spec_from_file_location('asf',ROOT/'tools/sprite/asf-to-px32.py');mod=importlib.util.module_from_spec(spec);spec.loader.exec_module(mod)
asf={n:check(g,'Hero_'+n) for n,g in groups.items()};dump(S/'FlipX_ASF_check.json',asf)
metrics=[pixel(a) for a in turn]
for d in metrics:assert d['colors']<=24 and d['outside_idle_palette']==0 and d['alpha']==[0,255] and d['floor']==127
edit=(read(B/'Sources/Turn_F1_bag_layer.png')[:,:,3]>0)|(full[:,:,3]>0)
report={'date':'2026-11-15','backup':'Sources/prev4','storage':'F0 right, F1 front, F2 right; flipX starts at F2','bag_top_left_display':[[50,94],[54,94],[58,95]],'lag':'F1 y94 retains F0 attachment height; horizontal x54 is integer midpoint of display endpoints (50,58).','F1_occluded_pixels':int(((full[:,:,3]>0)&occ).sum()),'F0_unchanged':bool(np.array_equal(f0,read(B/'Turn_F0.png'))),'F1_outside_edit_mismatch':int((np.any(f1!=read(B/'Turn_F1.png'),2)&~edit).sum()),'F1_head_mismatch':int(np.any(f1[:84]!=read(B/'Turn_F1.png')[:84],2).sum()),'F2_to_Run0_mismatch':int(np.any(turn[2]!=run[0],2).sum()),'frames':metrics,'previews':pre,'asf':asf,'new_imagegen_calls':0,'visual_review':'pending'}
assert report['F1_outside_edit_mismatch']==report['F1_head_mismatch']==report['F2_to_Run0_mismatch']==0
v=json.loads((M/'validation.json').read_text(encoding='utf-8'));v['current_revision']='left_view_flipx';v['left_view_flipx']=report;dump(M/'validation.json',v)
a=json.loads((M/'attempts.json').read_text(encoding='utf-8'));a['left_view_flipx']={'new_imagegen_calls':0,'method':'approved integer translation, existing body mask occlusion, assembly and preview mirroring only'};dump(M/'attempts.json',a)
print(json.dumps(report,ensure_ascii=False,indent=2))
