from pixel_io import *
import hashlib
base=ROOT.parent
src=base.parent.parent/'Assets/_Project/ResourcceEX/Sprites/Px32/Player/Hero_Idle.png'
orig=[r[:128] for r in readpng(src)[:128]]
writepng(ROOT/'original.png',orig)
writepng(ROOT/'original_8x.png',nearest(orig,8))
writepng(ROOT/'original_bag_16x.png',nearest([r[36:64] for r in orig[82:104]],16))
writepng(ROOT/'edit_region_24x.png',nearest([r[32:80] for r in orig[64:112]],24))
(ROOT/'source_hash.json').write_text(json.dumps({'path':str(src),'sha256':hashlib.sha256(src.read_bytes()).hexdigest()}))
