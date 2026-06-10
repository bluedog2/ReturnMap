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
        [Tooltip("true = 오른쪽 발사 (왼쪽 벽 설치). false = 왼쪽 발사 (오른쪽 벽 설치).")]
        private bool facingRight = true;

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

        // ── 슬롯 호환 ─────────────────────────────────────────────────────────

        private static readonly TrapAnchor[] COMPATIBLE =
            { TrapAnchor.LeftWall, TrapAnchor.RightWall };

        public override TrapAnchor[] CompatibleAnchors => COMPATIBLE;

        /// <summary>좌벽 슬롯 = 오른쪽 발사, 우벽 슬롯 = 왼쪽 발사.</summary>
        public override void ConfigureForAnchor(TrapAnchor anchor)
            => facingRight = anchor == TrapAnchor.LeftWall;

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

            if (arrowGo.TryGetComponent<Arrow>(out var arrow))
            {
                Vector2 dir = facingRight ? Vector2.right : Vector2.left;
                arrow.Initialize(dir, arrowSpeed, isBeneficial);
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
            Vector3   dir    = facingRight ? Vector3.right : Vector3.left;
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(origin.position, dir * 2f);
        }
#endif
    }
}
