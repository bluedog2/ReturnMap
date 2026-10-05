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
        private float extendedY;   // 돌출 위치 (셀 표면 밖)
        private float retractedY;  // 수축 위치 (셀 내부)

        private Coroutine oscillateCoroutine;

        // ── 슬롯 호환 ─────────────────────────────────────────────────────────
        // CompatibleAnchors 는 TrapBase 기본 구현(TrapDefinition 참조)을 그대로 사용.

        /// <summary>천장 슬롯이면 가시를 아래 방향으로 뒤집고 지오메트리 재계산.</summary>
        protected override void OnConfigureAnchor(TrapAnchor anchor)
        {
            isFlipped = anchor == TrapAnchor.Ceiling;
            RecalcGeometry();
        }

        // ── Unity ─────────────────────────────────────────────────────────────

        protected override void Awake()
        {
            base.Awake();

            if (spikeVisual != null)
                spikeRenderer = spikeVisual.GetComponent<SpriteRenderer>();

            if (spikeRenderer != null)
                originalColor = spikeRenderer.color;

            RecalcGeometry();
        }

        /// <summary>
        /// 함정 칸이 솔리드 타일이 된 규칙에 맞춰 가시·트리거를 셀 표면 밖으로 배치.
        /// <list type="bullet">
        ///   <item>돌출: 표면(±0.5) 밖으로 extendHeight 만큼 솟음 — 밟으면 데미지</item>
        ///   <item>수축: 셀 내부로 완전히 숨음 — 표면이 평범한 타일이 됨</item>
        ///   <item>데미지 트리거: 돌출 구역만 커버 (셀 내부는 솔리드 박스가 차지)</item>
        /// </list>
        /// </summary>
        private void RecalcGeometry()
        {
            // isFlipped=false(바닥): 표면 = +0.5, 돌출 = 위쪽
            // isFlipped=true(천장) : 표면 = -0.5, 돌출 = 아래쪽
            float sign = isFlipped ? -1f : 1f;

            extendedY  = sign * (0.5f + extendHeight * 0.5f);
            retractedY = extendedY - sign * extendHeight;

            // 데미지 트리거를 표면 돌출 구역으로 이동
            if (DamageArea is BoxCollider2D box)
            {
                box.size   = new Vector2(0.8f, extendHeight);
                box.offset = new Vector2(0f, extendedY);
            }
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
                RaiseActivated();
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
