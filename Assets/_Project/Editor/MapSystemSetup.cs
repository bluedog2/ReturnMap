using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ReTrap.EditorTools
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  MapSystemSetup — 맵 시스템 씬/에셋 원클릭 세팅 (1회용 유틸)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 메뉴 <b>ReTrap → Setup → 5. 맵 시스템 세팅</b> 한 번으로:
    /// <list type="number">
    ///   <item>'Map' Sorting Layer 추가 (없으면)</item>
    ///   <item>TilePaletteConfig 에셋 생성 + 기본값 채움 (없으면)</item>
    ///   <item>StageCatalog 에셋 생성 + StreamingAssets/Maps 스캔 시드 (없으면)</item>
    ///   <item>씬에 MapLoader 오브젝트 생성 + 팔레트/자동로드/카탈로그 연결</item>
    ///   <item>씬 저장</item>
    /// </list>
    /// 이미 세팅된 항목은 건너뛰므로 여러 번 실행해도 안전합니다.
    /// </summary>
    public static class MapSystemSetup
    {
        private const string PaletteAssetPath      = "Assets/_Project/Settings/TilePaletteConfig.asset";
        private const string StageCatalogAssetPath = "Assets/_Project/Settings/StageCatalog.asset";
        private const string AutoLoadMapId         = "stage_01";

        [MenuItem("ReTrap/Setup/5. 맵 시스템 세팅", false, 5)]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("맵 시스템 세팅",
                    "플레이 모드에서는 실행할 수 없습니다.\n플레이를 중지한 뒤 다시 실행하세요.", "확인");
                return;
            }

            EnsureSortingLayer("Map");
            var palette = EnsurePaletteAsset();
            var catalog = EnsureStageCatalogAsset();
            EnsureMapLoaderInScene(palette, catalog);

            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[MapSystemSetup] ✅ 세팅 완료 — 플레이하면 MapLoader 의 자동 로드 맵이 로드됩니다.");
        }

        // ── 플레이어 벽 마찰 제거 ─────────────────────────────────────────────

        private const string FrictionlessMatPath =
            "Assets/_Project/Settings/PlayerFrictionless.physicsMaterial2D";

        /// <summary>
        /// 마찰 0 PhysicsMaterial2D 를 만들어 플레이어 캡슐에 할당합니다.
        /// <para>공중에서 벽에 이동키를 누르고 있으면 마찰이 중력을 상쇄해
        /// 벽에 매달리는 현상(wall stick)을 제거합니다.</para>
        /// </summary>
        [MenuItem("ReTrap/Setup/기타/플레이어 벽 마찰 제거", false, 101)]
        public static void SetupPlayerFrictionless()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("벽 마찰 제거",
                    "플레이 모드에서는 실행할 수 없습니다.\n플레이를 중지한 뒤 다시 실행하세요.", "확인");
                return;
            }

            // 1. 마찰 0 머티리얼 에셋 (없으면 생성)
            var mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(FrictionlessMatPath);
            if (mat == null)
            {
                mat = new PhysicsMaterial2D("PlayerFrictionless")
                {
                    friction        = 0f,
                    bounciness      = 0f,
                    // Minimum: 상대(타일 0.4)와 조합해도 항상 0 보장
                    frictionCombine = PhysicsMaterialCombine2D.Minimum,
                    bounceCombine   = PhysicsMaterialCombine2D.Minimum,
                };
                AssetDatabase.CreateAsset(mat, FrictionlessMatPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[MapSystemSetup] PhysicsMaterial2D 생성 → {FrictionlessMatPath}");
            }

            // 2. 플레이어 캡슐에 할당
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            if (player == null)
            {
                Debug.LogWarning("[MapSystemSetup] 씬에서 PlayerController 를 찾지 못했습니다.");
                return;
            }

            var capsule = player.GetComponent<CapsuleCollider2D>();
            if (capsule == null)
            {
                Debug.LogWarning("[MapSystemSetup] Player 에 CapsuleCollider2D 가 없습니다.");
                return;
            }

            capsule.sharedMaterial = mat;
            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[MapSystemSetup] ✅ 플레이어 캡슐에 마찰 0 머티리얼 적용 — 벽 매달림 제거 완료");
        }

        // ── 1. Sorting Layer ─────────────────────────────────────────────────

        private static void EnsureSortingLayer(string layerName)
        {
            var tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("m_SortingLayers");

            for (int i = 0; i < layers.arraySize; i++)
            {
                var name = layers.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue;
                if (name == layerName)
                {
                    Debug.Log($"[MapSystemSetup] Sorting Layer '{layerName}' 이미 존재 — 건너뜀");
                    return;
                }
            }

            layers.InsertArrayElementAtIndex(layers.arraySize);
            var entry = layers.GetArrayElementAtIndex(layers.arraySize - 1);
            entry.FindPropertyRelative("name").stringValue = layerName;
            entry.FindPropertyRelative("uniqueID").uintValue =
                (uint)UnityEngine.Random.Range(1, int.MaxValue);
            tagManager.ApplyModifiedProperties();

            Debug.Log($"[MapSystemSetup] Sorting Layer '{layerName}' 추가 완료");
        }

        // ── 2. TilePaletteConfig 에셋 ────────────────────────────────────────

        private static TilePaletteConfig EnsurePaletteAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TilePaletteConfig>(PaletteAssetPath);
            if (existing != null)
            {
                Debug.Log("[MapSystemSetup] TilePaletteConfig 이미 존재 — 건너뜀");
                return existing;
            }

            // 폴더 보장
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Settings"))
                AssetDatabase.CreateFolder("Assets/_Project", "Settings");

            var palette = ScriptableObject.CreateInstance<TilePaletteConfig>();

            // Ground 레이어 마스크 (없으면 경고 후 Default)
            int groundIdx = LayerMask.NameToLayer("Ground");
            if (groundIdx < 0)
            {
                Debug.LogWarning("[MapSystemSetup] 'Ground' 레이어 없음 — Default 레이어로 설정. " +
                                 "Project Settings → Tags and Layers 에서 추가 후 팔레트를 수정하세요.");
                groundIdx = 0;
            }
            LayerMask groundMask = 1 << groundIdx;

            // private 직렬화 필드 → 리플렉션으로 채움
            var tiles = new System.Collections.Generic.List<TileVisual>();
            foreach (TileType t in Enum.GetValues(typeof(TileType)))
            {
                tiles.Add(new TileVisual
                {
                    type           = t,
                    // spriteRef / prefabRef 는 어드레서블 — Inspector 또는 마이그레이션에서 할당
                    fallbackColor  = TilePaletteConfig.FallbackColor(t),
                    isSolid        = t != TileType.Empty,
                    collisionLayer = t != TileType.Empty ? groundMask : (LayerMask)0,
                });
            }

            var slots = new System.Collections.Generic.List<SlotVisual>();
            foreach (TrapAnchor a in Enum.GetValues(typeof(TrapAnchor)))
            {
                slots.Add(new SlotVisual
                {
                    anchor = a,
                    // markerRef 는 어드레서블 — Inspector 에서 할당
                    tint   = TilePaletteConfig.DefaultSlotTint,
                });
            }

            SetPrivateField(palette, "tiles",       tiles);
            SetPrivateField(palette, "slotMarkers", slots);

            // URP Sprite-Lit-Default 머티리얼 (Light2D 영향)
            var mat = AssetDatabase.LoadAssetAtPath<Material>(
                "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat");
            if (mat != null)
                SetPrivateField(palette, "sharedMaterial", mat);
            else
                Debug.LogWarning("[MapSystemSetup] Sprite-Lit-Default 머티리얼을 찾지 못함 — " +
                                 "팔레트 Inspector 에서 수동 할당하세요.");

            AssetDatabase.CreateAsset(palette, PaletteAssetPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[MapSystemSetup] TilePaletteConfig 생성 완료 → {PaletteAssetPath}");
            return palette;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var f = target.GetType().GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null) f.SetValue(target, value);
            else Debug.LogError($"[MapSystemSetup] 필드 없음: {fieldName}");
        }

        // ── 3. MapLoader 씬 오브젝트 ─────────────────────────────────────────

        private static void EnsureMapLoaderInScene(TilePaletteConfig palette, StageCatalog catalog)
        {
            var loader = UnityEngine.Object.FindFirstObjectByType<MapLoader>();
            if (loader == null)
            {
                var go = new GameObject("MapLoader");
                loader = go.AddComponent<MapLoader>();
                Undo.RegisterCreatedObjectUndo(go, "Create MapLoader");
                Debug.Log("[MapSystemSetup] MapLoader 오브젝트 생성");
            }
            else
            {
                Debug.Log("[MapSystemSetup] MapLoader 이미 존재 — 설정만 갱신");
            }

            // private [SerializeField] → SerializedObject 로 안전하게 주입
            var so = new SerializedObject(loader);
            so.FindProperty("_palette").objectReferenceValue = palette;

            // 씬에 이미 값이 있으면 보존(재실행 시 자동 로드 맵이 조용히 바뀌는 것 방지).
            // 비어있을 때만 기본값을 채운다.
            var autoLoadProp = so.FindProperty("_autoLoadMapId");
            if (string.IsNullOrEmpty(autoLoadProp.stringValue))
                autoLoadProp.stringValue = AutoLoadMapId;
            else
                Debug.Log($"[MapSystemSetup] _autoLoadMapId 기존 값 유지: {autoLoadProp.stringValue}");

            // 카탈로그도 비어있을 때만 배선 (기존 배선 보존)
            var catalogProp = so.FindProperty("_stageCatalog");
            if (catalogProp.objectReferenceValue == null)
                catalogProp.objectReferenceValue = catalog;

            so.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(loader.gameObject.scene);
        }

        // ── 4. StageCatalog 에셋 ─────────────────────────────────────────────

        /// <summary>
        /// StageCatalog 에셋이 없으면 생성하고 StreamingAssets/Maps 의 JSON 을 스캔해
        /// mapId 순으로 시드합니다. 이미 존재하면 내용을 덮어쓰지 않고 그대로 반환합니다(멱등).
        /// </summary>
        private static StageCatalog EnsureStageCatalogAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<StageCatalog>(StageCatalogAssetPath);
            if (existing != null)
            {
                Debug.Log("[MapSystemSetup] StageCatalog 이미 존재 — 건너뜀");
                return existing;
            }

            if (!AssetDatabase.IsValidFolder("Assets/_Project/Settings"))
                AssetDatabase.CreateFolder("Assets/_Project", "Settings");

            var catalog = ScriptableObject.CreateInstance<StageCatalog>();
            var entries = ScanStreamingAssetsMaps();
            SetPrivateField(catalog, "_stages", entries);

            AssetDatabase.CreateAsset(catalog, StageCatalogAssetPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[MapSystemSetup] StageCatalog 생성 완료 → {StageCatalogAssetPath} " +
                      $"({entries.Count}개 스테이지 시드)");
            return catalog;
        }

        /// <summary>StreamingAssets/Maps 의 *.json 을 mapId 순으로 스캔해 카탈로그 엔트리로 변환합니다.</summary>
        private static List<StageCatalog.StageEntry> ScanStreamingAssetsMaps()
        {
            var entries = new List<StageCatalog.StageEntry>();

            string mapsDir = Path.Combine(Application.streamingAssetsPath, "Maps");
            if (!Directory.Exists(mapsDir))
            {
                Debug.LogWarning($"[MapSystemSetup] StreamingAssets/Maps 폴더 없음: {mapsDir}");
                return entries;
            }

            var files = Directory.GetFiles(mapsDir, "*.json");
            var parsed = new List<(string mapId, string displayName)>();

            foreach (var file in files)
            {
                try
                {
                    string json = File.ReadAllText(file, System.Text.Encoding.UTF8);
                    var    map  = MapData.FromJson(json);
                    if (map != null && !string.IsNullOrEmpty(map.mapId))
                        parsed.Add((map.mapId, map.displayName));
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[MapSystemSetup] 맵 스캔 실패 ({file}): {e.Message}");
                }
            }

            foreach (var (mapId, displayName) in parsed.OrderBy(p => p.mapId, StringComparer.Ordinal))
            {
                entries.Add(new StageCatalog.StageEntry
                {
                    mapId       = mapId,
                    displayName = displayName,
                });
            }

            return entries;
        }
    }
}
