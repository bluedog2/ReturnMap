from pathlib import Path
root=Path('NEWIMAG/Px32HeroV4')
p=root/'process.py';s=p.read_text(encoding='utf-8');s=s[:s.index("if __name__=='__main__':")]+"if __name__=='__main__':\n from revision1 import main\n main()\n";p.write_text(s,encoding='utf-8')
p=root/'revision1.py';s=p.read_text(encoding='utf-8-sig');start=s.index("if __name__=='__main__':");end=s.index('\ndef main():',start);s=s[:start]+s[end:];p.write_text(s,encoding='utf-8')
p=root/'README.md';old=(root/'Sources/Revision1_before/README.md').read_text(encoding='utf-8')
section='''

## 재요청 1

현재 선택은 **Run 3차 유지 / Hit 4차로 교체**다. 위 본문은 최초 작업 당시 기록이며, 이 절과 현재 validation.json이 최신 결과다. 내장 image_gen으로 Run·Hit 몸을 각각 1회 추가 생성했다. 모든 산출물은 이 폴더 안에 저장했고 Assets/git은 접근하지 않았다. JumpStart·Dash의 원본·시트·GIF는 SHA-256으로 불변을 확인했고 두 시트의 validation/attempts/layout 항목도 유지했다.

|시트|프레임 키 px|Idle 머리 bbox Δ폭/Δ높이 px|재요청 1 통과|
|---|---|---|---|
|Run|101,97,102,99,100,96,101,97|전부 0/0|미달 — 기존3차 유지|
|Hit|101,94,100|전부 0/0|통과|

Run 추가 후보(4차)의 키는98,100,98,99,98,100,94,100px로 범위6px, 인접 최대6px다. 7프레임에서 오히려 몸이 낮아지고, 4→5 몸 방향·팔 전환도 남아 개선안으로 채택하지 않았다. 전체 평행이동은 bbox 키 자체를 바꾸지 못하고, 머리 높이만 강제로 맞추면 일부 발이 바닥 아래로 내려가거나 허용된 공중 높이를 넘는다. 잘림·변형으로 수치를 맞추지 않았다. 기존 Run PNG/GIF는 바이트 그대로 보존했다. 현재 Run은 머리 높이 범위6px, 루프 포함 인접 최대5px로 요청한3px/2px 및 두 걸음 곡선에 미달한다.

Hit는4차에서 생성한 머리 없는 몸을 사용했다. 처음 배경처럼 보인 후광은 낮은 알파라 기존128 임계 정리로 제거됐다. 생성 블록 간격6px를 고정 복원하고 승인 팔레트에 정리한 뒤, 세 프레임 모두 Idle 전체 머리2180픽셀을 그대로 합성했다. 머리 크기69×57px, 저장 PNG에서2180개 픽셀 전부 일치. 움찔/젖힘/복귀 머리 이동은 Idle 대비(0,0),(-4,+7),(0,+1)px이다. 움찔/복귀 키는 Idle101px 대비0/-1px이며, 젖힘94px는 자세 예외다. 목·어깨 접합과 왼쪽·아래 젖힘을 눈으로 확인했다. 눈 패치는 사용하지 않았다. 움찔은 몸 자세로 표현한다. 몸·상자 선의 생성 특유 차이는 남으며 게임 안에서의 최종 동작 검수는 하지 않았다.

Hit 수치/시각 검사 통과와 Run 미달을 구분해 기록했다. 전체 overall_pass는 false다. JumpStart/Dash는 이번 검수 범위 밖이므로 최초 판정도 수정하지 않았다.

- `_1x/Hero_Hit.png`, `Animations/Hero_Hit_3x.gif` 교체. Hit GIF12fps(8/9/8cs), 마지막 프레임 유지.
- `Compare_3x.png`의 Hit 행만 갱신. 나머지 세 행 픽셀은 그대로다.
- `Sources/Run_attempt4.png`, `Hit_attempt4.png`: 내장 image_gen 원본. 전체 최종 프롬프트와 원본 경로는 `Sources/revision1_generation.json` 및 `attempts.json`의4차 항목에 있다.
- `Sources/*attempt4_composite_3x.png`: 새 후보 비교. `Hit_attempt5_composite_3x.png`는 기존1차 몸 재합성 탈락 실험이며, 생성5회차라는 의미가 아니다.
- `Sources/Revision1_before/`: 이전 Run/Hit 산출물 및 보고서 보존. `Sources/revision1_protected.json`: 변경 금지 파일 해시.
- 코드 작업은 복원·알파/팔레트 정리·평행이동·승인 머리 합성·조립·검증만 사용했다. 실루엣 리샘플·회전·팔다리 변형·새 캐릭터 픽셀 그리기 없음.

최신 결과 재현은 아래 명령만 사용한다. 최초 보고서 작성용 report.py는 과거 기록이므로 재실행하지 않는다.

```powershell
python NEWIMAG/Px32HeroV4/process.py
python NEWIMAG/Px32HeroV4/verify.py
```

독립 GIF 디코더로 프레임 수·지연·루프·마지막 프레임 유지와 PNG의 정확한 최근접3배 픽셀 일치를 검증했다. 파일 규격 검사와 보호 파일 해시 검사는 통과했다.
'''
p.write_text(old+section,encoding='utf-8')
