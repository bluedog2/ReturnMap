using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  StagePreviewPanel — 진입 전 웨이브 구성 미리보기 (Build HUD 부속)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 빌드 페이즈 동안 이번 스테이지 웨이브 구성(엔트리별 확정 태그 + 랜덤 슬롯,
    /// 랜덤 슬롯 상위 확률 태그)을 노출한다. <see cref="TagWeightService"/> 가 계산한
    /// 최종 가중치를 사용해, 이 패널이 보여주는 확률과 실제 <see cref="StageAgentRoster"/>
    /// 추첨이 같은 데이터를 쓰도록 한다.
    /// <para>씬 오브젝트 자동 생성/배선은 하지 않는다 — 컴포넌트 부착과 필드 배선은 수동/MCP.</para>
    /// </summary>
    public class StagePreviewPanel : MonoBehaviour
    {
        [Header("데이터")]
        [SerializeField]
        [Tooltip("미리보기 대상 스폰 테이블.")]
        private StageSpawnTable spawnTable;

        [SerializeField]
        [Tooltip("가중치 보정(연구소 투자)에 반영할 연구 노드 목록. 씬에서 배선.")]
        private ResearchNodeDefinition[] researchNodes;

        [Header("표시 UI")]
        [SerializeField]
        [Tooltip("이번 웨이브 구성을 표시할 텍스트 (BuildHudController 와 동일하게 uGUI legacy Text).")]
        private Text previewText;

        [Header("표시 옵션")]
        [SerializeField, Min(1)]
        [Tooltip("랜덤 슬롯 상위 확률로 노출할 태그 수.")]
        private int topTagCount = 3;

        // ── 내부 — 프레임 할당 회피용 재사용 버퍼 ────────────────────────────
        private readonly List<StageSpawnTable.TagWeightEntry> _finalWeights = new List<StageSpawnTable.TagWeightEntry>();
        private readonly List<StageSpawnTable.TagWeightEntry> _sortBuffer   = new List<StageSpawnTable.TagWeightEntry>();
        private readonly StringBuilder _sb = new StringBuilder(256);

        private static readonly Comparison<StageSpawnTable.TagWeightEntry> DescByWeight =
            (a, b) => b.baseWeight.CompareTo(a.baseWeight);

        // ── Unity ─────────────────────────────────────────────────────────────

        private void OnEnable()
        {
            GamePhaseManager.OnPhaseChanged += HandlePhaseChanged;

            if (GamePhaseManager.Instance != null)
                HandlePhaseChanged(GamePhaseManager.Instance.currentPhase);
        }

        private void OnDisable()
        {
            GamePhaseManager.OnPhaseChanged -= HandlePhaseChanged;
        }

        // ── 페이즈 ────────────────────────────────────────────────────────────

        private void HandlePhaseChanged(GamePhase phase)
        {
            bool show = phase == GamePhase.Build;

            if (previewText != null)
                previewText.gameObject.SetActive(show);

            if (show)
                RefreshText();
        }

        // ── 텍스트 구성 ───────────────────────────────────────────────────────

        private void RefreshText()
        {
            if (previewText == null || spawnTable == null) return;

            TagWeightService.ComputeFinalWeights(spawnTable, researchNodes, _finalWeights);

            _sb.Clear();

            StageSpawnTable.AgentSpawnEntry[] entries = spawnTable.Entries;
            int totalCount = 0;
            if (entries != null)
            {
                for (int i = 0; i < entries.Length; i++)
                    totalCount += entries[i].count;
            }

            _sb.Append("이번 웨이브: 총 ").Append(totalCount).Append("마리\n");

            if (entries != null)
            {
                for (int i = 0; i < entries.Length; i++)
                    AppendEntryLine(entries[i]);
            }

            AppendTopTagsLine();

            previewText.text = _sb.ToString();
        }

        /// <summary>엔트리 1건을 "N마리 [확정태그들] + 랜덤 M슬롯" 형식으로 덧붙인다.</summary>
        private void AppendEntryLine(StageSpawnTable.AgentSpawnEntry entry)
        {
            if (entry.archetype == null) return;

            _sb.Append("- ").Append(entry.archetype.DisplayName).Append(' ')
               .Append(entry.count).Append("마리: ");

            int fixedCount = entry.fixedTags != null ? entry.fixedTags.Length : 0;
            if (fixedCount > 0)
            {
                _sb.Append(fixedCount).Append("마리 [");
                for (int f = 0; f < fixedCount; f++)
                {
                    if (f > 0) _sb.Append(", ");
                    AITagDefinition tag = entry.fixedTags[f];
                    _sb.Append(tag != null ? tag.DisplayName : "?");
                }
                _sb.Append("] 확정");

                if (entry.randomTagSlots > 0) _sb.Append(" + ");
            }

            if (entry.randomTagSlots > 0)
                _sb.Append(entry.randomTagSlots).Append("마리 랜덤!");

            _sb.Append('\n');
        }

        /// <summary>랜덤 슬롯 상위 확률 태그 N개를 "[태그] NN% ..." 형식으로 덧붙인다.</summary>
        private void AppendTopTagsLine()
        {
            if (_finalWeights.Count == 0) return;

            _sortBuffer.Clear();
            _sortBuffer.AddRange(_finalWeights);
            _sortBuffer.Sort(DescByWeight);

            _sb.Append("랜덤 슬롯 상위 확률: ");

            int shown = 0;
            for (int i = 0; i < _sortBuffer.Count && shown < topTagCount; i++)
            {
                StageSpawnTable.TagWeightEntry entry = _sortBuffer[i];
                if (entry.tag == null || entry.baseWeight <= 0f) continue;

                float pct = TagWeightService.GetDisplayProbability(_finalWeights, entry.tag);

                if (shown > 0) _sb.Append(' ');
                _sb.Append('[').Append(entry.tag.DisplayName).Append("] ")
                   .Append(pct.ToString("0")).Append('%');
                shown++;
            }

            _sb.Append('\n');
        }
    }
}
