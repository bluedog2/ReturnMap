using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  MovementTrait — 이동 태그 런타임 로직 (플라이웨이트 SO)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 개체 1명의 Trait 별 런타임 상태. SO(<see cref="MovementTrait"/>)는 무상태
    /// 플라이웨이트이므로, 개체마다 달라지는 값은 전부 이 구조체에 담아 개체 배열로
    /// 별도 관리한다 (<see cref="VerificationAgent"/> 가 소유).
    /// <para>필드 용도는 Trait 마다 자체적으로 정의해 사용한다 — 각 Trait 구현부
    /// 주석에 실제 용도를 문서화할 것.</para>
    /// </summary>
    public struct TraitState
    {
        /// <summary>범용 누적 카운터 (예: 전진 칸 수, 마지막으로 반응한 셀 좌표 등).</summary>
        public int counter;

        /// <summary>범용 누적 타이머 (초).</summary>
        public float timer;

        /// <summary>범용 보조 상태값 (예: 마지막으로 반응한 위협의 셀 좌표).</summary>
        public int phase;
    }

    /// <summary>
    /// Trait 가 로코모션(<see cref="VerificationAgent.FollowPath"/>)에 요청하는 특수 행동.
    /// <para><b>합성 규칙</b>(여러 Trait 의 결과를 하나로 접을 때): 속도 배율은 곱연산
    /// 누적, <see cref="pauseSeconds"/> 는 최댓값, <see cref="stepBackCells"/> 는
    /// 최댓값, <see cref="vetoMove"/> 는 OR.</para>
    /// </summary>
    public struct TraitAction
    {
        /// <summary>0 초과면 그 시간(초)만큼 제자리 정지.</summary>
        public float pauseSeconds;

        /// <summary>0 초과면 경로 역방향으로 이 칸수만큼 후퇴(걸어서).</summary>
        public int stepBackCells;

        /// <summary>true 면 이번 이동을 거부(스킵). stepBackCells 와 조합해 반전 연출.</summary>
        public bool vetoMove;

        /// <summary>아무 행동도 요청하지 않음 (기본값과 동일하지만 의도 표현용).</summary>
        public static readonly TraitAction None = default;
    }

    /// <summary>
    /// 이동 판단 1건에 필요한 읽기 전용 정보 묶음 (현재 셀 → 다음 셀).
    /// </summary>
    public readonly struct MoveQuery
    {
        /// <summary>이동 시작 셀.</summary>
        public readonly GridCoord From;

        /// <summary>이동 목표 셀.</summary>
        public readonly GridCoord To;

        /// <summary>진행 방향 (+1 = 오른쪽, -1 = 왼쪽).</summary>
        public readonly int DirX;

        /// <summary>To.y &gt; From.y (상승 이동)인가.</summary>
        public readonly bool IsAscending;

        /// <summary>To.y &lt; From.y (하강 이동)인가.</summary>
        public readonly bool IsDescending;

        /// <summary>하강 칸 수 (하강이 아니면 0).</summary>
        public readonly int DropHeight;

        /// <summary>상승 칸 수 (상승이 아니면 0). 점프 링크의 아크 높이 산출에 사용.</summary>
        public readonly int AscendCells;

        /// <summary>가로 이동 칸수 (|To.x - From.x|). 다중 셀 점프/낙하 링크에서 1보다 클 수 있다.</summary>
        public readonly int HorizontalCells;

        /// <summary>
        /// From→To 이동 1건을 표현합니다. <paramref name="fallbackDirX"/> 는 수평 변위가
        /// 없는 순수 수직 이동(제자리 상승/하강)일 때 DirX 를 대신 채울 현재 진행 방향
        /// (보통 에이전트의 FacingSign)입니다.
        /// <para>A* 가 링크(점프/낙하) 기반으로 확장하므로 From→To 가 인접하지 않은
        /// 다중 칸 이동일 수 있다 — DropHeight/AscendCells/HorizontalCells 모두 이를 반영한다.</para>
        /// </summary>
        public MoveQuery(GridCoord from, GridCoord to, int fallbackDirX)
        {
            From = from;
            To   = to;

            int dx = to.x - from.x;
            DirX = dx != 0 ? (dx > 0 ? 1 : -1) : (fallbackDirX >= 0 ? 1 : -1);

            IsAscending     = to.y > from.y;
            IsDescending    = to.y < from.y;
            DropHeight      = IsDescending ? (from.y - to.y) : 0;
            AscendCells     = IsAscending  ? (to.y - from.y) : 0;
            HorizontalCells = Mathf.Abs(dx);
        }
    }

    /// <summary>점프/호핑 아크(연출용 포물선) 파라미터. 물리 없음 — transform 보간 전용.</summary>
    public struct ArcSpec
    {
        /// <summary>포물선 최고 높이(유닛).</summary>
        public float height;

        /// <summary>가로로 몇 칸 규모의 도약처럼 보이게 할지 (현재는 연출 메타데이터 —
        /// 실제 다중 셀 도약은 점프 링크가 도입되는 5단계에서 소비할 예정).</summary>
        public int horizontalCells;

        /// <summary>true 면 직선 이동 대신 포물선 보간으로 이동.</summary>
        public bool useArc;
    }

    /// <summary>
    /// 이동 태그의 런타임 로직 SO(플라이웨이트). 로직은 SO 에 두고 개체별 상태는
    /// <see cref="TraitState"/> 로 분리해 개체당 힙 할당을 없앤다.
    /// <para><b>주의</b>: 이 클래스의 모든 메서드는 <see cref="VerificationAgent"/> 의
    /// 프레임 루프/셀 루프에서 반복 호출되므로 내부에서 할당(new)·LINQ·GetComponent 를
    /// 하지 않는다. 맵/함정 조회가 필요하면 <see cref="WorldToCell"/> 처럼 정적 유틸을
    /// 통해 <see cref="MapLoader.Instance"/> 를 직접 참조한다.</para>
    /// </summary>
    public abstract class MovementTrait : ScriptableObject
    {
        /// <summary>셀 1칸 전진을 완료할 때마다 호출. 반환으로 특수 행동 요청.</summary>
        public virtual TraitAction OnCellAdvanced(ref TraitState s) => TraitAction.None;

        /// <summary>다음 셀로 이동을 시작하기 전에 호출. 위험 감지·정지 요청 지점.</summary>
        public virtual TraitAction OnBeforeMove(ref TraitState s, in MoveQuery q) => TraitAction.None;

        /// <summary>이번 프레임 이동 속도에 곱할 배율 (기본 1). 매 프레임 호출되므로 할당 금지.</summary>
        public virtual float GetSpeedMultiplier(ref TraitState s, in MoveQuery q) => 1f;

        /// <summary>점프/호핑 아크 파라미터 수정 (높이뛰기·멀리뛰기·천진난만).</summary>
        public virtual void ModifyArc(ref ArcSpec arc) { }

        /// <summary>
        /// <see cref="TraversalProfile"/> 파생 시 점프 스펙을 질의한다. 여러 Trait 가 동시에
        /// 부여되면(이론상 배타 그룹으로 대부분 막히지만) 각자 Mathf.Max 로 상향 조정하는
        /// 방식으로 합성한다. 기본 구현은 아무것도 하지 않음(점프 스펙에 영향 없는 태그).
        /// </summary>
        public virtual void ModifyTraversal(ref int maxJumpHeight, ref int maxJumpDistance) { }

        // ── 공용 유틸 — 월드 좌표 → 셀 좌표 ──────────────────────────────────

        /// <summary>
        /// 월드 좌표를 현재 로드된 맵의 셀 좌표로 환산합니다. 맵이 없으면 (0,0).
        /// <see cref="MapData.CellToWorld"/> 의 역산(셀 중심 = origin + (x+0.5)*tileUnit)이며,
        /// MapData 에 전용 WorldToCell API 가 없으므로 여기서 지역적으로 계산한다.
        /// </summary>
        protected static GridCoord WorldToCell(Vector2 worldPos)
        {
            MapLoader loader = MapLoader.Instance;
            MapData   map    = loader != null ? loader.CurrentMap : null;
            if (map == null) return default;

            Vector2 origin   = loader.MapOrigin;
            float   tileUnit = Mathf.Max(0.0001f, map.tileUnit);

            int cx = Mathf.FloorToInt((worldPos.x - origin.x) / tileUnit);
            int cy = Mathf.FloorToInt((worldPos.y - origin.y) / tileUnit);
            return new GridCoord(cx, cy);
        }
    }
}
