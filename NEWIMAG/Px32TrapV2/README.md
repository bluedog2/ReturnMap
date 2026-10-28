# Re:Trap 최종 1배 함정 부품

게임용 파일은 `Parts/` 44장 + `Misc/` 5장 = **49장**이다. 모두 원래 픽셀 크기의 RGBA PNG이며 피벗 기준은 캔버스 중앙이다. `Preview_4x.png`, `Parts_4x.png`, `Chain_Tiling_4x.png`는 확인용 최근접 확대 이미지이므로 게임에 넣지 않는다.

## 파일 목록

`<상태>`는 `Normal`, `Dud`, `Critical`, `Beneficial`이다.

| 경로 | 파일 | 크기 |
|---|---|---|
| `Parts/spike/<상태>/` | `base.png`, `spikes.png`, `glow.png`, `platform.png` | 각각 32×32 |
| `Parts/arrow_shooter/<상태>/` | `body.png` | 32×32 |
| 동일 | `arrow.png` | 16×8 |
| 동일 | `muzzle_flash.png` | 16×16 |
| `Parts/drop_hammer/<상태>/` | `head.png`, `mount.png` | 각각 32×32 |
| 동일 | `chain.png` | 8×8 |
| 동일 | `impact.png` | 32×16 |
| `Misc/` | `SlotMarker_Floor.png`, `SlotMarker_Ceiling.png`, `SlotMarker_LeftWall.png`, `SlotMarker_RightWall.png`, `SlotSeal.png` | 각각 32×32 |

## 부품별 출처와 처리

아래의 컨셉 경로는 모두 `../TrapConceptV2/_1x/` 기준이다. 원본 1배 컨셉을 확대·축소하지 않았다. 새 선이나 도형으로 함정 그림을 그리지 않았다. 상태 간 알파 차이는 Normal 실루엣으로 등록하여 위치·실루엣을 통일했다. 빈 곳의 색은 해당 상태 원본에서 가장 가까운 불투명 픽셀을 재사용했다.

| 부품 | 출처 | 이번 생성 시도 | 처리 |
|---|---|---:|---|
| spike/base | 컨셉 `SpikeTrap_<상태>` 컷아웃 | 0 | `(0,16,32,28)` 몸통. 기존 가장자리 픽셀로 투명 여백을 채우고 아래쪽 몸통 행을 반복해 높이 32로 연장. 1024px 불투명 |
| spike/spikes | 컨셉 `SpikeTrap_<상태>` 컷아웃 | 0 | Normal의 `(0,4,32,8)` 가시 실루엣. y=0 배치, 마지막 막대 행을 y=20까지 반복. Critical/Beneficial은 해당 컨셉 색·픽셀을 같은 마스크에 등록. Dud는 Normal의 같은 가시를 녹·회색으로 재매핑 |
| spike/glow | **내장 image_gen**, `Sources/glow_attempt1.png` | 1 | 생성된 다섯 개의 빈 고리형 열기 띠를 32×14로 샘플링하여 y=0 배치. Critical 붉은색, Beneficial 금색. Normal/Dud는 Critical과 동일 |
| spike/platform | 컨셉 `GoldBlock` | 0 | 기존 가장자리 픽셀을 복사해 32×32 전체 불투명. 네 상태 같은 파일 |
| arrow_shooter/body | 컨셉 `ArrowShooter_<상태>` | 0 | Normal 알파로 상태 정렬, 위로 1px 평행이동. 총구 x=31, 불투명 총구 행 15~17, 중심 y=16 |
| arrow_shooter/arrow | 컨셉 `Arrow_<상태>` | 0 | Normal 알파로 상태 정렬. 기존 깃 픽셀을 크림슨/녹/금으로, Normal 촉을 밝은 금속색으로 재매핑 |
| arrow_shooter/muzzle_flash | **내장 image_gen**, `Sources/flash_attempt1.png` | 1 | 생성 섬광을 8×10으로 샘플링해 `(8,3)` 배치. 같은 실루엣을 Dud 연기색, Critical 붉은색, Beneficial 금색으로 재매핑 |
| drop_hammer/head | 컨셉 `DropHammer_<상태>` | 0 | 위 32×28 머리를 컷아웃, Normal 알파로 정렬, y=4에 배치. 아래 불투명 행=31 |
| drop_hammer/mount | 컨셉 `DropHammer_Mount_Normal` | 0 | Scene_3x 천장 브래킷의 원본 부품. y=0 유지, 상태별 색 재매핑 |
| drop_hammer/chain | **내장 image_gen**, `Sources/chain_attempt1.png` | 1 | 생성 고리를 4×8로 샘플링, 8×8 가운데 x=2 배치. 원본 첫 행을 끝 행에 복사하여 RGBA 이음 일치. 상태별 재매핑 |
| drop_hammer/impact | **내장 image_gen**, `Sources/impact_attempt1.png` | 1 | 생성된 먼지·파편을 32×16으로 샘플링. 같은 실루엣의 회색/녹빛/적색/금색 변형. 바닥 행=15 |
| Misc/Floor·Ceiling·RightWall | 컨셉 해당 `SlotMarker` | 0 | 그대로 복사. RightWall은 컨셉 Wall의 오른쪽 화살표 |
| Misc/LeftWall | 컨셉 `SlotMarker_Wall` | 0 | 좌우 반전 |
| Misc/SlotSeal | 컨셉 `SlotSeal` + 확정 Play Floor A | 0 | 확정 바닥 타일 전체에 컨셉의 크림슨 X 픽셀만 컷아웃 합성. 전체 불투명, 주변과 같은 콘크리트 텍스처 |

## 검사

`validation.json`에는 49장 각각의 크기, 불투명 RGB 색 수, 팔레트 밖 색 수, 금지 진영 색 수, 순흑 수, 알파 종류, 최상단/최하단 불투명 행을 기록했다. 허용 팔레트는 `../palette_v2.txt` + `../TrapConceptV2/palette_add_traps.txt`이며 적 진영 초록·보라는 후보에서 제외했다.

- 49장 전부 지정 크기, 16색 이하, 알파 0/255. 팔레트 밖·적 진영색·불투명 순흑 0.
- 11종 부품 모두 네 상태의 알파 실루엣·위치가 완전히 동일.
- 가시 끝 y=0, 막대 마지막 y=20, y=21~31 투명.
- 받침·발판·SlotSeal 전체 불투명. 머리 아래 정렬, 마운트 위 정렬.
- 체인은 폭 4px, x=2~5. 첫 행과 끝 행의 RGBA가 동일하고 연결 열이 존재한다. `Chain_Tiling_4x.png`에 상태별 9회 세로 반복을 첨부했다.
- Preview는 #1A191F 배경에 확정 Play 타일을 합성했다. 가시는 바닥 위로 12px, 섬광 캔버스 중심은 본체 중심에서 오른쪽 14px. 체인은 8px×3=24px이며 머리의 위쪽 투명 4행을 겹쳐 연결 고리 사이 공백을 제거했다. 바닥 충격은 떨어지는 머리와 별도로 표시했다. 글로우는 가시 뒤에서 불투명도 60%로 합성했다.

## 한계 및 해석

- 승인 컨셉 네 상태는 원래 실루엣이 서로 다르다. 요청의 **동일 실루엣·동일 위치**를 맞추기 위해 Normal 마스크로 정렬했으므로 상태별 원본 외곽의 일부는 잘리거나 기존 픽셀로 보충되었다. 내부 색·문양은 가능한 한 해당 컨셉을 유지했다.
- Dud 컨셉은 접힌 상판이므로 같은 높이의 가시 원본이 없다. Normal 가시를 녹색이 아닌 **녹슨 갈색·회색**으로 바꿨다. 같은 알파 실루엣을 유지한 결과, 별도의 평평하고 무딘 외곽 형태는 구현하지 않았다.
- 동일 실루엣 제약 때문에 Dud 연기는 섬광과 같은 외곽을 공유하고, Dud 먼지와 Critical 파편의 실제 범위도 같다. 약함/강함은 명도와 색으로 표현했다. 별도 연기 형태·작은 먼지 범위·큰 Critical 범위까지 충족했다고 주장하지 않는다.
- 생성기는 요청한 k배 픽셀 격자를 완전히 지키지 않았다. 생성 원본의 불투명 경계 내부를 목표 크기로 중심 샘플링하고 알파를 128 기준으로 이진화했다. 1px 차콜 외곽선이 모든 파편에서 연속된다는 보장은 없다. 원본은 증빙용이며 게임용이 아니다.
- 글로우는 새 효과이므로 승인 컨셉의 기존 픽셀과 동일한 원본은 없다. 생성된 다섯 열기 띠를 사용했으며 최종 미리보기로 배치를 확인했다.
- Unity 임포트, 중앙 피벗 메타데이터, 게임 코드, 충돌체, 실제 애니메이션은 변경·실행하지 않았다. 이 폴더 밖은 읽기만 했고 git은 사용하지 않았다.

## 재현·보존 파일

저장소 루트에서 `./NEWIMAG/Px32TrapV2/build.ps1`, 이어서 `./NEWIMAG/Px32TrapV2/validate.ps1`을 실행한다. System.Drawing만 사용한다. 빌드 컴파일 임시 폴더 `.tmp`는 finally에서 정리한다.

`Sources/`의 4개 원본 PNG와 `prompts.json`은 출처 증빙을 위한 영구 산출물이다. 임시 파일이 아니다. 내장 image_gen을 사용했고 CLI/API 대체 생성은 사용하지 않았다. 원본 image_gen 자동 저장본은 허용된 `$CODEX_HOME/generated_images/`에 남아 있다.
