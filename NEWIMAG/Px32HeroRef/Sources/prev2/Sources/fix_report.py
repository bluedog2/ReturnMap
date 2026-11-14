from build import *
from collections import Counter
import re

def read(p):return np.array(Image.open(p).convert('RGBA'))
def mismatch(a,b,m):return int(np.any(a!=b,axis=2)[m].sum())
prev=SRC/'prev'
names=['Front','ThreeQuarter','Side','Back']
rows={n:dict(head_top=stats(read(ROOT/f'Turnaround/Hero_{n}.png'))['bbox'][1],shoulder=s,belt=b,sole=127) for n,s,b in [('Front',73,87),('ThreeQuarter',70,86),('Side',73,86),('Back',70,87)]}
placements=json.loads((SRC/'V9Fix_placement.json').read_text())
native=[]
for folder,pattern in [('Turnaround','Hero_*.png'),('Arms','Arm_*.png'),('Bag','Bag_*_*.png')]:
    for p in sorted((ROOT/folder).glob(pattern)):
        a=read(p);d=stats(a);d['path']=p.relative_to(ROOT).as_posix()
        d['palette_subset_of_idle0']=all(any(np.array_equal(c,pc) for pc in PAL) for c in np.unique(a[a[:,:,3]>0,:3],axis=0))
        assert d['colors']<=24 and set(d['alpha_values'])<={0,255} and d['palette_subset_of_idle0']
        if folder!='Bag':assert d['floor_row']==127
        native.append(d)
preservation={}
for n in names:
    a=read(ROOT/f'Turnaround/Hero_{n}.png');old=read(prev/f'Turnaround/Hero_{n}.png')
    m=read(ROOT/f'Masks/V9Fix_{n}.png')[:,:,0]>0
    preservation[n]={'outside_edit_mask_mismatch_vs_previous':mismatch(a,old,~m),'head_and_face_mismatch':mismatch(a,old,np.indices(a.shape[:2])[0]<69),'head_row_unchanged':stats(a)['bbox'][1]==stats(old)['bbox'][1]}
    assert all(v==0 if 'mismatch' in k else v for k,v in preservation[n].items())
q=read(ROOT/'Turnaround/Hero_ThreeQuarter.png');qm=read(ROOT/'Masks/Hero_ThreeQuarter.png')[:,:,0]>0
idle_checks={'ThreeQuarter':mismatch(q,BASE,~qm)}
armchecks={}
layer=read(SRC/'V9Fix_ThreeQuarter_bag_layer.png');bag_pixels=layer[:,:,3]>0
for n in ['Down','HandOnHip','Forward','Back']:
    a=read(ROOT/f'Arms/Arm_{n}.png');old=read(prev/f'Arms/Arm_{n}.png')
    am=read(ROOT/f'Masks/Arm_{n}.png')[:,:,0]>0
    actual=am.copy()
    if n=='Forward':actual&=np.indices(am.shape)[0]<88
    # Lower half of the Forward edit mask restores the old lowered arm's background.
    # The actual extended sleeve and glove are entirely above row 88.
    um=read(ROOT/f'Masks/Idle0_Arm_{n}.png')[:,:,0]>0
    idle_checks[n]=mismatch(a,BASE,~um)
    visible=bag_pixels&~actual
    armchecks[n]={'actual_arm_pixel_mismatch_vs_previous':mismatch(a,old,actual),'outside_pose_mask_mismatch_vs_threequarter':mismatch(a,q,~am),'bag_pixel_mismatch_where_unoccluded':mismatch(a,layer,visible),'translation':[78,85],'actual_arm_mask_definition':'original pose mask; Forward restricted to y<88 (sleeve/glove), background restoration below excluded' if n=='Forward' else 'original pose mask'}
    assert all(armchecks[n][k]==0 for k in ['actual_arm_pixel_mismatch_vs_previous','outside_pose_mask_mismatch_vs_threequarter','bag_pixel_mismatch_where_unoccluded'])
assert all(v==0 for v in idle_checks.values())
assert all(26<=r['head_top']<=28 and r['sole']==127 for r in rows.values())
assert all(p['box_top_belt_gap']==2 for p in placements.values())

settings=[
 ('머리','통과','백금발 포니테일·밴드·삐침·Z핀: 승인 픽셀 보존'),
 ('얼굴','통과','빨간 눈·송곳니·웃음·피어싱: 승인 픽셀 보존'),
 ('외투','통과','오버사이즈 재킷·빨간 안감·손목까지 긴 소매 보존'),
 ('손','통과','승인된 검은 장갑과 팔 4자세 픽셀 보존'),
 ('상의','통과','검은 크롭 탑·빨간 무늬·초커 보존'),
 ('하의','통과','기존 가방 영역만 생성 복구; 반바지·벨트 연결'),
 ('다리','통과','검정 니하이·빨간 양말 비대칭 보존'),
 ('신발','통과','하이탑·빨간 스트랩·발바닥 행 보존'),
 ('가방','통과','검은 입 틈, 큰 흰 상하 삼각 이빨; 닫힌 가방 장식 보존, Open 2종 재생성'),
 ('가방 위치(평소)','통과','캐릭터 왼쪽 앞옆 허리; 네 면 본체 윗변-벨트 2px'),
 ('가방 위치(긴 대기)','해당 없음','든 크기 자산만 수정. 긴 대기 애니메이션은 이번 단계 범위 밖'),
 ('크기','통과','128셀, 머리 26~28·바닥 127 유지; 인체 축소 없음')]
summary=[
 ('몸·얼굴·머리·행 정렬','통과','머리 27 / 27 / 26 / 28, 바닥 모두 127'),
 ('긴 소매·장갑·팔 4자세','통과','실제 팔 픽셀 불일치 0'),
 ('가방 위치 4면','통과','앞·3/4 오른쪽, 뒤 왼쪽, 옆은 앞 모서리만 노출'),
 ('가방 높이·걸이','통과','본체 윗변-벨트 2px, 상단 걸이 구간 3~4px'),
 ('가방 이빨·입 벌림 2종','통과','검은 틈·엇갈린 삼각 이빨, 열린 뚜껑·내부 복원'),
 ('팔 시트 재합성','통과','4장 모두 가방 위치 (78, 85), 보이는 가방 픽셀 불일치 0'),
 ('팔레트·알파·마스크','통과','승인 팔레트 20색 이하, 알파 0/255, Idle 0 기준 마스크 밖 불일치 0'),
 ('백업·확대본·마무리 문서','완료','Sources/prev, BagTeeth_16x.png, README·validation·attempts')]
attempts=[]
previous=[]
for p in sorted(SRC.glob('*_generated.png')):
    if p.name.startswith('V9Fix_'):
        match=re.match(r'V9Fix_(.+)_(\d+)_generated',p.stem)
        item,num=match[1],int(match[2]);prompt=p.with_name(p.name.replace('_generated.png','_prompt.txt'))
        assert prompt.exists()
        role='입 색상 일부만 채택; 이빨은 2차 전용 생성본 채택' if num==1 and item in ['Bag_Hip_Front','Bag_Held_Front'] else '채택'
        attempts.append(dict(item=item,attempt=num,tool='built-in image_gen',source=p.relative_to(ROOT).as_posix(),prompt=prompt.relative_to(ROOT).as_posix(),selection=role))
    else:
        match=re.match(r'(.+)_(\d+)_generated',p.stem)
        if match:
            prompt=p.with_name(p.name.replace('_generated.png','_prompt.txt'))
            previous.append(dict(item=match[1],attempt=int(match[2]),source=p.relative_to(ROOT).as_posix(),prompt=prompt.relative_to(ROOT).as_posix() if prompt.exists() else None,note='이전 작업 원본. 누락된 프롬프트는 추정 작성하지 않음' if not prompt.exists() else '이전 작업 원본'))
counts=dict(Counter(a['item'] for a in attempts));assert max(counts.values())<=3
attempt_report=dict(scope='이번 V9 1단계 수정 작업',max_calls_per_item_this_revision=3,total_calls_this_revision=len(attempts),counts_this_revision=counts,items=attempts,inherited_history=previous,inherited_count=len(previous),budget_note='이전 생성 이력과 이번 수정 이력을 분리. 이번 입 전용 2차 생성은 같은 앞면 항목의 2회째로 계산. 3/4 입은 앞면 픽셀을 합성하여 추가 생성 없음. 복구 패치는 Restore_* 항목. 이전+현재 누적을 3회 이하로 주장하지 않음')
(ROOT/'attempts.json').write_text(json.dumps(attempt_report,ensure_ascii=False,indent=2),encoding='utf-8')
report=dict(stage=1,status='revision_complete_pending_user_review',production_ready=False,approval_note='자체 검수 통과. 사용자 최종 승인 전이며 다음 단계/Assets 반영 없음.',settings=[dict(item=a,status=b,evidence=c) for a,b,c in settings],summary=summary,landmark_rows=rows,landmark_method='머리·발바닥 알파 경계 자동 측정; 어깨·벨트는 이전 의미 부위 측정값 유지(이번 승인 사항)',placements=placements,artifacts=native,preservation=preservation,idle0_outside_mask_mismatch=idle_checks,arm_checks=armchecks,all_native_palette_and_alpha_pass=True,all_character_floor_rows_127=True,visual_review=['Turnaround/Turnaround_4x.png','Sources/Turnaround_review_3x.png','Arms/Arms_4x.png','Sources/Arms_review_3x.png','Bag/Bag_8x.png','Bag/BagTeeth_16x.png'],processing='image_gen 그림 생성; 고정 정수 블록 복원·승인 팔레트 매핑·알파 이진화·영역 추출·마스크 합성·정수 평행이동·시트 조립. 생산 스프라이트 resize/rotate/코드 그림 없음. 확대 검수용만 정수 픽셀 반복.',limitations=['생성기가 요청 격자를 정확히 지키지 않아 고정 간격 복원 후 육안 확인함. 원본 생성 이미지와 최종 픽셀의 형태가 완전히 같지는 않음.','앞면과 3/4의 가방 일부는 승인된 소매·장갑에 가려짐. 옆면은 x80..81 두 열만 보임.','Open은 뚜껑이 열려 닫힌 가방보다 큼: 허리 20×23, 든 크기 28×30.','명목 96px 대신 승인된 실제 알파 높이 100~102행 유지. 어깨 70/73행 차이는 사용자 승인 범위로 보존.'],reference=dict(path='Sources/Idle0.png',sha256=hashlib.sha256((SRC/'Idle0.png').read_bytes()).hexdigest()))
(ROOT/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
table=lambda header,body:'| '+' | '.join(header)+' |\n|'+ '|'.join(['---']*len(header))+'|\n'+'\n'.join('| '+' | '.join(map(str,row))+' |' for row in body)
md='# Re:Trap 주인공 V9 — 1단계 수정 결과\n\n자체 검수 통과, **사용자 최종 승인 대기**. 기존 승인 몸·얼굴·머리·긴 소매·장갑을 유지하고 앞쪽 옆 허리 가방과 입을 수정했습니다. 수정 출력은 이 폴더에만 있으며 Assets·다른 NEWIMAG 폴더는 수정하지 않았고 git 명령은 실행하지 않았습니다.\n\n## 요약\n\n'+table(['항목','결과','측정·근거'],summary)+'\n\n## 설정표 항목별 검수\n\n이번 사용자 확정(왼쪽 앞옆 허리, 승인 신체·팔 유지)을 기존 설정표보다 우선합니다.\n\n'+table(['설정표 항목','통과/실패','확인 내용'],settings)+'\n\n## 4면 행 측정\n\n좌표는 0부터, 바운딩 박스 끝 좌표는 미포함입니다. 머리·발바닥은 알파로 측정, 어깨·벨트는 의미 부위 육안 측정값입니다. 기존 승인된 어깨 차이는 수정하지 않았습니다. 명목 96px로 축소하지 않고 기존 실제 높이를 보존했습니다.\n\n'+table(['면','머리','어깨','벨트','바닥','결과'],[(n,*rows[n].values(),'통과(승인 범위 유지)') for n in names])+'\n\n## 가방 위치 4면 측정\n\n가방 위치는 트리밍된 자산 좌상단의 정수 평행이동 좌표입니다. 상단 손잡이/고리와 본체 윗변을 구분합니다. 본체 윗변-벨트는 모두 2px이며 상단 걸이 구간은 3~4px입니다. 가방 가림은 기존 몸·팔을 우선하는 마스크 합성입니다.\n\n'+table(['면','자산','좌상단(x,y)','벨트','본체 윗변','간격','노출 bbox'],[(n,placements[n]['asset'],placements[n]['translation'],placements[n]['belt_row'],placements[n]['box_top'],placements[n]['box_top_belt_gap'],placements[n]['visible_bbox']) for n in names])+'\n\n- 앞면: 화면 오른쪽 허리, 오른쪽 소매에 일부 가림.\n- 3/4: 중앙 (60,87)에서 앞쪽 골반 (78,85)로 이동. 기존 자리에는 저장된 가방 없는 복구 베이스를 사용했습니다.\n- 옆면: 반대쪽 엉덩이의 가방 전체를 몸 뒤에 두고 앞 모서리 x80..81만 노출합니다.\n- 뒷면: 화면 왼쪽 허리의 가방 측면을 노출합니다.\n\n## 이빨·Open·보존 검증\n\n닫힌 가방은 기존 뚜껑 빨간 띠·손잡이·모서리 리벳을 남기고 입 영역만 합성했습니다. 허리 윗니 3개, 든 크기 윗니 4개이며 아랫니 줄은 생성 픽셀을 정수 평행이동해 엇갈리게 조립했습니다. 흰 이빨은 앞면 세로 영역 절반 이상을 차지하며 틈은 승인 팔레트의 가장 어두운 색(25,24,31), 일부 내부는 어두운 빨강입니다. Open 2종은 경첩이 이어진 열린 뚜껑·상하 이빨·내부를 다시 생성했습니다. 허리 Open 20×23, 든 Open 28×30으로 열린 뚜껑 때문에 닫힌 자산보다 큽니다.\n\n3/4와 팔 4장 모두 **승인 Idle 0 대비 마스크 밖 RGBA 불일치 0**입니다. `Masks/Idle0_Arm_*.png`는 3/4 수정 영역과 자세 영역의 합집합입니다. 기존 `Masks/Arm_*.png`는 3/4 대비 자세 수정 영역입니다. Forward 마스크의 y88 아래는 실제 팔이 아니라 내린 팔을 지웠던 배경 복구 영역이므로, 그 영역에 새 가방을 다시 올렸습니다. 실제 뻗은 팔·장갑은 y88 위이며 픽셀 불일치 0입니다. 나머지 자세는 기존 팔 마스크 전체 불일치 0입니다.\n\n모든 생산 PNG는 승인 팔레트 내 20색 이하, 알파 0/255입니다. 캐릭터 8장 바닥은 127입니다. 원시 image_gen PNG에는 중간 알파·많은 색이 있을 수 있으며 생산 검증 대상과 분리합니다. 검수 확대본은 정수 픽셀 반복이며 생산 이미지 리샘플이 아닙니다. 생성 격자가 정확하지 않아 복원 후 최종 픽셀을 3배·4배·8배·16배로 확인했습니다.\n\n## 생성 이력·백업·재현\n\n이번 내장 image_gen 호출 '+str(len(attempts))+'회, 항목당 최대 '+str(max(counts.values()))+'회입니다. 이전 이력은 attempts.json의 inherited_history에 별도 기록했고 누락 프롬프트는 null로 남겼습니다. 이번 정확한 프롬프트는 `Sources/V9Fix_*_prompt.txt`, 원본은 `Sources/V9Fix_*_generated.png`입니다. 이전 산출물은 `Sources/prev/`에 보관했습니다.\n\n`python NEWIMAG/Px32HeroRef/Sources/fix_recover.py` → `python NEWIMAG/Px32HeroRef/Sources/fix_assemble.py` → `python NEWIMAG/Px32HeroRef/Sources/fix_report.py`. 기존 assemble.py·report.py 진입점도 새 작업으로 연결했습니다. 재생성은 image_gen을 다시 호출하지 않습니다.\n\n주요 검수 파일: `Turnaround/Turnaround_4x.png`, `Arms/Arms_4x.png`, `Bag/Bag_8x.png`, `Bag/BagTeeth_16x.png`. `validation.json`에는 파일별 치수·색상·알파·위치·보존 검증이 있습니다.\n'
(ROOT/'README.md').write_text(md,encoding='utf-8')
print(json.dumps(dict(native_files=len(native),max_colors=max(a['colors'] for a in native),outside_mask=idle_checks,arms=armchecks,imagegen_calls=len(attempts),rows=rows),ensure_ascii=False))
