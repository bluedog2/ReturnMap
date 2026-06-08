using UnityEngine;
using Unity.Cinemachine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  CameraConfinerSync — 맵 크기 → CinemachineConfiner2D 경계 자동 동기화
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>CameraBounds</b> 오브젝트에 부착합니다.
    ///
    /// <para>맵이 로드될 때 <see cref="SetFromMap"/>을 호출하면
    /// BoxCollider2D 크기와 오프셋을 맵 월드 크기에 맞게 갱신하고
    /// <see cref="CinemachineConfiner2D"/> 캐시를 무효화합니다.</para>
    ///
    /// <para><b>MapLoader 연동 (4단계 완료 후)</b></para>
    /// <code>
    /// CameraConfinerSync.Instance?.SetFromMap(mapData, mapOrigin);
    /// </code>
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class CameraConfinerSync : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Tooltip("PlayerFollowCamera 의 CinemachineConfiner2D. " +
                 "비워두면 씬에서 자동 탐색합니다.")]
        [SerializeField] private CinemachineConfiner2D _confiner;

        // ── 런타임 ────────────────────────────────────────────────────────────

        private BoxCollider2D _col;

        // ── 싱글턴 ────────────────────────────────────────────────────────────

        public static CameraConfinerSync Instance { get; private set; }

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            _col = GetComponent<BoxCollider2D>();

            // Confiner 자동 탐색
            if (_confiner == null)
                _confiner = FindFirstObjectByType<CinemachineConfiner2D>();

            if (Instance == null) Instance = this;
            else { Destroy(this); return; }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── 공개 API ──────────────────────────────────────────────────────────

        /// <summary>
        /// 맵 데이터를 기준으로 BoxCollider2D 경계를 갱신합니다.
        /// <para>CinemachineConfiner2D 가 자동으로 새 경계를 적용합니다.</para>
        /// </summary>
        /// <param name="map">로드된 맵 데이터.</param>
        /// <param name="mapOrigin">맵 오브젝트의 월드 좌표 원점 (기본 Vector2.zero).</param>
        public void SetFromMap(MapData map, Vector2 mapOrigin = default)
        {
            if (map == null) return;
            if (_col == null) _col = GetComponent<BoxCollider2D>();

            float w = map.width  * map.tileUnit;
            float h = map.height * map.tileUnit;

            // 맵 월드 중심 = origin + (w/2, h/2)
            // BoxCollider2D.offset 은 transform 기준 상대 좌표
            _col.size   = new Vector2(w, h);
            _col.offset = (mapOrigin + new Vector2(w * 0.5f, h * 0.5f))
                          - (Vector2)transform.position;

            // Cinemachine Confiner2D 캐시 무효화 → 새 경계 즉시 반영
            if (_confiner != null)
                _confiner.InvalidateBoundingShapeCache();

            Debug.Log($"[CameraConfinerSync] 경계 갱신 → size({w:F1}, {h:F1})  " +
                      $"offset({_col.offset.x:F2}, {_col.offset.y:F2})");
        }

        // ── 에디터 기즈모 ─────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_col == null) return;
            Gizmos.color = new Color(0f, 1f, 1f, 0.4f);
            Gizmos.DrawWireCube(
                transform.position + (Vector3)(Vector2)_col.offset,
                _col.size);
        }
#endif
    }
}
