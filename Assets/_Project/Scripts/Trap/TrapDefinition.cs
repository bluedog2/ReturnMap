using UnityEngine;
using UnityEngine.AddressableAssets;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  TrapDefinition — 함정 스펙 중앙화 (ScriptableObject)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 함정 1종의 스펙(코스트·위험도·호환 슬롯·HUD 표시·프리팹 참조)을 한 곳에 모은 에셋.
    /// <para>
    /// 기존에는 baseCost/dangerLevel 이 프리팹 인스펙터 + TrapPrefabBuilder 매직넘버로
    /// 이중 관리되고, CompatibleAnchors 가 서브클래스마다 복붙되었다.
    /// 이 SO 를 <see cref="TrapBase.Definition"/> 에 배선하면 새 함정 추가 시
    /// "행동 클래스 1개 + SO 에셋 1개"만 있으면 된다.
    /// </para>
    /// <para>위치 권장: Assets/_Project/Settings/TrapDefinitions/TrapDefinition_Xxx.asset</para>
    /// </summary>
    [CreateAssetMenu(menuName = "ReTrap/Trap Definition", fileName = "TrapDefinition_")]
    public class TrapDefinition : ScriptableObject
    {
        [Header("HUD 표시")]
        [SerializeField]
        [Tooltip("Build HUD 핫바에 표시할 함정 이름.")]
        private string displayName;

        [SerializeField]
        [Tooltip("Build HUD 핫바 아이콘. 비워두면 HUD 가 프리팹의 Visual 스프라이트에서 자동 추출(기존 방식 폴백).")]
        private Sprite icon;

        [Header("코스트 & 위험도")]
        [SerializeField]
        [Tooltip("빌드 페이즈 소비 코스트. 역코스트 원칙: 위험할수록 낮게, 안전할수록 높게.")]
        private int baseCost = 10;

        [SerializeField, Range(0, 10)]
        [Tooltip("위험도. 높을수록 BaseCost 가 낮아진다 (역코스트).")]
        private int dangerLevel = 5;

        [Header("피해 타입")]
        [SerializeField]
        [Tooltip("이 함정이 가하는 피해 타입. AI 태그의 면역(DamageType 마스크) 판정에 사용됨.")]
        private DamageType damageType = DamageType.None;

        [Header("슬롯 호환")]
        [SerializeField]
        [Tooltip("이 함정을 설치할 수 있는 슬롯 anchor 목록. Build UI 가 설치 가능 슬롯 필터링에 사용.")]
        private TrapAnchor[] compatibleAnchors;

        [Header("프리팹 참조")]
        [SerializeField]
        [Tooltip("이 함정의 Addressable 프리팹 참조 (TrapBase 포함 필수).")]
        private AssetReferenceGameObject prefabRef;

        // ── 공개 프로퍼티 ─────────────────────────────────────────────────────

        /// <summary>Build HUD 핫바에 표시할 함정 이름.</summary>
        public string DisplayName => displayName;

        /// <summary>Build HUD 핫바 아이콘. null 이면 HUD 가 프리팹 스프라이트에서 폴백 추출.</summary>
        public Sprite Icon => icon;

        /// <summary>빌드 페이즈 소비 코스트.</summary>
        public int BaseCost => baseCost;

        /// <summary>위험도 0(안전) ~ 10(치명).</summary>
        public int DangerLevel => dangerLevel;

        /// <summary>이 함정이 가하는 피해 타입 (AI 태그 면역 판정용).</summary>
        public DamageType DamageType => damageType;

        /// <summary>이 함정을 설치할 수 있는 슬롯 anchor 목록.</summary>
        public TrapAnchor[] CompatibleAnchors => compatibleAnchors;

        /// <summary>함정 프리팹 Addressable 참조.</summary>
        public AssetReferenceGameObject PrefabRef => prefabRef;

        /// <summary>해당 anchor 슬롯에 설치 가능한지.</summary>
        public bool IsCompatibleWith(TrapAnchor anchor)
        {
            if (compatibleAnchors == null) return false;
            for (int i = 0; i < compatibleAnchors.Length; i++)
                if (compatibleAnchors[i] == anchor) return true;
            return false;
        }
    }
}
