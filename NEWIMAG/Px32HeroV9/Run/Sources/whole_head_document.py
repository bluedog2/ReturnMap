from pathlib import Path
import json,hashlib
from PIL import Image
import numpy as np
R=Path(__file__).resolve().parents[1];M=R.parent/'Move'
def dump(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf-8')
def read(p):return json.loads(p.read_text(encoding='utf-8'))
counts=[2,1,1,1,2,2,1,1,1,1]
names=['Run_Key0','Run_F1','Run_Key2','Run_F3','Run_F4','Run_F5','Run_I5-6','Run_F6','Run_F7','Run_I7-0']
for folder in [R,M]:
    v=read(folder/'validation.json');d=v['whole_head_redraw']
    d['visual_review']={'status':'reviewed','reviewer':'Codex','method':'Direct viewing of integer pixel-repeat 6x before/after crops and 3x full sprites','flat_slab':False,'upper_lower_separation':False,'interior_black_hole':False,'face_consistency':'same character, red eyes and mischievous smile reviewed visually; independently generated pixels are not pixel-identical','files':(['HeadBack_6x.png']+[f'Sources/WholeHead_pair{i}_6x.png' for i in range(5)]+['Sources/WholeHead_review3x.png']) if folder==R else ['Sources/WholeHead_Turn_review6x.png','Sources/WholeHead_Turn_boundary_3x.png']}
    for f in d['frames'].values():f['visual_review']='reviewed: coherent head/neck, no slab or upper/lower separation in changed heads'
    d['imagegen_calls_including_previous_session']=sum(counts) if folder==R else 1
    d['imagegen_calls_this_session']=9 if folder==R else 1
    d['resampling']=False;d['rotation']=False;d['code_drawing']=False
    d['asf_scope']='stage 4 check only; no automatic alignment or scaling'
    if folder==R:
        d['preserved_completed_sources']={str(i):{'path':f'Sources/WholeHead_{i:02}_final.png','sha256':hashlib.sha256((R/f'Sources/WholeHead_{i:02}_final.png').read_bytes()).hexdigest(),'regenerated_this_session':False,'rgba_mismatch_to_output':int(np.any(np.array(Image.open(R/f'Sources/WholeHead_{i:02}_final.png'))!=np.array(Image.open(R/(names[i]+'.png'))),2).sum())} for i in [0,5]}
        d['asf_status']='pass'
    else:
        old=read(M/'Sources/prev3/validation.json')['head_bundle_lock']['asf']
        d['asf_previous_failures']={k:x['failures'] for k,x in old.items()}
        d['asf_status']='RunStart and Run_L pass; RunStop and Turn retain position/height drift failures; not an all-pass result'
    dump(folder/'validation.json',v)
    a=read(folder/'attempts.json')
    a['whole_head_redraw']={'tool':'built-in image_gen','limit_per_frame':3,'calls_including_previous_session':sum(counts) if folder==R else 1,'calls_this_session':9 if folder==R else 1,'previous_session_calls':4 if folder==R else 0,'per_frame':{str(i):{'attempts':n,'selected':n,'generated':f'Sources/WholeHead_{i:02}_generated{n}.png','prompt':f'Sources/WholeHead_{i:02}_prompt{n}.txt' if i!=0 else None,'reason':'previously completed, retained unchanged' if i in [0,5] else ('attempt1 rejected: lifted tail left black gap; attempt2 connected' if i==4 else 'whole-head result visually reviewed at 6x')} for i,n in enumerate(counts)} if folder==R else {'Turn_F0':{'attempts':1,'selected':1,'generated':'Sources/WholeHead_Turn_generated1.png','prompt':'Sources/WholeHead_Turn_prompt1.txt'},'RunStart_F2_RunStop_F0_Run_L':{'attempts':0,'method':'reuse completed Run heads and exact horizontal mirror'}},'note':'Earlier unrelated revisions remain in their original attempt records. Whole-head counts include the interrupted session. Frame00 prompt was not present in recovery files; it is not reconstructed as an exact historical prompt.'}
    dump(folder/'attempts.json',a)
table='''| 항목 | 결과 | 검증 |
|---|---|---|
| 완료 00·05 | 기존 WholeHead_00/05_final 그대로 채택 | 이번 재생성 0회, 출력 RGBA 일치 |
| 나머지 8장 | 얼굴·머리 전체·목을 장별 한 번의 생성 결과로 합성 | 04 첫 결과 틈으로 폐기, 두 번째 채택 |
| 머리 뒤~목 | 곡선 머리카락과 깃 안으로 들어가는 목 | HeadBack_6x 및 5개 6배 비교본 직접 검토: 판·위아래 분리 없음 |
| 원본 보존 | 몸·팔·다리·가방·체인 보호 | Run·Move 마스크 밖 불일치 0 |
| 크기·픽셀 | 축소·리샘플 없음, 원본 머리 꼭대기 유지 | 전 장 20색, 승인 Idle 팔레트, 알파 0/255, 셀128·바닥127 |
| Move 연결 | Turn F0·RunStart F2·RunStop F0·Run_L_10 갱신 | 연결 끝점·머리 미러 불일치 0 |
| ASF ④ | Run·Run_L·RunStart 통과 | RunStop·Turn의 기존 위치/높이 드리프트 항목 잔존 |
| 생성 횟수 | 이전 실행 포함 Run 13회 + Turn 1회 | 00·04·05 각 2/3, 나머지 각 1/3; 이번 실행 10회 |
| 출력·보존 | GIF·Sheet_3x·HeadBack_6x·validation·attempts 갱신 | GIF 재디코딩 불일치 0, prev4·prev3 덮어쓰기 없음 |
'''
section='''## 머리 통째로 다시 그리기 (2026-11-14)

이번 결과는 `validation.json.whole_head_redraw`다. 이전 절은 이력이며 얼굴/뒤통수/꼬리 조각을 따로 붙이는 방식은 중단했다. 사용자가 완료로 지정한 00·05는 원본 final을 다시 그리거나 복원하지 않고 그대로 채택했다. 나머지 8장과 Turn F0는 **내장 image_gen**으로 얼굴·앞머리·옆머리·뒤통수·머리끈·포니테일 전체·목을 한 번에 그린 결과를 사용했다.

'''+table+'''
시트 순서는 F0,F1,F2,F3,F4,F5,I5,F6,F7,I7, 머리 꼭대기 y는 27,27,26,27,25,25,25,26,25,25다. 얼굴·표정·머리끈 디자인은 완료 00·05를 참조해 시각적으로 맞췄다. 각 장을 독립적으로 그렸으므로 얼굴 픽셀이 완전히 같은 템플릿이라는 뜻은 아니다. 머리 크기는 리샘플링 없이 원본 논리 격자를 복원하고 전체 머리를 정수 이동해 맞췄다.

마스크는 기존 깃 경계 위 머리 전체와 목, 깃 상단 1~2px를 대상으로 했다. Run은 기존 복구 스크립트의 연속 마스크, Turn은 그 경계를 (-20,-4) 이동한 마스크를 사용했다. 생성 그림에서 블록 중심 복원·승인 팔레트 매핑·알파 이진화·머리 전체 정수 이동·마스크 합성만 수행했다. 부품별 얼굴/뒤통수/포니테일 교체나 코드 드로잉, 회전, 리샘플링은 없다.

`HeadBack_6x.png`는 x45~95/y18~86, 위 직전/아래 새 결과를 6배 픽셀 반복한 비교다. `Sources/WholeHead_pair0~4_6x.png`는 두 장씩 x45~107로 넓혀 실제 6배로 검토했다. 04 첫 생성은 뿌리 아래가 들려 틈이 생겨 폐기했으며, 두 번째 전체 머리 생성본에서 연결을 확인했다. 전체 시트와 Turn 연결 전신도 직접 확인했다.

ASF는 ④ `check()`만 호출했다. RunStop F1 가로 중심과 Turn F1/F2 가로 중심 드리프트, Turn F0 높이105의 평균 대비 ±2px 초과는 남는다. 기존 이동 자세·머리 꼭대기·몸을 보존했으며 전체 ASF 통과로 표시하지 않았다. 상세 실패 목록과 직전 목록은 Move validation에 저장했다.

프롬프트는 `Sources/WholeHead_*_prompt*.txt`, 생성 원본은 `WholeHead_*_generated*.png`, 복원 좌표는 `WholeHead_recovery.json`이다. 이전 실행의 00 프롬프트는 남아 있지 않아 추정 복원하지 않았다. 이전 실행 4회도 횟수에 포함했다. Run/Sources/prev4 및 Move/Sources/prev3는 기존 백업을 그대로 보존했다. Assets·다른 NEWIMAG 폴더 수정과 git 사용은 없다.

재조립: `python NEWIMAG/Px32HeroV9/Run/Sources/whole_head_finalize.py`, 검토 후 기록은 `whole_head_document.py`. 원본 재복원이 필요하면 `whole_head_recover.py 1 2 3 4 6 7 8 9`와 `whole_head_turn_recover.py`를 사용한다. 완료된 00·05는 재복원하지 않는다.
'''
p=R/'README.md';s=p.read_text(encoding='utf-8');s=s.split('## 머리 통째로 다시 그리기 (2026-11-14)')[0];p.write_text('> 최신 결과: 아래 「머리 통째로 다시 그리기」 및 validation.json.whole_head_redraw.\n\n'+s+'\n'+section,encoding='utf-8')
p=M/'README.md';s=p.read_text(encoding='utf-8');s=s.split('## 머리 통째로 다시 그리기 (2026-11-14)')[0];p.write_text('> 최신 머리 결과: 아래 「머리 통째로 다시 그리기」.\n\n'+s+'\n## 머리 통째로 다시 그리기 (2026-11-14)\n\nTurn F0는 내장 image_gen 1/3회로 머리 전체·목을 한 번에 생성했다. RunStart F2=새 Run00, RunStop F0=새 Run09, Run_L_10은 새 Run 머리의 정확한 좌우 미러다. 몸·팔·다리·가방·체인 마스크 밖 불일치 0, 연결 끝점 0, 20색·알파0/255·바닥127. GIF·Sheet_3x 갱신 및 재디코딩 일치. 기존 Sources/prev3는 덮어쓰지 않았다.\n\nASF ④ RunStart·Run_L 통과, RunStop F1 중심과 Turn의 중심/높이 드리프트는 잔존한다. 상세는 validation.json.whole_head_redraw. 전체 작업 요약은 ../Run/README.md의 동명 절을 참조한다. 프롬프트·원본·6배 비교는 Sources/WholeHead_Turn_*, 복원은 WholeHead_recovery.json.\n',encoding='utf-8')
for folder in [R,M]:
    files={str(p.relative_to(folder)):hashlib.sha256(p.read_bytes()).hexdigest() for p in folder.rglob('*') if p.is_file() and not any(x.startswith('prev') for x in p.relative_to(folder).parts) and p.name!='WholeHead_output_manifest.json'}
    dump(folder/'Sources/WholeHead_output_manifest.json',files)
print(table)
