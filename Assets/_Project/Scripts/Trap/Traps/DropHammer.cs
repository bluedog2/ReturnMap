using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  DropHammer — 드롭 해머 함정
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 설치 위치: 타일 아래(천장).
    /// 코스트: 50 / 데미지: 3
    ///
    /// <para><b>씬 구성 (프리팹 계층)</b></para>
    /// <code>
    /// DropHammer (이 스크립트 + TrapBase + Rigidbody2D kinematic)
    ///   ├── HammerVisual        (스프라이트)
    ///   ├── DamageArea          (Collider2D trigger  → TrapBase damageArea 에 할당)
    ///   └── PlatformArea        (Collider2D solid    → TrapBase platformArea 에 할당)
    /// </code>
    /// <para>플레이어 감지는 별도 자식 오브젝트 없이, 천장~바닥 낙하 컬럼을
    /// 매 프레임 OverlapBox 로 검사합니다. (해머가 이동해도 감지 구역은 고정)</para>
    ///
    /// <para><b>변이별 동작</b></para>
    /// <list type="table">
    ///   <item><term>Normal</term>    <description>0.2초 선딜 후 낙하, 착지 1초 후 복귀.</description></item>
    ///   <item><term>Dud</term>       <description>천장에 고정, 작동 안 함.</description></item>
    ///   <item><term>Critical</term>  <description>거의 즉발 낙하 — 대시 선입력으로만 회피.</description></item>
    ///   <item><term>Beneficial</term><description>느리게 왕복 — 엘리베이터로 활용 가능.</description></item>
    /// </list>
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class DropHammer : TrapBase
    {
        // ── 슬롯 호환 ─────────────────────────────────────────────────────────

        private static readonly TrapAnchor[] COMPATIBLE = { TrapAnchor.Ceiling };

        public override TrapAnchor[] CompatibleAnchors => COMPATIBLE;

        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("DropHammer — 낙하 설정")]
        [SerializeField, Min(0f)]
        [Tooltip("Normal: 감지 후 낙하까지 선딜레이(초).")]
        private float normalDelay = 0.2f;

        [SerializeField, Min(0f)]
        [Tooltip("Critical: 선딜레이(초). 거의 즉발.")]
        private float criticalDelay = 0.03f;

        [SerializeField, Min(1f)]
        [Tooltip("Normal 낙하 속도(유닛/초).")]
        private float normalFallSpeed = 10f;

        [SerializeField, Min(1f)]
        [Tooltip("Critical 낙하 속도(유닛/초). 극단적으로 빠르게.")]
        private float criticalFallSpeed = 80f;

        [SerializeField, Min(0f)]
        [Tooltip("착지 후 복귀까지 대기 시간(초).")]
        private float landedWaitTime = 1f;

        [SerializeField, Min(1f)]
        [Tooltip("천장으로 복귀하는 속도(유닛/초).")]
        private float returnSpeed = 10f;

        [Header("DropHammer — Beneficial 엘리베이터")]
        [SerializeField, Min(0.1f)]
        [Tooltip("엘리베이터 이동 속도(유닛/초).")]
        private float elevatorSpeed = 2f;

        [Header("DropHammer — 지면 감지")]
        [SerializeField]
        [Tooltip("낙하 종료 지점을 찾기 위한 레이어 (Ground 레이어 설정).")]
        private LayerMask groundLayer;

        [SerializeField, Min(0f)]
        [Tooltip("해머 바닥에서 지면 레이캐스트 원점까지의 오프셋.")]
        private float rayOriginOffset = 0.5f;

        [Header("DropHammer — 플레이어 감지")]
        [SerializeField, Min(0.1f)]
        [Tooltip("낙하 컬럼(감지 영역)의 가로 폭. 보통 1타일.")]
        private float detectionWidth = 0.8f;

        [SerializeField]
        [Tooltip("감지 대상 레이어. 기본값(Everything)이면 PlayerController 컴포넌트로 한 번 더 필터.")]
        private LayerMask detectionMask = ~0;

        // ── 내부 상태 ─────────────────────────────────────────────────────────

        private Rigidbody2D  rb;
        private Vector2      ceilingPosition;  // 원래 천장 위치
        private float        floorY;           // 낙하 목표 Y (지면 감지 결과)

        // 플레이어 감지용 (OverlapBox 결과 재사용)
        private readonly List<Collider2D> detectionHits = new List<Collider2D>();
        private ContactFilter2D detectionFilter;

        private enum HammerState { Idle, Falling, Landed, Returning, Elevator }
        private HammerState  hammerState = HammerState.Idle;

        private bool isTrapActive; // Normal/Critical 상태에서 활성
        private Coroutine activeCoroutine;

        // ── Unity ─────────────────────────────────────────────────────────────

        protected override void Awake()
        {
            base.Awake();
            rb = GetComponent<Rigidbody2D>();
            rb.bodyType     = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;

            ceilingPosition = rb.position;

            // 플레이어 감지 필터 (트리거 포함, 지정 레이어)
            detectionFilter = new ContactFilter2D
            {
                useTriggers = true,
            };
            detectionFilter.SetLayerMask(detectionMask);
        }

        protected override void Start()
        {
            // 지면 Y 를 미리 계산 (씬 로드 완료 후)
            RefreshFloorY();
            base.Start();
        }

        private void Update()
        {
            // Normal/Critical 상태 + 대기 중일 때만 낙하 컬럼을 검사
            if (!isTrapActive || hammerState != HammerState.Idle) return;

            if (IsPlayerInColumn())
                TriggerDrop();
        }

        // ── TrapBase 구현 ─────────────────────────────────────────────────────

        protected override void OnNormal()
        {
            StopActive();
            SnapToCeiling();
            SetDamageAreaEnabled(false); // 대기 중 데미지 없음 (낙하 시 활성화)
            SetPlatformAreaEnabled(false);
            isTrapActive  = true;
            hammerState   = HammerState.Idle;
        }

        protected override void OnDud()
        {
            StopActive();
            SnapToCeiling();
            SetDamageAreaEnabled(false);
            SetPlatformAreaEnabled(false);
            isTrapActive = false;
            hammerState  = HammerState.Idle;
        }

        protected override void OnCritical()
        {
            StopActive();
            SnapToCeiling();
            SetDamageAreaEnabled(false);
            SetPlatformAreaEnabled(false);
            isTrapActive = true;
            hammerState  = HammerState.Idle;
        }

        protected override void OnBeneficial()
        {
            StopActive();
            SnapToCeiling();
            SetDamageAreaEnabled(false);
            SetPlatformAreaEnabled(true); // 발판 항상 ON
            isTrapActive = false;
            hammerState  = HammerState.Elevator;
            activeCoroutine = StartCoroutine(ElevatorRoutine());
        }

        /// <summary>
        /// TrapBase 의 OnTriggerEnter2D 를 재정의해 낙하 중에만 데미지 처리.
        /// 플레이어 감지(낙하 트리거)는 Update 의 IsPlayerInColumn() 이 담당.
        /// </summary>
        protected override void OnTriggerEnter2D(Collider2D other)
        {
            // 낙하 중에만 데미지
            if (hammerState == HammerState.Falling &&
                other.TryGetComponent<PlayerController>(out var player))
            {
                OnPlayerContact(player);
            }
        }

        protected override void OnPlayerContact(PlayerController player)
        {
            if (player.TryGetComponent<PlayerHealth>(out var health))
                health.TakeDamage(3);
        }

        // ── 플레이어 감지 ─────────────────────────────────────────────────────

        /// <summary>
        /// 천장(ceilingPosition.y)부터 바닥(floorY)까지의 낙하 컬럼 안에
        /// 플레이어가 있는지 검사. 감지 영역은 해머 이동과 무관하게 항상 고정.
        /// </summary>
        private bool IsPlayerInColumn()
        {
            float height = Mathf.Max(0.1f, ceilingPosition.y - floorY);
            Vector2 center = new Vector2(ceilingPosition.x,
                                         (ceilingPosition.y + floorY) * 0.5f);
            Vector2 size   = new Vector2(detectionWidth, height);

            int count = Physics2D.OverlapBox(center, size, 0f, detectionFilter, detectionHits);
            for (int i = 0; i < count; i++)
            {
                if (detectionHits[i] != null &&
                    detectionHits[i].GetComponentInParent<PlayerController>() != null)
                    return true;
            }
            return false;
        }

        /// <summary>낙하 사이클 시작. 대기(Idle) 상태에서만 진입.</summary>
        private void TriggerDrop()
        {
            float delay = (CurrentState == TrapState.Critical)
                ? criticalDelay : normalDelay;
            float speed = (CurrentState == TrapState.Critical)
                ? criticalFallSpeed : normalFallSpeed;

            activeCoroutine = StartCoroutine(DropRoutine(delay, speed));
        }

        // ── 코루틴 ────────────────────────────────────────────────────────────

        /// <summary>Normal / Critical 낙하 → 착지 → 복귀 사이클.</summary>
        private IEnumerator DropRoutine(float delay, float fallSpeed)
        {
            // 선딜레이
            hammerState = HammerState.Falling;
            yield return new WaitForSeconds(delay);

            // 낙하 (데미지 활성화)
            SetDamageAreaEnabled(true);
            yield return MoveToY(floorY, fallSpeed);

            // 착지
            hammerState = HammerState.Landed;
            SetDamageAreaEnabled(false);
            yield return new WaitForSeconds(landedWaitTime);

            // 복귀
            hammerState = HammerState.Returning;
            yield return MoveToY(ceilingPosition.y, returnSpeed);

            // 대기
            hammerState = HammerState.Idle;
        }

        /// <summary>Beneficial: 천장 ↔ 바닥 무한 왕복.</summary>
        private IEnumerator ElevatorRoutine()
        {
            while (true)
            {
                yield return MoveToY(floorY,              elevatorSpeed);
                yield return MoveToY(ceilingPosition.y,   elevatorSpeed);
            }
        }

        /// <summary>Rigidbody2D.MovePosition 으로 목표 Y 까지 이동.</summary>
        private IEnumerator MoveToY(float targetY, float speed)
        {
            while (Mathf.Abs(rb.position.y - targetY) > 0.02f)
            {
                float newY = Mathf.MoveTowards(rb.position.y, targetY,
                                               speed * Time.fixedDeltaTime);
                rb.MovePosition(new Vector2(rb.position.x, newY));
                yield return new WaitForFixedUpdate();
            }

            rb.MovePosition(new Vector2(rb.position.x, targetY));
        }

        // ── 내부 헬퍼 ────────────────────────────────────────────────────────

        private void StopActive()
        {
            if (activeCoroutine != null)
            {
                StopCoroutine(activeCoroutine);
                activeCoroutine = null;
            }
        }

        private void SnapToCeiling()
        {
            rb.MovePosition(ceilingPosition);
        }

        /// <summary>
        /// 해머 바닥에서 아래 방향으로 레이캐스트해 낙하 목표 Y 를 계산.
        /// Start() 에서 호출하며, 씬 구조가 바뀌면 재호출 필요.
        /// </summary>
        private void RefreshFloorY()
        {
            Vector2 origin = (Vector2)transform.position + Vector2.down * rayOriginOffset;
            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, 100f, groundLayer);

            if (hit.collider != null)
            {
                // 지면 표면 Y + 해머 절반 높이 오프셋 (해머 바닥이 지면에 닿는 위치)
                floorY = hit.point.y + rayOriginOffset;
            }
            else
            {
                // 감지 실패 시 10유닛 아래를 기본값으로 사용
                floorY = ceilingPosition.y - 10f;
                Debug.LogWarning($"[DropHammer] {name}: 지면을 감지하지 못했습니다. " +
                                 $"groundLayer 설정을 확인하세요. floorY={floorY}");
            }
        }

#if UNITY_EDITOR
        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            // 에디터 정지 중에는 런타임 값이 없으므로 transform 기준으로 프리뷰 계산
            Vector2 top = Application.isPlaying ? ceilingPosition : (Vector2)transform.position;
            float bottomY;
            if (Application.isPlaying)
            {
                bottomY = floorY;
            }
            else
            {
                // 에디터 미리보기: 아래 방향 레이캐스트로 바닥 추정
                Vector2 origin = top + Vector2.down * rayOriginOffset;
                RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, 100f, groundLayer);
                bottomY = hit.collider != null ? hit.point.y + rayOriginOffset : top.y - 10f;
            }

            // 낙하 경로
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.8f);
            Gizmos.DrawLine(top, new Vector2(top.x, bottomY));
            Gizmos.DrawWireSphere(new Vector2(top.x, bottomY), 0.15f);

            // 플레이어 감지 컬럼
            float height = Mathf.Max(0.1f, top.y - bottomY);
            Vector3 center = new Vector3(top.x, (top.y + bottomY) * 0.5f, 0f);
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.25f);
            Gizmos.DrawCube(center, new Vector3(detectionWidth, height, 0.05f));
        }
#endif
    }
}
