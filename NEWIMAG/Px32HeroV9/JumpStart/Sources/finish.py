from build import *
from PIL import ImageSequence
import importlib.util
def indexed(a):
 idx=np.zeros(a.shape[:2],dtype='uint8')
 for j,c in enumerate(PAL):idx[np.all(a[:,:,:3]==c,2)&(a[:,:,3]>0)]=j+1
 im=Image.fromarray(idx,'P');cols=[0,0,0]+PAL.astype('uint8').flatten().tolist();im.putpalette(cols+[0]*(768-len(cols)));im.info['transparency']=0;return im
def gif(name,arrays,durations):
 ims=[indexed(zoom(a,3)) for a in arrays];ims[0].save(O/name,save_all=True,append_images=ims[1:],duration=durations,loop=0,transparency=0,disposal=2,optimize=False)
 mismatch=0;actual=[]
 for j,im in enumerate(ImageSequence.Iterator(Image.open(O/name))):
  a=np.array(im.convert('RGBA'));b=zoom(arrays[j],3);u=(a[:,:,3]>0)|(b[:,:,3]>0);mismatch+=int(np.any(a[u]!=b[u],1).sum());actual.append(im.info['duration'])
 assert len(actual)==len(arrays) and sum(actual)==sum(durations) and mismatch==0
 return dict(frames=len(actual),total_ms=sum(actual),visible_mismatch=mismatch,durations_ms=actual)
def main():
 data=json.loads((S/'measurements.json').read_text(encoding='utf-8'));fs=[read(O/f'JumpStart_F{i}.png') for i in range(3)]
 base=read(S/'F0_bagless.png');b,r=recover(read(S/'I0_generated2.png'),base,50,91);m=np.array(Image.open(O/'Masks/F0_generation.png'))>0
 a=base.copy();a[m]=b[m];pos=[(positions[0][j]+positions[1][j]+1)//2 for j in range(2)];l=layer(pos);save('Sources/I0_bag_layer_full.png',l);occlusion=np.zeros((128,128),dtype=bool);occlusion[:91]=True;l[occlusion]=0;save('Masks/I0_occlusion.png',occlusion*255);bm=l[:,:,3]>0;a[bm]=l[bm];edit=m|bm|(read(S/'F0_bag_layer.png')[:,:,3]>0)
 save('JumpStart_I0-1.png',a);save('Sources/I0_blocks.png',b);save('Sources/I0_review_3x.png',zoom(a,3));save('Sources/I0_bag_layer.png',l);save('Masks/I0_generation.png',m*255);save('Masks/I0_edit.png',edit*255)
 four=[fs[0],a,fs[1],fs[2]];order=['F0','I0','F1','F2'];sheet=np.concatenate(four,1);save('Hero_JumpStart_4.png',sheet)
 for n,group in [(3,fs),(4,four)]:
  sh=np.concatenate(group,1);save(f'Hero_JumpStart_{n}_3x.png',zoom(sh,3));save(f'Waist_{n}_8x.png',zoom(np.concatenate([z[65:121,28:94] for z in group],1),8))
 top=np.zeros_like(sheet);top[:,:384]=np.concatenate(fs,1);save('Sheet_3x.png',zoom(np.concatenate([top,sheet],0),3));save('Sources/Final_review_3x.png',zoom(sheet,3))
 spec=importlib.util.spec_from_file_location('asf',ROOT/'tools/sprite/asf-to-px32.py');mod=importlib.util.module_from_spec(spec);spec.loader.exec_module(mod);checks={}
 for n,group in [(3,fs),(4,four)]:
  fails,metrics=mod.check(Image.fromarray(np.concatenate(group,1)),128,n,{tuple(c) for c in PAL},mod.rules_for('Hero_JumpStart'));checks[str(n)]=dict(failures=fails,metrics=metrics)
 old=mod.sheet_spec('Hero_JumpStart');warning=f'dimensions: Hero_JumpStart 규격 {old[1]}장 대 새 기준 4장 (예상 경고)'
 dump('Sources/ASF_check.json',dict(stage=4,scale_frames=False,place=False,checks=checks,expected_spec_warning=warning))
 previews={};previews['3']=gif('Hero_JumpStart_3_3x.gif',fs,[70,60,70]);previews['4']=gif('Hero_JumpStart_4_3x.gif',four,[50]*4)
 orig=[read(S/f'Original_{i}.png') for i in range(3)];arr=[];dur=[];last=None
 for t in range(20):
  key=(min(2,int(t*.15)),min(3,t//5))
  if key==last:dur[-1]+=10;continue
  arr.append(np.concatenate([orig[key[0]],fs[key[0]],four[key[1]]],1));dur.append(10);last=key
 previews['compare']=gif('Compare_3x.gif',arr,dur)
 run=read(ROOT/'NEWIMAG/Px32HeroV9/Run/Hero_Run_10.png')[:,-128:];fall=read(ROOT/'Assets/_Project/ResourcceEX/Sprites/Px32/Player/Hero_Fall.png')[:,:128];save('Sources/Transition_Fall_original.png',fall)
 previews['transition']=gif('Transition_3x.gif',[run]+four+[fall],[50]*6);save('Sources/Transition_3x.png',zoom(np.concatenate([run]+four+[fall],1),3))
 meta=data['frames'];meta['I0']=dict(recovery=r,bag_position=pos,outside_mask_mismatch=int((np.any(a!=fs[0],2)&~edit).sum()),reference='F0',interpolation='rear arm, sleeve and jacket only; head and legs retained')
 for name,z in zip(order,four):
  j=0 if name=='I0' else int(name[1]);ref=orig[j];entry=meta[name];gen=np.array(Image.open(O/f'Masks/{name}_generation.png'))>0;lay=read(S/f'{name}_bag_layer.png');u=lay[:,:,3]>0
  ys=np.where(z[:,:,3]>0)[0];ys0=np.where(ref[:,:,3]>0)[0];colors={tuple(c) for c in z[z[:,:,3]>0,:3]};toprow=91 if j==0 else 65
  entry.update(colors=len(colors),outside_palette=len(colors-{tuple(c) for c in PAL}),alpha=list(map(int,np.unique(z[:,:,3]))),head_top=int(ys.min()),original_head_top=int(ys0.min()),bottom=int(ys.max()),original_bottom=int(ys0.max()),bottom_row_mismatch=int(np.any(z[ys0.max()]!=ref[ys0.max()],1).sum()),head_mismatch=int(np.any(z[:toprow]!=ref[:toprow],2).sum()),front_arm_mismatch=int(np.any(z[100 if j==0 else 78:115 if j==0 else 89,80:92]!=ref[100 if j==0 else 78:115 if j==0 else 89,80:92],2).sum()),bag_layer_mismatch=int(np.any(z[u]!=lay[u],1).sum()))
  assert entry['outside_mask_mismatch']==0 and entry['colors']<=24 and entry['outside_palette']==0 and entry['alpha']==[0,255] and entry['head_top']==entry['original_head_top'] and entry['bottom']==entry['original_bottom'] and entry['bottom_row_mismatch']==0
 inputs=['Assets/_Project/ResourcceEX/Sprites/Px32/Player/Hero_JumpStart.png','Assets/_Project/ResourcceEX/Sprites/Px32/Player/Hero_Fall.png','NEWIMAG/Px32HeroV9/Idle/Hero_Idle_8.png','NEWIMAG/Px32HeroV9/Run/Hero_Run_10.png','NEWIMAG/Px32HeroRef/Bag/Bag_Hip_ThreeQuarter.png','NEWIMAG/Px32HeroRef/Sources/R2_chain.png']
 hashes={p:hashlib.sha256((ROOT/p).read_bytes()).hexdigest() for p in inputs}
 dump('validation.json',dict(frames=meta,order=order,changes=data['changes'],selected_interval=[0,1],asf=checks,expected_spec_warning=warning,previews=previews,input_sha256=hashes,visual_review='pending',timing=dict(original_frames=3,original_fps=15,new_frames=4,new_fps=20,seconds=0.2)))
 attempts={name:dict(attempts=1,selected=1,prompt=f'Sources/{name}_prompt1.txt',generated=f'Sources/{name}_generated1.png') for name in ['F0','F1','F2']};attempts['I0']=dict(attempts=2,selected=2,prompt='Sources/I0_prompt2.txt',generated='Sources/I0_generated2.png',rejected='1: fixed bag concealed folded hand; regenerate visibly empty hand left of bag')
 dump('attempts.json',dict(tool='built-in image_gen',max_per_frame=3,frames=attempts))
 print(json.dumps(dict(frames=meta,asf=checks,previews=previews),ensure_ascii=False,indent=2))
if __name__=='__main__':main()
