using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  SpriteUtil — 공용 1×1 흰 폴백 스프라이트 · 링 게이지 헬퍼
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// "스프라이트가 없을 때 색상 블록으로라도 보여주기" 위한 1×1 흰 <see cref="Sprite"/> 를
    /// 정적 캐시로 제공한다. 동일한 Texture2D+SetPixel(White)+Sprite.Create 코드가
    /// TrapBase(황금 블록·철거 모드 표시)·MapLoader(타일/슬롯/골 폴백) 등 여러 곳에
    /// 중복돼 있던 것을 여기 하나로 모은다.
    /// <para>파라미터는 기존 구현들과 동일하게 유지: PPU=1, pivot=(0.5,0.5).</para>
    /// </summary>
    public static class SpriteUtil
    {
        private static Sprite _unitWhite;

        /// <summary>1×1 흰색 스프라이트 (PPU=1, pivot=(0.5,0.5)). 최초 호출 시 1회 생성 후 캐시.</summary>
        public static Sprite UnitWhite()
        {
            if (_unitWhite != null) return _unitWhite;

            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            _unitWhite = Sprite.Create(
                tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            return _unitWhite;
        }

        private static Sprite _ring;

        /// <summary>
        /// 코드 생성 흰색 링 스프라이트 (128×128, 바깥 반지름 60 / 안쪽 44, PPU=100, pivot=(0.5,0.5)).
        /// 대시 게이지·함정 쿨다운 게이지 등 원형 게이지가 공유한다. 최초 호출 시 1회 생성 후 캐시
        /// (도메인 리로드 off 시 파괴된 에셋 대응을 위해 null 체크).
        /// </summary>
        public static Sprite Ring()
        {
            if (_ring != null) return _ring;

            const int   texSize = 128;
            const float outerR  = 60f;
            const float innerR  = 44f;
            const float center  = texSize * 0.5f;

            var tex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
            var pixels = new Color32[texSize * texSize];

            for (int y = 0; y < texSize; y++)
            {
                for (int x = 0; x < texSize; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f),
                                                  new Vector2(center, center));

                    // 링 경계 1px 부드럽게 (안티앨리어싱)
                    float alpha = Mathf.Clamp01(outerR - dist) * Mathf.Clamp01(dist - innerR);
                    pixels[y * texSize + x] = new Color32(255, 255, 255,
                        (byte)(Mathf.Clamp01(alpha) * 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();

            _ring = Sprite.Create(tex,
                new Rect(0, 0, texSize, texSize),
                new Vector2(0.5f, 0.5f), 100f);
            return _ring;
        }

        // ── 픽셀 링 게이지 (화풍 v2) ─────────────────────────────────────────

        /// <summary>
        /// 진행도(0~1)에 가장 가까운 채움 단계 프레임 인덱스. frameCount 9 = 0/8..8/8.
        /// </summary>
        public static int RingFrameIndex(float progress01, int frameCount)
            => frameCount <= 1 ? 0 : Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(progress01) * (frameCount - 1)), 0, frameCount - 1);

        /// <summary>
        /// 채움 프레임이 있으면 진행도에 맞는 프레임으로 교체, 없으면 기존 방식(fillAmount).
        /// </summary>
        public static void SetRingFill(UnityEngine.UI.Image img, Sprite[] frames, float progress01)
        {
            if (frames != null && frames.Length > 0)
                img.sprite = frames[RingFrameIndex(progress01, frames.Length)];
            else
                img.fillAmount = progress01;
        }

        /// <summary>링 Image 를 프레임 교체용(Simple, 원본 색)으로 바꾼다.</summary>
        public static void UseRingFrame(UnityEngine.UI.Image img, Sprite sprite)
        {
            img.sprite = sprite;
            img.type   = UnityEngine.UI.Image.Type.Simple;
        }
    }
}
