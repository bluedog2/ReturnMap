using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace ReTrap.Tests
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  Bug2Tests — BUG-2 재현: 함정이 없을 때 기본 이동 능력으로 스폰→골 경로가 존재해야 한다
    // ═══════════════════════════════════════════════════════════════════════════
    //  실제 스테이지 JSON 을 읽어 AgentWaveController 와 같은 방식(NavGrid.Build → FindPath)으로
    //  검증한다. EditMode 에서는 TrapSlotRegistry 에 등록된 슬롯이 없어 순수 지형만 반영된다.

    public class Bug2Tests
    {
        private static MapData LoadStage(string fileName)
        {
            string path = Path.Combine(Application.dataPath, "StreamingAssets", "Maps", fileName);
            Assert.IsTrue(File.Exists(path), $"맵 파일 없음: {path}");
            var map = MapData.FromJson(File.ReadAllText(path));
            Assert.IsNotNull(map, $"맵 파싱 실패: {fileName}");
            return map;
        }

        private static void AssertPathExists(string fileName)
        {
            var map = LoadStage(fileName);
            var p = AIBehaviorParams.Default;
            var profile = TraversalProfile.Default;

            var grid = NavGrid.Build(map, p, profile, null);
            var path = new AStarPathPlanner().FindPath(grid, map.spawnPoint, map.goalPoint, p, profile);

            Assert.IsNotNull(path,
                $"{fileName}: 기본 능력으로 경로가 없음 (spawn={map.spawnPoint}, goal={map.goalPoint})");
            Assert.Greater(path.Count, 0,
                $"{fileName}: 경로가 비어 있음 (spawn={map.spawnPoint}, goal={map.goalPoint})");
        }

        [Test]
        public void Bug2_Stage02_기본능력으로_스폰에서_골까지_경로가_존재한다()
            => AssertPathExists("stage_02.json");

        [Test]
        public void Bug2_대조_Stage01_경로존재()
            => AssertPathExists("stage_01.json");
    }
}
