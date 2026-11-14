from pixel_io import *
fs=[readpng(ROOT/'Sources'/f'R1_Run_{i:02}_original.png') for i in range(8)]
a=[[(0,0,0,0)]*1536 for _ in range(1024)]
for i,f in enumerate(fs):
 for y,r in enumerate(nearest(f,3)):a[(i//4)*512+y][(i%4)*384:(i%4+1)*384]=r
writepng(ROOT/'Sources/R1_Run_reference.png',a)
for n in ['R1_bag_attempt1','R1_Idle_attempt2','R1_Run_attempt1']:
 a=readpng(ROOT/'Sources'/f'{n}.png');print(n,len(a[0]),len(a))
