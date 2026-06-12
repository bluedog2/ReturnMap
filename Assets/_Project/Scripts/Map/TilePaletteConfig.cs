using System;
using System.Collections.Generic;
using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  TilePaletteConfig — 타일 팔레트 설정 (ScriptableObject)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// TileType / TrapAnchor → 스프라이트·색·물리 데이터 매핑.
    /// 에디터 MapEditorWindow 와 런타임 MapLoader 가 이 하나를 공유합니다.
    /// <para>프로젝트당 1개. 위치 권장: Assets/_Project/Settings/TilePaletteConfig.asset</para>
    /// </summary>
    [Serializable]
    public class TileVisual
    {
        [Tooltip("어떤 TileType 과 매핑되는 엔트리인지 명시합니다.")]
        public TileType   type;

        [Tooltip("에디터 미리보기 + 런타임 SpriteRenderer 에 사용할 스프라이트. " +
                 "Tiles 아틀라스에 포함되어 있으면 배칭이 자동으로 묶입니다.")]
        public Sprite     sprite;

        [Tooltip("스프라이트가 비어있을 때 에디터 셀에 채울 색입니다.")]
        public Color      fallbackColor = Color.gray;

        [Tooltip("true면 MapLoader 가 Collider2D 를 부착해 물리 충돌을 처리합니다.")]
        public bool       isSolid;

        [Tooltip("솔리드 타일을 올릴 물리 레이어. 단일 레이어 하나만 선택하세요.\n" +
                 "PlayerController.groundLayer / DropHammer.groundLayer 와 일치시켜야 합니다.")]
        public LayerMask  collisionLayer;

        [Tooltip("절차적 생성 대신 이 프리팹을 Instantiate 합니다. " +
                 "비워두면 SpriteRenderer + Collider2D 절차 생성 (배칭 보장).")]
        public GameObject prefabOverride;
    }

    /// <summary>함정 슬롯 오버레이의 시각 설정.</summary>
    [Serializable]
    public class SlotVisual
    {
        [Tooltip("어떤 TrapAnchor 와 매핑되는 엔트리인지 명시합니다.")]
        public TrapAnchor anchor;

        [Tooltip("슬롯 방향을 나타내는 아이콘 스프라이트. " +
                 "(예: Floor=위쪽 화살, Ceiling=아래쪽 화살)")]
        public Sprite     markerSprite;

        [Tooltip("슬롯 마커에 곱할 색. 반투명 주황 권장 (alpha 0.6).")]
        public Color      tint = TilePaletteConfig.DefaultSlotTint;
    }

    // ═══════════════════════════════════════════════════════════════════════════

    [CreateAssetMenu(menuName = "ReTrap/Tile Palette Config", fileName = "TilePaletteConfig")]
    public class TilePaletteConfig : ScriptableObject
    {
        // ── 폴백 기본값 (에디터·로더 공용 — 단일 정의) ───────────────────────

        /// <summary>슬롯 마커 기본 색 (반투명 주황). 팔레트/엔트리 미설정 시 공용 폴백.</summary>
        public static readonly Color DefaultSlotTint = new Color(1f, 0.5f, 0f, 0.6f);

        /// <summary>
        /// 팔레트 에셋 자체가 없을 때 사용할 타입별 기본 색.
        /// MapEditorWindow / MapLoader 가 공유합니다 — 색 변경은 여기 한 곳만.
        /// </summary>
        public static Color FallbackColor(TileType type)
        {
            switch (type)
            {
                case TileType.Floor:   return new Color(0.55f, 0.38f, 0.24f); // 갈색
                case TileType.Wall:    return new Color(0.45f, 0.45f, 0.48f); // 회색
                case TileType.Ceiling: return new Color(0.28f, 0.28f, 0.32f); // 짙은 회색
                default:               return new Color(0.18f, 0.18f, 0.20f); // Empty
            }
        }

        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("타일 엔트리 (TileType 1개당 1개)")]
        [SerializeField] private List<TileVisual> tiles = new List<TileVisual>();

        [Header("슬롯 마커 (TrapAnchor 1개당 1개)")]
        [SerializeField] private List<SlotVisual> slotMarkers = new List<SlotVisual>();

        [Header("슬롯 봉인 타일 (Play 중 빈 슬롯을 막는 타일)")]
        [Tooltip("봉인 타일 전용 스프라이트. 비워두면 anchor 에 맞는 일반 타일 스프라이트를 사용합니다.")]
        [SerializeField] private Sprite sealSprite;

        [Tooltip("봉인 타일에 곱할 틴트 색. 기본 흰색 = 스프라이트 원본 색 그대로.")]
        [SerializeField] private Color sealTint = Color.white;

        [Header("렌더 설정 — 배칭 조건")]
        [Tooltip("모든 타일·슬롯 마커가 공유할 머티리얼. Sprites-Default 권장.\n" +
                 "같은 머티리얼 + 같은 아틀라스 = 배칭 자동 성립.")]
        [SerializeField] private Material sharedMaterial;

        [Tooltip("타일 오브젝트의 Sorting Layer 이름. 'Map' 레이어 사용 권장.")]
        [SerializeField] private string   tileSortingLayer = "Map";

        [Tooltip("타일의 Sorting Order.")]
        [SerializeField] private int      tileSortingOrder;

        [Tooltip("슬롯 마커의 Sorting Order. 타일보다 큰 값으로 설정하세요.")]
        [SerializeField] private int      slotSortingOrder = 10;

        // ── 런타임 캐시 (Dictionary는 직렬화 불가 → OnEnable 에서 구축) ────────

        private Dictionary<TileType,   TileVisual> _tileCache;
        private Dictionary<TrapAnchor, SlotVisual> _slotCache;

        // ── 공개 프로퍼티 — 렌더 설정 ────────────────────────────────────────

        public Material SharedMaterial   => sharedMaterial;
        public string   TileSortingLayer => tileSortingLayer;
        public int      TileSortingOrder => tileSortingOrder;
        public int      SlotSortingOrder => slotSortingOrder;

        /// <summary>봉인 타일 전용 스프라이트. null 이면 일반 타일 스프라이트 폴백.</summary>
        public Sprite   SealSprite       => sealSprite;

        /// <summary>봉인 타일 틴트 색.</summary>
        public Color    SealTint         => sealTint;

        // ── Unity ─────────────────────────────────────────────────────────────

        private void OnEnable() => BuildCache();

        // ── 공개 API — 타일 ───────────────────────────────────────────────────

        /// <summary>스프라이트. 미설정이면 null.</summary>
        public Sprite GetSprite(TileType type)
            => TileEntry(type)?.sprite;

        /// <summary>폴백 색. 엔트리 없으면 마젠타 (Inspector 에서 눈에 띄도록).</summary>
        public Color GetColor(TileType type)
        {
            var v = TileEntry(type);
            return v != null ? v.fallbackColor : Color.magenta;
        }

        /// <summary>true면 MapLoader 가 Collider2D 를 부착합니다.</summary>
        public bool IsSolid(TileType type)
            => TileEntry(type)?.isSolid ?? false;

        /// <summary>
        /// 타일 게임오브젝트에 설정할 레이어 인덱스.
        /// <para>LayerMask 에서 최하위 비트를 추출합니다. 단일 레이어만 선택했을 때 정확합니다.</para>
        /// </summary>
        public int GetCollisionLayerIndex(TileType type)
        {
            var v = TileEntry(type);
            if (v == null) return 0;
            int mask = v.collisionLayer.value;
            if (mask == 0) return 0;
            return Mathf.RoundToInt(Mathf.Log(mask & -mask, 2));
        }

        /// <summary>Physics2D 쿼리용 LayerMask (비트마스크 그대로).</summary>
        public LayerMask GetCollisionLayerMask(TileType type)
            => TileEntry(type)?.collisionLayer ?? 0;

        /// <summary>프리팹 오버라이드. null이면 절차적 생성.</summary>
        public GameObject GetPrefabOverride(TileType type)
            => TileEntry(type)?.prefabOverride;

        // ── 공개 API — 슬롯 마커 ─────────────────────────────────────────────

        /// <summary>슬롯 방향 아이콘 스프라이트. 미설정이면 null.</summary>
        public Sprite GetSlotSprite(TrapAnchor anchor)
            => SlotEntry(anchor)?.markerSprite;

        /// <summary>슬롯 마커 색. 엔트리 없으면 기본 반투명 주황.</summary>
        public Color GetSlotTint(TrapAnchor anchor)
        {
            var v = SlotEntry(anchor);
            return v != null ? v.tint : DefaultSlotTint;
        }

        // ── 내부 — 캐시 ──────────────────────────────────────────────────────

        private void BuildCache()
        {
            _tileCache = new Dictionary<TileType, TileVisual>();
            if (tiles != null)
            {
                foreach (var v in tiles)
                {
                    if (!_tileCache.ContainsKey(v.type))
                        _tileCache[v.type] = v;
                }
            }

            _slotCache = new Dictionary<TrapAnchor, SlotVisual>();
            if (slotMarkers != null)
            {
                foreach (var s in slotMarkers)
                {
                    if (!_slotCache.ContainsKey(s.anchor))
                        _slotCache[s.anchor] = s;
                }
            }
        }

        private TileVisual TileEntry(TileType type)
        {
            if (_tileCache == null) BuildCache();
            return _tileCache.TryGetValue(type, out var v) ? v : null;
        }

        private SlotVisual SlotEntry(TrapAnchor anchor)
        {
            if (_slotCache == null) BuildCache();
            return _slotCache.TryGetValue(anchor, out var v) ? v : null;
        }

        // ── 검증 (에디터 저장·수정 시 자동 실행) ─────────────────────────────

#if UNITY_EDITOR
        private void OnValidate()
        {
            BuildCache();

            // TileType 엔트리 누락 검사
            var seenTypes = new HashSet<TileType>();
            foreach (TileType t in Enum.GetValues(typeof(TileType)))
            {
                if (!_tileCache.ContainsKey(t))
                    Debug.LogWarning($"[TilePaletteConfig] TileType.{t} 엔트리 없음 — " +
                                     $"Tiles 배열에 추가하세요: {name}", this);
            }

            // TileType 중복 검사
            if (tiles != null)
            {
                foreach (var v in tiles)
                {
                    if (!seenTypes.Add(v.type))
                        Debug.LogWarning($"[TilePaletteConfig] TileType.{v.type} 중복 엔트리: {name}", this);
                }
            }

            // TrapAnchor 엔트리 누락 검사
            var seenAnchors = new HashSet<TrapAnchor>();
            foreach (TrapAnchor a in Enum.GetValues(typeof(TrapAnchor)))
            {
                if (!_slotCache.ContainsKey(a))
                    Debug.LogWarning($"[TilePaletteConfig] TrapAnchor.{a} 엔트리 없음 — " +
                                     $"Slot Markers 배열에 추가하세요: {name}", this);
            }

            // TrapAnchor 중복 검사
            if (slotMarkers != null)
            {
                foreach (var s in slotMarkers)
                {
                    if (!seenAnchors.Add(s.anchor))
                        Debug.LogWarning($"[TilePaletteConfig] TrapAnchor.{s.anchor} 중복 엔트리: {name}", this);
                }
            }

            // 머티리얼 할당 검사
            if (sharedMaterial == null)
                Debug.LogWarning($"[TilePaletteConfig] Shared Material 미할당 — " +
                                 $"배칭이 보장되지 않습니다: {name}", this);

            // 솔리드 타일의 레이어 검사 (Default=1 이면 경고)
            if (tiles != null)
            {
                foreach (var v in tiles)
                {
                    if (v.isSolid && v.collisionLayer.value == 1)
                        Debug.LogWarning($"[TilePaletteConfig] {v.type} 은 Solid 지만 Default 레이어 — " +
                                         $"Ground 레이어 권장: {name}", this);

                    // 지형 타일인데 isSolid 꺼짐 → 플레이어가 뚫고 떨어짐
                    if (v.type != TileType.Empty && !v.isSolid)
                        Debug.LogWarning($"[TilePaletteConfig] {v.type} 의 Is Solid 가 꺼져 있음 — " +
                                         $"콜라이더가 생성되지 않아 플레이어가 통과합니다: {name}", this);

                    // 프리팹 오버라이드에 Collider2D 없음 → MapLoader 가 BoxCollider2D 를 자동 부착
                    if (v.isSolid && v.prefabOverride != null &&
                        v.prefabOverride.GetComponent<Collider2D>() == null)
                        Debug.LogWarning($"[TilePaletteConfig] {v.type} 프리팹 '{v.prefabOverride.name}' 에 " +
                                         $"Collider2D 없음 — 런타임에 1×1 BoxCollider2D 가 자동 부착됩니다. " +
                                         $"다른 모양이 필요하면 프리팹에 직접 추가하세요: {name}", this);
                }
            }
        }
#endif
    }
}
