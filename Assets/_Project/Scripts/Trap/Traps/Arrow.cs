using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  Arrow — 화살 슈터가 발사하는 투사체
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// ArrowShooter 가 풀(<see cref="ComponentPool{T}"/>)에서 꺼낸 뒤
    /// <see cref="Initialize"/> 를 호출해 설정합니다. 소멸 시 Destroy 대신
    /// 반납 콜백(<see cref="SetReleaseCallback"/>)으로 소유 풀에 되돌아갑니다.
    ///
    /// <para><b>Normal / Critical 화살</b>: 1 데미지 + 수평 넉백</para>
    /// <para><b>Beneficial 화살</b>: 데미지 없음 + 1초 무적 부여 (피격마다 갱신)</para>
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class Arrow : MonoBehaviour, IPoolable
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [SerializeField, Min(1f)]
        [Tooltip("화살이 소멸하는 최대 비행 거리.")]
        private float maxRange = 20f;

        [SerializeField]
        [Tooltip("Normal/Critical 피격 시 넉백 속도(유닛/초). 1칸 이동 기준 약 10.")]
        private float knockbackSpeed = 10f;

        [SerializeField]
        [Tooltip("Beneficial 화살 황금색.")]
        private Color beneficialColor = new Color(1f, 0.85f, 0.2f);

        // ── 런타임 상태 ───────────────────────────────────────────────────────

        private bool    isBeneficial;
        private Vector2 direction;
        private Vector3 spawnPosition;

        private Rigidbody2D    rb;
        private SpriteRenderer sr;

        private Color _originalColor;              // Beneficial 황금 틴트 복원용
        private System.Action<Arrow> _release;     // 풀 반납 콜백 (미지정 시 Destroy 폴백)
        private bool _despawned;                   // 같은 프레임 이중 소멸 방지

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            sr = GetComponentInChildren<SpriteRenderer>();
            if (sr != null) _originalColor = sr.color;
        }

        private void Update()
        {
            // 최대 사거리 초과 시 소멸
            if (Vector3.SqrMagnitude(transform.position - spawnPosition) >
                maxRange * maxRange)
            {
                Despawn();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // 플레이어 피격
            if (other.TryGetComponent<PlayerController>(out var player))
            {
                HitPlayer(player);
                Despawn();
                return;
            }

            // 지형(비-트리거 콜라이더) 충돌 시 소멸
            if (!other.isTrigger)
                Despawn();
        }

        // ── 공개 API ──────────────────────────────────────────────────────────

        /// <summary>
        /// 소멸 시 자신을 되돌릴 풀 반납 콜백. 소유자(ArrowShooter)가 스폰 직후 지정.
        /// 미지정 상태(수동 배치 등)에서는 기존처럼 Destroy 로 폴백합니다.
        /// </summary>
        public void SetReleaseCallback(System.Action<Arrow> release) => _release = release;

        /// <summary>
        /// ArrowShooter 가 풀에서 꺼낸 직후 호출.
        /// </summary>
        /// <param name="dir">발사 방향 (정규화된 벡터).</param>
        /// <param name="speed">비행 속도(유닛/초).</param>
        /// <param name="beneficial">true = 황금 화살 (무적 부여).</param>
        public void Initialize(Vector2 dir, float speed, bool beneficial)
        {
            direction    = dir.normalized;
            isBeneficial = beneficial;
            spawnPosition = transform.position;

            // 중력 무시 (수평 직선 비행)
            rb.gravityScale   = 0f;
            rb.linearVelocity = direction * speed;

            // 스프라이트 방향 회전
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);

            // Beneficial = 황금색
            if (isBeneficial && sr != null)
                sr.color = beneficialColor;
        }

        // ── IPoolable — ComponentPool 재사용 훅 ──────────────────────────────

        /// <summary>풀에서 꺼내질 때 — Beneficial 황금 틴트 등 이전 상태 잔류 제거.</summary>
        public void OnSpawned()
        {
            _despawned = false;
            if (sr != null) sr.color = _originalColor;
        }

        /// <summary>풀로 반납될 때 — 물리 잔여 속도 정리.</summary>
        public void OnDespawned()
        {
            if (rb != null) rb.linearVelocity = Vector2.zero;
        }

        // ── 내부 ──────────────────────────────────────────────────────────────

        /// <summary>Destroy 대신 소유 풀로 반납. 풀이 없으면(수동 배치) Destroy 폴백.</summary>
        private void Despawn()
        {
            if (_despawned) return;
            _despawned = true;

            if (_release != null) _release(this);
            else                  Destroy(gameObject);
        }

        private void HitPlayer(PlayerController player)
        {
            if (isBeneficial)
            {
                // 황금 화살: 무적 1초 부여 (피격마다 갱신)
                if (player.TryGetComponent<PlayerHealth>(out var health))
                    health.SetInvincible(1f);
            }
            else
            {
                // 일반/악화 화살: 1 데미지 + 넉백
                if (player.TryGetComponent<PlayerHealth>(out var health))
                    health.TakeDamage(1);

                // 발사 방향으로 넉백 (수평만)
                player.ApplyKnockback(new Vector2(direction.x * knockbackSpeed, 0f));
            }
        }
    }
}
