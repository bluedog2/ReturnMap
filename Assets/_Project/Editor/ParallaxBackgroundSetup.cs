using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace ReTrap.EditorTools
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  ParallaxBackgroundSetup — 패럴랙스 배경 + 페이즈별 타일 팔레트 원클릭 세팅
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 메뉴 <b>ReTrap → Setup → 패럴랙스 배경 세팅</b> (멱등):
    /// <list type="number">
    ///   <item>NEWIMAG/Backgrounds/Chapter1 의 PNG 를 ResourcceEX/Sprites/Backgrounds/Chapter1 로 가져오기
    ///         (<see cref="ArtInboxImporter"/> — 이미 있는 파일은 건너뜀. 이미지를 교체하려면 사람이 대상 PNG 를 지우고 다시 실행).</item>
    ///   <item>그 PNG 의 임포트 설정(Sprite Single·FullRect·Center 피벗·Point·무압축·밉맵 끔).</item>
    ///   <item>어드레서블 등록 (그룹 'Backgrounds', 주소 Bg/Parallax/Chapter1/{Verification|Play}/{far|near}).</item>
    ///   <item>팔레트의 패럴랙스 참조, Floor/Wall/Ceiling buildSpriteRef, renderTileBackground=false 설정.</item>
    /// </list>
    /// 배경 PNG 가 없으면 1~3 은 건너뛰고 4 의 타일/토글 부분만 처리합니다.
    /// </summary>
    public static class ParallaxBackgroundSetup
    {
        private const string PalettePath   = "Assets/_Project/Settings/TilePaletteConfig.asset";
        private const string DestFolder    = "Assets/_Project/ResourcceEX/Sprites/Backgrounds/Chapter1";
        private const string InboxSub      = "Backgrounds/Chapter1";
        private const string BuildAtlas    = "Assets/_Project/ResourcceEX/Atlases/Environment_BuildPhase_Chapter1.spriteatlasv2";

        private static readonly string[] Sets   = { "Verification", "Play" };
        private static readonly string[] Layers = { "far", "near" };

        [MenuItem("ReTrap/Setup/패럴랙스 배경 세팅", false, 110)]
        public static void Run()
        {
            var log = new StringBuilder();

            // 1. 가져오기
            string importReport = ArtInboxImporter.Import(InboxSub, DestFolder);
            log.AppendLine(importReport.Replace("\n\n", " | ").Replace("\n", " | "));

            // 배경 PNG 존재 여부
            bool hasBg = true;
            foreach (var set in Sets)
                foreach (var layer in Layers)
                    if (!File.Exists(PngPath(set, layer))) hasBg = false;

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError("[ParallaxBackgroundSetup] Addressable Settings 없음");
                return;
            }

            var palette = AssetDatabase.LoadAssetAtPath<TilePaletteConfig>(PalettePath);
            if (palette == null)
            {
                Debug.LogError("[ParallaxBackgroundSetup] 팔레트 없음: " + PalettePath);
                return;
            }
            var so = new SerializedObject(palette);

            if (hasBg)
            {
                // 2. 임포트 설정  3. 어드레서블 등록  4a. 팔레트 참조
                var group = settings.FindGroup("Backgrounds") ?? settings.DefaultGroup;
                foreach (var set in Sets)
                    foreach (var layer in Layers)
                    {
                        string path = PngPath(set, layer);
                        ConfigureImporter(path);

                        string guid = AssetDatabase.AssetPathToGUID(path);
                        var entry = settings.CreateOrMoveEntry(guid, group);
                        entry.address = $"Bg/Parallax/Chapter1/{set}/{layer}";

                        string field = set == "Play" ? "playParallax" : "verificationParallax";
                        SetRef(so.FindProperty($"{field}.{layer}.spriteRef"), guid, "");
                    }
                log.AppendLine("패럴랙스 스프라이트 4종 등록·참조 완료");
            }
            else
            {
                Debug.LogWarning("[ParallaxBackgroundSetup] NEWIMAG/Backgrounds/Chapter1 PNG(4종)가 없어 " +
                                 "배경 임포트·어드레서블·참조 단계를 건너뜁니다.");
                log.AppendLine("경고: 배경 PNG 없음 — 1~3단계 생략");
            }

            // 4b. 빌드용 타일 스프라이트 (같은 형식: 아틀라스 GUID + SubObjectName)
            string atlasGuid = AssetDatabase.AssetPathToGUID(BuildAtlas);
            if (string.IsNullOrEmpty(atlasGuid))
            {
                Debug.LogWarning("[ParallaxBackgroundSetup] 빌드 아틀라스 없음: " + BuildAtlas);
                log.AppendLine("경고: 빌드 아틀라스 없음 — buildSpriteRef 생략");
            }
            else
            {
                if (settings.FindAssetEntry(atlasGuid) == null)
                {
                    var atlasEntry = settings.CreateOrMoveEntry(atlasGuid, settings.DefaultGroup);
                    log.AppendLine("빌드 아틀라스 어드레서블 등록: " + atlasEntry.address);
                }

                var tiles = so.FindProperty("tiles");
                int set = 0;
                for (int i = 0; i < tiles.arraySize; i++)
                {
                    var el = tiles.GetArrayElementAtIndex(i);
                    var type = (TileType)el.FindPropertyRelative("type").enumValueIndex;
                    string sub = type switch
                    {
                        TileType.Floor   => "Build_C1_r01_c03",
                        TileType.Wall    => "Build_C1_r01_c04",
                        TileType.Ceiling => "Build_C1_r01_c05",
                        _                => null,
                    };
                    if (sub == null) continue;
                    SetRef(el.FindPropertyRelative("buildSpriteRef"), atlasGuid, sub);
                    set++;
                }
                log.AppendLine($"buildSpriteRef 설정: {set}개");
            }

            // 4c. 타일 격자 배경 끄기
            so.FindProperty("renderTileBackground").boolValue = false;

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(palette);
            AssetDatabase.SaveAssets();

            Debug.Log("[ParallaxBackgroundSetup] " + log.ToString().Replace("\n", " | "));
            EditorUtility.DisplayDialog("패럴랙스 배경 세팅", log.ToString(), "확인");
        }

        private static string PngPath(string set, string layer) => $"{DestFolder}/{set}/{layer}.png";

        private static void SetRef(SerializedProperty prop, string guid, string sub)
        {
            prop.FindPropertyRelative("m_AssetGUID").stringValue     = guid;
            prop.FindPropertyRelative("m_SubObjectName").stringValue = sub;
        }

        private static void ConfigureImporter(string path)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) return;

            ti.textureType         = TextureImporterType.Sprite;
            ti.spriteImportMode    = SpriteImportMode.Single;
            ti.filterMode          = FilterMode.Point;
            ti.textureCompression  = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled       = false;
            ti.alphaIsTransparency = true;

            var s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            s.spriteMeshType  = SpriteMeshType.FullRect;
            s.spriteAlignment = (int)SpriteAlignment.Center;
            s.spritePivot     = new Vector2(0.5f, 0.5f);
            ti.SetTextureSettings(s);
            ti.SaveAndReimport();
        }
    }
}
