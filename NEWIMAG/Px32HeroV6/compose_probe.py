from pixel_ops import *
full=readpng(ROOT.parent.parent/'Assets/_Project/ResourcceEX/Sprites/Px32/Player/Hero_Idle.png')
for name,a,k,n,c in [('Idle',3,8,6,3),('Run',2,7,8,4),('Run3',3,7,8,4)]:
 fs,mm=recover('Run' if name=='Run3' else name,c,2,n,k,a)
 canvas=[[ZERO]*(128*n) for _ in range(128)]
 for i,b in enumerate(fs):
  row=b[1];xs=[x for x,p in enumerate(row) if p[3]]; nx=round((min(xs)+max(xs))/2)
  dy=127-(len(b)-1);dx=77-nx
  head={(x,y):p for y,r in enumerate(full) for x,p in enumerate(r[(i if name=='Idle' else 0)*128:((i if name=='Idle' else 0)+1)*128]) if p[3] and headmask(x,y)}
  ht=dy+1-73
  f=[[ZERO]*128 for _ in range(128)]
  for y,r in enumerate(b):
   for x,p in enumerate(r):
    if p[3]: f[y+dy][x+dx]=p
  for (x,y),p in head.items():f[y+ht][x]=p
  bb=bbox(f); shift=round(64-(bb[0]+bb[2])/2)
  f=[[f[y][x-shift] if 0<=x-shift<128 else ZERO for x in range(128)] for y in range(128)]
  for y,r in enumerate(f):canvas[y][i*128:(i+1)*128]=r
  print(name,i,'body',len(b[0]),len(b),'neck',nx,'headtop',27+ht,'dxdy',dx+shift,dy)
 writepng(ROOT/'Sources'/f'{name}_probe_3x.png',nearest(canvas,3))
