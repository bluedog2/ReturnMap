using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  ResearchBoardPanel — "지구 평평 협회 연구소" 투자 패널 (Build HUD 부속)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 아웃게임 재화(박살 난 지구본)로 <see cref="ResearchNodeDefinition"/> 노드에 투자하는
    /// 화면. Build 페이즈 중에만 R 키(또는 <see cref="TogglePanel"/> 버튼)로 열고 닫을 수 있으며,
    /// 다른 페이즈로 넘어가면 강제로 닫힌다.
    /// <para>씬 오브젝트 자동 생성/배선은 하지 않는다 — 컴포넌트 부착과 필드 배선은
    /// <c>ReTrap → Setup → 6. 메타 UI 세팅 (미리보기 + 연구소)</c>(MetaUiSetup) 또는 수동/MCP.</para>
    /// </summary>
    public class ResearchBoardPanel : MonoBehaviour
    {
        [Header("데이터")]
        [SerializeField]
        [Tooltip("연구소 노드 6종. nodeLabels/upgradeButtons 와 인덱스가 대응해야 한다.")]
        private ResearchNodeDefinition[] nodes;

        [Header("표시 UI")]
        [SerializeField]
        [Tooltip("잔액(박살 난 지구본) 표시 텍스트.")]
        private Text currencyText;

        [SerializeField]
        [Tooltip("노드별 상태 텍스트. nodes 와 인덱스 대응.")]
        private Text[] nodeLabels;

        [SerializeField]
        [Tooltip("노드별 투자 버튼. nodes 와 인덱스 대응.")]
        private Button[] upgradeButtons;

        [SerializeField]
        [Tooltip("패널 전체 표시/숨김 대상 루트.")]
        private GameObject panelRoot;

        [Header("개발 편의")]
        [SerializeField]
        [Tooltip("테스트용 재화 지급량 (DebugGrantCurrency 버튼용).")]
        private int debugGrantAmount = 100;

        // ── 학파 한글 표기 ───────────────────────────────────────────────────

        private static readonly Dictionary<ResearchSchool, string> SchoolNameKr = new Dictionary<ResearchSchool, string>
        {
            { ResearchSchool.GeometryBreak,  "기하학 파괴" },
            { ResearchSchool.EarthTruth,     "대지 진리" },
            { ResearchSchool.CapitalReverse, "자본주의 역공학" },
        };

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            WireButtons();
        }

        private void Start()
        {
            // 기본 상태는 항상 닫힘 — 에디터 세팅이 이미 비활성으로 두더라도 방어적으로 강제.
            if (panelRoot != null)
                panelRoot.SetActive(false);
        }

        private void OnEnable()
        {
            CurrencyService.OnChanged      += HandleCurrencyChanged;
            ResearchBoardState.OnUpgraded  += HandleUpgraded;
            GamePhaseManager.OnPhaseChanged += HandlePhaseChanged;
        }

        private void OnDisable()
        {
            CurrencyService.OnChanged      -= HandleCurrencyChanged;
            ResearchBoardState.OnUpgraded  -= HandleUpgraded;
            GamePhaseManager.OnPhaseChanged -= HandlePhaseChanged;
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame)
                TogglePanel();
        }

        // ── 열기/닫기 ─────────────────────────────────────────────────────────

        /// <summary>패널을 열거나 닫는다 (UI 버튼에서도 호출 가능). Build 페이즈가 아니면 열지 않는다.</summary>
        public void TogglePanel()
        {
            if (panelRoot == null) return;

            bool willOpen = !panelRoot.activeSelf;
            if (willOpen && (GamePhaseManager.Instance == null ||
                             GamePhaseManager.Instance.currentPhase != GamePhase.Build))
                return;

            panelRoot.SetActive(willOpen);
            if (willOpen)
                RefreshAll();
        }

        /// <summary>개발용 — 재화를 즉시 지급한다 (테스트 버튼 배선용).</summary>
        public void DebugGrantCurrency()
        {
            CurrencyService.Add(debugGrantAmount);
        }

        // ── 페이즈 ────────────────────────────────────────────────────────────

        private void HandlePhaseChanged(GamePhase phase)
        {
            if (phase != GamePhase.Build && panelRoot != null && panelRoot.activeSelf)
                panelRoot.SetActive(false);
        }

        // ── 이벤트 구독 핸들러 ───────────────────────────────────────────────

        private void HandleCurrencyChanged(int newBalance) => RefreshAll();

        private void HandleUpgraded(ResearchNodeDefinition node, int newLevel) => RefreshAll();

        // ── 버튼 배선 ─────────────────────────────────────────────────────────

        private void WireButtons()
        {
            if (upgradeButtons == null) return;

            for (int i = 0; i < upgradeButtons.Length; i++)
            {
                if (upgradeButtons[i] == null) continue;

                int index = i; // 클로저 캡처 (로컬 변수)
                upgradeButtons[i].onClick.AddListener(() => HandleUpgradeClicked(index));
            }
        }

        private void HandleUpgradeClicked(int index)
        {
            if (nodes == null || index < 0 || index >= nodes.Length) return;

            ResearchNodeDefinition node = nodes[index];
            if (node == null) return;

            ResearchBoardState.TryUpgrade(node); // 성공/실패 무관 — 아래에서 즉시 갱신
            RefreshAll();
        }

        // ── 갱신 ─────────────────────────────────────────────────────────────

        /// <summary>잔액 + 노드별 라벨/버튼 상태를 즉시 갱신한다. Update 폴링 없이 이벤트 시점에만 호출.</summary>
        private void RefreshAll()
        {
            if (currencyText != null)
                currencyText.text = $"박살 난 지구본: {CurrencyService.Globes}";

            if (nodes == null) return;

            for (int i = 0; i < nodes.Length; i++)
            {
                ResearchNodeDefinition node = nodes[i];
                if (node == null) continue;

                int level    = ResearchBoardState.GetLevel(node);
                int maxLevel = node.MaxLevel;
                bool maxed   = level >= maxLevel;

                if (nodeLabels != null && i < nodeLabels.Length && nodeLabels[i] != null)
                    nodeLabels[i].text = BuildNodeLabel(node, level, maxLevel, maxed);

                if (upgradeButtons != null && i < upgradeButtons.Length && upgradeButtons[i] != null)
                {
                    bool canUpgrade = !maxed && CurrencyService.Globes >= node.GetCost(level + 1);
                    upgradeButtons[i].interactable = canUpgrade;
                }
            }
        }

        /// <summary>"[학파] 노드명 Lv.현재/최대 — 효과요약 (다음 비용: N)" 형식 라벨 구성.</summary>
        private static string BuildNodeLabel(ResearchNodeDefinition node, int level, int maxLevel, bool maxed)
        {
            string school   = SchoolNameKr.TryGetValue(node.School, out var kr) ? kr : node.School.ToString();
            string effect   = BuildEffectSummary(node, level);
            string costPart = maxed ? "(완료)" : $"(다음 비용: {node.GetCost(level + 1)})";

            return $"[{school}] {node.DisplayName} Lv.{level}/{maxLevel} — {effect} {costPart}";
        }

        /// <summary>현재(미투자 시 1레벨 예고) 보정치를 대상 태그와 함께 요약한다.</summary>
        private static string BuildEffectSummary(ResearchNodeDefinition node, int level)
        {
            int previewLevel = level > 0 ? level : Mathf.Min(1, node.MaxLevel);
            float delta = node.GetDelta(previewLevel);
            string sign = delta > 0f ? "+" : string.Empty;

            return $"{TagListText(node.TargetTags)} 가중치 {sign}{delta:0.#}";
        }

        private static string TagListText(AITagDefinition[] tags)
        {
            if (tags == null || tags.Length == 0) return "(대상 없음)";

            if (tags.Length == 1)
                return tags[0] != null ? tags[0].DisplayName : "?";

            var names = new List<string>(tags.Length);
            foreach (var tag in tags)
                names.Add(tag != null ? tag.DisplayName : "?");

            return string.Join("·", names);
        }
    }
}
