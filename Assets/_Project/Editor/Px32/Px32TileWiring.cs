using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace ReTrap.EditorTools
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  Px32TileWiring — 32px 타일 변형(Px32/Tiles) 어드레서블·아틀라스·팔레트 연결
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 메뉴 <b>ReTrap → Setup → Px32 타일 연결</b> (멱등, 배치모드는 <see cref="Run"/>):
    /// <list type="number">
    ///   <item>Px32/Tiles PNG 16장을 어드레서블 그룹 'Px32Tiles' 에 주소 'Px32/Tiles/{파일명}' 으로 등록.</item>
    ///   <item>스프라이트 아틀라스 Px32_Tiles.spriteatlasv2 생성(없을 때만).</item>
    ///   <item>TilePaletteConfig 의 Floor/Wall/Ceiling 엔트리에 A(기본)+변형 B/C 참조 연결.
    ///         덮어쓰기 전 기존 참조는 로그에 남김.</item>
    /// </list>
    /// </summary>
    public static class Px32TileWiring
    {
        private const string TilesFolder = "Assets/_Project/ResourcceEX/Sprites/Px32/Tiles";
        private const string AtlasPath   = "Assets/_Project/ResourcceEX/Atlases/Px32_Tiles.spriteatlasv2";
        private const string PalettePath = "Assets/_Project/Settings/TilePaletteConfig.asset";
        private const string GroupName   = "Px32Tiles";

        private static readonly string[] Phases = { "Play", "Build" };
        private static readonly string[] Kinds  = { "Floor", "Wall", "Ceiling" };

        [MenuItem("ReTrap/Setup/Px32 타일 연결", false, 120)]
        public static void Run()
        {
            var log = new StringBuilder();

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError("[Px32TileWiring] Addressable Settings 없음");
                return;
            }

            var palette = AssetDatabase.LoadAssetAtPath<TilePaletteConfig>(PalettePath);
            if (palette == null)
            {
                Debug.LogError("[Px32TileWiring] 팔레트 없음: " + PalettePath);
                return;
            }

            // 1. 어드레서블 등록
            var group = settings.FindGroup(GroupName);
            if (group == null)
                group = settings.CreateGroup(GroupName, false, false, false, null,
                    typeof(UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema),
                    typeof(UnityEditor.AddressableAssets.Settings.GroupSchemas.ContentUpdateGroupSchema));

            int registered = 0;
            foreach (string path in AllTilePaths())
            {
                if (!File.Exists(path))
                {
                    Debug.LogWarning("[Px32TileWiring] PNG 없음: " + path);
                    continue;
                }
                string guid  = AssetDatabase.AssetPathToGUID(path);
                var    entry = settings.CreateOrMoveEntry(guid, group);
                entry.address = "Px32/Tiles/" + Path.GetFileNameWithoutExtension(path);
                registered++;
            }
            log.AppendLine($"어드레서블 등록 {registered}장 (그룹 {GroupName})");

            // 2. 아틀라스
            log.AppendLine(EnsureAtlas(settings));

            // 3. 팔레트 참조
            var so    = new SerializedObject(palette);
            var tiles = so.FindProperty("tiles");
            int wired = 0;
            for (int i = 0; i < tiles.arraySize; i++)
            {
                var el   = tiles.GetArrayElementAtIndex(i);
                var type = (TileType)el.FindPropertyRelative("type").enumValueIndex;
                string kind = type switch
                {
                    TileType.Floor   => "Floor",
                    TileType.Wall    => "Wall",
                    TileType.Ceiling => "Ceiling",
                    _                => null,
                };
                if (kind == null) continue;

                string[] variants = type == TileType.Ceiling ? new[] { "B" } : new[] { "B", "C" };

                foreach (string phase in Phases)
                {
                    string baseField = phase == "Play" ? "spriteRef" : "buildSpriteRef";
                    string varField  = phase == "Play" ? "spriteVariants" : "buildSpriteVariants";

                    var baseProp = el.FindPropertyRelative(baseField);
                    log.AppendLine($"[{type}.{baseField}] 이전 값: {DescribeRef(baseProp)}");
                    SetRef(baseProp, $"Tile_{phase}_{kind}_A");

                    var list = el.FindPropertyRelative(varField);
                    log.AppendLine($"[{type}.{varField}] 이전 값: {DescribeList(list)}");
                    list.arraySize = variants.Length;
                    for (int v = 0; v < variants.Length; v++)
                        SetRef(list.GetArrayElementAtIndex(v), $"Tile_{phase}_{kind}_{variants[v]}");
                    wired++;
                }
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(palette);
            AssetDatabase.SaveAssets();
            log.AppendLine($"팔레트 연결 {wired}건");

            Debug.Log("[Px32TileWiring] " + log.ToString().Replace("\n", " | "));
        }

        private static System.Collections.Generic.IEnumerable<string> AllTilePaths()
        {
            foreach (string phase in Phases)
                foreach (string kind in Kinds)
                    foreach (string v in new[] { "A", "B", "C" })
                    {
                        if (kind == "Ceiling" && v == "C") continue; // Ceiling 은 A/B 만
                        yield return $"{TilesFolder}/Tile_{phase}_{kind}_{v}.png";
                    }
        }

        // 계획서의 PNG 는 Floor/Wall A/B/C + Ceiling A/B = 8종 x 2페이즈 = 16장.

        private static string EnsureAtlas(AddressableAssetSettings settings)
        {
            if (!File.Exists(AtlasPath))
            {
                var folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(TilesFolder);
                if (folder == null) return "경고: 타일 폴더 없음 — 아틀라스 생략";

                var asset = new SpriteAtlasAsset();
                asset.Add(new Object[] { folder });
                asset.SetIncludeInBuild(false); // SpriteAtlasPrebuildGenerator 규칙: 어드레서블 late-binding
                SpriteAtlasAsset.Save(asset, AtlasPath);
                AssetDatabase.ImportAsset(AtlasPath);

                var importer = AssetImporter.GetAtPath(AtlasPath) as SpriteAtlasImporter;
                if (importer != null)
                {
                    var packing = importer.packingSettings;
                    packing.padding            = 2;
                    packing.enableRotation     = false;
                    packing.enableTightPacking = false;
                    importer.packingSettings = packing;

                    var tex = importer.textureSettings;
                    tex.filterMode      = FilterMode.Point;
                    tex.generateMipMaps = false;
                    importer.textureSettings = tex;

                    var plat = importer.GetPlatformSettings("DefaultTexturePlatform");
                    plat.textureCompression = TextureImporterCompression.Uncompressed;
                    plat.overridden         = true;
                    importer.SetPlatformSettings(plat);

                    importer.includeInBuild = false;
                    importer.SaveAndReimport();
                }
                EnsureAtlasAddressable(settings);
                return "아틀라스 생성: " + AtlasPath;
            }

            EnsureAtlasAddressable(settings);
            return "아틀라스 이미 있음 (유지)";
        }

        /// <summary>SpriteAtlasPrebuildGenerator 규칙과 동일 — 기본 그룹, 주소 = 아틀라스 이름.</summary>
        private static void EnsureAtlasAddressable(AddressableAssetSettings settings)
        {
            string guid = AssetDatabase.AssetPathToGUID(AtlasPath);
            if (string.IsNullOrEmpty(guid)) return;
            var existing = settings.FindAssetEntry(guid);
            string name  = Path.GetFileNameWithoutExtension(AtlasPath);
            if (existing != null) { existing.address = name; return; }
            var entry = settings.CreateOrMoveEntry(guid, settings.DefaultGroup, false, false);
            entry.address = name;
        }

        private static void SetRef(SerializedProperty prop, string spriteName)
        {
            string path = $"{TilesFolder}/{spriteName}.png";
            prop.FindPropertyRelative("m_AssetGUID").stringValue     = AssetDatabase.AssetPathToGUID(path);
            prop.FindPropertyRelative("m_SubObjectName").stringValue = spriteName;
            prop.FindPropertyRelative("m_SubObjectType").stringValue =
                "UnityEngine.Sprite, UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null";
        }

        private static string DescribeRef(SerializedProperty prop)
        {
            string guid = prop.FindPropertyRelative("m_AssetGUID").stringValue;
            string sub  = prop.FindPropertyRelative("m_SubObjectName").stringValue;
            return $"guid={guid} sub={sub} path={AssetDatabase.GUIDToAssetPath(guid)}";
        }

        private static string DescribeList(SerializedProperty list)
        {
            if (list.arraySize == 0) return "(비어 있음)";
            var sb = new StringBuilder();
            for (int i = 0; i < list.arraySize; i++)
                sb.Append('[').Append(DescribeRef(list.GetArrayElementAtIndex(i))).Append(']');
            return sb.ToString();
        }
    }
}
