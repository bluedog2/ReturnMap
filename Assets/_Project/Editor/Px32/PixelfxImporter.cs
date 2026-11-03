using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ReTrap.EditorTools
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  PixelfxImporter — PIXELFX 수신함(pixelfx 변환 시트) → Px32 이펙트 시트 + 클립
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 메뉴 <b>ReTrap → Art → pixelfx 이펙트 가져오기</b>:
    /// <c>tools/vfx/pixelfx-render.ps1</c> 가 화풍 v2 규격으로 변환해 둔 <c>PIXELFX/&lt;Name&gt;/Pfx_&lt;Name&gt;.png + .json</c> 을
    /// <c>Sprites/Px32/Effects/Pixelfx/</c> 로 복사하고(슬라이스·피벗은 <see cref="Px32ImportPostprocessor"/>),
    /// <see cref="PixelArtSheetValidator"/> 로 검사한 뒤 <c>Animations/Effects/Pixelfx/Pfx_&lt;Name&gt;.anim</c> 클립을 만듭니다.
    /// <para>변환(팔레트 양자화·셀 재배치)은 Unity 없이 <c>tools/vfx/pixelfx-convert.mjs</c> 가 합니다 — 자동화(spec worktree)도 같은 결과.</para>
    /// <para>접두사 <c>Pfx_</c> 는 GPT 이펙트(<c>Fx_*</c>, <see cref="Px32Rules"/> 규격표)와 이름이 겹치지 않게 하려는 것입니다.
    /// 시트가 이미 있으면 복사는 건너뛰고 클립만 없으면 만듭니다(자동화가 시트만 넣은 경우). 프리팹·씬 배선은 하지 않습니다.</para>
    /// </summary>
    public static class PixelfxImporter
    {
        private const string InboxFolder = "PIXELFX";
        private const string DestFolder  = Px32Rules.RootFolder + "/Effects/Pixelfx";
        private const string ClipFolder  = "Assets/_Project/ResourcceEX/Animations/Effects/Pixelfx";

        // ── pixelfx-convert.mjs 출력 JSON ──
        [Serializable] private class SheetInfo { public string effect; public int cell; public int frames; public float fps; public bool loop; }

        [MenuItem("ReTrap/Art/pixelfx 이펙트 가져오기", false, 2)]
        public static void Run()
        {
            string inboxAbs = Path.Combine(Px32Rules.ProjectRoot(), InboxFolder).Replace('\\', '/');
            string src = EditorUtility.OpenFolderPanel("가져올 이펙트 폴더 선택 (PIXELFX 안)", inboxAbs, "");
            if (string.IsNullOrEmpty(src)) return;
            src = src.Replace('\\', '/');
            if (!src.StartsWith(inboxAbs + "/"))
            {
                EditorUtility.DisplayDialog("오류", "PIXELFX 폴더 안의 이펙트 폴더를 선택하세요.", "확인");
                return;
            }
            EditorUtility.DisplayDialog("pixelfx 가져오기", Import(Path.GetFileName(src)), "확인");
        }

        /// <summary>PIXELFX 하위 폴더 전부 가져오기. 배치모드 <c>-executeMethod</c> 진입점.</summary>
        [MenuItem("ReTrap/Art/pixelfx 이펙트 모두 가져오기", false, 3)]
        public static void ImportAll()
        {
            string inboxAbs = Path.Combine(Px32Rules.ProjectRoot(), InboxFolder);
            if (!Directory.Exists(inboxAbs)) { Debug.LogWarning("[PixelfxImporter] 수신함 없음: " + InboxFolder); return; }
            foreach (string dir in Directory.GetDirectories(inboxAbs))
                Debug.Log("[PixelfxImporter] " + Import(Path.GetFileName(dir)).Replace("\n", " | "));
        }

        /// <summary>
        /// <c>PIXELFX/&lt;name&gt;</c> 를 가져옵니다. 결과 요약 문자열을 반환합니다(MCP·배치에서 직접 호출 가능).
        /// </summary>
        public static string Import(string name)
        {
            string baseName = "Pfx_" + name;
            string srcDir   = Path.Combine(Px32Rules.ProjectRoot(), InboxFolder, name);
            string srcPng   = Path.Combine(srcDir, baseName + ".png");
            string srcJson  = Path.Combine(srcDir, baseName + ".json");
            if (!File.Exists(srcPng) || !File.Exists(srcJson)) return $"원본 없음: {InboxFolder}/{name}/{baseName}.png/.json — tools\\vfx\\pixelfx-render.ps1 로 만드세요";

            var info = JsonUtility.FromJson<SheetInfo>(File.ReadAllText(srcJson));
            if (info == null || info.fps <= 0) return "JSON 형식 오류(fps): " + srcJson;

            // 1) 복사 (이미 있으면 건너뜀)
            string destPath = $"{DestFolder}/{baseName}.png";
            string destAbs  = Path.Combine(Px32Rules.ProjectRoot(), destPath);
            bool copied = !File.Exists(destAbs);
            if (copied)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destAbs));
                File.Copy(srcPng, destAbs);
                AssetDatabase.Refresh();
            }

            // 2) 규격 검사 (CI sprites32 와 같은 기준)
            var palette = Px32Rules.LoadPalette(true);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            PixelArtSheetValidator.Result res;
            try
            {
                if (!tex.LoadImage(File.ReadAllBytes(destAbs))) return "PNG 디코딩 실패: " + destPath;
                res = PixelArtSheetValidator.Validate(tex.GetPixels32(), tex.width, tex.height,
                    PixelArtSheetValidator.OptionsFor("Effects", baseName, palette, false));
            }
            finally { UnityEngine.Object.DestroyImmediate(tex); }

            // 3) 클립
            string clipNote = res.Passed
                ? CreateClip(destPath, $"{ClipFolder}/{baseName}.anim", info.fps, info.loop)
                : "클립 생략(검사 FAIL)";

            string report = $"{info.effect} → {destPath} ({(copied ? "복사" : "이미 있음")})\n" +
                            $"셀 {info.cell}px ({info.cell / (float)Px32Rules.Ppu:0.##}칸) × {info.frames}프레임, {info.fps}fps, loop={info.loop}\n" +
                            (res.Passed ? "검사 PASS" : "검사 FAIL\n" + res.Summary()) + "\n" + clipNote;
            if (res.Passed) Debug.Log("[PixelfxImporter] " + report.Replace("\n", " | "));
            else Debug.LogWarning("[PixelfxImporter] " + report.Replace("\n", " | "));
            return report;
        }

        /// <summary>슬라이스된 Pfx_X_0.. 스프라이트로 SpriteRenderer 클립 생성. 이미 있으면 건너뜀.</summary>
        private static string CreateClip(string sheetPath, string clipPath, float fps, bool loop)
        {
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath) != null) return "클립 건너뜀(이미 있음): " + clipPath;

            var sprites = AssetDatabase.LoadAllAssetsAtPath(sheetPath).OfType<Sprite>()
                .OrderBy(s => int.TryParse(s.name.Substring(s.name.LastIndexOf('_') + 1), out int k) ? k : 0)
                .ToArray();
            if (sprites.Length == 0) return "클립 없음: 슬라이스된 스프라이트가 없음 — " + sheetPath;

            var keys = new List<ObjectReferenceKeyframe>();
            for (int i = 0; i < sprites.Length; i++)
                keys.Add(new ObjectReferenceKeyframe { time = i / fps, value = sprites[i] });
            if (!loop) keys.Add(new ObjectReferenceKeyframe { time = sprites.Length / fps, value = sprites[sprites.Length - 1] });

            var clip = new AnimationClip { frameRate = fps };
            var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys.ToArray());
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            Directory.CreateDirectory(Path.Combine(Px32Rules.ProjectRoot(), ClipFolder));
            AssetDatabase.Refresh();
            AssetDatabase.CreateAsset(clip, clipPath);
            AssetDatabase.SaveAssets();
            return $"클립: {clipPath} ({sprites.Length}프레임)";
        }
    }
}
