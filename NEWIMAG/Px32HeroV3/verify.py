import sys
sys.dont_write_bytecode = True
import json, struct
from pathlib import Path
from process import ROOT, readpng, metrics, PAL

report=json.loads((ROOT/'validation.json').read_text(encoding='utf-8'))
for name,entry in report['sheets'].items():
    one=ROOT/'_1x'/('Hero_'+name+'.png')
    four=ROOT/'Player'/('Hero_'+name+'.png')
    if 'frames' not in entry or not one.exists(): continue
    a=readpng(one); b=readpng(four)
    count=len(entry['frames'])
    frames=[[r[i*128:(i+1)*128] for r in a] for i in range(count)]
    measured=metrics(frames,name)
    entry.update(measured)
    entry['alpha_values']=sorted(set(p[3] for r in a for p in r))
    entry['dimensions_1x']=[len(a[0]),len(a)]
    entry['dimensions_4x']=[len(b[0]),len(b)]
    entry['nearest_4x_exact']=len(b)==len(a)*4 and len(b[0])==len(a[0])*4 and all(p==a[y//4][x//4] for y,r in enumerate(b) for x,p in enumerate(r))
    entry['palette_outside']=sum(p[3]>0 and p[:3] not in PAL for r in a for p in r)
    entry['nonbinary_alpha_pixels']=sum(p[3] not in (0,255) for r in a for p in r)
    entry['numeric_pass']=entry['numeric_pass'] and entry['nearest_4x_exact'] and not entry['palette_outside'] and not entry['nonbinary_alpha_pixels']
report['overall_pass']=all(v.get('numeric_pass',False) and v.get('production_ready',False) for v in report['sheets'].values())
(ROOT/'validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps({n:{'numeric_pass':v.get('numeric_pass'),'nearest_4x_exact':v.get('nearest_4x_exact'),'palette_outside':v.get('palette_outside'),'nonbinary_alpha_pixels':v.get('nonbinary_alpha_pixels')} for n,v in report['sheets'].items()},indent=2))
