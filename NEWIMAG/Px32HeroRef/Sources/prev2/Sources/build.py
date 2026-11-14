from pathlib import Path
import json, hashlib
import numpy as np
from PIL import Image, ImageDraw

ROOT=Path(__file__).resolve().parents[1]
SRC=ROOT/'Sources'
BASE=np.array(Image.open(SRC/'Idle0.png').convert('RGBA'))
PAL=np.unique(BASE[BASE[:,:,3]>0,:3],axis=0).astype(np.int32)

def save(path,a):
    Image.fromarray(a.astype(np.uint8),'RGBA').save(ROOT/path)

def enlarge(a,k):
    # Display-only integer pixel replication; never used to make production art.
    return a.repeat(k,axis=0).repeat(k,axis=1)

def recover(path,pitch=8,origin=(0,0),size=(128,128)):
    a=np.array(Image.open(path).convert('RGBA'))
    out=np.zeros((size[1],size[0],4),dtype=np.uint8)
    for y in range(size[1]):
        for x in range(size[0]):
            xx=origin[0]+x*pitch+pitch//2
            yy=origin[1]+y*pitch+pitch//2
            if 0<=yy<a.shape[0] and 0<=xx<a.shape[1]:
                p=a[yy,xx]
                if p[3]>=128:
                    c=PAL[np.argmin(((PAL-p[:3].astype(np.int32))**2).sum(axis=1))]
                    out[y,x]=[*c,255]
    return out

def mask(polygons):
    im=Image.new('1',(128,128)); d=ImageDraw.Draw(im)
    for p in polygons:d.polygon(p,fill=1)
    return np.array(im,dtype=bool)

def composite(base,patch,m):
    return np.where(m[:,:,None],patch,base)

def bbox(a):
    y,x=np.where(a[:,:,3]>0)
    return [int(x.min()),int(y.min()),int(x.max())+1,int(y.max())+1] if len(x) else None

def stats(a):
    b=bbox(a)
    return dict(size=[a.shape[1],a.shape[0]],bbox=b,colors=len(np.unique(a[a[:,:,3]>0,:3],axis=0)),alpha_values=np.unique(a[:,:,3]).tolist(),floor_row=b[3]-1 if b else None)

def mask_save(name,m):
    a=np.zeros((128,128,4),dtype=np.uint8);a[:,:,:3]=m[:,:,None]*255;a[:,:,3]=255
    save('Masks/'+name+'.png',a)

if __name__=='__main__':
    print(json.dumps(stats(BASE)))
