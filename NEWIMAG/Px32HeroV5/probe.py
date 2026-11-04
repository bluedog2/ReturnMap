from process import *
for name,c,r,n,k in [('Run',4,2,8,5),('Fall',2,1,2,10),('Land',3,1,3,6)]:
 fs,meta=recover(name,c,r,n,k)
 print(name,meta)
 out=[]
 for f in fs:
  print('top', [[(x,p[:3]) for x,p in enumerate(row) if p[3]] for row in f[:4]])
  out+=nearest(f,4)+[[(0,0,0,0)]*len(f[0])*4]*8
 width=max(map(len,out));out=[r+[(0,0,0,0)]*(width-len(r)) for r in out]
 writepng(ROOT/'Sources'/f'{name}_bodies_probe.png',out)
