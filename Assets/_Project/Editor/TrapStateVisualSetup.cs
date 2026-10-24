using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace ReTrap.EditorTools
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  TrapStateVisualSetup — 함정 상태별 파츠 스프라이트 + 플레이어 이펙트 배선
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 메뉴 <b>ReTrap → Setup → 함정 상태 비주얼 연결</b> (배치모드:
    /// <c>-executeMethod ReTrap.EditorTools.TrapStateVisualSetup.Run</c>).
    /// <list type="number">
    ///   <item>chain·platform 스프라이트를 알파 바운딩박스로 크롭(Multiple + 단일 rect)</item>
    ///   <item>SpikeTrap / ArrowShooter / Arrow / DropHammer 프리팹에 파츠 자식·TrapStateVisuals·참조 배선</item>
    ///   <item>SampleScene 의 Player 에 PlayerEffects 부착 + 프레임 배열 배선</item>
    /// </list>
    /// <b>멱등성</b>: 자식은 이름으로 찾아 재사용하고 바인딩은 매번 다시 쓰므로 여러 번 실행해도 안전합니다.
    /// 함정 부분만 필요하면 <see cref="RunTraps"/> (TrapPrefabBuilder 재생성 시 호출).
    /// </summary>
    public static class TrapStateVisualSetup
    {
        private const string PartsDir   = "Assets/_Project/ResourcceEX/Sprites/Traps/Parts";
        private const string EffectsDir = "Assets/_Project/ResourcceEX/Sprites/Effects";
        private const string PrefabDir  = "Assets/_Project/ResourcceEX/Prefabs/Traps";
        private const string ScenePath  = "Assets/_Project/ResourcceEX/Scenes/SampleScene.unity";

        private static readonly string[] States = { "Normal", "Dud", "Critical", "Beneficial" };

        /// <summary>바인딩 한 줄 정의 (렌더러 + 상태별 스프라이트 4종 + transient 여부).</summary>
        private struct BindingSpec
        {
            public SpriteRenderer target;
            public Sprite[]       sprites; // Normal, Dud, Critical, Beneficial
            public bool           transient;
        }

        // ── 진입점 ────────────────────────────────────────────────────────────

        [MenuItem("ReTrap/Setup/함정 상태 비주얼 연결", false, 4)]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[TrapStateVisualSetup] 플레이 모드에서는 실행할 수 없습니다.");
                return;
            }

            RunTraps();
            RunPlayerEffects();
            AssetDatabase.SaveAssets();
        }

        /// <summary>함정 부분만 (스프라이트 크롭 + 프리팹 4종 배선). 씬은 건드리지 않는다.</summary>
        public static void RunTraps()
        {
            int cropped = CropSprites();

            ConfigureSpikeTrap();
            ConfigureArrowShooter();
            ConfigureArrow();
            ConfigureDropHammer();

            AssetDatabase.SaveAssets();
            Debug.Log($"[TrapStateVisualSetup] 함정 상태 비주얼 연결 완료 — 스프라이트 크롭 {cropped}개, " +
                      "프리팹 4종(SpikeTrap/ArrowShooter/Arrow/DropHammer) 배선");
        }

        // ── 1. 스프라이트 크롭 (알파 바운딩박스) ─────────────────────────────

        private static int CropSprites()
        {
            int count = 0;
            foreach (string state in States)
            {
                if (CropToAlphaBounds(PartPath("drop_hammer", state, "chain"))) count++;
                if (CropToAlphaBounds(PartPath("spike",       state, "platform"))) count++;
            }
            return count;
        }

        /// <summary>
        /// PNG 의 알파 바운딩박스 영역만 단일 스프라이트(Multiple 모드, 피벗 중앙)로 만든다.
        /// 이미 같은 설정이면 건너뛴다. 새로 적용했으면 true.
        /// </summary>
        private static bool CropToAlphaBounds(string path)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null)
            {
                Debug.LogWarning($"[TrapStateVisualSetup] 텍스처 임포터를 찾지 못했습니다: {path}");
                return false;
            }
            if (!TryGetAlphaBounds(path, out RectInt bb, out _))
            {
                Debug.LogWarning($"[TrapStateVisualSetup] 알파 영역을 찾지 못했습니다: {path}");
                return false;
            }

            string spriteName = Path.GetFileNameWithoutExtension(path);
            var    rect       = new Rect(bb.x, bb.y, bb.width, bb.height);

            var factory = new SpriteDataProviderFactories();
            factory.Init();

            // 이미 완료됐는지 확인
            if (ti.spriteImportMode == SpriteImportMode.Multiple)
            {
                var check = factory.GetSpriteEditorDataProviderFromObject(ti);
                check.InitSpriteEditorDataProvider();
                var rects = check.GetSpriteRects();
                if (rects.Length == 1 && rects[0].name == spriteName && rects[0].rect == rect)
                    return false;
            }

            // FullRect 메시 (Tiled 드로우 모드·크롭 스프라이트 안정성)
            var settings = new TextureImporterSettings();
            ti.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            ti.SetTextureSettings(settings);
            ti.spriteImportMode = SpriteImportMode.Multiple;

            var dp = factory.GetSpriteEditorDataProviderFromObject(ti);
            dp.InitSpriteEditorDataProvider();

            var sr = new SpriteRect
            {
                name      = spriteName,
                rect      = rect,
                alignment = SpriteAlignment.Center,
                pivot     = new Vector2(0.5f, 0.5f),
                spriteID  = GUID.Generate(),
            };
            dp.SetSpriteRects(new[] { sr });

            var nameProvider = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (nameProvider != null)
                nameProvider.SetNameFileIdPairs(new[] { new SpriteNameFileIdPair(spriteName, sr.spriteID) });

            dp.Apply();
            ti.SaveAndReimport();
            return true;
        }

        /// <summary>PNG 바이트를 직접 디코드해 알파&gt;임계값 픽셀의 바운딩박스(텍스처 좌표, 하단 원점)를 구한다.</summary>
        private static bool TryGetAlphaBounds(string assetPath, out RectInt bounds, out Vector2Int texSize)
        {
            bounds  = default;
            texSize = default;

            string full = Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);
            if (!File.Exists(full)) return false;

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!tex.LoadImage(File.ReadAllBytes(full))) return false;

                int w = tex.width, h = tex.height;
                var px = tex.GetPixels32();
                int minX = w, minY = h, maxX = -1, maxY = -1;
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        if (px[y * w + x].a <= 8) continue;
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
                if (maxX < 0) return false;

                bounds  = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
                texSize = new Vector2Int(w, h);
                return true;
            }
            finally
            {
                Object.DestroyImmediate(tex);
            }
        }

        // ── 2. SpikeTrap ──────────────────────────────────────────────────────

        private static void ConfigureSpikeTrap()
        {
            string path = $"{PrefabDir}/SpikeTrap.prefab";
            var root = LoadPrefab(path);
            if (root == null) return;

            try
            {
                var visual = root.transform.Find("Visual");
                if (visual == null)
                {
                    Debug.LogWarning("[TrapStateVisualSetup] SpikeTrap: Visual 자식이 없습니다.");
                    return;
                }
                var visualSr = visual.GetComponent<SpriteRenderer>();
                PrepareRenderer(visualSr, 10);
                visual.localScale = Vector3.one;

                // 하우징(정적, 가시 Visual 의 형제) — 캔버스 하단 = 셀 하단(-0.5)
                // 가시 매립 방식으로 전환 — Housing 은 더 이상 쓰지 않는다(바인딩·flip 에서 제외, 비활성).
                var housing = EnsureChildRenderer(root.transform, "Housing", 11, Vector3.zero);
                housing.gameObject.SetActive(false);

                // 매립 덮개: 지형 타일 모양 TileBackground 를 가시(10) 위(12)에 덮어 가시가 타일에서 솟는 것처럼 보이게 한다.
                // "타일=0" 소팅 원칙의 의도적 예외. (이름 변경 금지 — BuildHudController 가 이름으로 아이콘에서 제외)
                var tileBg = root.transform.Find("TileBackground");
                if (tileBg != null)
                {
                    var tileBgSr = tileBg.GetComponent<SpriteRenderer>();
                    if (tileBgSr != null)
                    {
                        tileBgSr.sortingLayerName = "Map";
                        tileBgSr.sortingOrder     = 12;
                    }
                }
                else
                    Debug.LogWarning("[TrapStateVisualSetup] SpikeTrap: TileBackground 자식이 없습니다.");
                // 글로우(Critical/Beneficial 만)
                var glow = EnsureChildRenderer(root.transform, "Glow", 9, Vector3.zero);
                glow.color = new Color(1f, 1f, 1f, 0.6f);

                var spikes = LoadStateSprites("spike", "spikes");
                var bases  = LoadStateSprites("spike", "base");
                var glows  = LoadStateSprites("spike", "glow");
                glows[0] = null; // Normal
                glows[1] = null; // Dud

                var platformBeneficial = LoadSprite(PartPath("spike", "Beneficial", "platform"));

                WriteBindings(root, new List<BindingSpec>
                {
                    new BindingSpec { target = visualSr, sprites = spikes },
                    new BindingSpec { target = glow,     sprites = glows  },
                });

                // 초기(미변이) 표시도 Normal 에 맞춰 둔다
                visualSr.sprite  = spikes[0];
                glow.sprite      = null;
                glow.enabled     = false;

                float spikeTip = MeasureSpikeTip(visual.localScale.y);

                var trap = root.GetComponent<SpikeTrap>();
                if (trap != null)
                {
                    var so = new SerializedObject(trap);
                    so.FindProperty("beneficialColor").colorValue = Color.white;
                    SetObjectArray(so.FindProperty("flipWithAnchor"), new Object[] { glow });
                    if (spikeTip > 0f) so.FindProperty("spikeTipLocalY").floatValue = spikeTip;
                    SetObject(so, "beneficialBlockSprite", platformBeneficial);
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        /// <summary>
        /// Normal 가시 PNG 의 불투명 최상단까지 높이(Visual 피벗 기준, 유닛)를 잰다. 실패 시 0.
        /// 다른 상태 가시와 끝 높이가 다르거나 불투명 높이가 extendHeight 보다 작으면 경고.
        /// </summary>
        private static float MeasureSpikeTip(float visualScaleY)
        {
            const float extendHeight = 0.4f; // SpikeTrap.extendHeight 기본값
            float tipNormal = 0f;
            float[] tips = new float[States.Length];

            for (int i = 0; i < States.Length; i++)
            {
                string p = PartPath("spike", States[i], "spikes");
                var sp = LoadSprite(p);
                if (sp == null || !TryGetAlphaBounds(p, out RectInt bb, out _))
                {
                    Debug.LogWarning($"[TrapStateVisualSetup] 가시 끝 높이 측정 실패: {p}");
                    continue;
                }

                float ppu   = sp.pixelsPerUnit > 0f ? sp.pixelsPerUnit : PixelsPerUnit(p);
                // 스프라이트 rect 기준 피벗(px) — 단일 전체 rect 면 텍스처 좌표와 동일
                float pivotY = sp.rect.y + sp.pivot.y;
                tips[i] = ((bb.yMax) - pivotY) / ppu * visualScaleY;

                float opaqueH = bb.height / ppu * visualScaleY;
                if (opaqueH < extendHeight)
                    Debug.LogWarning($"[TrapStateVisualSetup] {States[i]} 가시 불투명 높이 {opaqueH:F3} < extendHeight {extendHeight} ({p})");
            }

            tipNormal = tips[0];
            for (int i = 1; i < tips.Length; i++)
                if (tips[i] > 0f && Mathf.Abs(tips[i] - tipNormal) > 2f / 384f)
                    Debug.LogWarning($"[TrapStateVisualSetup] {States[i]} 가시 끝 높이 {tips[i]:F3} 가 Normal {tipNormal:F3} 과 다릅니다 (2px 초과).");

            return tipNormal;
        }

        // ── 3. ArrowShooter ───────────────────────────────────────────────────

        private static void ConfigureArrowShooter()
        {
            string path = $"{PrefabDir}/ArrowShooter.prefab";
            var root = LoadPrefab(path);
            if (root == null) return;

            try
            {
                var visual = root.transform.Find("Visual");
                if (visual == null)
                {
                    Debug.LogWarning("[TrapStateVisualSetup] ArrowShooter: Visual 자식이 없습니다.");
                    return;
                }
                var visualSr = visual.GetComponent<SpriteRenderer>();
                PrepareRenderer(visualSr, 10);
                visual.localScale = Vector3.one;

                // 머즐 플래시 — Visual 자식이라 Visual 회전(발사 방향)을 그대로 따른다
                var flash = EnsureChildRenderer(visual, "MuzzleFlash", 11, new Vector3(0.45f, 0f, 0f));
                flash.enabled = false;

                var bodies = LoadStateSprites("arrow_shooter", "body");
                var flashes = LoadStateSprites("arrow_shooter", "muzzle_flash");
                var arrows = LoadStateSprites("arrow_shooter", "arrow");

                WriteBindings(root, new List<BindingSpec>
                {
                    new BindingSpec { target = visualSr, sprites = bodies },
                    new BindingSpec { target = flash,    sprites = flashes, transient = true },
                });

                visualSr.sprite = bodies[0];
                flash.sprite    = flashes[0];
                flash.enabled   = false;

                var trap = root.GetComponent<ArrowShooter>();
                if (trap != null)
                {
                    var so = new SerializedObject(trap);
                    SetStateSprites(so.FindProperty("arrowSprites"), arrows);
                    SetObject(so, "muzzleFlash", flash);
                    SetObject(so, "beneficialBlockSprite", LoadSprite(PartPath("spike", "Beneficial", "platform")));
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // ── 4. Arrow ──────────────────────────────────────────────────────────

        private static void ConfigureArrow()
        {
            string path = $"{PrefabDir}/Arrow.prefab";
            var root = LoadPrefab(path);
            if (root == null) return;

            try
            {
                // 전용 황금 화살 스프라이트가 상태별로 적용되므로 추가 틴트 제거
                var arrow = root.GetComponent<Arrow>();
                if (arrow != null)
                {
                    var so = new SerializedObject(arrow);
                    so.FindProperty("beneficialColor").colorValue = Color.white;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // ── 5. DropHammer ─────────────────────────────────────────────────────

        private static void ConfigureDropHammer()
        {
            string path = $"{PrefabDir}/DropHammer.prefab";
            var root = LoadPrefab(path);
            if (root == null) return;

            try
            {
                var visual = root.transform.Find("Visual");
                if (visual == null)
                {
                    Debug.LogWarning("[TrapStateVisualSetup] DropHammer: Visual 자식이 없습니다.");
                    return;
                }
                var visualSr = visual.GetComponent<SpriteRenderer>();
                PrepareRenderer(visualSr, 10);
                visual.localScale = Vector3.one;

                var mount  = EnsureChildRenderer(root.transform, "Mount",  9,  Vector3.zero);
                var chain  = EnsureChildRenderer(root.transform, "Chain",  8,  Vector3.zero); // 마운트(9) 뒤 — 같은 order 면 겹친 칸이 깜빡임
                var impact = EnsureChildRenderer(root.transform, "Impact", 12, Vector3.zero);
                impact.enabled = false;

                chain.drawMode = SpriteDrawMode.Tiled;
                chain.tileMode = SpriteTileMode.Continuous;

                var heads   = LoadStateSprites("drop_hammer", "head");
                var mounts  = LoadStateSprites("drop_hammer", "mount");
                var chains  = LoadStateSprites("drop_hammer", "chain");
                var impacts = LoadStateSprites("drop_hammer", "impact");

                WriteBindings(root, new List<BindingSpec>
                {
                    new BindingSpec { target = visualSr, sprites = heads },
                    new BindingSpec { target = mount,    sprites = mounts },
                    new BindingSpec { target = chain,    sprites = chains },
                    new BindingSpec { target = impact,   sprites = impacts, transient = true },
                });

                visualSr.sprite = heads[0];
                mount.sprite    = mounts[0];
                chain.sprite    = chains[0];
                impact.sprite   = impacts[0];
                impact.enabled  = false;
                if (chains[0] != null)
                    chain.size = new Vector2(chains[0].bounds.size.x, 1f);

                // 오프셋 — 스프라이트 알파 영역 기준 (캔버스 여백 무시)
                float mountBottom = 0f, headTop = 0f;
                if (TryGetAlphaBounds(PartPath("drop_hammer", "Normal", "mount"), out RectInt mb, out Vector2Int ms))
                    mountBottom = (mb.yMin - ms.y * 0.5f) / PixelsPerUnit(PartPath("drop_hammer", "Normal", "mount"));
                if (TryGetAlphaBounds(PartPath("drop_hammer", "Normal", "head"), out RectInt hb, out Vector2Int hs))
                    headTop = visual.localPosition.y +
                              (hb.yMax - hs.y * 0.5f) / PixelsPerUnit(PartPath("drop_hammer", "Normal", "head"));

                var trap = root.GetComponent<DropHammer>();
                if (trap != null)
                {
                    var so = new SerializedObject(trap);
                    SetObject(so, "mount",  mount.transform);
                    SetObject(so, "chain",  chain);
                    SetObject(so, "impact", impact);
                    so.FindProperty("mountBottomOffset").floatValue = mountBottom;
                    so.FindProperty("headTopOffset").floatValue     = headTop;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // ── 7. 플레이어 이펙트 (씬) ───────────────────────────────────────────

        private static void RunPlayerEffects()
        {
            if (!File.Exists(Path.Combine(Directory.GetParent(Application.dataPath).FullName, ScenePath)))
            {
                Debug.LogWarning($"[TrapStateVisualSetup] 씬을 찾지 못했습니다: {ScenePath}");
                return;
            }

            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var player = Object.FindFirstObjectByType<PlayerController>();
            if (player == null)
            {
                Debug.LogWarning("[TrapStateVisualSetup] 씬에서 PlayerController 를 찾지 못해 PlayerEffects 를 건너뜁니다.");
                return;
            }

            var fx = player.GetComponent<PlayerEffects>();
            bool added = fx == null;
            if (added) fx = player.gameObject.AddComponent<PlayerEffects>();

            var so = new SerializedObject(fx);
            FillFrames(so.FindProperty("dashFrames"),    "Dash");
            FillFrames(so.FindProperty("hitFrames"),     "Hit");
            FillFrames(so.FindProperty("deathFrames"),   "Death");
            FillFrames(so.FindProperty("respawnFrames"), "Respawn");

            // 신규 부착 시에만 스케일 자동 산정 (이후 수동 튜닝 보존). 이펙트 스프라이트 1유닛 = 몸 높이 기준.
            if (added)
            {
                var bodyTf = player.transform.Find("Visual");
                var body   = bodyTf != null ? bodyTf.GetComponent<SpriteRenderer>()
                                            : player.GetComponentInChildren<SpriteRenderer>();
                float h = body != null && body.sprite != null ? body.bounds.size.y : 1f;
                so.FindProperty("effectScale").floatValue = Mathf.Max(0.01f, h);
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[TrapStateVisualSetup] SampleScene Player — PlayerEffects {(added ? "추가" : "갱신")} 후 씬 저장");
        }

        /// <summary>배열이 비어 있을 때만 {dir}/00..03.png 로 채운다 (수동 튜닝 보존).</summary>
        private static void FillFrames(SerializedProperty arr, string dir)
        {
            if (arr.arraySize > 0) return;

            var frames = new List<Sprite>();
            for (int i = 0; i < 4; i++)
            {
                var s = LoadSprite($"{EffectsDir}/{dir}/{i:00}.png");
                if (s != null) frames.Add(s);
            }
            arr.arraySize = frames.Count;
            for (int i = 0; i < frames.Count; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
        }

        // ── 공통 헬퍼 ────────────────────────────────────────────────────────

        private static string PartPath(string trap, string state, string part)
            => $"{PartsDir}/{trap}/{state}/{part}.png";

        private static GameObject LoadPrefab(string path)
        {
            if (!File.Exists(Path.Combine(Directory.GetParent(Application.dataPath).FullName, path)))
            {
                Debug.LogWarning($"[TrapStateVisualSetup] 프리팹을 찾지 못했습니다: {path}");
                return null;
            }
            return PrefabUtility.LoadPrefabContents(path);
        }

        /// <summary>PNG 에서 Sprite 를 로드 (Single/Multiple 모두 — 첫 Sprite 서브에셋).</summary>
        private static Sprite LoadSprite(string path)
            => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();

        /// <summary>Normal, Dud, Critical, Beneficial 순서의 스프라이트 4종.</summary>
        private static Sprite[] LoadStateSprites(string trap, string part)
        {
            var result = new Sprite[States.Length];
            for (int i = 0; i < States.Length; i++)
            {
                result[i] = LoadSprite(PartPath(trap, States[i], part));
                if (result[i] == null)
                    Debug.LogWarning($"[TrapStateVisualSetup] 스프라이트 없음: {PartPath(trap, States[i], part)}");
            }
            return result;
        }

        private static float PixelsPerUnit(string path)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            return ti != null && ti.spritePixelsPerUnit > 0f ? ti.spritePixelsPerUnit : 100f;
        }

        /// <summary>Map 레이어 + 정렬 순서 + 흰색 틴트 지정.</summary>
        private static void PrepareRenderer(SpriteRenderer sr, int order)
        {
            if (sr == null) return;
            sr.sortingLayerName = "Map";
            sr.sortingOrder     = order;
            sr.color            = Color.white;
        }

        /// <summary>이름으로 자식 SpriteRenderer 를 찾고, 없으면 생성 (Map 레이어).</summary>
        private static SpriteRenderer EnsureChildRenderer(Transform parent, string name, int order, Vector3 localPos)
        {
            var child = parent.Find(name);
            if (child == null)
            {
                var go = new GameObject(name);
                child = go.transform;
                child.SetParent(parent, false);
            }
            child.localPosition = localPos;
            child.localRotation = Quaternion.identity;
            child.localScale    = Vector3.one;

            var sr = child.GetComponent<SpriteRenderer>();
            if (sr == null) sr = child.gameObject.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = "Map";
            sr.sortingOrder     = order;
            return sr;
        }

        /// <summary>TrapStateVisuals 를 확보하고 bindings 를 통째로 다시 쓴다 (멱등).</summary>
        private static void WriteBindings(GameObject root, List<BindingSpec> specs)
        {
            var visuals = root.GetComponent<TrapStateVisuals>();
            if (visuals == null) visuals = root.AddComponent<TrapStateVisuals>();

            var so   = new SerializedObject(visuals);
            var list = so.FindProperty("bindings");
            list.arraySize = specs.Count;
            for (int i = 0; i < specs.Count; i++)
            {
                var el = list.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("target").objectReferenceValue = specs[i].target;
                el.FindPropertyRelative("transient").boolValue         = specs[i].transient;
                SetStateSprites(el.FindPropertyRelative("sprites"), specs[i].sprites);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>TrapStateSprites 구조체 프로퍼티에 Normal/Dud/Critical/Beneficial 순서로 기록.</summary>
        private static void SetStateSprites(SerializedProperty prop, Sprite[] sprites)
        {
            prop.FindPropertyRelative("normal").objectReferenceValue     = sprites[0];
            prop.FindPropertyRelative("dud").objectReferenceValue        = sprites[1];
            prop.FindPropertyRelative("critical").objectReferenceValue   = sprites[2];
            prop.FindPropertyRelative("beneficial").objectReferenceValue = sprites[3];
        }

        private static void SetObject(SerializedObject so, string field, Object value)
        {
            var p = so.FindProperty(field);
            if (p == null)
            {
                Debug.LogWarning($"[TrapStateVisualSetup] 직렬화 필드를 찾지 못했습니다: {field}");
                return;
            }
            p.objectReferenceValue = value;
        }

        private static void SetObjectArray(SerializedProperty arr, Object[] values)
        {
            arr.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
