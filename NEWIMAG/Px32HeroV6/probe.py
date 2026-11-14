from pixel_ops import *
for name,a,k,n,c in [('Idle',3,8,6,3),('Run',2,7,8,4)]:
 fs,mm=recover(name,c,2,n,k,a)
 for i,f in enumerate(fs):
  writepng(ROOT/'Sources'/f'{name}_body{i}_6x.png',nearest(f,6))
 print(name,mm)
