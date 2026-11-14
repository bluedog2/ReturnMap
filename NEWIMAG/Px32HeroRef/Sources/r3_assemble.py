# 재요청 3(사용자 지시 2026-11-07): 가방을 왼쪽(보는 사람 기준) 허리로 옮기고 약 6px 위로.
# 새로 그리는 픽셀 없음 — 승인된 Bag_Hip_* · R2_chain 을 정수 평행이동, 체인은 아래쪽을 잘라 길이만 줄임(마스크).
import sys
from build import *
from assemble import put

OUT=Path(sys.argv[1]) if len(sys.argv)>1 else None   # 미리보기 폴더(없으면 실제 출력)
def read(p):return np.array(Image.open(p).convert('RGBA'))
def rect(x0,y0,x1,y1):
    m=np.zeros((128,128),bool);m[y0:y1,x0:x1]=True;return m
def out(rel,a):
    if OUT:(OUT/rel).parent.mkdir(parents=True,exist_ok=True);Image.fromarray(a.astype(np.uint8),'RGBA').save(OUT/rel)
    else:save(rel,a)

def rect2(box):
    m=np.zeros((128,128),bool);x0,y0,x1,y1=box;m[y0:y1,x0:x1]=True;return m
# 가방 없는 몸 = r2 와 같은 방법(prev2 원본 + 복원 레이어로 옛 가방 자리 채움)
restore={}
for n,box in {'Front':(51,90,63,104),'Side':(62,92,72,104),'Back':(65,90,71,99)}.items():
    restore[n]=composite(read(SRC/'prev'/f'Turnaround/Hero_{n}.png'),read(SRC/f'V9Fix_Restore_{n}_recovered.png'),rect2(box))
restore['ThreeQuarter']=read(SRC/'ThreeQuarter_no_bag.png')
bagless={}
for n in restore:
    bagless[n]=composite(read(SRC/'prev2'/f'Turnaround/Hero_{n}.png'),restore[n],read(SRC/f'V9Fix_{n}_bag_layer.png')[:,:,3]>0)

CHAIN_FULL=read(SRC/'R2_chain.png')
CHAIN_LEN={'Front':9,'ThreeQuarter':6,'Side':9,'Back':9}  # 손잡이 윗끝(y)까지 = 89+길이
# (자산, 가방 x, 가방 y, 체인 x) — 뚜껑 윗변 = y+3(뒷면 자산은 y+4) = 101 로 4면 동일
place={'Front':('Front',40,98,46),'ThreeQuarter':('ThreeQuarter',53,95,58),
       'Side':('ThreeQuarter',60,98,65),'Back':('Side',61,97,66)}
# 팔·장갑이 가방보다 앞(가방 픽셀이 덮지 않음)
arm={'Front':rect(36,84,51,104),'ThreeQuarter':rect(44,84,56,102),
     'Side':rect(49,84,62,106),'Back':rect(67,84,80,100)}
res={};layers={};meta={}
for n,(asset,x,y,cx) in place.items():
    base=bagless[n];chain=CHAIN_FULL[:CHAIN_LEN[n]]
    bag=put(np.zeros_like(BASE),read(ROOT/f'Bag/Bag_Hip_{asset}.png'),x,y)
    strap=put(np.zeros_like(BASE),chain,cx,89)
    keep=arm[n]&(base[:,:,3]>0)
    bag[keep]=0;strap[keep]=0
    a=composite(base,strap,strap[:,:,3]>0)
    a=composite(a,bag,bag[:,:,3]>0)
    lay=composite(strap,bag,bag[:,:,3]>0)
    res[n]=a;layers[n]=lay
    if n=='ThreeQuarter':
        fb=put(np.zeros_like(BASE),read(ROOT/f'Bag/Bag_Hip_{asset}.png'),x,y);fs=put(np.zeros_like(BASE),chain,cx,89)
        fulllay=composite(fs,fb,fb[:,:,3]>0)
    full=put(np.zeros_like(BASE),read(ROOT/f'Bag/Bag_Hip_{asset}.png'),x,y)
    meta[n]={'asset':f'Bag/Bag_Hip_{asset}.png','translation':[x,y],'box_top':y+(4 if asset=='Side' else 3),
             'bottom':bbox(full)[3]-1,'chain_translation':[cx,89],'chain_length':CHAIN_LEN[n],
             'hidden_by_arm_px':int(((full[:,:,3]>0)&keep).sum()),'changed_outside_layer':int((np.any(a!=base,axis=2)&~(lay[:,:,3]>0)).sum())}
    out(f'Turnaround/Hero_{n}.png',a);out(f'Sources/R3_{n}_bag_layer.png',lay)

# 팔 4자세: r2 팔 시트에서 '팔 픽셀'(r2 3/4 와 다른 픽셀)만 새 3/4 위에 얹는다
q=res['ThreeQuarter'];q2=read(SRC/'prev3'/'Turnaround/Hero_ThreeQuarter.png') if (SRC/'prev3').exists() else read(ROOT/'Turnaround/Hero_ThreeQuarter.png')
arms=[]
for n in ['Down','HandOnHip','Forward','Back']:
    src=read(SRC/'prev3'/f'Arms/Arm_{n}.png') if (SRC/'prev3').exists() else read(ROOT/f'Arms/Arm_{n}.png')
    am=read(SRC/'prev3'/f'Masks/Arm_{n}.png')[:,:,0]>0 if (SRC/'prev3').exists() else read(ROOT/f'Masks/Arm_{n}.png')[:,:,0]>0
    armpx=am&np.any(src!=q2,axis=2)
    # r2 가방(오른쪽 무릎) 자리는 팔 픽셀이 아니다
    r2bag=read(SRC/'R2_ThreeQuarter_bag_layer.png')[:,:,3]>0
    r2ch=read(SRC/'R2_ThreeQuarter_chain_layer.png')[:,:,3]>0
    armpx&=~((r2bag|r2ch)&np.all(src==q2,axis=2))
    if n=='Down':
        a=q
    else:
        # 내린 팔이 없는 자세: 가방·체인 전체를 먼저 깔고, 실제 팔 픽셀(불투명)만 위에 얹는다
        qf=composite(bagless['ThreeQuarter'],fulllay,fulllay[:,:,3]>0)
        a=composite(qf,src,armpx&(src[:,:,3]>0))
        gone=am&(src[:,:,3]==0)&~(fulllay[:,:,3]>0)
        a=a.copy();a[gone]=0
        armpx=armpx&(src[:,:,3]>0)
    hid=int(((fulllay[:,:,3]>0)&armpx).sum()) if n!='Down' else meta['ThreeQuarter']['hidden_by_arm_px']
    meta['Arm_'+n]={'bag_px_hidden_by_arm':hid}
    out(f'Arms/Arm_{n}.png',a);arms.append(a)
order=['Front','ThreeQuarter','Side','Back']
sheet=np.concatenate([res[n] for n in order],axis=1)
out('Turnaround/Turnaround_1x.png',sheet);out('Turnaround/Turnaround_4x.png',enlarge(sheet,4))
out('Turnaround/Hips_8x.png',enlarge(np.concatenate([res[n][82:118,24:96] for n in order],axis=1),8))
ash=np.concatenate(arms,axis=1);out('Arms/Arms_4x.png',enlarge(ash,4))
txt=json.dumps(meta,indent=2,ensure_ascii=False)
if OUT:(OUT/'R3_placement.json').write_text(txt,encoding='utf-8')
else:(SRC/'R3_placement.json').write_text(txt,encoding='utf-8')
print(txt)
