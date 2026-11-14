from pixel_io import *
from prepare import BASE,atlas
ZERO=(0,0,0,0)
PAL=sorted({p[:3] for r in readpng(BASE/'Hero_Idle.png') for p in r if p[3]})
def clean(p):
    if p[3]<128:return ZERO
    return min(PAL,key=lambda c:sum((c[k]-p[k])**2 for k in range(3)))+(255,)
def recover(name,attempt,pitch=4,cols=3,rowstride=512):
    src=readpng(ROOT/'Sources'/f'{name}_attempt{attempt}.png')
    n=6 if name=='Idle' else 8
    return [[[clean(src[(i//cols)*rowstride+y*pitch+pitch//2][(i%cols)*128*pitch+x*pitch+pitch//2]) for x in range(128)] for y in range(128)] for i in range(n)]
def compose(name,frames):
    s=readpng(BASE/f'Hero_{name}.png');out=[]
    for i,f in enumerate(frames):
        original=[r[i*128:(i+1)*128] for r in s];m=readpng(ROOT/'Masks'/f'{name}_{i:02}.png')
        out.append([[f[y][x] if m[y][x][0] else original[y][x] for x in range(128)] for y in range(128)])
    return out
if __name__=='__main__':
    import sys
    name=sys.argv[1];attempt=int(sys.argv[2]);p=4 if name=='Idle' else 3;c=3 if name=='Idle' else 4
    fs=recover(name,attempt,p,c);out=compose(name,fs)
    writepng(ROOT/'Sources'/f'{name}_attempt{attempt}_composite_3x.png',nearest(atlas(out,c),3))
    writepng(ROOT/'Sources'/f'{name}_attempt{attempt}_recovered.png',atlas(fs,c))
