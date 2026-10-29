using System.Collections;
using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  PlayerEffects — 플레이어 대시·피격·사망·부활 일회성 프레임 이펙트
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>Player</b> 루트에 부착합니다. 각 이벤트 발생 시 해당 이펙트 프레임 배열을
    /// 플레이어 위치에서 1회 재생합니다 (재발생 시 처음부터 다시 재생).
    ///
    /// <para><b>구독 이벤트</b>: <see cref="PlayerController.OnDashStart"/>, <see cref="PlayerController.OnLand"/>(착지 먼지),
    /// <see cref="PlayerHealth.OnDamageTaken"/>(사망 피격 제외), <see cref="PlayerController.OnKnockback"/>,
    /// <see cref="RespawnManager.OnDeath"/>, <see cref="RespawnManager.OnRespawn"/>.</para>
    ///
    /// <para><b>왜 플레이어 계층 밖에 렌더러를 만드는가</b>: <see cref="PlayerPhaseVisibility"/> 가
    /// 자식 SpriteRenderer 를 전부 토글하므로, 자식으로 두면 페이즈 전환 때 이펙트가 함께 켜지고 꺼진다.
    /// 또한 대시 잔상이 플레이어를 따라오지 않고 발동 위치에 남는다.</para>
    ///
    /// <para><b>재생 조건</b>: 플레이 페이즈에서만 재생하며, 페이즈를 벗어나면 즉시 숨긴다.</para>
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerEffects : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("이펙트 프레임")]
        [SerializeField]
        [Tooltip("대시 시작 이펙트 프레임 (오른쪽 방향 기준, 왼쪽 대시 시 좌우 반전).")]
        private Sprite[] dashFrames;

        [SerializeField]
        [Tooltip("피격 이펙트 프레임.")]
        private Sprite[] hitFrames;

        [SerializeField]
        [Tooltip("사망 이펙트 프레임.")]
        private Sprite[] deathFrames;

        [SerializeField]
        [Tooltip("리스폰 이펙트 프레임.")]
        private Sprite[] respawnFrames;

        [SerializeField]
        [Tooltip("착지 먼지 프레임 (발밑, 하단 피벗).")]
        private Sprite[] dustFrames;

        [Header("재생 설정")]
        [SerializeField, Min(1f)]
        [Tooltip("재생 속도(프레임/초).")]
        private float fps = 12f;

        [SerializeField, Min(0.01f)]
        [Tooltip("이펙트 스케일 배율. 이펙트 스프라이트 1유닛 = 플레이어 몸 높이 기준.")]
        private float effectScale = 1f;

        [SerializeField, Min(0f)]
        [Tooltip("피격·사망 이펙트(중앙 피벗)를 놓을 높이 — 발바닥 기준 몸 중심(유닛). 대시·리스폰·먼지는 하단 피벗이라 발바닥에 놓는다.")]
        private float bodyCenterHeight = 1.25f;

        // ── 내부 상태 ─────────────────────────────────────────────────────────

        private const int ChannelCount = 5;
        private const int Dash = 0, Hit = 1, Death = 2, Respawn = 3, Dust = 4;

        private PlayerController _controller;
        private PlayerHealth     _health;
        private float            _feetLocalY;

        private GameObject       _root;
        private SpriteRenderer[] _renderers = new SpriteRenderer[ChannelCount];
        private Coroutine[]      _routines  = new Coroutine[ChannelCount];

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            _controller = GetComponent<PlayerController>();
            _health     = GetComponent<PlayerHealth>();

            // 발바닥 = Visual 하단 피벗 (Px32HeroWiring 이 콜라이더 바닥에 맞춤)
            var visual = transform.Find("Visual");
            _feetLocalY = visual != null ? visual.localPosition.y : 0f;

            // 플레이어 계층 밖 런타임 루트 — PlayerPhaseVisibility 의 렌더러 일괄 토글 영향 회피
            _root = new GameObject("PlayerEffects_Runtime");

            string[] names  = { "Dash", "Hit", "Death", "Respawn", "Dust" };
            int[]    orders = { 19, 21, 21, 21, 19 }; // 캐릭터(20) 뒤 / 앞
            for (int i = 0; i < ChannelCount; i++)
            {
                var go = new GameObject(names[i]);
                go.transform.SetParent(_root.transform, false);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingLayerName = "Map";
                sr.sortingOrder     = orders[i];
                sr.enabled          = false;
                _renderers[i] = sr;
            }
        }

        private void OnEnable()
        {
            if (_controller != null) _controller.OnDashStart += HandleDashStart;
            if (_controller != null) _controller.OnKnockback += HandleKnockback;
            if (_controller != null) _controller.OnLand      += HandleLand;
            if (_health     != null) _health.OnDamageTaken   += HandleDamageTaken;
            RespawnManager.OnDeath   += HandleDeath;
            RespawnManager.OnRespawn += HandleRespawn;
            GamePhaseManager.OnPhaseChanged += HandlePhaseChanged;
        }

        private void OnDisable()
        {
            if (_controller != null) _controller.OnDashStart -= HandleDashStart;
            if (_controller != null) _controller.OnKnockback -= HandleKnockback;
            if (_controller != null) _controller.OnLand      -= HandleLand;
            if (_health     != null) _health.OnDamageTaken   -= HandleDamageTaken;
            RespawnManager.OnDeath   -= HandleDeath;
            RespawnManager.OnRespawn -= HandleRespawn;
            GamePhaseManager.OnPhaseChanged -= HandlePhaseChanged;

            HideAll();
        }

        private void OnDestroy()
        {
            if (_root != null) Destroy(_root);
        }

        // ── 이벤트 핸들러 ─────────────────────────────────────────────────────

        private void HandleDashStart()
            => PlayEffect(Dash, dashFrames, flipX: !_controller.IsFacingRight, atBodyCenter: false);

        private void HandleDamageTaken(int currentHp, int damage)
        {
            if (currentHp <= 0) return; // 사망 피격은 사망 이펙트가 담당
            PlayEffect(Hit, hitFrames, flipX: false, atBodyCenter: true);
        }

        // 넉백(비치명 피격)도 피격 이펙트 — maxHp 1 이면 데미지 피격은 전부 사망이라 넉백이 주 경로
        private void HandleKnockback() => PlayEffect(Hit, hitFrames, flipX: false, atBodyCenter: true);

        private void HandleDeath()   => PlayEffect(Death,   deathFrames,   flipX: false, atBodyCenter: true);
        private void HandleRespawn() => PlayEffect(Respawn, respawnFrames, flipX: false, atBodyCenter: false);
        private void HandleLand()    => PlayEffect(Dust,    dustFrames,    flipX: false, atBodyCenter: false);

        private void HandlePhaseChanged(GamePhase phase)
        {
            if (phase != GamePhase.Play) HideAll();
        }

        // ── 재생 ──────────────────────────────────────────────────────────────

        private static bool IsPlayPhase()
            => GamePhaseManager.Instance != null &&
               GamePhaseManager.Instance.currentPhase == GamePhase.Play;

        private void PlayEffect(int channel, Sprite[] frames, bool flipX, bool atBodyCenter)
        {
            if (frames == null || frames.Length == 0) return;
            if (!IsPlayPhase()) return;

            StopChannel(channel);

            var sr = _renderers[channel];
            sr.flipX = flipX;
            float y = _feetLocalY + (atBodyCenter ? bodyCenterHeight : 0f);
            sr.transform.position   = transform.position + new Vector3(0f, y, 0f);
            sr.transform.localScale = new Vector3(effectScale, effectScale, 1f);

            _routines[channel] = StartCoroutine(PlayRoutine(channel, frames));
        }

        private IEnumerator PlayRoutine(int channel, Sprite[] frames)
        {
            var sr = _renderers[channel];
            var wait = new WaitForSeconds(1f / fps);

            sr.enabled = true;
            for (int i = 0; i < frames.Length; i++)
            {
                sr.sprite = frames[i];
                yield return wait;
            }

            sr.enabled = false;
            _routines[channel] = null;
        }

        private void StopChannel(int channel)
        {
            if (_routines[channel] != null)
            {
                StopCoroutine(_routines[channel]);
                _routines[channel] = null;
            }
            if (_renderers[channel] != null) _renderers[channel].enabled = false;
        }

        private void HideAll()
        {
            for (int i = 0; i < ChannelCount; i++)
                StopChannel(i);
        }
    }
}
