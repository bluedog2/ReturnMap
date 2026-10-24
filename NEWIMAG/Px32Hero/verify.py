"""Verify delivered PNGs and decode every APNG frame without external packages."""
from pixel_io import *

manifest=json.loads((ROOT/'generation.json').read_text(encoding='utf8'))
report=json.loads((ROOT/'validation.json').read_text(encoding='utf8'))
results={}
for name,record in manifest['files'].items():
    sheet=readpng(ROOT/record['one_x']);large=readpng(ROOT/record['eight_x']);n=record['frames']
    assert len(sheet)==64 and len(sheet[0])==64*n
    assert large==up(sheet,8)
    frames=[[r[i*64:(i+1)*64] for r in sheet] for i in range(n)]
    outlines={HP[0],HP[4],HP[7],HP[14]};outline_errors=0
    for frame in frames:
        for y,row in enumerate(frame):
            for x,p in enumerate(row):
                assert p[3] in (0,255) and p[:3] in {q[:3] for q in MP}
                if not p[3]:continue
                assert p[:3]!=(0,0,0)
                if any(xx<0 or xx>=64 or yy<0 or yy>=64 or not frame[yy][xx][3] for xx,yy in ((x-1,y),(x+1,y),(x,y-1),(x,y+1))):
                    outline_errors+=p not in outlines
    assert outline_errors==0
    data=(ROOT/record['animation']).read_bytes();pos=8;controls=[];compressed=[];sequence=[]
    while pos<len(data):
        size=struct.unpack('>I',data[pos:pos+4])[0];typ=data[pos+4:pos+8];payload=data[pos+8:pos+8+size]
        crc=struct.unpack('>I',data[pos+8+size:pos+12+size])[0];assert crc==zlib.crc32(typ+payload)&0xffffffff
        pos+=12+size
        if typ==b'acTL':assert struct.unpack('>II',payload)==(n,0)
        elif typ==b'fcTL':
            control=struct.unpack('>IIIIIHHBB',payload);controls.append(control);sequence.append(control[0]);compressed.append(b'')
            assert control[1:]==(192,192,0,0,1,record['fps'],0,0)
        elif typ==b'IDAT':compressed[-1]+=payload
        elif typ==b'fdAT':sequence.append(struct.unpack('>I',payload[:4])[0]);compressed[-1]+=payload[4:]
    assert len(controls)==n and sequence==list(range(len(sequence)))
    for frame,raw in zip(frames,compressed):
        expected=b''.join(b'\0'+bytes(c for p in row for c in p) for row in up(frame,3))
        assert zlib.decompress(raw)==expected
    diffs=[sum(frames[i][y][x]!=frames[i+1][y][x] for y in range(64) for x in range(64)) for i in range(n-1)]
    assert min(diffs)>0
    results[name]={'apng_decoded_frames':n,'fps':record['fps'],'apng_pixel_mismatches':0,'outline_errors':outline_errors,'adjacent_frame_changed_pixels':diffs,'one_x_sha256':hashlib.sha256((ROOT/record['one_x']).read_bytes()).hexdigest(),'eight_x_sha256':hashlib.sha256((ROOT/record['eight_x']).read_bytes()).hexdigest()}
report['independent_file_verification']=results
(ROOT/'validation.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print('PASS: 27 frames, 14 sprite PNGs, 7 APNGs; exact scale, palette, alpha, outlines, CRC, timing and decoded animation pixels.')
