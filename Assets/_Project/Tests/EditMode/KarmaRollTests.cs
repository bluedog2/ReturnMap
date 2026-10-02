using NUnit.Framework;

namespace ReTrap.Tests
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  KarmaRollTests — 함정 변이(카르마) 굴림 순수 함수 테스트
    // ═══════════════════════════════════════════════════════════════════════════

    public class KarmaRollTests
    {
        [Test]
        public void CellHash_같은_입력은_같은_값()
        {
            Assert.AreEqual(TrapMutationManager.CellHash(12345, 7, 3),
                            TrapMutationManager.CellHash(12345, 7, 3));
        }

        [Test]
        public void CellHash_좌표나_시드가_다르면_값이_달라진다()
        {
            int baseHash = TrapMutationManager.CellHash(100, 5, 5);
            Assert.AreNotEqual(baseHash, TrapMutationManager.CellHash(100, 6, 5), "x 변경");
            Assert.AreNotEqual(baseHash, TrapMutationManager.CellHash(100, 5, 6), "y 변경");
            Assert.AreNotEqual(baseHash, TrapMutationManager.CellHash(101, 5, 5), "시드 변경");
        }

        [Test]
        public void CellHash_오버플로_좌표에서도_예외없이_결정적()
        {
            int a = TrapMutationManager.CellHash(int.MaxValue, 100000, -100000);
            int b = TrapMutationManager.CellHash(int.MaxValue, 100000, -100000);
            Assert.AreEqual(a, b);
        }

        [Test]
        public void RollState_같은_해시는_같은_상태()
        {
            for (int h = -50; h < 50; h++)
                Assert.AreEqual(TrapMutationManager.RollState(h, 0.5f, 0.2f, 0.2f),
                                TrapMutationManager.RollState(h, 0.5f, 0.2f, 0.2f));
        }

        [Test]
        public void RollState_10만회_분포가_확률과_일치한다()
        {
            const int n = 100000;
            int normal = 0, dud = 0, critical = 0, beneficial = 0;

            for (int i = 0; i < n; i++)
            {
                switch (TrapMutationManager.RollState(TrapMutationManager.CellHash(777, i, i * 31), 0.5f, 0.2f, 0.2f))
                {
                    case TrapState.Normal:     normal++;     break;
                    case TrapState.Dud:        dud++;        break;
                    case TrapState.Critical:   critical++;   break;
                    case TrapState.Beneficial: beneficial++; break;
                }
            }

            Assert.AreEqual(0.5, normal     / (double)n, 0.01, "Normal");
            Assert.AreEqual(0.2, dud        / (double)n, 0.01, "Dud");
            Assert.AreEqual(0.2, critical   / (double)n, 0.01, "Critical");
            Assert.AreEqual(0.1, beneficial / (double)n, 0.01, "Beneficial");
        }

        [Test]
        public void RollState_확률합이_1이면_Beneficial은_나오지_않는다()
        {
            for (int i = 0; i < 20000; i++)
            {
                var s = TrapMutationManager.RollState(i * 7919, 0.5f, 0.3f, 0.2f);
                Assert.AreNotEqual(TrapState.Beneficial, s, $"hash={i * 7919}");
            }
        }

        [Test]
        public void RollState_normal이_1이면_항상_Normal()
        {
            for (int i = 0; i < 2000; i++)
                Assert.AreEqual(TrapState.Normal, TrapMutationManager.RollState(i, 1f, 0f, 0f));
        }

        [Test]
        public void 같은_시드와_칸이면_굴림_순서와_무관하게_같은_상태()
        {
            // 칸을 정방향/역방향으로 굴려도 칸별 결과가 동일해야 한다 (순서·개수 독립)
            var forward = new TrapState[50];
            for (int x = 0; x < 50; x++)
                forward[x] = TrapMutationManager.RollState(TrapMutationManager.CellHash(42, x, 2), 0.5f, 0.2f, 0.2f);

            for (int x = 49; x >= 0; x--)
                Assert.AreEqual(forward[x],
                    TrapMutationManager.RollState(TrapMutationManager.CellHash(42, x, 2), 0.5f, 0.2f, 0.2f), $"x={x}");
        }
    }
}
