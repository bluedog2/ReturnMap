using NUnit.Framework;

namespace ReTrap.Tests
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  Spec8AiTests — SPEC-8 「6. AI」 순수 로직 테스트
    //  (저주 Worsen · 넉백 칸수 · 가속 배율 상한 · 재화 도난)
    // ═══════════════════════════════════════════════════════════════════════════

    public class Spec8AiTests
    {
        private int _savedGlobes;

        [SetUp]
        public void SetUp()
        {
            // CurrencyService 는 정적 + PlayerPrefs 영속 — 원래 잔액을 보관하고 0 에서 시작한다.
            _savedGlobes = CurrencyService.Globes;
            CurrencyService.Steal(int.MaxValue);
        }

        [TearDown]
        public void TearDown()
        {
            CurrencyService.Steal(int.MaxValue);
            CurrencyService.Add(_savedGlobes);
        }

        // ── 저주 Worsen ───────────────────────────────────────────────────────

        [TestCase(TrapState.Dud,        TrapState.Normal)]
        [TestCase(TrapState.Normal,     TrapState.Critical)]
        [TestCase(TrapState.Critical,   TrapState.Critical)]
        [TestCase(TrapState.Beneficial, TrapState.Normal)]
        public void Worsen_한_단계_불리하게_바꾼다(TrapState input, TrapState expected)
        {
            Assert.AreEqual(expected, TrapMutationManager.Worsen(input));
        }

        // ── 넉백 칸수 ─────────────────────────────────────────────────────────

        [TestCase(1, 1f, 1)]
        [TestCase(1, 2f, 2)]
        [TestCase(1, 3f, 3)]
        [TestCase(0, 3f, 0)]
        [TestCase(1, -2f, 0)]
        public void ComputeKnockbackCells_기본칸수와_배율로_후퇴_칸수를_구한다(int baseCells, float mult, int expected)
        {
            Assert.AreEqual(expected, AgentDamageSystem.ComputeKnockbackCells(baseCells, mult));
        }

        // ── 가속 배율 ─────────────────────────────────────────────────────────

        [Test]
        public void ComputeMultiplier_시간에_따라_증가하고_상한을_넘지_않는다()
        {
            Assert.AreEqual(1f, AccelerateTrait.ComputeMultiplier(0f, 4f), 1e-4f);
            Assert.AreEqual(2f, AccelerateTrait.ComputeMultiplier(10f, 4f), 1e-4f);
            Assert.AreEqual(4f, AccelerateTrait.ComputeMultiplier(1000f, 4f), 1e-4f);
            Assert.AreEqual(2.5f, AccelerateTrait.ComputeMultiplier(1000f, 2.5f), 1e-4f);
        }

        [Test]
        public void ComputeMultiplier_상한이_1이면_항상_1()
        {
            Assert.AreEqual(1f, AccelerateTrait.ComputeMultiplier(0f, 1f), 1e-4f);
            Assert.AreEqual(1f, AccelerateTrait.ComputeMultiplier(1000f, 1f), 1e-4f);
        }

        // ── 재화 도난 ─────────────────────────────────────────────────────────

        [Test]
        public void Steal_잔액이_충분하면_요청량만큼_차감한다()
        {
            CurrencyService.Add(10);

            int taken = CurrencyService.Steal(5);

            Assert.AreEqual(5, taken);
            Assert.AreEqual(5, CurrencyService.Globes);
        }

        [Test]
        public void Steal_잔액이_부족하면_0까지만_차감한다()
        {
            CurrencyService.Add(3);

            int taken = CurrencyService.Steal(5);

            Assert.AreEqual(3, taken);
            Assert.AreEqual(0, CurrencyService.Globes);
        }

        [Test]
        public void Steal_잔액이_0이거나_요청이_0이하면_아무것도_차감하지_않는다()
        {
            Assert.AreEqual(0, CurrencyService.Steal(5));
            Assert.AreEqual(0, CurrencyService.Globes);

            CurrencyService.Add(4);
            Assert.AreEqual(0, CurrencyService.Steal(0));
            Assert.AreEqual(0, CurrencyService.Steal(-3));
            Assert.AreEqual(4, CurrencyService.Globes);
        }

        [Test]
        public void Steal_변경_이벤트에_차감_후_잔액을_발행한다()
        {
            CurrencyService.Add(8);
            int last = -1;
            void Handler(int v) => last = v;
            CurrencyService.OnChanged += Handler;
            try { CurrencyService.Steal(5); }
            finally { CurrencyService.OnChanged -= Handler; }

            Assert.AreEqual(3, last);
        }
    }
}
