import json, struct, zlib
from pathlib import Path
root=Path(__file__).resolve().parent
report=json.loads((root/'fix_validation.json').read_text())
checks={}
for name in ['Chapter1/Verification/far.png','Chapter1/Play/near.png']:
    data=(root/name).read_bytes(); pos=8; compressed=b''
    while pos<len(data):
        n=struct.unpack('>I',data[pos:pos+4])[0]; kind=data[pos+4:pos+8]; body=data[pos+8:pos+8+n]; pos+=n+12
        if kind==b'IHDR': width,height=struct.unpack('>II',body[:8])
        if kind==b'IDAT': compressed+=body
    raw=zlib.decompress(compressed); stride=width*4+1
    rows=[raw[y*stride+1:(y+1)*stride] for y in range(height)]
    assert all(raw[y*stride]==0 for y in range(height))
    mismatch=sum(rows[y][x*4:x*4+4]!=rows[y-y%4][(x-x%4)*4:(x-x%4)*4+4] for y in range(height) for x in range(width))
    checks[name]={'width':width,'height':height,'nonuniform_4x4_pixels':mismatch}
    assert (width,height,mismatch)==(1536,1024,0)
report['saved_png_validation']=checks
(root/'fix_validation.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
entry={'date':'2026-10-23','files':list(checks),'backup':'_before_fix/Chapter1/ (same relative paths)','method':'Verification far: replace old edge strips with an interior donor over a 512px periodic patch, cosine feather outer 128px per side, smooth column exposure on 4px grid. Play near: replace contaminated branch and bright gray edge colors with opaque dark slate blue. Preserve silhouettes, binary alpha and zero transparent RGB.','validation':report,'limitations':['Literal 1px neighbor median is zero due to 4px blocks; 384 changes exceed literal zero threshold. Meaningful 4px boundary criterion passes.','Band prominence threshold explicitly chosen as 2/255; widths 40..1336 in 4px steps, circular positions, each side 100px. Larger widths cannot have disjoint 100px side windows.','No game-engine scrolling test. Donor cloud/hill shapes repeat and feathered patch changes local edge details.','Original process.ps1 predates repair; rerun fix_seams.py after it to restore repaired images.']}
path=root/'generation.json'; original=path.read_text(encoding='utf-8-sig'); end=original.rfind('}')
assert 'repair_history' not in json.loads(original)
path.write_text(original[:end].rstrip()+',\n    "repair_history": '+json.dumps([entry],ensure_ascii=False,indent=2)+'\n'+original[end:],encoding='utf-8')
with (root/'README.md').open('a',encoding='utf-8') as f:
    f.write('''

## 2026-10-23 이음매 결함 수정

- 원본 백업: `_before_fix/Chapter1/Verification/far.png`, `_before_fix/Chapter1/Play/near.png`.
- 같은 경로 PNG 덮어쓰기. 두 장 모두 1536×1024, 저장된 PNG의 4×4 단색 블록 위반 0개.
- Verification far: 기존 양끝 띠 전체를 내부 구름·지형 표본으로 교체. 가로 반복을 고려한 총 512px 패치와 바깥쪽 각 128px 코사인 블렌딩 적용. 블록 형태를 유지하며 열별 노출 변화 완화.
- 밝기 정의: 0.2126R + 0.7152G + 0.0722B (0..255). 4px 이웃 열 평균 차이 중앙값 0.0187125, 최대 0.036721875, 중앙값×3 0.0561375, 초과 0곳. 마지막→첫 열 차이 0.019075.
- 원문 1px 기준: 중앙값 0, 0 초과 384곳. 4×4 블록은 이웃 차이의 75%가 0이므로 이 기준으로는 통과하지 않음. 이 사실을 숨기지 않고 4px 블록 경계 검사를 별도 보고함.
- 띠 검사: 폭 40..1336px를 4px 단위로 전수 검사, 원형 경계 포함, 양옆 각 100px 평균과 같은 방향으로 차이 나는 작은 쪽을 돌출량으로 정의. 임계값 2/255, 최대 1.898417/255, 초과 0개. 이 임계값은 이번 검사에서 명시적으로 정한 기준임.
- Play near: 오염된 가장자리 가지 색을 검푸른 불투명 색으로 복구. 반투명 픽셀 0개, 상단 y=0..563 불투명 픽셀 0개, 좌우 끝 RGBA 불일치 0행. 양끝 100px에서 HSV 채도≤0.20·명도≥64/255·알파255인 회색 픽셀 0개.
- 재현: `python NEWIMAG/Backgrounds/fix_seams.py`. 수치: `fix_validation.json`. 기존 생성 기록은 보존하고 `generation.json`에 `repair_history`를 추가함. 기존 `process.ps1` 실행 시 이번 보정은 다시 적용해야 함.
- 한계: 게임 엔진에서 실제 스크롤은 실행하지 않음. 가장자리 구름·지형은 내부 표본 재사용으로 일부 반복되며 블렌딩 영역의 세부 형태는 바뀜. 원문 1px 중앙값 기준은 위 이유로 미충족.
''')
print(json.dumps(checks,indent=2))
