import sys
sys.dont_write_bytecode=True
import build as b
from build import np,Image,Path,json
from PIL import ImageSequence
import importlib.util,hashlib
def main(kind):
 b.O=b.ROOT/'NEWIMAG/Px32HeroV9'/kind;b.S=b.O/'Sources';O,S=b.O,b.S
 placement=json.loads((S/'placement.json').read_text(encoding='utf-8'));order=placement['order'];n=len(order);fps=20 if kind=='Land' else 28
 fs=[b.read(O/f'{kind}_{name}.png') for name in order];sheet=np.concatenate(fs,1)
 def indexed(a):
  ix=np.zeros(a.shape[:2],dtype='uint8')
  for k,c in enumerate(b.PAL):ix[np.all(a[:,:,:3]==c,2)&(a[:,:,3]>0)]=k+1
  im=Image.fromarray(ix,'P');pal=[0,0,0]+b.PAL.astype('uint8').flatten().tolist();im.putpalette(pal+[0]*(768-len(pal)));im.info['transparency']=0;return im
 def gif(name,arrays,fps,cycles=1):
  arrays=arrays*cycles;dur=[int(round((i+1)*100/fps)-round(i*100/fps))*10 for i in range(len(arrays))]
  ims=[indexed(b.zoom(a,3)) for a in arrays];ims[0].save(O/name,save_all=True,append_images=ims[1:],duration=dur,loop=0,transparency=0,disposal=2,optimize=False)
  mismatch=0;count=0;total=0
  for i,im in enumerate(ImageSequence.Iterator(Image.open(O/name))):
   a=np.array(im.convert('RGBA'));ref=b.zoom(arrays[i],3);u=(a[:,:,3]>0)|(ref[:,:,3]>0);mismatch+=int(np.any(a[u]!=ref[u],1).sum());count+=1;total+=im.info['duration']
  assert mismatch==0 and count==len(arrays) and total==sum(dur)
  return dict(frame_count=count,total_ms=total,visible_rgba_mismatch=mismatch,durations_ms=dur)
 v=dict(status='partial_strict_one_frame_lag_not_met',frames={},previews={},placement=placement)
 for name,a in zip(order,fs):
  m=placement['frames'][name];orig=b.read(S/f'Original_{m["source_frame"]}.png');edit=np.array(Image.open(O/f'Masks/{name}_edit.png'))>0;layer=b.read(S/f'{name}_layer.png');lm=layer[:,:,3]>0
  ys,xs=np.where(a[:,:,3]>0);oy,ox=np.where(orig[:,:,3]>0);colors=np.unique(a[a[:,:,3]>0,:3],axis=0);outside=sum(tuple(c) not in {tuple(p) for p in b.PAL} for c in colors)
  q=dict(colors=len(colors),outside_palette=outside,alpha=np.unique(a[:,:,3]).tolist(),head_top=int(ys.min()),original_head_top=int(oy.min()),bottom=int(ys.max()),original_bottom=int(oy.max()),bbox=[int(xs.min()),int(ys.min()),int(xs.max()+1),int(ys.max()+1)],outside_mask_mismatch=int((np.any(a!=orig,2)&~edit).sum()),bag_layer_mismatch=int(np.any(a[lm]!=layer[lm],1).sum()),source_sha256=hashlib.sha256((S/f'Original_{m["source_frame"]}.png').read_bytes()).hexdigest())
  assert q['colors']<=24 and outside==0 and q['alpha']==[0,255] and q['outside_mask_mismatch']==0 and q['bag_layer_mismatch']==0 and q['head_top']==q['original_head_top'] and q['bottom']==q['original_bottom']
  v['frames'][name]=q
 sp=importlib.util.spec_from_file_location('asf',b.ROOT/'tools/sprite/asf-to-px32.py');asf=importlib.util.module_from_spec(sp);sp.loader.exec_module(asf)
 rules=asf.rules_for('Hero_'+kind);fails,metrics=asf.check(Image.fromarray(sheet),128,n,{tuple(c) for c in b.PAL},rules)
 v['asf']=dict(stage=4,scale_frames=False,place=False,rules=rules,failures=fails,metrics=metrics,expected_spec_warning=f'기존 {asf.sheet_spec("Hero_"+kind)[1]}장 → 요청 {n}장',bbox_reason='원본 착지 웅크림→일어서기 및 옆으로 벌린 균형 팔' if kind=='Land' else '원본 대시 몸 기울기·포니테일·다리 벌림 변화 보존')
 b.dump('Sources/ASF_check.json',v['asf'])
 cycles=1 if kind=='Land' else 7
 v['previews']['animation']=gif(f'Hero_{kind}_{n}_3x.gif',fs,fps,cycles)
 compare=[np.concatenate([b.read(S/f'Original_{placement["frames"][name]["source_frame"]}.png'),a],1) for name,a in zip(order,fs)]
 v['previews']['compare']=gif('Compare_3x.gif',compare,fps,cycles)
 if kind=='Land':
  before=b.read(b.ROOT/'NEWIMAG/Px32HeroV9/Air/Fall_F3.png');after=b.read(b.ROOT/'NEWIMAG/Px32HeroV9/Idle/Hero_Idle_8.png')[:,:128]
 else:
  run=b.read(b.ROOT/'NEWIMAG/Px32HeroV9/Run/Hero_Run_10.png');before=run[:,-128:];after=run[:,:128]
 v['previews']['transition']=gif('Transition_3x.gif',[before]+fs+[after],fps,cycles)
 b.save(f'Hero_{kind}_{n}_3x.png',b.zoom(sheet,3));b.save('Sources/Transition_3x.png',b.zoom(np.concatenate([before]+fs+[after],1),3))
 # Inputs are read-only; hashes document provenance without rewriting them.
 inputs=[b.ROOT/'NEWIMAG/Px32HeroV9/Idle/Hero_Idle_8.png',b.ROOT/'NEWIMAG/Px32HeroRef/Bag/Bag_Hip_ThreeQuarter.png',b.ROOT/'NEWIMAG/Px32HeroRef/Sources/R2_chain.png',b.ROOT/f'Assets/_Project/ResourcceEX/Sprites/Px32/Player/Hero_{kind}.png']
 v['input_sha256']={str(p.relative_to(b.ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in inputs}
 b.dump('validation.json',v)
 b.dump('attempts.json',dict(tool='built-in image_gen',limit_per_frame=3,frames={name:dict(attempts=1,selected=1,prompt=f'Sources/{name}_prompt1.txt',generated=f'Sources/{name}_generated1.png') for name in order},chain=dict(attempts=1,prompt='Sources/Chain_prompt1.txt',generated='Sources/Chain_generated1.png') if kind=='Dash' else dict(attempts=0,asset='R2_chain first 6 rows')))
 print(json.dumps(dict(kind=kind,frames=v['frames'],asf=v['asf'],previews={k:{kk:vv for kk,vv in val.items() if kk!='durations_ms'} for k,val in v['previews'].items()}),ensure_ascii=False,indent=2))
if __name__=='__main__':main(sys.argv[1])
