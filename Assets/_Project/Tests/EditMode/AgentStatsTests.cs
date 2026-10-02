using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ReTrap.Tests
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  AgentStatsTests — 태그 스탯 보정(가산 후 승산) / 이동 프로파일 파생
    // ═══════════════════════════════════════════════════════════════════════════

    public class AgentStatsTests
    {
        private TestFactory _f;
        private AgentArchetype _arch;

        [SetUp]
        public void SetUp()
        {
            _f = new TestFactory();
            _arch = _f.Archetype(moveSpeed: 4f, maxHp: 3);
        }

        [TearDown]
        public void TearDown() => _f.DestroyAll();

        private static StatModifier Mod(AgentStatType stat, StatModifierOp op, float v)
            => new StatModifier { stat = stat, op = op, value = v };

        private TagSet Tags(params AITagDefinition[] tags) => new TagSet(tags);

        [Test]
        public void 태그가_없으면_아키타입_기본치()
        {
            var s = AgentStats.From(_arch, TagSet.Empty);

            Assert.AreEqual(4f, s.MoveSpeed, 1e-5f);
            Assert.AreEqual(3, s.MaxHP);
            Assert.AreEqual(1f, s.KnockbackMultiplier, 1e-5f);
            Assert.AreEqual(1f, s.Scale, 1e-5f);
        }

        [Test]
        public void 아키타입이_null이면_안전_기본값()
        {
            var s = AgentStats.From(null, null);

            Assert.AreEqual(4f, s.MoveSpeed, 1e-5f);
            Assert.AreEqual(1, s.MaxHP);
        }

        [Test]
        public void 가산을_먼저_합산한_뒤_승산을_곱한다()
        {
            var tag = _f.Tag("T", mods: new[]
            {
                Mod(AgentStatType.MoveSpeed, StatModifierOp.Multiply, 1.5f),
                Mod(AgentStatType.MoveSpeed, StatModifierOp.Add, 2f),
            });

            var s = AgentStats.From(_arch, Tags(tag));

            Assert.AreEqual((4f + 2f) * 1.5f, s.MoveSpeed, 1e-5f);
        }

        [Test]
        public void 여러_태그의_승산은_서로_곱해지고_가산은_합쳐진다()
        {
            var t1 = _f.Tag("T1", mods: new[] { Mod(AgentStatType.Scale, StatModifierOp.Multiply, 2f),
                                                Mod(AgentStatType.MaxHP, StatModifierOp.Add, 1f) });
            var t2 = _f.Tag("T2", mods: new[] { Mod(AgentStatType.Scale, StatModifierOp.Multiply, 1.5f),
                                                Mod(AgentStatType.MaxHP, StatModifierOp.Add, 2f) });

            var s = AgentStats.From(_arch, Tags(t1, t2));

            Assert.AreEqual(3f, s.Scale, 1e-5f);
            Assert.AreEqual(6, s.MaxHP);
        }

        [Test]
        public void MaxHP는_음수_보정에도_최소_1()
        {
            var tag = _f.Tag("T", mods: new[] { Mod(AgentStatType.MaxHP, StatModifierOp.Add, -50f) });

            Assert.AreEqual(1, AgentStats.From(_arch, Tags(tag)).MaxHP);
        }

        [Test]
        public void 다른_스탯의_보정은_영향을_주지_않는다()
        {
            var tag = _f.Tag("T", mods: new[] { Mod(AgentStatType.KnockbackMultiplier, StatModifierOp.Multiply, 0.5f) });

            var s = AgentStats.From(_arch, Tags(tag));

            Assert.AreEqual(0.5f, s.KnockbackMultiplier, 1e-5f);
            Assert.AreEqual(4f, s.MoveSpeed, 1e-5f);
        }

        [Test]
        public void TagSet은_4개_이상이면_에러로그후_앞_3개만_사용()
        {
            var a = _f.Tag("A"); var b = _f.Tag("B"); var c = _f.Tag("C"); var d = _f.Tag("D");
            LogAssert.Expect(LogType.Error, new Regex("상한"));

            var set = Tags(a, b, c, d);

            Assert.AreEqual(3, set.Tags.Count);
        }

        [Test]
        public void TagSet_면역과_플래그는_전_태그의_OR로_접힌다()
        {
            var t1 = _f.Tag("T1", immunities: DamageType.Spike, flags: SpecialFlag.Hover);
            var t2 = _f.Tag("T2", immunities: DamageType.Arrow);

            var set = Tags(t1, t2);

            Assert.IsTrue(set.IsImmuneTo(DamageType.Spike));
            Assert.IsTrue(set.IsImmuneTo(DamageType.Arrow));
            Assert.IsFalse(set.IsImmuneTo(DamageType.Hammer));
            Assert.IsTrue(set.HasFlag(SpecialFlag.Hover));
            Assert.IsFalse(set.HasFlag(SpecialFlag.Glide));
        }

        // ── TraversalProfile ────────────────────────────────────────────────

        [Test]
        public void TraversalProfile_태그없음_또는_null은_기본_프로파일()
        {
            foreach (var p in new[] { TraversalProfile.From(null), TraversalProfile.From(TagSet.Empty) })
            {
                Assert.AreEqual(1, p.MaxJumpHeight);
                Assert.AreEqual(2, p.MaxJumpDistance);
                Assert.AreEqual(4, p.MaxFallHeight);
                Assert.AreEqual(DamageType.None, p.Immunities);
                Assert.IsFalse(p.RecklessDrop);
            }
        }

        [Test]
        public void TraversalProfile_지름길중독은_낙하허용치_무제한()
        {
            var tag = _f.Tag("R", flags: SpecialFlag.RecklessDrop);

            var p = TraversalProfile.From(Tags(tag));

            Assert.IsTrue(p.RecklessDrop);
            Assert.AreEqual(int.MaxValue, p.MaxFallHeight);
        }

        [Test]
        public void TraversalProfile_면역_마스크가_태그에서_파생된다()
        {
            var tag = _f.Tag("I", immunities: DamageType.Spike | DamageType.Hammer);

            var p = TraversalProfile.From(Tags(tag));

            Assert.AreEqual(DamageType.Spike | DamageType.Hammer, p.Immunities);
        }

        [Test]
        public void TraversalProfile_CacheKey는_능력이_다르면_달라지고_같으면_같다()
        {
            var a = new TraversalProfile(1, 2, 4, DamageType.None, false);
            var same = new TraversalProfile(1, 2, 4, DamageType.None, false);
            var diffJump = new TraversalProfile(3, 2, 4, DamageType.None, false);
            var diffImm = new TraversalProfile(1, 2, 4, DamageType.Spike, false);
            var diffReckless = new TraversalProfile(1, 2, int.MaxValue, DamageType.None, true);

            Assert.AreEqual(a.CacheKey, same.CacheKey);
            Assert.AreNotEqual(a.CacheKey, diffJump.CacheKey);
            Assert.AreNotEqual(a.CacheKey, diffImm.CacheKey);
            Assert.AreNotEqual(a.CacheKey, diffReckless.CacheKey);
        }
    }
}
