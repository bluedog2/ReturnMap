using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ReTrap.Tests
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  SettingsServiceTests — 음량 단계 변환 / 저장 클램프 / 해상도 필터
    // ═══════════════════════════════════════════════════════════════════════════
    //  PlayerPrefs 를 쓰므로 SetUp 에서 ReTrap.Settings.* 키를 백업·삭제하고 TearDown 에서 복원한다.

    public class SettingsServiceTests
    {
        private static readonly string[] FloatKeys =
        {
            "ReTrap.Settings.MasterVolume", "ReTrap.Settings.BgmVolume", "ReTrap.Settings.SfxVolume",
        };
        private static readonly string[] IntKeys =
        {
            "ReTrap.Settings.ResWidth", "ReTrap.Settings.ResHeight", "ReTrap.Settings.Fullscreen",
        };

        private readonly Dictionary<string, float> _floatBackup = new Dictionary<string, float>();
        private readonly Dictionary<string, int>   _intBackup   = new Dictionary<string, int>();

        [SetUp]
        public void SetUp()
        {
            _floatBackup.Clear();
            _intBackup.Clear();
            foreach (var k in FloatKeys)
            {
                if (PlayerPrefs.HasKey(k)) _floatBackup[k] = PlayerPrefs.GetFloat(k);
                PlayerPrefs.DeleteKey(k);
            }
            foreach (var k in IntKeys)
            {
                if (PlayerPrefs.HasKey(k)) _intBackup[k] = PlayerPrefs.GetInt(k);
                PlayerPrefs.DeleteKey(k);
            }
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var k in FloatKeys) PlayerPrefs.DeleteKey(k);
            foreach (var k in IntKeys)   PlayerPrefs.DeleteKey(k);
            foreach (var kv in _floatBackup) PlayerPrefs.SetFloat(kv.Key, kv.Value);
            foreach (var kv in _intBackup)   PlayerPrefs.SetInt(kv.Key, kv.Value);
            PlayerPrefs.Save();
        }

        [Test]
        public void 선형과_단계는_서로_변환된다()
        {
            Assert.AreEqual(12, SettingsService.StepFromLinear(0.6f));
            Assert.AreEqual(0.5f, SettingsService.LinearFromStep(10), 1e-5f);
            Assert.AreEqual(20, SettingsService.StepFromLinear(5f));
            Assert.AreEqual(0, SettingsService.StepFromLinear(-1f));
        }

        [Test]
        public void SetVolume_은_0에서_1로_클램프해_저장한다()
        {
            SettingsService.SetVolume(SoundBus.Bgm, 1.7f);
            SettingsService.SetVolume(SoundBus.Sfx, -0.3f);

            Assert.IsTrue(SettingsService.TryGetVolume(SoundBus.Bgm, out float bgm));
            Assert.IsTrue(SettingsService.TryGetVolume(SoundBus.Sfx, out float sfx));
            Assert.AreEqual(1f, bgm, 1e-5f);
            Assert.AreEqual(0f, sfx, 1e-5f);
        }

        [Test]
        public void 저장값이_없으면_TryGetVolume_은_false()
        {
            Assert.IsFalse(SettingsService.TryGetVolume(SoundBus.Master, out _));
        }

        [Test]
        public void 해상도_필터는_모니터_이하만_오름차순으로_남긴다()
        {
            Vector2Int[] r = SettingsService.FilterResolutions(1920, 1080);

            Assert.AreEqual(3, r.Length);
            Assert.AreEqual(new Vector2Int(1280, 720), r[0]);
            Assert.AreEqual(new Vector2Int(1920, 1080), r[2]);
        }

        [Test]
        public void 해상도_필터_결과가_비면_1280x720_하나()
        {
            Vector2Int[] r = SettingsService.FilterResolutions(800, 600);

            Assert.AreEqual(1, r.Length);
            Assert.AreEqual(new Vector2Int(1280, 720), r[0]);
        }

        [Test]
        public void SetDisplay_는_해상도와_전체화면을_저장한다()
        {
            SettingsService.SetDisplay(1280, 720, false);

            Assert.IsTrue(SettingsService.TryGetResolution(out Vector2Int res));
            Assert.AreEqual(new Vector2Int(1280, 720), res);
            Assert.IsTrue(SettingsService.TryGetFullscreen(out bool fs));
            Assert.IsFalse(fs);
        }
    }
}
