import sys
sys.dont_write_bytecode=True
import common as b
from common import np
REGIONS={
'Hit':[
[[(51,84),(58,79),(62,74),(73,77),(74,88),(66,97),(65,108),(49,108)],[(82,83),(94,85),(98,96),(91,103),(82,99)]],
[[(55,88),(62,83),(66,80),(77,83),(77,94),(68,104),(66,112),(51,112)],[(86,84),(97,83),(104,87),(104,94),(89,96)]],
[[(51,84),(58,79),(62,74),(73,77),(74,88),(67,97),(65,108),(49,108)],[(82,83),(94,85),(98,96),(91,103),(82,99)]]],
'Death':[
[[(36,83),(45,78),(50,70),(63,72),(67,82),(60,95),(54,105),(35,105)],[(79,82),(91,84),(98,98),(94,106),(80,101)]],
[[(31,86),(40,82),(47,78),(59,78),(64,88),(55,103),(52,112),(30,112)],[(76,94),(88,95),(95,106),(94,114),(79,109)]],
[[(28,107),(39,99),(48,97),(57,97),(67,102),(69,114),(58,125),(53,127),(28,127)],[(78,109),(86,108),(93,118),(94,127),(79,127)]],
[[(35,111),(45,104),(52,99),(65,99),(75,107),(76,119),(80,127),(34,127)],[(81,113),(93,112),(102,121),(103,127),(83,127)]],
[[(39,114),(47,108),(53,103),(65,102),(76,108),(79,118),(79,127),(39,127)]],
[[(37,115),(47,108),(53,102),(66,101),(77,109),(80,119),(81,127),(37,127)]]]
}
def main(kind):
 b.O=b.ROOT/'NEWIMAG/Px32HeroV9'/kind;b.S=b.O/'Sources';(b.O/'Masks').mkdir(exist_ok=True)
 fs=[]
 for i,ps in enumerate(REGIONS[kind]):
  o=b.read(b.S/f'Original_{i}.png');top=int(np.where(o[:,:,3]>0)[0].min())
  a,r=b.recover(b.read(b.S/f'F{i}_generated1.png'),o,top,top+30)
  m=np.logical_or.reduce([b.poly(p) for p in ps])
  if kind=='Death' and i==1:m[90:103,28:34]=True
  base=o.copy();base[m]=a[m]
  b.save(f'Sources/F{i}_blocks.png',a);b.save(f'Sources/F{i}_bagless.png',base);b.save(f'Sources/F{i}_bagless_10x.png',b.zoom(base,10));b.save(f'Masks/F{i}_generation.png',m*255);b.dump(f'Sources/F{i}_recovery.json',r);fs.append(base)
 b.save('Sources/Bagless_3x.png',b.zoom(np.concatenate(fs,1),3))
 changes=[int(np.any(fs[i]!=fs[i+1],2).sum()) for i in range(len(fs)-1)]
 b.dump('Sources/key_changes.json',dict(changes=changes,insert_after=sorted(np.argsort(changes)[-(1 if kind=='Hit' else 4):].tolist())))
 print(kind,changes)
if __name__=='__main__':main(sys.argv[1])
