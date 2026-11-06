# tools/pixeloe — PixelOE (GPT 그림 → 픽셀 격자 축소, 비교 시험 단계)

**게임 이미지는 화풍 통일을 위해 전부 GPT 가 그린다.** PixelOE(Apache-2.0, https://github.com/KohakuBlueleaf/PixelOE)는
GPT 가 그린 고해상도 그림을 외곽선을 지키며 픽셀 격자로 줄이는 **후처리 부품**으로만 쓴다.
자리는 ASF 후처리(`tools/sprite/asf-post.ps1`)의 "축소" 단계 — **선택 옵션 `-Downscale pixeloe`**(기본은 box). 기본값은 새 시트로 비교한 뒤 정한다(`tools/sprite/README.md`).

```
pixeloe.lock.json    버전 고정: pixeloe 1.0.0 · torch 2.14.1+cpu · torchvision 0.29.2+cpu · kornia · slangpy · venv 위치
pixeloe-setup.ps1    ..\pixeloe-venv(py 3.12) 생성 + 고정 버전 설치 + 불러오기 확인. -Check 는 확인만(exit 0/1)
oe-sheet.py          asf-post 가 venv python 으로 부름: 원본 → (ASF 배율 × pixel) LANCZOS 리사이즈 → PixelOE 1/pixel 픽셀화
```

## 설치

```powershell
powershell -ExecutionPolicy Bypass -File tools\pixeloe\pixeloe-setup.ps1          # 처음 1회·버전 올린 뒤
powershell -ExecutionPolicy Bypass -File tools\pixeloe\pixeloe-setup.ps1 -Check   # PYTHON= · TORCH=… CUDA=False · VERSION=
```

- **CPU 전용**(2026-11-06 결정). 스프라이트는 작아서 셀 하나 0.2~0.5초. GPU 로 바꾸려면 lock 의 torch·torchvision·torchIndex 를 CUDA(cu128 이상, RTX 5070 Ti) 빌드로 바꾸고 setup 재실행.
- ASF 가 쓰는 시스템 Python(`py -3.12`)과 섞지 않으려고 venv 를 따로 둔다. 시스템 `python` 명령은 Windows 스토어 가짜 실행 파일이라 쓰지 않는다.

## 쓸 때 알아둘 것 (torch API, 2026-11-06 확인)

- CLI(`pixeloe.pixelize`)는 곧 없어질 legacy API → `pixeloe.torch.pixelize.pixelize` 를 쓴다.
- 입력은 **4차원**(배치) 텐서: `to_tensor(img).unsqueeze(0)`. 3차원이면 Slang 백엔드가 실패한다.
- `to_numpy(out)` 은 **리스트**를 돌려준다 → `[0]`.
- 백엔드 `auto` = Slang CPU(첫 호출 때 Visual Studio 로 커널을 컴파일 — `C4819`·`D3D12`·`sm_6_0` 경고는 무시해도 됨). `backend="torch"` 와 결과 차이는 픽셀 0.6%·값 최대 9.
- RGB 만 다룬다 — 크로마키 배경 원본만 받고, 투명 배경(`-Key none`)은 asf-post 가 FAIL로 막는다.
- `pixel_size` = 원본 몇 px 를 1px 로 줄일지. 출력 크기 = 원본 ÷ pixel_size(`no_post_upscale=True`).

## 금지

- PixelOE 로 사진·남의 그림을 변환해 게임에 넣지 않는다(GPT 그림 후처리 전용).
- `Assets/` 에 Python 을 넣지 않는다. 크레딧은 `THIRD_PARTY_NOTICES.md`.
- 버전 올리기: lock 변경 → setup 재실행 → 같은 GPT 원본으로 결과 비교 → NOTICES 확인.
