using System.Collections.Generic;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  AIMemory — 검증 AI 의 시도 간 학습 기록
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 한 번의 검증 페이즈 안에서 AI 가 겪은 사망 경험을 셀 단위로 축적하는 순수 데이터 클래스.
    /// <see cref="NavGrid.Build"/> 가 다음 시도의 노드 비용에 반영합니다
    /// (반영 강도는 성향 스텟 '학습력' → <see cref="AIBehaviorParams.memoryPenaltyWeight"/>).
    ///
    /// <para>수명: <see cref="VerificationDirector"/> 가 검증 1회(Run)마다 새로 생성.
    /// 스테이지를 넘어 기억을 유지하려면 이 객체를 밖에서 보존하도록 확장할 것.</para>
    /// </summary>
    public class AIMemory
    {
        // 셀 인덱스(y * width + x) → 해당 셀에서 죽은 횟수
        private readonly Dictionary<int, int> _deathCounts = new Dictionary<int, int>();
        private readonly int _width;

        public AIMemory(int mapWidth) => _width = mapWidth;

        /// <summary>죽은 횟수 총합 (디버그/HUD 표기용).</summary>
        public int TotalDeaths { get; private set; }

        /// <summary>사망 지점 기록. 같은 자리에서 반복 사망하면 페널티가 누적됩니다.</summary>
        public void RecordDeath(GridCoord cell)
        {
            int key = cell.y * _width + cell.x;
            _deathCounts.TryGetValue(key, out int n);
            _deathCounts[key] = n + 1;
            TotalDeaths++;
        }

        /// <summary>
        /// 해당 셀의 학습 페널티 비용.
        /// TODO(기획): 지금은 사망 횟수 × 가중치의 선형 누적. 주변 셀 확산(스플래시) 등은 미정.
        /// </summary>
        public float GetPenalty(int x, int y, float memoryPenaltyWeight)
        {
            _deathCounts.TryGetValue(y * _width + x, out int n);
            return n * memoryPenaltyWeight;
        }
    }
}
