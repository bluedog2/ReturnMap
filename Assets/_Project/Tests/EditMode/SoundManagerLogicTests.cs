using NUnit.Framework;
using UnityEngine;

namespace ReTrap.Tests
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  SoundManagerLogicTests — dB 변환·보이스 선택·BGM 선택 순수 로직
    // ═══════════════════════════════════════════════════════════════════════════

    public class SoundManagerLogicTests
    {
        private const int Count = 4;
        private float[] _start;
        private float[] _end;
        private int[]   _entry;
        private int[]   _prio;

        [SetUp]
        public void SetUp()
        {
            _start = new float[Count];
            _end   = new float[Count];
            _entry = new[] { -1, -1, -1, -1 };
            _prio  = new int[Count];
        }

        private void Occupy(int i, int entry, int prio, float start, float end)
        {
            _entry[i] = entry; _prio[i] = prio; _start[i] = start; _end[i] = end;
        }

        private int Choose(float now, int entry, int max, int prio, out bool stolen)
            => SoundManager.ChooseVoice(_start, _end, _entry, _prio, Count, now, entry, max, prio, out stolen);

        [Test]
        public void LinearToDb_대표값과_하한()
        {
            Assert.AreEqual(0f,       SoundManager.LinearToDb(1f),   1e-4f);
            Assert.AreEqual(-6.0206f, SoundManager.LinearToDb(0.5f), 1e-3f);
            Assert.AreEqual(-80f,     SoundManager.LinearToDb(0f),   1e-4f);
            Assert.AreEqual(-80f,     SoundManager.LinearToDb(0.0001f), 1e-4f);
            Assert.AreEqual(-4.437f,  SoundManager.LinearToDb(0.6f), 1e-2f);
        }

        [Test]
        public void ChooseVoice_빈_보이스가_있으면_스틸_없이_첫_빈칸()
        {
            Occupy(0, 0, 100, 0f, 5f);
            int v = Choose(1f, 0, 3, 100, out bool stolen);
            Assert.AreEqual(1, v);
            Assert.IsFalse(stolen);
        }

        [Test]
        public void ChooseVoice_끝난_보이스는_빈칸으로_취급()
        {
            for (int i = 0; i < Count; i++) Occupy(i, 0, 100, 0f, 1f);
            int v = Choose(1f, 0, 99, 100, out bool stolen);   // end == now → 비활성
            Assert.AreEqual(0, v);
            Assert.IsFalse(stolen);
        }

        [Test]
        public void ChooseVoice_같은_SFX_상한_초과시_가장_오래된_것_스틸()
        {
            Occupy(0, 0, 100, 2f, 9f);
            Occupy(1, 0, 100, 1f, 9f);   // 가장 오래됨
            Occupy(2, 0, 100, 3f, 9f);
            int v = Choose(4f, 0, 3, 100, out bool stolen);
            Assert.AreEqual(1, v);
            Assert.IsTrue(stolen);
        }

        [Test]
        public void ChooseVoice_풀_가득_동률이면_가장_오래된_것_스틸()
        {
            Occupy(0, 0, 100, 3f, 9f);
            Occupy(1, 0, 100, 1f, 9f);
            Occupy(2, 0, 100, 2f, 9f);
            Occupy(3, 0, 100, 4f, 9f);
            int v = Choose(5f, 0, 99, 100, out bool stolen);
            Assert.AreEqual(1, v);
            Assert.IsTrue(stolen);
        }

        [Test]
        public void ChooseVoice_풀_가득_고우선순위는_최저_우선순위를_스틸()
        {
            Occupy(0, 0, 100, 1f, 9f);
            Occupy(1, 0, 50,  2f, 9f);   // 최저
            Occupy(2, 0, 100, 3f, 9f);
            Occupy(3, 0, 100, 4f, 9f);
            int v = Choose(5f, 1, 1, 200, out bool stolen);
            Assert.AreEqual(1, v);
            Assert.IsTrue(stolen);
        }

        [Test]
        public void ChooseVoice_풀_가득_저우선순위는_버림()
        {
            for (int i = 0; i < Count; i++) Occupy(i, 1, 200, i, 9f);
            int v = Choose(5f, 0, 99, 100, out bool stolen);
            Assert.AreEqual(-1, v);
            Assert.IsFalse(stolen);
        }

        [Test]
        public void SelectBgm_검증_BGM_없으면_플레이_BGM_폴백()
        {
            var build = AudioClip.Create("b", 10, 1, 8000, false);
            var ver   = AudioClip.Create("v", 10, 1, 8000, false);
            var play  = AudioClip.Create("p", 10, 1, 8000, false);
            try
            {
                Assert.AreSame(build, SoundManager.SelectBgm(GamePhase.Build, build, ver, play));
                Assert.AreSame(ver,   SoundManager.SelectBgm(GamePhase.Verification, build, ver, play));
                Assert.AreSame(play,  SoundManager.SelectBgm(GamePhase.Verification, build, null, play));
                Assert.AreSame(play,  SoundManager.SelectBgm(GamePhase.Play, build, ver, play));
            }
            finally
            {
                Object.DestroyImmediate(build);
                Object.DestroyImmediate(ver);
                Object.DestroyImmediate(play);
            }
        }
    }
}
