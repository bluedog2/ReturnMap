from build import *

v=json.loads((O/'validation.json').read_text(encoding='utf-8'))
for n,f in v['frames'].items():
 assert f['colors']<=24 and f['outside_palette']==0 and f['alpha']==[0,255]
 assert f['head_top']==27 and f['floor']==127 and f['outside_composite_mask_mismatch']==0
 if 'approved_head_mismatch' in f:assert f['approved_head_mismatch']==0
for p in v['previews'].values():assert p['visible_mismatch']==0
assert all(x==0 for x in v['endpoint_checks'].values())
v['status']='deliverables_complete_with_limitations'
v['visual_review']={'performed':True,'images':['Sheet_3x.png','Waist_8x.png','Sources/Turn_flip_boundary_3x.png'],'bag_height':'jacket hem / upper thigh; not below knee or at chest','hands':'empty, black fingerless gloves, long sleeves','details':'See README Korean model-setting review table','limitations':['ASF center drift: stop 1, turn 2','Generated torso width and proportions not quantitatively identical to approved Idle','Backward braking lean is mild','All arbitrary Run entry phases not runtime-tested','Prior RunStart F1 prompt missing at resume']}
v['bag_rule']={'offset':[-11,-2],'delay':'previous pose hip for generated middle frames; exact approved endpoint positions override settling','placements':{'RunStart_F1':{'previous_hip':[62,97],'inertia':[-1,0]},'RunStop_F1':{'previous_hip':[63,96],'inertia':[3,0]},'RunStop_F2':{'previous_hip':[65,95],'inertia':[3,0]},'Turn_F1':{'previous_hip':[65,95],'inertia':[0,1]}}}
checks={}
for key in ['RunStart_F1','RunStop_F1','RunStop_F2','Turn_F1']:
 layer=read(S/(key+'_bag_layer.png'));pos=v['frames'][key]['bag_position'];expected=bag_layer(pos,'Front' if key=='Turn_F1' else 'ThreeQuarter')
 checks[key]=int(np.any(layer!=expected,2).sum());assert checks[key]==0
v['bag_source_mismatch']=checks
dump('validation.json',v)
required=['README.md','validation.json','attempts.json','Sheet_3x.png','Waist_8x.png','Start_3x.gif','Stop_3x.gif','Turn_3x.gif']
for group,n in [('RunStart',3),('RunStop',4),('Turn',3)]:
 required += [f'Hero_{group}_{n}.png',f'Hero_{group}_{n}_3x.gif']+[f'{group}_F{i}.png' for i in range(n)]
assert all((O/p).exists() for p in required)
dump('Sources/output_manifest.json',{p:hashlib.sha256((O/p).read_bytes()).hexdigest() for p in required})
print('Verified',len(required),'deliverables; palette/alpha/floor/protected pixels/GIF PASS; ASF drift 3 retained.')
