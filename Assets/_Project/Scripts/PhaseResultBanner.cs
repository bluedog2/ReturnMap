using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  PhaseResultBanner — 페이즈 결과 중앙 배너 ("방어 성공!" 등)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 화면 중앙에 결과 문구를 페이드 인 → 유지 → 페이드 아웃으로 잠깐 띄우는 배너.
    /// <para><b>씬 배선 불필요</b>: 최초 <see cref="Show"/> 호출 시 자기 오브젝트 아래에
    /// Screen Space Overlay Canvas 를 런타임 생성합니다 (DashGaugeUI 와 같은 방식).</para>
    /// <para>모든 시간은 <see cref="Time.unscaledDeltaTime"/> 기준 — 검증 배속(timeScale)과 무관하게
    /// 같은 길이로 보입니다.</para>
    /// </summary>
    public class PhaseResultBanner : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("연출")]
        [SerializeField, Min(0f)]
        [Tooltip("페이드 인 시간(초)")]
        private float fadeInTime = 0.25f;

        [SerializeField, Min(0f)]
        [Tooltip("페이드 아웃 시간(초)")]
        private float fadeOutTime = 0.4f;

        [SerializeField]
        [Tooltip("문구 글자 크기")]
        private int fontSize = 64;

        [SerializeField]
        [Tooltip("문구 색상")]
        private Color textColor = new Color(1f, 0.85f, 0.3f);

        // ── 런타임 ────────────────────────────────────────────────────────────

        private CanvasGroup _group;
        private Text        _text;
        private Coroutine   _routine;

        // ── 공개 API ──────────────────────────────────────────────────────────

        /// <summary>
        /// 배너를 띄웁니다. <paramref name="visibleTime"/> 은 페이드 인 이후 완전히 보이는 시간이며,
        /// 그 뒤 페이드 아웃합니다. 진행 중인 배너가 있으면 교체합니다.
        /// </summary>
        public void Show(string message, float visibleTime)
        {
            EnsureUi();
            _text.text = message;

            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(Animate(visibleTime));
        }

        // ── 내부 ──────────────────────────────────────────────────────────────

        private IEnumerator Animate(float visibleTime)
        {
            _group.gameObject.SetActive(true);

            yield return Fade(0f, 1f, fadeInTime);
            for (float t = 0f; t < visibleTime; t += Time.unscaledDeltaTime) yield return null;
            yield return Fade(1f, 0f, fadeOutTime);

            _group.gameObject.SetActive(false);
            _routine = null;
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                _group.alpha = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }
            _group.alpha = to;
        }

        private void EnsureUi()
        {
            if (_group != null) return;

            var canvasGo = new GameObject("PhaseResultBanner", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            _group = canvasGo.GetComponent<CanvasGroup>();
            _group.alpha          = 0f;
            _group.blocksRaycasts = false;
            _group.interactable   = false;

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(Outline));
            textGo.transform.SetParent(canvasGo.transform, false);

            var rect = (RectTransform)textGo.transform;
            rect.anchorMin        = new Vector2(0f, 0.5f);
            rect.anchorMax        = new Vector2(1f, 0.5f);
            rect.sizeDelta        = new Vector2(0f, fontSize * 2f);
            rect.anchoredPosition = new Vector2(0f, 80f);

            _text = textGo.GetComponent<Text>();
            _text.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _text.fontSize  = fontSize;
            _text.fontStyle = FontStyle.Bold;
            _text.alignment = TextAnchor.MiddleCenter;
            _text.color     = textColor;
            _text.raycastTarget = false;

            var outline = textGo.GetComponent<Outline>();
            outline.effectColor    = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(3f, -3f);

            canvasGo.SetActive(false);
        }
    }
}
