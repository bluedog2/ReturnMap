using UnityEngine;
using UnityEngine.UI;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  DashGaugeUI — 캐릭터 머리 옆 대시 아크 게이지
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 플레이어 머리 옆에 떠다니는 원형 대시 게이지 (월드 스페이스 Canvas).
    /// <list type="bullet">
    ///   <item>아크 채움 = 남은 대시 / 최대 대시</item>
    ///   <item>전량 소진 시 재충전 진행도가 차오르는 애니메이션</item>
    ///   <item>"2/3" 카운트 텍스트</item>
    /// </list>
    /// <para>Player 오브젝트에 부착 — UI는 런타임에 자동 생성 (아트 교체 시
    /// ringSprite 필드에 스프라이트만 할당).</para>
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class DashGaugeUI : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("배치")]
        [Tooltip("캐릭터 기준 게이지 위치 오프셋 (머리 옆).")]
        [SerializeField] private Vector2 offset = new Vector2(0.85f, 0.55f);

        [Tooltip("게이지 지름 (월드 유닛).")]
        [SerializeField, Min(0.2f)] private float size = 0.9f;

        [Header("색상")]
        [SerializeField] private Color fillColor  = new Color(0.25f, 0.8f, 1f, 0.95f);  // 시안
        [SerializeField] private Color emptyColor = new Color(0f, 0f, 0f, 0.45f);
        [SerializeField] private Color rechargeColor = new Color(1f, 0.85f, 0.2f, 0.9f); // 충전 중 금색

        [Header("아트 교체용 (비우면 코드 생성 링)")]
        [SerializeField] private Sprite ringSprite;

        // ── 내부 ─────────────────────────────────────────────────────────────

        private PlayerController _pc;
        private Image            _fillImage;
        private Text             _countText;
        private RectTransform    _canvasRect;

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            _pc = GetComponent<PlayerController>();
            BuildGauge();
        }

        private void LateUpdate()
        {
            if (_fillImage == null) return;

            int   max = Mathf.Max(1, _pc.MaxDashCount);
            int   rem = _pc.RemainingDashes;

            if (rem > 0)
            {
                _fillImage.fillAmount = (float)rem / max;
                _fillImage.color      = fillColor;
            }
            else
            {
                // 전량 소진 — 재충전 진행도가 금색으로 차오름
                _fillImage.fillAmount = _pc.DashRechargeProgress01;
                _fillImage.color      = rechargeColor;
            }

            if (_countText != null)
                _countText.text = $"{rem}/{max}";

            // 위치 고정 (좌우반전은 flipX 방식이라 child 가 안 뒤집히지만, 위치만 갱신)
            if (_canvasRect != null)
                _canvasRect.localPosition = offset;
        }

        // ── UI 생성 ───────────────────────────────────────────────────────────

        private void BuildGauge()
        {
            Sprite ring = ringSprite != null ? ringSprite : SpriteUtil.Ring();

            // ── 월드 스페이스 캔버스 (플레이어 자식) ─────────────────────────
            var canvasGo = new GameObject("DashGauge",
                typeof(RectTransform), typeof(Canvas));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode       = RenderMode.WorldSpace;
            canvas.sortingLayerName = "Map";  // Default 로 두면 Map(타일·배경) 에 가려진다
            canvas.sortingOrder     = 100;    // 배경 -10 < 타일 0 < 함정 10 < 캐릭터 20 < UI 100

            _canvasRect = (RectTransform)canvasGo.transform;
            _canvasRect.sizeDelta     = new Vector2(100f, 100f);
            _canvasRect.localScale    = Vector3.one * (size / 100f);
            _canvasRect.localPosition = offset;

            // ── 배경 링 (빈 게이지) ──────────────────────────────────────────
            var bg = MakeRingImage(canvasGo.transform, "Bg", ring, emptyColor);
            bg.fillAmount = 1f;

            // ── 채움 링 ──────────────────────────────────────────────────────
            _fillImage = MakeRingImage(canvasGo.transform, "Fill", ring, fillColor);
            _fillImage.fillAmount = 1f;

            // ── 카운트 텍스트 ("2/3") ────────────────────────────────────────
            var textGo = new GameObject("Count", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(canvasGo.transform, false);

            var textRect = (RectTransform)textGo.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            _countText = textGo.GetComponent<Text>();
            _countText.text          = "3/3";
            _countText.font          = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _countText.fontSize      = 32;
            _countText.fontStyle     = FontStyle.Bold;
            _countText.alignment     = TextAnchor.MiddleCenter;
            _countText.color         = Color.white;
            _countText.raycastTarget = false;
        }

        private static Image MakeRingImage(Transform parent, string name, Sprite ring, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var img = go.GetComponent<Image>();
            img.sprite        = ring;
            img.color         = color;
            img.type          = Image.Type.Filled;
            img.fillMethod    = Image.FillMethod.Radial360;
            img.fillOrigin    = (int)Image.Origin360.Top;
            img.fillClockwise = true;
            img.raycastTarget = false;
            return img;
        }
    }
}
