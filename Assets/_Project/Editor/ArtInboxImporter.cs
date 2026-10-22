using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ReTrap.EditorTools
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  ArtInboxImporter — NEWIMAG 수신함 PNG 를 ResourcceEX 로 가져오기
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 메뉴 <b>ReTrap → Art → NEWIMAG 수신함 가져오기</b>:
    /// 저장소 루트 <c>NEWIMAG/</c>(Assets 밖) 의 선택 폴더에서 PNG 만 복사해
    /// <c>Assets/_Project/ResourcceEX/</c> 아래 대상 폴더에 넣고 스프라이트 임포트 설정을 맞춥니다.
    /// <para>.meta 는 복사하지 않습니다(새 GUID). 이미 있는 파일은 건너뜁니다(기존 참조 보호).</para>
    /// <para>원본 폴더에 manifest.json(v2 형식)이 있으면 동작별 AnimationClip 도 만듭니다(기존 .anim 은 건너뜀).
    /// Animator·프리팹·소팅 레이어 배선은 하지 않습니다.</para>
    /// </summary>
    public static class ArtInboxImporter
    {
        private const string InboxFolder   = "NEWIMAG";
        private const string ResourceRoot  = "Assets/_Project/ResourcceEX";
        private const float  DefaultPpu    = 256f;
        private static readonly Vector2 DefaultPivot = new Vector2(0.5f, 0.125f);

        // ── manifest.json (v2) ──
        [Serializable] private class Manifest { public float pixelsPerUnit; public float[] pivot; public Character[] characters; }
        [Serializable] private class Character { public string id; public Anim[] animations; }
        [Serializable] private class Anim { public string state; public float fps; public bool loop; public string[] frames; }

        [MenuItem("ReTrap/Art/NEWIMAG 수신함 가져오기", false, 0)]
        public static void Run()
        {
            string inboxAbs = Path.Combine(ProjectRoot(), InboxFolder).Replace('\\', '/');
            string src = EditorUtility.OpenFolderPanel("가져올 수신함 폴더 선택 (NEWIMAG 안)", inboxAbs, "");
            if (string.IsNullOrEmpty(src)) return;
            src = src.Replace('\\', '/');
            if (!src.StartsWith(inboxAbs))
            {
                EditorUtility.DisplayDialog("오류", "NEWIMAG 폴더 안을 선택하세요.", "확인");
                return;
            }

            string destAbs = EditorUtility.OpenFolderPanel("복사할 대상 폴더 선택 (ResourcceEX 안)",
                Path.Combine(ProjectRoot(), ResourceRoot + "/Sprites"), "");
            if (string.IsNullOrEmpty(destAbs)) return;
            string dest = ToAssetPath(destAbs);
            if (dest == null || !(dest == ResourceRoot || dest.StartsWith(ResourceRoot + "/")))
            {
                EditorUtility.DisplayDialog("오류", "Assets/_Project/ResourcceEX 안을 선택하세요.", "확인");
                return;
            }

            string report = Import(src.Substring(inboxAbs.Length).TrimStart('/'), dest);
            EditorUtility.DisplayDialog("NEWIMAG 가져오기", report, "확인");
        }

        /// <summary>
        /// NEWIMAG 기준 상대 폴더 <paramref name="inboxSubFolder"/> 의 PNG 를 <paramref name="destFolder"/>(Assets 경로)로 가져옵니다.
        /// 하위 폴더 구조는 유지합니다. 결과 요약 문자열을 반환합니다(MCP 에서 직접 호출 가능).
        /// </summary>
        public static string Import(string inboxSubFolder, string destFolder)
        {
            string srcRoot = Path.Combine(ProjectRoot(), InboxFolder, inboxSubFolder ?? "").Replace('\\', '/').TrimEnd('/');
            if (!Directory.Exists(srcRoot)) return "원본 폴더 없음: " + srcRoot;
            if (!destFolder.StartsWith(ResourceRoot)) return "대상은 " + ResourceRoot + " 안이어야 합니다: " + destFolder;

            var manifest = LoadManifest(srcRoot);
            float ppu = manifest != null && manifest.pixelsPerUnit > 0 ? manifest.pixelsPerUnit : DefaultPpu;
            Vector2 pivot = manifest?.pivot != null && manifest.pivot.Length == 2
                ? new Vector2(manifest.pivot[0], manifest.pivot[1]) : DefaultPivot;

            // 1) PNG 복사 (기존 파일은 건너뜀)
            var copied = new List<string>();
            int skipped = 0;
            foreach (var file in Directory.GetFiles(srcRoot, "*.png", SearchOption.AllDirectories))
            {
                string rel = file.Replace('\\', '/').Substring(srcRoot.Length + 1);
                string destPath = destFolder + "/" + rel;
                string destAbs = Path.Combine(ProjectRoot(), destPath);
                if (File.Exists(destAbs)) { skipped++; continue; }
                Directory.CreateDirectory(Path.GetDirectoryName(destAbs));
                File.Copy(file, destAbs);
                copied.Add(destPath);
            }
            AssetDatabase.Refresh();

            // 2) 임포트 설정: Sprite(Single) · Point · 무압축 · PPU · 커스텀 피벗
            foreach (var path in copied)
            {
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) continue;
                ti.textureType         = TextureImporterType.Sprite;
                ti.spriteImportMode    = SpriteImportMode.Single;
                ti.filterMode          = FilterMode.Point;
                ti.textureCompression  = TextureImporterCompression.Uncompressed;
                ti.mipmapEnabled       = false;
                ti.alphaIsTransparency = true;
                ti.spritePixelsPerUnit = ppu;
                var s = new TextureImporterSettings();
                ti.ReadTextureSettings(s);
                s.spriteAlignment = (int)SpriteAlignment.Custom;
                s.spritePivot     = pivot;
                ti.SetTextureSettings(s);
                ti.SaveAndReimport();
            }

            // 3) manifest 가 있으면 동작별 AnimationClip 생성
            int clips = manifest?.characters != null ? CreateClips(manifest, destFolder) : 0;

            string report = $"{InboxFolder}/{inboxSubFolder} → {destFolder}\n\n복사: {copied.Count}개\n건너뜀(이미 있음): {skipped}개\n애니메이션 클립: {clips}개";
            Debug.Log("[ArtInboxImporter] " + report.Replace("\n\n", " | ").Replace("\n", " | "));
            return report;
        }

        /// <summary>manifest 의 동작마다 &lt;dest&gt;/&lt;캐릭터id&gt;/&lt;state&gt;.anim 생성. 이미 있으면 건너뜀.</summary>
        private static int CreateClips(Manifest manifest, string destFolder)
        {
            int created = 0;
            foreach (var ch in manifest.characters)
            {
                if (ch?.animations == null) continue;
                foreach (var anim in ch.animations)
                {
                    if (anim?.frames == null || anim.frames.Length == 0 || anim.fps <= 0) continue;
                    string clipPath = $"{destFolder}/{ch.id}/{anim.state}.anim";
                    if (AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath) != null) continue;

                    var keys = new List<ObjectReferenceKeyframe>();
                    for (int i = 0; i < anim.frames.Length; i++)
                    {
                        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(destFolder + "/" + anim.frames[i]);
                        if (sprite == null) { Debug.LogWarning("[ArtInboxImporter] 프레임 없음: " + anim.frames[i]); continue; }
                        keys.Add(new ObjectReferenceKeyframe { time = i / anim.fps, value = sprite });
                    }
                    if (keys.Count == 0) continue;

                    var clip = new AnimationClip { frameRate = anim.fps };
                    var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
                    AnimationUtility.SetObjectReferenceCurve(clip, binding, keys.ToArray());
                    var settings = AnimationUtility.GetAnimationClipSettings(clip);
                    settings.loopTime = anim.loop;
                    AnimationUtility.SetAnimationClipSettings(clip, settings);

                    Directory.CreateDirectory(Path.Combine(ProjectRoot(), destFolder, ch.id));
                    AssetDatabase.CreateAsset(clip, clipPath);
                    created++;
                }
            }
            AssetDatabase.SaveAssets();
            return created;
        }

        private static Manifest LoadManifest(string srcRoot)
        {
            string path = Path.Combine(srcRoot, "manifest.json");
            if (!File.Exists(path)) return null;
            try { return JsonUtility.FromJson<Manifest>(File.ReadAllText(path)); }
            catch (Exception e) { Debug.LogWarning("[ArtInboxImporter] manifest 파싱 실패: " + e.Message); return null; }
        }

        private static string ProjectRoot() => Path.GetDirectoryName(Application.dataPath);

        private static string ToAssetPath(string abs)
        {
            string root = ProjectRoot().Replace('\\', '/') + "/";
            abs = abs.Replace('\\', '/');
            return abs.StartsWith(root) ? abs.Substring(root.Length) : null;
        }
    }
}
