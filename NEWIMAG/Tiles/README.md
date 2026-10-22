# Re:Trap 타일 톤 맞춤

프로젝트의 기존 환경 타일을 찾아 NEWIMAG 캐릭터와 함정의 도트 스타일에 맞춰 내장 image_gen으로 다시 그렸습니다. 원본 게임 파일과 팔레트는 변경하지 않았습니다.

## 파일 구성

| 위치 | 내용 |
|---|---|
| Replacement/Environment/BuildPhase/Chapter1 | 따뜻한 성벽·석재·덩굴·하늘 132개 |
| Replacement/Environment/BuildPhase/Chapter2 | 따뜻한 석재·나무·식물·버섯 배경 132개 |
| Replacement/Environment/PlayPhase/Chapter1 | 청회색 성벽·균열·밤하늘 132개 |
| Replacement/Environment/PlayPhase/Chapter2 | 침식된 나무·보라색 이끼·발광 버섯·룬 132개 |
| Replacement/Environment/CorruptedCastleTile.png | 기존 1024×1024 공용 침식 석재 텍스처의 도트 버전 |
| Atlases | 네 세트를 각각 12열×11행으로 모은 504×462 시트, 3배 확대 미리보기 |
| Sources | 생성 도구가 반환한 원본 크기 이미지 |
| References | 프로젝트의 기존 타일을 원래 행/열 순서로 재구성한 비교용 이미지 |
| manifest.json | 원본→교체본의 대응 경로, 크기, 피벗, PPU |
| generation.json | 사용한 이미지 생성 프롬프트와 선택한 결과 경로 |
| preview.html | 세트별 원본/새 이미지 비교, 개별 PNG 열기 |

일반 타일은 총528개, 공용 석재1개를 포함하면 교체용 PNG는529개입니다. 각 타일은 기존과 같은42×42 크기와 파일명, 행/열 번호입니다. `.meta`는 기존 임포터 설정과 피벗/PPU를 바탕으로 새 에셋용 GUID를 사용했습니다. 모음 시트는42×42 Multiple Sprite로 잘라 쓸 수 있도록 메타데이터를 포함합니다.

## 교체할 때

원본 폴더는 `Assets/_Project/ResourcceEX/Sprites/Environment`입니다. `Replacement/Environment` 안의 PNG를 같은 하위 경로에 대응시켜 사용합니다. 기존 참조를 보존하며 덮어쓸 경우 **PNG만 복사하고 기존 `.meta`는 유지하세요.** NEWIMAG 쪽 `.meta`까지 덮어쓰면 GUID가 달라집니다. 현재 NEWIMAG는 프로젝트 루트에 있으므로, 새 에셋으로 쓸 때는 원하는 PNG와 `.meta`를 함께 Assets 하위 폴더로 복사해 Unity에서 선택하세요.

현재 TilePaletteConfig의 기본 Floor/Wall/Ceiling은 각각 PlayPhase/Chapter1의 `Play_C1_r01_c03`, `Play_C1_r01_c04`, `Play_C1_r01_c05`를 가리킵니다. 이 셋도 대응 이름으로 준비되어 있습니다. 배경 타일은 팔레트 인덱스에 연결되어 있으므로 원본을 교체하는 경우 기존 파일명을 유지하세요.

타일의 슬롯 순서와 큰 소재 배치를 유지하도록 다시 그렸습니다. 생성형 재작화이므로 균열·덩굴·하늘의 세부 경계는 원본과 조금 다를 수 있습니다. 12×11 모음과 분리 PNG가 정확히 재조립되는지 검증했으며, 실제 Unity 맵에서의 타일 간 시각적 연결은 적용 후 확인해야 합니다. 콜라이더, 맵 데이터, Addressables와 팔레트 순서는 바꾸지 않았습니다.

## 색과 출력

빌드 페이즈는 황토/아이보리 석재, 어두운 줄눈, 황동빛 하이라이트와 차분한 녹색 식생입니다. 플레이 페이즈는 갑옷과 어울리는 청회색 석재, 숯빛 음영, 보라색 침식과 제한적인 청록색 발광입니다. 캐릭터를 가리지 않도록 기본 벽에는 넓은 저채도 면을 남겼습니다.

원래 환경 타일은 불투명 이미지여서 교체 타일도 불투명하게 내보냈습니다. 생성 후 크기를 맞추고 분할할 때는 Nearest 샘플링을 사용했습니다. 실제 게임 적용이나 Unity 플레이 검증은 수행하지 않았습니다.
