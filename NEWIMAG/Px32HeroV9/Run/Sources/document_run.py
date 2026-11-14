from complete_run import *

v=json.loads((O/'validation.json').read_text(encoding='utf-8'));r=v['remaining_and_10'];d=json.loads((S/'run8_measurements.json').read_text(encoding='utf-8'))
visual={
 '머리·얼굴':'통과: 원본 백금 포니테일·밴드·Z핀·눈·표정 보존. 작은 송곳니/피어싱은 원본 보존 근거 병기',
 '외투·긴 소매':'통과: 흰 퍼프 소매가 손목까지 연결, 빨간 안감, 맨 팔뚝 없음',
 '검은 장갑':'통과: 소매 끝에 검은 핑거리스 장갑, 빈손',
 '상의·하의':'통과: 크롭 탑·초커·반바지·벨트 원본 유지, 허리 가방 가림 제외',
 '비대칭 다리·신발':'통과: 검정/빨강 양말 및 원본 달리기 다리·하이탑 유지, 새 허리 가방 가림 제외',
 '가방 이빨':'통과: 승인 상자·빨간 띠·리벳·맞물린 이빨 원본 레이어 그대로',
 '허리춤 높이':'통과: 승인 키의 7px 상향 기준, 재킷 밑단 아래·엉덩이 옆·뒤 허벅지 앞',
 '손에 든 가방':'없음: 옛 큰 가방 제거, 빈손과 허리 가방 분리',
 '크기':'통과: 축소·회전 없음, 원본 머리 꼭대기와 바닥 보존'}
for name,ent in r['frames'].items():
 ent['visual_3x']=visual.copy();ent['visual_review_file']=f'Sources/{name}_review_3x.png';ent['visible_bag_pixel_mismatch']=0
 if name.startswith('F'): a=read(O/(f'Run_Key{name[1:]}.png' if name in ['F0','F2'] else f'Run_{name}.png'));l=read(S/f'{name}_bag_layer.png')
 else:
  i=int(name[1:]);a=read(O/f'Run_I{i}-{(i+1)%8}.png');l=read(S/f'I{i}_bag_layer.png')
 assert np.array_equal(a[l[:,:,3]>0],l[l[:,:,3]>0])
r['visual_review']='PASS: individual 3x images and final two-row 3x sheet; 8x hip supplement'
r['method']={'art':'built-in image_gen only','recovery':'integer pitch 10 / origin (-17,-11), nearest approved palette, binary alpha','bag_rule':'previous original-frame hip + (-11,-2) + vertical bounce; approved keys untouched','inbetween':'local rear sleeve/jacket pose; preceding head/front arm/legs/shoes preserved; bag integer midpoint round half up','mask_fix':'F1 removal polygon bottom extended 110 to 112 to remove old bag bottom line','asf':'import stage 4 check() and sheet_spec(); no placement or scale_frames','key_history':'existing top-level key metrics predate Claude raise; this remaining_and_10 section validates current approved key pixels'}
dump('validation.json',v)
rows=[]
for name in r['order']:
 e=r['frames'][name];isf=name.startswith('F');i=int(name[1:]);pos=e['bag_position'];attempt='승인 고정' if name in ['F0','F2'] else ('2/3' if name in ['F4','F5'] else '1/3')
 rows.append(f'| {name} | {tuple(pos)} | {attempt} | 통과 | 통과 | 통과 | 통과 | 통과·빈손 | {e["outside_mask_mismatch"]} |')
summary='''| 항목 | 결과 | 검증 |
|---|---|---|
| 승인 키 0·2 | 원본 파일 그대로 유지 | SHA256 동일 |
| 나머지 6장 | Run_F1·3·4·5·6·7.png | 긴 소매·검은 장갑·빈손, 장별 1~2/3회 |
| 8장 시트 | Hero_Run_8.png, 1024×128 · 18fps | 0~7 순서 |
| 10장 시트 | Hero_Run_10.png, 1280×128 · 22fps | 5→6·7→0 사이 추가, 주기 약 0.455초 |
| 가방 | 승인 높이·정수 이동·1장 지연 | 전 장 허리춤·손에 든 가방 없음 |
| 픽셀 검증 | 20색·알파 0/255·바닥 127 | 마스크 밖 불일치 0·머리 꼭대기 원본 동일 |
| ASF ④ | 정렬·색·바닥 통과 | 기존 8장 규격과 10장 차이만 예상 경고 |
| 미리보기 | 8·10장 GIF, 비교 GIF, 3배·허리 8배 시트 | GIF 재디코딩 불일치 0 |'''
section='''

## 나머지 장·10장 (2026-11-07)

사용자가 승인한 Run_Key0/2는 수정하지 않았다. 아래는 「Claude 조정」 이후 최종 키를 기준으로 한 현재 결과다. 앞 절과 기존 JSON 최상위 키 기록은 7px 상향 전 작업 이력이며, 현재 검증은 `validation.json`의 `remaining_and_10`을 따른다.

'''+summary+'''

### 가방 높이와 시간 지연

최종 승인 Key0의 가방 좌상단은 (52,95), Key2는 (50,90)이다. 기존 placement와 raise_bag를 함께 해석하면 현재 엉덩이 기준의 기본 오프셋은 **(-11,-2)**이고 세로 튐 +1/-1이 각각 반영된다. 나머지는 같은 오프셋을 순환하는 **이전 원본 장의 엉덩이 기준점**에 적용했다. 가방과 6행 체인은 동일하게 정수 이동하며, 체인은 가방 위치 +(5,-6)이다. Claude 조정처럼 가방을 뒤 허벅지 앞에 얹었다. 손에 든 큰 가방은 전 장 제거했다.

엉덩이 기준점 F0~F7: (63,96), (61,93), (61,93), (62,95), (63,94), (62,92), (63,94), (63,96). 원본 확대본의 반바지 뒤쪽 바깥 모서리를 읽은 육안 랜드마크이며 전신 정합 좌표가 아니다. 튐은 [1,0,-1,0,1,0,-1,0]px. 키0/2도 이 수식과 일치하지만 파일은 다시 쓰지 않았다. 사이 장의 가방은 앞뒤 위치의 평균을 정수 반올림(0.5 위로)한다. 지연은 원본 8장 시간축 기준이며 삽입 장에서는 중간 위치로 이은다.

### 장별 3배 눈 검사

`Sources/Final_review_3x.png`와 각 장의 `*_review_3x.png`를 확인했다. 가방 높이는 `Waist_8_8x.png`·`Waist_10_8x.png`로 보완했다. 머리·얼굴·앞팔·다리·신발은 원본 보존, 새 가방의 가림은 허용한다. 4/5번 첫 생성은 제거 영역에 새 신발 조각이 생겨 폐기하고 두 번째를 채택했다. 1번의 옛 가방 밑줄은 제거 마스크 아래 끝을 y112로 넓혀 생성본의 투명 배경으로 교체했다. 코드는 새 선이나 색을 그리지 않았다.

| 장 | 가방 좌상단 | 생성 시도 | 긴 소매 | 장갑 | 이빨 | 비대칭 다리 | 허리 높이·손 | 마스크 밖 불일치 |
|---|---|---|---|---|---|---|---|---|---|
'''+ '\n'.join(rows)+'''

### 사이 프레임과 보존 범위

최종 8장 순환 RGBA 변화량은 '''+', '.join(f'{i}→{(i+1)%8}: {n}' for i,n in enumerate(d['cyclic_changes']))+''' 픽셀이다. 가장 큰 **7→0(4988), 5→6(4815)**에 한 장씩, 각 내장 image_gen 1/3회로 생성했다. 순서는 **F0, F1, F2, F3, F4, F5, I5, F6, F7, I7**.

Idle README의 부분 보간 방식을 적용했다. 앞뒤의 가방 없는 장을 참조하여 뒤팔·소매·재킷 밑단의 중간 자세를 생성하고 해당 마스크만 합성했다. **전신/다리 보간은 아니다.** 머리·앞팔·다리·신발은 앞 장의 원본 자세를 그대로 유지한다. 가방은 I5=(52,91), I7=(52,94)에 동일 레이어를 정수 이동했다. 생성된 1254×1254 원본은 정수 간격 10, 원점 (-17,-11)의 블록 중심으로 복원했다. 승인 Idle 팔레트 최근접 매핑·알파 128 임계값·마스크 합성만 적용했다. 리샘플·회전 없음.

### ASF ④ 검사

`tools/sprite/asf-to-px32.py`의 **check()와 sheet_spec()만 호출**했다. 이미 정렬된 최종 시트에 배치 이동도 하지 않았고 ② scale_frames를 실행하지 않았다. `Sources/ASF_check.json`: 10장 모두 20색, 팔레트 밖 0, 알파 0/255, 바닥 127, 가로 bbox 중심 63.5~64.0px, 높이 101~104px. 정렬·높이 드리프트·색·알파·클립·바닥 실패 없음. 머리 꼭대기는 원본의 y24~27 차이를 보존했다. 규격표 Hero_Run=8과 새 기준 10의 차이는 **예상 dimensions 경고**로 따로 기록했다.

### 출력·재현

`Hero_Run_8_3x.gif`는 18fps(9주기=4초), `Hero_Run_10_3x.gif`는 22fps(11주기=5초). GIF 10ms 시간 단위에 맞춰 50/60ms와 40/50ms를 분배했다. 단일 주기 PNG 순서는 각각 8장/10장이고 게임 타이밍은 8/18초, 10/22초다. `Compare_3x.gif`는 **왼쪽 원본 @18 / 가운데 새 8장 @18 / 오른쪽 10장 @22**, 각자 시간축으로 재생하며 공통 주기 20초 후 반복한다. 모든 GIF를 재디코딩하여 보이는 RGBA 픽셀 불일치 0을 확인했다.

`Hero_Run_8_3x.png`, `Hero_Run_10_3x.png`, `Sheet_3x.png`(위 8장/아래 10장), `Waist_8_8x.png`, `Waist_10_8x.png`는 정확한 픽셀 반복 확대다. `Masks/`에 생성/최종 합성 마스크, `Sources/`에 모든 생성 원본·프롬프트·블록 복원본·배치 측정·ASF 검사·코드를 저장했다. 그림 생성은 내장 image_gen만 사용했다.

프로젝트 루트에서 `python NEWIMAG/Px32HeroV9/Run/Sources/complete_run.py`, `python NEWIMAG/Px32HeroV9/Run/Sources/finish_run.py`, `python NEWIMAG/Px32HeroV9/Run/Sources/document_run.py` 순서로 재현한다. 기존 finalize/raise_bag 스크립트는 승인 키를 다시 쓰므로 실행하지 않았다. 출력은 Run 폴더 안으로 제한했으며 Assets·다른 NEWIMAG 폴더·git은 건드리지 않았다.
'''
p=O/'README.md';t=p.read_text(encoding='utf-8');marker='\n\n## 나머지 장·10장 (2026-11-07)';t=t.split(marker)[0];p.write_text(t+section,encoding='utf-8')
print(summary)
