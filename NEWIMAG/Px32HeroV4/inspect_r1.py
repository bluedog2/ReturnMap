from process import *
f,meta=recover('Hit',3,2,3,6,4)
print(meta)
out=[[ZERO]*384 for _ in range(128)]
for i,im in enumerate(f):
 for y,r in enumerate(im):out[y][i*128:i*128+len(r)]=r
writepng(ROOT/'Sources/Hit_attempt4_recovered_6x.png',nearest(out,6))
