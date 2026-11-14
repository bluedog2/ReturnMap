import build as b
from build import np,Image,Path,json
import sys
def main(kind):
 b.O=b.ROOT/'NEWIMAG/Px32HeroV9'/kind;b.S=b.O/'Sources'
 O,S=b.O,b.S
 order=['F0','I0','F1','F2'] if kind=='Land' else ['F0','F1','F2','F3','I3','F4']
 j=0 if kind=='Land' else 3;name='I'+str(j)
 orig=b.read(S/f'F{j}_bagless.png');g=b.read(S/f'{name}_generated1.png')
 top=int(np.where(orig[:,:,3]>0)[0].min());blocks,r=b.recover(g,orig,top,min(top+35,74))
 m=np.array(Image.open(O/f'Masks/F{j}_generation.png'))>0
 base=orig.copy();base[m]=blocks[m]
 for p,a in [(f'Sources/{name}_blocks.png',blocks),(f'Sources/{name}_bagless.png',base),(f'Masks/{name}_generation.png',m*255)]:b.save(p,a)
 b.dump(f'Sources/{name}_recovery.json',r)
 if kind=='Dash':
  g=b.read(S/'Chain_generated1.png');ix=np.arange(16)*78+39;a=g[ix[:,None],ix[None,:]].copy()
  ids=((a[:,:,:3].astype('int32')[:,:,None,:]-b.PAL[None,None,:,:])**2).sum(3).argmin(2);a[:,:,:3]=b.PAL[ids];a[:,:,3]=(a[:,:,3]>=128)*255;a[a[:,:,3]==0]=0
  chain=a[5:11,5:10];b.save('Sources/Chain_tilt.png',chain);b.dump('Sources/Chain_recovery.json',dict(pitch=78,origin=[0,0],crop=[5,5,10,11],source='Chain_generated1.png'))
 # Positions are review candidates. Final review corrections are explicitly recorded below.
 hips=([[62,102],[62,102],[62,94],[63,90]] if kind=='Land' else [[61,94],[63,93],[66,97],[65,96],[65,96],[63,88]])
 prev=([[62,102]]+hips[:-1] if kind=='Land' else [[63,96]]+hips[:-1])
 inertia=([[0,0],[0,2],[0,1],[0,0]] if kind=='Land' else [[-2,0],[-3,0],[-3,0],[-3,0],[-3,0],[-2,0]])
 corrections=([[0,0],[0,0],[0,-6],[0,0]] if kind=='Land' else [[0,0],[0,0],[0,0],[0,0],[0,0],[0,-6]])
 fs=[];meta={}
 for k,name in enumerate(order):
  refi=int(name[1:]);ref=b.read(S/f'Original_{refi}.png');base=b.read(S/f'{name}_bagless.png');m=np.array(Image.open(O/f'Masks/{name}_generation.png'))>0
  x=prev[k][0]-11+inertia[k][0]+corrections[k][0];y=prev[k][1]-2+inertia[k][1]+corrections[k][1]
  lay=b.layer((x,y))
  if kind=='Dash':
   lay[:]=0
   for part,xx,yy in [(chain,x+5,y-6),(b.bag,x,y)]:
    v=lay[yy:yy+len(part),xx:xx+part.shape[1]];u=part[:,:,3]>0;v[u]=part[u]
  bm=lay[:,:,3]>0;out=base.copy();out[bm]=lay[bm];edit=m|bm
  for p,a in [(f'{kind}_{name}.png',out),(f'Sources/{name}_layer.png',lay),(f'Masks/{name}_edit.png',edit*255),(f'Sources/{name}_review_3x.png',b.zoom(out,3))]:b.save(p,a)
  outside=int((np.any(out!=ref,2)&~edit).sum());assert outside==0
  meta[name]=dict(source_frame=refi,hip=hips[k],previous_hip=prev[k],offset=[-11,-2],inertia=inertia[k],review_translation=corrections[k],bag_position=[x,y],outside_mask_mismatch=outside)
  fs.append(out)
 b.save(f'Hero_{kind}_{len(fs)}.png',np.concatenate(fs,1));b.save('Sheet_3x.png',b.zoom(np.concatenate(fs,1),3));b.save('Waist_8x.png',b.zoom(np.concatenate([f[75:123,20:108] for f in fs],1),8))
 b.dump('Sources/placement.json',dict(order=order,frames=meta,entry_anchor='Land contact pose initialized; not previous Fall anchor' if kind=='Land' else 'previous Run I7 body hip (63,96)'))
if __name__=='__main__':main(sys.argv[1])
