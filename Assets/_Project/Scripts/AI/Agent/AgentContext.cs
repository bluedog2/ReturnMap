using System.Collections.Generic;
using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  AgentContext — 개체 태그·스탯 조회 단일 창구
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 검증 AI 개체 1명의 <see cref="TagSet"/>·<see cref="AgentStats"/> 를 보관하고,
    /// 함정/시스템이 "이 개체가 이 태그를 갖고 있나?" 를 물어보는 단일 창구 역할을 합니다.
    /// <para>
    /// <b>하위 호환</b>: 이 컴포넌트가 배선되지 않은(미배선) VerificationAgent 프리팹도
    /// 존재할 수 있으므로, 모든 소비처(TrapBase/Arrow/DropHammer 등)는 GetComponent 결과가
    /// null 이어도 안전하게 동작해야 합니다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public class AgentContext : MonoBehaviour, IPoolable
    {
        private TagSet        _tags = TagSet.Empty;
        private AgentStats    _stats;
        private AgentArchetype _archetype;

        private Vector3 _originalScale;
        private bool    _originalScaleCaptured;

        private AgentHealth _health; // GetComponent 캐시 (Initialize/OnSpawned 전달용)

        // ── 정적 레지스트리 — FindObjectsByType 씬 스캔 대체 (TrapBase._activeTraps 패턴) ──

        private static readonly List<AgentContext> _activeAgents = new List<AgentContext>();

        /// <summary>
        /// 씬에서 활성화된 모든 검증 AI 개체. 기사단(S-07) 최근접 탐색 등이
        /// 씬 전체 검색 없이 순회. 풀링(SetActive)에 맞춰 OnEnable/OnDisable 로 갱신된다.
        /// </summary>
        public static IReadOnlyList<AgentContext> ActiveAgents => _activeAgents;

        // ── 공개 프로퍼티 ─────────────────────────────────────────────────────

        /// <summary>부여된 태그 묶음.</summary>
        public TagSet Tags => _tags;

        /// <summary>태그가 접힌 최종 스탯 (스폰 시 1회 계산).</summary>
        public AgentStats Stats => _stats;

        /// <summary>태그 적용 전 기본 스펙.</summary>
        public AgentArchetype Archetype => _archetype;

        /// <summary>
        /// 진행 방향 (+1 = 오른쪽, -1 = 왼쪽). VerificationAgent 가 이동 중 갱신하며,
        /// 철벽 방패(FrontShieldOnly) 방향 판정에 쓰인다.
        /// </summary>
        public int FacingSign { get; set; } = 1;

        /// <summary>같은 개체의 AgentHealth (null 허용 — 미부착 프리팹 대비).</summary>
        public AgentHealth Health => _health;

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            _originalScale         = transform.localScale;
            _originalScaleCaptured = true;
            _health                = GetComponent<AgentHealth>();
        }

        /// <summary>레지스트리 자기 등록 (풀에서 SetActive(true) 될 때마다 호출됨).</summary>
        private void OnEnable()  => _activeAgents.Add(this);

        /// <summary>레지스트리 등록 해제 (풀로 반납되어 SetActive(false) 될 때마다 호출됨).</summary>
        private void OnDisable() => _activeAgents.Remove(this);

        // ── 초기화 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 스폰 직후(Director) 1회 호출. 스탯을 접어 캐시하고, 크기·체력을 초기화한 뒤
        /// 스폰 훅을 디스패치합니다.
        /// </summary>
        public void Initialize(AgentArchetype archetype, TagSet tags)
        {
            _archetype = archetype;
            _tags      = tags ?? TagSet.Empty;
            _stats     = AgentStats.From(_archetype, _tags); // 스폰 시 1회 계산 — 프레임 중 재계산 금지

            if (!_originalScaleCaptured)
            {
                _originalScale         = transform.localScale;
                _originalScaleCaptured = true;
            }
            transform.localScale = _originalScale * _stats.Scale;

            FacingSign = 1;

            if (_health == null) _health = GetComponent<AgentHealth>();
            _health?.ResetHealth(_stats.MaxHP);

            DispatchSpawnHooks();
        }

        // ── 편의 메서드 ───────────────────────────────────────────────────────

        /// <summary>주어진 피해 타입에 면역인지.</summary>
        public bool IsImmuneTo(DamageType type) => _tags.IsImmuneTo(type);

        /// <summary>주어진 특수 플래그를 보유했는지.</summary>
        public bool HasFlag(SpecialFlag flag) => _tags.HasFlag(flag);

        // ── 훅 디스패치 ───────────────────────────────────────────────────────
        // 전 태그의 eventHooks 배열을 for 루프로 순회하며 해당 virtual 을 호출한다.
        // LINQ/프레임 할당 없이 인덱스 기반으로 순회.

        /// <summary>스폰 시 훅 디스패치 (예: 가성비 타파 코스트 차감).</summary>
        public void DispatchSpawnHooks()
        {
            var tagList = _tags.Tags;
            for (int i = 0; i < tagList.Count; i++)
            {
                AITagDefinition tag = tagList[i];
                if (tag == null || tag.EventHooks == null) continue;

                TagEventHook[] hooks = tag.EventHooks;
                for (int j = 0; j < hooks.Length; j++)
                    hooks[j]?.OnSpawn(this);
            }
        }

        /// <summary>사망 시 훅 디스패치 (예: 기사단 쉴드, 도둑 재화).</summary>
        public void DispatchDeathHooks()
        {
            var tagList = _tags.Tags;
            for (int i = 0; i < tagList.Count; i++)
            {
                AITagDefinition tag = tagList[i];
                if (tag == null || tag.EventHooks == null) continue;

                TagEventHook[] hooks = tag.EventHooks;
                for (int j = 0; j < hooks.Length; j++)
                    hooks[j]?.OnDeath(this);
            }
        }

        /// <summary>함정 피격 시 훅 디스패치 (예: 백스텝).</summary>
        public void DispatchTrapHitHooks(TrapBase trap)
        {
            var tagList = _tags.Tags;
            for (int i = 0; i < tagList.Count; i++)
            {
                AITagDefinition tag = tagList[i];
                if (tag == null || tag.EventHooks == null) continue;

                TagEventHook[] hooks = tag.EventHooks;
                for (int j = 0; j < hooks.Length; j++)
                    hooks[j]?.OnTrapHit(this, trap);
            }
        }

        // ── IPoolable — ComponentPool 재사용 훅 ──────────────────────────────
        // 주의: ComponentPool<VerificationAgent> 는 풀링 대상 타입(VerificationAgent)의
        // IPoolable 만 직접 호출한다. 이 컴포넌트의 훅은 VerificationAgent.OnSpawned/
        // OnDespawned 가 명시적으로 전달(forwarding)해 줘야 실행된다.

        /// <summary>풀에서 꺼내질 때 이전 태그/스케일 잔류 제거. Initialize 가 곧 실제 값으로 덮어씀.</summary>
        public void OnSpawned()
        {
            _tags      = TagSet.Empty;
            FacingSign = 1;
            if (_originalScaleCaptured) transform.localScale = _originalScale;
            _health?.OnSpawned();
        }

        /// <summary>풀로 반납될 때 스케일 원복.</summary>
        public void OnDespawned()
        {
            if (_originalScaleCaptured) transform.localScale = _originalScale;
            _health?.OnDespawned();
        }
    }
}
