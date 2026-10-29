using NUnit.Framework;

namespace ReTrap.Tests
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  RingGaugeFrameTests — 픽셀 링 게이지 채움 단계 선택 테스트
    // ═══════════════════════════════════════════════════════════════════════════

    public class RingGaugeFrameTests
    {
        [Test]
        public void 빈_게이지와_가득_찬_게이지는_양끝_프레임()
        {
            Assert.AreEqual(0, SpriteUtil.RingFrameIndex(0f, 9));
            Assert.AreEqual(8, SpriteUtil.RingFrameIndex(1f, 9));
        }

        [Test]
        public void 진행도는_가장_가까운_단계로_반올림()
        {
            Assert.AreEqual(4, SpriteUtil.RingFrameIndex(0.5f, 9));
            Assert.AreEqual(1, SpriteUtil.RingFrameIndex(0.1f, 9));  // 0.8 → 1
            Assert.AreEqual(0, SpriteUtil.RingFrameIndex(0.05f, 9)); // 0.4 → 0
        }

        [Test]
        public void 범위_밖_진행도와_프레임_1장_이하는_안전()
        {
            Assert.AreEqual(0, SpriteUtil.RingFrameIndex(-1f, 9));
            Assert.AreEqual(8, SpriteUtil.RingFrameIndex(2f, 9));
            Assert.AreEqual(0, SpriteUtil.RingFrameIndex(0.7f, 1));
            Assert.AreEqual(0, SpriteUtil.RingFrameIndex(0.7f, 0));
        }
    }
}
