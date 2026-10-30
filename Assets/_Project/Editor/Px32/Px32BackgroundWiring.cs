using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace ReTrap.EditorTools
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  Px32BackgroundWiring — 화풍 v2 배경(원경·근경 × 플레이·빌드/검증) 입고 + 팔레트 연결
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 메뉴 <b>ReTrap → Setup → Px32 배경 연결</b> (배치모드는 <see cref="Run"/>, 멱등):
    /// <list type="number">
    ///   <item>NEWIMAG/BgConceptV2/_1x/{Play|Verification}_{Far|Near}.png(1배)를 Sprites/Px32/Backgrounds 에 덮어쓴다
    ///         (.meta 유지). 규격·피벗은 Px32Rules(원경 640×360 중앙, 근경 640×256 하단).</item>
    ///   <item>어드레서블 그룹 'Px32Tiles' 에 주소 'Px32/Backgrounds/{이름}' 등록.</item>
    ///   <item>TilePaletteConfig 의 playParallax·verificationParallax far/near 참조 연결 (이전 값 로그, 계수 유지)
    ///         + 하늘 채움 색(skyColor) = 원경 맨 윗줄 색.</item>
    /// </list>
    /// </summary>
    public static class Px32BackgroundWiring
    {
        private const string Inbox       = "NEWIMAG/BgConceptV2/_1x";
        private const string BgDir       = "Assets/_Project/ResourcceEX/Sprites/Px32/Backgrounds";
        private const string PalettePath = "Assets/_Project/Settings/TilePaletteConfig.asset";
        private const string GroupName   = "Px32Tiles";

        private static readonly string[] Names = { "Play_Far", "Play_Near", "Verification_Far", "Verification_Near" };

        [MenuItem("ReTrap/Setup/Px32 배경 연결", false, 116)]
        public static void Run()
        {
            var log  = new StringBuilder();
            string root = Directory.GetParent(Application.dataPath).FullName;

            int copied = 0;
            foreach (string n in Names)
            {
                string src = Path.Combine(root, Inbox, n + ".png");
                if (!File.Exists(src)) { log.AppendLine("수신함에 없음: " + n); continue; }
                File.Copy(src, Path.Combine(root, BgDir, n + ".png"), true);
                copied++;
            }
            AssetDatabase.Refresh();
            log.AppendLine($"배경 교체 {copied}장");

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var palette  = AssetDatabase.LoadAssetAtPath<TilePaletteConfig>(PalettePath);
            var group    = settings != null ? settings.FindGroup(GroupName) : null;
            if (palette == null || group == null)
            {
                Debug.LogError("[Px32BackgroundWiring] 팔레트 또는 어드레서블 그룹 없음");
                return;
            }

            var so = new SerializedObject(palette);
            foreach (var (setField, prefix) in new[] { ("playParallax", "Play"), ("verificationParallax", "Verification") })
                foreach (var (layer, suffix) in new[] { ("far", "Far"), ("near", "Near") })
                {
                    string name = $"{prefix}_{suffix}";
                    string path = $"{BgDir}/{name}.png";
                    string guid = AssetDatabase.AssetPathToGUID(path);
                    if (string.IsNullOrEmpty(guid)) { log.AppendLine("에셋 없음: " + path); continue; }
                    settings.CreateOrMoveEntry(guid, group).address = "Px32/Backgrounds/" + name;

                    var r = so.FindProperty(setField).FindPropertyRelative(layer).FindPropertyRelative("spriteRef");
                    log.AppendLine($"[{setField}.{layer}] 이전 guid={r.FindPropertyRelative("m_AssetGUID").stringValue} → {name}");
                    r.FindPropertyRelative("m_AssetGUID").stringValue     = guid;
                    r.FindPropertyRelative("m_SubObjectName").stringValue = name;
                    r.FindPropertyRelative("m_SubObjectType").stringValue =
                        "UnityEngine.Sprite, UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null";

                    // 하늘 채움 색 = 원경 맨 윗줄 가운데 색 (1배 원경이 화면보다 낮을 때 위를 메움)
                    if (layer == "far")
                    {
                        var tex = new Texture2D(2, 2);
                        tex.LoadImage(File.ReadAllBytes(Path.Combine(root, path)));
                        Color sky = tex.GetPixel(tex.width / 2, tex.height - 1);
                        Object.DestroyImmediate(tex);
                        so.FindProperty(setField).FindPropertyRelative("skyColor").colorValue = sky;
                        log.AppendLine($"[{setField}.skyColor] → #{ColorUtility.ToHtmlStringRGB(sky)}");
                    }
                }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(palette);
            AssetDatabase.SaveAssets();

            Debug.Log("[Px32BackgroundWiring] " + log.ToString().Replace("\r", "").Replace("\n", " | "));
        }
    }
}
