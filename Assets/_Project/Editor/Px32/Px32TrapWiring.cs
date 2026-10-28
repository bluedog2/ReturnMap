using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace ReTrap.EditorTools
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  Px32TrapWiring — 화풍 v2 함정 파츠·슬롯 마커·봉인 타일 입고 + 함정 연결
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 메뉴 <b>ReTrap → Setup → Px32 함정 연결</b> (배치모드는 <see cref="Run"/>):
    /// <list type="number">
    ///   <item>NEWIMAG/Px32TrapV2/Parts/{함정}/{상태}/{부품}.png(1배)를 기존 Sprites/Traps/Parts 같은 경로에 덮어쓴다
    ///         (.meta 유지 → GUID 유지) 후 32px 임포트 설정(PPU 32·Point·무압축·중앙 피벗) 적용.</item>
    ///   <item>NEWIMAG/Px32TrapV2/Misc 의 슬롯 마커 4방향·봉인 타일을 Px32/Traps 로 복사, 어드레서블 등록,
    ///         TilePaletteConfig 의 slotMarkers·sealRef 연결(틴트 흰색). 덮어쓰기 전 값은 로그.</item>
    ///   <item><see cref="TrapStateVisualSetup.RunTraps"/> 로 프리팹 재배선(크롭·가시 끝·해머 오프셋 재계산).</item>
    /// </list>
    /// </summary>
    public static class Px32TrapWiring
    {
        private const string InboxParts  = "NEWIMAG/Px32TrapV2/Parts";
        private const string InboxMisc   = "NEWIMAG/Px32TrapV2/Misc";
        private const string PartsDir    = "Assets/_Project/ResourcceEX/Sprites/Traps/Parts";
        private const string MiscDir     = "Assets/_Project/ResourcceEX/Sprites/Px32/Traps";
        private const string PalettePath = "Assets/_Project/Settings/TilePaletteConfig.asset";
        private const string GroupName   = "Px32Tiles";

        [MenuItem("ReTrap/Setup/Px32 함정 연결", false, 114)]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[Px32TrapWiring] 플레이 모드에서는 실행할 수 없습니다.");
                return;
            }

            var    log  = new StringBuilder();
            string root = Directory.GetParent(Application.dataPath).FullName;

            // 1. 파츠 덮어쓰기 (같은 경로 → .meta·GUID 유지)
            var copied = new System.Collections.Generic.List<string>();
            string inboxParts = Path.Combine(root, InboxParts);
            if (!Directory.Exists(inboxParts))
            {
                Debug.LogError("[Px32TrapWiring] 파츠 수신함 없음: " + InboxParts);
                return;
            }
            foreach (string src in Directory.GetFiles(inboxParts, "*.png", SearchOption.AllDirectories))
            {
                string rel  = src.Substring(inboxParts.Length + 1).Replace('\\', '/');
                string dest = $"{PartsDir}/{rel}";
                if (!File.Exists(Path.Combine(root, dest)))
                {
                    log.AppendLine("건너뜀(기존 파츠 경로 없음): " + rel);
                    continue;
                }
                File.Copy(src, Path.Combine(root, dest), true);
                copied.Add(dest);
            }

            // 2. 슬롯 마커·봉인 타일 복사 (Px32 폴더 → Px32ImportPostprocessor 가 PPU 32 적용)
            Directory.CreateDirectory(Path.Combine(root, MiscDir));
            string inboxMisc = Path.Combine(root, InboxMisc);
            int misc = 0;
            if (Directory.Exists(inboxMisc))
                foreach (string src in Directory.GetFiles(inboxMisc, "*.png"))
                {
                    File.Copy(src, Path.Combine(root, MiscDir, Path.GetFileName(src)), true);
                    misc++;
                }

            AssetDatabase.Refresh();

            foreach (string path in copied) ApplyPx32Import(path);
            log.AppendLine($"파츠 교체 {copied.Count}장, 마커·봉인 {misc}장");

            // 3. 어드레서블 + 팔레트
            WirePalette(log);

            // 4. 프리팹 재배선
            TrapStateVisualSetup.RunTraps();

            Debug.Log("[Px32TrapWiring] " + log.ToString().Replace("\n", " | "));
        }

        /// <summary>32px 픽셀아트 임포트 설정. chain·platform 은 RunTraps 의 크롭이 Multiple 로 다시 자른다.</summary>
        private static void ApplyPx32Import(string path)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) return;

            ti.textureType         = TextureImporterType.Sprite;
            ti.spriteImportMode    = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = Px32Rules.Ppu;
            ti.filterMode          = FilterMode.Point;
            ti.mipmapEnabled       = false;
            ti.alphaIsTransparency = true;
            ti.textureCompression  = TextureImporterCompression.Uncompressed;

            var settings = new TextureImporterSettings();
            ti.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot     = new Vector2(0.5f, 0.5f);
            settings.spriteMeshType  = SpriteMeshType.FullRect;
            ti.SetTextureSettings(settings);
            ti.SaveAndReimport();
        }

        private static void WirePalette(StringBuilder log)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var palette  = AssetDatabase.LoadAssetAtPath<TilePaletteConfig>(PalettePath);
            if (settings == null || palette == null)
            {
                log.AppendLine("경고: 어드레서블 설정 또는 팔레트 없음 — 마커·봉인 연결 생략");
                return;
            }
            var group = settings.FindGroup(GroupName);
            if (group == null)
            {
                log.AppendLine($"경고: 그룹 {GroupName} 없음(Px32 타일 연결 먼저) — 마커·봉인 연결 생략");
                return;
            }

            var so = new SerializedObject(palette);

            var markers = so.FindProperty("slotMarkers");
            for (int i = 0; i < markers.arraySize; i++)
            {
                var el     = markers.GetArrayElementAtIndex(i);
                var anchor = (TrapAnchor)el.FindPropertyRelative("anchor").enumValueIndex;
                string name = "SlotMarker_" + anchor;
                if (!Register(settings, group, name)) { log.AppendLine("마커 PNG 없음: " + name); continue; }
                log.AppendLine($"[slotMarkers.{anchor}] 이전 guid={el.FindPropertyRelative("markerRef").FindPropertyRelative("m_AssetGUID").stringValue}");
                SetRef(el.FindPropertyRelative("markerRef"), name);
                el.FindPropertyRelative("tint").colorValue = Color.white;
            }

            if (Register(settings, group, "SlotSeal"))
            {
                var seal = so.FindProperty("sealRef");
                log.AppendLine($"[sealRef] 이전 guid={seal.FindPropertyRelative("m_AssetGUID").stringValue}");
                SetRef(seal, "SlotSeal");
                so.FindProperty("sealTint").colorValue = Color.white;
            }
            else log.AppendLine("봉인 PNG 없음: SlotSeal");

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(palette);
            AssetDatabase.SaveAssets();
        }

        private static bool Register(UnityEditor.AddressableAssets.Settings.AddressableAssetSettings settings,
                                     UnityEditor.AddressableAssets.Settings.AddressableAssetGroup group, string name)
        {
            string guid = AssetDatabase.AssetPathToGUID($"{MiscDir}/{name}.png");
            if (string.IsNullOrEmpty(guid)) return false;
            settings.CreateOrMoveEntry(guid, group).address = "Px32/Traps/" + name;
            return true;
        }

        private static void SetRef(SerializedProperty prop, string spriteName)
        {
            prop.FindPropertyRelative("m_AssetGUID").stringValue     = AssetDatabase.AssetPathToGUID($"{MiscDir}/{spriteName}.png");
            prop.FindPropertyRelative("m_SubObjectName").stringValue = spriteName;
            prop.FindPropertyRelative("m_SubObjectType").stringValue =
                "UnityEngine.Sprite, UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null";
        }
    }
}
