using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  ArrowShooter — 화살 슈터 함정
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 설치 위치: 슬롯 anchor 4방향 (Floor·Ceiling·LeftWall·RightWall).
    /// 코스트: 25 / 데미지: 화살 1 + 1칸 넉백
    ///
    /// <para><b>발사 방향 규칙</b></para>
    /// 슬롯 anchor 가 결정한다 (OnConfigureAnchor → OutwardDir).
    /// 벽 규약: RightWall 슬롯(왼쪽 가장자리 벽)=오른쪽 발사,
    /// LeftWall 슬롯(오른쪽 가장자리 벽)=왼쪽 발사.
    ///
    /// <para><b>변이별 동작</b></para>
    /// <list type="table">
    ///   <item><term>Normal</term>    <description>1초 주기, 느린 화살 — 점프로 회피 가능.</description></item>
    ///   <item><term>Dud</term>       <description>스파크만 튀고 발사 안 됨.</description></item>
    ///   <item><term>Critical</term>  <description>0.5초 주기 — 극단적으로 빠른 연사.</description></item>
    ///   <item><term>Beneficial</term><description>황금 화살 — 피격 시 1초 무적 부여(피격마다 갱신).</description></item>
    /// </list>
    /// </summary>
    public class ArrowShooter : TrapBase
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("ArrowShooter — 발사 설정")]
        [SerializeField]
        [Tooltip("화살 프리팹 (Arrow 컴포넌트 포함 필수).")]
        private GameObject arrowPrefab;

        [SerializeField]
        [Tooltip("화살이 생성될 발사 지점.")]
        private Transform firePoint;

        [SerializeField, Min(1f)]
        [Tooltip("화살 비행 속도(유닛/초).")]
        private float arrowSpeed = 12f;

        [SerializeField, Min(0.05f)]
        [Tooltip("Normal 상태 발사 간격(초).")]
        private float normalFireInterval = 1f;

        [SerializeField, Min(0.05f)]
        [Tooltip("Critical 상태 발사 간격(초).")]
        private float criticalFireInterval = 0.5f;

        // ── 내부 상태 ─────────────────────────────────────────────────────────

        private Coroutine firingCoroutine;

        /// <summary>현재 발사 사이클 시작 시각 (Time.time, 스케일 시간).</summary>
        private float _cycleStartTime;

        /// <summary>현재 발사 사이클 길이(초). 0 이면 사이클 없음(정지).</summary>
        private float _cycleInterval;

        /// <summary>발사 사이클이 진행 중인지 (코루틴 동작 + 간격 &gt; 0). 쿨다운 게이지 표시용.</summary>
        public bool IsCycleActive => firingCoroutine != null && _cycleInterval > 0f;

        /// <summary>현재 사이클 진행도 0~1 (발사 직후 0 → 다음 발사 직전 1).</summary>
        public float CycleProgress01
            => TrapCooldownGauge.Progress(_cycleStartTime, _cycleInterval, Time.time);

        /// <summary>발사 방향 (슬롯이 붙은 면의 바깥 = 맵 안쪽). 기본 오른쪽.</summary>
        private Vector2 _fireDir = Vector2.right;

        /// <summary>
        /// 이 슈터 전용 화살 풀. 슈터별 소유인 이유: 스폰 시 거는
        /// Physics2D.IgnoreCollision(자기 몸 통과)이 콜라이더 쌍 단위로 영구 저장되므로,
        /// 화살을 슈터 간 공유하면 다른 슈터의 솔리드 셀을 통과하는 버그가 생긴다.
        /// </summary>
        private ComponentPool<Arrow> _arrowPool;

        /// <summary>화살 반납 콜백 (매 발사 델리게이트 할당 방지용 캐시).</summary>
        private System.Action<Arrow> _releaseArrow;

        /// <summary>비행 중인 화살 (씬 루트로 분리돼 있으므로 슈터 철거 시 직접 정리).</summary>
        private readonly List<Arrow> _activeArrows = new List<Arrow>();

        // ── Unity ─────────────────────────────────────────────────────────────

        protected override void Awake()
        {
            base.Awake();

            // 쿨다운 게이지는 표시 전용 — 프리팹 수정 없이 런타임에 부착
            if (!TryGetComponent<TrapCooldownGauge>(out _))
                gameObject.AddComponent<TrapCooldownGauge>();
        }

        // ── 슬롯 호환 ─────────────────────────────────────────────────────────
        // 4방향 모두 설치 가능 — 슬롯이 붙은 면의 바깥으로 발사한다.
        // CompatibleAnchors 는 TrapBase 기본 구현(TrapDefinition 참조)을 그대로 사용.

        /// <summary>
        /// anchor 슬롯이 발사하는 방향. Floor=위, Ceiling=아래.
        /// 벽 규약(맵 데이터 기준): RightWall 슬롯은 왼쪽 가장자리 벽에 붙어 오른쪽(맵 안)으로,
        /// LeftWall 슬롯은 오른쪽 가장자리 벽에 붙어 왼쪽(맵 안)으로 발사한다.
        /// </summary>
        private static Vector2 OutwardDir(TrapAnchor anchor) => anchor switch
        {
            TrapAnchor.Floor     => Vector2.up,
            TrapAnchor.Ceiling   => Vector2.down,
            TrapAnchor.LeftWall  => Vector2.left,
            TrapAnchor.RightWall => Vector2.right,
            _                    => Vector2.right,
        };

        /// <summary>
        /// 슬롯 방향(anchor 바깥)으로 발사하도록 방향·발사구를 설정합니다.
        /// 발사구(firePoint)도 발사 방향 쪽으로 옮겨 — 안 하면 벽/바닥 안에서
        /// 화살이 생성돼 즉시 소멸한다.
        /// </summary>
        protected override void OnConfigureAnchor(TrapAnchor anchor)
        {
            _fireDir = OutwardDir(anchor);

            // 발사구를 발사 방향으로 (벽/바닥 안에서 화살이 생기지 않도록)
            if (firePoint != null)
            {
                float dist = firePoint.localPosition.magnitude;
                if (dist < 0.01f) dist = 0.45f;
                firePoint.localPosition = (Vector3)(_fireDir * dist);
            }

            // 활 본체(Visual) 회전. 실제 발사 방향(_fireDir)·화살 비행은 건드리지 않고
            // 스프라이트만 돌린다. 위아래는 y 반전, 좌우는 스프라이트가 발사 방향과
            // 반대로 그려져 x 도 반전한다(x=0 인 상하 발사엔 영향 없음).
            var vis = transform.Find("Visual");
            if (vis != null)
            {
                float angle = Mathf.Atan2(-_fireDir.y, -_fireDir.x) * Mathf.Rad2Deg + 180f;
                vis.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        // ── TrapBase 구현 ─────────────────────────────────────────────────────

        protected override void OnNormal()
        {
            RestartFiring(normalFireInterval, isBeneficial: false);
        }

        protected override void OnDud()
        {
            // 발사 정지 — 스파크는 TrapBase 가 dudParticles 재생
            StopFiring();
        }

        protected override void OnCritical()
        {
            RestartFiring(criticalFireInterval, isBeneficial: false);
        }

        protected override void OnBeneficial()
        {
            // 황금 화살 발사 (발사 간격은 Normal 과 동일)
            RestartFiring(normalFireInterval, isBeneficial: true);
        }

        /// <summary>ArrowShooter 는 발사체가 직접 플레이어와 충돌하므로
        /// TrapBase 의 OnPlayerContact 는 사용하지 않는다.</summary>
        protected override void OnPlayerContact(PlayerController player) { }

        // ── 발사 로직 ─────────────────────────────────────────────────────────

        private void RestartFiring(float interval, bool isBeneficial)
        {
            StopFiring();
            firingCoroutine = StartCoroutine(FireRoutine(interval, isBeneficial));
        }

        private void StopFiring()
        {
            if (firingCoroutine != null)
            {
                StopCoroutine(firingCoroutine);
                firingCoroutine = null;
            }
            _cycleInterval = 0f;
        }

        private IEnumerator FireRoutine(float interval, bool isBeneficial)
        {
            // 첫 발사 전 1사이클 대기 (배치 직후 즉발 방지)
            _cycleStartTime = Time.time;
            _cycleInterval  = interval;
            yield return new WaitForSeconds(interval);

            while (true)
            {
                SpawnArrow(isBeneficial);
                _cycleStartTime = Time.time;
                _cycleInterval  = interval;
                yield return new WaitForSeconds(interval);
            }
        }

        private void SpawnArrow(bool isBeneficial)
        {
            if (!EnsureArrowPool()) return;

            Transform spawnPoint = firePoint != null ? firePoint : transform;
            Arrow arrow = _arrowPool.Get(spawnPoint.position, Quaternion.identity);
            arrow.SetReleaseCallback(_releaseArrow);

            // 비행 중엔 슈터 계층에서 분리 — 자식으로 두면 다음 발사(형제 생성/활성화) 때
            // 계층 변경으로 비행 중 화살의 물리 포즈가 다시 동기화돼 발사 타이밍에 화살이 튄다.
            arrow.transform.SetParent(null, true);
            _activeArrows.Add(arrow);

            // 화살이 자기 솔리드 셀·황금 블록에 박혀 즉시 소멸하지 않도록 충돌 무시.
            // 풀 화살이 자식으로 붙어 있으므로 다른 화살의 콜라이더는 건너뛴다.
            if (arrow.TryGetComponent<Collider2D>(out var arrowCol))
            {
                foreach (var ownCol in GetComponentsInChildren<Collider2D>(true))
                {
                    if (ownCol.GetComponentInParent<Arrow>() != null) continue;
                    Physics2D.IgnoreCollision(arrowCol, ownCol);
                }
            }

            arrow.Initialize(_fireDir, arrowSpeed, isBeneficial, this);
        }

        /// <summary>화살 풀 지연 생성. 프리팹 미지정/Arrow 누락 시 false.</summary>
        private bool EnsureArrowPool()
        {
            if (_arrowPool != null) return true;

            if (arrowPrefab == null)
            {
                Debug.LogWarning($"[ArrowShooter] arrowPrefab 이 설정되지 않았습니다: {name}");
                return false;
            }

            if (!arrowPrefab.TryGetComponent<Arrow>(out var prefabArrow))
            {
                Debug.LogWarning($"[ArrowShooter] Arrow 컴포넌트를 찾을 수 없습니다: {arrowPrefab.name}");
                return false;
            }

            // 대기 중 화살은 슈터 자식으로 보관 — 슈터 철거 시 함께 정리된다
            _arrowPool    = new ComponentPool<Arrow>(prefabArrow, transform);
            _releaseArrow = ReleaseArrow;
            return true;
        }

        /// <summary>화살 반납 — 슈터 자식으로 되돌린 뒤 풀에 보관.</summary>
        private void ReleaseArrow(Arrow arrow)
        {
            _activeArrows.Remove(arrow);
            if (arrow != null) arrow.transform.SetParent(transform, true);
            _arrowPool.Release(arrow);
        }

        private void OnDestroy()
        {
            // 비행 중 화살은 씬 루트에 있으므로 직접 파괴 (반납 콜백이 파괴된 슈터를 부르지 않도록)
            foreach (var arrow in _activeArrows)
                if (arrow != null) Destroy(arrow.gameObject);
            _activeArrows.Clear();

            // 대기 중 화살은 자식이라 함께 파괴되지만, 풀 내부 참조를 명시적으로 비운다
            _arrowPool?.Clear();
        }

#if UNITY_EDITOR
        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            // 발사 방향 화살표 표시
            Transform origin = firePoint != null ? firePoint : transform;
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(origin.position, (Vector3)(_fireDir == Vector2.zero ? Vector2.right : _fireDir) * 2f);
        }
#endif
    }
}
