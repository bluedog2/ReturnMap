# Px32Pilot2 결과

모든 신규 파일은 이 폴더 안에만 저장. git 및 Unity 리소스/설정 변경 없음.
타일은 1차 파일럿 생성 픽셀의 내부 변형, 주인공은 기존 첫 프레임의 정수 픽셀 이동. 배경 3원본은 built-in image_gen 생성, Verification_Far는 Play_Far 색상 대응.
프롬프트 전문과 생성 경로: sources.json / generation.json. 원본 복사: _sources/.

## 파일 목록

- `Tiles/Tile_Play_Floor_A.png` / 1배: `_1x/Tile_Play_Floor_A.png`
- `Tiles/Tile_Build_Floor_A.png` / 1배: `_1x/Tile_Build_Floor_A.png`
- `Tiles/Tile_Play_Floor_B.png` / 1배: `_1x/Tile_Play_Floor_B.png`
- `Tiles/Tile_Build_Floor_B.png` / 1배: `_1x/Tile_Build_Floor_B.png`
- `Tiles/Tile_Play_Floor_C.png` / 1배: `_1x/Tile_Play_Floor_C.png`
- `Tiles/Tile_Build_Floor_C.png` / 1배: `_1x/Tile_Build_Floor_C.png`
- `Tiles/Tile_Play_Wall_A.png` / 1배: `_1x/Tile_Play_Wall_A.png`
- `Tiles/Tile_Build_Wall_A.png` / 1배: `_1x/Tile_Build_Wall_A.png`
- `Tiles/Tile_Play_Wall_B.png` / 1배: `_1x/Tile_Play_Wall_B.png`
- `Tiles/Tile_Build_Wall_B.png` / 1배: `_1x/Tile_Build_Wall_B.png`
- `Tiles/Tile_Play_Wall_C.png` / 1배: `_1x/Tile_Play_Wall_C.png`
- `Tiles/Tile_Build_Wall_C.png` / 1배: `_1x/Tile_Build_Wall_C.png`
- `Tiles/Tile_Play_Ceiling_A.png` / 1배: `_1x/Tile_Play_Ceiling_A.png`
- `Tiles/Tile_Build_Ceiling_A.png` / 1배: `_1x/Tile_Build_Ceiling_A.png`
- `Tiles/Tile_Play_Ceiling_B.png` / 1배: `_1x/Tile_Play_Ceiling_B.png`
- `Tiles/Tile_Build_Ceiling_B.png` / 1배: `_1x/Tile_Build_Ceiling_B.png`
- `Player/Hero_Idle.png` / 1배: `_1x/Hero_Idle.png`
- `Backgrounds/Play_Far.png` / 1배: `_1x/Play_Far.png`
- `Backgrounds/Verification_Far.png` / 1배: `_1x/Verification_Far.png`
- `Backgrounds/Play_Near.png` / 1배: `_1x/Play_Near.png`
- `Backgrounds/Verification_Near.png` / 1배: `_1x/Verification_Near.png`

- `Preview_3x.png`: Play/Build 각각 8x4 혼합 배치 + 6프레임 + 배경 합성 축소.
- `Tile_Random_3x3.png`: 종류별 3x3 무작위 변형 타일 검사.
- `Hero_Idle_Loop_3x.apng`: 8fps 무한 반복 프리뷰.
- `process.py`, `pixel_io.py`, `verify.py`: 재현 및 검사 스크립트.
- `sources.json`, `generation.json`, `validation.json`: 출처·처리·검사 기록.

## 검사

- 21개 최종 에셋: 팔레트 밖 색 0, 알파는 0/255, 최근접 확대 블록 오차 0.
- 타일 44개 순서쌍 × 2방향 = 88개 경계 검사 불일치 0. 공통 2px 테두리 차이 0.
- 배경 4개 좌우 끝 열 불일치 0. 근경 상단 128행 불투명 픽셀 0.
- 바닥 행 평균 명도 범위: {'pilot1': 84.9254, 'pilot2': {'A': 19.3066, 'B': 19.3066, 'C': 20.3805}, 'note': 'Lower row-mean range reduces horizontal band dominance; not a perceptual guarantee.'}

| 프레임 | PNG 최하단 y | 발 기준 행 | 몸통 중심 x | 키 | 다음 프레임 차이 픽셀 |
|---|---|---|---|---|---|
| 0 | 63 | 0 | 31.5 | 48 | 382 |
| 1 | 63 | 0 | 31.5 | 49 | 65 |
| 2 | 63 | 0 | 31.5 | 49 | 385 |
| 3 | 63 | 0 | 31.5 | 48 | 367 |
| 4 | 63 | 0 | 31.5 | 47 | 65 |
| 5 | 63 | 0 | 31.5 | 47 | 370 |

차이는 1배 RGBA 기준, 마지막 프레임은 첫 프레임과 비교. 중심은 1차와 동일하게 y34..47의 청색 갑옷 경계 중심으로 측정.

## 한계

- Unity 실제 플레이/카메라 이동/패럴랙스 검사는 미실행.
- 경계 픽셀 일치는 지각적인 반복 무늬의 완전 제거를 보장하지 않음. 작은 변형 세트의 반복은 남을 수 있음.
- Verification_Near는 같은 위치의 성벽·나무·전경 구도를 유지하되 식생 추가로 픽셀 실루엣이 달라짐.
- 배경 생성 결과를 2x2 픽셀 덩어리로 정리해 세부 묘사가 단순화됨.

재현: `python -B NEWIMAG/Px32Pilot2/process.py` 후 `python -B NEWIMAG/Px32Pilot2/verify.py`.
