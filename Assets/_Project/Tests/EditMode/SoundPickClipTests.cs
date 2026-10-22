using NUnit.Framework;
using UnityEngine;

namespace ReTrap.Tests
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  SoundPickClipTests — SoundManager.PickClip 클립 선택 순수 로직 (SPEC-21)
    // ═══════════════════════════════════════════════════════════════════════════

    public class SoundPickClipTests
    {
        private AudioClip _a;   // 엔트리(전역) 클립
        private AudioClip _b;   // 함정별 override 클립
        private AudioClip _c;

        [SetUp]
        public void SetUp()
        {
            _a = AudioClip.Create("clipA", 1, 1, 44100, false);
            _b = AudioClip.Create("clipB", 1, 1, 44100, false);
            _c = AudioClip.Create("clipC", 1, 1, 44100, false);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_a);
            Object.DestroyImmediate(_b);
            Object.DestroyImmediate(_c);
        }

        [Test]
        public void override가_있으면_엔트리보다_우선()
        {
            Assert.AreSame(_b, SoundManager.PickClip(new[] { _b }, new[] { _a }));
        }

        [Test]
        public void override_다수면_override_안에서만_선택()
        {
            var over = new[] { _b, _c };
            for (int i = 0; i < 30; i++)
            {
                AudioClip got = SoundManager.PickClip(over, new[] { _a });
                Assert.IsTrue(got == _b || got == _c);
            }
        }

        [Test]
        public void override_null_빈배열_널원소만이면_엔트리로_폴백()
        {
            var entry = new[] { _a };
            Assert.AreSame(_a, SoundManager.PickClip(null, entry));
            Assert.AreSame(_a, SoundManager.PickClip(new AudioClip[0], entry));
            Assert.AreSame(_a, SoundManager.PickClip(new AudioClip[] { null }, entry));
        }

        [Test]
        public void override_널원소는_건너뛴다()
        {
            Assert.AreSame(_b, SoundManager.PickClip(new AudioClip[] { null, _b, null }, new[] { _a }));
        }

        [Test]
        public void 둘_다_없으면_null()
        {
            Assert.IsNull(SoundManager.PickClip(null, null));
            Assert.IsNull(SoundManager.PickClip(new AudioClip[0], new AudioClip[0]));
            Assert.IsNull(SoundManager.PickClip(new AudioClip[] { null }, new AudioClip[] { null }));
        }
    }
}
