from build import *

P=SRC/'prev2'
def read(p):return np.array(Image.open(p).convert('RGBA'))
def diff(a,b,m):return int((np.any(a!=b,axis=2)&m).sum())
def write(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf-8')
yy,xx=np.indices((128,128))
placements=json.loads((SRC/'R2_placement.json').read_text())
checks={};native=[]
for n in placements:
    a=read(ROOT/f'Turnaround/Hero_{n}.png');old=read(P/f'Turnaround/Hero_{n}.png')
    m=read(ROOT/f'Masks/R2_{n}.png')[:,:,0]>0
    full=read(SRC/f'R2_{n}_full_bag_layer.png');body=(full[:,:,3]>0)&(yy>=placements[n]['box_top'])
    checks[n]={'outside_edit_mask_mismatch':diff(a,old,~m),'head_face_mismatch':diff(a,old,yy<69),'body_bag_mismatch':diff(a,full,body) if n!='Side' else None,'handle_occluded_pixels':diff(a,full,(full[:,:,3]>0)&~body),'head_top':stats(a)['bbox'][1],'sole':stats(a)['floor_row']}
    assert checks[n]['outside_edit_mask_mismatch']==0 and checks[n]['head_face_mismatch']==0 and checks[n]['sole']==127
    if n!='Side':assert checks[n]['body_bag_mismatch']==0
armchecks={}
for n in ['Down','HandOnHip','Forward','Back']:
    a=read(ROOT/f'Arms/Arm_{n}.png');old=read(P/f'Arms/Arm_{n}.png')
    am=read(P/f'Masks/Arm_{n}.png')[:,:,0]>0
    if n=='Forward':am&=yy<88
    layer=read(SRC/'R2_ThreeQuarter_full_bag_layer.png');bm=(layer[:,:,3]>0)&(yy>=104)
    m=read(ROOT/f'Masks/R2_Arm_{n}.png')[:,:,0]>0
    im=read(ROOT/f'Masks/Idle0_Arm_{n}.png')[:,:,0]>0
    armchecks[n]={'actual_pose_region_mismatch':diff(a,old,am),'outside_revision_mask_mismatch':diff(a,old,~m),'bag_body_occluded_pixels':diff(a,layer,bm),'idle0_outside_mask_mismatch':diff(a,BASE,~im)}
    assert all(v==0 for v in armchecks[n].values())
# The fixed lowered sleeve/glove polygons are independent from the edit masks.
protect={'Front':mask([[(78,80),(92,88),(92,104),(79,104),(77,96)]]),'ThreeQuarter':mask([[(84,78),(92,80),(98,88),(98,100),(88,100),(86,89)]]),'Back':mask([[(32,82),(42,82),(43,91),(43,100),(33,100)]])}
for n,m in protect.items():
    m&=yy<({'Front':104,'ThreeQuarter':100,'Back':100}[n])
    checks[n]['lowered_arm_mismatch']=diff(read(ROOT/f'Turnaround/Hero_{n}.png'),read(P/f'Turnaround/Hero_{n}.png'),m)
    assert checks[n]['lowered_arm_mismatch']==0
baghash={}
for p in sorted((ROOT/'Bag').glob('*.png')):
    baghash[p.name]=hashlib.sha256(p.read_bytes()).hexdigest()==hashlib.sha256((P/'Bag'/p.name).read_bytes()).hexdigest()
assert all(baghash.values())
for folder,pattern in [('Turnaround','Hero_*.png'),('Arms','Arm_*.png'),('Bag','Bag_*_*.png')]:
    for p in sorted((ROOT/folder).glob(pattern)):
        a=read(p);s=stats(a);s['path']=p.relative_to(ROOT).as_posix();native.append(s)
        assert s['colors']<=24 and set(s['alpha_values'])<={0,255}
chain=read(SRC/'R2_chain.png');cs=stats(chain)
assert cs['size']==[2,12] and np.all(chain[:,:,3]==255)
summary=[
 ['몸·얼굴·머리·소매·장갑','통과','보호 영역 불일치 0, 바닥 127'],
 ['가방 디자인·Open 2종','통과','Bag 전체 파일 해시 동일; 정수 평행이동'],
 ['앞면·3/4 가방 본체 가림','통과','앞면 기본 자세 및 3/4 팔 4자세 모두 0px'],
 ['4면 본체 윗변·끈','통과','윗변 y104(차이 0), 생성 체인 12×2px'],
 ['허벅지 중간·무릎 위','미통과','원본 높이 유지 시 아랫변 y116~119로 무릎 아래'],
 ['3배 육안 확인','부분 통과','이빨 상자 식별 가능; 가방이 무릎 부근까지 내려옴'],
 ['마스크·팔레트·알파','통과',f'마스크 밖 0, 최대 {max(s["colors"] for s in native)}색, 알파 0/255'],
 ['백업·확대본·생성 횟수','완료','Sources/prev2, Hips_8x.png, image_gen 1/3회']]
report={'stage':1,'revision':'재요청 2','status':'partial_constraints_conflict','all_requirements_pass':False,'production_ready':False,'summary':summary,'placements':placements,'preservation':checks,'arm_checks':armchecks,'bag_file_hash_unchanged':baghash,'artifacts':native,'chain':cs,'visual_review':{'performed':True,'files':['Sources/Turnaround_review_3x.png','Sources/Arms_review_3x.png','Turnaround/Hips_8x.png'],'result':'이빨 상자는 드러나지만 무릎 위·허벅지 중간 목표 미달'},'limitations':['앞면 손의 최하단은 y103. 본체 가림 0을 확보한 윗변은 y104, 원본 본체 높이 13px(뒤 16px)로 아랫변 y116~119. 무릎 위 조건과 동시 충족하지 못함.','가방 본체 가림과 손잡이 가림을 분리 측정. 앞면의 기존 손잡이는 장갑 뒤에 일부 가려짐.','12px는 생성 체인 전체 자산 길이. 소매 뒤 구간은 가려지며 기존 허리 고리~체인 접속은 육안 판정이 필요함.','앞면 팔 4자세 원본은 없으므로 앞면 기본 자세와 기존 3/4 팔 4자세만 검증.','3/4 돌출은 보이는 허벅지 경계 x87 대비 가방 우측 끝 x92로 5px. Side는 앞 모서리 5열만 노출.','뒷면은 승인된 Bag_Hip_Side 원본을 유지하며 이빨 없음.'],'processing':'built-in image_gen 체인 생성 1회; 고정 68px 격자 샘플링, 승인 팔레트 매핑, 알파 이진화. 가방/몸 리사이즈 및 코드 드로잉 없음. 기존 생성된 가방 없는 몸체로 이전 자리만 복구.'}
write(ROOT/'validation.json',report)
settings=json.loads((P/'validation.json').read_text(encoding='utf-8'))['settings']
for s in settings:
    if s['item']=='가방 위치(평소)':s.update(status='미통과',evidence='본체 가림 0과 높이 정렬 완료. 무릎 위 조건 미달.')
    if s['item']=='가방':s.update(status='통과',evidence='Bag 전체 파일 해시 동일; Open 2종 포함 변경 없음.')
report['settings']=settings
write(ROOT/'validation.json',report)
old=json.loads((P/'attempts.json').read_text(encoding='utf-8'))
write(ROOT/'attempts.json',{'scope':'재요청 2','max_calls_this_revision':3,'total_calls_this_revision':1,'items':[{'item':'silver_chain','attempt':1,'tool':'built-in image_gen','source':'Sources/R2_chain_generated.png','prompt':'Sources/R2_chain_prompt.txt','recovery':{'pitch':68,'origin':[560,270],'size':[2,12]},'selection':'채택'}],'previous_revision':old})
table='| 항목 | 결과 | 측정·근거 |\n|---|---|---|\n'+'\n'.join('| '+' | '.join(row)+' |' for row in summary)
md='# Re:Trap 주인공 V9 — 1단계 재요청 2\n\n## 재요청 2\n\n**부분 완료: 무릎 위 조건 미통과.** 통과하지 못한 결과를 전체 통과로 기록하지 않습니다. 가방을 낮게 매달고 팔·가방 원본을 보존했지만, 고정된 몸과 가방 크기에서는 요청한 높이 조건을 동시에 충족하지 못했습니다.\n\n'+table+'\n\n앞면 장갑 끝은 y103입니다. 가방 본체 윗변 y104는 손 아래이지만 본체 높이가 13px(뒷면 16px)이므로 끝은 y116~119까지 내려갑니다. 무릎은 육안상 y108~112 부근입니다. 팔 또는 가방 크기를 바꾸지 않는 이번 범위에서 무릎 위 조건을 달성하지 못했습니다. 앞면 손잡이 일부는 장갑 뒤에 남고, **본체·뚜껑·입**은 가림 0입니다.\n\n### 4면 배치\n\n| 면 | 자산 | 이동 좌표 | 본체 윗변 | 아랫변 |\n|---|---|---|---|---|\n'+'\n'.join(f'| {n} | {p["asset"]} | {p["translation"]} | {p["box_top"]} | {p["bottom"]} |' for n,p in placements.items())+'\n\n3/4는 허벅지 앞 경계 x87에서 가방 끝 x92까지 5px 돌출합니다. 앞면은 화면 오른쪽, 뒷면은 왼쪽이며 이빨 없는 기존 측면 자산을 유지합니다. 옆면은 반대편 가방의 앞 모서리 5열만 노출합니다. 팔 4자세는 기존 3/4 원본이며 앞면 4자세를 새로 만들지는 않았습니다.\n\n### 원본·처리·재현\n\n직전 산출물과 Sources 파일을 `Sources/prev2/`로 이동 후 동일 이름으로 복사하여 작업했습니다. 기존 `Sources/prev/`도 유지했습니다. Bag 파일은 전부 바이트 해시 동일합니다. 기존 가방 자리만 저장된 가방 없는 몸체로 복구했습니다. 새 끈만 **내장 image_gen**으로 생성했으며 코드는 격자 복원·팔레트 매핑·마스크 합성·정수 평행이동·검증만 수행합니다. 정확한 최종 프롬프트는 `Sources/R2_chain_prompt.txt`, 생성 원본은 `Sources/R2_chain_generated.png`입니다. 체인 원본은 12×2px이고 팔 뒤의 일부는 가려집니다.\n\n재현: `python NEWIMAG/Px32HeroRef/Sources/r2_assemble.py` 다음 `python NEWIMAG/Px32HeroRef/Sources/r2_report.py`. 이전 fix_* 진입점은 직전 버전 재현용이므로 이번 결과에 실행하지 않습니다.\n\n검수: `Turnaround/Hips_8x.png`, `Turnaround/Turnaround_4x.png`, `Arms/Arms_4x.png`, `Sources/Turnaround_review_3x.png`, `Sources/Arms_review_3x.png`. 검증 수치는 `validation.json`에 있습니다. 이전 README·검증·생성 이력은 `Sources/prev2/`에 보존했습니다. Assets·다른 NEWIMAG 폴더 변경 및 git 실행 없음.\n'
(ROOT/'README.md').write_text(md,encoding='utf-8')
print(json.dumps({'summary':summary,'checks':checks,'arms':armchecks},ensure_ascii=False))
