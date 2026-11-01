using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace ReTrap.Tests
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  SlotSizeTests — 맵 단위 슬롯 크기(slotSize, s×s 슬롯) 데이터·NavGrid 마킹·방향 규약
    // ═══════════════════════════════════════════════════════════════════════════

    public class SlotSizeTests
    {
        // ── 버전 / 직렬화 ────────────────────────────────────────────────────

        [Test]
        public void FromJson_v1_맵은_slotSize_1로_마이그레이션되고_버전이_2가_된다()
        {
            // slotSize 필드가 없는 구버전 JSON — JsonUtility 는 누락 필드에 초기값(2)을 넣으므로 Migrate 가 1로 강제해야 한다
            string json = "{\"version\":1,\"mapId\":\"old\",\"width\":4,\"height\":4,"
                        + "\"goalPoint\":{\"x\":2,\"y\":2},"
                        + "\"grid\":[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0],"
                        + "\"trapSlots\":[{\"x\":1,\"y\":1,\"anchor\":\"Floor\"}]}";

            MapData map = MapData.FromJson(json);

            Assert.AreEqual(1, map.slotSize);
            Assert.AreEqual(2, map.version);
            Assert.AreEqual(2, MapData.CurrentVersion);
            Assert.IsTrue(map.Validate(out string err), err);
            Assert.IsNotNull(map.FindSlotCovering(1, 1));
            Assert.IsNull(map.FindSlotCovering(2, 1), "s=1 이면 이웃 셀은 영역 밖");
        }

        [Test]
        public void CreateEmpty_는_slotSize_2_버전_2()
        {
            var map = MapData.CreateEmpty(8, 8);
            Assert.AreEqual(2, map.slotSize);
            Assert.AreEqual(2, map.version);
        }

        [TestCase(2)]
        [TestCase(3)]
        public void JSON_v2_왕복에서_slotSize가_보존된다(int s)
        {
            var map = MapData.CreateEmpty(10, 10);
            map.slotSize = s;
            map.ToggleSlot(2, 2, TrapAnchor.Floor);

            MapData back = MapData.FromJson(map.ToJson());

            Assert.AreEqual(s, back.slotSize);
            Assert.AreEqual(2, back.version);
            Assert.AreEqual(1, back.trapSlots.Count);
            Assert.IsTrue(back.Validate(out string err), err);
        }

        [Test]
        public void FromJson_slotSize가_1미만이면_1로_보정()
        {
            var map = MapData.CreateEmpty(4, 4);
            map.slotSize = 0;

            MapData back = MapData.FromJson(map.ToJson());

            Assert.AreEqual(1, back.slotSize);
        }

        // ── 영역 헬퍼 ────────────────────────────────────────────────────────

        [Test]
        public void SlotCells_s1은_1칸_s2는_4칸()
        {
            var slot = new TrapSlotData(3, 4, TrapAnchor.Floor);
            var map  = MapData.CreateEmpty(10, 10);

            map.slotSize = 1;
            CollectionAssert.AreEquivalent(new[] { new Vector2Int(3, 4) }, map.SlotCells(slot).ToArray());

            map.slotSize = 2;
            CollectionAssert.AreEquivalent(
                new[] { new Vector2Int(3, 4), new Vector2Int(4, 4), new Vector2Int(3, 5), new Vector2Int(4, 5) },
                map.SlotCells(slot).ToArray());
        }

        [Test]
        public void FindSlotCovering_은_영역_4칸_모두에서_같은_슬롯을_찾는다()
        {
            var map = MapData.CreateEmpty(10, 10); // s=2
            map.ToggleSlot(3, 4, TrapAnchor.Ceiling);
            var slot = map.GetSlot(3, 4);
            Assert.IsNotNull(slot);

            foreach (var c in new[] { new Vector2Int(3, 4), new Vector2Int(4, 4), new Vector2Int(3, 5), new Vector2Int(4, 5) })
                Assert.AreSame(slot, map.FindSlotCovering(c.x, c.y), $"{c}");

            Assert.IsNull(map.FindSlotCovering(5, 4));
            Assert.IsNull(map.FindSlotCovering(3, 6));
            Assert.IsNull(map.GetSlot(4, 4), "GetSlot 은 앵커 정확 일치만");
        }

        [Test]
        public void SlotCenterToWorld_s1은_CellToWorld와_같고_s2는_영역_중심()
        {
            var map = MapData.CreateEmpty(10, 10);
            var origin = new Vector2(5f, -2f);
            var slot = new TrapSlotData(3, 4, TrapAnchor.Floor);

            map.slotSize = 1;
            Assert.AreEqual(map.CellToWorld(3, 4, origin), map.SlotCenterToWorld(slot, origin));

            map.slotSize = 2;
            Assert.AreEqual(origin + new Vector2(4f, 5f), map.SlotCenterToWorld(slot, origin));

            map.tileUnit = 2f;
            Assert.AreEqual(origin + new Vector2(8f, 10f), map.SlotCenterToWorld(slot, origin));
        }

        // ── Validate / ToggleSlot / Resize ───────────────────────────────────

        [Test]
        public void Validate_슬롯_영역이_맵_밖으로_나가면_실패()
        {
            var map = MapData.CreateEmpty(4, 4); // s=2
            map.trapSlots.Add(new TrapSlotData(3, 3, TrapAnchor.Floor)); // 앵커는 안이지만 (4,4) 가 밖
            Assert.IsFalse(map.Validate(out string err));
            StringAssert.Contains("범위", err);
        }

        [Test]
        public void Validate_슬롯_영역이_겹치면_실패_맞닿기만_하면_통과()
        {
            var map = MapData.CreateEmpty(6, 6); // s=2
            map.trapSlots.Add(new TrapSlotData(0, 0, TrapAnchor.Floor));
            map.trapSlots.Add(new TrapSlotData(2, 0, TrapAnchor.Floor)); // 맞닿음
            Assert.IsTrue(map.Validate(out string err), err);

            map.trapSlots.Add(new TrapSlotData(1, 1, TrapAnchor.Floor)); // (1..2,1..2) 가 위 두 영역과 겹침
            Assert.IsFalse(map.Validate(out err));
            StringAssert.Contains("겹침", err);
        }

        [Test]
        public void Validate_slotSize_0이면_실패()
        {
            var map = MapData.CreateEmpty(4, 4);
            map.slotSize = 0;
            Assert.IsFalse(map.Validate(out _));
        }

        [Test]
        public void ToggleSlot_겹치는_위치는_추가_거부_영역_안쪽_셀은_제거_갱신()
        {
            var map = MapData.CreateEmpty(8, 8); // s=2
            map.ToggleSlot(2, 2, TrapAnchor.Floor);
            Assert.AreEqual(1, map.trapSlots.Count);

            map.ToggleSlot(1, 1, TrapAnchor.Floor); // 앵커 (1,1) 영역이 (2,2) 와 겹침 → 신규 거부
            Assert.AreEqual(1, map.trapSlots.Count, "겹치는 신규 추가는 거부");

            map.ToggleSlot(3, 3, TrapAnchor.Ceiling); // 영역 안쪽 셀 → 다른 anchor 이므로 갱신
            Assert.AreEqual(1, map.trapSlots.Count);
            Assert.AreEqual(TrapAnchor.Ceiling, map.GetSlot(2, 2).Anchor);

            map.ToggleSlot(3, 2, TrapAnchor.Ceiling); // 영역 안쪽 셀 + 같은 anchor → 제거
            Assert.AreEqual(0, map.trapSlots.Count);

            map.ToggleSlot(7, 7, TrapAnchor.Floor); // 영역이 맵 밖 → 거부
            Assert.AreEqual(0, map.trapSlots.Count);
        }

        [Test]
        public void Resize_축소시_영역이_범위를_벗어나는_슬롯을_제거()
        {
            var map = MapData.CreateEmpty(6, 6); // s=2
            map.ToggleSlot(0, 0, TrapAnchor.Floor);
            map.ToggleSlot(2, 2, TrapAnchor.Floor);

            map.Resize(3, 3); // (2,2) 슬롯은 앵커는 안이지만 영역이 (3,3) 로 밖

            Assert.AreEqual(1, map.trapSlots.Count);
            Assert.IsNotNull(map.GetSlot(0, 0));
            Assert.IsTrue(map.Validate(out string err), err);
        }

        // ── NavGrid 마킹 ─────────────────────────────────────────────────────

        private static NavNode[] Nodes(int w, int h) => new NavNode[w * h];

        [Test]
        public void NavGrid_MarkTrapSlot_s2_Floor는_4칸_솔리드_바깥_2칸_띠에_위험()
        {
            const int W = 8, H = 8;
            var n = Nodes(W, H);

            NavGrid.MarkTrapSlot(n, W, H, 2, 2, 2, TrapAnchor.Floor, markSolid: true, danger: 5f);

            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                bool inArea = x >= 2 && x <= 3 && y >= 2 && y <= 3;
                Assert.AreEqual(inArea, n[y * W + x].solid, $"solid ({x},{y})");

                bool band = y == 4 && (x == 2 || x == 3); // Floor 바깥 = 위쪽 행 y+s
                Assert.AreEqual(band ? 5f : 0f, n[y * W + x].dangerCost, 1e-5f, $"danger ({x},{y})");
            }
        }

        [Test]
        public void NavGrid_MarkTrapSlot_s1은_기존과_동일_1칸_솔리드_바깥_1칸_위험()
        {
            const int W = 6, H = 6;
            var n = Nodes(W, H);

            NavGrid.MarkTrapSlot(n, W, H, 2, 2, 1, TrapAnchor.Floor, markSolid: true, danger: 3f);

            Assert.AreEqual(1, n.Count(v => v.solid));
            Assert.IsTrue(n[2 * W + 2].solid);
            Assert.AreEqual(1, n.Count(v => v.dangerCost > 0f));
            Assert.AreEqual(3f, n[3 * W + 2].dangerCost, 1e-5f);
        }

        [Test]
        public void NavGrid_MarkTrapSlot_벽_앵커의_위험_띠는_바깥_열()
        {
            const int W = 8, H = 8;

            // LeftWall: 바깥 = -x → 영역 왼쪽 열 (x=1, y=2..3)
            var left = Nodes(W, H);
            NavGrid.MarkTrapSlot(left, W, H, 2, 2, 2, TrapAnchor.LeftWall, true, 1f);
            Assert.AreEqual(1f, left[2 * W + 1].dangerCost);
            Assert.AreEqual(1f, left[3 * W + 1].dangerCost);
            Assert.AreEqual(2, left.Count(v => v.dangerCost > 0f));

            // RightWall: 바깥 = +x → 영역 오른쪽 열 (x=4, y=2..3)
            var right = Nodes(W, H);
            NavGrid.MarkTrapSlot(right, W, H, 2, 2, 2, TrapAnchor.RightWall, true, 1f);
            Assert.AreEqual(1f, right[2 * W + 4].dangerCost);
            Assert.AreEqual(1f, right[3 * W + 4].dangerCost);
            Assert.AreEqual(2, right.Count(v => v.dangerCost > 0f));
        }

        [Test]
        public void NavGrid_MarkTrapSlot_솔리드_opt_out과_범위_밖은_안전()
        {
            const int W = 4, H = 4;
            var n = Nodes(W, H);

            // 이동형 함정(markSolid=false): 솔리드 없음. 위험은 그대로
            NavGrid.MarkTrapSlot(n, W, H, 1, 1, 2, TrapAnchor.Ceiling, markSolid: false, danger: 2f);
            Assert.AreEqual(0, n.Count(v => v.solid));
            Assert.AreEqual(2f, n[0 * W + 1].dangerCost); // Ceiling 바깥 = 아래 행 y-1=0
            Assert.AreEqual(2f, n[0 * W + 2].dangerCost);

            // 맵 가장자리에서 바깥 띠가 밖으로 나가도 예외 없음
            NavGrid.MarkTrapSlot(n, W, H, 2, 2, 2, TrapAnchor.Floor, true, 1f);
        }

        // ── 방향 규약 ────────────────────────────────────────────────────────

        [TestCase(TrapAnchor.Floor)]
        [TestCase(TrapAnchor.Ceiling)]
        [TestCase(TrapAnchor.LeftWall)]
        [TestCase(TrapAnchor.RightWall)]
        public void OutwardOf_는_ArrowShooter_발사_방향과_일치(TrapAnchor anchor)
        {
            Vector2Int o = TrapBase.OutwardOf(anchor);
            Vector2    f = ArrowShooter.OutwardDir(anchor);

            Assert.AreEqual(f.x, o.x, 1e-5f, $"{anchor} x");
            Assert.AreEqual(f.y, o.y, 1e-5f, $"{anchor} y");
        }
    }
}
