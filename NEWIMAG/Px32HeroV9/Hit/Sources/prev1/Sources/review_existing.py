import sys
sys.dont_write_bytecode=True
import common as b
for kind,n in [('Hit',3),('Death',4)]:
 b.O=b.ROOT/'NEWIMAG/Px32HeroV9'/kind;b.S=b.O/'Sources'
 fs=[]
 for i in range(n):
  orig=b.read(b.S/f'Original_{i}.png');top=int(b.np.where(orig[:,:,3]>0)[0].min())
  a,r=b.recover(b.read(b.S/f'F{i}_generated1.png'),orig,top,top+30)
  b.save(f'Sources/F{i}_blocks.png',a);b.dump(f'Sources/F{i}_recovery.json',r);fs.append(a)
 b.save('Sources/Recovered_3x.png',b.zoom(b.np.concatenate(fs,1),3))
