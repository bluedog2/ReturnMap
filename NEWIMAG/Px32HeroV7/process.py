"""Only fixed-grid recovery, palette mapping, alpha threshold, and masked compositing.
No artwork drawn, no rotations, no fitted resampling. Preview uses integer pixel repetition.
"""
from preview import *
from gif_io import gif
import hashlib
def digest(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def main():
    spec=json.loads((ROOT/'Sources/mask_spec.json').read_text())
    rawbag=readpng(ROOT/'Sources/Idle_attempt3_bag.png')
    bag=[[clean(rawbag[y*32+16][x*32+16]) for x in range(12,35)] for y in range(7,26)]
    writepng(ROOT/'Sources/bag_master.png',bag)
    idle=recover('Idle',1)
    run=recover('Run',3,3,4)
    report={'production_ready':False,'mask_semantic_review_pass':False,'bag_size':[23,19],'bag_master_sha256':digest(ROOT/'Sources/bag_master.png'),'processing':{'alpha_threshold':128,'palette':'approved Idle 20 RGB colors; original outside-mask RGBA untouched','recovery':'fixed block centers; Idle 4px, Run 3px with512px row stride, bag32px','no_rotation_or_resampling':True},'sheets':{}}
    comparisons=[]
    for name,fs,fps in [('Idle',idle,8),('Run',run,18)]:
        src=readpng(BASE/f'Hero_{name}.png');out=[];rows=[]
        for i,f in enumerate(fs):
            original=[r[i*128:(i+1)*128] for r in src]
            m=readpng(ROOT/'Masks'/f'{name}_{i:02}.png')
            # Idle reuses the same generated arm/patch, no morph or scale.
            if name=='Idle':f=idle[0]
            candidate=[r[:] for r in original]
            for y in range(128):
                for x in range(128):
                    if m[y][x][0]:candidate[y][x]=f[y][x]
            # Restore original visible lower-leg pixels outside the new insert footprint.
            bx,by=spec[name]['frames'][i]['bag_xy']
            for y in range(102 if name=='Idle' else 109,128):
                for x in range(128):
                    if not (bx<=x<bx+23 and by<=y<by+19):candidate[y][x]=original[y][x]
            layer=[[ZERO]*128 for _ in range(128)]
            for y,r in enumerate(bag):
                for x,p in enumerate(r):
                    if p[3]:
                        assert m[by+y][bx+x][0]
                        candidate[by+y][bx+x]=p;layer[by+y][bx+x]=p
            writepng(ROOT/'Sources'/f'{name}_{i:02}_bag_layer.png',layer)
            outside=sum(candidate[y][x]!=original[y][x] for y in range(128) for x in range(128) if not m[y][x][0])
            same=sum(candidate[by+y][bx+x]!=p for y,r in enumerate(bag) for x,p in enumerate(r) if p[3])
            a=sorted({p[3] for r in candidate for p in r});colors=len({p[:3] for r in candidate for p in r if p[3]})
            rows.append({'frame':i,'mask':f'Masks/{name}_{i:02}.png','mask_sha256':digest(ROOT/'Masks'/f'{name}_{i:02}.png'),'mask_pixels':sum(bool(p[0]) for r in m for p in r),'outside_mask_mismatch_count':outside,'bag_xy':[bx,by],'bag_size':[23,19],'bag_opaque_pixel_mismatch_count':same,'bag_layer_identical_under_translation':True,'colors':colors,'alpha_values':a,'bottom_opaque_row':bbox(candidate)[3],'source_bottom_opaque_row':bbox(original)[3],'production_ready':False})
            out.append(candidate)
            comparisons.extend(nearest([a+b for a,b in zip(original,candidate)],3))
        sheet=[sum([f[y] for f in out],[]) for y in range(128)]
        writepng(ROOT/'_1x'/f'Hero_{name}.png',sheet)
        pal=sorted({p[:3] for r in sheet for p in r if p[3]})
        delays=gif(ROOT/'Animations'/f'Hero_{name}_3x.gif',out,fps,True,pal)
        writepng(ROOT/'Sources'/f'{name}_final_3x.png',nearest(atlas(out,3 if name=='Idle' else 4),3))
        report['sheets'][name]={'size':[len(sheet[0]),len(sheet)],'frames':rows,'colors':len(pal),'outside_mask_mismatch_count':sum(r['outside_mask_mismatch_count'] for r in rows),'source_sha256_unchanged':digest(BASE/f'Hero_{name}.png')==spec[name]['sha256'],'source_sha256':spec[name]['sha256'],'attempts':3,'selected_arm_attempt':1 if name=='Idle' else 3,'selected_bag_attempt':'Idle3(shared)','gif_requested_fps':fps,'gif_delays_cs':delays,'production_ready':False,'failure_reasons':['벨트 고리의 몸통 접합이 불명확하며 가방이 손에 매달린 것처럼 보일 수 있음','가방 아래 모서리에 생성 잔여 픽셀이 있으며 원본 1px 외곽선과 접합 품질 미달','새 가방 영역 일부가 기존 다리/신발 실루엣과 겹쳐 엄격한 의미 마스크 조건 미달']+(['앞/뒤 팔 교대 및 다리와 반대 위상 전체 검증 미달'] if name=='Run' else [])}
    writepng(ROOT/'Compare_3x.png',comparisons)
    (ROOT/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    attempts={'tool':'built-in image_gen','attempt_limit_per_sheet':3,'total_calls':6,'Idle':[{'attempt':1,'source':'Sources/Idle_attempt1.png','selected':'arm frame0 reused across all Idle frames','result':'small bag; arm best available'},{'attempt':2,'source':'Sources/Idle_attempt2.png','selected':False,'result':'1254x1254 unexpected output; rejected, no fitted resampling'},{'attempt':3,'source':'Sources/Idle_attempt3_bag.png','selected':'shared23x19 bag only','result':'single canonical bag,32px fixed-block recovery'}],'Run':[{'attempt':1,'source':'Sources/Run_attempt1.png','selected':False,'result':'1774x887 noninteger frame grid; rejected'},{'attempt':2,'source':'Sources/Run_attempt2.png','selected':False,'result':'weak arm swing; generated bag not retained'},{'attempt':3,'source':'Sources/Run_attempt3.png','selected':'all8 arm patches','result':'no generated bag; shared Idle3 bag translated in'}]}
    (ROOT/'attempts.json').write_text(json.dumps(attempts,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({n:{'outside_mismatch':s['outside_mask_mismatch_count'],'colors':s['colors'],'bottom':[r['bottom_opaque_row'] for r in s['frames']]} for n,s in report['sheets'].items()}))
if __name__=='__main__':main()
