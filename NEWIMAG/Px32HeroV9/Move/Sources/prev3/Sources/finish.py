from build import *

def normalized(name):
 a=read(S/(name+'_blocks.png')); y=np.where(a[:,:,3]>0)[0]; top=int(y.min()); bottom=int(y.max())
 # Integer translation of overlapping upper/lower regions only, no resize.
 upper=shift(a,0,27-top); lower=shift(a,0,127-bottom)
 out=upper.copy(); out[104:]=lower[104:]
 if name in ['RunStop_F2','Turn_F1']:
  ref=idle[0] if name=='RunStop_F2' else shift(read(ROOT/'NEWIMAG/Px32HeroRef/Turnaround/Hero_Front.png'),-5,0)
  m=np.zeros((128,128),bool);m[:76]=True
  if name=='Turn_F1':m[:84,85:]=True
  out[m]=ref[m]
  save('Masks/'+name+'_approved_head.png',m.astype('uint8')*255)
  save('Sources/'+name+'_approved_head_reference.png',ref)
 save('Sources/'+name+'_aligned.png',out)
 return out,dict(head_translation=[0,27-top],lower_translation=[0,127-bottom],composite_seam_y=104)

def addbag(a,pos,kind='ThreeQuarter'):
 l=bag_layer(pos,kind); out=a.copy();m=l[:,:,3]>0;out[m]=l[m];return out,l

def indexed(a):
 ids=np.zeros(a.shape[:2],dtype='uint8')
 for j,c in enumerate(PAL):ids[np.all(a[:,:,:3]==c,2)&(a[:,:,3]>0)]=j+1
 im=Image.fromarray(ids,'P');p=[0,0,0]+PAL.astype('uint8').flatten().tolist();im.putpalette(p+[0]*(768-len(p)));return im

def gif(name,frames):
 # Coalesce identical adjacent frames explicitly so decode checks follow GIF timings.
 merged=[];dur=[]
 for a in frames:
  if merged and np.array_equal(a,merged[-1]):dur[-1]+=50
  else:merged.append(a);dur.append(50)
 ims=[indexed(zoom(a)) for a in merged]
 ims[0].save(O/name,save_all=True,append_images=ims[1:],duration=dur,loop=0,transparency=0,disposal=2,optimize=False)
 mismatch=0
 for im,b in zip(ImageSequence.Iterator(Image.open(O/name)),merged):
  a=np.array(im.convert('RGBA'));b=zoom(b);m=(a[:,:,3]>0)|(b[:,:,3]>0);mismatch+=int(np.any(a[m]!=b[m],1).sum())
 return dict(logical_frames=len(frames),encoded_frames=len(merged),duration_ms=sum(dur),fps=20,visible_mismatch=mismatch)

def main():
 names=['RunStart_F1','RunStop_F1','RunStop_F2','Turn_F1'];body={};align={}
 for n in names:body[n],align[n]=normalized(n)
 start,sl=addbag(body[names[0]],[50,95])
 brake,bl=addbag(body[names[1]],[55,94])
 rebound,rl=addbag(body[names[2]],[57,93])
 turn,tl=addbag(body[names[3]],[54,94],'Front')
 groups={'RunStart':[idle[0],start,run[0]],'RunStop':[run[-1],brake,rebound,idle[0]],'Turn':[brake,turn,run[0]]}
 sources={'RunStart':['Idle0','RunStart_F1','Run0'],'RunStop':['Run9','RunStop_F1','RunStop_F2','Idle0'],'Turn':['RunStop_F1','Turn_F1','Run0']}
 positions={'RunStart':[[51,95],[50,95],[52,95]],'RunStop':[[52,94],[55,94],[57,93],[51,95]],'Turn':[[55,94],[54,94],[52,95]]}
 layers={'RunStart_F1':sl,'RunStop_F1':bl,'RunStop_F2':rl,'Turn_F1':tl}
 validation={};rows=[];previews={};asf={}
 spec=importlib.util.spec_from_file_location('asf',ROOT/'tools/sprite/asf-to-px32.py');mod=importlib.util.module_from_spec(spec);spec.loader.exec_module(mod)
 for group,fs in groups.items():
  sheet=np.concatenate(fs,1);save(f'Hero_{group}_{len(fs)}.png',sheet)
  row=np.zeros((128,512,4),np.uint8);row[:,:sheet.shape[1]]=sheet;rows.append(row)
  for i,a in enumerate(fs):
   n=f'{group}_F{i}';src=sources[group][i];save(n+'.png',a)
   isgen=src in body
   ref=body[src] if isgen else (idle[0] if src=='Idle0' else run[0] if src=='Run0' else run[-1])
   mask=layers[src][:,:,3]>0 if isgen else np.zeros((128,128),bool)
   save('Masks/'+n+'_composite.png',mask.astype('uint8')*255)
   # Entire generated pose is authorized; approved endpoint frames have empty edit masks.
   gm=np.full((128,128),255 if isgen else 0,dtype='uint8')
   if src in ['RunStop_F2','Turn_F1']:
    hm=np.array(Image.open(O/('Masks/'+src+'_approved_head.png')))>0;gm[hm]=0
   save('Masks/'+n+'_generation.png',gm)
   if isgen:save('Sources/'+n+'_bag_layer.png',layers[src])
   cols={tuple(c) for c in a[a[:,:,3]>0,:3]};ys=np.where(a[:,:,3]>0)[0]
   validation[n]=dict(source=src,generated_pose=isgen,colors=len(cols),outside_palette=len(cols-{tuple(c) for c in PAL}),alpha=list(map(int,np.unique(a[:,:,3]))),head_top=int(ys.min()),floor=int(ys.max()),bag_position=positions[group][i],outside_composite_mask_mismatch=int((np.any(a!=ref,2)&~mask).sum()),approved_endpoint_mismatch=None if isgen else int(np.any(a!=ref,2).sum()),alignment=align.get(src))
   if src in ['RunStop_F2','Turn_F1']:
    hr=read(S/(src+'_approved_head_reference.png'));validation[n]['approved_head_mismatch']=int((np.any(a!=hr,2)&hm).sum())
  fails,metrics=mod.check(Image.fromarray(sheet),128,len(fs),{tuple(c) for c in PAL},mod.rules_for('Hero_'+group))
  asf[group]=dict(failures=fails,metrics=metrics,stage=4,scale_frames=False)
  previews[group]=gif(f'Hero_{group}_{len(fs)}_3x.gif',fs)
 save('Sheet_3x.png',zoom(np.concatenate(rows,0)))
 allfs=sum(groups.values(),[])
 waistrows=[]
 for fs in groups.values():
  row=np.zeros((40,320,4),np.uint8)
  for i,a in enumerate(fs):row[:,i*80:(i+1)*80]=a[76:116,20:100]
  waistrows.append(row)
 save('Waist_8x.png',zoom(np.concatenate(waistrows,0),8))
 previews['Start']=gif('Start_3x.gif',idle[:4]+groups['RunStart']+run*2)
 previews['Stop']=gif('Stop_3x.gif',run+groups['RunStop']+idle)
 # Only previews use horizontal flipping. Runtime changes flipX on entering Turn F2.
 turnpreview=run+groups['Turn'][:2]+[groups['Turn'][2][:,::-1].copy()]+[a[:,::-1].copy() for a in run]
 previews['Turn_transition']=gif('Turn_3x.gif',turnpreview)
 save('Sources/Turn_flip_boundary_3x.png',zoom(np.concatenate([groups['Turn'][1],groups['Turn'][2][:,::-1],run[0][:,::-1]],1)))
 dump('Sources/ASF_check.json',asf)
 dump('validation.json',dict(status='pending visual review',frames=validation,previews=previews,asf=asf,endpoint_checks=dict(start_first=int(np.any(groups['RunStart'][0]!=idle[0],2).sum()),start_last=int(np.any(groups['RunStart'][-1]!=run[0],2).sum()),stop_last=int(np.any(groups['RunStop'][-1]!=idle[0],2).sum()),turn_flipped_last_to_run0=int(np.any(groups['Turn'][-1][:,::-1]!=run[0][:,::-1],2).sum())),approved_input_sha256={str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in [O.parent/'Idle/Hero_Idle_8.png',O.parent/'Run/Hero_Run_10.png']}))
 attempts={n:dict(used=1,limit=3,selected=1,resumed_existing=n in names[:2],generated='Sources/'+n+'_generated1.png',prompt=None if n=='RunStart_F1' else 'Sources/'+n+'_prompt1.txt') for n in names}
 dump('attempts.json',dict(total_including_previous=4,previous=2,this_session=2,unique_generated_frames=attempts,reused_frames=sources,note='RunStart F1 prior prompt missing on resume; original generated image/recovery preserved. Turn F0 reuses RunStop F1, no new generation.'))
 print(json.dumps(dict(frames=validation,asf=asf,previews=previews),ensure_ascii=False))

if __name__=='__main__':main()
