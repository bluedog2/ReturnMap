using System;
using System.Linq;
using NUnit.Framework;

namespace ReTrap.Tests
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  WeightedTagSelectorTests — 가중치 태그 추첨 (결정성·상한·제외)
    // ═══════════════════════════════════════════════════════════════════════════

    public class WeightedTagSelectorTests
    {
        private TestFactory _f;
        private AITagDefinition _a, _b, _c, _d, _e;
        private StageSpawnTable.TagWeightEntry[] _weights;

        [SetUp]
        public void SetUp()
        {
            _f = new TestFactory();
            _a = _f.Tag("A"); _b = _f.Tag("B"); _c = _f.Tag("C"); _d = _f.Tag("D"); _e = _f.Tag("E");
            _weights = new[]
            {
                TestFactory.W(_a, 1f), TestFactory.W(_b, 2f), TestFactory.W(_c, 3f),
                TestFactory.W(_d, 4f), TestFactory.W(_e, 5f),
            };
        }

        [TearDown]
        public void TearDown() => _f.DestroyAll();

        [Test]
        public void 같은_시드는_같은_결과()
        {
            for (int seed = 0; seed < 30; seed++)
            {
                var r1 = WeightedTagSelector.Roll(_weights, null, 3, new Random(seed));
                var r2 = WeightedTagSelector.Roll(_weights, null, 3, new Random(seed));
                CollectionAssert.AreEqual(r1, r2, $"seed={seed}");
            }
        }

        [Test]
        public void 랜덤_슬롯이_상한을_넘어도_3개까지만()
        {
            var r = WeightedTagSelector.Roll(_weights, null, 10, new Random(1));
            Assert.AreEqual(TagSet.MaxTagsPerAgent, r.Count);
        }

        [Test]
        public void 확정_태그가_먼저_자리를_차지하고_남은_슬롯만_추첨()
        {
            var r = WeightedTagSelector.Roll(_weights, new[] { _a, _b }, 5, new Random(3));
            Assert.AreEqual(3, r.Count);
            Assert.AreSame(_a, r[0]);
            Assert.AreSame(_b, r[1]);
        }

        [Test]
        public void 확정_태그가_상한을_넘으면_앞_3개만()
        {
            var r = WeightedTagSelector.Roll(_weights, new[] { _a, _b, _c, _d, _e }, 2, new Random(3));
            CollectionAssert.AreEqual(new[] { _a, _b, _c }, r);
        }

        [Test]
        public void 중복_태그는_뽑히지_않는다()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                var r = WeightedTagSelector.Roll(_weights, new[] { _a }, 2, new Random(seed));
                Assert.AreEqual(r.Count, r.Distinct().Count(), $"seed={seed}");
            }
        }

        [Test]
        public void 같은_배타그룹_태그는_함께_뽑히지_않는다()
        {
            var g1 = _f.Tag("G1", exclusionGroup: 7);
            var g2 = _f.Tag("G2", exclusionGroup: 7);
            var weights = new[] { TestFactory.W(g1, 1f), TestFactory.W(g2, 1f), TestFactory.W(_a, 1f) };

            for (int seed = 0; seed < 200; seed++)
            {
                var r = WeightedTagSelector.Roll(weights, null, 3, new Random(seed));
                Assert.LessOrEqual(r.Count(t => t.ExclusionGroup == 7), 1, $"seed={seed}");
            }
        }

        [Test]
        public void 확정_태그와_같은_배타그룹_후보는_제외()
        {
            var fixedTag = _f.Tag("F", exclusionGroup: 4);
            var rival    = _f.Tag("R", exclusionGroup: 4);
            var weights  = new[] { TestFactory.W(rival, 100f), TestFactory.W(_a, 1f) };

            for (int seed = 0; seed < 50; seed++)
            {
                var r = WeightedTagSelector.Roll(weights, new[] { fixedTag }, 1, new Random(seed));
                CollectionAssert.DoesNotContain(r, rival);
            }
        }

        [Test]
        public void 가중치_0이하_후보는_절대_뽑히지_않고_후보가_바닥나면_조용히_종료()
        {
            var weights = new[] { TestFactory.W(_a, 0f), TestFactory.W(_b, -1f), TestFactory.W(_c, 1f) };

            var r = WeightedTagSelector.Roll(weights, null, 3, new Random(5));

            CollectionAssert.AreEqual(new[] { _c }, r); // 유효 후보 1개뿐 → 1개만
        }

        [Test]
        public void 가중치와_확정태그가_null이어도_예외없이_빈_결과()
        {
            Assert.AreEqual(0, WeightedTagSelector.Roll(null, null, 2, new Random(1)).Count);
        }

        [Test]
        public void 가중치_비율대로_뽑힌다()
        {
            var weights = new[] { TestFactory.W(_a, 1f), TestFactory.W(_b, 3f) };
            var rng = new Random(2024);
            int b = 0;
            const int n = 20000;
            for (int i = 0; i < n; i++)
                if (WeightedTagSelector.Roll(weights, null, 1, rng)[0] == _b) b++;

            Assert.AreEqual(0.75, b / (double)n, 0.02);
        }
    }
}
