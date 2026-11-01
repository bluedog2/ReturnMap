import json,sys,hashlib,shutil
from pathlib import Path
import process as p
R=p.ROOT
COUNTS={'Run':8,'JumpStart':2,'Fall':2,'Land':3,'Dash':4,'Hit':2,'Death':6}
def run():
 records=json.loads((R/'size_revision_records.json').read_text())
 for r in records:
  dest=R/'Sources'/('Hero_'+r['name']+'_sizefix_attempt'+str(r['attempt'])+'.png')
  if not dest.exists(): shutil.copyfile(r['path'],dest)
  r['source']=str(dest.relative_to(R)).replace('\\','/')
  if 'recovery' in r: continue
  frames,meta=p.recover(dest,1,1,COUNTS[r['name']],k=4)
  r['recovery']=meta
  if frames:
   r['metrics']=p.metrics(frames,r['name'])
   heights=[f['height'] for f in r['metrics']['frames']]
   targets=heights if r['name']=='Death' else ([heights[0],101] if r['name']=='JumpStart' else ([heights[0],heights[1],101] if r['name']=='Land' else [92 if r['name']=='Dash' else 101]*len(heights)))
   r['selection_score']=sum(abs(a-b) for a,b in zip(heights,targets))/len(heights)
   sheet=[sum((f[y] for f in frames),[]) for y in range(128)]
   p.writepng(R/'Sources'/('Hero_'+r['name']+'_sizefix_candidate'+str(r['attempt'])+'_4x.png'),p.nearest(sheet,4))
  print(r['name'],r['attempt'],[f['height'] for f in r.get('metrics',{}).get('frames',[])],meta.get('error',''),flush=True)
  (R/'size_revision_records.json').write_text(json.dumps(records,indent=2))
if __name__=='__main__':run()
