using System.Collections.Generic;
using NUnit.Framework;

namespace ReTrap.Tests
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  TagWeightServiceTests — 최종 가중치 / 카테고리별 표기 확률
    // ═══════════════════════════════════════════════════════════════════════════
    //  ResearchBoardState(정적 투자 상태)를 건드리는 노드 보정 경로는 리셋 API 가 없어
    //  제외하고, nodes == null (보정 없음) 경로만 검증한다.

    public class TagWeightServiceTests
    {
        private TestFactory _f;

        [SetUp]
        public void SetUp() => _f = new TestFactory();

        [TearDown]
        public void TearDown() => _f.DestroyAll();

        [Test]
        public void 노드가_없으면_기본_가중치가_그대로_복사된다()
        {
            var a = _f.Tag("A"); var b = _f.Tag("B");
            var table = _f.Table(new (AgentArchetype, int, AITagDefinition[], int)[0],
                                 new[] { (a, 1.5f), (b, 4f) });
            var results = new List<StageSpawnTable.TagWeightEntry>();

            TagWeightService.ComputeFinalWeights(table, null, results);

            Assert.AreEqual(2, results.Count);
            Assert.AreSame(a, results[0].tag);
            Assert.AreEqual(1.5f, results[0].baseWeight, 1e-5f);
            Assert.AreEqual(4f, results[1].baseWeight, 1e-5f);
        }

        [Test]
        public void 결과_리스트는_매번_먼저_비워진다()
        {
            var a = _f.Tag("A");
            var table = _f.Table(new (AgentArchetype, int, AITagDefinition[], int)[0], new[] { (a, 1f) });
            var results = new List<StageSpawnTable.TagWeightEntry> { TestFactory.W(a, 9f), TestFactory.W(a, 9f) };

            TagWeightService.ComputeFinalWeights(table, null, results);
            TagWeightService.ComputeFinalWeights(table, null, results);

            Assert.AreEqual(1, results.Count);
        }

        [Test]
        public void 테이블이_null이면_빈_결과()
        {
            var results = new List<StageSpawnTable.TagWeightEntry> { TestFactory.W(null, 1f) };

            TagWeightService.ComputeFinalWeights(null, null, results);

            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void 표기확률은_같은_카테고리_총합_대비_비율()
        {
            var a = _f.Tag("A", AITagCategory.Stat);
            var b = _f.Tag("B", AITagCategory.Stat);
            var m = _f.Tag("M", AITagCategory.Movement);
            var weights = new[] { TestFactory.W(a, 1f), TestFactory.W(b, 3f), TestFactory.W(m, 100f) };

            Assert.AreEqual(25f, TagWeightService.GetDisplayProbability(weights, a), 1e-3f);
            Assert.AreEqual(75f, TagWeightService.GetDisplayProbability(weights, b), 1e-3f);
            Assert.AreEqual(100f, TagWeightService.GetDisplayProbability(weights, m), 1e-3f, "카테고리 단독");
        }

        [Test]
        public void 표기확률_가중치_0이하_태그는_0_그리고_총합에도_불포함()
        {
            var a = _f.Tag("A"); var zero = _f.Tag("Z");
            var weights = new[] { TestFactory.W(a, 2f), TestFactory.W(zero, 0f) };

            Assert.AreEqual(0f, TagWeightService.GetDisplayProbability(weights, zero));
            Assert.AreEqual(100f, TagWeightService.GetDisplayProbability(weights, a), 1e-3f);
        }

        [Test]
        public void 표기확률_null_입력은_0()
        {
            var a = _f.Tag("A");
            Assert.AreEqual(0f, TagWeightService.GetDisplayProbability(null, a));
            Assert.AreEqual(0f, TagWeightService.GetDisplayProbability(new[] { TestFactory.W(a, 1f) }, null));
        }
    }
}
