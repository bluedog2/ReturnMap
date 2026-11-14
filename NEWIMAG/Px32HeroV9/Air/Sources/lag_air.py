from build_air import *

# Manual anatomical landmarks: rear upper corner of shorts/hip, inspected at 8x.
# Previous-frame anchor is used, with directional inertial offset in cell pixels.
hips={'Fall_F0':[62,87],'Fall_F1':[62,87],'Fall_F2':[62,88],'Fall_F3':[62,88],
 'Rise_F0':[65,87],'Rise_F1':[65,87],'Rise_F2':[65,87],
 'Apex_F0':[65,87],'Apex_F1':[64,87],'Apex_F2':[62,87]}
prev={'Fall_F0':'Fall_F3','Fall_F1':'Fall_F0','Fall_F2':'Fall_F1','Fall_F3':'Fall_F2',
 'Rise_F0':'Rise_F2','Rise_F1':'Rise_F0','Rise_F2':'Rise_F1',
 'Apex_F0':'Rise_F2','Apex_F1':'Apex_F0','Apex_F2':'Apex_F1'}
inertia={'Fall_F0':-2,'Fall_F1':-1,'Fall_F2':-1,'Fall_F3':-1,
 'Rise_F0':1,'Rise_F1':2,'Rise_F2':1,'Apex_F0':0,'Apex_F1':0,'Apex_F2':0}

def main():
 records={}
 for name,hip in hips.items():
  ph=hips[prev[name]];pos=[ph[0]-11,ph[1]-2+inertia[name]]
  m=json.loads((S/f'{name}_metrics.json').read_text(encoding='utf-8'))
  if name=='Fall_F0':assert pos==m['bag_position']
  else:
   base=read(S/f'{name}_bagless.png');layer=j.layer(pos);bm=layer[:,:,3]>0;a=base.copy();a[bm]=layer[bm]
   gm=np.array(Image.open(O/f'Masks/{name}_generation.png'))>0
   save(f'{name}.png',a);save(f'Sources/{name}_layer.png',layer);save(f'Sources/{name}_review_3x.png',zoom(a,3));save(f'Masks/{name}_edit.png',(gm|bm)*255)
   m['bag_position']=pos;m['chain_position']=[pos[0]+5,pos[1]-6];dump(f'Sources/{name}_metrics.json',m)
  records[name]=dict(current_hip=hip,previous_frame=prev[name],previous_hip=ph,base_offset=[-11,-2],inertial_y=inertia[name],bag_position=pos,relative_current_hip_y=pos[1]-(hip[1]-2))
 records['flow_boundary']={'Fall_F0':{'previous_frame':'Apex_F2','previous_hip':[62,87],'inertial_y':-1,'bag_position':[51,84]},'Rise_F0':{'previous_frame':'JumpStart_F2','previous_hip':[65,87],'inertial_y':1,'bag_position':[54,86]}}
 dump('Sources/bag_lag.json',records)
if __name__=='__main__':main()
