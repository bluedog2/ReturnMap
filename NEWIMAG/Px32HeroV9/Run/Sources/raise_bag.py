# 사용자 검수 전 Claude 조정(2026-11-07): 가방·체인 레이어가 무릎 높이라 7px 위로 정수 평행이동(새 픽셀 없음).
# 가방은 몸 앞(뒤 다리 허벅지 앞)으로 얹는다.
import numpy as np, json
from PIL import Image
from pathlib import Path
R=Path(__file__).resolve().parents[1];DY=-7
orig={k:np.array(Image.open(R/f'Sources/Original_{k}.png')) for k in (0,2)}
out=[];meta={}
for k in (0,2):
    b=np.array(Image.open(R/f'Sources/Key{k}_bagless.png'));L=np.array(Image.open(R/f'Sources/Key{k}_bag_layer_full.png'))
    l=np.zeros_like(L);l[:DY]=L[-DY:]   # 위로 7px 이동(아래쪽은 투명)
    m=l[:,:,3]>0
    a=np.where(m[:,:,None],l,b)
    Image.fromarray(a).save(R/f'Run_Key{k}.png');out.append(a)
    y,x=np.where(m);meta[k]={'dy':DY,'layer_bbox':[int(x.min()),int(y.min()),int(x.max()),int(y.max())],
        'colors':len(np.unique(a[a[:,:,3]>0,:3],axis=0)),'floor':int(np.where(a[:,:,3]>0)[0].max())}
def wb(a):return np.where(a[:,:,3:]>0,a,255).astype(np.uint8)
row=np.concatenate([wb(orig[0]),wb(orig[2]),wb(out[0]),wb(out[1])],1)
Image.fromarray(row.repeat(3,0).repeat(3,1)).save(R/'Keys_3x.png')
hip=np.concatenate([wb(a)[70:112,20:110] for a in out],1)
Image.fromarray(hip.repeat(8,0).repeat(8,1)).save(R/'Keys_8x_hip.png')
(R/'Sources/raise_bag.json').write_text(json.dumps(meta,indent=2),encoding='utf-8');print(meta)
