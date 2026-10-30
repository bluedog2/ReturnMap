using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  ParallaxBackground — 2층(원경·근경) 패럴랙스 배경
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 카메라를 따라다니며 원경(far)·근경(near) 두 층을 가로로 반복(Tiled) 렌더합니다.
    /// 층마다 <c>factor</c> 만큼만 월드와 함께 밀려 시차가 생기고, <c>autoScroll</c> 로 천천히 흐를 수 있습니다.
    /// <para>페이즈(빌드·검증 / 플레이)에 따라 팔레트의 해당 세트 스프라이트로 교체합니다 (스크롤 위치 유지).</para>
    /// <para>MapLoader 가 맵 루트 자식 "ParallaxRoot" 에 부착하므로 맵 언로드 시 함께 파괴됩니다.</para>
    /// <para>CinemachineBrain 의 LateUpdate 이후 카메라 위치를 읽도록 실행 순서를 뒤로 미룹니다.</para>
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public class ParallaxBackground : MonoBehaviour
    {
        private const float HeightMargin = 1.2f; // 층 높이 = 화면 높이 × 1.2 (세로 시차 여유)
        private const float PixelArtPpu  = 32f;  // 32px 규격 배경 — 1배 표시

        private TilePaletteConfig _palette;
        private float             _mapCenterY;

        private SpriteRenderer _sky;
        private SpriteRenderer _far;
        private SpriteRenderer _near;
        private float          _farScroll;
        private float          _nearScroll;

        /// <summary>자식 렌더러를 만들고 현재 페이즈의 스프라이트를 적용합니다.</summary>
        public void Init(TilePaletteConfig palette, float mapCenterY)
        {
            _palette    = palette;
            _mapCenterY = mapCenterY;

            // 하늘: 공유 재질은 타일 아틀라스 전용 → 기본 스프라이트 재질 (MapLoader 슬롯 마커와 같은 이유)
            _sky  = CreateLayer("Sky",  -40, useSharedMaterial: false);
            _sky.drawMode = SpriteDrawMode.Simple;
            _sky.sprite   = SpriteUtil.UnitWhite();
            _sky.enabled  = false;
            _far  = CreateLayer("Far",  -30);
            _near = CreateLayer("Near", -20);
            ApplySprites(CurrentPhase());
        }

        private void OnEnable()  => GamePhaseManager.OnPhaseChanged += ApplySprites;
        private void OnDisable() => GamePhaseManager.OnPhaseChanged -= ApplySprites;

        private SpriteRenderer CreateLayer(string layerName, int order, bool useSharedMaterial = true)
        {
            var go = new GameObject(layerName);
            go.transform.SetParent(transform, false);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.drawMode     = SpriteDrawMode.Tiled;
            sr.tileMode     = SpriteTileMode.Continuous;
            if (_palette != null)
            {
                if (!string.IsNullOrEmpty(_palette.TileSortingLayer))
                    sr.sortingLayerName = _palette.TileSortingLayer;
                if (useSharedMaterial && _palette.SharedMaterial != null)
                    sr.sharedMaterial = _palette.SharedMaterial;
            }
            sr.sortingOrder = order;
            return sr;
        }

        private static GamePhase CurrentPhase()
            => GamePhaseManager.Instance != null ? GamePhaseManager.Instance.currentPhase : GamePhase.Play;

        private void ApplySprites(GamePhase phase)
        {
            if (_palette == null) return;
            SetSprite(_far,  _palette.GetParallaxSprite(phase, false));
            SetSprite(_near, _palette.GetParallaxSprite(phase, true));
        }

        private static void SetSprite(SpriteRenderer sr, Sprite sp)
        {
            if (sr == null) return;
            sr.sprite  = sp;
            sr.enabled = sp != null; // 스프라이트 없는 층은 그리지 않음
        }

        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null || _palette == null) return;

            GamePhase phase = CurrentPhase();
            UpdateLayer(cam, _far,  _palette.GetParallaxLayer(phase, false), ref _farScroll);
            UpdateLayer(cam, _near, _palette.GetParallaxLayer(phase, true),  ref _nearScroll);
            UpdateSky(cam, _palette.GetParallaxSkyColor(phase));
        }

        /// <summary>하늘 채움: 화면 전체를 단색으로 (원경 뒤). 1배 원경이 화면보다 낮을 때 위쪽 빈 곳을 메운다.</summary>
        private void UpdateSky(Camera cam, Color color)
        {
            if (_sky == null) return;
            _sky.enabled = color.a > 0f;
            if (!_sky.enabled) return;

            float viewH = 2f * cam.orthographicSize;
            float viewW = viewH * cam.aspect;
            _sky.color = color;
            _sky.transform.localScale = new Vector3(viewW + 2f, viewH + 2f, 1f); // UnitWhite = 1×1 유닛
            Vector3 cp = cam.transform.position;
            _sky.transform.position = new Vector3(cp.x, cp.y, transform.position.z);
        }

        private void UpdateLayer(Camera cam, SpriteRenderer sr, ParallaxLayer layer, ref float scroll)
        {
            if (sr == null || layer == null || sr.sprite == null) return;

            float viewH = 2f * cam.orthographicSize;
            float viewW = viewH * cam.aspect;

            Vector2 b = sr.sprite.bounds.size;
            if (b.x < 0.0001f || b.y < 0.0001f) return;

            // 32px 규격 배경(PPU 32: 원경 640×360·근경 640×256 하단 피벗)은 1배 그대로 — 늘리면 픽셀이 뭉개진다.
            // 그 외(구 배경)는 화면 높이 × 마진에 맞춰 균일 스케일
            bool pixelArt = Mathf.Approximately(sr.sprite.pixelsPerUnit, PixelArtPpu);
            float s = pixelArt ? 1f : viewH * HeightMargin / b.y;
            sr.transform.localScale = new Vector3(s, s, 1f);

            // 화면 폭을 덮고도 한 주기 이동할 여유(+2장)만큼 가로로 이어붙임
            float tileW = b.x * s;
            sr.size = new Vector2(b.x * (Mathf.CeilToInt(viewW / tileW) + 2), b.y);

            scroll += layer.autoScroll * Time.deltaTime;

            Vector3 cp   = cam.transform.position;
            // 카메라가 dx 만큼 가면 화면상 무늬는 factor*dx 만큼만 반대로 밀림 → factor 작을수록 멀리 보임.
            // 한 주기(tileW) 이동해도 같은 그림이므로 Repeat 로 wrap 해도 이음새가 보이지 않음.
            float x      = cp.x - Mathf.Repeat(cp.x * layer.factor + scroll, tileW);
            float layerH = b.y * s;
            float maxOff = Mathf.Max(0f, (layerH - viewH) * 0.5f);
            float y      = cp.y - Mathf.Clamp((cp.y - _mapCenterY) * layer.verticalFactor, -maxOff, maxOff);

            // 32px 1배 층은 화면 바닥 기준으로 붙이고 세로 시차만큼만 내려간다(올라가면 아래가 비므로 0 이상).
            // 화면이 층보다 높으면 위쪽은 하늘 채움(UpdateSky)이 메운다.
            if (pixelArt)
            {
                float bottom = cp.y - viewH * 0.5f - Mathf.Max(0f, (cp.y - _mapCenterY) * layer.verticalFactor);
                float pivotY = sr.sprite.pivot.y / sr.sprite.pixelsPerUnit; // 하단 피벗(근경) 0, 중앙 피벗(원경) 높이/2
                y = bottom + pivotY;
            }

            // z 는 맵 루트와 동일 유지 (소팅 레이어/오더로 순서 결정)
            sr.transform.position = new Vector3(x, y, transform.position.z);
        }
    }
}
