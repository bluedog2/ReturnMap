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
    /// <para><b>로코모션 FSM(3단계)</b>: 여전히 물리 없는 키네마틱 웨이포인트 이동이지만,
    /// 셀 1칸 전진마다 <see cref="MovementTrait"/> 파이프라인(OnBeforeMove → 이동 →
    /// 착지 경직 → OnCellAdvanced)을 거친다. Trait 가 없는(태그 미배선) 개체는 파이프라인이
    /// 사실상 빈 배열이라 기존 직선 이동과 동일하게 동작한다.</para>
    ///
    /// <para>TODO(실행 본체):
    /// 1) 지금은 웨이포인트 직선/아크 이동(자리표시자) — 실제로는 Rigidbody2D 기반
    ///    플랫포머 이동(점프/낙하)으로 교체 검토. 점프/낙하 <b>링크</b>는 5단계에서
    ///    NavGrid·AStarPathPlanner 가 이미 생성하며(<see cref="TraversalProfile"/>),
    ///    이 클래스는 From→To 가 인접하지 않은 다중 셀 이동을 아크/직선으로 소비한다.
    /// 2) mistakeChance 로 점프 타이밍 오차 구현(미착수).</para>
    /// </summary>
    public class VerificationAgent : MonoBehaviour, IPoolable
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("이동 (자리표시자)")]
        [SerializeField]
        [Tooltip("기준 이동속도 (칸/초). 성향의 moveSpeedMultiplier 가 곱해진다. " +
                 "AgentContext 가 배선돼 있으면 ctx.Stats.MoveSpeed 로 대체된다.")]
        private float baseMoveSpeed = 4f;

        // ── 애니메이션 — Enemy_*.controller 의 상태 이름과 일치해야 함 ──────────

        private static readonly int StateIdle  = Animator.StringToHash("Idle");
        private static readonly int StateRun   = Animator.StringToHash("Run");
        private static readonly int StateDeath = Animator.StringToHash("Death");

        private Animator       _animator; // 없어도 동작 (선택 구성)
        private SpriteRenderer _sprite;   // 좌우 반전용
        private AgentContext   _ctx;      // 태그/스탯 조회 (null 허용 — 미배선 프리팹 하위 호환)

        // ── 로코모션 FSM 상태 ─────────────────────────────────────────────────

        /// <summary>로코모션 진행 상태 — 연출·디버그 구분용 (분기 대부분은 코루틴 흐름이 담당).</summary>
        private enum LocomotionState { Moving, Paused, SteppingBack, Arcing, Stunned, Dead }

        private LocomotionState _locomotionState = LocomotionState.Moving;

        // ── Trait 파이프라인 — 개체별 상태는 배열로 분리(플라이웨이트 SO 는 무상태) ──

        private readonly List<MovementTrait> _traitList   = new List<MovementTrait>(TagSet.MaxTagsPerAgent);
        private readonly TraitState[]        _traitStates = new TraitState[TagSet.MaxTagsPerAgent];

        private AIBehaviorParams _params;
        private float            _baseSpeed;

        /// <summary>백스텝 태그 훅 등 외부(피격)에서 요청한 후퇴 칸수. 다음 셀 루프에서 소비.</summary>
        private int _pendingStepBackCells;

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

        // ── 외부 요청 API ─────────────────────────────────────────────────────

        /// <summary>
        /// 함정 피격 등 <see cref="TagEventHook.OnTrapHit"/> 에서 호출. 물리 콜백 도중
        /// 코루틴을 직접 건드리지 않고 플래그만 세워 다음 셀 루프에서 안전하게 처리한다.
        /// 여러 번 요청되면 최댓값을 취한다(합성 규칙과 동일).
        /// </summary>
        public void RequestStepBack(int cells)
        {
            if (cells <= 0) return;
            _pendingStepBackCells = Mathf.Max(_pendingStepBackCells, cells);
        }

        // ── 실행 ──────────────────────────────────────────────────────────────

        /// <summary>
        /// 웨이포인트 경로를 순서대로 수행합니다. Director 가 yield 로 대기합니다.
        /// </summary>
        public IEnumerator FollowPath(IReadOnlyList<GridCoord> path, MapData map, Vector2 origin,
                                      AIBehaviorParams p)
        {
            IsDone                = false;
            ReachedGoal           = false;
            _pendingStepBackCells = 0;
            _params               = p;

            // ctx 가 있으면 태그가 접힌 스탯을 기준으로, 없으면 기존 baseMoveSpeed 로.
            // 어느 경로든 성향(personality)의 moveSpeedMultiplier 는 항상 곱해진다(개체별 성향 유지).
            _baseSpeed = (_ctx != null) ? _ctx.Stats.MoveSpeed : baseMoveSpeed;

            BuildTraitCache();

            if (path == null || path.Count == 0)
            {
                IsDone = true; // 계획 실패 방어 — Director 는 보통 빈 경로를 넘기지 않음
                yield break;
            }

            int committedIndex = 0;
            CurrentCell      = path[0];
            _locomotionState = LocomotionState.Moving;

            if (_animator != null) _animator.Play(StateRun);

            while (true)
            {
                if (IsDone) { _locomotionState = LocomotionState.Dead; yield break; }

                // ── 외부(피격) 백스텝 요청 우선 처리 ────────────────────────────
                if (_pendingStepBackCells > 0)
                {
                    int cells = _pendingStepBackCells;
                    _pendingStepBackCells = 0;

                    int newIndex = Mathf.Max(0, committedIndex - cells);
                    _locomotionState = LocomotionState.SteppingBack;
                    yield return WalkBackward(path, committedIndex, newIndex, map, origin);
                    if (IsDone) yield break;

                    committedIndex   = newIndex;
                    CurrentCell      = path[newIndex];
                    _locomotionState = LocomotionState.Moving;

                    // WalkBackward 가 0칸 이동(이미 index 0)으로 끝났을 수 있으므로,
                    // 무브먼트 없이 continue 만 반복해 프레임을 소비 안 하는 무한루프를 방지.
                    yield return null;
                    continue;
                }

                if (committedIndex >= path.Count - 1)
                    break; // 골 도달

                int       nextIndex = committedIndex + 1;
                GridCoord from      = path[committedIndex];
                GridCoord to        = path[nextIndex];
                var       q         = new MoveQuery(from, to, _ctx != null ? _ctx.FacingSign : 1);

                // ── OnBeforeMove 합성 (pause=최댓값, stepBack=최댓값, veto=OR) ──
                TraitAction before = ComposeBeforeMove(in q);

                if (before.pauseSeconds > 0f)
                {
                    _locomotionState = LocomotionState.Paused;
                    yield return PauseFor(before.pauseSeconds);
                    if (IsDone) yield break;
                }

                if (before.stepBackCells > 0)
                {
                    int newIndex = Mathf.Max(0, committedIndex - before.stepBackCells);
                    _locomotionState = LocomotionState.SteppingBack;
                    yield return WalkBackward(path, committedIndex, newIndex, map, origin);
                    if (IsDone) yield break;

                    committedIndex   = newIndex;
                    CurrentCell      = path[newIndex];
                    _locomotionState = LocomotionState.Moving;

                    // WalkBackward 가 0칸 이동(이미 index 0)으로 끝났을 수 있으므로,
                    // 무브먼트 없이 continue 만 반복해 프레임을 소비 안 하는 무한루프를 방지.
                    yield return null;
                    continue; // 이번 이동 예정지는 무효 — 다음 루프에서 새 목표로 재평가
                }

                if (before.vetoMove)
                {
                    _locomotionState = LocomotionState.Moving;
                    // 정지·후퇴 없이 순수 veto 만 온 경우도 프레임을 반드시 소비한다
                    // (안전제일은 stepBack 과 함께 오므로 위에서 이미 처리되고 여기 도달하지 않음).
                    yield return null;
                    continue; // 이동 스킵
                }

                // ── 실제 이동 ──────────────────────────────────────────────────
                Vector2 target = map.CellToWorld(to.x, to.y, origin);
                UpdateFacing(target);

                // 상승(점프) 또는 가로 2칸 이상 이동(다중 셀 링크)이면 아크로 연출한다.
                // 아크 높이는 상승 칸수에 비례 — 계단 오르듯 낮게, 높이 점프는 크게.
                ArcSpec arc = new ArcSpec
                {
                    height          = 0.5f + q.AscendCells * 0.3f,
                    horizontalCells = q.HorizontalCells,
                    useArc          = q.IsAscending || q.HorizontalCells >= 2,
                };
                for (int i = 0; i < _traitList.Count; i++)
                    _traitList[i].ModifyArc(ref arc);

                _locomotionState = arc.useArc ? LocomotionState.Arcing : LocomotionState.Moving;

                if (arc.useArc) yield return MoveArc(target, q, arc);
                else            yield return MoveStraight(target, q);

                if (IsDone) yield break; // 이동 중 사망

                committedIndex = nextIndex;
                CurrentCell    = to;

                // ── 착지 경직 (하강 2칸 이상, 유연함 태그 면제) ─────────────────
                bool immuneToStun = _ctx != null && _ctx.HasFlag(SpecialFlag.NoLandingStun);
                if (q.DropHeight >= 2 && !immuneToStun)
                {
                    _locomotionState = LocomotionState.Stunned;
                    yield return PauseFor(1f);
                    if (IsDone) yield break;
                }

                // ── OnCellAdvanced 합성 ──────────────────────────────────────────
                TraitAction after = ComposeCellAdvanced();

                if (after.pauseSeconds > 0f)
                {
                    _locomotionState = LocomotionState.Paused;
                    yield return PauseFor(after.pauseSeconds);
                    if (IsDone) yield break;
                }

                if (after.stepBackCells > 0)
                {
                    int newIndex = Mathf.Max(0, committedIndex - after.stepBackCells);
                    _locomotionState = LocomotionState.SteppingBack;
                    yield return WalkBackward(path, committedIndex, newIndex, map, origin);
                    if (IsDone) yield break;

                    committedIndex = newIndex;
                    CurrentCell    = path[newIndex];
                }

                _locomotionState = LocomotionState.Moving;
            }

            ReachedGoal      = true;
            IsDone           = true;
            _locomotionState = LocomotionState.Moving;
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

        // ── Trait 파이프라인 내부 ────────────────────────────────────────────

        /// <summary>
        /// ctx 의 태그 중 movementTrait 가 있는 것만 캐시한다. FollowPath 시작 시(스폰당)
        /// 1회만 호출 — 프레임 루프에서는 재사용만 한다.
        /// </summary>
        private void BuildTraitCache()
        {
            _traitList.Clear();

            if (_ctx != null)
            {
                var tags = _ctx.Tags.Tags;
                for (int i = 0; i < tags.Count; i++)
                {
                    MovementTrait trait = tags[i]?.MovementTrait;
                    if (trait != null) _traitList.Add(trait);
                }
            }

            for (int i = 0; i < _traitStates.Length; i++)
                _traitStates[i] = default;
        }

        /// <summary>합성 규칙: pause=최댓값, stepBack=최댓값, veto=OR.</summary>
        private TraitAction ComposeBeforeMove(in MoveQuery q)
        {
            TraitAction combined = TraitAction.None;
            for (int i = 0; i < _traitList.Count; i++)
            {
                TraitAction a = _traitList[i].OnBeforeMove(ref _traitStates[i], in q);
                combined.pauseSeconds  = Mathf.Max(combined.pauseSeconds, a.pauseSeconds);
                combined.stepBackCells = Mathf.Max(combined.stepBackCells, a.stepBackCells);
                combined.vetoMove      = combined.vetoMove || a.vetoMove;
            }
            return combined;
        }

        /// <summary>합성 규칙: pause=최댓값, stepBack=최댓값, veto=OR (동일 규칙).</summary>
        private TraitAction ComposeCellAdvanced()
        {
            TraitAction combined = TraitAction.None;
            for (int i = 0; i < _traitList.Count; i++)
            {
                TraitAction a = _traitList[i].OnCellAdvanced(ref _traitStates[i]);
                combined.pauseSeconds  = Mathf.Max(combined.pauseSeconds, a.pauseSeconds);
                combined.stepBackCells = Mathf.Max(combined.stepBackCells, a.stepBackCells);
                combined.vetoMove      = combined.vetoMove || a.vetoMove;
            }
            return combined;
        }

        /// <summary>
        /// 이번 프레임 이동 속도. ctx.Stats.MoveSpeed × p.moveSpeedMultiplier × Π(Trait 배율).
        /// 하강 이동 + Glide 플래그(낙하산) 보유 시 추가로 ×0.3.
        /// 매 프레임 호출되므로 할당 없이 for 루프만 사용한다.
        /// </summary>
        private float ComputeFrameSpeed(in MoveQuery q)
        {
            float speed = _baseSpeed * _params.moveSpeedMultiplier;
            for (int i = 0; i < _traitList.Count; i++)
                speed *= _traitList[i].GetSpeedMultiplier(ref _traitStates[i], in q);

            if (q.IsDescending && _ctx != null && _ctx.HasFlag(SpecialFlag.Glide))
                speed *= 0.3f;

            return speed;
        }

        // ── 이동 세그먼트 ─────────────────────────────────────────────────────

        private void UpdateFacing(Vector2 target)
        {
            float dx = target.x - transform.position.x;
            if (Mathf.Abs(dx) <= 0.01f) return;

            if (_sprite != null) _sprite.flipX = dx < 0f;
            if (_ctx    != null) _ctx.FacingSign = dx < 0f ? -1 : 1;
        }

        private IEnumerator MoveStraight(Vector2 target, MoveQuery q)
        {
            while (!IsDone && ((Vector2)transform.position - target).sqrMagnitude > 0.0001f)
            {
                float speed = ComputeFrameSpeed(in q);
                transform.position = Vector2.MoveTowards(transform.position, target, speed * Time.deltaTime);
                yield return null;
            }
        }

        /// <summary>
        /// 연출용 포물선 이동(물리 없음). 시작→끝 lerp 위치에 sin 높이를 얹는다.
        /// 진행도(t)는 (누적 이동거리 / 전체 거리)로 계산해 Trait 의 GetSpeedMultiplier
        /// (가속 등 시간 가변 배율)가 직선 이동과 동일하게 반영되도록 한다.
        /// <para><see cref="ArcSpec.horizontalCells"/> 는 현재 연출에 소비되지 않는
        /// 메타데이터(점프 링크 도입 5단계에서 다중 셀 도약에 사용 예정)이다.</para>
        /// </summary>
        private IEnumerator MoveArc(Vector2 target, MoveQuery q, ArcSpec arc)
        {
            Vector2 start = transform.position;
            float   dist  = Vector2.Distance(start, target);
            if (dist < 0.0001f) yield break;

            float traveled = 0f;
            while (!IsDone && traveled < dist)
            {
                traveled += ComputeFrameSpeed(in q) * Time.deltaTime;
                float t = Mathf.Clamp01(traveled / dist);

                Vector2 pos = Vector2.Lerp(start, target, t);
                pos.y += arc.height * Mathf.Sin(t * Mathf.PI);
                transform.position = pos;

                yield return null;
            }

            if (!IsDone) transform.position = target; // 보간 오차 보정 — 정확히 착지
        }

        // ── 되돌아가기 (걸어서, 아크 없음) ────────────────────────────────────

        /// <summary>
        /// path[fromIndex] 위치에서 path[toIndex] 위치까지 경로 역방향으로 한 칸씩 걸어서
        /// 되돌아간다. toIndex &lt; fromIndex 가정.
        /// </summary>
        private IEnumerator WalkBackward(IReadOnlyList<GridCoord> path, int fromIndex, int toIndex,
                                          MapData map, Vector2 origin)
        {
            for (int i = fromIndex - 1; i >= toIndex; i--)
            {
                if (IsDone) yield break;

                GridCoord cur  = path[i + 1];
                GridCoord dest = path[i];
                Vector2   target = map.CellToWorld(dest.x, dest.y, origin);
                UpdateFacing(target);

                var q = new MoveQuery(cur, dest, _ctx != null ? _ctx.FacingSign : -1);
                yield return MoveStraight(target, q);
                if (IsDone) yield break;
            }
        }

        // ── 정지 대기 ─────────────────────────────────────────────────────────

        private IEnumerator PauseFor(float seconds)
        {
            if (_animator != null) _animator.Play(StateIdle);

            float t = 0f;
            while (t < seconds && !IsDone)
            {
                t += Time.deltaTime;
                yield return null;
            }

            if (!IsDone && _animator != null) _animator.Play(StateRun);
        }

        // ── IPoolable — ComponentPool 재사용 훅 ──────────────────────────────

        /// <summary>풀에서 스폰될 때 새 개체처럼 초기화 (Awake 는 재사용 시 안 불림).</summary>
        public void OnSpawned()
        {
            IsDone                = false;
            ReachedGoal           = false;
            CurrentCell           = default;
            _pendingStepBackCells = 0;
            _locomotionState      = LocomotionState.Moving;

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
