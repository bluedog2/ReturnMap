using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  AgentKillFx — 검증 AI 처치 이펙트 (전역 1개)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <see cref="AgentHealth.OnAgentKilled"/> 를 구독해 사망 위치에서 프레임 이펙트를 1회 재생합니다.
    /// 에이전트는 사망 즉시 풀로 돌아가므로 이펙트는 에이전트 계층 밖 자체 렌더러 풀에서 그립니다.
    /// </summary>
    public class AgentKillFx : MonoBehaviour
    {
        [Header("이펙트")]
        [Tooltip("처치 연기 프레임 (중앙 피벗).")]
        [SerializeField] private Sprite[] frames;

        [Tooltip("재생 속도(프레임/초).")]
        [SerializeField, Min(1f)] private float fps = 15f;

        [Tooltip("Map 레이어 정렬 순서 — 캐릭터(20) 앞.")]
        [SerializeField] private int sortingOrder = 21;

        private readonly List<SpriteRenderer> _pool = new List<SpriteRenderer>();

        private void OnEnable()  => AgentHealth.OnAgentKilled += HandleKilled;
        private void OnDisable() => AgentHealth.OnAgentKilled -= HandleKilled;

        private void HandleKilled(Vector3 position)
        {
            if (frames == null || frames.Length == 0 || !isActiveAndEnabled) return;
            var sr = Rent();
            sr.transform.position = position;
            StartCoroutine(PlayRoutine(sr));
        }

        private SpriteRenderer Rent()
        {
            for (int i = 0; i < _pool.Count; i++)
                if (!_pool[i].enabled) return _pool[i];

            var go = new GameObject("KillFx");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = "Map";
            sr.sortingOrder     = sortingOrder;
            sr.enabled          = false;
            _pool.Add(sr);
            return sr;
        }

        private IEnumerator PlayRoutine(SpriteRenderer sr)
        {
            var wait = new WaitForSeconds(1f / fps);
            sr.enabled = true;
            for (int i = 0; i < frames.Length; i++)
            {
                sr.sprite = frames[i];
                yield return wait;
            }
            sr.enabled = false;
        }
    }
}
