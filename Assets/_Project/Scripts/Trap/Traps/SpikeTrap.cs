using System.Collections;
using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  SpikeTrap — 스파이크 함정
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 설치 위치: 타일 위(바닥) 또는 아래(천장).
    /// 코스트: 10 / 데미지: 1
    ///
    /// <para><b>변이별 동작</b></para>
    /// <list type="table">
    ///   <item><term>Normal</term>    <description>고정 가시. 밟으면 1 데미지.</description></item>
    ///   <item><term>Dud</term>       <description>가시가 타일 속으로 수축 → 안전 통과.</description></item>
    ///   <item><term>Critical</term>  <description>가시가 돌출↔수축 반복. 완전히 수축했을 때만 안전.</description></item>
    ///   <item><term>Beneficial</term><description>황금빛으로 굳어 1칸짜리 추가 발판이 됨.</description></item>
    /// </list>
    /// </summary>
    public class SpikeTrap : TrapBase
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("SpikeTrap — 비주얼")]
        [SerializeField]
        [Tooltip("가시 스프라이트를 담고 있는 자식 Transform. localPosition.y 로 돌출/수축 표현.")]
        private Transform spikeVisual;

        [SerializeField, Min(0f)]
        [Tooltip("가시가 타일 밖으로 돌출되는 최대 거리(유닛).")]
        private float extendHeight = 0.4f;

        [SerializeField]
        [Tooltip("천장에 설치 시 true — 가시가 아래 방향으로 돌출.")]
        private bool isFlipped = false;

        [Header("SpikeTrap — 진동")]
        [SerializeField, Min(0.1f)]
        [Tooltip("Normal 진동 1사이클 시간(초). 사이클의 35%는 수축 상태로 대기 — 통과 타이밍 구간.")]
        private float normalCycleDuration = 2f;

        [SerializeField, Min(0.1f)]
        [Tooltip("Critical 진동 1사이클 시간(초). 수축 대기 없이 쉴 새 없이 진동.")]
        private float cycleDuration = 1f;

        [Header("SpikeTrap — Beneficial 색상")]
        [SerializeField]
        private Color beneficialColor = new Color(1f, 0.85f, 0.2f);

        // ── 내부 상태 ─────────────────────────────────────────────────────────

        private SpriteRenderer spikeRenderer;
        private Color          originalColor;

        // 가시 localPosition.y 기준값
        private float extendedY;   // 돌출 위치 (타일 표면 위)
        private float retractedY;  // 수축 위치 (타일 내부)

        private Coroutine oscillateCoroutine;

        // ── 슬롯 호환 ─────────────────────────────────────────────────────────

        private static readonly TrapAnchor[] COMPATIBLE =
            { TrapAnchor.Floor, TrapAnchor.Ceiling };

        public override TrapAnchor[] CompatibleAnchors => COMPATIBLE;

        /// <summary>천장 슬롯이면 가시를 아래 방향으로 뒤집고 Y 기준값 재계산.</summary>
        public override void ConfigureForAnchor(TrapAnchor anchor)
        {
            isFlipped  = anchor == TrapAnchor.Ceiling;
            retractedY = isFlipped ? extendHeight : -extendHeight; // Awake 계산 갱신
        }

        // ── Unity ─────────────────────────────────────────────────────────────

        protected override void Awake()
        {
            base.Awake();

            if (spikeVisual != null)
                spikeRenderer = spikeVisual.GetComponent<SpriteRenderer>();

            if (spikeRenderer != null)
                originalColor = spikeRenderer.color;

            // 방향에 따른 돌출/수축 Y 계산
            // isFlipped=false(바닥): 돌출 = 위(+), 수축 = 아래(-)
            // isFlipped=true(천장) : 돌출 = 아래(-), 수축 = 위(+)
            extendedY  = 0f;
            retractedY = isFlipped ? extendHeight : -extendHeight;
        }

        // ── TrapBase 구현 ──────────────────────────────────────────────────────

        protected override void OnNormal()
        {
            StopOscillate();
            RestoreColor();
            // 느린 진동 — 사이클의 35% 는 수축 대기 (데미지 OFF, 통과 타이밍)
            oscillateCoroutine = StartCoroutine(
                OscillateRoutine(normalCycleDuration, normalCycleDuration * 0.35f));
        }

        protected override void OnDud()
        {
            StopOscillate();
            MoveSpikeToY(retractedY);   // 타일 속으로 수축
            // damageArea 는 TrapBase 가 Dud 판단으로 차단
            RestoreColor();
        }

        protected override void OnCritical()
        {
            StopOscillate();
            RestoreColor();
            // 빠른 진동 — 수축 대기 없음 (완전 수축 순간만 안전)
            oscillateCoroutine = StartCoroutine(OscillateRoutine(cycleDuration, 0f));
        }

        protected override void OnBeneficial()
        {
            StopOscillate();
            MoveSpikeToY(extendedY);            // 가시 위치 고정 (발판 역할)
            // damageArea OFF, platformArea ON — TrapBase 가 처리
            if (spikeRenderer != null)
                spikeRenderer.color = beneficialColor;
        }

        protected override void OnPlayerContact(PlayerController player)
        {
            if (player.TryGetComponent<PlayerHealth>(out var health))
                health.TakeDamage(1);
        }

        // ── 진동 코루틴 (Normal·Critical 공용) ────────────────────────────────

        /// <summary>
        /// 수축 → 돌출 → 수축 무한 반복.
        /// </summary>
        /// <param name="cycle">1사이클 전체 시간(초).</param>
        /// <param name="retractedHold">
        /// 완전 수축 상태로 대기하는 시간(초) — 이 동안 데미지 OFF (통과 타이밍).
        /// 0이면 1프레임만 OFF (Critical).
        /// </param>
        private IEnumerator OscillateRoutine(float cycle, float retractedHold)
        {
            float moveTime = Mathf.Max(0.05f, (cycle - retractedHold) * 0.5f);

            // 초기: 수축 위치에서 시작
            MoveSpikeToY(retractedY);

            while (true)
            {
                // ── 수축 대기: 안전 구간 (데미지 OFF) ────────────────────────
                SetDamageAreaEnabled(false);
                if (retractedHold > 0f)
                    yield return new WaitForSeconds(retractedHold);
                else
                    yield return null;

                // ── 돌출 → 수축: 데미지 ON ───────────────────────────────────
                SetDamageAreaEnabled(true);
                yield return LerpY(retractedY, extendedY, moveTime);
                yield return LerpY(extendedY, retractedY, moveTime);
            }
        }

        private IEnumerator LerpY(float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                MoveSpikeToY(Mathf.Lerp(from, to, elapsed / duration));
                yield return null;
            }
            MoveSpikeToY(to);
        }

        // ── 내부 헬퍼 ────────────────────────────────────────────────────────

        private void MoveSpikeToY(float y)
        {
            if (spikeVisual == null) return;
            var p = spikeVisual.localPosition;
            spikeVisual.localPosition = new Vector3(p.x, y, p.z);
        }

        private void StopOscillate()
        {
            if (oscillateCoroutine == null) return;
            StopCoroutine(oscillateCoroutine);
            oscillateCoroutine = null;
        }

        private void RestoreColor()
        {
            if (spikeRenderer != null)
                spikeRenderer.color = originalColor;
        }

#if UNITY_EDITOR
        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            if (spikeVisual == null) return;

            // 돌출 범위 표시
            float dir = isFlipped ? -1f : 1f;
            Vector3 tip = spikeVisual.position + Vector3.up * dir * extendHeight;
            Gizmos.color = Color.red;
            Gizmos.DrawLine(spikeVisual.position, tip);
        }
#endif
    }
}
