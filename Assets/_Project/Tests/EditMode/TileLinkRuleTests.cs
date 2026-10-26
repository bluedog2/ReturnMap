using NUnit.Framework;

namespace ReTrap.Tests
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  TileLinkRuleTests — 이음 낙서 타일 선택 규칙 테스트
    // ═══════════════════════════════════════════════════════════════════════════

    public class TileLinkRuleTests
    {
        // 인덱스 → (first, second). b 변형은 같은 종류 쌍.
        private static (int first, int second) Edges(int idx)
        {
            if (idx < 0)  return (0, 0);
            if (idx == 9) return (1, 1);
            if (idx == 10) return (2, 2);
            return (idx / 3, idx % 3);
        }

        [Test]
        public void 가로로_이어진_칸은_공유_경계_종류가_같다()
        {
            for (int y = -3; y < 20; y++)
            for (int x = -3; x < 40; x++)
            {
                var left  = Edges(TileLinkRule.SpriteIndex(false, x,     y, true, true));
                var right = Edges(TileLinkRule.SpriteIndex(false, x + 1, y, true, true));
                Assert.AreEqual(left.second, right.first, $"({x},{y})-({x + 1},{y})");
            }
        }

        [Test]
        public void 세로로_이어진_칸은_공유_경계_종류가_같다()
        {
            for (int y = -3; y < 20; y++)
            for (int x = -3; x < 40; x++)
            {
                var lower = Edges(TileLinkRule.SpriteIndex(true, x, y,     true, true));
                var upper = Edges(TileLinkRule.SpriteIndex(true, x, y + 1, true, true));
                Assert.AreEqual(lower.first, upper.second, $"({x},{y})-({x},{y + 1})");
            }
        }

        [Test]
        public void 이웃이_없으면_그쪽_경계는_0()
        {
            for (int x = 0; x < 50; x++)
            {
                Assert.AreEqual(0, Edges(TileLinkRule.SpriteIndex(false, x, 3, false, true)).first);
                Assert.AreEqual(0, Edges(TileLinkRule.SpriteIndex(false, x, 3, true, false)).second);
                Assert.AreEqual(-1, TileLinkRule.SpriteIndex(false, x, 3, false, false));
            }
        }

        [Test]
        public void 인덱스는_항상_목록_범위_안()
        {
            for (int y = -5; y < 30; y++)
            for (int x = -5; x < 30; x++)
            {
                int i = TileLinkRule.SpriteIndex(x % 2 == 0, x, y, true, true);
                Assert.That(i == -1 || (i >= 1 && i < TileLinkRule.SpriteCount), $"({x},{y}) → {i}");
            }
        }

        [Test]
        public void 경계_종류_분포는_없음이_절반_가량()
        {
            int none = 0, total = 0;
            for (int y = 0; y < 40; y++)
            for (int x = 0; x < 40; x++) { if (TileLinkRule.Kind(x, y) == 0) none++; total++; }
            Assert.That(none / (float)total, Is.InRange(0.4f, 0.6f));
        }
    }
}
