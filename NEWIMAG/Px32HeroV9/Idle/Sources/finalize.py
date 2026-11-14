from build import *
import hashlib
from PIL import ImageSequence
between={i:read(OUT/f'Sources/Between_{i}.png') for i in edges}
eight=[];order=[]
for i,a in enumerate(frames):
 eight.append(a);order.append(f'F{i}')
 if i in between:eight.append(between[i]);order.append(f'I{i}-{(i+1)%6}')
save('Hero_Idle_8.png',np.concatenate(eight,1))
save('Hero_Idle_6_3x.png',zoom(np.concatenate(frames,1)))
save('Hero_Idle_8_3x.png',zoom(np.concatenate(eight,1)))
sheet=np.zeros((256,1024,4),np.uint8);sheet[:128,:768]=np.concatenate(frames,1);sheet[128:]=np.concatenate(eight,1)
save('Sheet_3x.png',zoom(sheet))
review=np.concatenate([np.concatenate(eight[j:j+4],1) for j in [0,4]],0);save('Sources/Final_review_3x.png',zoom(review))
palette=np.unique(raw[raw[:,:,3]>0,:3],axis=0)
def indexed(a):
 ids=np.zeros(a.shape[:2],np.uint8)
 for i,c in enumerate(palette,1):ids[np.all(a[:,:,:3]==c,2)&(a[:,:,3]>0)]=i
 im=Image.fromarray(ids,'P');p=[0,0,0]+palette.flatten().tolist();im.putpalette(p+[0]*(768-len(p)));im.info['transparency']=0;return im
gifs={}
def gif(name,seq):
 ims=[indexed(zoom(a)) for a in seq];dur=[120 if i%2==0 else 130 for i in range(len(seq))]
 ims[0].save(OUT/name,save_all=True,append_images=ims[1:],duration=dur,loop=0,transparency=0,disposal=2,optimize=False)
 im=Image.open(OUT/name);mismatch=[];times=[]
 for i,f in enumerate(ImageSequence.Iterator(im)):
  decoded=np.array(f.convert('RGBA'));target=zoom(seq[i]);active=(decoded[:,:,3]>0)|(target[:,:,3]>0)
  mismatch.append(int((np.any(decoded!=target,2)&active).sum()));times.append(f.info['duration'])
 gifs[name]=dict(frames=len(mismatch),total_ms=sum(times),durations_ms=times,visible_pixel_mismatches=mismatch)
gif('Hero_Idle_6_3x.gif',frames);gif('Hero_Idle_8_3x.gif',eight)
gif('Compare_3x.gif',[np.concatenate([orig[i%6],frames[i%6],eight[i%8]],1) for i in range(24)])
def stats(a,o,m,label):
 ys,xs=np.where(a[:,:,3]>0);colors=np.unique(a[a[:,:,3]>0,:3],axis=0)
 return dict(frame=label,colors=len(colors),approved_palette=all(any(np.array_equal(c,p) for p in palette) for c in colors),alpha=np.unique(a[:,:,3]).tolist(),top=int(ys.min()),floor=int(ys.max()),outside_mismatch=int((np.any(a!=o,2)&~m).sum()),head_unchanged=bool(np.array_equal(a[:69],o[:69])),floor_row_unchanged=bool(np.array_equal(a[127],o[127])),visual_3x=dict(long_sleeves='통과',black_gloves='통과',bag_fangs='통과',asymmetric_legs='통과',old_bag_remnants='없음',seams='통과'))
records=[]
for i,a in enumerate(frames):
 r=stats(a,orig[i],masks[i],f'F{i}');r.update(body_translation=moves[i],bag_translation=moves[(i-1)%6],seam_generation_attempts=0);records.append(r)
for i,a in between.items():
 m=np.array(Image.open(OUT/f'Masks/Between_{i}.png'))>0
 r=stats(a,frames[i],m,f'I{i}-{(i+1)%6}');r.update(generation_attempts=1,distinct_from_before=bool(np.any(a!=frames[i])),distinct_from_after=bool(np.any(a!=frames[(i+1)%6])));records.append(r)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
report=dict(master_byte_identical=sha(OUT/'Hero_Idle_Master.png')==sha(REF/'Arms/Arm_HandOnHip.png'),master_mask_exact=bool(np.array_equal(np.array(Image.open(OUT/'Masks/Master_edit.png'))>0,edit)),master=stats(master,orig[0],edit,'master'),frames=records,order_8=order,changes=changes,selected_edges=edges,gifs=gifs,source_sha256=sha(ROOT/'Assets/_Project/ResourcceEX/Sprites/Px32/Player/Hero_Idle.png'),master_source_sha256=sha(REF/'Arms/Arm_HandOnHip.png'),measurement_roi=[66,78,87,98],measurement_method='RGBA mismatch minimum over integer dx/dy -3..3 on visible torso/hip band; ties prefer shortest shift. Original hand-drawn frames are not exact translations.',bag_delay='cyclic previous frame, including F0 <- F5',preview_scaling='integer pixel repetition only; no artwork resampling or rotation',intermediate_scope='Generated torso/arm/hem only; head and legs inherited from preceding frame, bag/chain protected.',settings_review='Master identity retained: platinum ponytail, red eyes and Z hairpin, white jacket/red lining, crop top, shorts/belt, high-tops; long sleeves/gloves/fangs/asymmetric legs visually checked at 3x for every frame.')
assert report['master_byte_identical'] and report['master_mask_exact']
assert all(r['outside_mismatch']==0 and r['floor']==127 and r['colors']<=24 and r['approved_palette'] and r['alpha']==[0,255] and r['head_unchanged'] and r['floor_row_unchanged'] for r in records)
assert all(not any(g['visible_pixel_mismatches']) for g in gifs.values())
(OUT/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
attempts=dict(tool='built-in image_gen',intermediates=[dict(edge=f'{i}->{i+1}',attempt=1,status='채택',generated=f'Sources/Between_{i}_generated1.png',prompt=f'Sources/Between_{i}_prompt.txt',recovery='Sources/recovery.json',reason='정수 블록 복원 후 몸통 마스크 합성; 앞뒤와 다른 중간 자세, 보호 영역 원본 보존') for i in [1,2]],seams=[dict(frame=i,attempts=0,reason='정수 패치 합성 후 소매·장갑·밑단 이음새 수정 불필요; 원래 가방 잔여는 제거 마스크 보완으로 해소') for i in range(6)])
(OUT/'attempts.json').write_text(json.dumps(attempts,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(dict(order=order,gifs=gifs,frames=records),ensure_ascii=False))
