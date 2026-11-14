from pixel_io import *
for name,n in [('Idle',6),('Run',8)]:
 for i in range(n):
  a=readpng(ROOT/'Sources'/f'{name}_{i:02}_original_4x.png'); f=[[a[y*4][x*4] for x in range(128)] for y in range(128)]
  assert a==nearest(f,4)
  writepng(ROOT/'Sources'/f'R1_{name}_{i:02}_original.png',f)
  if i==0:
   print(name)
   for y in range(76,108):
    print(y,''.join(' ' if not p[3] else 'W' if min(p[:3])>170 else 'R' if p[0]>p[1]*1.5 else 's' if p[0]>150 else '#' for p in f[y][25:100]))
