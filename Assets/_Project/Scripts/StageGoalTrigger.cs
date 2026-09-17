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
        /// <para>재클리어(이미 최초 클리어를 완료한 스테이지)는 보상량 0 으로 발행됩니다 —
        /// 클리어 자체는 유효하되(다음 스테이지 진행 등은 재도전에도 동작해야 함)
        /// 재화만 최초 1회로 게이트되기 때문입니다. <see cref="StageProgressService"/> 참조.</para>
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
        /// <b>중복 지급 방지 — 2단 게이트</b>:
        /// <list type="number">
        ///   <item>인스턴스 플래그(<see cref="_cleared"/>): 이 맵 인스턴스 수명 동안 트리거를
        ///         한 번만 처리 (콜라이더 재진입 방지).</item>
        ///   <item>영속 기록(<see cref="StageProgressService"/>): mapId 별 <b>최초 클리어에만
        ///         재화를 지급</b>. 맵 리로드(재도전)로 인스턴스 플래그가 리셋돼도, 이미 클리어한
        ///         스테이지를 다시 깨면 재화는 재지급되지 않아 무한 파밍을 막습니다.</item>
        /// </list>
        /// 클리어 <b>이벤트</b>(<see cref="OnStageCleared"/>)는 최초/재클리어 모두 발행하되,
        /// 재클리어는 보상량 0 으로 알립니다 — 클리어 자체(다음 스테이지 진행 등)는 재도전에도
        /// 유효해야 하지만 재화만 1회로 제한하기 위함입니다.
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

            string mapId = MapLoader.Instance != null && MapLoader.Instance.CurrentMap != null
                ? MapLoader.Instance.CurrentMap.mapId
                : "";

            // 최초 클리어에만 재화 지급 (mapId 별 영속 게이트) — 재클리어는 파밍 방지로 미지급
            bool firstClear    = StageProgressService.MarkCleared(mapId);
            int  rewardGranted = firstClear ? _rewardGlobes : 0;
            if (firstClear)
                CurrencyService.Add(_rewardGlobes);

            OnStageCleared?.Invoke(mapId, rewardGranted);

            Debug.Log(firstClear
                ? $"[StageGoalTrigger] 스테이지 최초 클리어: {mapId} — 지구본 +{rewardGranted}"
                : $"[StageGoalTrigger] 스테이지 재클리어: {mapId} — 보상 이미 수령(재지급 없음)");
        }
    }
}
