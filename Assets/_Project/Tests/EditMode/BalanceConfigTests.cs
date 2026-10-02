using NUnit.Framework;
using UnityEngine;

namespace ReTrap.Tests
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  BalanceConfigTests — BalanceConfig 기본값이 기존 씬 값과 동작 동일한지 검증
    // ═══════════════════════════════════════════════════════════════════════════

    public class BalanceConfigTests
    {
        private BalanceConfig _cfg;

        [SetUp]
        public void SetUp() => _cfg = ScriptableObject.CreateInstance<BalanceConfig>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_cfg);

        [Test]
        public void 기본값으로_RollState가_기존_하드코딩_값과_동일하다()
        {
            for (int i = 0; i < 10000; i++)
            {
                int h = TrapMutationManager.CellHash(777, i, i * 31);
                Assert.AreEqual(
                    TrapMutationManager.RollState(h, 0.5f, 0.2f, 0.2f),
                    TrapMutationManager.RollState(h, _cfg.NormalChance, _cfg.DudChance, _cfg.CriticalChance),
                    $"i={i}");
            }
        }

        [Test]
        public void BeneficialChance는_나머지_확률이다()
        {
            Assert.AreEqual(0.1f, _cfg.BeneficialChance, 1e-4f);
        }

        [Test]
        public void 기본_검증시간과_보상은_기존_씬_값이다()
        {
            Assert.AreEqual(60f, _cfg.VerificationTimeLimit);
            Assert.AreEqual(10, _cfg.StageClearReward);
        }
    }
}
