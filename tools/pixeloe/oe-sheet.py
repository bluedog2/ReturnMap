# ═══════════════════════════════════════════════════════════════
#  oe-sheet.py — GPT 원본 시트 → PixelOE 픽셀화 축소 (asf-post.ps1 -Downscale pixeloe 의 ②a 단계)
# ═══════════════════════════════════════════════════════════════
# ASF box 축소가 정한 배율(scale)과 같은 크기가 되도록 원본을 (scale × pixel_size) 배로 LANCZOS 리사이즈한 뒤
# PixelOE 로 1/pixel_size 픽셀화한다. 결과 시트는 원본과 같은 칸 구성 → ASF scale_frames 를 배율 1·nearest 로 정렬만 돌린다.
# PixelOE 는 RGB 만 다룬다 → 크로마키(초록 등) 배경 원본 전용. 투명 배경 원본은 asf-post 가 미리 막는다.
# 사용: <venv python> -I oe-sheet.py --raw <원본> --out <png> --scale <ASF 배율> [--pixel 4] [--thickness 1] [--mode contrast]
# 출력 마지막 줄: OE_SHEET=<경로> <가로>x<세로>
import argparse
from PIL import Image
import pixeloe.torch.pixelize as P
from pixeloe.torch.utils import to_tensor, to_numpy

ap = argparse.ArgumentParser()
ap.add_argument('--raw', required=True)
ap.add_argument('--out', required=True)
ap.add_argument('--scale', required=True, type=float, help='ASF box 축소 배율(scale_frames 결과의 scale)')
ap.add_argument('--pixel', type=int, default=4, help='PixelOE pixel_size: 리사이즈된 원본 몇 px 를 1px 로')
ap.add_argument('--thickness', type=int, default=1, help='외곽선 두께(2026-11-07 비교: 1 이 2 보다 덜 거침)')
ap.add_argument('--mode', default='contrast')
a = ap.parse_args()

im = Image.open(a.raw).convert('RGB')
p = a.pixel
k = a.scale * p                                   # 최종 배율 = k / p = ASF 배율
W, H = round(im.width * k / p) * p, round(im.height * k / p) * p
im = im.resize((W, H), Image.LANCZOS)
t = to_tensor(im)
if t.ndim == 3:
    t = t.unsqueeze(0)                            # Slang 백엔드는 4차원(배치) 입력만 받는다
out = to_numpy(P.pixelize(t, pixel_size=p, thickness=a.thickness, mode=a.mode, no_post_upscale=True))
if isinstance(out, list):
    out = out[0]
if out.ndim == 4:
    out = out[0]
img = Image.fromarray(out)
img.save(a.out)
print(f'OE_SHEET={a.out} {img.width}x{img.height} pixel={p} thickness={a.thickness} mode={a.mode} k={k:.4f}')
