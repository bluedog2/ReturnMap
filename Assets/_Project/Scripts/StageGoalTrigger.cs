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

        /// <summary>
        /// Play 페이즈 진입 시각(<see cref="Time.time"/>). 클리어 시간 측정 기준점.
        /// 이 트리거 인스턴스는 맵 로드 때 1회 생성돼 Build→Verification→Play 전환을
        /// 모두 거치며 다음 맵 로드 전까지 살아있으므로, <see cref="GamePhaseManager.OnPhaseChanged"/>
        /// 를 구독해두면 재도전(빌드 복귀 후 재출발)으로 Play 에 다시 들어올 때마다
        /// 자동으로 타이머가 리셋된다.
        /// </summary>
        private float _playPhaseStartTime;
        private bool  _playTimerArmed;

        // ── 초기화 ────────────────────────────────────────────────────────────

        /// <summary>MapLoader 가 생성 직후 호출. 클리어 시 지급할 보상량을 저장합니다.</summary>
        public void Init(int rewardGlobes)
        {
            _rewardGlobes = rewardGlobes;
            _cleared      = false;
        }

        // ── Unity ─────────────────────────────────────────────────────────────

        private void OnEnable()  => GamePhaseManager.OnPhaseChanged += HandlePhaseChangedForTimer;
        private void OnDisable() => GamePhaseManager.OnPhaseChanged -= HandlePhaseChangedForTimer;

        /// <summary>
        /// Play 페이즈 진입 시각을 기록합니다. timeScale 은 <see cref="VerificationSpeedController"/>
        /// 가 Verification 페이즈 전용으로만 조절하고 Play 진입 전 항상 1로 복원하는 것이
        /// 불변식이므로(클래스 헤더 참고), Play 구간에서는 <see cref="Time.time"/> 이
        /// <c>Time.unscaledTime</c> 과 동일하게 흐른다 — 기존 관례(WaitForSeconds 등)와
        /// 맞추기 위해 Time.time 을 사용한다.
        /// </summary>
        private void HandlePhaseChangedForTimer(GamePhase phase)
        {
            if (phase != GamePhase.Play) return;

            _playPhaseStartTime = Time.time;
            _playTimerArmed     = true;
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

            // 진행 데이터(원천) 수집 — 별점/랭크 공식은 기획 미정이라 최고 기록만 축적한다.
            // _playTimerArmed 가 false 인 것은 Play 페이즈 전환 이벤트를 못 받은 비정상 경로뿐이라
            // 방어적으로 0 초로 기록한다(집계값이 왜곡되지 않도록 로그로 원인을 남긴다).
            float clearSeconds = _playTimerArmed ? Time.time - _playPhaseStartTime : 0f;
            if (!_playTimerArmed)
                Debug.LogWarning("[StageGoalTrigger] Play 페이즈 진입 시각이 기록되지 않음 — 클리어 시간 0으로 기록");
            int deaths = RespawnManager.Instance != null ? RespawnManager.Instance.DeathCount : 0;
            StageProgressService.RecordClear(mapId, clearSeconds, deaths);

            OnStageCleared?.Invoke(mapId, rewardGranted);

            Debug.Log(firstClear
                ? $"[StageGoalTrigger] 스테이지 최초 클리어: {mapId} — 지구본 +{rewardGranted}"
                : $"[StageGoalTrigger] 스테이지 재클리어: {mapId} — 보상 이미 수령(재지급 없음)");
        }
    }
}
