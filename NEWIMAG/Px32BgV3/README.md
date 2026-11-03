# Re:Trap Px32 Backgrounds v3

2026-11-04. 960×540 화면용 배경 4장. 기존 게임 리소스와 `BgConceptV2` 승인 목업을 참조해 **내장 image_gen으로 각각 새로 생성**했다. 기존 640px 그림의 확대·리샘플은 하지 않았다. Play는 어두운 밤 도시와 폐건물, Verification은 따뜻한 황혼과 온전한 주거·상가 분위기를 유지했다.

## 산출물

- `Backgrounds/Play_Far.png`, `Backgrounds/Verification_Far.png`: 960×540, 전체 불투명, 맨 위 행 단색.
- `Backgrounds/Play_Near.png`, `Backgrounds/Verification_Near.png`: 960×384, 상단 투명 실루엣, 맨 아래 행 전체 불투명.
- `Preview_2x.png`: 3840×3360. 첫 줄은 Play / Verification의 960×540 합성을 각각 최근접 2배로 표시. 둘째·셋째 줄은 각 페이즈 합성을 가로 2회 반복한 2배 화면. 이음 경계는 x=1920. Near 합성 위치는 원본 화면 기준 (0,156), 즉 아래 정렬.
- `Sources/prompts.json`: 4개 전체 생성 프롬프트.
- `Sources/*_raw.png`: 재현용 영구 생성 원본. Far 1672×941, Near 1983×793. 생성기가 반환한 새 그림에서 최근접 표본으로 목표 격자를 복원했다. 최종 배경 자체는 1배 규격이다.
- `Sources/process.ps1`, `Sources/validation.txt`: 재현용 후처리 및 실측 기록.

## 후처리 범위

그림·선·도형은 코드로 만들지 않았다. 코드는 새 생성물의 최근접 격자 복원, `NEWIMAG/palette_v2.txt` 부분집합으로 색 정리, 알파 128 기준 이진화, 고립색 정리, 이음 보정과 검수용 합성·라벨에만 사용했다. 미리보기 2배 확대는 검수 표시용이다.

원경은 건물·하늘의 색 수를 줄여 평면 명암으로 정리했다. 알파는 0/255만 사용한다. 디더나 AA를 추가하지 않았고, 생성물의 작은 양자화 점무늬는 주변 다수색 및 5픽셀 이하 고립색 영역 정리로 줄였다. 이음은 기존 `BgConceptV2/Sources/process_v2.ps1`의 좌우 끝 8열 정렬·블렌드 후 팔레트 재매핑 방식이다. 원래 `Backgrounds/fix_seams.py`처럼 기존 그림의 색을 보정하며 새 구조물을 그리지 않는다.

## 검사

아래 값은 저장한 최종 레이어 기준이다. RGBA 색 수에는 투명색을 포함한다. 끝 열 차이는 모든 행의 RGBA 비교, 최대 차이는 채널별 절댓값(0~255)이다.

| 파일 | 크기 | 불투명 색 | RGBA 색 | 알파 | 끝 열 불일치 행 | 끝 열 최대 차이 | 팔레트 밖 픽셀 | 하단 비불투명 픽셀 |
|---|---|---:|---:|---|---:|---:|---:|---:|
| Play_Far.png | 960×540 | 6 | 6 | 255 | 0 | 0 | 0 | 0 |
| Play_Near.png | 960×384 | 9 | 10 | 0/255 | 0 | 0 | 0 | 0 |
| Verification_Far.png | 960×540 | 5 | 5 | 255 | 0 | 0 | 0 | 0 |
| Verification_Near.png | 960×384 | 8 | 9 | 0/255 | 0 | 0 | 0 | 0 |

Far 두 장의 맨 위 행은 각 1색, Near 두 장의 맨 위 행 불투명 픽셀은 0. 모든 레이어는 투명색까지 포함해 24색 이하다. 끝 열 RGBA는 완전히 일치한다. 합성·반복 미리보기를 육안 확인했다.

## 재현과 범위

저장소 루트에서 `powershell -NoProfile -ExecutionPolicy Bypass -File NEWIMAG/Px32BgV3/Sources/process.ps1` 실행. Python 패키지 설치 없이 Windows System.Drawing을 사용한다.

저장소 쓰기는 `NEWIMAG/Px32BgV3/`에만 수행했다. git은 사용하지 않았으며 실제 Assets 교체나 Unity 임포트는 하지 않았다. 임시 중간 파일은 남기지 않았다. Sources는 재현용 영구 기록이고 image_gen 원본은 허용된 `$CODEX_HOME/generated_images/`에도 유지된다.

끝 열 일치는 자동 검사했지만, Unity에서 움직이는 패럴랙스·카메라 스크롤은 실행 검증하지 않았다. 반복 주기의 동일한 간판·건물은 알아볼 수 있으며, 생성물의 가는 전선과 작은 창문은 일부 불균일한 픽셀 간격을 가진다.
