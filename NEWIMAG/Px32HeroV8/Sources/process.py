from pixel_io import *
import sys
BASE=ROOT.parent
Z=(0,0,0,0)
orig=readpng(ROOT/'original.png')
PAL=sorted({p[:3] for r in orig for p in r if p[3]})
def poly(x,y,ps):
 hit=False
 for (a,b),(c,d) in zip(ps,ps[1:]+ps[:1]):
  if (b>y)!=(d>y) and x<(c-a)*(y-b)/(d-b)+a:hit=not hit
 return hit
def clean(p):return min(PAL,key=lambda c:sum((c[k]-p[k])**2 for k in range(3)))+(255,) if p[3]>=128 else Z
if __name__=='__main__':
 name=sys.argv[1]; attempt=int(sys.argv[2]); pitch=int(sys.argv[3]) if len(sys.argv)>3 else 8
 a=readpng(ROOT/f'{name}{attempt}_generated.png')
 recovered=[[clean(a[y*pitch+pitch//2][x*pitch+pitch//2]) if y*pitch+pitch//2<len(a) and x*pitch+pitch//2<len(a[0]) else Z for x in range(128)] for y in range(128)]
 # Semantic old sleeve/carried bag plus proposed arm and hip insert. Head, torso front and exposed lower legs remain locked.
 areas=[[(53,69),(64,69),(72,78),(72,92),(64,104),(36,104),(36,86),(49,83),(49,79)]]
 if name=='A':areas.append([(61,72),(68,75),(78,83),(78,88),(69,88),(61,80)])
 if name=='C':areas.append([(53,71),(61,73),(63,84),(54,87),(44,82),(44,77)])
 mask=[[any(poly(x+.5,y+.5,p) for p in areas) for x in range(128)] for y in range(128)]
 cand=[[recovered[y][x] if mask[y][x] else orig[y][x] for x in range(128)] for y in range(128)]
 writepng(ROOT/f'{name}{attempt}_recovered.png',recovered)
 writepng(ROOT/f'{name}{attempt}_composite.png',cand)
 writepng(ROOT/f'{name}{attempt}_review_3x.png',nearest([o+c for o,c in zip(orig,cand)],3))
 writepng(BASE/'Masks'/f'Hero_Idle0_{name}.png',[[((255,)*4 if p else (0,0,0,255)) for p in r] for r in mask])
 print(json.dumps({'candidate':name,'attempt':attempt,'generated_size':[len(a[0]),len(a)],'pitch':pitch,'colors':len({p[:3] for r in cand for p in r if p[3]}),'outside_mismatch':sum(cand[y][x]!=orig[y][x] for y in range(128) for x in range(128) if not mask[y][x])}))
