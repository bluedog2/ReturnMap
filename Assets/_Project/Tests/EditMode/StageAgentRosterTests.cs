using System.Linq;
using NUnit.Framework;

namespace ReTrap.Tests
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  StageAgentRosterTests — 시드 고정 웨이브 로스터 확정
    // ═══════════════════════════════════════════════════════════════════════════

    public class StageAgentRosterTests
    {
        private TestFactory _f;
        private AITagDefinition _a, _b, _c, _d;
        private AgentArchetype _arch;

        [SetUp]
        public void SetUp()
        {
            _f = new TestFactory();
            _a = _f.Tag("A"); _b = _f.Tag("B"); _c = _f.Tag("C"); _d = _f.Tag("D");
            _arch = _f.Archetype();
        }

        [TearDown]
        public void TearDown() => _f.DestroyAll();

        private StageSpawnTable MakeTable(int count, AITagDefinition[] fixedTags, int randomSlots)
            => _f.Table(
                new[] { (_arch, count, fixedTags, randomSlots) },
                new[] { (_a, 1f), (_b, 2f), (_c, 3f), (_d, 4f) });

        [Test]
        public void 플랜_수는_엔트리_count_합과_같다()
        {
            var table = _f.Table(
                new[] { (_arch, 4, (AITagDefinition[])null, 1), (_arch, 6, (AITagDefinition[])null, 0) },
                new[] { (_a, 1f) });

            var roster = StageAgentRoster.Build(table, 1);

            Assert.AreEqual(10, roster.Plans.Count);
        }

        [Test]
        public void 아키타입이_null인_엔트리는_건너뛴다()
        {
            var table = _f.Table(
                new[] { ((AgentArchetype)null, 5, (AITagDefinition[])null, 1), (_arch, 2, (AITagDefinition[])null, 1) },
                new[] { (_a, 1f) });

            Assert.AreEqual(2, StageAgentRoster.Build(table, 1).Plans.Count);
        }

        [Test]
        public void 같은_시드는_같은_태그_구성_다른_시드는_달라질_수_있다()
        {
            var table = MakeTable(20, null, 2);

            var r1 = StageAgentRoster.Build(table, 99);
            var r2 = StageAgentRoster.Build(table, 99);

            for (int i = 0; i < r1.Plans.Count; i++)
                CollectionAssert.AreEqual(r1.Plans[i].Tags.Tags, r2.Plans[i].Tags.Tags, $"plan {i}");
            Assert.AreEqual(99, r1.Seed);

            var other = StageAgentRoster.Build(table, 100);
            bool anyDiff = Enumerable.Range(0, r1.Plans.Count)
                .Any(i => !r1.Plans[i].Tags.Tags.SequenceEqual(other.Plans[i].Tags.Tags));
            Assert.IsTrue(anyDiff, "20명 x 2슬롯에서 시드가 달라도 전부 동일할 확률은 사실상 0");
        }

        [Test]
        public void 확정_태그는_모든_개체에_포함되고_태그는_3개를_넘지_않는다()
        {
            var table = MakeTable(15, new[] { _a }, 5);

            var roster = StageAgentRoster.Build(table, 7);

            foreach (var plan in roster.Plans)
            {
                Assert.AreSame(_a, plan.Tags.Tags[0]);
                Assert.LessOrEqual(plan.Tags.Tags.Count, TagSet.MaxTagsPerAgent);
                Assert.AreEqual(plan.Tags.Tags.Count, plan.Tags.Tags.Distinct().Count(), "중복 금지");
            }
        }

        [Test]
        public void weightOverrides가_있으면_테이블_기본_가중치_대신_사용된다()
        {
            var table = MakeTable(10, null, 1);
            var overrides = new[] { TestFactory.W(_d, 1f) };

            var roster = StageAgentRoster.Build(table, 3, overrides);

            foreach (var plan in roster.Plans)
                CollectionAssert.AreEqual(new[] { _d }, plan.Tags.Tags);
        }

        [Test]
        public void BuildSingle은_테이블없이_1개체_플랜을_만든다()
        {
            var roster = StageAgentRoster.BuildSingle(_arch, null);

            Assert.AreEqual(1, roster.Plans.Count);
            Assert.IsNull(roster.Table);
            Assert.AreEqual(0, roster.Plans[0].Tags.Tags.Count);
        }
    }
}
