using System.Collections;
using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  ArrowShooter — 화살 슈터 함정
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 설치 위치: 좌/우 벽 타일.
    /// 코스트: 25 / 데미지: 화살 1 + 1칸 넉백
    ///
    /// <para><b>발사 방향 규칙</b></para>
    /// 왼쪽 벽 = 오른쪽으로만 발사, 오른쪽 벽 = 왼쪽으로만 발사.
    /// (<see cref="facingRight"/> 로 Inspector 에서 설정.)
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

        /// <summary>발사 방향 (슬롯이 붙은 면의 바깥 = 맵 안쪽). 기본 오른쪽.</summary>
        private Vector2 _fireDir = Vector2.right;

        // ── 슬롯 호환 ─────────────────────────────────────────────────────────

        // 4방향 모두 설치 가능 — 슬롯이 붙은 면의 바깥으로 발사한다.
        private static readonly TrapAnchor[] COMPATIBLE =
            { TrapAnchor.Floor, TrapAnchor.Ceiling, TrapAnchor.LeftWall, TrapAnchor.RightWall };

        public override TrapAnchor[] CompatibleAnchors => COMPATIBLE;

        /// <summary>anchor 가 붙은 면의 바깥 방향 (Floor=위, Ceiling=아래, 벽=반대쪽).</summary>
        private static Vector2 OutwardDir(TrapAnchor anchor) => anchor switch
        {
            TrapAnchor.Floor     => Vector2.up,
            TrapAnchor.Ceiling   => Vector2.down,
            TrapAnchor.LeftWall  => Vector2.right,
            TrapAnchor.RightWall => Vector2.left,
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

            // 활 본체(Visual)를 발사 방향으로 회전.
            // 활 스프라이트가 왼쪽을 향한 기본이라 +180° 보정해야 발사 방향과 맞는다.
            var vis = transform.Find("Visual");
            if (vis != null)
            {
                float angle = Mathf.Atan2(_fireDir.y, _fireDir.x) * Mathf.Rad2Deg + 180f;
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
        }

        private IEnumerator FireRoutine(float interval, bool isBeneficial)
        {
            // 첫 발사 전 1사이클 대기 (배치 직후 즉발 방지)
            yield return new WaitForSeconds(interval);

            while (true)
            {
                SpawnArrow(isBeneficial);
                yield return new WaitForSeconds(interval);
            }
        }

        private void SpawnArrow(bool isBeneficial)
        {
            if (arrowPrefab == null)
            {
                Debug.LogWarning($"[ArrowShooter] arrowPrefab 이 설정되지 않았습니다: {name}");
                return;
            }

            Transform spawnPoint = firePoint != null ? firePoint : transform;
            GameObject arrowGo   = Instantiate(arrowPrefab, spawnPoint.position, Quaternion.identity);

            // 화살이 자기 솔리드 셀·황금 블록에 박혀 즉시 소멸하지 않도록 충돌 무시
            if (arrowGo.TryGetComponent<Collider2D>(out var arrowCol))
            {
                foreach (var ownCol in GetComponentsInChildren<Collider2D>(true))
                    Physics2D.IgnoreCollision(arrowCol, ownCol);
            }

            if (arrowGo.TryGetComponent<Arrow>(out var arrow))
            {
                arrow.Initialize(_fireDir, arrowSpeed, isBeneficial);
            }
            else
            {
                Debug.LogWarning($"[ArrowShooter] Arrow 컴포넌트를 찾을 수 없습니다: {arrowPrefab.name}");
                Destroy(arrowGo);
            }
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
