using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ReTrap.Tests
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  MapDataTests — 맵 데이터 JSON 왕복 / 검증 / 리사이즈 / 슬롯 토글
    // ═══════════════════════════════════════════════════════════════════════════

    public class MapDataTests
    {
        [Test]
        public void JSON_왕복_후_타일_슬롯_스폰이_보존된다()
        {
            var map = MapData.CreateEmpty(6, 4);
            map.SetTile(2, 1, TileType.Wall);
            map.SetTile(5, 3, TileType.Ceiling);
            map.SetBackground(1, 1, 3);
            map.ToggleSlot(2, 2, TrapAnchor.Ceiling);
            map.spawnPoint = new GridCoord(1, 2);
            map.mapId = "rt";

            MapData back = MapData.FromJson(map.ToJson());

            Assert.AreEqual(6, back.width);
            Assert.AreEqual(4, back.height);
            Assert.AreEqual(TileType.Wall, back.GetTile(2, 1));
            Assert.AreEqual(TileType.Ceiling, back.GetTile(5, 3));
            Assert.AreEqual(3, back.GetBackground(1, 1));
            Assert.AreEqual(1, back.trapSlots.Count);
            Assert.AreEqual(TrapAnchor.Ceiling, back.GetSlot(2, 2).Anchor);
            Assert.AreEqual(1, back.spawnPoint.x);
            Assert.AreEqual(2, back.spawnPoint.y);
            Assert.AreEqual("rt", back.mapId);
            Assert.IsTrue(back.Validate(out string err), err);
        }

        [Test]
        public void FromJson_배경이_비어있으면_grid_크기로_보정된다()
        {
            var map = MapData.CreateEmpty(5, 3);
            map.background = null;

            MapData back = MapData.FromJson(map.ToJson());

            Assert.AreEqual(15, back.background.Length);
        }

        [Test]
        public void FromJson_구버전은_경고후_현재_버전으로_올라간다()
        {
            var map = MapData.CreateEmpty(4, 4);
            map.version = 0;
            LogAssert.Expect(LogType.Warning, new Regex("마이그레이션 규칙 없음"));

            MapData back = MapData.FromJson(map.ToJson());

            Assert.AreEqual(MapData.CurrentVersion, back.version);
        }

        [Test]
        public void Validate_빈_맵은_통과()
        {
            Assert.IsTrue(MapData.CreateEmpty(10, 8).Validate(out string err), err);
        }

        [Test]
        public void Validate_grid_길이가_다르면_실패()
        {
            var map = MapData.CreateEmpty(4, 4);
            map.grid = new int[5];
            Assert.IsFalse(map.Validate(out string err));
            Assert.IsNotEmpty(err);
        }

        [Test]
        public void Validate_스폰이_범위_밖이면_실패()
        {
            var map = MapData.CreateEmpty(4, 4);
            map.spawnPoint = new GridCoord(4, 0);
            Assert.IsFalse(map.Validate(out _));
        }

        [Test]
        public void Validate_골이_범위_밖이면_실패()
        {
            var map = MapData.CreateEmpty(4, 4);
            map.goalPoint = new GridCoord(0, -1);
            Assert.IsFalse(map.Validate(out _));
        }

        [Test]
        public void Validate_슬롯_좌표_중복이면_실패()
        {
            var map = MapData.CreateEmpty(4, 4);
            map.trapSlots.Add(new TrapSlotData(1, 1, TrapAnchor.Floor));
            map.trapSlots.Add(new TrapSlotData(1, 1, TrapAnchor.Ceiling));
            Assert.IsFalse(map.Validate(out string err));
            StringAssert.Contains("중복", err);
        }

        [Test]
        public void Validate_슬롯_anchor_오타면_실패()
        {
            var map = MapData.CreateEmpty(4, 4);
            map.trapSlots.Add(new TrapSlotData { x = 1, y = 1, anchor = "Flor" });
            Assert.IsFalse(map.Validate(out _));
        }

        [Test]
        public void Validate_슬롯이_범위_밖이면_실패()
        {
            var map = MapData.CreateEmpty(4, 4);
            map.trapSlots.Add(new TrapSlotData(9, 1, TrapAnchor.Floor));
            Assert.IsFalse(map.Validate(out _));
        }

        [Test]
        public void ToggleSlot_추가_갱신_제거_순환()
        {
            var map = MapData.CreateEmpty(4, 4);

            map.ToggleSlot(1, 1, TrapAnchor.Floor);
            Assert.AreEqual(1, map.trapSlots.Count, "추가");

            map.ToggleSlot(1, 1, TrapAnchor.Ceiling);
            Assert.AreEqual(1, map.trapSlots.Count, "다른 anchor 는 갱신");
            Assert.AreEqual(TrapAnchor.Ceiling, map.GetSlot(1, 1).Anchor);

            map.ToggleSlot(1, 1, TrapAnchor.Ceiling);
            Assert.AreEqual(0, map.trapSlots.Count, "같은 anchor 는 제거");
        }

        [Test]
        public void ToggleSlot_범위_밖은_무시()
        {
            var map = MapData.CreateEmpty(4, 4);
            map.ToggleSlot(4, 0, TrapAnchor.Floor);
            Assert.AreEqual(0, map.trapSlots.Count);
        }

        [Test]
        public void Resize_확대시_기존_타일을_보존하고_새_칸은_Empty()
        {
            var map = MapData.CreateEmpty(3, 3);
            map.SetTile(2, 1, TileType.Wall);

            map.Resize(5, 4);

            Assert.AreEqual(5, map.width);
            Assert.AreEqual(20, map.grid.Length);
            Assert.AreEqual(TileType.Wall, map.GetTile(2, 1));
            Assert.AreEqual(TileType.Empty, map.GetTile(4, 3));
            Assert.IsTrue(map.Validate(out string err), err);
        }

        [Test]
        public void Resize_축소시_범위밖_슬롯_제거_스폰골_클램프()
        {
            var map = MapData.CreateEmpty(8, 8);
            map.ToggleSlot(1, 1, TrapAnchor.Floor);
            map.ToggleSlot(6, 6, TrapAnchor.Floor);
            map.spawnPoint = new GridCoord(7, 7);
            map.goalPoint  = new GridCoord(6, 0);

            map.Resize(4, 4);

            Assert.AreEqual(1, map.trapSlots.Count);
            Assert.IsNotNull(map.GetSlot(1, 1));
            Assert.AreEqual(3, map.spawnPoint.x);
            Assert.AreEqual(3, map.spawnPoint.y);
            Assert.AreEqual(3, map.goalPoint.x);
            Assert.IsTrue(map.Validate(out string err), err);
        }

        [Test]
        public void GetTile_범위_밖은_Empty()
        {
            var map = MapData.CreateEmpty(3, 3);
            Assert.AreEqual(TileType.Empty, map.GetTile(-1, 0));
            Assert.AreEqual(TileType.Empty, map.GetTile(3, 3));
        }

        [Test]
        public void CellToWorld_셀_중심과_월드_바운드()
        {
            var map = MapData.CreateEmpty(4, 2);
            map.tileUnit = 2f;

            Vector2 c = map.CellToWorld(1, 0, new Vector2(10f, 0f));
            Assert.AreEqual(13f, c.x, 1e-5f);
            Assert.AreEqual(1f, c.y, 1e-5f);

            Bounds b = map.GetWorldBounds();
            Assert.AreEqual(8f, b.size.x, 1e-5f);
            Assert.AreEqual(4f, b.size.y, 1e-5f);
        }
    }
}
