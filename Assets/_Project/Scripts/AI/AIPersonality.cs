using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  AIPersonality — 검증 AI 성향 스텟 (데이터 에셋)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 검증 페이즈 AI 한 개체의 <b>성향 스텟</b>을 담는 ScriptableObject.
    /// 스테이지/개체별로 에셋을 만들어 꽂으면 같은 알고리즘이 다른 행동을 하게 됩니다
    /// (GDD의 "무지성 돌진 ➡️ 우회로 탐색" 지능 스케일링).
    ///
    /// <para><b>설계 원칙</b>: 알고리즘 코드는 이 스텟을 <b>직접 읽지 않습니다</b>.
    /// 반드시 <see cref="ToBehaviorParams"/> 가 변환한 <see cref="AIBehaviorParams"/> 만 사용합니다.
    /// 구체 기획이 확정되면 변환 공식 한 곳만 고치면 전체 밸런스가 바뀝니다.</para>
    ///
    /// <para>스텟 구성은 아직 기획 미확정 — 아래 5축은 자리표시자이며,
    /// 축 추가/삭제 시 <see cref="AIBehaviorParams"/> 와 변환 공식만 함께 수정할 것.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "AIPersonality_New", menuName = "ReTrap/AI 성향(Personality)")]
    public class AIPersonality : ScriptableObject
    {
        // ── 정체성 ────────────────────────────────────────────────────────────

        [Header("정체성")]
        [Tooltip("HUD/연출에 노출할 개체 이름 (예: 고블린 척후병)")]
        public string displayName = "이름 없는 마족";

        // ── 성향 스텟 (전부 0~1 정규화) ──────────────────────────────────────

        [Header("성향 스텟 (0~1, 기획 미확정 자리표시자)")]
        [Range(0f, 1f)]
        [Tooltip("신중함 — 함정 위험을 경로 비용에 얼마나 반영하나. 0=무지성 직진, 1=철저한 우회")]
        public float caution = 0.5f;

        [Range(0f, 1f)]
        [Tooltip("끈기 — 사망 후 재도전 성향. 시도 횟수를 결정")]
        public float persistence = 0.5f;

        [Range(0f, 1f)]
        [Tooltip("학습력 — 죽은 자리(AIMemory)를 다음 시도의 경로 비용에 반영하는 정도")]
        public float adaptability = 0.5f;

        [Range(0f, 1f)]
        [Tooltip("민첩함 — 실행 정밀도. 낮으면 이동/점프 실수 확률 증가, 높으면 이동속도 보너스")]
        public float agility = 0.5f;

        [Range(0f, 1f)]
        [Tooltip("침착함 — 함정 타이밍을 기다렸다 통과하는 성향. 0=무조건 강행 돌파")]
        public float patience = 0.5f;

        // ── 변환 계층 ─────────────────────────────────────────────────────────

        /// <summary>
        /// 성향 스텟 → 알고리즘 파라미터 변환.
        /// TODO(밸런스): 아래 공식은 전부 임시값. 기획 확정 시 여기만 조정.
        /// </summary>
        public AIBehaviorParams ToBehaviorParams()
        {
            return new AIBehaviorParams
            {
                dangerCostMultiplier = Mathf.Lerp(0.2f, 5f, caution),
                maxAttempts          = 1 + Mathf.RoundToInt(persistence * 4f), // 1~5회
                memoryPenaltyWeight  = adaptability * 10f,
                mistakeChance        = Mathf.Lerp(0.3f, 0f, agility),
                moveSpeedMultiplier  = Mathf.Lerp(0.8f, 1.3f, agility),
                trapWaitTolerance    = patience * 3f,                          // 최대 3초 대기
            };
        }
    }

    /// <summary>
    /// 성향 스텟에서 파생된 <b>알고리즘 소비용 파라미터</b> 번들.
    /// 길찾기·실행·오케스트레이션 코드는 이 구조체만 참조합니다.
    /// </summary>
    public struct AIBehaviorParams
    {
        /// <summary>함정 NodeCost 에 곱하는 위험 가중 배율 (신중함).</summary>
        public float dangerCostMultiplier;

        /// <summary>검증 1회당 최대 돌파 시도 횟수 (끈기).</summary>
        public int maxAttempts;

        /// <summary>사망 지점에 얹는 추가 노드 비용 (학습력).</summary>
        public float memoryPenaltyWeight;

        /// <summary>웨이포인트 실행 시 실수(오차) 확률 0~1 (민첩함).</summary>
        public float mistakeChance;

        /// <summary>기준 이동속도에 곱하는 배율 (민첩함).</summary>
        public float moveSpeedMultiplier;

        /// <summary>함정 앞에서 안전 타이밍을 기다려 줄 최대 시간(초) (침착함).</summary>
        public float trapWaitTolerance;

        /// <summary>
        /// 성향 에셋(<see cref="AIPersonality"/>)이 없는 개체(아키타입 미배선 등)를 위한
        /// 합리적 기본값. 위험 가중·재시도·학습·실수 모두 "성향 없음 = 평범하게 1회 시도"로
        /// 동작하게 한다.
        /// </summary>
        public static AIBehaviorParams Default => new AIBehaviorParams
        {
            dangerCostMultiplier = 1f,
            maxAttempts          = 1,
            memoryPenaltyWeight  = 0f,
            mistakeChance        = 0f,
            moveSpeedMultiplier  = 1f,
            trapWaitTolerance    = 0f,
        };
    }
}
