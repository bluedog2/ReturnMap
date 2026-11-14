from pixel_ops import *
from gif_decode import decode_gif
def main():
 v=json.loads((ROOT/'validation.json').read_text(encoding='utf-8'));checks={}
 ref=ROOT.parent.parent/'Assets/_Project/ResourcceEX/Sprites/Px32/Player/Hero_Idle.png'
 assert hashlib.sha256(ref.read_bytes()).hexdigest()==v['reference_sha256']
 full=readpng(ref)
 for name,s in v['sheets'].items():
  png=readpng(ROOT/'_1x'/f'Hero_{name}.png')
  assert [len(png[0]),len(png)]==s['dimensions']
  assert {p[3] for r in png for p in r}=={0,255}
  assert len({p[:3] for r in png for p in r if p[3]})<=24
  w,h,loop,gfs=decode_gif(ROOT/'Animations'/f'Hero_{name}_3x.gif')
  assert (w,h)==(384,384) and loop and len(gfs)==len(s['frames'])
  for i,m in enumerate(s['frames']):
   f=[r[128*i:128*(i+1)] for r in png]
   si=m['head_source_frame'];original=[r[128*si:128*(si+1)] for r in full]
   head={(x,y):p for y,r in enumerate(original) for x,p in enumerate(r) if p[3] and headmask(x,y)}
   tx,ty=m['head_translation']
   assert all(f[y+ty][x+tx]==p for (x,y),p in head.items())
   assert bbox(f)==m['bbox'] and min(y for x,y in head)+ty==m['head_top_y']
   assert m['top_side_border_clear'] and abs(m['center_x']-64)<=2
   assert gfs[i]['rgba']==[p for r in nearest(f,3) for p in r]
   assert gfs[i]['delay']==s['gif']['delay_centiseconds'][i]
  checks[name]={'dimensions_alpha_palette':True,'exact_approved_head_per_frame':True,'gif_decode_exact_pixel_repeat':True,'frames':len(gfs),'delays_cs':[g['delay'] for g in gfs],'production_ready':False}
 cmp=readpng(ROOT/'Compare_3x.png');assert [len(cmp[0]),len(cmp)]==[3456,768]
 a=json.loads((ROOT/'attempts.json').read_text(encoding='utf-8'))
 assert all(len(s['attempts'])==3 for s in a['sheets'].values())
 report={'artifact_checks_pass':True,'production_ready':False,'approved_source_sha256_unchanged':True,'compare_dimensions':[3456,768],'sheets':checks}
 (ROOT/'file_checks.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
 print(json.dumps(report,ensure_ascii=False))
if __name__=='__main__':main()
