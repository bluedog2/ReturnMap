using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ReTrap.EditorTools
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  BalanceSetup — BalanceConfig 에셋 생성 + 씬 컴포넌트 배선
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// SampleScene 의 TrapMutationManager / VerificationDirector / MapLoader 에 박힌
    /// 현재 값으로 <see cref="BalanceConfig"/> 에셋을 만들고(없을 때만) 3개 컴포넌트의
    /// balance 참조를 연결합니다. 멱등이며 대화상자를 쓰지 않아 배치모드에서도 동작합니다.
    /// <c>-executeMethod ReTrap.EditorTools.BalanceSetup.CreateAndWire</c>
    /// </summary>
    public static class BalanceSetup
    {
        private const string AssetPath         = "Assets/_Project/Settings/BalanceConfig.asset";
        private const string FallbackScenePath = "Assets/_Project/ResourcceEX/Scenes/SampleScene.unity";

        [MenuItem("ReTrap/Setup/밸런스 설정 에셋 생성·배선")]
        public static void CreateAndWire()
        {
            string scenePath = FindScenePath();
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            var mutation = Object.FindFirstObjectByType<TrapMutationManager>(FindObjectsInactive.Include);
            var director = Object.FindFirstObjectByType<VerificationDirector>(FindObjectsInactive.Include);
            var loader   = Object.FindFirstObjectByType<MapLoader>(FindObjectsInactive.Include);
            if (mutation == null || director == null || loader == null)
            {
                Debug.LogError($"[BalanceSetup] {scenePath}: 컴포넌트를 찾지 못했습니다 — " +
                               $"TrapMutationManager={(mutation != null)}, VerificationDirector={(director != null)}, MapLoader={(loader != null)}");
                return;
            }

            var config = AssetDatabase.LoadAssetAtPath<BalanceConfig>(AssetPath);
            bool created = false;
            if (config == null)
            {
                var mso = new SerializedObject(mutation);
                var dso = new SerializedObject(director);
                var lso = new SerializedObject(loader);

                config = ScriptableObject.CreateInstance<BalanceConfig>();
                var cso = new SerializedObject(config);
                cso.FindProperty("normalChance").floatValue          = mso.FindProperty("normalChance").floatValue;
                cso.FindProperty("dudChance").floatValue             = mso.FindProperty("dudChance").floatValue;
                cso.FindProperty("criticalChance").floatValue        = mso.FindProperty("criticalChance").floatValue;
                cso.FindProperty("verificationTimeLimit").floatValue = dso.FindProperty("timeLimit").floatValue;
                cso.FindProperty("stageClearReward").intValue        = lso.FindProperty("_stageClearReward").intValue;
                cso.ApplyModifiedPropertiesWithoutUndo();

                Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
                AssetDatabase.CreateAsset(config, AssetPath);
                created = true;
            }

            Assign(mutation);
            Assign(director);
            Assign(loader);
            void Assign(Object target)
            {
                var so = new SerializedObject(target);
                so.FindProperty("balance").objectReferenceValue = config;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(target);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log($"[BalanceSetup] {(created ? "에셋 생성" : "기존 에셋 재사용(값 유지)")}: {AssetPath} — " +
                      $"normal {config.NormalChance}, dud {config.DudChance}, critical {config.CriticalChance}, " +
                      $"timeLimit {config.VerificationTimeLimit}, reward {config.StageClearReward}. " +
                      $"씬 {scenePath} 의 3개 컴포넌트에 balance 배선 완료.");
        }

        private static string FindScenePath()
        {
            foreach (var s in EditorBuildSettings.scenes)
                if (s.path.EndsWith("/SampleScene.unity")) return s.path;
            return FallbackScenePath;
        }
    }
}
