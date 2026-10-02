using NUnit.Framework;

namespace ReTrap.Tests
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  TrapCooldownGaugeTests — 쿨다운 게이지 순수 함수 경계값
    // ═══════════════════════════════════════════════════════════════════════════

    public class TrapCooldownGaugeTests
    {
        [Test]
        public void Progress_시작과_끝_경계()
        {
            Assert.AreEqual(0f, TrapCooldownGauge.Progress(5f, 2f, 5f), 1e-5f);
            Assert.AreEqual(1f, TrapCooldownGauge.Progress(5f, 2f, 7f), 1e-5f);
        }

        [Test]
        public void Progress_범위를_벗어나면_0과_1로_클램프()
        {
            Assert.AreEqual(0f, TrapCooldownGauge.Progress(5f, 2f, 1f), 1e-5f, "시작 이전");
            Assert.AreEqual(1f, TrapCooldownGauge.Progress(5f, 2f, 99f), 1e-5f, "종료 이후");
        }

        [Test]
        public void Progress_간격이_0이하면_0()
        {
            Assert.AreEqual(0f, TrapCooldownGauge.Progress(0f, 0f, 3f));
            Assert.AreEqual(0f, TrapCooldownGauge.Progress(0f, -1f, 3f));
        }

        [Test]
        public void IsVisiblePhase_Play와_Verification만_표시()
        {
            Assert.IsTrue(TrapCooldownGauge.IsVisiblePhase(GamePhase.Play));
            Assert.IsTrue(TrapCooldownGauge.IsVisiblePhase(GamePhase.Verification));
            Assert.IsFalse(TrapCooldownGauge.IsVisiblePhase(GamePhase.Build));
        }
    }
}
