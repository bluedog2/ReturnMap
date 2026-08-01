using System;
using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  CurrencyService — 아웃게임 재화 "박살 난 지구본" 정적 서비스
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 아웃게임 재화(박살 난 지구본)의 유일한 진실 소스. PlayerPrefs 로 즉시 영속화되며,
    /// 정적 생성자에서 1회 로드한다. 도둑(S-04) 재화 훅, 연구소 투자(<see cref="ResearchBoardState"/>)
    /// 가 이 서비스를 통해서만 재화를 증감한다.
    /// </summary>
    public static class CurrencyService
    {
        private const string PrefsKey = "ReTrap.Meta.Globes";

        private static int _globes;

        /// <summary>박살 난 지구본 보유량.</summary>
        public static int Globes => _globes;

        /// <summary>잔액이 변경될 때마다 발행 (UI 구독용). 인자는 변경 후 잔액.</summary>
        public static event Action<int> OnChanged;

        static CurrencyService()
        {
            _globes = PlayerPrefs.GetInt(PrefsKey, 0);
        }

        /// <summary>
        /// 플레이 진입마다(도메인 리로드 여부 무관) 디스크에서 강제로 재조회한다.
        /// "Fast Play(Reload Domain 끄기)"로 반복 재생하면 정적 필드가 이전 세션 값을 그대로
        /// 들고 있어 잔액 갱신이 반영되지 않는데, 이 훅으로 매 플레이 시작 시 최신 값을 보장한다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ReloadOnPlayEnter()
        {
            _globes = PlayerPrefs.GetInt(PrefsKey, 0);
        }

        /// <summary>지구본을 획득한다. amount 가 0 이하면 무시한다.</summary>
        public static void Add(int amount)
        {
            if (amount <= 0) return;

            _globes += amount;
            Save();
            OnChanged?.Invoke(_globes);
        }

        /// <summary>지구본을 소비한다. 잔액이 부족하면 아무 변화 없이 false 를 반환한다.</summary>
        public static bool TrySpend(int amount)
        {
            if (amount <= 0) return true;
            if (_globes < amount) return false;

            _globes -= amount;
            Save();
            OnChanged?.Invoke(_globes);
            return true;
        }

        private static void Save()
        {
            if (_globes < 0) _globes = 0; // 음수 방지
            PlayerPrefs.SetInt(PrefsKey, _globes);
            PlayerPrefs.Save();
        }
    }
}
