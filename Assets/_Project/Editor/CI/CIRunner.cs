using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace ReTrap.EditorTools
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  CIRunner — 헤드리스 배치모드 CI 검증 진입점
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 배치모드에서 프로젝트 무결성 검사를 일괄 실행하고 JSON 리포트를 기록합니다.
    ///
    /// <para>호출 예:
    /// <c>Unity.exe -batchmode -nographics -quit -projectPath &lt;p&gt;
    /// -executeMethod ReTrap.EditorTools.CIRunner.RunAll -ciOutput &lt;json 절대경로&gt; -logFile &lt;log&gt;</c></para>
    ///
    /// <para>종료 코드: 0 = 통과(경고 허용), 1 = 오류 있음, 3 = 러너 자체 예외.</para>
    /// <para>검사 항목: compile / maps / stageCatalog / trapDefinitions / prefabs / scenes / sortingLayers / scriptFolders / balance.</para>
    /// </summary>
    public static class CIRunner
    {
        private const string MapsFolderRel      = "Maps";
        private const string StageCatalogPath   = "Assets/_Project/Settings/StageCatalog.asset";
        private const string PrefabsFolder      = "Assets/_Project/ResourcceEX/Prefabs";
        private const string OutputArg          = "-ciOutput";
        private const int    MaxEntries         = 50;

        // ── 리포트 데이터 (JsonUtility 직렬화) ─────────────────────────────────

        /// <summary>검사 1개의 결과.</summary>
        [Serializable]
        public class CICheck
        {
            public string       name;
            /// <summary>"pass" | "warn" | "fail"</summary>
            public string       status = "pass";
            public List<string> errors   = new List<string>();
            public List<string> warnings = new List<string>();
            public double       durationMs;
        }

        /// <summary>전체 검사 리포트.</summary>
        [Serializable]
        public class CIReport
        {
            public string        startedAt;
            public string        unityVersion;
            public bool          success;
            public int           errorCount;
            public int           warningCount;
            public double        totalMs;
            public List<CICheck> checks = new List<CICheck>();
            public string        exception;
        }

        // ── 진입점 ────────────────────────────────────────────────────────────

        /// <summary>배치모드 진입점. 검사 → JSON 기록 → 요약 로그 → Exit.</summary>
        public static void RunAll()
        {
            int exitCode;
            CIReport report = null;
            string outPath = ResolveOutputPath();
            try
            {
                report = RunChecks();
                WriteReport(report, outPath);
                LogSummary(report);
                exitCode = report.success ? 0 : 1;
            }
            catch (Exception e)
            {
                Debug.LogError($"[CI] 러너 예외: {e}");
                try
                {
                    if (report == null)
                    {
                        report = new CIReport
                        {
                            startedAt    = DateTime.Now.ToString("o"),
                            unityVersion = Application.unityVersion,
                            success      = false
                        };
                    }
                    report.success   = false;
                    report.exception = e.Message;
                    WriteReport(report, outPath);
                }
                catch (Exception e2)
                {
                    Debug.LogError($"[CI] 리포트 기록 실패: {e2.Message}");
                }
                exitCode = 3;
            }
            EditorApplication.Exit(exitCode);
        }

        [MenuItem("ReTrap/Dev/CI 검증 실행 (에디터)")]
        private static void RunFromMenu()
        {
            try
            {
                CIReport report = RunChecks();
                string outPath = Path.Combine(ProjectRoot(), "Logs", "ci-result.json");
                WriteReport(report, outPath);
                LogSummary(report);
            }
            catch (Exception e)
            {
                Debug.LogError($"[CI] 러너 예외: {e}");
            }
        }

        // ── 검사 본체 ─────────────────────────────────────────────────────────

        private static CIReport RunChecks()
        {
            var total = Stopwatch.StartNew();
            var report = new CIReport
            {
                startedAt    = DateTime.Now.ToString("o"),
                unityVersion = Application.unityVersion
            };

            report.checks.Add(RunCheck("compile",         CheckCompile));
            report.checks.Add(RunCheck("maps",            CheckMaps));
            report.checks.Add(RunCheck("stageCatalog",    CheckStageCatalog));
            report.checks.Add(RunCheck("trapDefinitions", CheckTrapDefinitions));
            report.checks.Add(RunCheck("prefabs",         CheckPrefabs));
            report.checks.Add(RunCheck("scenes",          CheckScenes));
            report.checks.Add(RunCheck("sortingLayers",   CheckSortingLayers));
            report.checks.Add(RunCheck("scriptFolders",   CheckScriptFolders));
            report.checks.Add(RunCheck("balance",         CheckBalance));

            foreach (var c in report.checks)
            {
                report.errorCount   += c.errors.Count;
                report.warningCount += c.warnings.Count;
            }
            total.Stop();
            report.totalMs = total.Elapsed.TotalMilliseconds;
            report.success = report.errorCount == 0;
            return report;
        }

        /// <summary>검사 1개를 try/catch + 시간 측정으로 감싸 실행합니다.</summary>
        private static CICheck RunCheck(string name, Action<CICheck> body)
        {
            var check = new CICheck { name = name };
            var sw = Stopwatch.StartNew();
            try
            {
                body(check);
            }
            catch (Exception e)
            {
                check.errors.Add($"검사 중 예외: {e.Message}");
            }
            sw.Stop();
            check.durationMs = sw.Elapsed.TotalMilliseconds;
            CapList(check.errors);
            CapList(check.warnings);
            check.status = check.errors.Count > 0 ? "fail"
                         : check.warnings.Count > 0 ? "warn" : "pass";
            return check;
        }

        private static void CapList(List<string> list)
        {
            if (list.Count <= MaxEntries) return;
            int extra = list.Count - MaxEntries;
            list.RemoveRange(MaxEntries, extra);
            list.Add($"... 외 {extra}개");
        }

        // 1) compile ──────────────────────────────────────────────────────────
        private static void CheckCompile(CICheck c)
        {
            if (EditorUtility.scriptCompilationFailed)
                c.errors.Add("스크립트 컴파일 실패");
        }

        // 2) maps ─────────────────────────────────────────────────────────────
        private static string MapsDir()
            => Path.Combine(Application.dataPath, "StreamingAssets", MapsFolderRel);

        private static string[] MapFiles()
        {
            string dir = MapsDir();
            return Directory.Exists(dir) ? Directory.GetFiles(dir, "*.json") : new string[0];
        }

        private static void CheckMaps(CICheck c)
        {
            string[] files = MapFiles();
            if (files.Length == 0)
            {
                c.errors.Add("맵 JSON 파일이 없습니다: " + MapsDir());
                return;
            }

            foreach (string path in files)
            {
                string file = Path.GetFileName(path);
                string json = File.ReadAllText(path, Encoding.UTF8);
                // JsonUtility 는 문법 오류 시 예외를 던진다 — 파일 단위로 잡아야 나머지 맵 검사가 계속된다
                MapData map;
                try { map = MapData.FromJson(json); }
                catch (Exception e)
                {
                    c.errors.Add($"{file}: JSON 파싱 실패 — {e.Message}");
                    continue;
                }
                if (map == null)
                {
                    c.errors.Add($"{file}: JSON 파싱 실패");
                    continue;
                }

                if (!map.Validate(out string err))
                    c.errors.Add($"{file}: {err}");

                List<string> msgs = MapAuthoringValidator.Validate(map);
                if (msgs != null)
                    foreach (string m in msgs)
                        c.warnings.Add($"{file}: {m}");

                string stem = Path.GetFileNameWithoutExtension(path);
                if (map.mapId != stem)
                    c.warnings.Add($"{file}: mapId '{map.mapId}' 가 파일명 '{stem}' 과 다릅니다");
            }
        }

        // 3) stageCatalog ─────────────────────────────────────────────────────
        private static void CheckStageCatalog(CICheck c)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<StageCatalog>(StageCatalogPath);
            if (catalog == null)
            {
                c.errors.Add($"StageCatalog 에셋을 찾을 수 없습니다: {StageCatalogPath}");
                return;
            }

            var fileIds = new HashSet<string>();
            foreach (string path in MapFiles())
                fileIds.Add(Path.GetFileNameWithoutExtension(path));

            var seen = new HashSet<string>();
            for (int i = 0; i < catalog.Count; i++)
            {
                StageCatalog.StageEntry e = catalog.GetByIndex(i);
                if (e == null || string.IsNullOrEmpty(e.mapId))
                {
                    c.errors.Add($"[{i}] mapId 가 비어있는 엔트리");
                    continue;
                }
                if (!seen.Add(e.mapId))
                    c.errors.Add($"{e.mapId}: mapId 중복");
                if (!fileIds.Contains(e.mapId))
                    c.errors.Add($"{e.mapId}: 대응하는 맵 JSON 파일 없음");
                if (e.spawnTable == null)
                    c.warnings.Add($"{e.mapId}: 스폰 테이블 미지정 (소비자 기본 테이블 사용)");
            }

            foreach (string id in fileIds)
                if (!seen.Contains(id))
                    c.warnings.Add($"{id}.json: StageCatalog 에 등록되지 않음");
        }

        // 4) trapDefinitions ──────────────────────────────────────────────────
        private static void CheckTrapDefinitions(CICheck c)
        {
            string[] guids = AssetDatabase.FindAssets("t:TrapDefinition");
            if (guids.Length == 0)
            {
                c.errors.Add("TrapDefinition 에셋이 하나도 없습니다");
                return;
            }
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var def = AssetDatabase.LoadAssetAtPath<TrapDefinition>(path);
                if (def == null)
                {
                    c.errors.Add($"{path}: 로드 실패");
                    continue;
                }
                ScanMissingReferences(def, path, c.warnings);
            }
        }

        // 5) prefabs ──────────────────────────────────────────────────────────
        private static void CheckPrefabs(CICheck c)
        {
            if (!AssetDatabase.IsValidFolder(PrefabsFolder))
            {
                c.errors.Add($"프리팹 폴더 없음: {PrefabsFolder}");
                return;
            }
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabsFolder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root == null)
                {
                    c.errors.Add($"{path}: 프리팹 로드 실패");
                    continue;
                }
                ScanGameObjectTree(root, path, c);
            }
        }

        // 6) scenes ───────────────────────────────────────────────────────────
        private static void CheckScenes(CICheck c)
        {
            if (!Application.isBatchMode && HasDirtyScene())
            {
                c.warnings.Add("저장되지 않은 씬 변경이 있어 씬 검사를 건너뜀 (저장 후 재실행)");
                return;
            }

            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            {
                if (!s.enabled) continue;
                try
                {
                    Scene scene = EditorSceneManager.OpenScene(s.path, OpenSceneMode.Single);
                    foreach (GameObject root in scene.GetRootGameObjects())
                        ScanGameObjectTree(root, s.path, c);
                }
                catch (Exception e)
                {
                    c.errors.Add($"{s.path}: 씬 검사 실패 — {e.Message}");
                }
            }
        }

        // 7) sortingLayers ────────────────────────────────────────────────────
        // ── 스크립트 폴더 경계 (asmdef) ──────────────────────────────────────

        private const string ScriptsRoot = "Assets/_Project/Scripts/";
        private static readonly string[] AllowedScriptRoots =
        {
            "Assets/_Project/Scripts/",
            "Assets/_Project/Editor/",
            "Assets/_Project/Tests/",
        };

        /// <summary>
        /// Assets/ 아래 .cs 가 허용 폴더(Scripts·Editor·Tests) 안에 있는지, 런타임(Scripts)
        /// 코드가 #if UNITY_EDITOR 밖에서 UnityEditor 를 using 하지 않는지 검사합니다.
        /// </summary>
        private static void CheckScriptFolders(CICheck c)
        {
            string assetsDir = Application.dataPath;
            foreach (string full in Directory.GetFiles(assetsDir, "*.cs", SearchOption.AllDirectories))
            {
                string rel = "Assets/" + full.Substring(assetsDir.Length).TrimStart('\\', '/').Replace('\\', '/');

                bool allowed = false;
                foreach (string root in AllowedScriptRoots)
                    if (rel.StartsWith(root, StringComparison.Ordinal)) { allowed = true; break; }

                if (!allowed)
                {
                    c.errors.Add($"{rel}: 허용 폴더 밖 스크립트 — Scripts(런타임)·Editor·Tests 중 하나에 둘 것 (asmdef 경계)");
                    continue;
                }

                if (!rel.StartsWith(ScriptsRoot, StringComparison.Ordinal)) continue;

                // 단순 휴리스틱: 줄 단위로 #if/#endif 깊이를 추적, UNITY_EDITOR 블록 안 여부 판정
                int depth = 0;
                var editorDepths = new Stack<int>(); // UNITY_EDITOR #if 가 열린 시점의 깊이
                string[] lines = File.ReadAllLines(full);
                for (int i = 0; i < lines.Length; i++)
                {
                    string t = lines[i].Trim();
                    if (t.StartsWith("#if", StringComparison.Ordinal))
                    {
                        depth++;
                        if (t.StartsWith("#if UNITY_EDITOR", StringComparison.Ordinal)) editorDepths.Push(depth);
                    }
                    else if (t.StartsWith("#endif", StringComparison.Ordinal))
                    {
                        if (editorDepths.Count > 0 && editorDepths.Peek() == depth) editorDepths.Pop();
                        depth--;
                    }
                    else if (editorDepths.Count == 0
                          && t.StartsWith("using UnityEditor", StringComparison.Ordinal))
                    {
                        c.errors.Add($"{rel}:{i + 1}: 런타임 스크립트가 #if UNITY_EDITOR 밖에서 UnityEditor 를 using (asmdef 경계)");
                    }
                }
            }
        }

        // 9) balance ──────────────────────────────────────────────────────────
        private const string BalanceConfigPath = "Assets/_Project/Settings/BalanceConfig.asset";

        /// <summary>BalanceConfig 에셋 값 범위 + 빌드 씬 3개 컴포넌트의 balance 배선 검사.</summary>
        private static void CheckBalance(CICheck c)
        {
            var cfg = AssetDatabase.LoadAssetAtPath<BalanceConfig>(BalanceConfigPath);
            if (cfg == null)
            {
                c.errors.Add($"BalanceConfig 에셋을 찾을 수 없습니다: {BalanceConfigPath}");
                return;
            }

            void Prob(string n, float v)
            {
                if (v < 0f || v > 1f) c.errors.Add($"{n}={v} — [0,1] 범위 밖");
            }
            Prob("normalChance", cfg.NormalChance);
            Prob("dudChance", cfg.DudChance);
            Prob("criticalChance", cfg.CriticalChance);
            float sum = cfg.NormalChance + cfg.DudChance + cfg.CriticalChance;
            if (sum > 1f + 1e-4f) c.errors.Add($"변이 확률 합 {sum} > 1");
            if (cfg.VerificationTimeLimit <= 0f) c.errors.Add($"verificationTimeLimit={cfg.VerificationTimeLimit} — 0 보다 커야 함");
            if (cfg.StageClearReward < 0) c.errors.Add($"stageClearReward={cfg.StageClearReward} — 0 이상이어야 함");

            if (!Application.isBatchMode && HasDirtyScene())
            {
                c.warnings.Add("저장되지 않은 씬 변경이 있어 balance 씬 배선 검사를 건너뜀 (저장 후 재실행)");
                return;
            }

            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            {
                if (!s.enabled) continue;
                Scene scene = EditorSceneManager.OpenScene(s.path, OpenSceneMode.Single);
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    CheckBalanceRef<TrapMutationManager>(root, s.path, c);
                    CheckBalanceRef<VerificationDirector>(root, s.path, c);
                    CheckBalanceRef<MapLoader>(root, s.path, c);
                }
            }
        }

        private static void CheckBalanceRef<T>(GameObject root, string scenePath, CICheck c) where T : Component
        {
            foreach (T comp in root.GetComponentsInChildren<T>(true))
            {
                SerializedProperty p = new SerializedObject(comp).FindProperty("balance");
                if (p == null || p.objectReferenceValue == null)
                    c.errors.Add($"{scenePath}/{HierarchyPath(comp.transform)} ({typeof(T).Name}): balance(BalanceConfig) 참조가 비어 있음");
            }
        }

        /// <summary>소팅 레이어 위반을 경고로만 처리할 프리팹 (미사용 잔재 추정).</summary>
        private const string SortingAllowlistPrefab = "Assets/_Project/ResourcceEX/Prefabs/Tlie/Tlie.prefab";
        private const string RequiredSortingLayer   = "Map";

        private static void CheckSortingLayers(CICheck c)
        {
            if (!AssetDatabase.IsValidFolder(PrefabsFolder))
            {
                c.errors.Add($"프리팹 폴더 없음: {PrefabsFolder}");
                return;
            }
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabsFolder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root == null) continue; // 로드 실패는 prefabs 검사에서 보고

                bool allowlisted = path == SortingAllowlistPrefab;
                foreach (SpriteRenderer sr in root.GetComponentsInChildren<SpriteRenderer>(true))
                    ReportSortingLayer(c, allowlisted, path, sr.transform, "SpriteRenderer", sr.sortingLayerName);

                foreach (Canvas cv in root.GetComponentsInChildren<Canvas>(true))
                {
                    if (!cv.isRootCanvas && !cv.overrideSorting) continue;
                    ReportSortingLayer(c, allowlisted, path, cv.transform, "Canvas", cv.sortingLayerName);
                }
            }
        }

        private static void ReportSortingLayer(CICheck c, bool allowlisted, string prefabPath,
                                               Transform t, string kind, string layerName)
        {
            if (layerName == RequiredSortingLayer) return;
            string msg = $"{prefabPath}/{HierarchyPath(t)} ({kind}): 소팅 레이어 '{layerName}' — Map 이어야 함 (CLAUDE.md 소팅 규칙)";
            if (allowlisted) c.warnings.Add(msg + " (미사용 잔재 추정 — 확인 필요)");
            else             c.errors.Add(msg);
        }

        private static bool HasDirtyScene()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) return true;
            return false;
        }

        // ── 공용 스캔 ─────────────────────────────────────────────────────────

        /// <summary>root 및 모든 자식(비활성 포함)의 Missing Script / 누락 참조를 검사합니다.</summary>
        private static void ScanGameObjectTree(GameObject root, string assetPath, CICheck c)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                GameObject go = t.gameObject;
                string label = $"{assetPath}/{HierarchyPath(t)}";

                int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                if (missing > 0)
                    c.errors.Add($"{label}: Missing Script {missing}개");

                foreach (Component comp in go.GetComponents<Component>())
                {
                    if (comp == null) continue; // Missing Script 는 위에서 집계
                    ScanMissingReferences(comp, label + " (" + comp.GetType().Name + ")", c.warnings);
                }
            }
        }

        private static string HierarchyPath(Transform t)
        {
            string p = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                p = t.name + "/" + p;
            }
            return p;
        }

        /// <summary>ObjectReference 중 대상이 사라진(null 이지만 instanceID != 0) 프로퍼티를 찾아 경고로 기록합니다.</summary>
        private static void ScanMissingReferences(UnityEngine.Object obj, string label, List<string> warnings)
        {
            var so = new SerializedObject(obj);
            SerializedProperty it = so.GetIterator();
            while (it.Next(true))
            {
                if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                string pp = it.propertyPath;
                if (pp.Contains("m_PrefabInstance") || pp.Contains("m_PrefabAsset")
                    || pp.Contains("m_CorrespondingSourceObject") || pp.Contains("m_GameObject"))
                    continue;
                if (it.objectReferenceValue == null && it.objectReferenceInstanceIDValue != 0)
                    warnings.Add($"{label}: missing reference: {pp}");
            }
        }

        // ── 출력 ──────────────────────────────────────────────────────────────

        private static string ProjectRoot()
            => Directory.GetParent(Application.dataPath).FullName;

        private static string ResolveOutputPath()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == OutputArg && !string.IsNullOrEmpty(args[i + 1]))
                    return args[i + 1];
            return Path.Combine(ProjectRoot(), "Logs", "ci-result.json");
        }

        private static void WriteReport(CIReport report, string path)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, JsonUtility.ToJson(report, true), new UTF8Encoding(false));
        }

        private static void LogSummary(CIReport report)
        {
            foreach (CICheck c in report.checks)
                Debug.Log($"[CI] {c.name}: {c.status} (err {c.errors.Count} / warn {c.warnings.Count}, {(long)c.durationMs}ms)");
            Debug.Log($"[CI] RESULT: {(report.success ? "PASS" : "FAIL")} (err {report.errorCount} / warn {report.warningCount})");
        }
    }
}
