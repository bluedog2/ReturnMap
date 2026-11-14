import numpy as np, json
from PIL import Image
A='C:/Users/Owner/work/ReturnMap/Assets/_Project/ResourcceEX/Sprites/Px32/Player/'
rd=lambda p:np.array(Image.open(p).convert('RGBA'))
wb=lambda x:np.where(x[:,:,3:]>0,x,255).astype(np.uint8)
def frames(s):return [s[:,i*128:(i+1)*128] for i in range(s.shape[1]//128)]
def gif(fr,path,dur):
    ims=[Image.fromarray(wb(f).repeat(3,0).repeat(3,1)).convert('P',palette=Image.ADAPTIVE,colors=255) for f in fr]
    ims[0].save(path,save_all=True,append_images=ims[1:],duration=dur,loop=0)
idle0=frames(rd('Idle/Hero_Idle_8.png'))[0]
res={}
for name,fps,orig in [('Hit',16,'Hero_Hit.png'),('Death',12,'Hero_Death.png')]:
    s=rd(f'{name}/Hero_{name}_{4 if name=="Hit" else 10}.png');fr=frames(s)
    d=int(1000/fps)
    durs=[d]*len(fr)
    if name=='Death':durs[-1]=1000
    gif(fr,f'{name}/Hero_{name}_3x.gif',durs)
    o=frames(rd(A+orig))
    # 비교: 원본(같은 시간 길이)·새 그림 나란히 — 새 장 기준으로 원본 장 인덱스 대응
    comp=[np.concatenate([o[min(int(i*len(o)/len(fr)),len(o)-1)],f],1) for i,f in enumerate(fr)]
    gif(comp,f'{name}/Compare_3x.gif',durs)
    if name=='Hit':gif([idle0]+fr+[idle0],'Hit/Transition_3x.gif',[d]*(len(fr)+1)+[400])
    st=[]
    for i,f in enumerate(fr):
        y,x=np.where(f[:,:,3]>0)
        st.append(dict(frame=i,colors=int(len(np.unique(f[f[:,:,3]>0,:3],axis=0))),alpha=np.unique(f[:,:,3]).tolist(),floor=int(y.max()),top=int(y.min())))
    res[name]=st
print(json.dumps(res))
