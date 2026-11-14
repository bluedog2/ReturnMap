"""Independent saved-file checks; does not call composition functions."""
from pixel_io import *
import hashlib
def verify():
    v=json.loads((ROOT/'validation.json').read_text(encoding='utf-8'))
    master=readpng(ROOT/'Sources/bag_master.png');mb=bbox(master)
    checks=[]
    for name,s in v['sheets'].items():
        srcpath=ROOT.parent.parent/f'Assets/_Project/ResourcceEX/Sprites/Px32/Player/Hero_{name}.png'
        src=readpng(srcpath);sheet=readpng(ROOT/'_1x'/f'Hero_{name}.png')
        assert hashlib.sha256(srcpath.read_bytes()).hexdigest()==s['source_sha256']
        assert len(sheet)==128 and len(sheet[0])==len(s['frames'])*128
        assert len({p[:3] for r in sheet for p in r if p[3]})<=24
        assert {p[3] for r in sheet for p in r}<={0,255}
        for row in s['frames']:
            i=row['frame'];f=[r[128*i:128*(i+1)] for r in sheet];orig=[r[128*i:128*(i+1)] for r in src]
            mask=readpng(ROOT/row['mask']);layer=readpng(ROOT/'Sources'/f'{name}_{i:02}_bag_layer.png');bx,by=row['bag_xy']
            outside=sum(f[y][x]!=orig[y][x] for y in range(128) for x in range(128) if not mask[y][x][0])
            assert outside==0==row['outside_mask_mismatch_count']
            extracted=[r[bx:bx+23] for r in layer[by:by+19]]
            assert extracted==master
            assert all(f[by+y][bx+x]==p for y,r in enumerate(master) for x,p in enumerate(r) if p[3])
            assert bbox(f)[3]==127
            checks.append({'sheet':name,'frame':i,'outside_mask_mismatch_count':outside,'bag_layer_exact':True,'bottom_row':127})
        data=(ROOT/'Animations'/f'Hero_{name}_3x.gif').read_bytes()
        assert data[:6]==b'GIF89a' and struct.unpack('<HH',data[6:10])==(384,384)
        # Parse GIF blocks and verify frame count and centisecond delays.
        pos=13+3*(2**((data[10]&7)+1));count=0;delays=[]
        while data[pos]!=59:
            tag=data[pos];pos+=1
            if tag==33:
                label=data[pos];pos+=1
                if label==249:delays.append(struct.unpack('<H',data[pos+2:pos+4])[0])
                while data[pos]:pos+=1+data[pos]
                pos+=1
            elif tag==44:
                packed=data[pos+8];pos+=9
                if packed&128:pos+=3*2**((packed&7)+1)
                pos+=1
                while data[pos]:pos+=1+data[pos]
                pos+=1;count+=1
            else:raise ValueError((tag,pos))
        assert count==len(s['frames']) and delays==s['gif_delays_cs']
    assert [mb[2]-mb[0]+1,mb[3]-mb[1]+1]==[23,19]
    results={'file_checks_pass':True,'production_ready':False,'frames':checks,'semantic_mask_and_visual_quality_pass':False}
    (ROOT/'file_checks.json').write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf-8')
    print('PASS: saved PNGs, 14 masks, original hashes, identical translated bag layers, binary alpha, palette, bottom row, GIF structure/timing. Visual production approval: FAIL.')
if __name__=='__main__':verify()
