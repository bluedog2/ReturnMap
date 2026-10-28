using System.Collections.Generic;
using UnityEditor;
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
    /// <para><b>지형 규칙</b>: 함정 슬롯 칸은 Play 페이즈에서 항상 솔리드가 됩니다
    /// (함정 설치 시 함정이 타일 역할, 미설치 시 봉인 타일로 막힘).
    /// 따라서 모든 검사에서 슬롯 칸을 지형으로 취급합니다.</para>
    ///
    /// <para>검사 항목 — 전부 <b>경고</b>이며 저장을 막지 않습니다
    /// (의도적인 특수 맵을 허용하기 위해 확인 후 저장 가능):</para>
    /// <list type="number">
    ///   <item>스폰/골 지점이 지형(타일·슬롯) 안에 묻힘</item>
    ///   <item>스폰 지점 아래에 바닥 없음 (시작하자마자 추락)</item>
    ///   <item>맵 테두리 뚫림 (플레이어 이탈 가능)</item>
    ///   <item>슬롯이 솔리드 타일과 같은 칸에 겹침 (지형 중복)</item>
    ///   <item>스폰 → 골 연결성 없음 (빈 칸 기준 — 근사 검사)</item>
    ///   <item>통행 높이 2칸 미만인 바닥 칸 (주인공 콜라이더 1.875칸)</item>
    ///   <item>허용 함정 이름 미존재·중복 및 슬롯 앵커별 호환 함정 부재</item>
    /// </list>
    /// </summary>
    public static class MapAuthoringValidator
    {
        /// <summary>경고 목록 반환. 비어있으면 통과.</summary>
        public static List<string> Validate(MapData map)
        {
            var warnings = new List<string>();
            if (map == null || map.grid == null) return warnings;

            // 슬롯 좌표 인덱스 사전 구축 (BFS 등에서 O(1) 조회)
            var slotCells = new HashSet<int>();
            foreach (var s in map.trapSlots)
                if (map.InBounds(s.x, s.y))
                    slotCells.Add(map.Index(s.x, s.y));

            CheckSpawnGoal(map, slotCells, warnings);
            CheckBorder(map, slotCells, warnings);
            CheckSlotBuried(map, warnings);
            CheckReachability(map, slotCells, warnings);
            CheckHeadroom(map, slotCells, warnings);
            CheckAllowedTraps(map, warnings);

            return warnings;
        }

        // ── 1·2. 스폰 / 골 ───────────────────────────────────────────────────

        private static void CheckSpawnGoal(MapData map, HashSet<int> slots, List<string> w)
        {
            var s = map.spawnPoint;
            var g = map.goalPoint;

            if (IsTerrain(map, slots, s.x, s.y))
                w.Add($"스폰 {s} 이 지형(타일·슬롯) 안에 있습니다");
            if (IsTerrain(map, slots, g.x, g.y))
                w.Add($"골 {g} 이 지형(타일·슬롯) 안에 있습니다");

            // 스폰 아래로 내려가며 바닥 탐색 (슬롯 칸도 Play 중 솔리드이므로 바닥 인정)
            if (!IsTerrain(map, slots, s.x, s.y))
            {
                if (!HasFloorBelow(map, slots, s.x, s.y))
                    w.Add($"스폰 {s} 아래에 바닥이 없습니다 — 시작하자마자 추락");
            }

            // 골도 동일 검사 — 골이 착지 불가능한 공중이면 검증 AI 의 A* 가
            // 시작/골 스냅(TrySnapToGround)에 실패해 경로가 영영 없고,
            // 그 맵은 항상 '방어 성공'으로 오판된다 (조용한 저작 실수 조기 차단).
            if (!IsTerrain(map, slots, g.x, g.y))
            {
                if (!HasFloorBelow(map, slots, g.x, g.y))
                    w.Add($"골 {g} 아래에 바닥이 없습니다 — 검증 AI 가 도달 판정을 받을 수 없어 항상 방어 성공이 됩니다");
            }
        }

        /// <summary>해당 칸에서 아래로 내려가며 지형(타일·슬롯)이 있는지 탐색.</summary>
        private static bool HasFloorBelow(MapData map, HashSet<int> slots, int x, int startY)
        {
            for (int y = startY - 1; y >= 0; y--)
                if (IsTerrain(map, slots, x, y)) return true;
            return false;
        }

        // ── 3. 테두리 ─────────────────────────────────────────────────────────

        private static void CheckBorder(MapData map, HashSet<int> slots, List<string> w)
        {
            int open = 0;
            for (int x = 0; x < map.width; x++)
            {
                if (!IsTerrain(map, slots, x, 0))              open++;
                if (!IsTerrain(map, slots, x, map.height - 1)) open++;
            }
            for (int y = 1; y < map.height - 1; y++)
            {
                if (!IsTerrain(map, slots, 0, y))              open++;
                if (!IsTerrain(map, slots, map.width - 1, y))  open++;
            }

            if (open > 0)
                w.Add($"맵 테두리에 뚫린 칸 {open}개 — 플레이어가 맵 밖으로 이탈할 수 있습니다");
        }

        // ── 4. 슬롯·타일 중복 ────────────────────────────────────────────────

        /// <summary>
        /// 슬롯 칸은 그 자체가 지형이므로 부착면 검사는 하지 않습니다.
        /// 솔리드 타일과 같은 칸에 겹친 경우만 제작 실수로 경고합니다.
        /// </summary>
        private static void CheckSlotBuried(MapData map, List<string> w)
        {
            foreach (var slot in map.trapSlots)
            {
                if (IsSolidTile(map, slot.x, slot.y))
                    w.Add($"슬롯 ({slot.x},{slot.y}) 이 솔리드 타일과 같은 칸에 있습니다 — 지형 중복");
            }
        }

        // ── 5. 연결성 (근사) ─────────────────────────────────────────────────

        /// <summary>
        /// 스폰 → 골 빈 칸 4방향 연결성 검사.
        /// 점프 높이를 고려하지 않는 근사 — "완전히 벽으로 막힌" 경우만 잡습니다.
        /// (정밀 검사는 Verification Phase 의 A* 가 담당)
        /// </summary>
        private static void CheckReachability(MapData map, HashSet<int> slots, List<string> w)
        {
            var s = map.spawnPoint;
            var g = map.goalPoint;

            if (IsTerrain(map, slots, s.x, s.y) || IsTerrain(map, slots, g.x, g.y))
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
                    if (IsTerrain(map, slots, nx, ny)) continue;
                    int idx = map.Index(nx, ny);
                    if (!visited.Add(idx)) continue;
                    queue.Enqueue((nx, ny));
                }
            }

            w.Add($"스폰 {s} 에서 골 {g} 까지 빈 칸 경로가 없습니다 — 클리어 불가능한 맵");
        }

        // ── 6. 통행 높이 ─────────────────────────────────────────────────────

        /// <summary>주인공 콜라이더 키 1.875칸(화풍 v2) → 서서 지나가려면 빈 칸 2칸 이상.</summary>
        private const int MinHeadroom = 2;

        /// <summary>
        /// 설 수 있는 칸(빈 칸 + 바로 아래 지형) 위로 빈 칸이 <see cref="MinHeadroom"/> 미만이면
        /// 주인공이 들어갈 수 없는 칸입니다. 의도된 막다른 틈일 수 있으므로 위치만 알립니다.
        /// </summary>
        private static void CheckHeadroom(MapData map, HashSet<int> slots, List<string> w)
        {
            var low = new List<string>();
            for (int y = 1; y < map.height; y++)
            for (int x = 0; x < map.width; x++)
            {
                if (IsTerrain(map, slots, x, y) || !IsTerrain(map, slots, x, y - 1)) continue;
                int room = 0;
                for (int yy = y; yy < map.height && room < MinHeadroom && !IsTerrain(map, slots, x, yy); yy++) room++;
                if (room < MinHeadroom) low.Add($"({x},{y})");
            }

            if (low.Count > 0)
                w.Add($"통행 높이 {MinHeadroom}칸 미만인 바닥 칸 {low.Count}개 — 주인공(키 1.875칸)이 서서 지나갈 수 없습니다: " +
                      string.Join(" ", low.GetRange(0, System.Math.Min(8, low.Count))) + (low.Count > 8 ? " …" : ""));
        }

        // ── 7. 허용 함정 ─────────────────────────────────────────────────────

        /// <summary>허용 이름의 유효성과 맵에 있는 앵커별 설치 가능한 함정을 검사합니다.</summary>
        private static void CheckAllowedTraps(MapData map, List<string> w)
        {
            var definitions = new List<TrapDefinition>();
            var names = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (string guid in AssetDatabase.FindAssets("t:TrapDefinition"))
            {
                var def = AssetDatabase.LoadAssetAtPath<TrapDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (def == null) continue;
                definitions.Add(def);
                names.Add(def.name);
            }

            if (map.allowedTraps != null)
            {
                var seen = new HashSet<string>(System.StringComparer.Ordinal);
                foreach (string name in map.allowedTraps)
                {
                    if (!names.Contains(name))
                        w.Add($"allowedTraps 에 미존재 함정 이름: '{name}'");
                    if (!seen.Add(name))
                        w.Add($"allowedTraps 에 중복 함정 이름: '{name}'");
                }
            }

            var anchors = new HashSet<TrapAnchor>();
            foreach (var slot in map.trapSlots)
            {
                if (!anchors.Add(slot.Anchor)) continue;
                bool compatible = false;
                foreach (var def in definitions)
                {
                    if (!map.IsTrapAllowed(def.name) || !def.IsCompatibleWith(slot.Anchor)) continue;
                    compatible = true;
                    break;
                }
                if (!compatible)
                    w.Add($"{slot.Anchor} 슬롯과 호환되는 허용 함정이 없습니다");
            }
        }

        // ── 유틸 ─────────────────────────────────────────────────────────────

        /// <summary>타일 기준 솔리드 (슬롯 무시).</summary>
        private static bool IsSolidTile(MapData map, int x, int y)
            => map.GetTile(x, y) != TileType.Empty;

        /// <summary>
        /// 지형 판정 — 슬롯 칸은 Play 중 항상 솔리드(함정 또는 봉인 타일)가 되므로
        /// 솔리드 타일과 동일하게 취급합니다.
        /// </summary>
        private static bool IsTerrain(MapData map, HashSet<int> slots, int x, int y)
            => IsSolidTile(map, x, y)
            || (map.InBounds(x, y) && slots.Contains(map.Index(x, y)));
    }
}
