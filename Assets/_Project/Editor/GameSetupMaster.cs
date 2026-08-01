using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ReTrap.EditorTools
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  GameSetupMaster — Setup 메뉴 1~6 을 의존 순서대로 원클릭 실행
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 메뉴 <b>ReTrap → Setup → ★ 전체 기본 세팅 (원클릭)</b>:
    /// <see cref="AITagAssetGenerator"/>부터 <see cref="MetaUiSetup"/>까지
    /// 1~6번 Setup 메뉴를 의존 순서(태그 → 연구노드 → 함정/HUD → 검증 AI → 맵 시스템 → 메타 UI)대로
    /// 순차 호출합니다. 새 씬이나 새 클론에서 게임 기본 세팅을 한 번에 재현할 때 사용합니다.
    /// <para>
    /// 개별 1~6 메뉴는 부분 재실행(예: 함정 하나만 추가한 뒤 3번만 다시 실행)용으로 남겨둡니다.
    /// 각 단계는 독립적으로 try/catch 로 감싸 하나가 실패해도 나머지 단계를 계속 진행하고,
    /// 마지막에 실패 목록을 모아 요약 로그와 다이얼로그로 보고합니다.
    /// </para>
    /// <para>
    /// 씬에 오브젝트를 만드는 단계(3·5·6) 이후, 모든 단계가 끝나면 마스터가 마지막에
    /// <see cref="EditorSceneManager.SaveOpenScenes"/> 로 열린 씬을 명시적으로 저장합니다.
    /// 원클릭 세팅의 목적이 '한 번에 완성'이므로, 사용자가 Ctrl+S 를 잊어 6번(메타 UI)
    /// 오브젝트만 유실되는 사고를 막기 위함입니다(3·5번이 내부적으로 이미 저장하더라도
    /// 6번 결과까지 포함해 마지막에 한 번 더 확실히 디스크에 반영합니다).
    /// </para>
    /// </summary>
    public static class GameSetupMaster
    {
        /// <summary>단계 정의 — 순서·라벨·실행 액션을 한곳에 모아 total 을 여기서 파생시킨다.</summary>
        private static (string label, Action action)[] BuildSteps() => new (string, Action)[]
        {
            ("AI 태그 에셋 생성 (35종)", () => AITagAssetGenerator.GenerateAll()),
            ("연구소 노드 에셋 생성 (6종)", () => ResearchAssetGenerator.GenerateAll()),
            ("함정 프리팹 + Build UI 세팅", () => TrapPrefabBuilder.Run()),
            ("검증 AI 프리팹 세팅", () => AgentPrefabSetup.SetupAgentPrefabs()),
            ("맵 시스템 세팅", () => MapSystemSetup.Run()),
            // 메타 UI 는 연구 노드/스폰 테이블 에셋을 참조하므로 반드시 위 에셋 생성 이후 실행돼야 한다.
            ("메타 UI 세팅 (미리보기 + 연구소)", () => MetaUiSetup.Run()),
        };

        [MenuItem("ReTrap/Setup/★ 전체 기본 세팅 (원클릭)", false, 0)]
        public static void RunAll()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("전체 기본 세팅",
                    "플레이 모드에서는 실행할 수 없습니다.\n플레이를 중지한 뒤 다시 실행하세요.", "확인");
                return;
            }

            if (EditorApplication.isCompiling)
            {
                EditorUtility.DisplayDialog("전체 기본 세팅",
                    "스크립트 컴파일 중입니다.\n컴파일 완료 후 다시 실행하세요.", "확인");
                return;
            }

            var steps = BuildSteps();
            var failures = new List<string>();
            int total = steps.Length;
            int succeeded = 0;

            // AITagAssetGenerator.GenerateAll / ResearchAssetGenerator.GenerateAll 은 내부에서
            // 각자 SaveAssets/Refresh 를 수행하므로, 여기서 중복으로 다시 호출하지 않는다.
            // (연구소 노드는 태그 에셋을 FindAssets 로 찾으므로, 태그 생성 단계의 내부 저장에 의존한다.)
            for (int i = 0; i < steps.Length; i++)
            {
                succeeded += RunStep(i + 1, total, steps[i].label, steps[i].action, failures);
            }

            int failedCount = failures.Count;

            // 원클릭 세팅은 '한 번에 완성'이 목적이므로, 씬을 건드린 단계(3·5·6) 이후
            // dirty 표시만 하고 끝내지 않고 여기서 명시적으로 저장까지 마친다.
            var activeScene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);

            bool sceneSaved = false;
            try
            {
                sceneSaved = EditorSceneManager.SaveOpenScenes();
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameSetup] 씬 저장 중 예외 발생: {e}");
            }

            if (sceneSaved)
                Debug.Log("[GameSetup] 씬 저장 완료.");
            else
                Debug.LogWarning("[GameSetup] 씬 저장에 실패했습니다 — 직접 Ctrl+S 로 저장하세요.");

            if (failedCount > 0)
            {
                string summary = string.Join("\n", failures);
                Debug.LogError($"[GameSetup] 전체 세팅 완료 — 성공 {succeeded}/{total}, 실패 {failedCount}건:\n{summary}");
            }
            else
            {
                Debug.Log($"[GameSetup] 전체 세팅 완료 — {succeeded}/{total} 단계 모두 성공.");
            }

            string saveLine = sceneSaved
                ? "\n\n씬 저장 완료."
                : "\n\n⚠ 씬 자동 저장 실패 — Ctrl+S 로 직접 저장하세요.";

            EditorUtility.DisplayDialog("전체 기본 세팅 완료",
                $"성공 {succeeded}/{total} 단계" +
                (failedCount > 0 ? $"\n실패 {failedCount} 단계 (콘솔 로그 확인)" : "\n모두 성공") +
                saveLine, "확인");
        }

        /// <summary>단계 하나를 try/catch 로 감싸 실행. 성공 시 1, 실패 시 0 을 반환하고 failures 에 기록.</summary>
        private static int RunStep(int index, int total, string label, Action action, List<string> failures)
        {
            Debug.Log($"[GameSetup] {index}/{total} — {label} 시작");
            try
            {
                action.Invoke();
                Debug.Log($"[GameSetup] {index}/{total} — {label} 완료");
                return 1;
            }
            catch (Exception e)
            {
                string message = $"{index}/{total} {label} 실패: {e.Message}";
                Debug.LogError($"[GameSetup] {message}\n{e}");
                failures.Add(message);
                return 0;
            }
        }
    }
}
