"""Sprite restoration only: center sampling, palette mapping, translation, approved idle cutouts.
No generated geometry, drawing, rotation, warping, or silhouette rescaling.
PNG/APNG codec uses Python standard library, with no external dependencies.
"""
import struct, zlib, json, re, math
from pathlib import Path
from functools import lru_cache
ROOT=Path(__file__).resolve().parent
REPO=ROOT.parent.parent
PAL=[tuple(bytes.fromhex(s)) for s in re.findall(r'^#([0-9A-Fa-f]{6})\s*$',(ROOT.parent/'palette_v2.txt').read_text('utf-8'),re.M)]
ZERO=(0,0,0,0)
def readpng(path):
    data=Path(path).read_bytes(); pos=8; compressed=b''; palette=None; trans=b''
    while pos<len(data):
        n=struct.unpack('>I',data[pos:pos+4])[0]; t=data[pos+4:pos+8]; v=data[pos+8:pos+8+n]; pos+=n+12
        if t==b'IHDR': w,h,depth,ctype,_,_,inter=struct.unpack('>IIBBBBB',v)
        if t==b'IDAT': compressed+=v
        if t==b'PLTE': palette=[tuple(v[i:i+3]) for i in range(0,len(v),3)]
        if t==b'tRNS': trans=v
    assert depth==8 and inter==0,(path,depth,inter)
    channels={2:3,6:4,3:1,0:1,4:2}[ctype]; stride=w*channels; raw=zlib.decompress(compressed); prev=bytearray(stride); out=[]; pos=0
    def paeth(a,b,c):
        p=a+b-c; ds=[abs(p-a),abs(p-b),abs(p-c)]; return [a,b,c][ds.index(min(ds))]
    for y in range(h):
        f=raw[pos]; pos+=1; row=bytearray(raw[pos:pos+stride]); pos+=stride
        for i in range(stride):
            a=row[i-channels] if i>=channels else 0; b=prev[i]; c=prev[i-channels] if i>=channels else 0
            row[i]=(row[i]+(0 if f==0 else a if f==1 else b if f==2 else (a+b)//2 if f==3 else paeth(a,b,c)))&255
        pixels=[]
        for i in range(0,stride,channels):
            p=tuple(row[i:i+channels])
            if ctype==2: p+= (255,)
            elif ctype==3: p=palette[p[0]]+(trans[p[0]] if p[0]<len(trans) else 255,)
            elif ctype==0: p=(p[0],p[0],p[0],255)
            elif ctype==4: p=(p[0],p[0],p[0],p[1])
            pixels.append(p)
        out.append(pixels); prev=row
    return out
def chunk(t,v): return struct.pack('>I',len(v))+t+v+struct.pack('>I',zlib.crc32(t+v)&0xffffffff)
def packed(im): return zlib.compress(b''.join(b'\0'+bytes(v for p in row for v in p) for row in im),9)
def header(w,h): return b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',w,h,8,6,0,0,0))
def save(im,path): Path(path).write_bytes(header(len(im[0]),len(im))+chunk(b'IDAT',packed(im))+chunk(b'IEND',b''))
def upscale(im,k): return [[p for p in row for _ in range(k)] for row in im for _ in range(k)]
def apng(frames,path,fps,loop=True):
    h=len(frames[0]); w=len(frames[0][0]); data=header(w,h)+chunk(b'acTL',struct.pack('>II',len(frames),0 if loop else 1)); seq=0
    for i,im in enumerate(frames):
        data+=chunk(b'fcTL',struct.pack('>IIIIIHHBB',seq,w,h,0,0,1,fps,0,0)); seq+=1
        if i==0: data+=chunk(b'IDAT',packed(im))
        else: data+=chunk(b'fdAT',struct.pack('>I',seq)+packed(im)); seq+=1
    Path(path).write_bytes(data+chunk(b'IEND',b''))
@lru_cache(maxsize=131072)
def clean(p):
    r,g,b,a=p
    if a<128 or (g>100 and g>r*1.45 and g>b*1.45): return ZERO
    q=min(PAL,key=lambda q:sum((x-y)**2 for x,y in zip(p[:3],q)))
    return q+(255,)
def blank(w=96,h=96): return [[ZERO for _ in range(w)] for _ in range(h)]
def bbox(im):
    coords=[(x,y) for y,row in enumerate(im) for x,p in enumerate(row) if p[3]]
    return min(x for x,y in coords),min(y for x,y in coords),max(x for x,y in coords),max(y for x,y in coords)
def translate(im,dx,dy):
    out=blank(); lost=0
    for y,row in enumerate(im):
        for x,p in enumerate(row):
            if p[3]:
                if 0<=x+dx<96 and 0<=y+dy<96: out[y+dy][x+dx]=p
                else: lost+=1
    assert lost==0,('translation would clip',lost)
    return out
def align(im):
    x0,y0,x1,y1=bbox(im); return translate(im,round(47.5-(x0+x1)/2),95-y1)
def remove_dots(im):
    seen=set(); removed=0
    for y in range(96):
        for x in range(96):
            if not im[y][x][3] or (x,y) in seen: continue
            stack=[(x,y)]; seen.add((x,y)); comp=[]
            while stack:
                a,b=stack.pop(); comp.append((a,b))
                for dy in (-1,0,1):
                    for dx in (-1,0,1):
                        c,d=a+dx,b+dy
                        if 0<=c<96 and 0<=d<96 and im[d][c][3] and (c,d) not in seen: seen.add((c,d)); stack.append((c,d))
            if len(comp)<=2:
                for a,b in comp: im[b][a]=ZERO; removed+=1
    return removed
SPECS={'Idle':(6,8,3,2),'Run':(8,12,4,2),'JumpStart':(2,12,2,1),'Fall':(2,8,2,1),'Land':(3,12,3,1),'Dash':(4,20,2,2),'Hit':(2,10,2,1),'Death':(6,10,3,2)}
def extract(name,attempt,cols,rows):
    src=readpng(ROOT/f'Sources/Hero_{name}_attempt{attempt}.png'); h=len(src); w=len(src[0]); frames=[]; metrics=[]
    for i in range(cols*rows):
        # Recover logical cells by sampling block centers, never interpolate artwork.
        yoffset=28 if name=='Death' and attempt==1 and i//cols==1 else 0
        def sample(x,y):
            sy=int(((i//cols)*96+y+.5)*h/(rows*96))+yoffset
            sx=int(((i%cols)*96+x+.5)*w/(cols*96))
            return clean(src[sy][sx]) if 0<=sy<h and 0<=sx<w else ZERO
        im=[[sample(x,y) for x in range(96)] for y in range(96)]
        dots=remove_dots(im); b=bbox(im); frames.append(align(im)); metrics.append({'source_bbox':b,'removed_isolated_pixels':dots})
    return frames,{'source_size':[w,h],'sample_pitch':[w/(cols*96),h/(rows*96)],'method':'uniform requested-cell grid block-center sampling','frames':metrics}
def idle():
    src=readpng(REPO/'NEWIMAG/StylePilot2/_1x/Hero_B.png'); src=[[clean(p) for p in row] for row in src]; src=align(src); frames=[]
    # Move existing upper-body pixels 1px up/down; lower-body pixels remain intact.
    # Ponytail left-tip and jacket hem cutouts shift horizontally by at most 1px.
    for dy,tail,hem in [(0,0,0),(1,0,0),(1,-1,0),(1,-1,-1),(0,-1,-1),(0,0,-1)]:
        out=blank()
        for y,row in enumerate(src):
            for x,p in enumerate(row):
                if not p[3]: continue
                oy=dy if y<64 else 0
                ox=tail if x<38 and 27<=y<55 else hem if x<47 and 56<=y<72 else 0
                out[y+oy][x+ox]=p
        frames.append(align(out))
    return frames,{'method':'approved Hero_B cutout translations only, upper body +1px, left ponytail and jacket hem -1px','source_size':[96,96]}
def validate(frames,name):
    stats=[]
    for i,im in enumerate(frames):
        x0,y0,x1,y1=bbox(im); colors={p[:3] for row in im for p in row if p[3]}; edges={'top':sum(p[3]>0 for p in im[0]),'left':sum(row[0][3]>0 for row in im[:-1]),'right':sum(row[-1][3]>0 for row in im[:-1])}
        stats.append({'frame':i,'bbox':[x0,y0,x1,y1],'bottom_row':y1,'center_x':(x0+x1)/2,'height':y1-y0+1,'width':x1-x0+1,'color_count':len(colors),'off_palette':len(colors-set(PAL)),'pure_black':(0,0,0) in colors,'edge_opaque':edges,'non_binary_alpha':sum(p[3] not in (0,255) for row in im for p in row)})
    mh=sum(s['height'] for s in stats)/len(stats); mc=sum(s['center_x'] for s in stats)/len(stats)
    for s in stats:
        s['height_deviation']=round(s['height']-mh,3); s['center_deviation']=round(s['center_x']-mc,3)
        s['pass']=not(s['off_palette'] or s['pure_black'] or s['non_binary_alpha'] or any(s['edge_opaque'].values())) and (s['bottom_row']==95 or name in ('Fall','JumpStart')) and (name=='Death' or abs(s['height']-mh)<=2 and abs(s['center_x']-mc)<=2)
    return {'pass':all(s['pass'] for s in stats),'mean_height':mh,'mean_center_x':mc,'frames':stats}
def main():
    manifest=json.loads((ROOT/'attempts.json').read_text('utf-8-sig')); allframes={}; report={}
    for name,(count,fps,cols,rows) in SPECS.items():
        if name=='Idle': frames,restoration=idle(); manifest[name]={'attempt_count':0,'selected':'approved-cutout','attempts':[],'method':restoration['method']}
        else:
            entry=manifest[name]; frames,restoration=extract(name,entry['selected'],cols,rows); entry['attempt_count']=len(entry['attempts'])
        report[name]=validate(frames,name); report[name]['restoration']=restoration; report[name]['fps']=fps
        sheet=[sum((fr[y] for fr in frames),[]) for y in range(96)]
        save(sheet,ROOT/f'_1x/Hero_{name}.png'); save(upscale(sheet,8),ROOT/f'Player/Hero_{name}.png')
        apng([upscale(fr,3) for fr in frames],ROOT/f'Animations/Hero_{name}_3x.apng',fps,name in ('Idle','Run','Fall','Dash'))
        allframes[name]=frames
        print(name,'PASS' if report[name]['pass'] else 'FAIL','height',[s['height'] for s in report[name]['frames']],'width',[s['width'] for s in report[name]['frames']])
    reference=align([[clean(p) for p in row] for row in readpng(REPO/'NEWIMAG/StylePilot2/_1x/Hero_B.png')]); preview=blank(96*9,96*8)
    for row,(name,frames) in enumerate(allframes.items()):
        for col,fr in enumerate([reference]+frames):
            for y in range(96): preview[row*96+y][col*96:(col+1)*96]=fr[y]
    save(upscale(preview,3),ROOT/'Preview_3x.png')
    report={'all_pass':all(r['pass'] for r in report.values()),'cell':[96,96],'export_scale':8,'palette':20,'coordinate_system':'top-left origin, bottom row 95','preview_row_order':list(SPECS),'sheets':report}
    (ROOT/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),'utf-8')
    (ROOT/'attempts.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),'utf-8')
if __name__=='__main__': main()
