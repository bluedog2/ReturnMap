# Re:Trap 캐릭터 스프라이트

타일은 `Tiles` 폴더에 있습니다. 기존 환경 타일 4세트와 공용 석재를 다시 그린 교체본이며, `Tiles/preview.html`에서 비교할 수 있습니다.

**최신 추천 버전은 AnimationV2입니다.** 자연스러운 동작을 우선해 새로 생성한 동작당 8프레임 버전입니다. `preview-v2.html`에서 캐릭터·함정·이펙트를 함께 확인하세요. 캐릭터 무기는 몸체 프레임에 포함되어 있습니다.

`Replacement`는 기존 파일의 시트 구조와 클립 타이밍을 유지하려고 만든 4프레임 기반 호환본입니다. 기존 슬롯에 반복·유지 프레임을 넣은 버전이므로, 새 애니메이션을 구성할 때는 AnimationV2를 사용하세요.

`Traps`에는 가시·화살 발사대·낙하 해머의 정상/고장/악화/축복 부품이, `Effects`에는 대시/피격/사망/부활 효과가 분리되어 있습니다. 자세한 구성은 `새버전_사용법.md`를 참고하세요.

리트랩.docx의 함락된 성을 탈환하는 영주, 마왕군, 빛과 어둠의 대비를 바탕으로 내장 image_gen으로 제작했습니다. 생성 프롬프트는 generation.json에 보관합니다.

## 구성

- hero: 왕관, 청회색 갑옷, 붉은 망토의 영주. Idle, Run, Jump_Start, Jump_Loop, Land, Dash, Lord_Hit, Death.
- goblin / orc / skeleton / imp / knight: 고블린 약탈자, 오크 투사, 해골 궁수, 비행 임프, 타락 기사. Idle, Run, Hit, Death.
- 캐릭터별 source.png: 생성 원본.
- spritesheet.png: 4열의 정규 프레임 시트. 주인공 8행, 몬스터 4행.
- 상태별 *_strip.png: 4프레임 가로 시트.
- 상태별 *_00.png ~ *_03.png: 투명 배경 384×384 개별 프레임.
- manifest.json: 파일 경로, 순서, 권장 FPS, 루프 여부.
- preview.html: 브라우저에서 열어 동작, 속도, 좌우 반전을 확인.

## Unity 사용

개별 프레임에는 Sprite (Single), Point 필터, 압축 없음, 256 Pixels Per Unit, 피벗 (0.5, 0.125)의 .meta가 포함되어 있습니다. 원하는 동작의 4개 프레임을 순서대로 선택해 Animation 창에 넣고 manifest.json의 FPS로 설정하세요. 전체 시트를 사용할 경우 Sprite Mode를 Multiple로 바꾸고 384×384 Grid By Cell Size로 자릅니다. 왼쪽 방향은 SpriteRenderer.flipX로 반전합니다.

프레임은 생성 원본을 잘라 투명 여백을 추가한 것으로, 그림 자체를 다시 그리거나 축소하지 않았습니다. 생성형 도트 스타일 에셋이며 프레임 사이에 외형·위치 차이가 있을 수 있습니다. 기존 씬이나 컨트롤러에는 연결하지 않았으므로 게임 화면에서 크기와 재생 타이밍을 맞추세요. 미리보기는 모든 동작을 반복 재생하지만 Death, Hit, Land 등은 게임에서 1회 재생하도록 설정합니다.
