from pixel_io import *
import hashlib
ZERO=(0,0,0,0)
BASE=ROOT.parent.parent/'Assets/_Project/ResourcceEX/Sprites/Px32/Player'
def poly(x,y,points):
    hit=False
    for (a,b),(c,d) in zip(points,points[1:]+points[:1]):
        if (b>y)!=(d>y) and x < (c-a)*(y-b)/(d-b)+a: hit=not hit
    return hit
def atlas(frames,cols):
    return [sum([frames[j][y] for j in range(k,k+cols)],[]) for k in range(0,len(frames),cols) for y in range(128)]
def prepare():
    meta={}
    for name,n,cols in [('Idle',6,3),('Run',8,4)]:
        src=readpng(BASE/f'Hero_{name}.png'); frames=[[r[i*128:(i+1)*128] for r in src] for i in range(n)]
        masks=[];overlays=[];spec=[]
        for i,f in enumerate(frames):
            if name=='Idle':
                dy=[0,0,0,0,0,0][i]
                polygons=[[(53,69),(64,69),(72,78),(69,88),(62,95),(61,102),(40,103),(39,87),(48,84),(48,79)],[(43,87),(60,87),(64,98),(63,113),(40,113),(40,96)]]
                bag=[40,94+(1 if i in (2,3) else 0)]
            else:
                # Camera-side sleeve/hand and old bag; no head, torso or existing legs.
                dx=[0,0,-1,0,0,1,1,1][i];dy=[0,1,-3,-1,0,-2,-1,-4][i]
                polygons=[[(45+dx,81+dy),(55+dx,79+dy),(65+dx,84+dy),(63+dx,94+dy),(56+dx,101+dy),(54+dx,110+dy),(30+dx,110+dy),(28+dx,94+dy),(39+dx,90+dy)],[(32+dx,90+dy),(50+dx,88+dy),(57+dx,97+dy),(54+dx,113+dy),(30+dx,113+dy)]]
                bag=[[31,94],[32,95],[32,93],[30,92],[31,94],[32,94],[33,92],[31,92]][i]
            mask=[[(255,255,255,255) if any(poly(x+.5,y+.5,p) for p in polygons) else (0,0,0,255) for x in range(128)] for y in range(128)]
            # Predeclared exact destination rectangle is permitted insert area.
            for y in range(bag[1],bag[1]+19):
                for x in range(bag[0],bag[0]+23):mask[y][x]=(255,255,255,255)
            masks.append(mask)
            over=[[(round(p[0]*.5+127),round(p[1]*.5),round(p[2]*.5+127),255) if mask[y][x][0] else p for x,p in enumerate(r)] for y,r in enumerate(f)]
            overlays.append(over)
            writepng(ROOT/'Masks'/f'{name}_{i:02}.png',mask)
            writepng(ROOT/'Sources'/f'{name}_{i:02}_original_4x.png',nearest(f,4))
            spec.append({'frame':i,'polygons':polygons,'bag_xy':bag,'bag_rect':[bag[0],bag[1],bag[0]+23,bag[1]+19]})
        for suffix,fs in [('original',frames),('mask',masks),('overlay',overlays)]:writepng(ROOT/'Sources'/f'{name}_{suffix}_4x.png',nearest(atlas(fs,cols),4))
        meta[name]={'source':str(BASE/f'Hero_{name}.png'),'sha256':hashlib.sha256((BASE/f'Hero_{name}.png').read_bytes()).hexdigest(),'cols':cols,'frames':spec}
    (ROOT/'Sources/mask_spec.json').write_text(json.dumps(meta,indent=2),encoding='utf-8')
if __name__=='__main__':prepare()
