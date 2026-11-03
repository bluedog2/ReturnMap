# Fx_GoalMarker — 3칸 기준

- 결과: `Effects/Fx_GoalMarker.png`, RGBA PNG, **256×64**, 64×64 셀 4개 가로 배치, 1배.
- 재생: 왼쪽부터 0 → 1 → 2 → 3 → 0, **6fps**, 프레임당 1/6초, 루프 2/3초. PNG 자체에는 재생 속도 메타데이터가 없으므로 엔진에서 설정.
- 피벗: 각 셀 하단 중앙, Unity 정규화 `(0.5, 0)`, PPU 32 권장. 이미지 좌표는 좌상단 원점.
- 네 프레임 모두 불투명 세로 범위 `y=4..63` = **60px**. 밑동 마지막 행은 `x=23..41`, 중심 `x=32`, 중앙 바닥 픽셀 `(32,63)` 불투명. 깃대 배치와 밑동 좌표 고정. 생성된 금속 음영·윤곽에는 프레임별 소수 픽셀 차이가 남아 있으며 깃대 RGBA 전체가 완전히 동일한 시트는 아님.
- 깃발은 크림슨 천, 흰 별/X 낙서, 접힌 그림자, 차콜 금속 깃대, 빨간 띠, 볼트, 반짝임으로 구성. 차콜 외곽선과 계단형 픽셀 경계, 디더 없음.
- 불투명 RGB **12색**, 모두 `NEWIMAG/palette_v2.txt`에 포함. 순흑 없음. 알파는 **0/255**만 사용. 팔레트: `#19181F #2A282D #3D3940 #555058 #8D7B80 #B3A1A0 #641F36 #92223F #B83450 #DC526B #E7D8CE #F7EEE4`.

## 비교 프리뷰

`Preview_4x.png`: 1536×512, 투명 배경. 왼쪽은 새 시트 전체를 정수 4배, 오른쪽은 `Assets/_Project/ResourcceEX/Sprites/Px32/Player/Hero_Idle.png`의 첫 128×128 프레임을 정수 4배로 표시. 모두 바닥 마지막 픽셀 행 511에 정렬. 주인공은 원본 그대로이며 비율 보정하지 않음. 문서 기준 키는 96px지만 제공된 첫 프레임의 알파 경계는 y=27..127, 실제 불투명 높이 101px. 깃대 60px는 문서 기준 96px의 62.5%.

## 제작 및 검수

내장 **image_gen** 사용. 기존 32px 시트는 디자인 참고만 하고 새로 생성. 생성본의 확대 표시 격자를 목표 셀에 복원하고 최근접 팔레트 매핑, 알파 임계값 128 이진화, 전체 프레임 평행이동만 적용. 코드로 선·도형을 그리지 않음. 기존 시트 확대 리샘플 없음. 프리뷰에만 정수 픽셀 복제 사용.

크기·바닥 좌표·RGB/알파·팔레트 포함 여부를 검사했고 4배 프리뷰를 육안 확인. 엔진 파일과 원본은 수정하지 않았으며 git은 사용하지 않음. 작업용 스크립트는 정리함.

## image_gen 프롬프트

```text
Create a production pixel-art sprite sheet newly drawn at a logical resolution of 256x64 pixels (four equal 64x64 cells in ONE horizontal row). Reference image is design reference ONLY, do not enlarge or resample it. Deliver the logical pixel grid at 4x nearest-neighbor display size 1024x256, each logical pixel a crisp 4x4 square. Transparent background. In EACH cell the same vertical narrow charcoal metal pole is centered exactly at logical x=32; pole top y=4, base y=63 touching bottom row, 60 pixels tall. Pole must have identical silhouette, position and metal highlight/bolts/red bands in all four frames. Crimson triangular pennant flies to the RIGHT of pole in upper half, fits within x=35..60 and y=6..30. Ragged white star/X brush graffiti, narrow crimson bands on pole, tiny sparkle beside flag. Same design as reference but NEW richer hand placed pixel details: cloth creases, folded dark crimson shadow, 3-4 tone shading, metal highlight and bolt, broken brush-stroke star. Four smoothly looping flutter phases for 6fps: neutral, rising fold, neutral opposite fold, falling fold. Small localized cloth movement only; flag triangle never changes direction. Uniform 1 logical pixel charcoal outline #19181F, no black, no antialiasing, no dithering, no gradients. Use ONLY these 12 colors plus transparent: #19181F #2A282D #3D3940 #555058 #8D7B80 #B3A1A0 #641F36 #92223F #B83450 #DC526B #E7D8CE #F7EEE4. No ground, no cast shadow, no frame borders, no labels, no extra objects. Four cells contiguous with no gutters. IMPORTANT pole at center of every cell even though flag flies to right, base at very bottom. Artwork drawn for 64px cells, not old 32px pixel art blown up.
```

