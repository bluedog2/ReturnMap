# Re:Trap 64px 함정 파츠 V3

게임용 PNG **49장**: `Parts/` 44장 + `Misc/` 5장. 게임용 파일은 전부 1배이며 피벗은 각 캔버스 중앙을 기준으로 배치했다. `Preview_3x.png`만 확인용 최근접 3배다. `Parts_1x.png`는 부품 확인용 원배율 목록이다.

## 파일 목록

상태 폴더는 `Normal`, `Dud`, `Critical`, `Beneficial` 순서다.

| 폴더 | 파일 | 캔버스 |
|---|---|---|
| `Parts/spike/<상태>/` | base / spikes / glow / platform.png | 각각 64×64 |
| `Parts/arrow_shooter/<상태>/` | body.png | 64×64 |
| 동일 | arrow.png / muzzle_flash.png | 32×16 / 32×32 |
| `Parts/drop_hammer/<상태>/` | head.png / mount.png | 64×64 |
| 동일 | chain.png / impact.png | 16×16 / 64×32 |
| `Misc/` | SlotMarker_Floor / Ceiling / LeftWall / RightWall.png | 각각 64×64 |
| `Misc/` | SlotSeal.png | 32×32, V2와 바이트 동일 |

## 출처·생성 횟수·추가 디테일

그림 생성은 **내장 image_gen**만 사용했다. 각 함정의 네 상태와 모든 부품은 한 장의 시트로 함께 생성했다. 기존 V2 32px 파츠를 확대해서 새 그림으로 사용하지 않았다. 컨셉·V2 미리보기는 디자인 참조로 확인했고, 발사기·해머 생성에는 승인된 `TrapConceptV2/States_4x.png`도 참조 이미지로 전달했다.

| 부품 | 새 원본 / 시도 횟수 | 추가 디테일 |
|---|---|---|
| spike/base | Sources/spike_attempt1.png 1행 / 1회 | 4개 모서리 볼트 홈, 상하 고정판, 판금 이음, RE·X 표식, 사선 경고 도장 |
| spike/spikes | 같은 시트 2행 / 1회 | 날의 평면 하이라이트, 각 축의 볼트 칼라, 세로 금속 음영, 아래쪽 축 분절 |
| spike/glow | 같은 시트 3행 / 1회 | 가시 주변의 붉은색·금색 빈 열기 띠, 위 28행 안 배치 |
| spike/platform | 같은 시트 4행 / 1회 | 금빛 상단 턱, 모서리 리벳, 왕관 문양, 중앙 보석 모티프. Beneficial 원본을 네 상태에 공통 사용 |
| arrow_shooter/body | Sources/arrow_attempt1.png 1행 / 1회 | 벽 고정 브래킷, 피스톤 리브, 환기 홈, 볼트, 총구 칼라, RE·별 낙서 |
| arrow_shooter/arrow | 같은 시트 2행 / 1회 | 밝은 금속 촉의 면 분할, 연결 칼라, 크림슨 깃의 사선 결 |
| arrow_shooter/muzzle_flash | 같은 시트 3행 / 1회 | 밝은 중심과 크림슨/금색 바깥 불꽃, Dud의 회색 연기 |
| drop_hammer/head | Sources/hammer_attempt1.png 1행 / 1회 | 측면 볼트 레일, 바닥 타격 돌기, 두 줄 경고 도장, 위 체인 고리, RE·별 표식 |
| drop_hammer/mount | 같은 시트 2행 / 1회 | 상단 체결판, 볼트, 아래 삼각 지지대와 중앙 고리 |
| drop_hammer/chain | 같은 시트 3행 / 1회 | 앞 고리와 측면 연결쇠, 금속 하이라이트, 녹/열/금 변형 |
| drop_hammer/impact | 같은 시트 4행 / 1회 | 면이 나뉜 파편과 먼지 덩어리, 상태별 색·내부 명암 |
| 네 방향 SlotMarker | Sources/marker_attempt1.png / 1회 | 기존 크림슨 테두리·크림색 방향 화살표, 새 모서리 체결점과 프레임 이음 |
| SlotSeal | ../Px32TrapV2/Misc/SlotSeal.png / 0회 | 원본 그대로 복사 |

Normal은 아이보리·크림슨, Dud는 갈색 녹 번짐·꺼진 부품, Critical은 붉은 균열과 달아오른 금속, Beneficial은 금 장식과 왕관을 사용한다. 실제 브랜드는 사용하지 않았다.

## 후처리와 배치

- `build.py`: 생성된 각 부품 영역에서 셀 중심을 샘플링해 목표 논리 격자로 복원, 허용 팔레트 최근접 정리, 파일당 최대 24색, 알파 192 기준 이진화, 고립점 제거, 배치 이동 및 네 상태 알파 교집합 컷아웃만 수행한다. 새 선·도형으로 부품을 그리지 않는다.
- 받침과 황금 발판은 둥근 모서리 바깥을 잘라낸 완전 불투명 내부 영역을 사용한다. 빈 픽셀을 채우거나 행을 늘려 붙이지 않았다.
- 체인은 폭 10px, 중심 x=7.5. 이미 불투명한 양 끝 행의 공통 연결 열만 남기고 해당 끝 픽셀의 색을 차콜로 정리해 상하 RGBA를 같게 했다. 연결 열은 x=5~10이다.
- Normal·Dud glow는 Critical과 같은 파일 내용이다. platform도 네 상태가 동일하다.
- `Sources/extraction.json`에 실제 원본 컷아웃 좌표와 X/Y 샘플 간격을 기록했다. `Sources/prompts.json`에는 프롬프트·호출 방식·자동 저장 출처를 기록했다.

## 검사와 미리보기

`validation.json`은 저장된 49개 PNG를 다시 열어 크기, 색 수, 팔레트 밖 픽셀, 금지 진영색, 순흑, 알파 값, 불투명 경계, 가시 끝 행, 체인 상하 RGBA 이음, 상태별 실루엣을 검사한다. 스타일의 엄격한 충족 여부와 수치 검사는 구분한다.

`Preview_3x.png`는 32px 눈금 위에 네 상태를 나란히 조립했다. 가시는 받침 위로 24px, 발사기는 벽·본체·섬광·화살, 해머는 천장·마운트·체인 3개·머리·바닥 충격으로 구성한다. 바닥은 지정된 Play Floor A를 확대하지 않고 32px 타일 그대로 2행 반복한다. 각 64px 영역은 2×2타일이다. 비교용 주인공은 지정된 Hero_Idle의 첫 128×128 프레임을 원배율로 사용하며 불투명 발끝을 바닥에 맞췄다.

재현 순서(저장소 루트):

```powershell
python -B NEWIMAG/Px32TrapV3/build.py
python -B NEWIMAG/Px32TrapV3/validate.py
powershell -NoProfile -ExecutionPolicy Bypass -File NEWIMAG/Px32TrapV3/preview.ps1
```

## 한계

- 생성기는 요청한 정수 배수 격자·셀 크기를 정확히 지키지 않았다. 목표 PNG는 새 생성 원본의 부품별 컷아웃을 **비정수 간격으로 중심 샘플링**한 결과다. 따라서 엄밀한 의미의 원생 64px 격자 복원이라고 보증할 수 없다. 기존 32px 파츠 리샘플링은 하지 않았다.
- 1px 차콜 외곽선이 모든 둘레에서 연속하는 것과 재질별 정확히 3~4단 음영은 보증하지 않는다. 일부 가장자리에 밝은 금속색이 노출되며, 완전 불투명 상자 컷아웃에서는 원본의 바깥 모서리 일부가 빠졌다. 이 때문에 `production_ready`는 false로 남긴다. 수치 검사 통과를 전체 스타일 요구 충족으로 오인하지 않도록 했다.
- 알파 교집합으로 상태 실루엣을 통일하면서 일부 가장자리 파편·열기·연기를 제거했다. Dud 연기는 다른 상태의 섬광과 같은 외곽 범위로 제한된다. 작은 RE 글자는 부품에 따라 축약돼 보일 수 있다.
- 주인공은 기존 첫 프레임을 그대로 사용했다. 실제 불투명 범위는 x=30~98, y=27~127로 높이 **101px**다. 요청한 원본 사용을 우선했으며, 그 파일 자체를 96px로 수정하거나 리샘플링하지 않았다.
- Unity 임포트 설정·중앙 피벗 메타데이터·게임 애니메이션·충돌체는 변경하거나 실행하지 않았다.

저장소 쓰기는 `NEWIMAG/Px32TrapV3/` 안으로 제한했고 git을 사용하지 않았다. 생성 도구 자동 원본은 허용된 `$CODEX_HOME/generated_images/`에 있다. `Sources/`는 재현과 출처 확인을 위한 영구 산출물이며 임시 파일이 아니다. 컴파일 임시 폴더는 실행 종료 시 정리한다.
