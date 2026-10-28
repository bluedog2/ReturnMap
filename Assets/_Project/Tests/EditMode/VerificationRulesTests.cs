using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ReTrap.Tests
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  VerificationRulesTests — 검증 규칙(시간·마릿수·라이프) / 리포트 / 연구 효과 분기
    // ═══════════════════════════════════════════════════════════════════════════
    //  ResearchBoardState 의 정적 투자 상태는 SetUp 에서 리플렉션으로 채우고
    //  TearDown 에서 null 로 되돌려 다음 접근 때 디스크에서 다시 로드되게 한다.

    public class VerificationRulesTests
    {
        private TestFactory _f;
        private readonly List<Object> _nodes = new List<Object>();

        [SetUp]
        public void SetUp() => _f = new TestFactory();

        [TearDown]
        public void TearDown()
        {
            _f.DestroyAll();
            foreach (var o in _nodes) if (o != null) Object.DestroyImmediate(o);
            _nodes.Clear();
            SetResearchLevels(null);
        }

        // ── 헬퍼 ──────────────────────────────────────────────────────────────

        /// <summary>범위·breach 를 지정한 빈 스폰 테이블.</summary>
        private StageSpawnTable MakeTable(int breach, float tMin, float tMax, int cMin, int cMax)
        {
            var table = _f.Table(new (AgentArchetype, int, AITagDefinition[], int)[0],
                                 new (AITagDefinition, float)[0]);
            var so = new SerializedObject(table);
            so.FindProperty("breachThreshold").intValue = breach;
            so.FindProperty("timeLimitMin").floatValue  = tMin;
            so.FindProperty("timeLimitMax").floatValue  = tMax;
            so.FindProperty("agentCountMin").intValue   = cMin;
            so.FindProperty("agentCountMax").intValue   = cMax;
            so.ApplyModifiedPropertiesWithoutUndo();
            return table;
        }

        private ResearchNodeDefinition MakeNode(string id, ResearchEffect effect, float[] deltas,
                                                AITagDefinition[] targets = null)
        {
            var node = ScriptableObject.CreateInstance<ResearchNodeDefinition>();
            var costs = new int[deltas.Length];
            for (int i = 0; i < costs.Length; i++) costs[i] = 50;
            node.EditorInit(id, ResearchSchool.EarthTruth, id, "", targets ?? new AITagDefinition[0],
                            deltas, costs, effect);
            _nodes.Add(node);
            return node;
        }

        private static void SetResearchLevels(Dictionary<string, int> levels)
        {
            typeof(ResearchBoardState)
                .GetField("_levels", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, levels);
        }

        // ── 라이프 규칙 (#9) ──────────────────────────────────────────────────

        [Test]
        public void 라이프는_breach_기본값에_연구_보너스를_더한다()
        {
            var table = MakeTable(1, 40f, 60f, 7, 10);

            Assert.AreEqual(1, VerificationRules.Resolve(table, 10, 60f, 0, 0f, 0).Life);
            Assert.AreEqual(2, VerificationRules.Resolve(table, 10, 60f, 1, 0f, 0).Life);
            Assert.AreEqual(3, VerificationRules.Resolve(table, 10, 60f, 2, 0f, 0).Life);
        }

        [Test]
        public void 라이프는_투입_마릿수를_넘지_않는다()
        {
            var stage04 = MakeTable(3, 40f, 60f, 5, 5);
            Assert.AreEqual(5, VerificationRules.Resolve(stage04, 5, 60f, 2, 0f, 0).Life, "3+2 = 5 ≤ 5");
            Assert.AreEqual(5, VerificationRules.Resolve(stage04, 5, 60f, 9, 0f, 0).Life, "보너스가 커도 5 로 자름");

            var tiny = MakeTable(1, 40f, 60f, 7, 10);
            Assert.AreEqual(2, VerificationRules.Resolve(tiny, 2, 60f, 5, 0f, 0).Life, "총원 2 면 라이프도 2");
        }

        [Test]
        public void 라이프는_최소_1이다()
        {
            var table = MakeTable(0, 40f, 60f, 7, 10);
            Assert.AreEqual(1, VerificationRules.Resolve(table, 10, 60f, 0, 0f, 0).Life);
        }

        // ── 시간 규칙 (#10) ───────────────────────────────────────────────────

        [Test]
        public void 시간은_기본_시간에서_단축을_빼고_범위로_자른다()
        {
            var table = MakeTable(1, 40f, 60f, 7, 10);

            Assert.AreEqual(60f, VerificationRules.Resolve(table, 10, 60f, 0, 0f, 0).TimeLimit, 1e-4f);
            Assert.AreEqual(55f, VerificationRules.Resolve(table, 10, 60f, 0, 5f, 0).TimeLimit, 1e-4f);
            Assert.AreEqual(40f, VerificationRules.Resolve(table, 10, 60f, 0, 20f, 0).TimeLimit, 1e-4f);
        }

        [Test]
        public void 시간은_하한_아래로_내려가지_않고_상한을_넘지_않는다()
        {
            var table = MakeTable(1, 40f, 60f, 7, 10);

            Assert.AreEqual(40f, VerificationRules.Resolve(table, 10, 60f, 0, 35f, 0).TimeLimit, 1e-4f, "하한 40");
            Assert.AreEqual(60f, VerificationRules.Resolve(table, 10, 90f, 0, 0f, 0).TimeLimit, 1e-4f, "상한 60");
        }

        // ── 마릿수 규칙 (#11) ─────────────────────────────────────────────────

        [Test]
        public void 마릿수는_총원에서_감소를_빼고_범위로_자른다()
        {
            var table = MakeTable(1, 40f, 60f, 7, 10);

            Assert.AreEqual(10, VerificationRules.Resolve(table, 10, 60f, 0, 0f, 0).AgentCount);
            Assert.AreEqual(7,  VerificationRules.Resolve(table, 10, 60f, 0, 0f, 3).AgentCount, "10-3 = 7");
            Assert.AreEqual(7,  VerificationRules.Resolve(table, 10, 60f, 0, 0f, 6).AgentCount, "하한 7");
        }

        [Test]
        public void 마릿수는_실제_로스터_총원을_넘지_않는다()
        {
            var table = MakeTable(1, 40f, 60f, 7, 10);

            Assert.AreEqual(10, VerificationRules.Resolve(table, 12, 60f, 0, 0f, 0).AgentCount, "상한 10");
            Assert.AreEqual(5,  VerificationRules.Resolve(table, 5, 60f, 0, 0f, 0).AgentCount, "총원 5 < 하한 7");
            Assert.AreEqual(5,  VerificationRules.Resolve(table, 5, 60f, 0, 0f, 3).AgentCount);
        }

        [Test]
        public void 테이블이_없으면_총원_그대로_연구_단축은_적용되지_않는다()
        {
            VerificationRuleSet r = VerificationRules.Resolve(null, 1, 60f, 0, 20f, 3);

            Assert.AreEqual(1, r.AgentCount);
            Assert.AreEqual(60f, r.TimeLimit, 1e-4f);
            Assert.AreEqual(1, r.Life);
        }

        // ── 리포트 (#8) ───────────────────────────────────────────────────────

        [Test]
        public void 시간_초과_사망은_처치로_세지_않는다()
        {
            var report = new VerificationReport();

            bool recorded = report.TryRecordDeath(false, true, "가시 (1,1)", "(1,1)");

            Assert.IsFalse(recorded);
            Assert.AreEqual(0, report.DeathEvents);
            Assert.AreEqual(1, report.TimedOutAttempts);
            Assert.AreEqual(0, report.TopKills(5).Count);
            Assert.AreEqual(0, report.TopCells(5).Count);
        }

        [Test]
        public void 도달한_시도는_처치로_세지_않는다()
        {
            var report = new VerificationReport();

            Assert.IsFalse(report.TryRecordDeath(true, false, "가시 (1,1)", "(1,1)"));
            Assert.AreEqual(0, report.DeathEvents);
        }

        [Test]
        public void 함정_사망은_라벨별_셀별로_집계되고_합이_처치와_같다()
        {
            var report = new VerificationReport();
            report.TryRecordDeath(false, false, "가시 (1,1)", "(1,2)");
            report.TryRecordDeath(false, false, "가시 (1,1)", "(1,2)");
            report.TryRecordDeath(false, false, "화살 (4,4)", "(3,2)");
            report.TryRecordDeath(false, true,  "화살 (4,4)", "(9,9)"); // 시간 초과 — 제외

            Assert.AreEqual(3, report.DeathEvents);

            var kills = report.TopKills(5);
            Assert.AreEqual(2, kills.Count);
            Assert.AreEqual("가시 (1,1)", kills[0].Label);
            Assert.AreEqual(2, kills[0].Count);

            int killSum = 0; foreach (var r in kills) killSum += r.Count;
            int cellSum = 0; foreach (var r in report.TopCells(5)) cellSum += r.Count;
            Assert.AreEqual(report.DeathEvents, killSum);
            Assert.AreEqual(report.DeathEvents, cellSum);
        }

        [Test]
        public void 상위_5개_밖은_기타_한_행으로_합쳐진다()
        {
            var report = new VerificationReport();
            for (int i = 0; i < 7; i++)
                for (int n = 0; n <= i; n++)   // 라벨 i 는 i+1 번 사망
                    report.TryRecordDeath(false, false, "함정" + i, "(0," + i + ")");

            var kills = report.TopKills(5);

            Assert.AreEqual(6, kills.Count, "5행 + 기타");
            Assert.AreEqual("함정6", kills[0].Label);
            Assert.AreEqual(VerificationReport.OthersLabel, kills[5].Label);
            Assert.AreEqual(1 + 2, kills[5].Count, "함정0(1) + 함정1(2)");

            int sum = 0; foreach (var r in kills) sum += r.Count;
            Assert.AreEqual(report.DeathEvents, sum);
        }

        [Test]
        public void 라벨이_비어_있으면_기타로_센다_그리고_Reset_이_모두_지운다()
        {
            var report = new VerificationReport();
            report.Reached = 2; report.Spawned = 7;
            report.TryRecordDeath(false, false, null, "");

            Assert.AreEqual(VerificationReport.OthersLabel, report.TopKills(5)[0].Label);

            report.Reset();

            Assert.AreEqual(0, report.DeathEvents);
            Assert.AreEqual(0, report.Reached);
            Assert.AreEqual(0, report.Spawned);
            Assert.AreEqual(0, report.TopKills(5).Count);
        }

        // ── 연구 효과 분기 ────────────────────────────────────────────────────

        [Test]
        public void SumEffect_는_해당_효과_노드의_투자_레벨_수치만_합한다()
        {
            var life = MakeNode("Life", ResearchEffect.BreachLife, new[] { 1f, 2f });
            var time = MakeNode("Time", ResearchEffect.VerificationTime, new[] { 5f, 10f, 15f, 20f });
            SetResearchLevels(new Dictionary<string, int> { { "Life", 2 }, { "Time", 3 } });
            var nodes = new[] { life, time };

            Assert.AreEqual(2f,  VerificationRules.SumEffect(nodes, ResearchEffect.BreachLife), 1e-4f);
            Assert.AreEqual(15f, VerificationRules.SumEffect(nodes, ResearchEffect.VerificationTime), 1e-4f);
            Assert.AreEqual(0f,  VerificationRules.SumEffect(nodes, ResearchEffect.AgentCount), 1e-4f);
            Assert.AreEqual(0f,  VerificationRules.SumEffect(null, ResearchEffect.BreachLife), 1e-4f);
        }

        [Test]
        public void SumEffect_미투자_노드는_0()
        {
            var life = MakeNode("Life", ResearchEffect.BreachLife, new[] { 1f, 2f });
            SetResearchLevels(new Dictionary<string, int>());

            Assert.AreEqual(0f, VerificationRules.SumEffect(new[] { life }, ResearchEffect.BreachLife));
        }

        [Test]
        public void 태그_가중치_합산은_TagWeight_외_효과_노드를_건너뛴다()
        {
            var tag = _f.Tag("A");
            var table = _f.Table(new (AgentArchetype, int, AITagDefinition[], int)[0], new[] { (tag, 10f) });
            var weightNode = MakeNode("W", ResearchEffect.TagWeight, new[] { 7f }, new[] { tag });
            var lifeNode   = MakeNode("L", ResearchEffect.BreachLife, new[] { 100f }, new[] { tag });
            SetResearchLevels(new Dictionary<string, int> { { "W", 1 }, { "L", 1 } });
            var results = new List<StageSpawnTable.TagWeightEntry>();

            TagWeightService.ComputeFinalWeights(table, new[] { weightNode, lifeNode }, results);

            Assert.AreEqual(17f, results[0].baseWeight, 1e-4f, "TagWeight +7 만 반영, BreachLife +100 은 무시");
        }

        // ── S-08 가드 (#15) ───────────────────────────────────────────────────

        [Test]
        public void 어떤_스폰_테이블에도_S08_가성비_타파가_없다()
        {
            var offenders = new List<string>();

            foreach (string guid in AssetDatabase.FindAssets("t:StageSpawnTable"))
            {
                string path  = AssetDatabase.GUIDToAssetPath(guid);
                var    table = AssetDatabase.LoadAssetAtPath<StageSpawnTable>(path);
                if (table == null) continue;

                if (table.BaseWeights != null)
                    foreach (var w in table.BaseWeights)
                        if (w.baseWeight > 0f && HasCostDrainHook(w.tag))
                            offenders.Add(path + " (가중치)");

                if (table.Entries != null)
                    foreach (var e in table.Entries)
                        if (e.fixedTags != null)
                            foreach (var t in e.fixedTags)
                                if (HasCostDrainHook(t))
                                    offenders.Add(path + " (확정 태그)");
            }

            Assert.IsEmpty(offenders, "SpawnCostDrainHook(S-08) 보유 태그가 스폰 테이블에 있음: " + string.Join(", ", offenders));
        }

        private static bool HasCostDrainHook(AITagDefinition tag)
        {
            if (tag == null || tag.EventHooks == null) return false;
            foreach (var hook in tag.EventHooks)
                if (hook is SpawnCostDrainHook) return true;
            return false;
        }
    }
}
