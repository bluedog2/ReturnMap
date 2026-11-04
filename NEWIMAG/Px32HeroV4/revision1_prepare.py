from process import *
import shutil
B=ROOT/'Sources/Revision1_before'
B.mkdir(exist_ok=True)
for rel in ['validation.json','attempts.json','layout.json','README.md','Compare_3x.png','_1x/Hero_Run.png','_1x/Hero_Hit.png','Animations/Hero_Run_3x.gif','Animations/Hero_Hit_3x.gif']:
 p=B/rel;p.parent.mkdir(exist_ok=True,parents=True)
 if not p.exists():shutil.copyfile(ROOT/rel,p)
protected={str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in ROOT.rglob('*') if p.is_file() and ('JumpStart' in p.name or 'Dash' in p.name)}
(ROOT/'Sources/revision1_protected.json').write_text(json.dumps(protected,indent=2))
print('IDLE',bbox(IDLE),'HEAD',HB)
f,meta=recover('Run',4,2,8,5,4)
print('Run4',meta)
canvas=[[ZERO]*1024 for _ in range(128)]
for i,im in enumerate(f):
 for y,r in enumerate(im):canvas[y][i*128:i*128+len(r)]=r
writepng(ROOT/'Sources/Run_attempt4_recovered_3x.png',nearest(canvas,3))
# Compose existing generated Hit bodies with exact Idle head, translation only.
f,meta=recover('Hit',3,1,3,6,1)
frames=[];anns=[]
for i,im in enumerate(f):
 nx,ny=[(50,51),(49,53),(49,51)][i]
 pixels={(x,y):p for y,r in enumerate(im) for x,p in enumerate(r) if p[3] and y>=[51,43,51][i] and not (i==1 and x<36 and y<66)}
 hx,hy=nx-77,ny-73
 for (x,y),p in HEAD.items():pixels[x+hx,y+hy]=p
 xs=[x for x,y in pixels];ys=[y for x,y in pixels]
 dx=round(63.5-(min(xs)+max(xs))/2);dy=127-max(ys)
 out=[[ZERO]*128 for _ in range(128)]
 for (x,y),p in pixels.items():
  assert 0<=x+dx<128 and 0<=y+dy<128
  out[y+dy][x+dx]=p
 frames.append(out);anns.append({'head_translation':[hx+dx,hy+dy],'head_bbox':[HB[0]+hx+dx,HB[1]+hy+dy,HB[2]+hx+dx,HB[3]+hy+dy],'frame_translation':[dx,dy],'neck_source':[nx,ny]})
 print('Hit',i,bbox(out),anns[-1])
sheet=[sum((f[y] for f in frames),[]) for y in range(128)]
writepng(ROOT/'Sources/Hit_attempt5_composite_3x.png',nearest(sheet,3))
(ROOT/'Sources/Hit_attempt5_annotations.json').write_text(json.dumps(anns,indent=2))
