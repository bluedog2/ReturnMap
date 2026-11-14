from build_v2 import *
def compose(i,source,dx,gen_dx=0,gen_dy=0,face_source=None,face_dx=0,face_dy=0):
 g=read(S/source)
 # Approved head/hair and torso pixels retain their scale and 3/4 direction.
 upper=idle.copy();upper[:98]=shift(idle,dx)[:98]
 gm=shift(g,gen_dx,gen_dy)
 m=np.zeros((128,128),bool);m[27:98,20:111]=True;m[98:103,88:101]=True;m|=vis
 out=idle.copy();out[m]=gm[m]
 protect=np.zeros((128,128),bool);protect[:72]=True;protect[72:85,:53+dx]=True
 out[protect]=upper[protect]
 # Only image-generated facial pixels; no pixel drawing or rotation.
 face=shift(read(S/face_source) if face_source else g,face_dx,face_dy)
 fm=np.zeros((128,128),bool);fm[55:70,66+dx:89+dx]=True
 out[fm]=face[fm]
 # Keep the approved crop-top perspective and shoulder/neck attachment.
 core=np.zeros((128,128),bool);core[72:85,71+dx:84+dx]=True
 out[core]=upper[core]
 # Existing bag footprint is repaired from the approved bagless Idle body.
 under=read(O.parent/'Idle/Sources/Body_patch.png')
 out[vis]=under[vis]
 layer=shift(bag,[1,2,2,0][i]);lm=layer[:,:,3]>0;out[lm]=layer[lm];m|=lm
 save(f'Sources/F{i}_bag_layer.png',layer);save(f'Masks/F{i}_edit.png',m*255)
 save(f'Masks/F{i}_head_protected.png',protect*255);save(f'Masks/F{i}_face_edit.png',fm*255)
 save(f'Hit_F{i}.png',out);save(f'Sources/F{i}_review_3x.png',zoom(np.concatenate([idle,out],1)))
 return out
if __name__=='__main__':
 compose(0,'F1_blocks3.png',-3,gen_dx=1,gen_dy=1,face_source='F0_blocks3.png',face_dx=-3)
 compose(1,'F1_blocks3.png',-4,face_dy=-1)
 compose(2,'F2_blocks.png',-1,gen_dx=-1,face_dx=-1,face_dy=-1)
