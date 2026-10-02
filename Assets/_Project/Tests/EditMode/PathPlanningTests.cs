using NUnit.Framework;

namespace ReTrap.Tests
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  PathPlanningTests — NavGrid 빌드 + A* 경로 존재/차단
    // ═══════════════════════════════════════════════════════════════════════════
    //  TrapSlotRegistry(정적)에 등록된 함정이 없다는 전제 — EditMode 에서는 슬롯 마커가
    //  자가 등록하지 않으므로 순수 지형만으로 그리드가 만들어진다.

    public class PathPlanningTests
    {
        /// <summary>바닥(y=0) 한 줄만 있는 맵. 서 있는 칸은 y=1.</summary>
        private static MapData FlatMap(int w, int h)
        {
            var map = MapData.CreateEmpty(w, h);
            for (int x = 0; x < w; x++) map.SetTile(x, 0, TileType.Floor);
            return map;
        }

        private static NavGrid Build(MapData map, TraversalProfile profile)
            => NavGrid.Build(map, AIBehaviorParams.Default, profile, null);

        [Test]
        public void NavGrid_바닥_바로_위_칸만_grounded()
        {
            var g = Build(FlatMap(6, 4), TraversalProfile.Default);

            Assert.IsTrue(g.IsSolid(2, 0));
            Assert.IsFalse(g.IsGrounded(2, 0), "솔리드 칸은 서 있는 자리가 아님");
            Assert.IsTrue(g.IsGrounded(2, 1));
            Assert.IsFalse(g.IsGrounded(2, 2), "공중");
            Assert.IsFalse(g.IsGrounded(-1, 1), "범위 밖");
            Assert.IsFalse(g.IsSolid(6, 0), "범위 밖은 솔리드 아님");
        }

        [Test]
        public void NavGrid_맨_아래줄은_맵_밖이_바닥이_아니므로_grounded_아님()
        {
            var map = MapData.CreateEmpty(4, 4); // 전부 Empty
            var g = Build(map, TraversalProfile.Default);

            Assert.IsFalse(g.IsGrounded(1, 0));
        }

        [Test]
        public void TryFindLanding_낙차_제한을_지킨다()
        {
            var g = Build(FlatMap(4, 8), TraversalProfile.Default);

            Assert.IsTrue(g.TryFindLanding(1, 5, 4, out int landY));
            Assert.AreEqual(1, landY);
            Assert.IsFalse(g.TryFindLanding(1, 7, 3, out _), "낙차 6 > 허용 3");
        }

        [Test]
        public void AStar_평지에서_시작과_골을_잇는_경로()
        {
            var map = FlatMap(8, 4);
            var g = Build(map, TraversalProfile.Default);

            var path = new AStarPathPlanner().FindPath(
                g, new GridCoord(1, 1), new GridCoord(6, 1), AIBehaviorParams.Default, TraversalProfile.Default);

            Assert.IsNotNull(path);
            Assert.AreEqual(1, path[0].x);
            Assert.AreEqual(6, path[path.Count - 1].x);
            Assert.AreEqual(1, path[path.Count - 1].y);
        }

        [Test]
        public void AStar_1칸_벽은_기본_점프로_넘는다()
        {
            var map = FlatMap(8, 5);
            map.SetTile(3, 1, TileType.Wall);
            var g = Build(map, TraversalProfile.Default);

            var path = new AStarPathPlanner().FindPath(
                g, new GridCoord(1, 1), new GridCoord(6, 1), AIBehaviorParams.Default, TraversalProfile.Default);

            Assert.IsNotNull(path, "점프 높이 1 로 1칸 벽 통과 가능");
        }

        [Test]
        public void AStar_3칸_벽은_기본_점프로_막히고_높이뛰기_능력이면_넘는다()
        {
            var map = FlatMap(8, 8);
            for (int y = 1; y <= 3; y++) map.SetTile(3, y, TileType.Wall);

            var planner = new AStarPathPlanner();
            var start = new GridCoord(1, 1);
            var goal  = new GridCoord(6, 1);

            var blocked = planner.FindPath(Build(map, TraversalProfile.Default), start, goal,
                AIBehaviorParams.Default, TraversalProfile.Default);
            Assert.IsNull(blocked, "기본 능력(점프 1)으로는 3칸 벽을 못 넘는다");

            var highJump = new TraversalProfile(3, 2, 4, DamageType.None, false);
            var open = planner.FindPath(Build(map, highJump), start, goal,
                AIBehaviorParams.Default, highJump);
            Assert.IsNotNull(open, "점프 높이 3 이면 통과");
        }

        [Test]
        public void AStar_범위_밖_좌표는_null()
        {
            var map = FlatMap(6, 4);
            var g = Build(map, TraversalProfile.Default);

            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning,
                new System.Text.RegularExpressions.Regex("그리드 밖"));
            var path = new AStarPathPlanner().FindPath(
                g, new GridCoord(1, 1), new GridCoord(20, 1), AIBehaviorParams.Default, TraversalProfile.Default);

            Assert.IsNull(path);
        }
    }
}
