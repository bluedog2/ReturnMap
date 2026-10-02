using UnityEngine;
using UnityEngine.UI;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  TrapCooldownGauge — 화살 슈터 발사 쿨다운 원형 게이지 (표시 전용)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// ArrowShooter 위에 떠서 "다음 발사까지" 진행도를 보여주는 월드 스페이스 원형 게이지.
    /// <list type="bullet">
    ///   <item>Play·Verification 에서 표시, Build·Dud 는 숨김 (발사 사이클 진행 중일 때만)</item>
    ///   <item>채움 = <see cref="ArrowShooter.CycleProgress01"/> (발사 직후 0 → 발사 직전 1)</item>
    ///   <item>색상: Normal 연두 / Critical 빨강 / Beneficial 금색</item>
    /// </list>
    /// <para>함정 동작에는 관여하지 않는다. ArrowShooter.Awake 가 런타임에 자동 부착하며,
    /// UI 는 함정 루트 자식으로 생성 (Visual 자식 아님 — anchor 회전 영향 방지).</para>
    /// </summary>
    [RequireComponent(typeof(ArrowShooter))]
    public class TrapCooldownGauge : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("배치")]
        [Tooltip("함정 중심 기준 게이지 위치 오프셋 (월드 좌표계 — 앵커 회전과 무관하게 항상 화면 위쪽).")]
        [SerializeField] private Vector2 worldOffset = new Vector2(0f, 1.0f);

        [Tooltip("게이지 지름 (월드 유닛).")]
        [SerializeField, Min(0.1f)] private float diameter = 0.8f;

        [Header("색상")]
        [Tooltip("Normal 변이 채움 색.")]
        [SerializeField] private Color normalColor = new Color(0.55f, 1f, 0.25f, 1f);

        [Tooltip("Critical 변이 채움 색.")]
        [SerializeField] private Color criticalColor = new Color(1f, 0.2f, 0.2f, 1f);

        [Tooltip("Beneficial 변이 채움 색.")]
        [SerializeField] private Color beneficialColor = new Color(1f, 0.85f, 0.2f, 1f);

        [Tooltip("배경 링(빈 게이지) 색.")]
        [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.8f);

        // ── 내부 ─────────────────────────────────────────────────────────────

        private ArrowShooter  _shooter;
        private Canvas        _canvas;
        private RectTransform _canvasRect;
        private Image         _fillImage;
        private bool          _isVisiblePhase;

        // ── 진행도 계산 (순수 함수 — 테스트 가능) ────────────────────────────

        /// <summary>게이지를 표시하는 페이즈 (Play·Verification). Build 는 숨김.</summary>
        public static bool IsVisiblePhase(GamePhase p)
            => p == GamePhase.Play || p == GamePhase.Verification;

        /// <summary>
        /// 사이클 진행도 0~1. interval 이 0 이하면 0.
        /// </summary>
        public static float Progress(float start, float interval, float now)
            => interval <= 0f ? 0f : Mathf.Clamp01((now - start) / interval);

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            _shooter = GetComponent<ArrowShooter>();
            BuildGauge();
        }

        private void OnEnable()
        {
            GamePhaseManager.OnPhaseChanged += HandlePhaseChanged;

            // 초기 동기화 규약: 이벤트가 아니라 currentPhase 직접 읽기 (Instance 가 없으면 Build 취급)
            _isVisiblePhase = GamePhaseManager.Instance != null
                           && IsVisiblePhase(GamePhaseManager.Instance.currentPhase);
            RefreshVisible();
        }

        private void OnDisable()
        {
            GamePhaseManager.OnPhaseChanged -= HandlePhaseChanged;
        }

        private void LateUpdate()
        {
            if (_canvas == null || _shooter == null) return;

            RefreshVisible();
            if (!_canvas.enabled) return;

            // 앵커 회전과 무관하게 항상 화면 위쪽
            _canvasRect.SetPositionAndRotation(
                transform.position + (Vector3)worldOffset, Quaternion.identity);

            _fillImage.fillAmount = _shooter.CycleProgress01;
            _fillImage.color      = ColorFor(_shooter.CurrentState);
        }

        // ── 표시 규칙 ─────────────────────────────────────────────────────────

        private void HandlePhaseChanged(GamePhase phase)
        {
            _isVisiblePhase = IsVisiblePhase(phase);
            RefreshVisible();
        }

        /// <summary>Play·Verification 페이즈 + 사이클 진행 중일 때만 표시.</summary>
        private void RefreshVisible()
        {
            if (_canvas == null || _shooter == null) return;
            _canvas.enabled = _isVisiblePhase && _shooter.IsCycleActive;
        }

        private Color ColorFor(TrapState state) => state switch
        {
            TrapState.Critical   => criticalColor,
            TrapState.Beneficial => beneficialColor,
            _                    => normalColor,
        };

        // ── UI 생성 ───────────────────────────────────────────────────────────

        private void BuildGauge()
        {
            // 월드 스페이스 캔버스 (함정 루트 자식). SpriteRenderer/Collider2D 는 만들지 않는다
            // (TrapBase 틴트 자동 수집·물리 간섭 방지).
            var canvasGo = new GameObject("CooldownGauge", typeof(RectTransform), typeof(Canvas));
            canvasGo.transform.SetParent(transform, false);

            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode       = RenderMode.WorldSpace;
            _canvas.sortingLayerName = "Map";
            // UI 오버레이 예외: DashGaugeUI 와 동일하게 캐릭터(20) 보다 위로 의도적으로 100 사용
            _canvas.sortingOrder     = 100;
            _canvas.enabled          = false;

            _canvasRect = (RectTransform)canvasGo.transform;
            _canvasRect.sizeDelta  = new Vector2(100f, 100f);
            _canvasRect.localScale = Vector3.one * (diameter / 100f);

            Sprite ring = SpriteUtil.Ring();

            var bg = MakeRingImage(canvasGo.transform, "Bg", ring, backgroundColor);
            bg.fillAmount = 1f;

            _fillImage = MakeRingImage(canvasGo.transform, "Fill", ring, normalColor);
            _fillImage.fillAmount = 0f;
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
