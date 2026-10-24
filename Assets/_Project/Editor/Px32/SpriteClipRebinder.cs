using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ReTrap.EditorTools
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  SpriteClipRebinder — 기존 애니메이션 클립을 32px 새 시트 프레임으로 재배선
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 메뉴 <b>ReTrap → Art → Px32 클립 재배선 (드라이런 / 적용)</b>
    /// (배치모드: <c>-executeMethod ReTrap.EditorTools.SpriteClipRebinder.RunDry</c> / <c>RunApply</c>).
    /// <para>기존 .anim 을 <b>같은 에셋(GUID 유지)</b> 그대로 수정한다: <c>SpriteRenderer.m_Sprite</c>
    /// 오브젝트 레퍼런스 커브를 새 시트 프레임(i/fps)으로 교체하고 frameRate·loopTime 을 규격값으로 맞춘다.</para>
    /// <list type="bullet">
    ///   <item>새 시트(<c>Sprites/Px32/...</c>)가 없거나 프레임 수가 규격과 다르면 그 클립은 건너뛴다</item>
    ///   <item>m_Sprite 외 커브(특히 localScale)가 있으면 경고만 남기고 수정하지 않는다</item>
    /// </list>
    /// 결과는 <c>Logs/px32-rebind.txt</c>. 멱등 — 이미 새 프레임이면 같은 결과를 다시 쓴다.
    /// </summary>
    public static class SpriteClipRebinder
    {
        private const string AnimRoot  = "Assets/_Project/ResourcceEX/Animations";
        private const string EnemyRoot = AnimRoot + "/Enemies";
        private const string SheetRoot = "Assets/_Project/ResourcceEX/Sprites/Px32";

        /// <summary>매핑 한 줄: 클립 경로 → 시트 경로 + 규격(프레임 수·fps·loop).</summary>
        private struct Entry
        {
            public string ClipPath;
            public string SheetPath;
            public int    Frames;
            public float  Fps;
            public bool   Loop;
        }

        // 상태별 규격 (계획서 2.2). 주인공 Idle 6@8 · Run 8@12 · JumpStart 2@12 · Fall 2@8 루프 · Land 3@12 · Dash 4@20 · Hit 2@10 · Death 6@10
        private static List<Entry> BuildTable()
        {
            var t = new List<Entry>();
            void Hero(string clip, string sheet, int n, float fps, bool loop) => t.Add(new Entry
            {
                ClipPath  = $"{AnimRoot}/{clip}.anim",
                SheetPath = $"{SheetRoot}/Player/{sheet}.png",
                Frames = n, Fps = fps, Loop = loop,
            });

            Hero("Idle",       "Hero_Idle",      6, 8,  true);
            Hero("Run",        "Hero_Run",       8, 12, true);
            Hero("Jump_Start", "Hero_JumpStart", 2, 12, false);
            Hero("Jump_Loop",  "Hero_Fall",      2, 8,  true);   // Fall 공유
            Hero("Fall_Loop",  "Hero_Fall",      2, 8,  true);
            Hero("Land",       "Hero_Land",      3, 12, false);
            Hero("Dash",       "Hero_Dash",      4, 20, false);  // 대시 0.2s 와 길이 일치
            Hero("Lord_Hit",   "Hero_Hit",       2, 10, false);
            Hero("Death",      "Hero_Death",     6, 10, false);

            // 적: 5종 × 4상태 — 상태 규격은 종 공통 (Idle 4@6 · Run 6@10 · Hit 2@10 · Death 6@10)
            string[] species = { "Goblin_Raider", "Imp_Flyer", "Skeleton_Archer", "Elite_Knight", "Orc_Brute" };
            var states = new (string name, int n, float fps, bool loop)[]
            {
                ("Idle", 4, 6, true), ("Run", 6, 10, true), ("Hit", 2, 10, false), ("Death", 6, 10, false),
            };
            foreach (string sp in species)
                foreach (var s in states)
                    t.Add(new Entry
                    {
                        ClipPath  = $"{EnemyRoot}/Enemy_{sp}_{s.name}.anim",
                        SheetPath = $"{SheetRoot}/Enemies/{sp}_{s.name}.png",
                        Frames = s.n, Fps = s.fps, Loop = s.loop,
                    });
            return t;
        }

        // ── 진입점 ────────────────────────────────────────────────────────────

        [MenuItem("ReTrap/Art/Px32 클립 재배선 (드라이런)", false, 130)]
        public static void RunDry() => Run(false);

        [MenuItem("ReTrap/Art/Px32 클립 재배선 (적용)", false, 131)]
        public static void RunApply() => Run(true);

        /// <summary>주인공 클립 9개만 적용한다 (Px32HeroWiring 용). 전부 재배선되면 true, 하나라도 건너뛰면 false.</summary>
        public static bool RunApplyHero() => Run(true, heroOnly: true);

        private static bool Run(bool apply, bool heroOnly = false)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[SpriteClipRebinder] 플레이 모드에서는 실행할 수 없습니다.");
                return false;
            }

            var log = new StringBuilder();
            log.AppendLine($"# Px32 클립 재배선 — {(apply ? "적용" : "드라이런")}  {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            int done = 0, skipped = 0;
            if (apply) AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var e in BuildTable())
                {
                    if (heroOnly && e.ClipPath.StartsWith(EnemyRoot, StringComparison.Ordinal)) continue;
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(e.ClipPath);
                    if (clip == null)
                    {
                        log.AppendLine($"[건너뜀] 클립 없음: {e.ClipPath}");
                        skipped++;
                        continue;
                    }

                    var frames = LoadFrames(e.SheetPath);
                    if (frames == null)
                    {
                        log.AppendLine($"[건너뜀] {clip.name}: 새 시트 없음 — {e.SheetPath}");
                        skipped++;
                        continue;
                    }
                    if (frames.Count != e.Frames)
                    {
                        log.AppendLine($"[건너뜀] {clip.name}: 프레임 수 불일치 (시트 {frames.Count} ≠ 규격 {e.Frames}) — {e.SheetPath}");
                        skipped++;
                        continue;
                    }

                    log.AppendLine(Rebind(clip, frames, e.Fps, e.Loop, !apply));
                    done++;
                }
            }
            finally { if (apply) AssetDatabase.StopAssetEditing(); }
            if (apply) AssetDatabase.SaveAssets();

            log.AppendLine();
            log.AppendLine($"== 요약: 재배선 {(apply ? "적용" : "대상")} {done}개, 건너뜀 {skipped}개 ==");

            string logPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "px32-rebind.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath));
            File.WriteAllText(logPath, log.ToString(), new UTF8Encoding(false));
            Debug.Log($"[SpriteClipRebinder] {(apply ? "적용" : "드라이런")} 완료 — 재배선 {done} / 건너뜀 {skipped}. 로그: Logs/px32-rebind.txt");
            return skipped == 0;
        }

        // ── 재배선 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 클립의 m_Sprite 커브를 <paramref name="newFrames"/> 로 교체한다 (키 시간 = i/fps).
        /// <paramref name="dryRun"/> 이면 클립을 수정하지 않고 결과 설명만 돌려준다.
        /// </summary>
        /// <returns>클립별 이전/이후 프레임 수·fps·경고를 담은 로그 문자열.</returns>
        public static string Rebind(AnimationClip clip, IList<Sprite> newFrames, float fps, bool loop, bool dryRun)
        {
            var sb = new StringBuilder();
            string path = AssetDatabase.GetAssetPath(clip);

            // 기존 m_Sprite 바인딩 찾기 (경로 보존 — 예: "Visual")
            EditorCurveBinding? spriteBinding = null;
            int oldCount = 0;
            foreach (var b in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                if (b.type == typeof(SpriteRenderer) && b.propertyName == "m_Sprite")
                {
                    spriteBinding = b;
                    var keys = AnimationUtility.GetObjectReferenceCurve(clip, b);
                    oldCount = keys != null ? keys.Length : 0;
                    break;
                }
            }

            float oldFps  = clip.frameRate;
            bool  oldLoop = AnimationUtility.GetAnimationClipSettings(clip).loopTime;

            sb.AppendLine($"[{(dryRun ? "드라이런" : "적용")}] {clip.name}  ({path})");
            sb.AppendLine($"  프레임 {oldCount} → {newFrames.Count}, fps {oldFps:F0} → {fps:F0}, loop {oldLoop} → {loop}");

            // m_Sprite 외 커브 경고 (수정 안 함)
            foreach (var b in AnimationUtility.GetCurveBindings(clip))
                sb.AppendLine($"  [경고] 스프라이트 외 커브 있음: {b.path}/{b.type.Name}.{b.propertyName}" +
                              (b.propertyName.StartsWith("m_LocalScale", StringComparison.Ordinal) ? " (localScale — 확인 필요)" : ""));
            foreach (var b in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                if (!(b.type == typeof(SpriteRenderer) && b.propertyName == "m_Sprite"))
                    sb.AppendLine($"  [경고] 스프라이트 외 레퍼런스 커브 있음: {b.path}/{b.type.Name}.{b.propertyName}");

            if (spriteBinding == null)
            {
                sb.AppendLine("  [건너뜀] SpriteRenderer.m_Sprite 커브 없음");
                return sb.ToString().TrimEnd();
            }
            if (dryRun) return sb.ToString().TrimEnd();

            // frameRate 를 먼저 설정해야 키 시간이 프레임 격자에 맞는다
            clip.frameRate = fps;
            var newKeys = new ObjectReferenceKeyframe[newFrames.Count];
            for (int i = 0; i < newKeys.Length; i++)
                newKeys[i] = new ObjectReferenceKeyframe { time = i / fps, value = newFrames[i] };
            AnimationUtility.SetObjectReferenceCurve(clip, spriteBinding.Value, newKeys);

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            settings.startTime = 0f;
            settings.stopTime = newFrames.Count / fps; // 마지막 프레임을 1프레임 유지(기존 클립과 동일)
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            EditorUtility.SetDirty(clip);
            return sb.ToString().TrimEnd();
        }

        // ── 시트 로드 ─────────────────────────────────────────────────────────

        /// <summary>시트 PNG 의 스프라이트를 이름 끝 번호(_i) 순으로 돌려준다. 시트/스프라이트가 없으면 null.</summary>
        private static List<Sprite> LoadFrames(string sheetPath)
        {
            string full = Path.Combine(Directory.GetParent(Application.dataPath).FullName, sheetPath);
            if (!File.Exists(full)) return null;

            var sprites = AssetDatabase.LoadAllAssetsAtPath(sheetPath).OfType<Sprite>().ToList();
            if (sprites.Count == 0) return null;
            return sprites.OrderBy(SpriteIndex).ThenBy(s => s.name, StringComparer.Ordinal).ToList();
        }

        private static int SpriteIndex(Sprite s)
        {
            int u = s.name.LastIndexOf('_');
            return u >= 0 && int.TryParse(s.name.Substring(u + 1), out int i) ? i : int.MaxValue;
        }
    }
}
