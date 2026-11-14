using NUnit.Framework;

namespace ReTrap.Tests
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  PlayerAnimAirStateTests — 수직 속도 → 공중 애니 상태(Rise/Apex/Fall) 경계 검사
    // ═══════════════════════════════════════════════════════════════════════════

    public class PlayerAnimAirStateTests
    {
        private const float Hang = 1f;

        [Test]
        public void VyEqualsHang_IsRise()
            => Assert.AreEqual(PlayerAnimState.Rise, PlayerAnimationFSM.AirStateFor(Hang, Hang));

        [Test]
        public void VyJustBelowHang_IsApex()
            => Assert.AreEqual(PlayerAnimState.Apex, PlayerAnimationFSM.AirStateFor(Hang - 0.001f, Hang));

        [Test]
        public void VyZero_IsApex()
            => Assert.AreEqual(PlayerAnimState.Apex, PlayerAnimationFSM.AirStateFor(0f, Hang));

        [Test]
        public void VyEqualsMinusHang_IsFall()
            => Assert.AreEqual(PlayerAnimState.Fall, PlayerAnimationFSM.AirStateFor(-Hang, Hang));

        [Test]
        public void VyJustAboveMinusHang_IsApex()
            => Assert.AreEqual(PlayerAnimState.Apex, PlayerAnimationFSM.AirStateFor(-Hang + 0.001f, Hang));
    }
}
