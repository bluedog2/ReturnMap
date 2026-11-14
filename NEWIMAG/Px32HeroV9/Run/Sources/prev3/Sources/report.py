from pathlib import Path
import json,hashlib
import numpy as np
from PIL import Image
O=Path('NEWIMAG/Px32HeroV9/Run');S=O/'Sources'
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
visual={
'머리':'통과 — 백금발 포니테일·밴드·Z핀 원본 보존, y77 위 불일치 0',
'얼굴':'통과 — 빨간 눈·웃음 등 승인 얼굴 픽셀 보존. 작은 송곳니·피어싱은 원본 보존 근거 병기',
'외투':'통과 — 흰 긴 퍼프 소매와 빨간 안감, 뒤팔 손목까지 소매 연결',
'손':'통과 — 뒤팔 소매 끝에서 검은 핑거리스 장갑으로 연결, 맨 팔뚝 없음',
'상의':'통과 — 검은 크롭 탑·초커 원본 유지',
'하의':'통과 — 반바지·벨트 원본 유지, 가방/체인 가림 부분 제외',
'다리':'통과 — 비대칭 검정/빨강 양말과 원본 자세 유지, 앞 다리가 가방보다 위',
'신발':'통과 — 흰/검정 하이탑·빨간 스트랩 원본 보존',
'가방':'통과 — 승인 3/4 가방의 빨간 띠·리벳·맞물린 이빨 그대로, 가려진 부분 제외',
'가방 위치(평소)':'통과 — 왼쪽 뒤 다리 허벅지 앞, 엉덩이 이동에 세로 ±1px 추가',
'가방 위치(긴 대기)':'해당 없음 — Run 키 포즈만 작업',
'크기':'통과 — 128셀, 원본 머리 꼭대기와 바닥 127 유지, 축소·회전 없음'}
v=json.loads((O/'validation.json').read_text(encoding='utf-8'))
for i in [0,2]:
 a=np.array(Image.open(O/f'Run_Key{i}.png'));o=np.array(Image.open(S/f'Original_{i}.png'));m=np.array(Image.open(O/f'Masks/Key{i}_edit.png'))>0
 assert not np.any(a[~m]!=o[~m]);assert a.shape==(128,128,4);assert set(np.unique(a[:,:,3]))=={0,255}
 assert v[str(i)]['colors']<=24 and v[str(i)]['outside_idle_palette']==0
 v[str(i)]['visual_3x']=visual;v[str(i)]['visual_review_files']=['Keys_3x.png','Keys_8x_hip.png'];v[str(i)]['image_gen_attempts']=1
v['method']={'art':'built-in image_gen only','postprocess':['integer block recovery pitch 10 origin (-17,-11)','approved Idle palette mapping','alpha threshold 128','predefined region mask compositing','integer translation of original bag and cropped 6px chain','pixel repetition for previews'],'hip_measurement':'3x/8x visual landmark: upper outer corner of rear hip/shorts; integer point selection, not whole-body image registration','lag':'Only keys: specified bounce approximation (+1/-1). Full 10-frame one-frame temporal lag is deferred until key approval.','preview_order':['original 0','original 2','new 0','new 2'],'user_approval':'pending'}
paths=['Assets/_Project/ResourcceEX/Sprites/Px32/Player/Hero_Run.png','Assets/_Project/ResourcceEX/Sprites/Px32/Player/Hero_Idle.png','NEWIMAG/Px32HeroRef/Arms/Arm_Forward.png','NEWIMAG/Px32HeroRef/Arms/Arm_Back.png','NEWIMAG/Px32HeroRef/Bag/Bag_Hip_ThreeQuarter.png','NEWIMAG/Px32HeroRef/Sources/R2_chain.png']
v['source_sha256']={p:sha(p) for p in paths}
(O/'validation.json').write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf-8')
attempts={'tool':'built-in image_gen','max_per_frame':3,'frames':{str(i):{'attempts':1,'selected':1,'prompt':f'Sources/Key{i}_prompt1.txt','generated':f'Sources/Key{i}_generated1.png','reason':'긴 뒤소매·장갑 및 가방 제거 영역 사용. 전신 생성 변화는 마스크 밖 폐기.'} for i in [0,2]}}
(O/'attempts.json').write_text(json.dumps(attempts,ensure_ascii=False,indent=2),encoding='utf-8')
print('Independent saved-file validation passed for both keys.')
