using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

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

        [Tooltip("에디터 미리보기 + 런타임 SpriteRenderer 에 사용할 스프라이트 (Addressable). " +
                 "런타임은 MapLoader 가 Preload 후 캐시에서 조회, 에디터는 editorAsset 동기 접근.")]
        public AssetReferenceSprite spriteRef;

        [Tooltip("스프라이트가 비어있을 때 에디터 셀에 채울 색입니다.")]
        public Color      fallbackColor = Color.gray;

        [Tooltip("true면 MapLoader 가 Collider2D 를 부착해 물리 충돌을 처리합니다.")]
        public bool       isSolid;

        [Tooltip("솔리드 타일을 올릴 물리 레이어. 단일 레이어 하나만 선택하세요.\n" +
                 "PlayerController.groundLayer / DropHammer.groundLayer 와 일치시켜야 합니다.")]
        public LayerMask  collisionLayer;

        [Tooltip("절차적 생성 대신 이 프리팹을 Instantiate 합니다 (Addressable). " +
                 "비워두면 SpriteRenderer + Collider2D 절차 생성 (배칭 보장).")]
        public AssetReferenceGameObject prefabRef;
    }

    /// <summary>함정 슬롯 오버레이의 시각 설정.</summary>
    [Serializable]
    public class SlotVisual
    {
        [Tooltip("어떤 TrapAnchor 와 매핑되는 엔트리인지 명시합니다.")]
        public TrapAnchor anchor;

        [Tooltip("슬롯 방향을 나타내는 아이콘 스프라이트 (Addressable). " +
                 "(예: Floor=위쪽 화살, Ceiling=아래쪽 화살)")]
        public AssetReferenceSprite markerRef;

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

        [Header("배경 타일 (충돌 없는 장식 — 인덱스 기반, 종류 무제한)")]
        [Tooltip("배경 레이어에 칠할 스프라이트 목록 (Addressable). " +
                 "맵 데이터의 background 인덱스 = 이 목록 인덱스+1 (0=없음). 순서를 바꾸면 기존 맵이 어긋나니 끝에 추가만 권장.")]
        [SerializeField] private List<AssetReferenceSprite> backgroundTiles = new List<AssetReferenceSprite>();

        [Header("슬롯 봉인 타일 (Play 중 빈 슬롯을 막는 타일)")]
        [Tooltip("봉인 타일 전용 스프라이트 (Addressable). 비워두면 anchor 에 맞는 일반 타일 스프라이트를 사용합니다.")]
        [SerializeField] private AssetReferenceSprite sealRef;

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

        // ── 엔트리 조회 캐시 (Dictionary는 직렬화 불가 → OnEnable 에서 구축) ───

        private Dictionary<TileType,   TileVisual> _tileCache;
        private Dictionary<TrapAnchor, SlotVisual> _slotCache;

        // ── 어드레서블 런타임 캐시 (PreloadRoutine 이 채움) ──────────────────

        private Dictionary<TileType,   Sprite>     _loadedSprites;
        private Dictionary<TileType,   GameObject> _loadedPrefabs;
        private Dictionary<TrapAnchor, Sprite>     _loadedSlotSprites;
        private Dictionary<int,        Sprite>     _loadedBackgrounds;
        private Sprite                             _loadedSeal;
        private bool _preloaded;

        // ── 공개 프로퍼티 — 렌더 설정 ────────────────────────────────────────

        public Material SharedMaterial   => sharedMaterial;
        public string   TileSortingLayer => tileSortingLayer;
        public int      TileSortingOrder => tileSortingOrder;
        public int      SlotSortingOrder => slotSortingOrder;

        /// <summary>봉인 타일 전용 스프라이트. null 이면 일반 타일 스프라이트 폴백.</summary>
        public Sprite SealSprite => ResolveSprite(SealRef, _loadedSeal);

        /// <summary>봉인 타일 틴트 색.</summary>
        public Color    SealTint         => sealTint;

        private AssetReferenceSprite SealRef => sealRef;

        // ── Unity ─────────────────────────────────────────────────────────────

        private void OnEnable() => BuildCache();

        // ── 어드레서블 로드 위임 ─────────────────────────────────────────────

        /// <summary>
        /// 팔레트가 참조하는 모든 어드레서블 스프라이트·프리팹을 <paramref name="loader"/> 에
        /// 로드 요청으로 등록합니다 (논블로킹). 완료 시 결과가 런타임 캐시에 채워집니다.
        /// 실제 대기는 호출 측에서 <see cref="AddressableLoader.WaitAll"/> 로 수행합니다.
        /// <para>런타임 Getter(<see cref="GetSprite"/> 등)는 이 캐시를 조회합니다.</para>
        /// </summary>
        public void BeginLoad(AddressableLoader loader)
        {
            if (loader == null) return;
            if (_preloaded) return;
            BuildCache();

            _loadedSprites     = new Dictionary<TileType, Sprite>();
            _loadedPrefabs     = new Dictionary<TileType, GameObject>();
            _loadedSlotSprites = new Dictionary<TrapAnchor, Sprite>();
            _loadedBackgrounds = new Dictionary<int, Sprite>();
            _loadedSeal        = null;

            if (tiles != null)
                foreach (var v in tiles)
                {
                    TileType t = v.type;
                    if (RefValid(v.spriteRef)) loader.Load(v.spriteRef, s => _loadedSprites[t] = s);
                    if (RefValid(v.prefabRef)) loader.Load(v.prefabRef, p => _loadedPrefabs[t] = p);
                }

            if (slotMarkers != null)
                foreach (var s in slotMarkers)
                {
                    TrapAnchor a = s.anchor;
                    if (RefValid(s.markerRef)) loader.Load(s.markerRef, sp => _loadedSlotSprites[a] = sp);
                }

            // 배경 타일은 종류가 많을 수 있어 전부 로드하지 않는다.
            // 맵이 실제 사용하는 인덱스만 LoadBackgrounds 로 따로 로드한다.

            if (RefValid(sealRef)) loader.Load(sealRef, sp => _loadedSeal = sp);

            _preloaded = true;
        }

        /// <summary>
        /// 맵이 실제 사용하는 배경 타일 인덱스만 어드레서블 로드합니다(누적 — 이미 로드된 건 건너뜀).
        /// MapLoader 가 맵 빌드 직전 호출하며, 종류가 수백 개여도 화면에 쓰는 것만 메모리에 올립니다.
        /// </summary>
        public void LoadBackgrounds(AddressableLoader loader, IEnumerable<int> usedIndices)
        {
            if (loader == null || usedIndices == null || backgroundTiles == null) return;
            _loadedBackgrounds ??= new Dictionary<int, Sprite>();

            foreach (int idx in usedIndices)
            {
                if (idx < 1 || idx > backgroundTiles.Count) continue;
                if (_loadedBackgrounds.ContainsKey(idx)) continue; // 이미 로드됨
                if (RefValid(backgroundTiles[idx - 1]))
                    loader.Load(backgroundTiles[idx - 1], sp => _loadedBackgrounds[idx] = sp);
            }
        }

        /// <summary>런타임 캐시를 비웁니다 (핸들 해제는 AddressableLoader.ReleaseAll 이 담당).</summary>
        public void ClearCache()
        {
            _loadedSprites?.Clear();
            _loadedPrefabs?.Clear();
            _loadedSlotSprites?.Clear();
            _loadedBackgrounds?.Clear();
            _loadedSeal = null;
            _preloaded  = false;
        }

        private static bool RefValid(AssetReference r) => r != null && r.RuntimeKeyIsValid();

        /// <summary>
        /// 에디터(비플레이)는 editorAsset 으로 동기 해석, 런타임은 Preload 캐시 값을 사용.
        /// </summary>
        private static Sprite ResolveSprite(AssetReferenceSprite r, Sprite cached)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                return EditorResolveSprite(r);
#endif
            return cached;
        }

#if UNITY_EDITOR
        // OnGUI 가 매 프레임 호출하므로 해석 결과(특히 SpriteAtlas.GetSprite 클론)를 캐시한다.
        private static readonly Dictionary<string, Sprite> _editorSpriteCache
            = new Dictionary<string, Sprite>();

        /// <summary>
        /// 에디터 미리보기용 스프라이트 해석 (캐시).
        /// <para>① editorAsset 이 Sprite → 그대로. ② editorAsset 이 <b>SpriteAtlas</b> →
        /// SubObjectName 으로 아틀라스에서 추출. ③ 그 외 GUID 경로의 Sprite 서브에셋 폴백.</para>
        /// </summary>
        private static Sprite EditorResolveSprite(AssetReferenceSprite r)
        {
            if (r == null || !r.RuntimeKeyIsValid()) return null;

            string key = r.AssetGUID + "|" + r.SubObjectName;
            if (_editorSpriteCache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            Sprite resolved = ResolveEditorSpriteUncached(r);
            if (resolved != null) _editorSpriteCache[key] = resolved;
            return resolved;
        }

        private static Sprite ResolveEditorSpriteUncached(AssetReferenceSprite r)
        {
            var ea = r.editorAsset;

            // ① 곧장 스프라이트
            if (ea is Sprite s) return s;

            // ② SpriteAtlas + SubObjectName → 아틀라스에서 스프라이트 추출
            if (ea is UnityEngine.U2D.SpriteAtlas atlas && !string.IsNullOrEmpty(r.SubObjectName))
            {
                var sp = atlas.GetSprite(r.SubObjectName); // 패킹된 미리보기 스프라이트
                if (sp != null) return sp;
            }

            // ③ GUID 경로의 Sprite 서브에셋 폴백 (단일/Multiple 텍스처)
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(r.AssetGUID);
            if (string.IsNullOrEmpty(path)) return null;

            string sub = r.SubObjectName;
            Sprite first = null;
            foreach (var rep in UnityEditor.AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
            {
                if (rep is not Sprite sp2) continue;
                if (!string.IsNullOrEmpty(sub) && sp2.name == sub) return sp2;
                first ??= sp2;
            }
            return first != null ? first : UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>에디터 스프라이트 캐시를 비웁니다 (팔레트 수정·아틀라스 재패킹 시).</summary>
        private static void ClearEditorSpriteCache() => _editorSpriteCache.Clear();
#endif

        // ── 공개 API — 타일 ───────────────────────────────────────────────────

        /// <summary>스프라이트. 런타임은 Preload 캐시, 에디터는 editorAsset. 미설정이면 null.</summary>
        public Sprite GetSprite(TileType type)
        {
            var v = TileEntry(type);
            if (v == null) return null;
            return ResolveSprite(v.spriteRef,
                _loadedSprites != null && _loadedSprites.TryGetValue(type, out var s) ? s : null);
        }

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

        /// <summary>프리팹 오버라이드. null이면 절차적 생성. 런타임은 Preload 캐시, 에디터는 editorAsset.</summary>
        public GameObject GetPrefabOverride(TileType type)
        {
            var v = TileEntry(type);
            if (v == null || !RefValid(v.prefabRef)) return null;
#if UNITY_EDITOR
            if (!Application.isPlaying) return v.prefabRef.editorAsset as GameObject;
#endif
            return _loadedPrefabs != null && _loadedPrefabs.TryGetValue(type, out var p) ? p : null;
        }

        // ── 공개 API — 슬롯 마커 ─────────────────────────────────────────────

        /// <summary>슬롯 방향 아이콘 스프라이트. 런타임은 Preload 캐시, 에디터는 editorAsset.</summary>
        public Sprite GetSlotSprite(TrapAnchor anchor)
        {
            var v = SlotEntry(anchor);
            if (v == null) return null;
            return ResolveSprite(v.markerRef,
                _loadedSlotSprites != null && _loadedSlotSprites.TryGetValue(anchor, out var s) ? s : null);
        }

        /// <summary>슬롯 마커 색. 엔트리 없으면 기본 반투명 주황.</summary>
        public Color GetSlotTint(TrapAnchor anchor)
        {
            var v = SlotEntry(anchor);
            return v != null ? v.tint : DefaultSlotTint;
        }

        // ── 공개 API — 배경 타일 ─────────────────────────────────────────────

        /// <summary>배경 타일 종류 수 (에디터 팔레트 UI 용).</summary>
        public int BackgroundTileCount => backgroundTiles != null ? backgroundTiles.Count : 0;

        /// <summary>
        /// 배경 타일 스프라이트. <paramref name="index"/> 는 맵 background 값(1-base, 0=없음).
        /// 런타임은 Preload 캐시, 에디터는 editorAsset/아틀라스 해석.
        /// </summary>
        public Sprite GetBackgroundSprite(int index)
        {
            if (backgroundTiles == null || index <= 0 || index > backgroundTiles.Count)
                return null;
            return ResolveSprite(backgroundTiles[index - 1],
                _loadedBackgrounds != null && _loadedBackgrounds.TryGetValue(index, out var s) ? s : null);
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
            ClearEditorSpriteCache(); // 스프라이트 참조 변경 시 미리보기 캐시 무효화

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
                    var prefabAsset = (v.prefabRef != null && v.prefabRef.editorAsset != null)
                                      ? v.prefabRef.editorAsset as GameObject : null;
                    if (v.isSolid && prefabAsset != null &&
                        prefabAsset.GetComponent<Collider2D>() == null)
                        Debug.LogWarning($"[TilePaletteConfig] {v.type} 프리팹 '{prefabAsset.name}' 에 " +
                                         $"Collider2D 없음 — 런타임에 1×1 BoxCollider2D 가 자동 부착됩니다. " +
                                         $"다른 모양이 필요하면 프리팹에 직접 추가하세요: {name}", this);
                }
            }
        }
#endif
    }
}
