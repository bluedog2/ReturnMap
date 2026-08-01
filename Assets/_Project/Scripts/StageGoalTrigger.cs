using System;
using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  StageGoalTrigger — 스테이지 클리어(골 도달) 감지
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <see cref="MapData.goalPoint"/> 위치에 <see cref="MapLoader"/> 가 생성해 부착하는
    /// 트리거. <b>Play 페이즈에서 플레이어가 골에 도달했을 때만</b> 클리어로 처리하고
    /// 아웃게임 재화(박살 난 지구본)를 지급합니다.
    ///
    /// <para><b>Verification 과 완전히 별개</b>: 검증 페이즈에서 AI(VerificationAgent)가
    /// goalPoint 경로를 향해 이동하다 실제로 도달하는 것은 "방어 실패" 판정 로직
    /// (AI/VerificationDirector 등)의 몫이며, 이 컴포넌트가 다루는 "스테이지 클리어"와는
    /// 의미가 다릅니다. 그래서 currentPhase != Play 인 경우 이 트리거는 아무 것도 하지 않습니다.</para>
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class StageGoalTrigger : MonoBehaviour
    {
        // ── 이벤트 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 스테이지 클리어(Play 페이즈 골 도달) 완료 후 발행. 인자는 (mapId, 지급된 보상량).
        /// 향후 클리어 연출 UI·다음 스테이지 전환 로직이 이 이벤트를 구독할 지점.
        /// </summary>
        public static event Action<string, int> OnStageCleared;

        // ── 런타임 상태 ───────────────────────────────────────────────────────

        private int  _rewardGlobes;
        private bool _cleared;

        // ── 초기화 ────────────────────────────────────────────────────────────

        /// <summary>MapLoader 가 생성 직후 호출. 클리어 시 지급할 보상량을 저장합니다.</summary>
        public void Init(int rewardGlobes)
        {
            _rewardGlobes = rewardGlobes;
            _cleared      = false;
        }

        // ── 트리거 ────────────────────────────────────────────────────────────

        /// <summary>
        /// <b>중복 지급 정책 주의</b>: <see cref="_cleared"/> 플래그는 이 맵 인스턴스(오브젝트)
        /// 수명 동안만 유효합니다. 현재는 영속 클리어 기록(세이브)이 없어서, 맵을 리로드
        /// (재도전)하면 플래그가 리셋되어 같은 스테이지를 다시 깨면 또 지급됩니다.
        /// 스테이지 전환 로직 자체가 아직 미구현이라 실질적 문제는 없으나,
        /// TODO: 영속 클리어 기록(PlayerPrefs/세이브) 도입 시 mapId 별 최초 클리어만
        /// 지급하도록 게이트를 추가할 것.
        /// </summary>
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_cleared) return;

            // Play 페이즈 전용 — Build/Verification 중에는 무시
            var phaseManager = GamePhaseManager.Instance;
            if (phaseManager == null || phaseManager.currentPhase != GamePhase.Play)
                return;

            // 플레이어 확인 (검증 AI가 닿아도 클리어 아님 — 애초에 Verification 이면 위에서 걸러짐)
            if (!other.TryGetComponent(out PlayerController player))
                player = other.GetComponentInParent<PlayerController>();
            if (player == null)
                return;

            _cleared = true;

            CurrencyService.Add(_rewardGlobes);

            string mapId = MapLoader.Instance != null && MapLoader.Instance.CurrentMap != null
                ? MapLoader.Instance.CurrentMap.mapId
                : "";
            OnStageCleared?.Invoke(mapId, _rewardGlobes);

            Debug.Log($"[StageGoalTrigger] 스테이지 클리어: {mapId} — 지구본 +{_rewardGlobes}");
        }
    }
}
