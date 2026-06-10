using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  TrapSlotMarker — 런타임 함정 슬롯 데이터 보관
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// MapLoader 가 슬롯 마커 오브젝트에 부착합니다.
    /// Build Phase UI 가 이 컴포넌트를 탐색해 설치 가능 위치를 파악합니다.
    ///
    /// <para><b>5단계</b>에서 TrapSlotRegistry 가 이 컴포넌트를 수집·관리합니다.</para>
    /// </summary>
    public class TrapSlotMarker : MonoBehaviour
    {
        // ── 슬롯 정보 (읽기 전용) ────────────────────────────────────────────

        public int        GridX      { get; private set; }
        public int        GridY      { get; private set; }
        public TrapAnchor Anchor     { get; private set; }

        /// <summary>현재 이 슬롯에 설치된 함정 GameObject. null 이면 비어있음.</summary>
        public GameObject OccupiedBy { get; private set; }

        /// <summary>슬롯이 비어있는지 여부.</summary>
        public bool IsEmpty => OccupiedBy == null;

        // ── 초기화 / 레지스트리 등록 ─────────────────────────────────────────

        private bool _registered;

        /// <summary>
        /// MapLoader 에서 생성 직후 호출. 좌표 확정과 동시에
        /// <see cref="TrapSlotRegistry"/> 에 등록됩니다.
        /// (OnEnable 등록은 AddComponent 시점에 좌표가 0,0 이라 사용 불가)
        /// </summary>
        public void Init(int x, int y, TrapAnchor anchor)
        {
            GridX  = x;
            GridY  = y;
            Anchor = anchor;

            TrapSlotRegistry.Register(this);
            _registered = true;
        }

        private void OnDestroy()
        {
            if (_registered)
                TrapSlotRegistry.Unregister(this);
        }

        // ── 5단계 Build Phase 연동 ────────────────────────────────────────────

        /// <summary>함정 설치. Build Phase UI 에서 호출합니다.</summary>
        public bool TryOccupy(GameObject trapObject)
        {
            if (!IsEmpty) return false;
            OccupiedBy = trapObject;
            TrapSlotRegistry.NotifyOccupancyChanged();
            return true;
        }

        /// <summary>함정 제거. Build Phase UI 에서 호출합니다.</summary>
        public void Vacate()
        {
            if (OccupiedBy == null) return;
            OccupiedBy = null;
            TrapSlotRegistry.NotifyOccupancyChanged();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = IsEmpty
                ? new Color(1f, 0.5f, 0f, 0.6f)
                : new Color(0.2f, 0.9f, 0.3f, 0.6f);

            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.8f);
        }
#endif
    }
}
