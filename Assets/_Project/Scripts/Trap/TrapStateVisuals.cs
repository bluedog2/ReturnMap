using System;
using System.Collections.Generic;
using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  TrapStateSprites — 변이 4상태별 스프라이트 묶음
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Normal / Dud / Critical / Beneficial 상태별 스프라이트 4종.
    /// 비어 있는(null) 상태는 "표시 없음"을 뜻한다 (예: Glow 는 Normal/Dud 가 null).
    /// </summary>
    [Serializable]
    public struct TrapStateSprites
    {
        [SerializeField] private Sprite normal;
        [SerializeField] private Sprite dud;
        [SerializeField] private Sprite critical;
        [SerializeField] private Sprite beneficial;

        [Tooltip("빌드·AI 검증 페이즈 외형 (타일 Build 세트와 같은 분위기). 이 페이즈엔 변이가 없어 Normal 1장. 비우면 normal.")]
        [SerializeField] private Sprite build;

        /// <summary>
        /// 페이즈까지 고려한 스프라이트: buildLook(빌드·AI 페이즈)이고 Normal 이면 build 외형(있을 때), 그 외 상태별.
        /// </summary>
        public Sprite Get(TrapState state, bool buildLook)
            => buildLook && state == TrapState.Normal && build != null ? build : Get(state);

        /// <summary>상태에 대응하는 스프라이트. 미지정이면 null.</summary>
        public Sprite Get(TrapState state)
        {
            switch (state)
            {
                case TrapState.Dud:        return dud;
                case TrapState.Critical:   return critical;
                case TrapState.Beneficial: return beneficial;
                default:                   return normal;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  TrapStateVisuals — 함정 상태별 스프라이트 스왑
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 함정 루트에 부착. <see cref="TrapBase"/> 가 상태 변이 시
    /// <see cref="Apply"/> 를 호출해 바인딩된 SpriteRenderer 들의 스프라이트를 교체한다.
    ///
    /// <para><b>일반 바인딩</b>: 스프라이트 교체 + (스프라이트 null → 렌더러 비활성).</para>
    /// <para><b>transient 바인딩</b>(머즐 플래시·충격파 등 순간 연출): 스프라이트만 교체하고
    /// 활성 여부는 함정 로직이 직접 제어한다.</para>
    ///
    /// <para>여기에 바인딩된 렌더러는 <see cref="TrapBase"/> 의 Beneficial 황금 틴트 대상에서
    /// 제외된다(<see cref="Owns"/>) — 전용 스프라이트가 이미 상태 색을 담고 있기 때문.</para>
    /// </summary>
    public class TrapStateVisuals : MonoBehaviour
    {
        [Serializable]
        private class Binding
        {
            [Tooltip("스프라이트를 교체할 렌더러.")]
            public SpriteRenderer target;

            [Tooltip("상태별 스프라이트.")]
            public TrapStateSprites sprites;

            [Tooltip("true 면 스프라이트만 교체하고 활성 여부는 건드리지 않는다 (순간 연출용).")]
            public bool transient;
        }

        [Header("상태별 스프라이트 바인딩")]
        [SerializeField]
        [Tooltip("렌더러별 상태 스프라이트 목록.")]
        private List<Binding> bindings = new List<Binding>();

        /// <summary>상태에 맞게 모든 바인딩의 스프라이트를 교체한다 (플레이 외형).</summary>
        public void Apply(TrapState state) => Apply(state, false);

        /// <summary>
        /// 상태·페이즈 외형에 맞게 모든 바인딩의 스프라이트를 교체한다.
        /// buildLook = 빌드·AI 검증 페이즈 → 바인딩에 build 스프라이트가 있으면 그것.
        /// </summary>
        public void Apply(TrapState state, bool buildLook)
        {
            for (int i = 0; i < bindings.Count; i++)
            {
                var b = bindings[i];
                if (b == null || b.target == null) continue;

                Sprite sprite = b.sprites.Get(state, buildLook);
                b.target.sprite = sprite;
                if (!b.transient)
                    b.target.enabled = sprite != null;
            }
        }

        /// <summary>이 컴포넌트가 관리하는 렌더러인지.</summary>
        public bool Owns(SpriteRenderer sr)
        {
            if (sr == null) return false;
            for (int i = 0; i < bindings.Count; i++)
                if (bindings[i] != null && bindings[i].target == sr) return true;
            return false;
        }
    }
}
