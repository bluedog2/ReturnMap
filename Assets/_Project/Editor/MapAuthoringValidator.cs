using System.Collections.Generic;
using UnityEngine;

namespace ReTrap.EditorTools
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  MapAuthoringValidator — 맵 제작 콘텐츠 검증 (에디터 저장 시)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <see cref="MapData.Validate"/> (데이터 무결성)와 별개로,
    /// "플레이 가능한 맵인가"를 검사하는 제작 단계 검증입니다.
    ///
    /// <para>검사 항목 — 전부 <b>경고</b>이며 저장을 막지 않습니다
    /// (의도적인 특수 맵을 허용하기 위해 확인 후 저장 가능):</para>
    /// <list type="number">
    ///   <item>스폰/골 지점이 솔리드 타일 안에 묻힘</item>
    ///   <item>스폰 지점 아래에 바닥 없음 (시작하자마자 추락)</item>
    ///   <item>맵 테두리 뚫림 (플레이어 이탈 가능)</item>
    ///   <item>슬롯이 지형 면에 붙어있지 않음 (anchor 방향 이웃이 솔리드 아님)</item>
    ///   <item>스폰 → 골 연결성 없음 (빈 칸 기준 — 근사 검사)</item>
    /// </list>
    /// </summary>
    public static class MapAuthoringValidator
    {
        /// <summary>경고 목록 반환. 비어있으면 통과.</summary>
        public static List<string> Validate(MapData map)
        {
            var warnings = new List<string>();
            if (map == null || map.grid == null) return warnings;

            CheckSpawnGoal(map, warnings);
            CheckBorder(map, warnings);
            CheckSlotAttachment(map, warnings);
            CheckReachability(map, warnings);

            return warnings;
        }

        // ── 1·2. 스폰 / 골 ───────────────────────────────────────────────────

        private static void CheckSpawnGoal(MapData map, List<string> w)
        {
            var s = map.spawnPoint;
            var g = map.goalPoint;

            if (IsSolid(map, s.x, s.y))
                w.Add($"스폰 {s} 이 솔리드 타일 안에 있습니다");
            if (IsSolid(map, g.x, g.y))
                w.Add($"골 {g} 이 솔리드 타일 안에 있습니다");

            // 스폰 아래로 내려가며 바닥 탐색
            if (!IsSolid(map, s.x, s.y))
            {
                bool hasFloor = false;
                for (int y = s.y - 1; y >= 0; y--)
                {
                    if (IsSolid(map, s.x, y)) { hasFloor = true; break; }
                }
                if (!hasFloor)
                    w.Add($"스폰 {s} 아래에 바닥이 없습니다 — 시작하자마자 추락");
            }
        }

        // ── 3. 테두리 ─────────────────────────────────────────────────────────

        private static void CheckBorder(MapData map, List<string> w)
        {
            int open = 0;
            for (int x = 0; x < map.width; x++)
            {
                if (!IsSolid(map, x, 0))              open++;
                if (!IsSolid(map, x, map.height - 1)) open++;
            }
            for (int y = 1; y < map.height - 1; y++)
            {
                if (!IsSolid(map, 0, y))              open++;
                if (!IsSolid(map, map.width - 1, y))  open++;
            }

            if (open > 0)
                w.Add($"맵 테두리에 뚫린 칸 {open}개 — 플레이어가 맵 밖으로 이탈할 수 있습니다");
        }

        // ── 4. 슬롯 부착 ─────────────────────────────────────────────────────

        private static void CheckSlotAttachment(MapData map, List<string> w)
        {
            foreach (var slot in map.trapSlots)
            {
                // 슬롯 칸 자체가 타일에 묻혀있으면 함정 설치 불가
                if (IsSolid(map, slot.x, slot.y))
                {
                    w.Add($"슬롯 ({slot.x},{slot.y}) 이 솔리드 타일 안에 있습니다");
                    continue;
                }

                // anchor 방향의 이웃이 솔리드여야 부착 가능
                (int nx, int ny) = slot.Anchor switch
                {
                    TrapAnchor.Floor     => (slot.x, slot.y - 1),
                    TrapAnchor.Ceiling   => (slot.x, slot.y + 1),
                    TrapAnchor.LeftWall  => (slot.x - 1, slot.y),
                    TrapAnchor.RightWall => (slot.x + 1, slot.y),
                    _                    => (slot.x, slot.y),
                };

                if (!IsSolid(map, nx, ny))
                    w.Add($"슬롯 ({slot.x},{slot.y}) [{slot.Anchor}] 의 부착면 ({nx},{ny}) 이 " +
                          "솔리드 타일이 아닙니다 — 함정이 허공에 붙게 됩니다");
            }
        }

        // ── 5. 연결성 (근사) ─────────────────────────────────────────────────

        /// <summary>
        /// 스폰 → 골 빈 칸 4방향 연결성 검사.
        /// 점프 높이를 고려하지 않는 근사 — "완전히 벽으로 막힌" 경우만 잡습니다.
        /// (정밀 검사는 Verification Phase 의 A* 가 담당)
        /// </summary>
        private static void CheckReachability(MapData map, List<string> w)
        {
            var s = map.spawnPoint;
            var g = map.goalPoint;

            if (IsSolid(map, s.x, s.y) || IsSolid(map, g.x, g.y))
                return; // 1번 경고가 이미 커버

            var visited = new HashSet<int>();
            var queue   = new Queue<(int, int)>();
            queue.Enqueue((s.x, s.y));
            visited.Add(map.Index(s.x, s.y));

            int[] dx = { 0, 0, -1, 1 };
            int[] dy = { 1, -1, 0, 0 };

            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();
                if (x == g.x && y == g.y) return; // 도달 가능

                for (int i = 0; i < 4; i++)
                {
                    int nx = x + dx[i], ny = y + dy[i];
                    if (!map.InBounds(nx, ny)) continue;
                    if (IsSolid(map, nx, ny)) continue;
                    int idx = map.Index(nx, ny);
                    if (!visited.Add(idx)) continue;
                    queue.Enqueue((nx, ny));
                }
            }

            w.Add($"스폰 {s} 에서 골 {g} 까지 빈 칸 경로가 없습니다 — 클리어 불가능한 맵");
        }

        // ── 유틸 ─────────────────────────────────────────────────────────────

        private static bool IsSolid(MapData map, int x, int y)
            => map.GetTile(x, y) != TileType.Empty;
    }
}
