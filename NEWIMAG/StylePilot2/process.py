import ast, json, math, struct, zlib, re, shutil
from pathlib import Path
from collections import Counter, deque
ROOT=Path(__file__).resolve().parent
REPO=ROOT.parent.parent
# Reuse PNG codec only; no prior drawing or sprite-generation code is executed.
tree=ast.parse((REPO/'NEWIMAG/StylePilot/build.py').read_text(encoding='utf-8'))
for node in tree.body:
    if isinstance(node,ast.FunctionDef) and node.name in ('read','save'):
        exec(compile(ast.Module(body=[node],type_ignores=[]),'<png-codec>','exec'))
def canvas(w,h,c=(0,0,0,0)): return [[c]*w for _ in range(h)]
def crop(im,b):
    x,y,X,Y=b
    return [r[x:X] for r in im[y:Y]]
def bbox(im):
    pts=[(x,y) for y,r in enumerate(im) for x,p in enumerate(r) if p[3]]
    return [min(x for x,y in pts),min(y for x,y in pts),max(x for x,y in pts)+1,max(y for x,y in pts)+1] if pts else None
def paste(dst,src,x,y):
    for j,r in enumerate(src):
        for i,p in enumerate(r):
            if p[3] and 0<=y+j<len(dst) and 0<=x+i<len(dst[0]): dst[y+j][x+i]=p
def zoom(im,n): return [[p for p in r for _ in range(n)] for r in im for _ in range(n)]
P=[tuple(bytes.fromhex(s)) for s in re.findall(r'^#([0-9A-Fa-f]{6})$',(REPO/'NEWIMAG/StylePilot/palette_v2.txt').read_text(encoding='utf-8'),re.M)]
def norm(p):
    r,g,b,a=p
    if a<128 or (g>110 and g>r*1.5 and g>b*1.5): return (0,0,0,0)
    return min(P,key=lambda c:sum((c[i]-p[i])**2 for i in range(3)))+(255,)
def edge_profile(im,axis):
    h,w=len(im),len(im[0]); vals=[0.0]*(w if axis==0 else h)
    # Strong transitions only, with small anti-alias fluctuations ignored.
    if axis==0:
        for y in range(0,h,4):
            row=im[y]
            for x in range(1,w):
                p,q=row[x],row[x-1]
                d=max(abs(p[i]-q[i]) for i in range(4))
                if d>40: vals[x]+=d
    else:
        for y in range(1,h):
            row,prev=im[y],im[y-1]
            for x in range(0,w,4):
                d=max(abs(row[x][i]-prev[x][i]) for i in range(4))
                if d>40: vals[y]+=d
    return vals
def fit(profile,low,high):
    # Periodic boundary energy, searches k at 0.1 source-pixel precision.
    total=sum(profile) or 1; best=None
    for ki in range(round(low*10),round(high*10)+1):
        k=ki/10; bins=[0.0]*max(1,round(k*2))
        for pos,e in enumerate(profile):
            bins[round((pos%k)/k*len(bins))%len(bins)]+=e
        for phase_i in range(len(bins)):
            phase=phase_i/len(bins)*k
            energy=sum(v for i,v in enumerate(bins) if min((i-phase_i)%len(bins),(phase_i-i)%len(bins))<=1)
            ratio=energy/total
            # Correct for chance coverage; avoids preference for tiny periods.
            score=(ratio-3/len(bins))/(1-3/len(bins)) if len(bins)>3 else 0
            if best is None or score>best['score']: best=dict(k=k,phase=phase,score=score,edge_fraction=ratio)
    return best
def process(a):
    im=read(Path(a['path'])); h,w=len(im),len(im[0]); n={'A':64,'B':96,'C':128}[a['name']]
    # Search the block size range around the requested logical canvas.
    expected=w/n
    ex,ey=edge_profile(im,0),edge_profile(im,1)
    # One square grid, shared period; independent phase permitted per axis.
    candidates=[]
    for ki in range(round(expected*.65*10),round(expected*1.35*10)+1):
        k=ki/10
        xfit,yfit=fit(ex,k,k),fit(ey,k,k)
        candidates.append((xfit['score']+yfit['score'],xfit,yfit))
    _,fx,fy=max(candidates,key=lambda v:v[0])
    # Never height-normalize or rescale a restored character.
    xs=[int(fx['phase']+(i+.5)*fx['k']) for i in range(math.ceil(w/fx['k']))]
    ys=[int(fy['phase']+(i+.5)*fy['k']) for i in range(math.ceil(h/fy['k']))]
    sampled=[[norm(im[y][x]) for x in xs if x<w] for y in ys if y<h]
    # Remove only fully detached 1-2 pixel alpha components, never repaint contours.
    seen=set(); removed=0
    for y,row in enumerate(sampled):
        for x,p in enumerate(row):
            if not p[3] or (x,y) in seen: continue
            todo=[(x,y)]; seen.add((x,y)); comp=[]
            while todo:
                u,v=todo.pop(); comp.append((u,v))
                for dv in (-1,0,1):
                    for du in (-1,0,1):
                        q,t=u+du,v+dv
                        if 0<=t<len(sampled) and 0<=q<len(sampled[0]) and (q,t) not in seen and sampled[t][q][3]:
                            seen.add((q,t));todo.append((q,t))
            if len(comp)<=2:
                for u,v in comp: sampled[v][u]=(0,0,0,0)
                removed+=len(comp)
    b=bbox(sampled); spr=crop(sampled,b)
    fits=len(spr)<=n and len(spr[0])<=n
    result=canvas(n,n)
    if fits: paste(result,spr,(n-len(spr[0]))//2,n-len(spr))
    out=ROOT/'Candidates';out.mkdir(exist_ok=True)
    stem=f"Hero_{a['name']}_attempt{a['attempt']}"
    save(out/(stem+'_grid.png'),zoom(spr,6))
    if fits: save(out/(stem+'_1x.png'),result)
    return dict(name=a['name'],attempt=a['attempt'],source_size=[w,h],grid_x=fx,grid_y=fy,restored_size=[len(spr[0]),len(spr)],fits_canvas=fits,removed_isolated_pixels=removed,bbox=bbox(result) if fits else None)
if __name__=='__main__':
    attempts=json.loads((ROOT/'attempts.json').read_text(encoding='utf-8'))
    reports=[]
    for a in attempts:
        r=process(a);reports.append(r);print(json.dumps(r),flush=True)
    (ROOT/'grid_report.json').write_text(json.dumps(reports,indent=2),encoding='utf-8')
