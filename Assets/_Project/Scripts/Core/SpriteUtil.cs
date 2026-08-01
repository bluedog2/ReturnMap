using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  SpriteUtil — 공용 1×1 흰 폴백 스프라이트
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
    }
}
