using UnityEditor;
using UnityEngine;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;

namespace ReTrap.EditorTools
{
    /// <summary>
    /// 탑 메뉴 "Addressables" — 자주 쓰는 두 가지 작업을 빠르게 실행합니다.
    /// <list type="bullet">
    ///   <item><b>Open Groups Window</b> — 어드레서블 그룹 창을 엽니다.</item>
    ///   <item><b>Play Mode Data ▸</b> — 플레이 모드 데이터 모드를 전환합니다
    ///         (Use Asset Database / Use Existing Build). 현재 모드에 ✓ 체크 표시.</item>
    /// </list>
    /// 이 프로젝트의 Addressables 데이터 빌더 구성에 맞춰 표시 이름으로 매칭하므로
    /// 인덱스가 바뀌어도 안전합니다.
    /// </summary>
    internal static class AddressablesMenu
    {
        // 메뉴 경로 상수 (체크마크 SetChecked 에서 동일 문자열 필요)
        private const string Root           = "Addressables/";
        private const string MenuGroups     = Root + "Open Groups Window";
        private const string MenuFastMode   = Root + "Play Mode Data/Use Asset Database (fastest)";
        private const string MenuPackedMode = Root + "Play Mode Data/Use Existing Build (requires built groups)";

        // 데이터 빌더 표시 이름 (AddressableAssetSettings 의 IDataBuilder.Name 과 일치)
        private const string NameFastMode   = "Use Asset Database (fastest)";
        private const string NamePackedMode = "Use Existing Build (requires built groups)";

        // ── 1) 그룹 창 열기 ──────────────────────────────────────────────────

        [MenuItem(MenuGroups, false, 0)]
        private static void OpenGroupsWindow()
        {
            // Unity 내장 메뉴를 그대로 호출 (창 타입 직접 참조 불필요 · 버전 안전)
            EditorApplication.ExecuteMenuItem("Window/Asset Management/Addressables/Groups");
        }

        // ── 2) 플레이 모드 데이터 전환 ───────────────────────────────────────

        [MenuItem(MenuFastMode, false, 20)]
        private static void SetFastMode() => SetPlayModeByName(NameFastMode);

        [MenuItem(MenuFastMode, true)]
        private static bool ValidateFastMode()
        {
            Menu.SetChecked(MenuFastMode, IsActivePlayMode(NameFastMode));
            return AddressableAssetSettingsDefaultObject.Settings != null;
        }

        [MenuItem(MenuPackedMode, false, 21)]
        private static void SetPackedMode() => SetPlayModeByName(NamePackedMode);

        [MenuItem(MenuPackedMode, true)]
        private static bool ValidatePackedMode()
        {
            Menu.SetChecked(MenuPackedMode, IsActivePlayMode(NamePackedMode));
            return AddressableAssetSettingsDefaultObject.Settings != null;
        }

        // ── 내부 헬퍼 ────────────────────────────────────────────────────────

        /// <summary>표시 이름으로 플레이 모드 데이터 빌더를 찾아 활성화합니다.</summary>
        private static void SetPlayModeByName(string builderName)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError("[AddressablesMenu] Addressable 설정이 없습니다. " +
                               "Window ▸ Asset Management ▸ Addressables ▸ Groups 에서 먼저 초기화하세요.");
                return;
            }

            int index = FindPlayModeIndex(settings, builderName);
            if (index < 0)
            {
                Debug.LogWarning($"[AddressablesMenu] 플레이 모드 빌더를 찾을 수 없습니다: '{builderName}'");
                return;
            }

            if (settings.ActivePlayModeDataBuilderIndex != index)
            {
                settings.ActivePlayModeDataBuilderIndex = index;
                Debug.Log($"[AddressablesMenu] Play Mode Data → '{builderName}'");
            }
        }

        /// <summary>현재 활성 플레이 모드가 주어진 표시 이름인지 여부.</summary>
        private static bool IsActivePlayMode(string builderName)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) return false;

            int active = settings.ActivePlayModeDataBuilderIndex;
            if (active < 0 || active >= settings.DataBuilders.Count) return false;

            var idb = settings.DataBuilders[active] as IDataBuilder;
            return idb != null && idb.Name == builderName;
        }

        /// <summary>표시 이름과 일치하고 플레이 모드 빌드가 가능한 빌더의 인덱스.</summary>
        private static int FindPlayModeIndex(
            UnityEditor.AddressableAssets.Settings.AddressableAssetSettings settings, string builderName)
        {
            for (int i = 0; i < settings.DataBuilders.Count; i++)
            {
                var idb = settings.DataBuilders[i] as IDataBuilder;
                if (idb == null) continue;
                if (idb.CanBuildData<AddressablesPlayModeBuildResult>() && idb.Name == builderName)
                    return i;
            }
            return -1;
        }
    }
}