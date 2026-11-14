import sys
sys.dont_write_bytecode=True
import common as b
from common import np,Image,json
ANCHORS={'Hit':[(69,94),(71,99),(69,94)],'Death':[(63,92),(61,100),(64,115),(51,112),(49,115),(48,115)]}
def main(kind):
 b.O=b.ROOT/'NEWIMAG/Px32HeroV9'/kind;b.S=b.O/'Sources';O,S=b.O,b.S
 insert=json.loads((S/'key_changes.json').read_text())['insert_after'];order=[]
 for i in range(len(ANCHORS[kind])):
  order.append('F'+str(i))
  if i in insert:order.append('I'+str(i))
 for i in insert:
  orig=b.read(S/f'F{i}_bagless.png');top=int(np.where(orig[:,:,3]>0)[0].min());a,r=b.recover(b.read(S/f'I{i}_generated1.png'),orig,top,top+30)
  m=np.array(Image.open(O/f'Masks/F{i}_generation.png'))>0;base=orig.copy();base[m]=a[m]
  b.save(f'Sources/I{i}_blocks.png',a);b.save(f'Sources/I{i}_bagless.png',base);b.save(f'Masks/I{i}_generation.png',m*255);b.dump(f'Sources/I{i}_recovery.json',r)
 src=b.read(S/'F0_bagless.png');top=int(np.where(src[:,:,3]>0)[0].min())
 arm,rec=b.recover(b.read(S/'F0_generated2.png'),src,top,top+30)
 b.save('Sources/F0_blocks2.png',arm);b.dump('Sources/F0_recovery2.json',rec)
 arm_mask=b.poly([(51,84),(58,77),(62,73),(77,73),(80,85),(77,93),(67,99),(65,108),(49,108)]) if kind=='Hit' else b.poly([(50,70),(66,70),(70,89),(62,103),(33,103),(33,91),(43,81)])
 b.save('Masks/Arm_reuse_source.png',arm_mask*255)
 placement={'order':order,'offset':[-11,-2],'frames':{}};fs=[]
 for j,name in enumerate(order):
  i=int(name[1:]);anchor=ANCHORS[kind][i];prev=ANCHORS[kind][int(order[max(0,j-1)][1:])];impulse=[2,0] if kind=='Hit' and name in ['I0','F1'] else [0,0]
  ideal=[prev[0]-11+impulse[0],prev[1]-2];pos=ideal.copy()
  # Candidate clips are prevented by floor contact. Visual corrections added separately.
  fixes={'Hit':{'F2':[58,92]},'Death':{'F1':[50,97],'F2':[53,110],'I2':[53,112],'F3':[40,111],'I3':[40,112],'F4':[32,112],'F5':[31,112]}}
  pos=fixes[kind].get(name,pos);pos[1]=min(pos[1],112)
  base=b.read(S/f'{name}_bagless.png')
  m=np.array(Image.open(O/f'Masks/{name}_generation.png'))>0
  reuse=None
  if kind=='Hit' or name in ['F0','I0']:
   dx,dy=({'F0':(0,0),'I0':(1,0),'F1':(3,6),'F2':(0,1)}[name] if kind=='Hit' else ((0,0) if name=='F0' else (-1,0)))
   yy,xx=np.where(arm_mask);base[yy+dy,xx+dx]=arm[yy,xx];m[yy+dy,xx+dx]=True;reuse=[dx,dy]
  b.save(f'Sources/{name}_final_bagless.png',base);b.save(f'Masks/{name}_composition.png',m*255)
  lay=b.layer(pos);lm=lay[:,:,3]>0;out=base.copy();out[lm]=lay[lm]
  b.save(f'{kind}_{name}.png',out);b.save(f'Sources/{name}_layer.png',lay);b.save(f'Masks/{name}_edit.png',(m|lm)*255)
  placement['frames'][name]=dict(source_frame=i,waist=anchor,previous_waist=prev,impulse=impulse,strict_position=ideal,bag_position=pos,correction=[pos[0]-ideal[0],pos[1]-ideal[1]],arm_reuse_translation=reuse)
  fs.append(out)
 sheet=np.concatenate(fs,1);b.save(f'Hero_{kind}_{len(fs)}.png',sheet);b.save('Sheet_3x.png',b.zoom(sheet,3));b.save('Waist_8x.png',b.zoom(np.concatenate([a[76:128,24:100] for a in fs],1),8));b.dump('Sources/placement.json',placement)
if __name__=='__main__':main(sys.argv[1])
