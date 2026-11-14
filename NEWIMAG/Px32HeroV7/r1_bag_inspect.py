from pixel_io import *
orig=readpng(ROOT/'Sources/R1_Idle_00_original.png');pal=sorted({p[:3] for r in orig for p in r if p[3]})
def clean(p):return min(pal,key=lambda c:sum((c[k]-p[k])**2 for k in range(3)))+(255,) if p[3]>=128 else (0,0,0,0)
a=readpng(ROOT/'Sources/R1_bag_attempt1.png')
f=[[clean(a[y*40+20][x*40+20]) if y*40+20<len(a) and x*40+20<len(a[0]) else (0,0,0,0) for x in range(32)] for y in range(32)]
print('bag grid bbox',bbox(f))
for y,r in enumerate(f):print(y,''.join(' ' if not p[3] else 'W' if min(p[:3])>170 else 'R' if p[0]>p[1]*1.5 else '#' for p in r))
writepng(ROOT/'Sources/R1_bag_recovered_16x.png',nearest(f,16))
