using UnityEditor;
using UnityEngine;

namespace ReTrap.EditorTools
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  AgentPrefabSetup — 검증 AI 프리팹에 태그 런타임 컴포넌트 자동 배선
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Enemies 폴더의 모든 적 프리팹(VerificationAgent 보유)에
    /// <see cref="AgentContext"/>·<see cref="AgentHealth"/>·<see cref="AgentHitFlash"/> 를 추가합니다.
    /// AI 태그 시스템(2단계)이 요구하는 배선 — 미배선 프리팹은 기존 즉사 동작으로
    /// 폴백하지만, 태그(면역·HP·쉴드)와 피격 연출(무적 점멸)이 동작하려면 세 컴포넌트가 필요합니다.
    /// 이미 배선된 프리팹은 건너뛰므로 반복 실행해도 안전합니다.
    /// </summary>
    public static class AgentPrefabSetup
    {
        private const string ENEMIES_FOLDER = "Assets/_Project/ResourcceEX/Prefabs/Enemies";

        [MenuItem("ReTrap/Setup/검증 AI 프리팹 세팅 (AgentContext + Health + HitFlash)")]
        public static void SetupAgentPrefabs()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { ENEMIES_FOLDER });
            int wired = 0, skipped = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    // VerificationAgent 가 없는 프리팹(연출용 등)은 대상 아님
                    if (root.GetComponent<VerificationAgent>() == null)
                    {
                        skipped++;
                        continue;
                    }

                    bool changed = false;
                    if (root.GetComponent<AgentContext>() == null)
                    {
                        root.AddComponent<AgentContext>();
                        changed = true;
                    }
                    if (root.GetComponent<AgentHealth>() == null)
                    {
                        root.AddComponent<AgentHealth>();
                        changed = true;
                    }
                    if (root.GetComponent<AgentHitFlash>() == null)
                    {
                        root.AddComponent<AgentHitFlash>();
                        changed = true;
                    }

                    if (changed)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        wired++;
                        Debug.Log($"[AgentPrefabSetup] 배선 완료: {path}");
                    }
                    else
                    {
                        skipped++;
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            Debug.Log($"[AgentPrefabSetup] 완료 — 배선 {wired}개, 건너뜀(이미 배선/대상 아님) {skipped}개");
        }
    }
}
