from build import *
frames=[]
for group,n in [('BagOut',6),('LongIdle',12),('BagIn',4)]:
 for i in range(n):
  name=f'{group}_F{i}';files=list(S.glob(name+'_generated*.png'));frames.append(dict(frame=name,calls=len(files),limit=3,prior_run_calls=len(files)))
(O/'attempts.json').write_text(json.dumps(dict(tool='built-in image_gen',total_calls=4,frames=frames),indent=2))
