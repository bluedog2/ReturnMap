using System;
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
    /// 메뉴 <b>ReTrap → Setup → 맵 시스템 세팅</b> 한 번으로:
    /// <list type="number">
    ///   <item>'Map' Sorting Layer 추가 (없으면)</item>
    ///   <item>TilePaletteConfig 에셋 생성 + 기본값 채움 (없으면)</item>
    ///   <item>씬에 MapLoader 오브젝트 생성 + 팔레트/자동로드 연결</item>
    ///   <item>씬 저장</item>
    /// </list>
    /// 이미 세팅된 항목은 건너뛰므로 여러 번 실행해도 안전합니다.
    /// </summary>
    public static class MapSystemSetup
    {
        private const string PaletteAssetPath = "Assets/_Project/Settings/TilePaletteConfig.asset";
        private const string AutoLoadMapId    = "stage_01";

        [MenuItem("ReTrap/Setup/맵 시스템 세팅")]
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
            EnsureMapLoaderInScene(palette);

            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[MapSystemSetup] ✅ 세팅 완료 — 플레이하면 stage_01 이 자동 로드됩니다.");
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
                    sprite         = null, // 아트 준비되면 할당
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
                    anchor       = a,
                    markerSprite = null,
                    tint         = TilePaletteConfig.DefaultSlotTint,
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

        private static void EnsureMapLoaderInScene(TilePaletteConfig palette)
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
            so.FindProperty("_autoLoadMapId").stringValue    = AutoLoadMapId;
            so.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(loader.gameObject.scene);
        }
    }
}
