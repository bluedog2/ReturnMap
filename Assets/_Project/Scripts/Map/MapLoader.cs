using System;
using System.Collections;
using System.Collections.Generic;
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

        [Tooltip("스테이지 순서의 단일 소스. 자동 로드 mapId가 비어 있으면 카탈로그 첫 스테이지를 로드")]
        [SerializeField] private StageCatalog _stageCatalog;

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

        /// <summary>
        /// 맵 로드 실패 시 발행: (mapId, 실패 사유).
        /// 파일 없음 / JSON 파싱 실패 / 검증 실패 경로 전부에서 호출됩니다.
        /// 발행 후 <see cref="IsLoaded"/> 는 항상 재시도 가능한 상태로 복귀합니다.
        /// </summary>
        public static event Action<string, string> OnMapLoadFailed;

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
            string mapId = _autoLoadMapId;

            // _autoLoadMapId 가 비어 있으면 카탈로그의 첫 스테이지로 폴백 (하위 호환 유지)
            if (string.IsNullOrEmpty(mapId) && _stageCatalog != null && _stageCatalog.Count > 0)
                mapId = _stageCatalog.GetByIndex(0)?.mapId;

            if (!string.IsNullOrEmpty(mapId))
                LoadMap(mapId);
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
                string reason = $"파일 없음: {path}";
                Debug.LogError($"[MapLoader] {reason}");
                FailLoad(mapId, reason);
                yield break;
            }

            MapData map = null;
            string  parseError = null;
            try
            {
                string json = File.ReadAllText(path, System.Text.Encoding.UTF8);
                map = MapData.FromJson(json);
                if (map == null)
                    parseError = "JsonUtility.FromJson 결과 null";
            }
            catch (Exception e)
            {
                parseError = e.Message;
            }

            if (parseError != null)
            {
                Debug.LogError($"[MapLoader] JSON 파싱 실패: {mapId} — {parseError}");
                FailLoad(mapId, $"JSON 파싱 실패: {parseError}");
                yield break;
            }

            if (!map.Validate(out string err))
            {
                Debug.LogError($"[MapLoader] 맵 검증 실패 ({mapId}): {err}");
                FailLoad(mapId, $"검증 실패: {err}");
                yield break;
            }

            // ── 빌드 ──────────────────────────────────────────────────────────
            EnsureMapRoot();

            // 타일 시각 에셋을 전담 로더로 어드레서블에서 로드 → 캐시.
            if (_palette != null)
            {
                // 타일·슬롯·봉인: 팔레트가 바뀐 경우에만 재로드 (같은 팔레트는 캐시 재사용)
                if (_palette != _loadedPalette)
                {
                    _assetLoader.ReleaseAll();                       // 이전 팔레트 핸들 해제
                    if (_loadedPalette != null) _loadedPalette.ClearCache();
                    _palette.BeginLoad(_assetLoader);
                    _loadedPalette = _palette;
                }

                // 배경: 이 맵이 실제 사용하는 인덱스만 로드 (종류가 수백 개여도 화면에 쓰는 것만)
                _palette.LoadBackgrounds(_assetLoader, CollectUsedBackgrounds(map));

                yield return StartCoroutine(_assetLoader.WaitAll());
            }

            BuildMap(map);

            CurrentMap = map;
            IsLoaded   = true;

            OnMapLoaded?.Invoke(map);
            Debug.Log($"[MapLoader] 로드 완료: {mapId}  ({map.width}×{map.height})");
        }

        /// <summary>
        /// 로드 실패 공통 처리. 상태를 재시도 가능하게 정리한 뒤 실패 이벤트를 발행합니다.
        /// (실패 시점에는 이미 <see cref="UnloadRoutine"/> 을 거쳐 CurrentMap/IsLoaded 가
        /// 정리된 상태이지만, 방어적으로 한 번 더 보장합니다.)
        /// </summary>
        private void FailLoad(string mapId, string reason)
        {
            CurrentMap = null;
            IsLoaded   = false;
            OnMapLoadFailed?.Invoke(mapId, reason);
        }

        // ── 빌드 내부 ─────────────────────────────────────────────────────────

        private void BuildMap(MapData map)
        {
            Vector2 origin = (Vector2)_mapRoot.position;

            // ── 1. 지형 물리 준비 (Rigidbody2D + CompositeCollider2D) ────────
            SetupTerrainPhysics();

            // ── 1.5. 배경 레이어 (충돌 없는 장식, 소팅 뒤) ───────────────────
            BuildBackground(map, origin);

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

            // 스프라이트는 자식 "Visual" 에 분리한다. 셀 크기에 맞춰 스케일해도
            // 부모(콜라이더)는 스케일 1 을 유지해 물리 크기가 틀어지지 않는다.
            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);

            var sr     = visual.AddComponent<SpriteRenderer>();
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

            // PPU·해상도가 제각각인 스프라이트도 정확히 1셀(tileUnit)을 채우도록 스케일 보정
            Vector2 spriteWorld = sr.sprite.bounds.size;
            if (spriteWorld.x > 0.0001f && spriteWorld.y > 0.0001f)
                visual.transform.localScale = new Vector3(
                    map.tileUnit / spriteWorld.x, map.tileUnit / spriteWorld.y, 1f);

            // 솔리드 타일 → BoxCollider2D (부모 go, 스케일 1 → 루트 CompositeCollider2D 로 병합)
            if (_palette != null && _palette.IsSolid(type))
            {
                var col  = go.AddComponent<BoxCollider2D>();
                col.size = Vector2.one * map.tileUnit;
                col.compositeOperation = Collider2D.CompositeOperation.Merge;
                go.layer = _palette.GetCollisionLayerIndex(type);
            }
        }

        /// <summary>맵의 배경 그리드에 실제 등장하는 타일 인덱스 집합 (0=없음 제외).</summary>
        private static HashSet<int> CollectUsedBackgrounds(MapData map)
        {
            var set = new HashSet<int>();
            if (map.background != null)
                foreach (int b in map.background)
                    if (b > 0) set.Add(b);
            return set;
        }

        /// <summary>
        /// 배경 레이어를 빌드합니다. 충돌 없는 SpriteRenderer 를 타일보다 뒤 소팅 순서로 배치.
        /// </summary>
        private void BuildBackground(MapData map, Vector2 origin)
        {
            if (_palette == null) return;
            map.EnsureBackground();

            for (int y = 0; y < map.height; y++)
            {
                for (int x = 0; x < map.width; x++)
                {
                    int bg = map.GetBackground(x, y);
                    if (bg <= 0) continue;

                    Sprite sp = _palette.GetBackgroundSprite(bg);
                    if (sp == null) continue;

                    var go = new GameObject($"Bg_{bg}_{x}_{y}");
                    go.transform.SetParent(_mapRoot, false);
                    go.transform.position = map.CellToWorld(x, y, origin);

                    var sr    = go.AddComponent<SpriteRenderer>();
                    sr.sprite = sp;
                    if (!string.IsNullOrEmpty(_palette.TileSortingLayer))
                        sr.sortingLayerName = _palette.TileSortingLayer;
                    sr.sortingOrder = _palette.TileSortingOrder - 10; // 지형 타일보다 뒤
                    if (_palette.SharedMaterial != null)
                        sr.sharedMaterial = _palette.SharedMaterial;

                    // 1셀(tileUnit)에 맞춰 스케일 보정 (PPU·해상도 무관)
                    Vector2 b = sp.bounds.size;
                    if (b.x > 0.0001f && b.y > 0.0001f)
                        go.transform.localScale = new Vector3(map.tileUnit / b.x, map.tileUnit / b.y, 1f);
                }
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
