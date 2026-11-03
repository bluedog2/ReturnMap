import sys
sys.dont_write_bytecode=True
from pngio import readpng,bbox
from pathlib import Path
import re,json,hashlib
R=Path(__file__).resolve().parent
S=['Normal','Dud','Critical','Beneficial']
pal={tuple(bytes.fromhex(h)) for h in re.findall(r'^#([0-9a-fA-F]{6})\s*$',(R.parent/'palette_v2.txt').read_text(encoding='utf-8'),re.M)}
banned={tuple(bytes.fromhex(h)) for h in '334629 58783B 88AE43 B8D849 D8EC76 38284D 58377B 8050AD B581D4'.split()}
report={'files':[],'checks':{},'production_ready':False}
loaded={}
for f in sorted(list((R/'Parts').rglob('*.png'))+list((R/'Misc').glob('*.png'))):
 a=readpng(f);loaded[f.relative_to(R).as_posix()]=a
 pixels=[p for r in a for p in r];visible=[p for p in pixels if p[3]];colors={p[:3] for p in visible};b=bbox(a)
 exp={'arrow.png':[32,16],'muzzle_flash.png':[32,32],'chain.png':[16,16],'impact.png':[64,32],'SlotSeal.png':[32,32]}.get(f.name,[64,64])
 m={'file':f.relative_to(R).as_posix(),'size':[len(a[0]),len(a)],'expected_size':exp,'size_pass':[len(a[0]),len(a)]==exp,'colors':len(colors),'palette_outside_pixels':sum(p[:3] not in pal for p in visible),'banned_pixels':sum(p[:3] in banned for p in visible),'pure_black_pixels':sum(p[:3]==(0,0,0) for p in visible),'alpha_values':sorted({p[3] for p in pixels}),'bbox':b,'top_opaque_row':b[1] if b else None,'bottom_opaque_row':b[3] if b else None,'opaque_count':len(visible)}
 m['pass']=m['size_pass'] and m['colors']<=24 and m['palette_outside_pixels']==m['banned_pixels']==m['pure_black_pixels']==0 and set(m['alpha_values'])<={0,255}
 report['files'].append(m)
def get(t,s,p):return loaded[f'Parts/{t}/{s}/{p}.png']
def mask(a):return [[p[3] for p in r] for r in a]
checks=report['checks']
checks['file_count_49']=len(report['files'])==49
checks['spike_tip_rows']=[bbox(get('spike',s,'spikes'))[1] for s in S]
checks['spike_tip_all_zero']=checks['spike_tip_rows']==[0]*4
checks['spike_shafts_at_least_40_rows']=all(bbox(get('spike',s,'spikes'))[3]>=39 for s in S)
checks['spike_base_full_opaque']=all(all(p[3]==255 for r in get('spike',s,'base') for p in r) for s in S)
checks['platform_full_opaque']=all(all(p[3]==255 for r in get('spike',s,'platform') for p in r) for s in S)
checks['platform_identical']=all(get('spike',s,'platform')==get('spike','Normal','platform') for s in S)
checks['glow_normal_dud_critical_identical']=all(get('spike',s,'glow')==get('spike','Critical','glow') for s in S[:2])
checks['glow_in_top32']=all(bbox(get('spike',s,'glow'))[3]<32 for s in S)
checks['muzzle_at_right_center']=all(get('arrow_shooter',s,'body')[32][63][3]==255 for s in S)
checks['flash_right_half']=all(bbox(get('arrow_shooter',s,'muzzle_flash'))[0]>=16 for s in S)
checks['head_bottom_63']=all(bbox(get('drop_hammer',s,'head'))[3]==63 for s in S)
checks['mount_top_0']=all(bbox(get('drop_hammer',s,'mount'))[1]==0 for s in S)
checks['impact_bottom_31']=all(bbox(get('drop_hammer',s,'impact'))[3]==31 for s in S)
checks['chain_seams']=[]
for s in S:
 a=get('drop_hammer',s,'chain');b=bbox(a);columns=[x for x in range(16) if a[0][x][3] and a[-1][x][3]]
 checks['chain_seams'].append({'state':s,'top_bottom_rgba_equal':a[0]==a[-1],'connected_columns':columns,'width':b[2]-b[0]+1,'center':(b[0]+b[2])/2,'pass':a[0]==a[-1] and bool(columns) and 8<=b[2]-b[0]+1<=12})
checks['chain_seams_pass']=all(c['pass'] for c in checks['chain_seams'])
checks['silhouettes']={}
for t,parts in [('spike',['base','spikes','glow','platform']),('arrow_shooter',['body','arrow','muzzle_flash']),('drop_hammer',['head','mount','chain','impact'])]:
 for p in parts:checks['silhouettes'][t+'/'+p]=all(mask(get(t,s,p))==mask(get(t,'Normal',p)) for s in S)
checks['all_state_silhouettes_equal']=all(checks['silhouettes'].values())
checks['marker_centers_transparent']=all(loaded[f'Misc/SlotMarker_{s}.png'][32][32][3]==0 for s in ['Floor','Ceiling','LeftWall','RightWall'])
checks['seal_byte_identical']=(R/'Misc/SlotSeal.png').read_bytes()==(R.parent/'Px32TrapV2/Misc/SlotSeal.png').read_bytes()
report['numeric_pass']=all(f['pass'] for f in report['files']) and all(v for v in checks.values() if isinstance(v,bool))
report['limitations']=['Generated source grids are not exact integer grids: per-part cell-center grid recovery uses recorded source crops and pitches.','Continuous 1px charcoal outlines and exactly 3-4 shades per material are not mechanically guaranteed.','Uniform silhouette uses intersection cutouts; some state-specific edge details are removed.','Preview hero is copied unscaled from existing Idle frame 0, regardless of its actual opaque height.']
(R/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'numeric_pass':report['numeric_pass'],'checks':checks,'max_colors':max(f['colors'] for f in report['files'])},ensure_ascii=False,indent=2))
