from pathlib import Path
from PIL import Image, ImageSequence
import numpy as np, json, importlib.util, hashlib
R=Path(__file__).resolve().parents[1]; M=R.parent/'Move'; ROOT=R.parents[2]; B=R/'Sources/prev2'; MB=M/'Sources/prev1'
def read(p):return np.array(Image.open(p).convert('RGBA'))
def save(p,a):p.parent.mkdir(exist_ok=True,parents=True);Image.fromarray(a.astype('uint8')).save(p)
def dump(p,d):p.write_text(json.dumps(d,ensure_ascii=False,indent=2),encoding='utf-8')
def zoom(a,n=3):return a.repeat(n,0).repeat(n,1)
def shift(a,dx=0,dy=0):
 b=np.zeros_like(a);h,w=a.shape[:2];x0=max(0,dx);x1=min(w,w+dx);y0=max(0,dy);y1=min(h,h+dy);b[y0:y1,x0:x1]=a[y0-dy:y1-dy,x0-dx:x1-dx];return b
oldsheet=read(B/'Hero_Run_10.png');old=[oldsheet[:,i*128:(i+1)*128].copy() for i in range(10)];ref=old[0]
mask=np.zeros((128,128),bool)
for y0,y1,x0,x1 in [(39,48,78,111),(48,53,70,111),(53,59,67,111),(59,64,63,111),(64,72,64,111),(72,76,69,111),(76,78,74,102),(78,79,77,98),(79,80,79,97),(80,81,80,95),(81,82,82,94)]:mask[y0:y1,x0:x1]=1
save(R/'Masks/FaceLock_core.png',mask*255)
def lock(a,dx=0,dy=0,base_mask=None):
 sm=shift(mask,dx,dy);ed=(mask if base_mask is None else base_mask)|sm;t=shift(ref,dx,dy);o=a.copy();o[ed]=t[ed];return o,ed,sm
names=['Run_Key0','Run_F1','Run_Key2','Run_F3','Run_F4','Run_F5','Run_I5-6','Run_F6','Run_F7','Run_I7-0'];ids=['F0','F1','F2','F3','F4','F5','I5','F6','F7','I7'];fs=[];rv={};offsets=[]
for i,(a,n) in enumerate(zip(old,names)):
 scores=[int(np.any(a[83+d:94+d,68:86]!=ref[83:94,68:86],2).sum()) for d in range(-2,3)];dy=int(np.argmin(scores))-2;offsets.append(dy)
 o,e,hm=lock(a,dy=dy);fs.append(o);save(R/(n+'.png'),o);save(R/f'Masks/FaceLock_{i:02}_edit.png',e*255);save(R/f'Sources/FaceLock_{i:02}_head.png',np.where(hm[:,:,None],o,0))
 rv[n]={'offset':[0,dy],'shoulder_registration':{'reference_region':[68,83,86,94],'dy_candidates':[-2,-1,0,1,2],'rgba_mismatch_scores':scores},'outside_mask_mismatch':int((np.any(o!=a,2)&~e).sum()),'head_template_mismatch':int(np.any(o[hm]!=shift(ref,0,dy)[hm],1).sum()),'below_head_y82_mismatch':int(np.any(o[82:]!=a[82:],2).sum())}
# Recover bagless intermediate frames from the exact existing saved mask composite.
bodies=[]
for i,k in enumerate(ids):
 if k in ['F0','F2']:base=read(R/f'Sources/Key{k[1:]}_bagless.png')
 elif k.startswith('I'):
  j=k[1:];base=read(R/f'Sources/F{j}_bagless.png');gm=np.array(Image.open(R/f'Masks/F{j}_generation.png'))>0;blocks=read(R/f'Sources/I{j}_blocks.png');base[gm]=blocks[gm]
 else:base=read(R/f'Sources/{k}_bagless.png')
 # Preserve all approved run pixels except where the existing bag actually covered them.
 layer=read(R/f'Sources/{k}_bag_layer.png');bagmask=layer[:,:,3]>0;body=fs[i].copy();body[bagmask]=base[bagmask];bodies.append(body);save(M/f'Sources/Run_L_{i:02}_body_R.png',body)
 save(M/f'Masks/Run_L_{i:02}_remove_bag.png',bagmask*255)
# Approved bag is asymmetric in pixel shading; retain original orientation for stable screen-side appearance.
bag=read(ROOT/'NEWIMAG/Px32HeroRef/Bag/Bag_Hip_ThreeQuarter.png');chain=read(ROOT/'NEWIMAG/Px32HeroRef/Sources/R2_chain.png')[:6]
def layer(pos):
 a=np.zeros((128,128,4),np.uint8)
 for v,(x,y) in [(chain,(pos[0]+5,pos[1]-6)),(bag,pos)]:
  h,w=v.shape[:2];reg=a[y:y+h,x:x+w];m=v[:,:,3]>0;reg[m]=v[m]
 return a

def add(a,l):o=a.copy();m=l[:,:,3]>0;o[m]=l[m];return o
hips=[[63,96],[61,93],[61,93],[62,95],[63,94],[62,92],[62,93],[63,94],[63,96],[63,96]]
left=[];positions=[];lv={}
for i,body in enumerate(bodies):
 prev=hips[(i-1)%10];pos=[127-prev[0]-14,prev[1]-2];positions.append(pos);l=layer(pos);mir=body[:,::-1].copy();o=add(mir,l);left.append(o)
 save(M/f'Run_L_F{i}.png',o);save(M/f'Sources/Run_L_{i:02}_bag_layer.png',l);save(M/f'Masks/Run_L_{i:02}_composite.png',(l[:,:,3]>0)*255)
 lv[f'Run_L_F{i}']={'previous_frame':(i-1)%10,'previous_hip_R':prev,'previous_hip_L':[127-prev[0],prev[1]],'offset':[-14,-2],'bag_position':pos,'outside_bag_mirror_mismatch':int((np.any(o!=mir,2)&(l[:,:,3]==0)).sum()),'bag_layer_mismatch':int(np.any(o[l[:,:,3]>0]!=l[l[:,:,3]>0],1).sum())}
start=[read(MB/f'RunStart_F{i}.png') for i in range(3)];stop=[read(MB/f'RunStop_F{i}.png') for i in range(4)];start[-1]=fs[0];stop[0]=fs[-1]
# Brake neck center moves from reference (85,79) to (65,75).
brakebody=read(M/'Sources/RunStop_F1_aligned.png');tm=shift(mask,-20,-4);tb,te,th=lock(brakebody,-20,-4,tm)
# Current face occupies x43..85, fully covered by translated selection; preserve flying ponytail.
save(M/'Masks/Turn_F0_head.png',te*255);save(M/'Sources/Turn_F0_headlocked_body.png',tb)
turnpos=[[50,94],[48,94],positions[0]]
turn=[add(tb,layer(turnpos[0])),add(read(M/'Sources/Turn_F1_aligned.png'),layer(turnpos[1])),left[0]]
for i in range(2):save(M/f'Sources/Turn_F{i}_bag_layer.png',layer(turnpos[i]))
save(M/'Turn_F2_L.png',turn[2]);save(M/'Sources/Turn_boundary_3x.png',zoom(np.concatenate([fs[-1]]+turn+[left[0]],1)))
# Image assembly and previews, pixel repetition only.
sheet=np.concatenate(fs,1);save(R/'Hero_Run_10.png',sheet);save(R/'Hero_Run_10_3x.png',zoom(sheet));save(R/'Sheet_3x.png',zoom(np.concatenate([oldsheet,sheet],0)))
eight=[fs[i] for i in [0,1,2,3,4,5,7,8]];save(R/'Hero_Run_8.png',np.concatenate(eight,1));save(R/'Hero_Run_8_3x.png',zoom(np.concatenate(eight,1)))
for nn,group in [('8',eight),('10',fs)]:save(R/f'Waist_{nn}_8x.png',zoom(np.concatenate([a[74:112,20:100] for a in group],1),8))
# Transparent equal-height cells share y20..85; no face alignment in diagnostic before/after strip.
faces=np.concatenate([np.concatenate([a[20:86,48:112] for a in group],1) for group in [old,fs]],0)
save(R/'FaceLock_4x.png',zoom(faces,4));save(R/'FaceLock_3x.png',zoom(faces,3))
save(R/'Sources/FaceLock_review_3x.png',zoom(np.concatenate([np.concatenate(fs[:5],1),np.concatenate(fs[5:],1)],0)))
save(M/'Run_L_10.png',np.concatenate(left,1));save(M/'Run_L_10_3x.png',zoom(np.concatenate(left,1)))
groups={'RunStart':start,'RunStop':stop,'Turn':turn};rows=[]
for n,group in groups.items():
 for i,a in enumerate(group):save(M/f'{n}_F{i}.png',a)
 save(M/f'Hero_{n}_{len(group)}.png',np.concatenate(group,1));row=np.zeros((128,512,4),np.uint8);row[:,:128*len(group)]=np.concatenate(group,1);rows.append(row)
save(M/'Sheet_3x.png',zoom(np.concatenate(rows,0)))
waist=[]
for group in groups.values():
 row=np.zeros((40,320,4),np.uint8)
 for i,a in enumerate(group):row[:,i*80:(i+1)*80]=a[76:116,20:100]
 waist.append(row)
save(M/'Waist_8x.png',zoom(np.concatenate(waist,0),8))
idle_sheet=read(R.parent/'Idle/Hero_Idle_8.png');idle=[idle_sheet[:,i*128:(i+1)*128] for i in range(8)];pal=np.unique(idle_sheet[idle_sheet[:,:,3]>0,:3],axis=0)
def indexed(a):
 ids=np.zeros(a.shape[:2],np.uint8)
 for j,c in enumerate(pal):ids[np.all(a[:,:,:3]==c,2)&(a[:,:,3]>0)]=j+1
 im=Image.fromarray(ids);im=im.convert('P');p=[0,0,0]+pal.flatten().tolist();im.putpalette(p+[0]*(768-len(p)));return im

def gif(path,arrays,fps=20,reps=1):
 seq=arrays*reps;ds=[round((i+1)*100/fps)*10-round(i*100/fps)*10 for i in range(len(seq))];merged=[];dur=[]
 for a,d in zip(seq,ds):
  if merged and np.array_equal(a,merged[-1]):dur[-1]+=d
  else:merged.append(a);dur.append(d)
 ims=[indexed(zoom(a)) for a in merged];ims[0].save(path,save_all=True,append_images=ims[1:],duration=dur,loop=0,transparency=0,disposal=2,optimize=False)
 decoded=[im.copy() for im in ImageSequence.Iterator(Image.open(path))];mismatch=0
 for im,b in zip(decoded,merged):
  a=np.array(im.convert('RGBA'));b=zoom(b);u=(a[:,:,3]>0)|(b[:,:,3]>0);mismatch+=int(np.any(a[u]!=b[u],1).sum())
 assert len(decoded)==len(merged) and mismatch==0
 return {'logical_frames':len(seq),'decoded_frames':len(decoded),'duration_ms':sum(dur),'fps':fps,'visible_mismatch':mismatch}
rpre={'run10':gif(R/'Hero_Run_10_3x.gif',fs,22,11),'run8':gif(R/'Hero_Run_8_3x.gif',eight,18,9),'compare':gif(R/'Compare_3x.gif',[np.concatenate([a,b],1) for a,b in zip(old,fs)],22,11)}
mpre={n:gif(M/f'Hero_{n}_{len(group)}_3x.gif',group) for n,group in groups.items()}
mpre.update(Start=gif(M/'Start_3x.gif',idle[:4]+start+fs*2),Stop=gif(M/'Stop_3x.gif',fs+stop+idle),Turn_transition=gif(M/'Turn_3x.gif',fs+turn+left),Run_L=gif(M/'Run_L_10_3x.gif',left,22,11))
def pixel(a):
 cols={tuple(c) for c in a[a[:,:,3]>0,:3]};ys=np.where(a[:,:,3]>0)[0];return {'colors':len(cols),'outside_idle_palette':len(cols-{tuple(c) for c in pal}),'alpha':list(map(int,np.unique(a[:,:,3]))),'floor':int(ys.max())}
for a,n in zip(fs,names):rv[n].update(pixel(a));assert rv[n]['outside_mask_mismatch']==rv[n]['head_template_mismatch']==rv[n]['below_head_y82_mismatch']==0
mv={}
for n,group in groups.items():
 for i,a in enumerate(group):mv[f'{n}_F{i}']=pixel(a)
for i,a in enumerate(left):lv[f'Run_L_F{i}'].update(pixel(a))
for p in list(rv.values())+list(mv.values())+list(lv.values()):assert p['colors']<=24 and p['outside_idle_palette']==0 and p['alpha']==[0,255] and p['floor']==127
spec=importlib.util.spec_from_file_location('asf',ROOT/'tools/sprite/asf-to-px32.py');mod=importlib.util.module_from_spec(spec);spec.loader.exec_module(mod)
def check(group,name):
 failures,metrics=mod.check(Image.fromarray(np.concatenate(group,1)),128,len(group),{tuple(c) for c in pal},mod.rules_for(name));return {'stage':4,'scale_frames':False,'failures':failures,'metrics':metrics}
ra=check(fs,'Hero_Run');ma={n:check(group,'Hero_'+n) for n,group in groups.items()};ma['Run_L']=check(left,'Hero_Run')
dump(R/'Sources/FaceLock_ASF_check.json',ra);dump(M/'Sources/ScreenSide_ASF_check.json',ma)
rd={'date':'2026-11-13','backup':'Sources/prev2','frames':rv,'previews':rpre,'asf':ra,'head_source':'prev2/Run_Key0.png','core_mask':'Masks/FaceLock_core.png','offsets_y':offsets,'new_imagegen_calls':0,'visual_review':'pending'}
md={'date':'2026-11-13','backup':'Sources/prev1','frames':mv,'left_frames':lv,'turn_bag_positions':turnpos,'turn_max_axis_delta':int(np.abs(np.diff(np.array(turnpos),axis=0)).max()),'turn_max_euclidean_delta':float(np.linalg.norm(np.diff(np.array(turnpos),axis=0),axis=1).max()),'bag_symmetry_rgba_mismatch':int(np.any(bag!=bag[:,::-1],2).sum()),'bag_mirrored':False,'turn_head_offset':[-20,-4],'turn_head_template_mismatch':int(np.any(tb[th]!=shift(ref,-20,-4)[th],1).sum()),'endpoint_mismatch':{'start_to_run0':int(np.any(start[-1]!=fs[0],2).sum()),'run9_to_stop0':int(np.any(stop[0]!=fs[-1],2).sum()),'turn2_to_left_run0':int(np.any(turn[-1]!=left[0],2).sum())},'previews':mpre,'asf':ma,'new_imagegen_calls':0,'visual_review':'pending'}
for folder,key,data in [(R,'face_lock',rd),(M,'screen_side_bag',md)]:
 v=json.loads((folder/'validation.json').read_text(encoding='utf-8'));v['current_revision']=key;v[key]=data;dump(folder/'validation.json',v)
 a=json.loads((folder/'attempts.json').read_text(encoding='utf-8'));a[key]={'new_imagegen_calls':0,'method':'approved pixel mask compositing, integer translation, body mirror; no drawing','limit_per_frame':3};dump(folder/'attempts.json',a)
print(json.dumps({'offsets':offsets,'turn_bag':turnpos,'left_bag':positions,'asf_run':ra['failures'],'asf_move':{k:v['failures'] for k,v in ma.items()}},indent=2))

