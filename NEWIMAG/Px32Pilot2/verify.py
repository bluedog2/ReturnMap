"""Independent final disk audit; run after process.py with python -B."""
from pixel_io import *
report=json.loads((ROOT/'validation.json').read_text(encoding='utf8'))
gen=json.loads((ROOT/'generation.json').read_text(encoding='utf8'))
for name,rec in gen['files'].items():
    a=readpng(ROOT/rec['one_x']);b=readpng(ROOT/rec['file'])
    k=2 if 'Backgrounds' in rec['file'] else 8
    assert b==up(a,k)
    assert all(p[:3] in {q[:3] for q in MP} and p[3] in (0,255) for row in b for p in row)
    assert hashlib.sha256((ROOT/rec['file']).read_bytes()).hexdigest()==report[name]['sha256']
sheet=readpng(ROOT/'_1x'/'Hero_Idle.png')
old=readpng(ROOT.parent/'Px32Pilot'/'_1x'/'Hero_Idle.png')
for i in range(6):
    a=[r[i*64:(i+1)*64] for r in sheet]
    bad=0
    for y,row in enumerate(a):
        for x,p in enumerate(row):
            if p[3] and any(xx<0 or xx>=64 or yy<0 or yy>=64 or not a[yy][xx][3] for xx,yy in [(x-1,y),(x+1,y),(x,y-1),(x,y+1)]):
                bad+=p not in [HP[0],HP[4],HP[7],HP[14]]
    assert bad==0
    report['Hero_Idle']['frames'][i]['outline_boundary_color_errors']=bad
    report['Hero_Idle']['frames'][i]['width']=bbox(a)[2]-bbox(a)[0]+1
report['Hero_Idle']['frame0_difference_from_pilot1']=sum(sheet[y][x]!=old[y][x] for y in range(64) for x in range(64))
report['final_disk_audit']='PASS'
(ROOT/'validation.json').write_text(json.dumps(report,indent=2),encoding='utf8')

# Native APNG preview, 8fps, each 64px frame enlarged nearest-neighbor 3x.
def chunk(t,d):return struct.pack('>I',len(d))+t+d+struct.pack('>I',zlib.crc32(t+d)&0xffffffff)
parts=[b'\x89PNG\r\n\x1a\n',chunk(b'IHDR',struct.pack('>IIBBBBB',192,192,8,6,0,0,0)),chunk(b'acTL',struct.pack('>II',6,0))]
seq=0
for i in range(6):
    a=up([r[i*64:(i+1)*64] for r in sheet],3)
    parts.append(chunk(b'fcTL',struct.pack('>IIIIIHHBB',seq,192,192,0,0,1,8,0,0)));seq+=1
    data=zlib.compress(b''.join(b'\0'+bytes(c for p in row for c in p) for row in a),9)
    if i==0:parts.append(chunk(b'IDAT',data))
    else:parts.append(chunk(b'fdAT',struct.pack('>I',seq)+data));seq+=1
parts.append(chunk(b'IEND',b''))
(ROOT/'Hero_Idle_Loop_3x.apng').write_bytes(b''.join(parts))

lines=['# Px32Pilot2 결과','',
       '모든 신규 파일은 이 폴더 안에만 저장. git 및 Unity 리소스/설정 변경 없음.',
       '타일은 1차 파일럿 생성 픽셀의 내부 변형, 주인공은 기존 첫 프레임의 정수 픽셀 이동. 배경 3원본은 built-in image_gen 생성, Verification_Far는 Play_Far 색상 대응.',
       '프롬프트 전문과 생성 경로: sources.json / generation.json. 원본 복사: _sources/.',
       '', '## 파일 목록','']
for name,rec in gen['files'].items():lines.append('- `'+rec['file'].replace('\\','/')+'` / 1배: `'+rec['one_x'].replace('\\','/')+'`')
lines+=['','- `Preview_3x.png`: Play/Build 각각 8x4 혼합 배치 + 6프레임 + 배경 합성 축소.',
        '- `Tile_Random_3x3.png`: 종류별 3x3 무작위 변형 타일 검사.',
        '- `Hero_Idle_Loop_3x.apng`: 8fps 무한 반복 프리뷰.',
        '- `process.py`, `pixel_io.py`, `verify.py`: 재현 및 검사 스크립트.',
        '- `sources.json`, `generation.json`, `validation.json`: 출처·처리·검사 기록.',
        '', '## 검사','',
        '- 21개 최종 에셋: 팔레트 밖 색 0, 알파는 0/255, 최근접 확대 블록 오차 0.',
        '- 타일 44개 순서쌍 × 2방향 = 88개 경계 검사 불일치 0. 공통 2px 테두리 차이 0.',
        '- 배경 4개 좌우 끝 열 불일치 0. 근경 상단 128행 불투명 픽셀 0.',
        '- 바닥 행 평균 명도 범위: '+str(report['floor_horizontal_row_luminance_range']),
        '', '| 프레임 | PNG 최하단 y | 발 기준 행 | 몸통 중심 x | 키 | 다음 프레임 차이 픽셀 |',
        '|---|---|---|---|---|---|']
for r in report['Hero_Idle']['frames']:
    lines.append('| '+' | '.join(str(r[k]) for k in ['frame','bottom_png_y','bottom_origin_row','torso_center_x','height','difference_to_next_pixels'])+' |')
lines+=['','차이는 1배 RGBA 기준, 마지막 프레임은 첫 프레임과 비교. 중심은 1차와 동일하게 y34..47의 청색 갑옷 경계 중심으로 측정.',
        '', '## 한계','',
        '- Unity 실제 플레이/카메라 이동/패럴랙스 검사는 미실행.',
        '- 경계 픽셀 일치는 지각적인 반복 무늬의 완전 제거를 보장하지 않음. 작은 변형 세트의 반복은 남을 수 있음.',
        '- Verification_Near는 같은 위치의 성벽·나무·전경 구도를 유지하되 식생 추가로 픽셀 실루엣이 달라짐.',
        '- 배경 생성 결과를 2x2 픽셀 덩어리로 정리해 세부 묘사가 단순화됨.',
        '', '재현: `python -B NEWIMAG/Px32Pilot2/process.py` 후 `python -B NEWIMAG/Px32Pilot2/verify.py`.']
(ROOT/'README.md').write_text('\n'.join(lines)+'\n',encoding='utf8')
print(json.dumps({'audit':'PASS','summary':report['summary'],'color_counts':{k:v['opaque_colors'] for k,v in report.items() if k in gen['files']},'stripe':report['floor_horizontal_row_luminance_range'],'hero':report['Hero_Idle']['frames']},indent=2))
