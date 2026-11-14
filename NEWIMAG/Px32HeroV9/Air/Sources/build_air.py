from pathlib import Path
from PIL import Image, ImageDraw, ImageSequence
import numpy as np, json, importlib.util, hashlib, sys
sys.dont_write_bytecode=True
O=Path(__file__).resolve().parents[1]; S=O/'Sources'; ROOT=O.parents[2]
def read(p): return np.array(Image.open(p).convert('RGBA'))
def save(p,a): Image.fromarray(np.asarray(a,dtype='uint8')).save(O/p)
def dump(p,a): (O/p).write_text(json.dumps(a,ensure_ascii=False,indent=2),encoding='utf-8')
def zoom(a,n): return a.repeat(n,0).repeat(n,1)
spec=importlib.util.spec_from_file_location('jump_build',ROOT/'NEWIMAG/Px32HeroV9/JumpStart/Sources/build.py'); j=importlib.util.module_from_spec(spec);spec.loader.exec_module(j)
PAL=j.PAL
def polygon(points):
 im=Image.new('L',(128,128));ImageDraw.Draw(im).polygon(points,fill=255);return np.array(im)>0
def process(name,source,mask,pos,attempt=1,top=30,bottom=65):
 original=read(source); generated=read(S/f'{name}_generated{attempt}.png')
 blocks,rec=j.recover(generated,original,top,bottom)
 base=original.copy();base[mask]=blocks[mask]
 layer=j.layer(pos); visible=layer[:,:,3]>0
 final=base.copy();final[visible]=layer[visible]; edit=mask|visible
 for path,arr in [(f'{name}.png',final),(f'Sources/{name}_blocks.png',blocks),(f'Sources/{name}_bagless.png',base),(f'Sources/{name}_bagless_10x.png',zoom(base,10)),(f'Sources/{name}_layer.png',layer),(f'Sources/{name}_review_3x.png',zoom(final,3)),(f'Masks/{name}_generation.png',mask*255),(f'Masks/{name}_edit.png',edit*255)]:save(path,arr)
 colors={tuple(c) for c in final[final[:,:,3]>0,:3]};ys,xs=np.where(final[:,:,3]>0)
 meta=dict(source=str(source.relative_to(ROOT)),source_sha256=hashlib.sha256(source.read_bytes()).hexdigest(),recovery=rec,bag_position=pos,chain_position=[pos[0]+5,pos[1]-6],attempt=attempt,outside_mask_mismatch=int((np.any(final!=original,2)&~edit).sum()),colors=len(colors),outside_palette=len(colors-{tuple(c) for c in PAL}),alpha=list(map(int,np.unique(final[:,:,3]))),head_top=int(ys.min()),bottom=int(ys.max()),visual_review='pending')
 dump(f'Sources/{name}_metrics.json',meta)
 return final,meta
FALL_MASK=polygon([(18,74),(29,71),(31,65),(40,66),(46,70),(51,73),(59,77),(60,84),(55,89),(55,99),(39,102),(18,101)])
FALL1_MASK=polygon([(18,74),(29,71),(31,68),(40,68),(46,70),(51,73),(59,77),(60,84),(55,89),(52,107),(18,109)])
if __name__=='__main__':
 import sys
 if sys.argv[1]=='fall0':
  process('Fall_F0',S/'Fall_original0.png',FALL_MASK,[51,84])
 if sys.argv[1]=='fall2':
  process('Fall_F2',S/'Fall_original1.png',FALL1_MASK,[51,85])
