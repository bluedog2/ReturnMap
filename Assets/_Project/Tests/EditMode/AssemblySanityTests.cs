using NUnit.Framework;

namespace ReTrap.Tests
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  AssemblySanityTests — asmdef 경계 스모크 테스트
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// ReTrap.Runtime 어셈블리를 테스트 어셈블리에서 참조·호출할 수 있는지 확인하는 최소 테스트.
    /// </summary>
    public class AssemblySanityTests
    {
        [Test]
        public void TrapCooldownGauge_Progress_는_중간값을_반환한다()
        {
            Assert.AreEqual(0.5f, TrapCooldownGauge.Progress(0f, 1f, 0.5f), 1e-5f);
        }

        [Test]
        public void TrapCooldownGauge_Build_페이즈에서는_숨김()
        {
            Assert.IsFalse(TrapCooldownGauge.IsVisiblePhase(GamePhase.Build));
        }
    }
}
