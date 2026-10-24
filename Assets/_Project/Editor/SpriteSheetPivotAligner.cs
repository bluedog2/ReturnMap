using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace ReTrap.EditorTools
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  SpriteSheetPivotAligner — 애니메이션 클립 프레임의 스프라이트 피벗 자동 정렬
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 메뉴 <b>ReTrap → Art → 스프라이트 피벗 정렬 (드라이런 / 적용)</b>
    /// (배치모드: <c>-executeMethod ReTrap.EditorTools.SpriteSheetPivotAligner.RunDry</c> / <c>RunApply</c>).
    /// <para>생성형 스프라이트 시트는 프레임마다 캐릭터 위치가 흔들려 재생 시 몸이 떨린다.
    /// 클립 <b>키프레임이 실제로 참조하는 스프라이트</b>만 대상으로 피벗을 재계산해 흔들림을 줄인다.</para>
    /// <list type="bullet">
    ///   <item>X: 절대 위치 이동 금지 — 캐릭터(주인공/적 종류)별 Idle 프레임에서 오프셋 C = 평균(측정X − 현재 pivotX) 를 구하고
    ///         새 pivotX = 측정X − C. 측정X: 주인공=왕관 금색 픽셀 무게중심(부족하면 몸통 폴백), 적=몸통 띠(높이 30~70%) 무게중심.
    ///         프레임별(Idle·Run·점프·착지) 또는 프레임0 하나로 클립 공통(Hit·Death·Dash)</item>
    ///   <item>Y: 지상 클립(Idle·Run·Land·Hit·Death)만 클립 상수 — 프레임0 발 높이가 Idle 프레임0 과 같도록.
    ///         프레임별 Y 정렬은 하지 않으며 공중 클립 Y 는 건드리지 않음</item>
    ///   <item>같은 스프라이트(텍스처 경로+이름)가 여러 클립에 나오면 우선순위상 먼저 처리한 클립 결과만 사용</item>
    /// </list>
    /// <b>멱등</b>: 절대값을 픽셀에서 다시 계산하며, 기존과 1px 이하 차이면 쓰지 않는다.
    /// spriteID/internalID 는 그대로 보존(애니메이션 참조 유지). 결과는 <c>Logs/pivot-align.txt</c>.
    /// </summary>
    public static class SpriteSheetPivotAligner
    {
        private const string PlayerController = "Assets/_Project/ResourcceEX/Animations/Lord_Controller.controller";
        private const string EnemyClipDir     = "Assets/_Project/ResourcceEX/Animations/Enemies";

        private const float BodyBandLo   = 0.30f; // 몸통 띠: 불투명 높이 하한 비율
        private const float BodyBandHi   = 0.70f; // 몸통 띠: 불투명 높이 상한 비율
        private const int   MinGoldPixels = 20;   // 왕관 금색 픽셀 최소 개수 (미만이면 몸통 폴백)
        private const byte  AlphaCut     = 128;   // 이 값 초과를 불투명으로 간주
        private const float SkipDeltaPx  = 1f;    // 이 이하 변경은 쓰지 않음
        private const float EnemyTopXTol = 4f;    // 적: 프레임 간 측정X 편차가 이 이하면 X 정렬 건너뜀

        // 클립 이름 접미사 → 규칙. 긴 이름이 먼저 매칭되도록 정렬해서 사용.
        private static readonly string[] PerFrameXSuffix = { "Jump_Start", "Jump_Loop", "Fall_Loop", "Idle", "Run", "Land" };
        private static readonly string[] SingleXSuffix   = { "Hit", "Death", "Dash" };
        // 공유 스프라이트 처리 우선순위 (앞일수록 먼저)
        private static readonly string[] PrioritySuffix  = { "Idle", "Run", "Jump_Start", "Jump_Loop", "Fall_Loop", "Land", "Hit", "Death", "Dash" };
        private static readonly string[] GroundSuffix    = { "Idle", "Run", "Land", "Hit", "Death" };

        // ── 데이터 ────────────────────────────────────────────────────────────

        private class Frame
        {
            public string TexPath;
            public string Name;     // 스프라이트 이름 (텍스처 내 유일)
            public RectInt Rect;    // 텍스처 내 셀 (하단 원점)
            public float MeasX;     // 셀 내 px (측정 기준 X)
            public string Basis;    // 사용한 기준: 왕관 / 몸통 / 폴백(몸통)
            public bool  Shared;    // 다른 클립이 먼저 처리한 스프라이트
            public float FootY;    // 셀 내 px
            public float OldPx, OldPy;
            public float NewPx, NewPy;
            public bool  SetX, SetY;
        }

        private class ClipInfo
        {
            public string AssetPath;
            public string Name;
            public string Group;       // 기준 Idle 을 찾는 그룹 (플레이어 / 적 종류)
            public string Suffix;
            public bool   PerFrameX;
            public bool   Ground;
            public bool   IsEnemy;
            public List<Frame> Frames = new List<Frame>();
        }

        private static readonly Dictionary<string, Color32[]> PixelCache = new Dictionary<string, Color32[]>();
        private static readonly Dictionary<string, Vector2Int> SizeCache = new Dictionary<string, Vector2Int>();

        // ── 진입점 ────────────────────────────────────────────────────────────

        [MenuItem("ReTrap/Art/스프라이트 피벗 정렬 (드라이런)", false, 120)]
        public static void RunDry() => Run(false);

        [MenuItem("ReTrap/Art/스프라이트 피벗 정렬 (적용)", false, 121)]
        public static void RunApply() => Run(true);

        private static void Run(bool apply)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[SpriteSheetPivotAligner] 플레이 모드에서는 실행할 수 없습니다.");
                return;
            }

            PixelCache.Clear();
            SizeCache.Clear();

            var log   = new StringBuilder();
            log.AppendLine($"# 스프라이트 피벗 정렬 — {(apply ? "적용" : "드라이런")}  {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            var clips = CollectClips(log);
            clips = clips.OrderBy(c => Array.IndexOf(PrioritySuffix, c.Suffix)).ToList(); // 안정 정렬
            foreach (var c in clips) Measure(c, log);
            ComputeTargets(clips, log);

            // (텍스처 경로 → 스프라이트 이름 → 변경할 px) 로 모은다
            var pending = new Dictionary<string, Dictionary<string, Vector2>>();
            var changedMask = new Dictionary<string, Dictionary<string, (bool x, bool y)>>();
            foreach (var c in clips)
            {
                foreach (var f in c.Frames)
                {
                    if (!f.SetX && !f.SetY) continue;
                    if (!pending.TryGetValue(f.TexPath, out var d)) pending[f.TexPath] = d = new Dictionary<string, Vector2>();
                    if (!changedMask.TryGetValue(f.TexPath, out var m)) changedMask[f.TexPath] = m = new Dictionary<string, (bool, bool)>();
                    d.TryGetValue(f.Name, out var cur);
                    m.TryGetValue(f.Name, out var mask);
                    // 같은 스프라이트를 여러 번 참조해도 값은 동일 — 마지막 값 사용
                    d[f.Name] = new Vector2(f.SetX ? f.NewPx : cur.x, f.SetY ? f.NewPy : cur.y);
                    m[f.Name] = (mask.x || f.SetX, mask.y || f.SetY);
                }
            }

            int spriteCount = pending.Values.Sum(v => v.Count);
            log.AppendLine();
            log.AppendLine($"== 요약: 변경 대상 텍스처 {pending.Count}개, 스프라이트 {spriteCount}개 ==");

            if (apply)
            {
                AssetDatabase.StartAssetEditing();
                try
                {
                    foreach (var kv in pending)
                        ApplyToTexture(kv.Key, kv.Value, changedMask[kv.Key], log);
                }
                finally { AssetDatabase.StopAssetEditing(); }
                AssetDatabase.SaveAssets();
            }

            string logPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "pivot-align.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath));
            File.WriteAllText(logPath, log.ToString(), new UTF8Encoding(false));
            Debug.Log($"[SpriteSheetPivotAligner] {(apply ? "적용" : "드라이런")} 완료 — 텍스처 {pending.Count}개 / 스프라이트 {spriteCount}개. 로그: Logs/pivot-align.txt");
        }

        // ── 1. 클립 수집 ──────────────────────────────────────────────────────

        private static List<ClipInfo> CollectClips(StringBuilder log)
        {
            var result = new List<ClipInfo>();

            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerController);
            if (ctrl != null)
            {
                foreach (var clip in ctrl.animationClips.Distinct())
                    AddClip(result, clip, "Lord", false, log);
            }
            else log.AppendLine($"[경고] 컨트롤러를 찾지 못했습니다: {PlayerController}");

            foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { EnemyClipDir }))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid));
                if (clip == null) continue;
                AddClip(result, clip, null, true, log);
            }
            return result;
        }

        private static void AddClip(List<ClipInfo> list, AnimationClip clip, string group, bool enemy, StringBuilder log)
        {
            string suffix = MatchSuffix(clip.name, PerFrameXSuffix) ?? MatchSuffix(clip.name, SingleXSuffix);
            if (suffix == null) { log.AppendLine($"[건너뜀] 규칙 없는 클립: {clip.name}"); return; }

            var info = new ClipInfo
            {
                AssetPath = AssetDatabase.GetAssetPath(clip),
                Name      = clip.name,
                Suffix    = suffix,
                PerFrameX = Array.IndexOf(PerFrameXSuffix, suffix) >= 0,
                Ground    = Array.IndexOf(GroundSuffix, suffix) >= 0,
                IsEnemy   = enemy,
                Group     = group ?? clip.name.Substring(0, clip.name.Length - suffix.Length).TrimEnd('_'),
            };

            foreach (var b in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                if (b.type != typeof(SpriteRenderer)) continue;
                var keys = AnimationUtility.GetObjectReferenceCurve(clip, b);
                foreach (var k in keys)
                {
                    var sp = k.value as Sprite;
                    if (sp == null) continue;
                    string tex = AssetDatabase.GetAssetPath(sp.texture);
                    info.Frames.Add(new Frame
                    {
                        TexPath = tex,
                        Name    = sp.name,
                        Rect    = new RectInt(Mathf.RoundToInt(sp.rect.x), Mathf.RoundToInt(sp.rect.y),
                                              Mathf.RoundToInt(sp.rect.width), Mathf.RoundToInt(sp.rect.height)),
                        OldPx   = sp.pivot.x,
                        OldPy   = sp.pivot.y,
                    });
                }
                break; // 스프라이트 커브는 하나
            }

            if (info.Frames.Count == 0) { log.AppendLine($"[건너뜀] 스프라이트 키프레임 없음: {clip.name}"); return; }
            list.Add(info);
        }

        private static string MatchSuffix(string name, string[] suffixes)
        {
            foreach (string s in suffixes.OrderByDescending(x => x.Length))
                if (name.EndsWith(s, StringComparison.Ordinal)) return s;
            return null;
        }

        // ── 2. 측정 ───────────────────────────────────────────────────────────

        private static void Measure(ClipInfo c, StringBuilder log)
        {
            foreach (var f in c.Frames)
            {
                if (!TryGetPixels(f.TexPath, out var px, out var size))
                {
                    log.AppendLine($"[경고] 텍스처 읽기 실패: {f.TexPath}");
                    f.MeasX = float.NaN;
                    continue;
                }
                MeasureCell(px, size, f, !c.IsEnemy);
            }
        }

        private static bool TryGetPixels(string assetPath, out Color32[] px, out Vector2Int size)
        {
            if (PixelCache.TryGetValue(assetPath, out px)) { size = SizeCache[assetPath]; return px != null; }

            size = default;
            string full = Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);
            if (!File.Exists(full)) { PixelCache[assetPath] = null; return false; }

            // 원본 PNG 직접 디코드 — 압축 텍스처 isReadable 문제 회피
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!tex.LoadImage(File.ReadAllBytes(full))) { PixelCache[assetPath] = null; return false; }
                px   = tex.GetPixels32();
                size = new Vector2Int(tex.width, tex.height);
                PixelCache[assetPath] = px;
                SizeCache[assetPath]  = size;
                return true;
            }
            finally { UnityEngine.Object.DestroyImmediate(tex); }
        }

        /// <summary>측정X(주인공=왕관 금색 / 적=몸통 띠)와 최하단 불투명 행을 구한다.</summary>
        private static void MeasureCell(Color32[] px, Vector2Int size, Frame f, bool player)
        {
            RectInt r = f.Rect;
            int minRow = int.MaxValue, maxRow = -1;
            for (int y = 0; y < r.height; y++)
            {
                int ty = r.y + y;
                for (int x = 0; x < r.width; x++)
                {
                    if (px[ty * size.x + r.x + x].a <= AlphaCut) continue;
                    if (y < minRow) minRow = y;
                    if (y > maxRow) maxRow = y;
                    break;
                }
            }
            if (maxRow < 0) { f.MeasX = float.NaN; return; }
            f.FootY = minRow;

            if (player)
            {
                double gs = 0; int gc = 0;
                for (int y = 0; y < r.height; y++)
                {
                    int ty = r.y + y;
                    for (int x = 0; x < r.width; x++)
                    {
                        Color32 c = px[ty * size.x + r.x + x];
                        if (c.a > AlphaCut && c.r > 200 && c.g > 150 && c.b < 90) { gs += x + 0.5; gc++; }
                    }
                }
                if (gc >= MinGoldPixels) { f.MeasX = (float)(gs / gc); f.Basis = "왕관"; return; }
                f.Basis = "폴백(몸통)";
            }
            else f.Basis = "몸통";

            int height = maxRow - minRow + 1;
            int lo = minRow + Mathf.FloorToInt(height * BodyBandLo);
            int hi = Mathf.Min(maxRow, minRow + Mathf.CeilToInt(height * BodyBandHi) - 1);
            double sum = 0; int cnt = 0;
            for (int y = lo; y <= hi; y++)
            {
                int ty = r.y + y;
                for (int x = 0; x < r.width; x++)
                {
                    if (px[ty * size.x + r.x + x].a <= AlphaCut) continue;
                    sum += x + 0.5; cnt++;
                }
            }
            f.MeasX = cnt > 0 ? (float)(sum / cnt) : float.NaN;
        }

        // ── 3. 목표 피벗 계산 ────────────────────────────────────────────────

        private static void ComputeTargets(List<ClipInfo> clips, StringBuilder log)
        {
            // 공유 스프라이트: (텍스처 경로, 이름) 쌍을 처음 처리한 클립만 사용
            var owner = new Dictionary<(string, string), ClipInfo>();
            foreach (var c in clips)
                foreach (var f in c.Frames)
                {
                    var key = (f.TexPath, f.Name);
                    if (!owner.TryGetValue(key, out var o)) owner[key] = c;
                    else if (o != c) f.Shared = true;
                }

            // 그룹별 기준: Idle 프레임0 의 (발 높이 - 현재 피벗 Y), 오프셋 C = 평균(측정X - 현재 pivotX)
            var refRel  = new Dictionary<string, float>();
            var offsetC = new Dictionary<string, float>();
            foreach (var c in clips)
            {
                if (c.Suffix != "Idle") continue;
                var ok = c.Frames.Where(f => !f.Shared && !float.IsNaN(f.MeasX)).ToList();
                if (ok.Count == 0) continue;
                refRel[c.Group]  = ok[0].FootY - ok[0].OldPy;
                offsetC[c.Group] = ok.Average(f => f.MeasX - f.OldPx);
            }
            log.AppendLine();
            foreach (var kv in offsetC) log.AppendLine($"[오프셋 C] {kv.Key}: {kv.Value:F2}px (Idle 프레임 평균 측정X − 현재 pivotX)");

            foreach (var c in clips)
            {
                log.AppendLine();
                log.AppendLine($"── {c.Name}  [{(c.PerFrameX ? "X:프레임별" : "X:클립공통")}{(c.Ground ? ", Y:지상상수" : "")}]  ({c.AssetPath})");

                var valid = c.Frames.Where(f => !f.Shared && !float.IsNaN(f.MeasX)).ToList();
                if (valid.Count == 0)
                {
                    log.AppendLine(c.Frames.All(f => f.Shared) ? "  전 프레임 공유 — 건너뜀" : "  측정 가능한 프레임 없음");
                    foreach (var f in c.Frames) { f.NewPx = f.OldPx; f.NewPy = f.OldPy; }
                    LogFrames(c, log);
                    continue;
                }

                bool haveC = offsetC.TryGetValue(c.Group, out float C);
                float beforeDev = Range(valid.Select(f => f.MeasX - f.OldPx));

                bool doX = haveC;
                if (!haveC) log.AppendLine($"  X 건너뜀: 그룹 '{c.Group}' 의 Idle 기준(C)이 없음");
                else if (c.IsEnemy && beforeDev <= EnemyTopXTol)
                {
                    doX = false;
                    log.AppendLine($"  X 건너뜀: 측정X 편차 {beforeDev:F1}px ≤ {EnemyTopXTol}px (적)");
                }

                float clipMeasX = valid[0].MeasX; // 프레임0
                bool  haveRef   = refRel.TryGetValue(c.Group, out float rel);
                if (c.Ground && !haveRef) log.AppendLine($"  Y 건너뜀: 그룹 '{c.Group}' 의 Idle 기준이 없음");

                // Idle: 모든 프레임이 프레임0 의 현재 피벗 Y 를 공유
                float idleY = c.Suffix == "Idle" ? valid[0].OldPy : 0f;

                foreach (var f in c.Frames)
                {
                    f.NewPx = f.OldPx; f.NewPy = f.OldPy;
                    if (f.Shared || float.IsNaN(f.MeasX)) continue;

                    if (doX)
                    {
                        float tx = (c.PerFrameX ? f.MeasX : clipMeasX) - C;
                        if (Mathf.Abs(tx - f.OldPx) > SkipDeltaPx) { f.NewPx = tx; f.SetX = true; }
                    }
                    if (c.Ground && haveRef)
                    {
                        float ty = c.Suffix == "Idle" ? idleY : valid[0].FootY - rel;
                        if (Mathf.Abs(ty - f.OldPy) > SkipDeltaPx) { f.NewPy = ty; f.SetY = true; }
                    }
                }

                // 예측 편차 (프레임 간 측정 위치 − 피벗 범위)
                float afterDev = Range(valid.Select(f => f.MeasX - f.NewPx));
                log.AppendLine($"  측정X 편차(전→후 예측): {beforeDev:F1}px → {afterDev:F1}px" + (haveC ? $"  (C={C:F2})" : ""));
                LogFrames(c, log);
            }
        }

        private static void LogFrames(ClipInfo c, StringBuilder log)
        {
            for (int i = 0; i < c.Frames.Count; i++)
            {
                var f = c.Frames[i];
                if (f.Shared) { log.AppendLine($"  [{i}] {f.Name}: 공유 — 건너뜀"); continue; }
                if (float.IsNaN(f.MeasX)) { log.AppendLine($"  [{i}] {f.Name}: 측정 실패"); continue; }
                log.AppendLine($"  [{i}] {f.Name}  기준={f.Basis} 측정X={f.MeasX:F1} footY={f.FootY:F0}  " +
                               $"pivot(px) ({f.OldPx:F1},{f.OldPy:F1}) → ({f.NewPx:F1},{f.NewPy:F1})" +
                               $"{(f.SetX || f.SetY ? "  *변경*" : "")}");
            }
        }

        private static float Range(IEnumerable<float> v)
        {
            float min = float.MaxValue, max = float.MinValue;
            foreach (float x in v) { if (x < min) min = x; if (x > max) max = x; }
            return max < min ? 0f : max - min;
        }

        // ── 4. 적용 ───────────────────────────────────────────────────────────

        private static void ApplyToTexture(string texPath, Dictionary<string, Vector2> updates,
                                           Dictionary<string, (bool x, bool y)> mask, StringBuilder log)
        {
            var ti = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (ti == null) { log.AppendLine($"[경고] 임포터 없음: {texPath}"); return; }

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var dp = factory.GetSpriteEditorDataProviderFromObject(ti);
            dp.InitSpriteEditorDataProvider();

            var rects = dp.GetSpriteRects();
            // spriteID 와 internalID(이름-fileID 쌍)는 그대로 유지해 애니메이션 참조를 보존
            var nameProvider = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
            var pairs = nameProvider != null ? nameProvider.GetNameFileIdPairs().ToList() : null;

            int n = 0;
            foreach (var sr in rects)
            {
                if (!updates.TryGetValue(sr.name, out Vector2 px)) continue;
                var m = mask[sr.name];

                Vector2 p = sr.pivot;
                if (m.x) p.x = px.x / sr.rect.width;
                if (m.y) p.y = px.y / sr.rect.height;
                sr.alignment = SpriteAlignment.Custom;
                sr.pivot     = p;
                n++;
            }

            dp.SetSpriteRects(rects);
            if (nameProvider != null && pairs != null) nameProvider.SetNameFileIdPairs(pairs);
            dp.Apply();
            ti.SaveAndReimport();
            log.AppendLine($"[적용] {texPath} — 스프라이트 {n}개 피벗 갱신");
        }
    }
}
