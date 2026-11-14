from build_v2 import *

def compose_v3(i,attempt=1):
    g,rec=recover(read(S/f'V3_F{i}_generated{attempt}.png'))
    save(f'Sources/V3_F{i}_blocks{attempt}.png',g)
    # Rectangular authorized edit mask; preserve feet and lower legs exactly.
    mask=np.zeros((128,128),bool)
    mask[18:91,15:124]=True
    mask[91:103,88:110]=True
    mask|=vis
    out=idle.copy()
    g=shift(g,[4,5][i])
    out[mask]=g[mask]
    under=read(O.parent/'Idle/Sources/Body_patch.png')
    out[vis]=under[vis]
    layer=shift(bag,[2,3][i]);lm=layer[:,:,3]>0
    out[lm]=layer[lm];mask|=lm
    save(f'Sources/V3_F{i}_bag_layer.png',layer)
    save(f'Masks/F{i}_edit.png',mask*255)
    save(f'Hit_F{i}.png',out)
    (S/f'V3_F{i}_recovery{attempt}.json').write_text(json.dumps(rec,indent=2))
    prev=read(S/f'prev2/Hit_F{i}.png')
    save(f'Sources/V3_F{i}_review_3x.png',zoom(np.concatenate([idle,prev,out],1)))
    print(i,rec)

if __name__=='__main__':
    compose_v3(int(sys.argv[1]),int(sys.argv[2]) if len(sys.argv)>2 else 1)
