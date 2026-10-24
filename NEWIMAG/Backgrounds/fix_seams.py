"""Deterministic 4px-grid repair; inputs are backed-up RGBA block samples."""
import json, math, statistics, struct, zlib
from pathlib import Path
ROOT = Path(__file__).resolve().parent
W,H=384,256
def read(name):
    b=(ROOT/'_before_fix'/f'{name}.rgba').read_bytes()
    return [[list(b[(y*W+x)*4:(y*W+x+1)*4]) for x in range(W)] for y in range(H)]
def save(a,path):
    def chunk(t,b): return struct.pack('>I',len(b))+t+b+struct.pack('>I',zlib.crc32(t+b)&0xffffffff)
    raw=b''.join((b'\0'+b''.join(bytes(p)*4 for p in row))*4 for row in a)
    path.write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',1536,1024,8,6,0,0,0))+chunk(b'IDAT',zlib.compress(raw,9))+chunk(b'IEND',b''))
def means(a): return [sum(.2126*a[y][x][0]+.7152*a[y][x][1]+.0722*a[y][x][2] for y in range(H))/H for x in range(W)]
def metrics(a):
    m=means(a); d=[abs(m[(x+1)%W]-m[x]) for x in range(W)]
    med=statistics.median(d)
    bands=[]
    doubled=m*4
    prefix=[0]
    for value in doubled: prefix.append(prefix[-1]+value)
    def avg(start,length): return (prefix[start+length]-prefix[start])/length
    for width in range(10,335):
        for x in range(W):
            v=avg(x+W,width)
            left=avg(x+W-25,25)
            right=avg(x+W+width,25)
            # Both sides must differ in the same direction to constitute a stripe.
            prominence=min(abs(v-left),abs(v-right)) if (v-left)*(v-right)>0 else 0
            bands.append((prominence,x*4,width*4))
    return dict(block_neighbor_median=med,block_neighbor_max=max(d),block_threshold=3*med,block_violations=sum(v>3*med for v in d),raw_1px_median=0,raw_1px_violations=sum(v>0 for v in d),wrap_difference=d[-1],band_test_widths_px=[40,1336],band_side_width_px=100,band_threshold=2.0,band_max=max(bands),band_violations=sum(v[0]>2 for v in bands))
far=read('far'); before=metrics(far)
# Replace the complete old edge blends using a continuous interior cloud/hill
# strip. Feather its outer 128px on each side across a 512px circular region.
for row in far:
    original=[p[:] for p in row]
    for i in range(-64,64):
        x=i%W; distance=abs(i+.5)
        t=1 if distance<=32 else (1+math.cos(math.pi*(distance-32)/32))/2
        donor=original[192+i]
        row[x]=[round(original[x][c]*(1-t)+donor[c]*t) for c in range(3)]+[255]
# Remove column exposure discontinuities without spatially blurring pixel shapes.
m=means(far)
weights=[math.exp(-i*i/(2*64**2)) for i in range(-144,145)]
target=[sum(m[(x+i)%W]*weights[i+144] for i in range(-144,145))/sum(weights) for x in range(W)]
for y,row in enumerate(far):
    for x,p in enumerate(row):
        delta=target[x]-m[x]
        shift=math.floor(delta)+(1 if ((y*73)%256)<round((delta-math.floor(delta))*256) else 0)
        row[x]=[max(0,min(255,v+shift)) for v in p[:3]]+[255]
near=read('near'); changed=0
# Correct contaminated opaque branch colors only; keep silhouettes and wall detail.
for y,row in enumerate(near):
    for x,p in enumerate(row):
        if (x<32 or x>=W-32) and p[3]:
            if (y<186 and max(p[:3])>48) or (max(p[:3])>=64 and (max(p[:3])-min(p[:3]))/max(p[:3])<=.2):
                # Dark slate palette, with restrained branch texture.
                level=min(12,round((sum(p[:3])/3)/20)*4)
                row[x]=[12+level,16+level,24+level,255]; changed+=16
        if row[x][3]: row[x][3]=255
        else: row[x]=[0,0,0,0]
for row in near: row[-1]=row[0][:]
save(far,ROOT/'Chapter1/Verification/far.png'); save(near,ROOT/'Chapter1/Play/near.png')
edge=[p for row in near for x,p in enumerate(row) if x<25 or x>=359]
gray=lambda p:p[3]==255 and max(p[:3])>=64 and (max(p[:3])-min(p[:3]))/max(p[:3])<=.2
report={'far_before':before,'far_after':metrics(far),'near':{'partial_alpha_pixels':sum(p[3] not in (0,255) for row in near for p in row)*16,'top_564_nontransparent_pixels':sum(p[3]!=0 for row in near[:141] for p in row)*16,'left_right_different_rows':sum(row[0]!=row[-1] for row in near)*4,'edge_100px_gray_opaque_pixels':sum(gray(p) for p in edge)*16,'gray_definition':'HSV saturation <= 0.20 and value >= 64/255, alpha=255','recolored_pixels':changed},'size':[1536,1024],'uniform_4x4_blocks':True}
(ROOT/'fix_validation.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print(json.dumps(report,indent=2))

