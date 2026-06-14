using UnityEditor;
using UnityEngine;

namespace ReTrap.EditorTools
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  ReloadDomainToggle — Enter Play Mode 의 Reload Domain 빠른 토글
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 메뉴 <b>ReTrap → Dev → Fast Play (Reload Domain 끄기)</b> 로
    /// 도메인 리로드를 켜고 끕니다. 체크 표시 = 현재 꺼진(빠른 플레이) 상태.
    ///
    /// <para><b>⚠️ 주의</b>: 끄면 플레이 진입이 빨라지지만 static 필드·이벤트·싱글턴이
    /// 플레이 종료 후에도 리셋되지 않습니다. 이 프로젝트는 static 이벤트
    /// (OnPhaseChanged/OnMapLoaded/OnKnockback)·싱글턴 Instance·static 캐시가 많아
    /// 두 번째 플레이부터 상태 오염이 생길 수 있습니다. MCP 작업 등 짧은 검증에만 켜고
    /// 평소엔 다시 끄는(=Reload Domain 켜는) 것을 권장합니다.</para>
    /// </summary>
    public static class ReloadDomainToggle
    {
        private const string MenuPath = "ReTrap/Dev/Fast Play (Reload Domain 끄기)";

        [MenuItem(MenuPath, false, 200)]
        private static void Toggle()
        {
            bool nowDisabled = IsDomainReloadDisabled();
            SetDomainReloadDisabled(!nowDisabled);

            bool result = IsDomainReloadDisabled();
            Debug.Log(result
                ? "[ReloadDomainToggle] Reload Domain 끔 — 플레이 진입 빠름. ⚠️ static 상태가 리셋되지 않으니 검증 후 다시 켜세요."
                : "[ReloadDomainToggle] Reload Domain 켬 — 매 플레이 static 상태 정상 리셋 (안전).");
        }

        // 메뉴에 현재 상태를 체크 표시로 반영
        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, IsDomainReloadDisabled());
            return true;
        }

        private static bool IsDomainReloadDisabled()
            => EditorSettings.enterPlayModeOptionsEnabled
               && (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) != 0;

        private static void SetDomainReloadDisabled(bool disable)
        {
            if (disable)
            {
                EditorSettings.enterPlayModeOptionsEnabled = true;
                EditorSettings.enterPlayModeOptions       |= EnterPlayModeOptions.DisableDomainReload;
            }
            else
            {
                EditorSettings.enterPlayModeOptions &= ~EnterPlayModeOptions.DisableDomainReload;

                // 남은 옵션이 없으면 Enter Play Mode Options 자체를 꺼서 완전 기본 동작으로 복귀
                if (EditorSettings.enterPlayModeOptions == EnterPlayModeOptions.None)
                    EditorSettings.enterPlayModeOptionsEnabled = false;
            }
        }
    }
}
