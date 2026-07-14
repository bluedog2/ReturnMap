using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  VerificationAgent — 검증 AI 실행체 (씬 위를 실제로 움직이는 캐릭터)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 계획된 경로(웨이포인트)를 씬에서 실제로 수행하는 AI 캐릭터의 <b>골격</b>.
    /// 프리팹으로 만들어 <see cref="VerificationDirector"/> 가 스폰/회수합니다.
    ///
    /// <para><b>계획과 실행의 분리</b>: 경로 계산은 <see cref="IPathPlanner"/> 담당,
    /// 이 클래스는 "그 경로를 몸으로 얼마나 잘 수행하나"만 담당합니다.
    /// 성향 스텟 중 민첩함(실수율·속도)과 침착함(함정 대기)이 여기서 소비됩니다.</para>
    ///
    /// <para>TODO(실행 본체):
    /// 1) 지금은 웨이포인트 직선 이동(자리표시자) — 실제로는 Rigidbody2D 기반
    ///    플랫포머 이동(점프/낙하)으로 교체. PlayerController 물리 상수 재사용 검토.
    /// 2) 함정 히트 판정: TrapBase 충돌 감지가 플레이어만 인지한다면
    ///    VerificationAgent 도 대상에 포함하도록 확장하고, 피격 시 <see cref="Kill"/> 호출.
    /// 3) mistakeChance 로 점프 타이밍 오차, trapWaitTolerance 로 함정 앞 대기 구현.</para>
    /// </summary>
    public class VerificationAgent : MonoBehaviour, IPoolable
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("이동 (자리표시자)")]
        [SerializeField]
        [Tooltip("기준 이동속도 (칸/초). 성향의 moveSpeedMultiplier 가 곱해진다")]
        private float baseMoveSpeed = 4f;

        // ── 애니메이션 — Enemy_*.controller 의 상태 이름과 일치해야 함 ──────────

        private static readonly int StateIdle  = Animator.StringToHash("Idle");
        private static readonly int StateRun   = Animator.StringToHash("Run");
        private static readonly int StateDeath = Animator.StringToHash("Death");

        private Animator       _animator; // 없어도 동작 (선택 구성)
        private SpriteRenderer _sprite;   // 좌우 반전용
        private AgentContext   _ctx;      // 태그/스탯 조회 (null 허용 — 미배선 프리팹 하위 호환)

        // ── 결과 상태 — Director 가 읽음 ─────────────────────────────────────

        /// <summary>경로 수행이 끝났는가 (골 도달 또는 사망).</summary>
        public bool IsDone { get; private set; }

        /// <summary>골 지점에 도달했는가 (= 방어 뚫림).</summary>
        public bool ReachedGoal { get; private set; }

        /// <summary>마지막으로 지나던 셀 — 사망 시 AIMemory 기록용.</summary>
        public GridCoord CurrentCell { get; private set; }

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _sprite   = GetComponentInChildren<SpriteRenderer>();
            _ctx      = GetComponent<AgentContext>();

            // 자리표시자 이동은 transform 직접 제어 — Dynamic 물리와 싸우지 않게
            // 키네마틱으로 강제. useFullKinematicContacts 는 함정 루트 RB(키네마틱)와의
            // 트리거 이벤트가 누락되지 않게 하기 위해 필수.
            if (TryGetComponent<Rigidbody2D>(out var rb))
            {
                rb.bodyType                 = RigidbodyType2D.Kinematic;
                rb.useFullKinematicContacts = true;
                // 보간이 켜져 있으면 물리 보간 포즈가 transform 직접 이동을 매 프레임
                // 되돌려 제자리걸음이 된다 — 반드시 꺼야 함.
                rb.interpolation            = RigidbodyInterpolation2D.None;
            }
        }

        // ── 실행 ──────────────────────────────────────────────────────────────

        /// <summary>
        /// 웨이포인트 경로를 순서대로 수행합니다. Director 가 yield 로 대기합니다.
        /// </summary>
        public IEnumerator FollowPath(IReadOnlyList<GridCoord> path, MapData map, Vector2 origin,
                                      AIBehaviorParams p)
        {
            IsDone      = false;
            ReachedGoal = false;

            // ctx 가 있으면 태그가 접힌 스탯을 기준으로, 없으면 기존 baseMoveSpeed 로.
            // 어느 경로든 성향(personality)의 moveSpeedMultiplier 는 항상 곱해진다(개체별 성향 유지).
            float speed = (_ctx != null)
                ? _ctx.Stats.MoveSpeed * p.moveSpeedMultiplier
                : baseMoveSpeed        * p.moveSpeedMultiplier;

            if (_animator != null) _animator.Play(StateRun);

            // TODO(실행 본체): 아래 직선 이동은 파이프라인 검증용 자리표시자.
            foreach (var cell in path)
            {
                CurrentCell = cell;
                Vector2 target = map.CellToWorld(cell.x, cell.y, origin);

                // 진행 방향으로 좌우 반전 (기본 스프라이트는 오른쪽을 봄)
                float dx = target.x - transform.position.x;
                if (_sprite != null && Mathf.Abs(dx) > 0.01f)
                    _sprite.flipX = dx < 0f;

                // 철벽 방패(FrontShieldOnly) 방향 판정용 — 진행 방향이 바뀔 때만 갱신
                if (_ctx != null && Mathf.Abs(dx) > 0.01f)
                    _ctx.FacingSign = dx < 0f ? -1 : 1;

                while (!IsDone && ((Vector2)transform.position - target).sqrMagnitude > 0.001f)
                {
                    transform.position = Vector2.MoveTowards(
                        transform.position, target, speed * Time.deltaTime);
                    yield return null;
                }

                if (IsDone) yield break; // 이동 중 사망(Kill)
            }

            ReachedGoal = true;
            IsDone      = true;
            if (_animator != null) _animator.Play(StateIdle);
        }

        /// <summary>
        /// 함정 피격 등으로 사망 처리. 진행 중인 FollowPath 를 중단시킵니다.
        /// TrapBase/Arrow/DropHammer 의 트리거 판정에서 호출됩니다.
        /// </summary>
        public void Kill()
        {
            if (IsDone) return;
            IsDone      = true;
            ReachedGoal = false;
            if (_animator != null) _animator.Play(StateDeath);
        }

        // ── IPoolable — ComponentPool 재사용 훅 ──────────────────────────────

        /// <summary>풀에서 스폰될 때 새 개체처럼 초기화 (Awake 는 재사용 시 안 불림).</summary>
        public void OnSpawned()
        {
            IsDone      = false;
            ReachedGoal = false;
            CurrentCell = default;

            if (_sprite   != null) _sprite.flipX = false;
            if (_animator != null) _animator.Play(StateIdle);

            // ComponentPool 은 풀링 대상 타입(VerificationAgent)의 IPoolable 만 호출하므로
            // 같은 개체의 AgentContext 훅은 여기서 명시적으로 전달(forwarding)한다.
            _ctx?.OnSpawned();
        }

        /// <summary>풀로 반납될 때 잔여 코루틴 정리 (비활성화로도 멎지만 명시적으로).</summary>
        public void OnDespawned()
        {
            StopAllCoroutines();
            _ctx?.OnDespawned();
        }
    }
}
