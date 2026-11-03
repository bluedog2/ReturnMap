# WallSeamFix

32×32 PNG 13장. 좌표는 0부터 시작. 가장자리 4~27행의 막대색 #2A282D / #303940만 대응 기본 타일(A/B/C)의 옆 열에서 같은 행 콘크리트 질감을 복사해 교체했다. 다른 가장자리 색과 내부 픽셀은 보존했다. 새 도형·색·AA·디더 없음.

| 타일 | 변경 내용 | 위 0행 / 아래 31행 원본 일치 | 색 수 (RGBA) |
|---|---|---|---|
| Tile_Play_Wall_A.png | x=0, y=4..27의 차콜 막대색을 A 원본 x=1 같은 행 콘크리트로 교체 (24 px); 기타 색 보존 | PASS / PASS | 6 |
| Tile_Play_Wall_B.png | x=0, y=4..27의 차콜 막대색을 B 원본 x=1 같은 행 콘크리트로 교체 (24 px); 기타 색 보존 | PASS / PASS | 6 |
| Tile_Play_Wall_C.png | x=31, y=4..27의 차콜 막대색을 C 원본 x=30 같은 행 콘크리트로 교체 (24 px); 기타 색 보존 | PASS / PASS | 6 |
| Tile_Play_Wall_T0B1.png | x=31, y=4..27의 차콜 막대색을 C 원본 x=30 같은 행 콘크리트로 교체 (20 px); 기타 색 보존 | PASS / PASS | 17 |
| Tile_Play_Wall_T0B2.png | x=31, y=4..27의 차콜 막대색을 C 원본 x=30 같은 행 콘크리트로 교체 (24 px); 기타 색 보존 | PASS / PASS | 18 |
| Tile_Play_Wall_T1B0.png | x=0, y=4..27의 차콜 막대색을 A 원본 x=1 같은 행 콘크리트로 교체 (24 px); 기타 색 보존 | PASS / PASS | 18 |
| Tile_Play_Wall_T1B1.png | x=31, y=4..27의 차콜 막대색을 C 원본 x=30 같은 행 콘크리트로 교체 (22 px); 기타 색 보존 | PASS / PASS | 17 |
| Tile_Play_Wall_T1B1b.png | x=0, y=4..27의 차콜 막대색을 A 원본 x=1 같은 행 콘크리트로 교체 (24 px); 기타 색 보존 | PASS / PASS | 8 |
| Tile_Play_Wall_T1B2.png | x=31, y=4..27의 차콜 막대색을 C 원본 x=30 같은 행 콘크리트로 교체 (24 px); 기타 색 보존 | PASS / PASS | 17 |
| Tile_Play_Wall_T2B0.png | x=0, y=4..27의 차콜 막대색을 A 원본 x=1 같은 행 콘크리트로 교체 (24 px); 기타 색 보존 | PASS / PASS | 16 |
| Tile_Play_Wall_T2B1.png | x=31, y=4..27의 차콜 막대색을 C 원본 x=30 같은 행 콘크리트로 교체 (18 px); 기타 색 보존 | PASS / PASS | 19 |
| Tile_Play_Wall_T2B2.png | x=31, y=4..27의 차콜 막대색을 C 원본 x=30 같은 행 콘크리트로 교체 (24 px); 기타 색 보존 | PASS / PASS | 19 |
| Tile_Play_Wall_T2B2b.png | x=0, y=4..27의 차콜 막대색을 A 원본 x=1 같은 행 콘크리트로 교체 (24 px); 기타 색 보존 | PASS / PASS | 8 |


검사: 13/13 PASS. 변경 위치는 가장자리 1열의 4~27행에 한정. 그 밖의 픽셀은 원본과 RGBA 단위로 동일하다. 위·아래 행 원본 완전 일치. 팔레트는 NEWIMAG/palette_v2.txt 색만 사용, 알파 255, 파일당 24색 이하. 원본 파일 변경 없음.

Preview_6x.png: 위 두 행은 원본/수정 13장(파일명 순), 가운데는 A·B·C·A·C·B 3행 블록 원본(왼쪽)/수정(오른쪽), 아래는 A/B/C 각각 4장 반복 원본(왼쪽)/수정(오른쪽). 전체 6배 최근접 확대. A/B/C 반복 배치의 연속 차콜 이음 줄 제거를 육안 확인했다. 원본의 얼룩과 짧은 균열, 위·아래 연결은 보존했다.
