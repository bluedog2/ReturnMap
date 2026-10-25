"""Verify saved output bytes against logical sprites and exact APNG timings."""
import json, struct, zlib, hashlib, sys
sys.dont_write_bytecode=True
import build
R=build.ROOT
def chunks(path):
    data=path.read_bytes(); p=8; out=[]
    assert data[:8]==b'\x89PNG\r\n\x1a\n'
    while p<len(data):
        n=struct.unpack('>I',data[p:p+4])[0]; t=data[p+4:p+8]; v=data[p+8:p+8+n]
        assert zlib.crc32(t+v)&0xffffffff==struct.unpack('>I',data[p+8+n:p+12+n])[0]
        out.append((t,v)); p+=n+12
    return out
def raw(im): return b''.join(b'\0'+bytes(v for p in row for v in p) for row in im)
report=json.loads((R/'validation.json').read_text('utf-8'))
for name,(n,fps,_,_) in build.SPECS.items():
    small=build.readpng(R/f'_1x/Hero_{name}.png'); assert len(small)==96 and len(small[0])==96*n
    big=chunks(R/f'Player/Hero_{name}.png'); dims=struct.unpack('>II',big[0][1][:8]); assert dims==(96*n*8,768)
    decoded=zlib.decompress(b''.join(v for t,v in big if t==b'IDAT'))
    assert decoded==raw(build.upscale(small,8)),name+' not exact nearest neighbor'
    anim=chunks(R/f'Animations/Hero_{name}_3x.apng'); assert struct.unpack('>II',anim[0][1][:8])==(288,288)
    controls=[struct.unpack('>IIIIIHHBB',v) for t,v in anim if t==b'fcTL']; assert len(controls)==n
    assert all(c[5:7]==(1,fps) for c in controls)
    frames=[]; buf=b''
    for t,v in anim:
        if t==b'fcTL':
            if buf: frames.append(zlib.decompress(buf)); buf=b''
        elif t==b'IDAT': buf+=v
        elif t==b'fdAT': buf+=v[4:]
    frames.append(zlib.decompress(buf)); assert len(frames)==n
    for i,fr in enumerate(frames):
        logical=[row[i*96:(i+1)*96] for row in small]
        assert fr==raw(build.upscale(logical,3))
    report['sheets'][name]['export_verification']={'png_8x_exact_nearest_neighbor':True,'dimensions':list(dims),'apng_frames':n,'apng_fps_exact':fps,'apng_pixels_match':True,'crc_valid':True}
    print(name,'saved PNG/APNG verified')
report['external_importer_executed']=False
report['visual_approval']='not claimed; see REVIEW.md for design consistency limitations'
(R/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),'utf-8')
