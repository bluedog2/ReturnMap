from legacy_io import *
import hashlib
B=ROOT/'Sources/Revision1_before'
v=json.loads((ROOT/'validation.json').read_text(encoding='utf-8'))
assert readpng(ROOT/'Compare_3x.png')[:1152]==readpng(B/'Compare_3x.png')[:1152]
protected=json.loads((ROOT/'Sources/revision1_protected.json').read_text())
assert all(hashlib.sha256((ROOT/p).read_bytes()).hexdigest()==h for p,h in protected.items())
for key in ['attempts.json','layout.json']:
 a=json.loads((ROOT/key).read_text(encoding='utf-8'));b=json.loads((B/key).read_text(encoding='utf-8'))
 if key=='attempts.json':a=a['sheets'];b=b['sheets']
 for n in ['JumpStart','Dash']:assert a[n]==b[n]
print('Protected files, metadata and 3 comparison rows unchanged; Run PNG/GIF preserved.')
for n in ['Run','Hit']:
 print(n,[f['height'] for f in v['sheets'][n]['frames']],[f['head_delta_idle'] for f in v['sheets'][n]['frames']])
