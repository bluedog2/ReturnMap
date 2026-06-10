using System;
using System.Collections.Generic;
using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  TrapSlotRegistry — 함정 슬롯 전역 조회소 (5단계)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 씬에 존재하는 모든 <see cref="TrapSlotMarker"/> 를 좌표로 조회하는 정적 레지스트리.
    ///
    /// <para>마커가 생성(Init)/파괴 시 스스로 등록·해제하므로
    /// 씬 오브젝트나 수동 세팅이 전혀 필요 없습니다.</para>
    ///
    /// <para><b>Build Phase UI 사용 예</b></para>
    /// <code>
    /// // 설치 가능한 빈 바닥 슬롯 순회
    /// foreach (var slot in TrapSlotRegistry.GetEmpty(TrapAnchor.Floor)) { ... }
    ///
    /// // 클릭한 셀에 슬롯이 있는지
    /// if (TrapSlotRegistry.TryGet(x, y, out var slot) &amp;&amp; slot.IsEmpty)
    ///     slot.TryOccupy(trapInstance);
    /// </code>
    /// </summary>
    public static class TrapSlotRegistry
    {
        // ── 저장소 ────────────────────────────────────────────────────────────

        private static readonly Dictionary<Vector2Int, TrapSlotMarker> _slots
            = new Dictionary<Vector2Int, TrapSlotMarker>();

        // ── 이벤트 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 슬롯 등록/해제/점유 상태 변화 시 발행.
        /// Build UI 가 목록·하이라이트를 갱신할 때 구독합니다.
        /// </summary>
        public static event Action OnChanged;

        // ── 조회 API ──────────────────────────────────────────────────────────

        /// <summary>등록된 슬롯 수.</summary>
        public static int Count => _slots.Count;

        /// <summary>모든 슬롯 (순서 비보장).</summary>
        public static IEnumerable<TrapSlotMarker> All => _slots.Values;

        /// <summary>좌표로 슬롯 조회. 없으면 false.</summary>
        public static bool TryGet(int x, int y, out TrapSlotMarker slot)
            => _slots.TryGetValue(new Vector2Int(x, y), out slot);

        /// <summary>좌표로 슬롯 조회. 없으면 null.</summary>
        public static TrapSlotMarker Get(int x, int y)
            => _slots.TryGetValue(new Vector2Int(x, y), out var s) ? s : null;

        /// <summary>특정 anchor 방향의 슬롯만 순회.</summary>
        public static IEnumerable<TrapSlotMarker> GetByAnchor(TrapAnchor anchor)
        {
            foreach (var s in _slots.Values)
                if (s.Anchor == anchor)
                    yield return s;
        }

        /// <summary>비어있는 슬롯만 순회. anchor 를 주면 방향까지 필터링.</summary>
        public static IEnumerable<TrapSlotMarker> GetEmpty(TrapAnchor? anchor = null)
        {
            foreach (var s in _slots.Values)
            {
                if (!s.IsEmpty) continue;
                if (anchor.HasValue && s.Anchor != anchor.Value) continue;
                yield return s;
            }
        }

        /// <summary>설치된 함정이 있는 슬롯만 순회.</summary>
        public static IEnumerable<TrapSlotMarker> GetOccupied()
        {
            foreach (var s in _slots.Values)
                if (!s.IsEmpty)
                    yield return s;
        }

        // ── 내부 — 마커 자가 등록 (TrapSlotMarker 전용) ───────────────────────

        internal static void Register(TrapSlotMarker marker)
        {
            var key = new Vector2Int(marker.GridX, marker.GridY);

            if (_slots.TryGetValue(key, out var existing) && existing != marker)
                Debug.LogWarning($"[TrapSlotRegistry] 슬롯 좌표 중복 {key} — " +
                                 $"기존 '{existing.name}' 을 '{marker.name}' 으로 교체합니다.");

            _slots[key] = marker;
            OnChanged?.Invoke();
        }

        internal static void Unregister(TrapSlotMarker marker)
        {
            var key = new Vector2Int(marker.GridX, marker.GridY);

            // 같은 좌표에 다른 마커가 재등록된 경우를 보호
            if (_slots.TryGetValue(key, out var current) && current == marker)
            {
                _slots.Remove(key);
                OnChanged?.Invoke();
            }
        }

        /// <summary>점유 상태 변화 통지 (TryOccupy / Vacate 에서 호출).</summary>
        internal static void NotifyOccupancyChanged() => OnChanged?.Invoke();
    }
}
