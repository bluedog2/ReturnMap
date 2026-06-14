using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  MapLoader — JSON → 런타임 타일 GameObject 생성
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <c>StreamingAssets/Maps/{mapId}.json</c> 을 읽어 타일·슬롯 마커 오브젝트를
    /// 씬에 빌드합니다.
    ///
    /// <para><b>물리</b>: 솔리드 타일의 BoxCollider2D 는 맵 루트의
    /// <see cref="CompositeCollider2D"/> 로 병합됩니다 — 타일 이음새에
    /// 플레이어가 걸리는 버그 방지 + 브로드페이즈 비용 절감.</para>
    ///
    /// <para><b>렌더 배칭</b>: 같은 아틀라스 + 같은 머티리얼 + 같은 소팅 레이어 조건이
    /// 갖춰지면 SRP Batcher 가 자동으로 드로콜을 묶습니다. 별도 호출 불필요.
    /// (StaticBatchingUtility 는 MeshRenderer 전용이라 스프라이트에 효과 없음)</para>
    ///
    /// <para><b>씬 세팅</b></para>
    /// <list type="bullet">
    ///   <item>빈 GameObject 에 컴포넌트 부착 후 <see cref="_palette"/> 할당.</item>
    ///   <item><see cref="_mapRoot"/> 비우면 자동으로 자식 "MapTiles" 를 생성합니다.</item>
    /// </list>
    ///
    /// <para><b>사용법</b></para>
    /// <code>
    /// MapLoader.Instance.LoadMap("stage_01");
    /// // 또는 코루틴 직접:
    /// yield return MapLoader.Instance.LoadMapRoutine("stage_01");
    /// </code>
    /// </summary>
    public class MapLoader : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("필수 설정")]
        [Tooltip("타일 스프라이트·머티리얼·레이어 정보. 없으면 폴백 색상으로 렌더링.")]
        [SerializeField] private TilePaletteConfig _palette;

        [Tooltip("생성된 타일 오브젝트의 부모. 비워두면 'MapTiles' 자식 자동 생성.")]
        [SerializeField] private Transform _mapRoot;

        [Header("자동 로드")]
        [Tooltip("씬 시작 시 자동으로 로드할 맵 ID. 비워두면 수동 호출 대기.")]
        [SerializeField] private string _autoLoadMapId = "";

        // ── 런타임 상태 ───────────────────────────────────────────────────────

        /// <summary>현재 로드된 맵 데이터. 로드 전/언로드 후 null.</summary>
        public MapData  CurrentMap { get; private set; }
        public bool     IsLoaded   { get; private set; }

        /// <summary>맵의 월드 좌표 원점. <see cref="MapData.CellToWorld"/> 의 origin 인자로 사용.</summary>
        public Vector2  MapOrigin  => _mapRoot != null ? (Vector2)_mapRoot.position : Vector2.zero;

        /// <summary>타일 시각 에셋(스프라이트·프리팹)을 어드레서블에서 로드하는 전담 로더.</summary>
        private readonly AddressableLoader _assetLoader = new AddressableLoader();

        /// <summary>현재 <see cref="_assetLoader"/> 에 로드돼 있는 팔레트. 같은 팔레트 재로드 시 중복 로드 방지.</summary>
        private TilePaletteConfig _loadedPalette;

        // ── 이벤트 ────────────────────────────────────────────────────────────

        /// <summary>맵 빌드 완료 후 발행. MapData 를 인자로 전달합니다.</summary>
        public static event Action<MapData> OnMapLoaded;

        /// <summary>언로드(타일 제거) 완료 후 발행.</summary>
        public static event Action OnMapUnloaded;

        // ── 싱글턴 ────────────────────────────────────────────────────────────

        public static MapLoader Instance { get; private set; }

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }

            EnsureMapRoot();
        }

        private void Start()
        {
            if (!string.IsNullOrEmpty(_autoLoadMapId))
                LoadMap(_autoLoadMapId);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            // 어드레서블 핸들 해제 + 팔레트 캐시 비우기 (씬 종료 시)
            _assetLoader.ReleaseAll();
            if (_loadedPalette != null) _loadedPalette.ClearCache();
            _loadedPalette = null;
        }

        // ── 공개 API ──────────────────────────────────────────────────────────

        /// <summary>맵을 비동기 로드합니다 (코루틴 내부 시작).</summary>
        public void LoadMap(string mapId) => StartCoroutine(LoadMapRoutine(mapId));

        /// <summary>현재 맵을 언로드합니다.</summary>
        public void UnloadMap() => StartCoroutine(UnloadRoutine());

        /// <summary>
        /// 맵 로드 코루틴. <c>yield return</c> 으로 완료를 기다릴 수 있습니다.
        /// </summary>
        public IEnumerator LoadMapRoutine(string mapId)
        {
            // 기존 맵 제거
            if (IsLoaded)
                yield return StartCoroutine(UnloadRoutine());

            // ── JSON 읽기 ──────────────────────────────────────────────────────
            string path = Path.Combine(Application.streamingAssetsPath, "Maps", $"{mapId}.json");

            if (!File.Exists(path))
            {
                Debug.LogError($"[MapLoader] 파일 없음: {path}");
                yield break;
            }

            string  json = File.ReadAllText(path, System.Text.Encoding.UTF8);
            MapData map  = MapData.FromJson(json);

            if (map == null)
            {
                Debug.LogError($"[MapLoader] JSON 파싱 실패: {mapId}");
                yield break;
            }

            if (!map.Validate(out string err))
            {
                Debug.LogError($"[MapLoader] 맵 검증 실패 ({mapId}): {err}");
                yield break;
            }

            // ── 빌드 ──────────────────────────────────────────────────────────
            EnsureMapRoot();

            // 타일 시각 에셋(스프라이트·프리팹)을 전담 로더로 어드레서블에서 로드 → 캐시.
            // 팔레트가 바뀐 경우에만 재로드 — 같은 팔레트(맵 리트라이·재로드)는 캐시 재사용.
            if (_palette != null && _palette != _loadedPalette)
            {
                _assetLoader.ReleaseAll();                       // 이전 팔레트 핸들 해제
                if (_loadedPalette != null) _loadedPalette.ClearCache();

                _palette.BeginLoad(_assetLoader);
                yield return StartCoroutine(_assetLoader.WaitAll());
                _loadedPalette = _palette;
                Debug.Log($"[MapLoader] 팔레트 어드레서블 로드: {_palette.name} (핸들 {_assetLoader.Count}개)");
            }
            else if (_palette != null)
            {
                Debug.Log($"[MapLoader] 팔레트 캐시 재사용 — 어드레서블 재로드 생략 ({_palette.name})");
            }

            BuildMap(map);

            CurrentMap = map;
            IsLoaded   = true;

            OnMapLoaded?.Invoke(map);
            Debug.Log($"[MapLoader] 로드 완료: {mapId}  ({map.width}×{map.height})");
        }

        // ── 빌드 내부 ─────────────────────────────────────────────────────────

        private void BuildMap(MapData map)
        {
            Vector2 origin = (Vector2)_mapRoot.position;

            // ── 1. 지형 물리 준비 (Rigidbody2D + CompositeCollider2D) ────────
            SetupTerrainPhysics();

            // ── 2. 타일 생성 ─────────────────────────────────────────────────
            for (int y = 0; y < map.height; y++)
            {
                for (int x = 0; x < map.width; x++)
                {
                    TileType type = map.GetTile(x, y);
                    if (type == TileType.Empty) continue;
                    BuildTile(map, x, y, type, origin);
                }
            }

            // ── 3. 슬롯 마커 생성 ─────────────────────────────────────────────
            foreach (var slot in map.trapSlots)
            {
                if (map.InBounds(slot.x, slot.y))
                    BuildSlotMarker(map, slot, origin);
            }

            // (렌더 배칭은 공유 머티리얼+아틀라스 조건으로 SRP Batcher 가 자동 처리)

            // ── 4. 카메라 경계 동기화 ─────────────────────────────────────────
            CameraConfinerSync.Instance?.SetFromMap(map, origin);
        }

        /// <summary>
        /// 맵 루트에 Static Rigidbody2D + CompositeCollider2D 를 1회 부착합니다.
        /// 자식 타일의 BoxCollider2D(Merge) 가 모두 하나의 콜라이더로 병합되어
        /// 평평한 바닥에서 이음새 걸림이 사라집니다.
        /// </summary>
        private void SetupTerrainPhysics()
        {
            var rootGo = _mapRoot.gameObject;

            var rb = rootGo.GetComponent<Rigidbody2D>();
            if (rb == null) rb = rootGo.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;

            if (rootGo.GetComponent<CompositeCollider2D>() == null)
                rootGo.AddComponent<CompositeCollider2D>();

            // 병합 후 물리 레이어는 컴포지트(루트) 기준 — 솔리드 타일의 레이어를 따름
            if (_palette != null)
            {
                foreach (TileType t in new[] { TileType.Floor, TileType.Wall, TileType.Ceiling })
                {
                    if (_palette.IsSolid(t))
                    {
                        rootGo.layer = _palette.GetCollisionLayerIndex(t);
                        break;
                    }
                }
            }
        }

        private void BuildTile(MapData map, int x, int y, TileType type, Vector2 origin)
        {
            Vector2 worldPos = map.CellToWorld(x, y, origin);

            // 프리팹 오버라이드 우선 — 특수 타일용 (애니메이션·파티클·스크립트 등)
            GameObject prefab = _palette != null ? _palette.GetPrefabOverride(type) : null;
            if (prefab != null)
            {
                var inst = Instantiate(prefab, worldPos, Quaternion.identity, _mapRoot);
                inst.name = $"Tile_{type}_{x}_{y}";

                // 물리는 프리팹 구성과 무관하게 팔레트 isSolid 가 보장
                if (_palette.IsSolid(type))
                {
                    var col = inst.GetComponent<Collider2D>();
                    if (col == null)
                    {
                        var box  = inst.AddComponent<BoxCollider2D>();
                        box.size = Vector2.one * map.tileUnit;
                        col      = box;
                    }

                    // Box/Polygon 만 컴포지트 병합 가능 — 그 외는 단독 콜라이더로 동작
                    if (col is BoxCollider2D || col is PolygonCollider2D)
                        col.compositeOperation = Collider2D.CompositeOperation.Merge;

                    inst.layer = _palette.GetCollisionLayerIndex(type);
                }
                return;
            }

            // ── 절차적 생성 ────────────────────────────────────────────────────
            var go = new GameObject($"Tile_{type}_{x}_{y}");
            go.transform.SetParent(_mapRoot, false);
            go.transform.position = worldPos;

            // SpriteRenderer
            var sr     = go.AddComponent<SpriteRenderer>();
            Sprite sp  = _palette != null ? _palette.GetSprite(type) : null;
            sr.sprite  = sp;
            sr.color   = sp != null ? Color.white
                        : (_palette != null ? _palette.GetColor(type)
                                            : TilePaletteConfig.FallbackColor(type));

            if (_palette != null)
            {
                if (!string.IsNullOrEmpty(_palette.TileSortingLayer))
                    sr.sortingLayerName = _palette.TileSortingLayer;
                sr.sortingOrder = _palette.TileSortingOrder;
                if (_palette.SharedMaterial != null)
                    sr.sharedMaterial = _palette.SharedMaterial;
            }

            // 스프라이트 없으면 1×1 흰 스프라이트 폴백 (색상 블록 표시용)
            if (sp == null)
                sr.sprite = GetFallbackSprite();

            // 솔리드 타일 → BoxCollider2D (루트의 CompositeCollider2D 로 병합)
            if (_palette != null && _palette.IsSolid(type))
            {
                var col  = go.AddComponent<BoxCollider2D>();
                col.size = Vector2.one * map.tileUnit;
                col.compositeOperation = Collider2D.CompositeOperation.Merge;
                go.layer = _palette.GetCollisionLayerIndex(type);
            }
        }

        private void BuildSlotMarker(MapData map, TrapSlotData slot, Vector2 origin)
        {
            Vector2 worldPos = map.CellToWorld(slot.x, slot.y, origin);

            var go = new GameObject($"Slot_{slot.anchor}_{slot.x}_{slot.y}");
            go.transform.SetParent(_mapRoot, false);
            go.transform.position = worldPos;

            var sr = go.AddComponent<SpriteRenderer>();
            if (_palette != null)
            {
                sr.sprite = _palette.GetSlotSprite(slot.Anchor);
                sr.color  = _palette.GetSlotTint(slot.Anchor);
                if (!string.IsNullOrEmpty(_palette.TileSortingLayer))
                    sr.sortingLayerName = _palette.TileSortingLayer;
                sr.sortingOrder = _palette.SlotSortingOrder;
                if (_palette.SharedMaterial != null)
                    sr.sharedMaterial = _palette.SharedMaterial;
            }
            else
            {
                sr.color = TilePaletteConfig.DefaultSlotTint;
            }

            // 마커 스프라이트 미설정이어도 설치 가능 위치가 항상 보이도록 색상 블록 폴백
            if (sr.sprite == null)
                sr.sprite = GetFallbackSprite();

            // TrapSlotMarker 컴포넌트 — 5단계 런타임 레지스트리용
            var marker = go.AddComponent<TrapSlotMarker>();
            marker.Init(slot.x, slot.y, slot.Anchor);

            // Play 페이즈에서 빈 슬롯을 막을 봉인 타일 구성
            // (anchor 방향에 어울리는 타일 외형 — 부착면과 같은 재질로 보이게)
            TileType sealType = slot.Anchor switch
            {
                TrapAnchor.Ceiling   => TileType.Ceiling,
                TrapAnchor.LeftWall  => TileType.Wall,
                TrapAnchor.RightWall => TileType.Wall,
                _                    => TileType.Floor,
            };

            // 외형 우선순위: 팔레트 봉인 전용 스프라이트 → anchor 타일 스프라이트 → 색상 블록
            Sprite sealSprite;
            Color  sealColor;
            if (_palette != null && _palette.SealSprite != null)
            {
                sealSprite = _palette.SealSprite;
                sealColor  = _palette.SealTint;
            }
            else if (_palette != null && _palette.GetSprite(sealType) != null)
            {
                sealSprite = _palette.GetSprite(sealType);
                sealColor  = _palette.SealTint;
            }
            else
            {
                sealSprite = GetFallbackSprite();
                sealColor  = _palette != null ? _palette.GetColor(sealType)
                                              : TilePaletteConfig.FallbackColor(sealType);
            }

            marker.CreateSeal(
                sealSprite, sealColor,
                _palette != null ? _palette.SharedMaterial : null,
                _palette != null ? _palette.TileSortingLayer : null,
                _palette != null ? _palette.TileSortingOrder : 0,
                _palette != null ? _palette.GetCollisionLayerIndex(sealType) : 0,
                map.tileUnit);
        }

        // ── 언로드 ────────────────────────────────────────────────────────────

        private IEnumerator UnloadRoutine()
        {
            for (int i = _mapRoot.childCount - 1; i >= 0; i--)
                Destroy(_mapRoot.GetChild(i).gameObject);

            CurrentMap = null;
            IsLoaded   = false;

            OnMapUnloaded?.Invoke();
            yield return null; // Destroy 1프레임 처리
        }

        // ── 유틸 ─────────────────────────────────────────────────────────────

        private void EnsureMapRoot()
        {
            if (_mapRoot != null) return;

            var child = new GameObject("MapTiles");
            child.transform.SetParent(transform, false);
            _mapRoot = child.transform;
        }

        // 스프라이트 없을 때 색상 블록 표시용 1×1 흰 스프라이트 캐시
        private static Sprite _fallbackSprite;
        private static Sprite GetFallbackSprite()
        {
            if (_fallbackSprite != null) return _fallbackSprite;

            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            _fallbackSprite = Sprite.Create(
                tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            return _fallbackSprite;
        }
    }
}
