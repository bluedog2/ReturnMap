using NUnit.Framework;
using UnityEngine;

namespace ReTrap.Tests
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  AgentDamageSystemTests — 함정 → AI 피해 게이트웨이 (면역/플래그/HP)
    // ═══════════════════════════════════════════════════════════════════════════
    //  VerificationAgent 없이 AgentHealth + AgentContext 만 붙인 GameObject 로 검증한다.
    //  (HP 가 0 이 되면 _body?.Kill() 은 null 이라 건너뛴다.)

    public class AgentDamageSystemTests
    {
        private TestFactory _f;
        private GameObject _go;
        private AgentContext _ctx;
        private AgentHealth _health;

        [SetUp]
        public void SetUp()
        {
            _f = new TestFactory();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            _f.DestroyAll();
        }

        /// <summary>태그를 가진 개체를 만든다 (HP 3 아키타입).</summary>
        private void Spawn(params AITagDefinition[] tags)
        {
            _go = new GameObject("TestAgent");
            _health = _go.AddComponent<AgentHealth>();   // AgentContext.Awake 가 조회하므로 먼저 부착
            _ctx = _go.AddComponent<AgentContext>();
            _ctx.Initialize(_f.Archetype(maxHp: 3), new TagSet(tags));
        }

        [Test]
        public void 면역_타입은_Immune이고_HP가_깎이지_않는다()
        {
            Spawn(_f.Tag("I", immunities: DamageType.Spike));

            var r = AgentDamageSystem.TryDamage(_ctx, DamageType.Spike, Vector2.zero);

            Assert.AreEqual(DamageResult.Immune, r);
            Assert.AreEqual(3, _health.CurrentHP);
        }

        [Test]
        public void 면역이_아닌_타입은_피해를_입는다()
        {
            Spawn(_f.Tag("I", immunities: DamageType.Spike));

            var r = AgentDamageSystem.TryDamage(_ctx, DamageType.Arrow, Vector2.zero);

            Assert.AreEqual(DamageResult.Damaged, r);
            Assert.AreEqual(2, _health.CurrentHP);
        }

        [Test]
        public void 태그가_없으면_피해를_받고_HP가_소진되면_Killed()
        {
            Spawn();

            Assert.AreEqual(DamageResult.Killed,
                AgentDamageSystem.TryDamage(_ctx, DamageType.Hammer, Vector2.zero, amount: 3));
            Assert.IsTrue(_health.IsDead);
        }

        [Test]
        public void 피격_직후_무적시간_동안_추가_피해는_Immune()
        {
            Spawn();

            Assert.AreEqual(DamageResult.Damaged, AgentDamageSystem.TryDamage(_ctx, DamageType.Spike, Vector2.zero));
            Assert.AreEqual(DamageResult.Immune,  AgentDamageSystem.TryDamage(_ctx, DamageType.Spike, Vector2.zero));
            Assert.AreEqual(2, _health.CurrentHP, "무적 중 두 번째 피해는 HP 를 깎지 않는다");
        }

        [Test]
        public void Hover는_가시만_무시한다()
        {
            Spawn(_f.Tag("H", flags: SpecialFlag.Hover));

            Assert.AreEqual(DamageResult.Immune,  AgentDamageSystem.TryDamage(_ctx, DamageType.Spike, Vector2.zero));
            Assert.AreEqual(DamageResult.Damaged, AgentDamageSystem.TryDamage(_ctx, DamageType.Arrow, Vector2.zero));
        }

        [Test]
        public void 철벽방패는_전방_화살만_막고_후방은_피해()
        {
            Spawn(_f.Tag("S", flags: SpecialFlag.FrontShieldOnly));
            _ctx.FacingSign = 1; // 오른쪽을 봄

            Assert.AreEqual(DamageResult.Immune,
                AgentDamageSystem.TryDamage(_ctx, DamageType.Arrow, new Vector2(5f, 0f)), "전방(오른쪽)");
            Assert.AreEqual(3, _health.CurrentHP);

            Assert.AreEqual(DamageResult.Damaged,
                AgentDamageSystem.TryDamage(_ctx, DamageType.Arrow, new Vector2(-5f, 0f)), "후방(왼쪽)");
            Assert.AreEqual(2, _health.CurrentHP);
        }

        [Test]
        public void 스펀지몸은_화살을_Absorbed로_흡수하고_HP는_그대로()
        {
            Spawn(_f.Tag("A", flags: SpecialFlag.AbsorbProjectile));

            Assert.AreEqual(DamageResult.Absorbed, AgentDamageSystem.TryDamage(_ctx, DamageType.Arrow, Vector2.zero));
            Assert.AreEqual(3, _health.CurrentHP);
        }

        [Test]
        public void 쉴드가_있으면_HP보다_먼저_소모된다()
        {
            Spawn();
            _health.AddShield(2);

            var r = AgentDamageSystem.TryDamage(_ctx, DamageType.Spike, Vector2.zero, amount: 1);

            Assert.AreEqual(DamageResult.Damaged, r);
            Assert.AreEqual(3, _health.CurrentHP);
            Assert.AreEqual(1, _health.Shield);
        }
    }
}
