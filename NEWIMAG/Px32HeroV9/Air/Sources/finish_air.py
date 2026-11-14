from build_air import *

def indexed(a):
 idx=np.zeros(a.shape[:2],dtype='uint8')
 for k,c in enumerate(PAL):idx[np.all(a[:,:,:3]==c,2)&(a[:,:,3]>0)]=k+1
 im=Image.fromarray(idx,'P');pal=[0,0,0]+PAL.astype('uint8').flatten().tolist();im.putpalette(pal+[0]*(768-len(pal)));im.info['transparency']=0
 return im

def gif(name,arrays,durations,loop=True):
 ims=[indexed(zoom(a,3)) for a in arrays]
 kw=dict(save_all=True,append_images=ims[1:],duration=durations,transparency=0,disposal=2,optimize=False)
 if loop:kw['loop']=0
 ims[0].save(O/name,**kw)
 mism=0;count=0;total=0
 for i,im in enumerate(ImageSequence.Iterator(Image.open(O/name))):
  a=np.array(im.convert('RGBA'));b=zoom(arrays[i],3);u=(a[:,:,3]>0)|(b[:,:,3]>0)
  mism+=int(np.any(a[u]!=b[u],1).sum());count+=1;total+=im.info['duration']
 assert mism==0 and count==len(arrays) and total==sum(durations)
 return dict(frames=count,total_ms=total,visible_rgba_mismatch=mism,durations_ms=durations,loop=loop)

def main():
 fixed='ede3ee6547d4206034dbbe85db7d1eac55f182dc6568d0c5189b642f4ce4e53d'
 assert hashlib.sha256((O/'Fall_F0.png').read_bytes()).hexdigest()==fixed
 groups={k:[read(O/f'{k}_F{i}.png') for i in range(n)] for k,n in [('Fall',4),('Rise',3),('Apex',3)]}
 v={'status':'visual_review_pending','approved_Fall_F0_sha256':fixed,'frames':{},'previews':{},'asf':{}}
 spec=importlib.util.spec_from_file_location('asf',ROOT/'tools/sprite/asf-to-px32.py');asf=importlib.util.module_from_spec(spec);spec.loader.exec_module(asf)
 refs={'Fall_F1':'Fall_F0','Fall_F3':'Fall_F2','Apex_F0':'Rise_F2','Apex_F1':'Rise_F2','Apex_F2':'Fall_F0'}
 rows=[];hips=[]
 for kind,fs in groups.items():
  n=len(fs);sheet=np.concatenate(fs,1)
  save(f'Hero_{kind}_{n}.png',sheet);save(f'Hero_{kind}_{n}_3x.png',zoom(sheet,3));save(f'Waist_{kind}_8x.png',zoom(np.concatenate([a[68:110,18:106] for a in fs],1),8))
  row=np.zeros((128,512,4),dtype='uint8');row[:,:n*128]=sheet;rows.append(row)
  hr=np.zeros((42,352,4),dtype='uint8');hr[:,:n*88]=np.concatenate([a[68:110,18:106] for a in fs],1);hips.append(hr)
  rules=asf.rules_for('Hero_Fall') # all three are airborne; no ground baseline enforcement
  fails,metrics=asf.check(Image.fromarray(sheet),128,n,{tuple(c) for c in PAL},rules)
  legacy=asf.sheet_spec('Hero_'+kind)
  warning=f'기존 규격 {legacy[1]}장 → 요청 {n}장' if legacy else f'Hero_{kind}: 기존 규격표에 없는 신규 공중 동작'
  v['asf'][kind]=dict(stage=4,scale_frames=False,placement=False,rules=rules,failures=fails,metrics=metrics,expected_spec_warning=warning)
  v['previews'][kind]=gif(f'Hero_{kind}_{n}_3x.gif',fs,[80,90,80] if kind=='Apex' else [100]*n,kind!='Apex')
  for i,a in enumerate(fs):
   name=f'{kind}_F{i}';m=json.loads((S/f'{name}_metrics.json').read_text(encoding='utf-8'))
   edit=np.array(Image.open(O/f'Masks/{name}_edit.png'))>0
   if kind=='Rise':
    refpath=ROOT/'NEWIMAG/Px32HeroV9/JumpStart/JumpStart_F2.png';ref=read(refpath);old=read(ROOT/'NEWIMAG/Px32HeroV9/JumpStart/Sources/F2_bag_layer.png')
   elif name in refs:
    refpath=O/f'{refs[name]}.png';ref=read(refpath);old=read(S/f'{refs[name]}_layer.png')
   else:refpath=ROOT/m['source'];ref=read(refpath);old=np.zeros_like(a)
   edit|=old[:,:,3]>0
   if name!='Fall_F0':save(f'Masks/{name}_edit.png',edit*255)
   mismatch=int((np.any(a!=ref,2)&~edit).sum());lay=read(S/f'{name}_layer.png');lm=lay[:,:,3]>0
   m.update(reference=str(refpath.relative_to(ROOT)),reference_sha256=hashlib.sha256(refpath.read_bytes()).hexdigest(),outside_mask_mismatch=mismatch,bag_layer_mismatch=int(np.any(a[lm]!=lay[lm],1).sum()),reference_head_top=int(np.where(ref[:,:,3]>0)[0].min()),head_crown_mismatch=int(np.any(a[:45]!=ref[:45],2).sum()))
   assert mismatch==0 and m['bag_layer_mismatch']==0 and m['colors']<=24 and m['outside_palette']==0 and m['alpha']==[0,255] and m['head_top']==m['reference_head_top']
   v['frames'][name]=m
 save('Sheet_3x.png',zoom(np.concatenate(rows,0),3));save('Waist_8x.png',zoom(np.concatenate(hips,0),8))
 jump=read(ROOT/'NEWIMAG/Px32HeroV9/JumpStart/Hero_JumpStart_4.png');jf=[jump[:,i*128:(i+1)*128] for i in range(4)]
 flow=jf+groups['Rise']*2+groups['Apex']+groups['Fall']*2
 v['previews']['Jump_Flow']=gif('Jump_Flow_3x.gif',flow,[50]*4+[100]*6+[80,90,80]+[100]*8)
 dump('Sources/ASF_check.json',v['asf']);dump('validation.json',v)
 attempts={'tool':'built-in image_gen','limit_per_frame':3,'frames':{}}
 for name,m in v['frames'].items():
  n=m['attempt'];attempts['frames'][name]=dict(attempts_reserved=n,selected=n,prompt=f'Sources/{name}_prompt{n}.txt',generated=f'Sources/{name}_generated{n}.png')
 attempts['frames']['Fall_F0']['note']='직전 실행 생성 1회, 완성 파일 보존'
 attempts['frames']['Fall_F2']['note']='기존 prompt1은 undefined 10바이트이며 결과 없음. 과거 호출 여부 확인 불가: 보수적으로 1회 예약, 이번 성공 호출은 2번으로 기록.'
 dump('attempts.json',attempts)
 print(json.dumps({'asf':v['asf'],'previews':v['previews']},ensure_ascii=False,indent=2))
if __name__=='__main__':main()
