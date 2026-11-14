from complete_run import *
import importlib.util
from PIL import ImageSequence

def indexed(a):
 idx=np.zeros(a.shape[:2],dtype='uint8')
 for j,c in enumerate(PAL): idx[np.all(a[:,:,:3]==c,2)&(a[:,:,3]>0)]=j+1
 im=Image.fromarray(idx,'P'); colors=[0,0,0]+PAL.astype('uint8').flatten().tolist();im.putpalette(colors+[0]*(768-len(colors)));im.info['transparency']=0;return im
def gif(name,arrays,fps,repeats):
 ims=[indexed(zoom(a,3)) for a in arrays]*repeats; n=len(ims); durations=[round((i+1)*100/fps)*10-round(i*100/fps)*10 for i in range(n)]
 ims[0].save(O/name,save_all=True,append_images=ims[1:],duration=durations,loop=0,transparency=0,disposal=2,optimize=False)
 mismatch=0; count=0
 for j,im in enumerate(ImageSequence.Iterator(Image.open(O/name))):
  a=np.array(im.convert('RGBA')); b=zoom(arrays[j%len(arrays)],3);u=(a[:,:,3]>0)|(b[:,:,3]>0);mismatch+=int(np.any(a[u]!=b[u],1).sum());count+=1
 return dict(frames=count,total_ms=sum(durations),fps=fps,visible_mismatch=mismatch)

def main():
 data=json.loads((S/'run8_measurements.json').read_text(encoding='utf-8')); fs=[read(O/(f'Run_Key{i}.png' if i in [0,2] else f'Run_F{i}.png')) for i in range(8)]
 mids={}; validation={}
 for i in data['selected_intervals']:
  base=read(S/f'F{i}_bagless.png'); b,rec=recover(read(S/f'Between_{i}_generated1.png'),base)
  m=np.array(Image.open(O/f'Masks/F{i}_generation.png'))>0
  a=base.copy();a[m]=b[m]
  p=data['frames'][str(i)]['bag_position']; q=data['frames'][str((i+1)%8)]['bag_position'];pos=[(p[j]+q[j]+1)//2 for j in range(2)]
  l=layer_at(pos);bm=l[:,:,3]>0;a[bm]=l[bm]; oldlayer=read(S/f'F{i}_bag_layer.png');edit=m|bm|(oldlayer[:,:,3]>0)
  save(f'Run_I{i}-{(i+1)%8}.png',a); save(f'Masks/I{i}_edit.png',edit.astype('uint8')*255);save(f'Sources/I{i}_blocks.png',b);save(f'Sources/I{i}_review_3x.png',zoom(a,3));save(f'Sources/I{i}_bag_layer.png',l)
  mids[i]=a;validation[f'I{i}']=dict(recovery=rec,bag_position=pos,preceding_frame=i,following_frame=(i+1)%8,outside_mask_mismatch=int((np.any(a!=fs[i],2)&~edit).sum()),head_mismatch=int(np.any(a[:77]!=fs[i][:77],2).sum()))
 order=[];ten=[]
 for i,a in enumerate(fs):
  order.append(f'F{i}');ten.append(a)
  if i in mids:order.append(f'I{i}');ten.append(mids[i])
 sheet=np.concatenate(ten,1);save('Hero_Run_10.png',sheet);save('Hero_Run_10_3x.png',zoom(sheet,3));save('Waist_10_8x.png',zoom(np.concatenate([a[74:112,20:100] for a in ten],1),8))
 review=np.concatenate([np.concatenate(ten[:5],1),np.concatenate(ten[5:],1)],0);save('Sources/Final_review_3x.png',zoom(review,3))
 row=np.zeros_like(sheet);row[:,:1024]=np.concatenate(fs,1);save('Sheet_3x.png',zoom(np.concatenate([row,sheet],0),3))
 (S/'ASF_frames').mkdir(exist_ok=True)
 for j,a in enumerate(ten):save(f'Sources/ASF_frames/frame_{j:02}.png',a)
 # Only the stage-4 checker: no scale_frames, resize, placement or drawing.
 spec=importlib.util.spec_from_file_location('asf',ROOT/'tools/sprite/asf-to-px32.py');mod=importlib.util.module_from_spec(spec);spec.loader.exec_module(mod)
 fails,metrics=mod.check(Image.fromarray(sheet),128,10,{tuple(c) for c in PAL},mod.rules_for('Hero_Run'))
 oldspec=mod.sheet_spec('Hero_Run'); count_warning=f'dimensions: Hero_Run specification {oldspec[1]} frames versus approved new count 10'
 dump('Sources/ASF_check.json',dict(stage=4,checker='tools/sprite/asf-to-px32.py:check',scale_frames=False,placement='none; read-only check',metrics=metrics,art_failures=fails,expected_spec_warning=count_warning,result='PASS' if not fails else 'FAIL'))
 previews={};previews['8']=gif('Hero_Run_8_3x.gif',fs,18,9);previews['10']=gif('Hero_Run_10_3x.gif',ten,22,11)
 # Original / eight / ten, each on its own clock. Exact common cycle 20s.
 original=read(ROOT/'Assets/_Project/ResourcceEX/Sprites/Px32/Player/Hero_Run.png');orig=[original[:,i*128:(i+1)*128] for i in range(8)]
 ims=[];dur=[];last=None;expected=[]
 cached=[[np.array(indexed(zoom(a,3))) for a in group] for group in [orig,fs,ten]]
 palette=indexed(ten[0]).getpalette()
 for tick in range(2000):
  key=(int(tick*.18)%8,int(tick*.22)%10)
  if key==last:dur[-1]+=10;continue
  a=np.concatenate([cached[0][key[0]],cached[1][key[0]],cached[2][key[1]]],1);im=Image.fromarray(a,'P');im.putpalette(palette);im.info['transparency']=0;ims.append(im);dur.append(10);expected.append(key);last=key
 ims[0].save(O/'Compare_3x.gif',save_all=True,append_images=ims[1:],duration=dur,loop=0,transparency=0,disposal=2,optimize=False)
 mism=0
 for im,key in zip(ImageSequence.Iterator(Image.open(O/'Compare_3x.gif')),expected):
  a=np.array(im.convert('RGBA'));b=zoom(np.concatenate([orig[key[0]],fs[key[0]],ten[key[1]]],1),3);u=(a[:,:,3]>0)|(b[:,:,3]>0);mism+=int(np.any(a[u]!=b[u],1).sum())
 previews['compare']=dict(order=['original @18','8 @18','10 @22'],duration_ms=20000,visible_mismatch=mism)
 for name,a in zip(order,ten):
  ref=int(name[1:]);o=read(S/f'Original_{ref}.png');cols={tuple(c) for c in a[a[:,:,3]>0,:3]}
  ent=validation.get(name,dict(data['frames'][str(ref)]));ent.update(colors=len(cols),outside_idle_palette=len(cols-{tuple(c) for c in PAL}),alpha=list(map(int,np.unique(a[:,:,3]))),floor=int(np.where(a[:,:,3]>0)[0].max()),original_head_top=int(np.where(o[:,:,3]>0)[0].min()),head_top=int(np.where(a[:,:,3]>0)[0].min()),floor_row_mismatch=int(np.any(a[127]!=o[127],1).sum()))
  assert ent['outside_mask_mismatch']==0 and ent['colors']<=24 and ent['outside_idle_palette']==0 and ent['alpha']==[0,255] and ent['floor']==127 and ent['head_top']==ent['original_head_top']
  validation[name]=ent
 hashes={str(i):hashlib.sha256((O/f'Run_Key{i}.png').read_bytes()).hexdigest() for i in [0,2]}
 assert hashes=={'0':'09b6c454947d4acf130f1ec789d250a3d8f25673d2d3bc48cf8cf2591d76d63b','2':'a5c9d08080c2b90b20163c592fd087b2b0123ffe5a617db58da090def655c9ac'}
 v=json.loads((O/'validation.json').read_text(encoding='utf-8'));v['remaining_and_10']=dict(frames=validation,order=order,approved_key_sha256=hashes,asf=dict(failures=fails,spec_warning=count_warning),previews=previews,cyclic_changes=data['cyclic_changes'],visual_review='pending');dump('validation.json',v)
 attempts=json.loads((O/'attempts.json').read_text(encoding='utf-8'))
 for i in [1,3,4,5,6,7]:
  n=2 if i in [4,5] else 1;attempts['frames'][str(i)]=dict(attempts=n,selected=n,prompt=f'Sources/F{i}_prompt{n}.txt',generated=f'Sources/F{i}_generated{n}.png',reason='4/5 first result rejected: new boot pixels in removed bag area; second used' if n==2 else 'first result selected; masked rear sleeve restoration')
 for i in mids:attempts['frames'][f'I{i}']=dict(attempts=1,selected=1,prompt=f'Sources/Between_{i}_prompt1.txt',generated=f'Sources/Between_{i}_generated1.png')
 dump('attempts.json',attempts);print(json.dumps(dict(order=order,asf_failures=fails,previews=previews),indent=2))
if __name__=='__main__':main()
