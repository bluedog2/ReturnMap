using System.Collections.Generic;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  VerificationReport — 검증 1회의 결과 리포트 (순수 C#)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 검증 1회의 집계: 돌파/라이프/투입/저지 수와 함정별 처치 수·사망 지점별 횟수.
    /// <para>「처치」 = 함정에 의한 사망 횟수(재시도마다 1회). 도달·시간 초과로 끝난 시도는
    /// 처치로 세지 않는다(<see cref="TryRecordDeath"/>).</para>
    /// </summary>
    public sealed class VerificationReport
    {
        /// <summary>상위 목록에서 6위부터 묶는 행의 라벨.</summary>
        public const string OthersLabel = "기타";

        /// <summary>집계 행 1건 (라벨, 횟수).</summary>
        public readonly struct Row
        {
            public readonly string Label;
            public readonly int Count;

            public Row(string label, int count)
            {
                Label = label;
                Count = count;
            }
        }

        /// <summary>방어 실패(뚫림) 여부.</summary>
        public bool Breached;

        /// <summary>골에 도달한 개체 수.</summary>
        public int Reached;

        /// <summary>허용 돌파 수(라이프).</summary>
        public int Life;

        /// <summary>투입된 개체 수.</summary>
        public int Spawned;

        /// <summary>영구 사망(저지)한 개체 수.</summary>
        public int StoppedAgents;

        /// <summary>함정에 의한 사망 횟수 (= 처치).</summary>
        public int DeathEvents { get; private set; }

        /// <summary>시간 초과로 끝나 처치로 세지 않은 시도 수.</summary>
        public int TimedOutAttempts { get; private set; }

        private readonly Dictionary<string, int> _killsByTrap = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _deathsByCell = new Dictionary<string, int>();

        /// <summary>
        /// 시도 1건의 종료를 기록한다. 도달했거나 시간 초과면 처치로 세지 않고 false,
        /// 함정 사망이면 라벨별·셀별로 집계하고 true.
        /// </summary>
        public bool TryRecordDeath(bool reached, bool timedOut, string trapLabel, string cellLabel)
        {
            if (reached) return false;
            if (timedOut)
            {
                TimedOutAttempts++;
                return false;
            }

            DeathEvents++;
            Increment(_killsByTrap, trapLabel);
            Increment(_deathsByCell, cellLabel);
            return true;
        }

        /// <summary>함정 라벨별 처치 상위 <paramref name="n"/> 행. 초과분은 「기타」 한 행으로 합친다.</summary>
        public List<Row> TopKills(int n) => Top(_killsByTrap, n);

        /// <summary>사망 셀별 상위 <paramref name="n"/> 행. 초과분은 「기타」 한 행으로 합친다.</summary>
        public List<Row> TopCells(int n) => Top(_deathsByCell, n);

        /// <summary>모든 집계를 초기화한다.</summary>
        public void Reset()
        {
            Breached         = false;
            Reached          = 0;
            Life             = 0;
            Spawned          = 0;
            StoppedAgents    = 0;
            DeathEvents      = 0;
            TimedOutAttempts = 0;
            _killsByTrap.Clear();
            _deathsByCell.Clear();
        }

        private static void Increment(Dictionary<string, int> map, string key)
        {
            if (string.IsNullOrEmpty(key)) key = OthersLabel;
            map.TryGetValue(key, out int v);
            map[key] = v + 1;
        }

        private static List<Row> Top(Dictionary<string, int> map, int n)
        {
            var rows = new List<Row>(map.Count);
            foreach (var kv in map) rows.Add(new Row(kv.Key, kv.Value));

            // 횟수 내림차순, 같으면 라벨 순 — 항상 같은 순서가 나오도록
            rows.Sort((a, b) =>
            {
                int c = b.Count.CompareTo(a.Count);
                return c != 0 ? c : string.CompareOrdinal(a.Label, b.Label);
            });

            if (n < 0) n = 0;
            if (rows.Count <= n) return rows;

            int rest = 0;
            for (int i = n; i < rows.Count; i++) rest += rows[i].Count;
            rows.RemoveRange(n, rows.Count - n);
            rows.Add(new Row(OthersLabel, rest));
            return rows;
        }
    }
}
