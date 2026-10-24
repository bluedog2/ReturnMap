using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ReTrap.EditorTools
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  PixelArtSheetValidator — 32px 픽셀아트 시트 수용 검사 (계획서 2.6)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Color32[] + 가로/세로만 받는 순수 로직 검사기(Unity API 는 Color32 만 사용).
    /// 픽셀 배열은 Unity 관례대로 <b>아래→위</b> 순서(행 0 = 최하단)입니다.
    /// 항목: grid(격자·가짜 픽셀) / dimensions / alpha / palette / baseline / drift / clip.
    /// </summary>
    public static class PixelArtSheetValidator
    {
        public const string ItemGrid = "grid", ItemDims = "dimensions", ItemAlpha = "alpha",
                            ItemPalette = "palette", ItemBaseline = "baseline", ItemDrift = "drift", ItemClip = "clip";

        private const float FakePixelThreshold = 0.02f; // 재업스케일 오차 허용치 2%
        private const float FakeCandidateLimit = 0.08f; // k=1 추정 시 "격자 근접" 으로 볼 오차 상한
        private const int   MaxK               = 16;
        private const float PaletteDistance    = 12f;
        private const float DriftTolerance     = 2f;

        // ── 결과 ───────────────────────────────────────────────────────────────

        public sealed class Item
        {
            public string       name;
            public bool         pass = true;
            public List<string> reasons = new List<string>();
            public void Fail(string reason) { pass = false; reasons.Add(reason); }
        }

        public sealed class Result
        {
            public List<Item> items = new List<Item>();
            /// <summary>추정된 업스케일 배율(격자 검출을 안 했으면 1).</summary>
            public int  k = 1;
            /// <summary>다운샘플 이후(원본 아트 픽셀 기준) 크기.</summary>
            public int  width, height;
            public bool Passed { get { foreach (var i in items) if (!i.pass) return false; return true; } }
            public Item Get(string name) { foreach (var i in items) if (i.name == name) return i; return null; }

            public string Summary()
            {
                var sb = new StringBuilder();
                foreach (var i in items)
                {
                    sb.Append(i.pass ? "PASS " : "FAIL ").Append(i.name);
                    if (i.reasons.Count > 0) sb.Append(": ").Append(string.Join("; ", i.reasons));
                    sb.Append('\n');
                }
                return sb.ToString().TrimEnd();
            }
        }

        /// <summary>검사 옵션. cellW/cellH/frames 가 0 이면 셀=높이 정사각·프레임=가로/세로 로 추정.</summary>
        public struct Options
        {
            public string  category;     // null 이면 시트로 간주(추정 모드)
            public string  baseName;
            public int     cellW, cellH, frames;
            public bool    airborne;     // 기준선 드리프트 예외
            public bool    death;        // 수평·높이 드리프트 예외
            public int     maxColors;    // 0 이면 16
            public bool    detectGrid;   // true: 원본이 k배 업스케일. false: 이미 1배(가져온 결과)
            public Color32[] palette;
        }

        /// <summary>카테고리·파일명으로 Options 를 구성(Px32Rules 규격표 사용).</summary>
        public static Options OptionsFor(string category, string baseName, Color32[] palette, bool detectGrid)
        {
            var o = new Options
            {
                category = category, baseName = baseName, palette = palette, detectGrid = detectGrid,
                airborne = Px32Rules.IsAirborne(baseName), death = Px32Rules.IsDeath(baseName),
                maxColors = (category == "Tiles" || category == "Backgrounds") ? 24 : 16,
            };
            var spec = Px32Rules.GetSheetSpec(category, baseName);
            if (spec != null) { o.cellW = spec.cellW; o.cellH = spec.cellH; o.frames = spec.frames; }
            return o;
        }

        // ── 진입점 ─────────────────────────────────────────────────────────────

        public static Result Validate(Color32[] px, int w, int h, Options o)
        {
            var res = new Result();
            var grid = new Item { name = ItemGrid };
            res.items.Add(grid);

            // 1) 격자 검출 + 다운샘플
            int k = 1;
            if (o.detectGrid)
            {
                // (a) 규격이 있고 치수가 정수배로 맞으면 k 확정. k=1 이면 격자 휴리스틱 생략.
                bool decided = false;
                if (o.cellW > 0 && o.cellH > 0 && o.frames > 0)
                {
                    int unit = o.cellW * o.frames;
                    if (w % unit == 0)
                    {
                        int kk = w / unit;
                        if (kk >= 1 && h == kk * o.cellH) { k = kk; decided = true; }
                    }
                }

                if (!decided)
                {
                    // (b) 런 길이 GCD>1 이면 그 값
                    k = EstimateK(px, w, h);
                    if (k <= 1)
                    {
                        // 노이즈로 GCD 가 무너진 경우: 오차 ≤2% 인 가장 큰 c
                        k = 1;
                        int bestC = 0; float bestErr = float.MaxValue;
                        for (int c = MaxK; c >= 2; c--)
                        {
                            if (w % c != 0 || h % c != 0) continue;
                            float e = UpscaleError(px, w, h, c);
                            if (e <= FakePixelThreshold) { k = c; bestC = 0; break; }
                            if (e < bestErr) { bestErr = e; bestC = c; }
                        }
                        // (c) 없고 최소 오차 ≤8% 면 그 c 로 grid FAIL (다운샘플은 진행)
                        if (k == 1 && bestC > 1 && bestErr <= FakeCandidateLimit)
                        {
                            k = bestC;
                            grid.Fail($"가짜 픽셀: k={bestC} 격자에 근접하나 오차 {bestErr * 100f:F1}% > 2% (블록 경계 노이즈·반투명 가장자리 의심)");
                        }
                    }
                }

                if (k > 1 && grid.pass)
                {
                    float err = UpscaleError(px, w, h, k);
                    if (err > FakePixelThreshold)
                        grid.Fail($"가짜 픽셀: k={k} 재업스케일 오차 {err * 100f:F1}% > 2%");
                }
            }
            res.k = k;
            int dw = w, dh = h;
            Color32[] a = px;
            if (k > 1 && w % k == 0 && h % k == 0) a = Downsample(px, w, h, k, out dw, out dh);
            else res.k = 1;
            res.width = dw; res.height = dh;

            // 2) 레이아웃 결정
            bool sheet = o.category == null || Px32Rules.IsSheetCategory(o.category);
            var dims = new Item { name = ItemDims };
            res.items.Add(dims);
            int cw, ch, frames;
            if (o.cellW > 0 && o.cellH > 0 && o.frames > 0)
            {
                cw = o.cellW; ch = o.cellH; frames = o.frames;
                if (dw != cw * frames || dh != ch)
                    dims.Fail($"치수 {dw}×{dh} ≠ 규격 {cw * frames}×{ch} (셀 {cw}×{ch} × {frames}프레임)");
            }
            else if (o.category == "Backgrounds")
            {
                cw = dw; ch = dh; frames = 1;
                if (dw != 640 || (dh != 360 && dh != 256))
                    dims.Fail($"배경 치수 {dw}×{dh} ≠ 640×360(원경) / 640×256(근경)");
            }
            else if (sheet)
            {
                ch = dh; cw = dh; frames = dh > 0 ? dw / dh : 0;
                if (dh <= 0 || dw % dh != 0 || frames < 1)
                { dims.Fail($"추정 실패: {dw}×{dh} 는 셀=높이 정사각 × 정수 프레임이 아님"); frames = 1; cw = dw; ch = dh; }
            }
            else { cw = dw; ch = dh; frames = 1; }
            if (dims.pass == false && (dw != cw * frames || dh != ch)) { cw = dw; ch = dh; frames = 1; }

            // 3) 알파 0/255
            var alpha = new Item { name = ItemAlpha };
            res.items.Add(alpha);
            int badAlpha = 0;
            for (int i = 0; i < a.Length; i++) if (a[i].a != 0 && a[i].a != 255) badAlpha++;
            if (badAlpha > 0) alpha.Fail($"알파 0/255 외 값 {badAlpha}개");

            // 4) 팔레트·색 수
            var pal = new Item { name = ItemPalette };
            res.items.Add(pal);
            CheckPalette(a, dw, dh, cw, ch, frames, o, pal);

            if (sheet)
            {
                var baseline = new Item { name = ItemBaseline };
                var drift    = new Item { name = ItemDrift };
                var clip     = new Item { name = ItemClip };
                res.items.Add(baseline); res.items.Add(drift); res.items.Add(clip);
                CheckFrames(a, dw, cw, ch, frames, o, baseline, drift, clip);
            }
            return res;
        }

        // ── 격자 검출 ──────────────────────────────────────────────────────────

        private static uint Key(Color32 c) => c.a == 0 ? 0u : ((uint)c.r << 24) | ((uint)c.g << 16) | ((uint)c.b << 8) | c.a;

        private static int Gcd(int x, int y) { while (y != 0) { int t = x % y; x = y; y = t; } return x; }

        /// <summary>가로·세로 같은색 런 길이의 최대공약수 → k(1..16). 16 초과면 그 약수 중 16 이하 최대.</summary>
        public static int EstimateK(Color32[] px, int w, int h)
        {
            int g = 0;
            for (int y = 0; y < h && g != 1; y++)
            {
                int run = 1;
                for (int x = 1; x < w; x++)
                {
                    if (Key(px[y * w + x]) == Key(px[y * w + x - 1])) run++;
                    else { g = Gcd(g, run); run = 1; if (g == 1) break; }
                }
                g = Gcd(g, run);
            }
            for (int x = 0; x < w && g != 1; x++)
            {
                int run = 1;
                for (int y = 1; y < h; y++)
                {
                    if (Key(px[y * w + x]) == Key(px[(y - 1) * w + x])) run++;
                    else { g = Gcd(g, run); run = 1; if (g == 1) break; }
                }
                g = Gcd(g, run);
            }
            if (g <= 1) return 1;
            for (int d = Math.Min(g, MaxK); d >= 1; d--) if (g % d == 0) return d;
            return 1;
        }

        private static readonly uint[] _blockBuf = new uint[MaxK * MaxK];

        /// <summary>블록 (bx,by) 의 최빈 키와 그 개수. 비어 있지 않은 블록 여부는 anyOpaque.</summary>
        private static uint BlockMode(Color32[] px, int w, int k, int bx, int by, out int count, out bool anyOpaque)
        {
            int n = 0;
            anyOpaque = false;
            for (int y = 0; y < k; y++)
                for (int x = 0; x < k; x++)
                {
                    Color32 c = px[(by * k + y) * w + bx * k + x];
                    if (c.a != 0) anyOpaque = true;
                    _blockBuf[n++] = Key(c);
                }
            Array.Sort(_blockBuf, 0, n);
            uint best = _blockBuf[0]; int bestCount = 0, cur = 0;
            for (int i = 0; i < n; i++)
            {
                if (i > 0 && _blockBuf[i] == _blockBuf[i - 1]) cur++; else cur = 1;
                if (cur > bestCount) { bestCount = cur; best = _blockBuf[i]; }
            }
            count = bestCount;
            return best;
        }

        /// <summary>k×k 블록 최빈색으로 다운샘플. w,h 는 k 의 배수여야 합니다.</summary>
        public static Color32[] Downsample(Color32[] px, int w, int h, int k, out int dw, out int dh)
        {
            dw = w / k; dh = h / k;
            var outPx = new Color32[dw * dh];
            for (int by = 0; by < dh; by++)
                for (int bx = 0; bx < dw; bx++)
                {
                    uint key = BlockMode(px, w, k, bx, by, out _, out _);
                    outPx[by * dw + bx] = key == 0 ? new Color32(0, 0, 0, 0)
                        : new Color32((byte)(key >> 24), (byte)(key >> 16), (byte)(key >> 8), (byte)key);
                }
            return outPx;
        }

        /// <summary>
        /// 재업스케일 오차 = (최빈색과 다른 픽셀 수) / (불투명 픽셀이 하나라도 있는 블록의 전체 픽셀 수).
        /// w,h 가 k 의 배수가 아니면 1.
        /// </summary>
        public static float UpscaleError(Color32[] px, int w, int h, int k)
        {
            if (k < 1 || w % k != 0 || h % k != 0) return 1f;
            long mismatch = 0, total = 0;
            for (int by = 0; by < h / k; by++)
                for (int bx = 0; bx < w / k; bx++)
                {
                    BlockMode(px, w, k, bx, by, out int cnt, out bool any);
                    if (!any) continue;
                    mismatch += k * k - cnt;
                    total    += k * k;
                }
            return total == 0 ? 0f : (float)mismatch / total;
        }

        // ── 팔레트 ─────────────────────────────────────────────────────────────

        private static void CheckPalette(Color32[] a, int w, int h, int cw, int ch, int frames, Options o, Item item)
        {
            if (o.palette == null || o.palette.Length == 0) { item.Fail("팔레트가 비어 있음(NEWIMAG/palette_32px.txt)"); return; }
            var cache = new Dictionary<uint, bool>();
            int outside = 0;
            string example = null;
            int maxColors = o.maxColors > 0 ? o.maxColors : 16;
            int worstColors = 0, worstFrame = 0;
            for (int f = 0; f < frames; f++)
            {
                var seen = new HashSet<uint>();
                for (int y = 0; y < ch; y++)
                    for (int x = 0; x < cw; x++)
                    {
                        int xx = f * cw + x;
                        if (xx >= w || y >= h) continue;
                        Color32 c = a[y * w + xx];
                        if (c.a == 0) continue;
                        uint key = Key(c);
                        if (!seen.Add(key)) continue;
                        if (!cache.TryGetValue(key, out bool ok))
                        {
                            ok = NearestPaletteDistance(c, o.palette) <= PaletteDistance;
                            cache[key] = ok;
                        }
                        if (!ok) { outside++; example = example ?? $"#{c.r:X2}{c.g:X2}{c.b:X2}"; }
                    }
                if (seen.Count > worstColors) { worstColors = seen.Count; worstFrame = f; }
            }
            if (outside > 0) item.Fail($"팔레트 밖 색 {outside}종 (예 {example})");
            if (worstColors > maxColors) item.Fail($"스프라이트당 색 수 {worstColors} > {maxColors} (프레임 {worstFrame})");
        }

        public static float NearestPaletteDistance(Color32 c, Color32[] palette)
        {
            int best = int.MaxValue;
            foreach (var p in palette)
            {
                int dr = c.r - p.r, dg = c.g - p.g, db = c.b - p.b;
                int d = dr * dr + dg * dg + db * db;
                if (d < best) best = d;
            }
            return (float)Math.Sqrt(best);
        }

        /// <summary>팔레트에서 RGB 거리가 가장 가까운 색(알파 255).</summary>
        public static Color32 NearestPaletteColor(Color32 c, Color32[] palette)
        {
            int best = int.MaxValue; Color32 bc = c;
            foreach (var p in palette)
            {
                int dr = c.r - p.r, dg = c.g - p.g, db = c.b - p.b;
                int d = dr * dr + dg * dg + db * db;
                if (d < best) { best = d; bc = p; }
            }
            return new Color32(bc.r, bc.g, bc.b, 255);
        }

        // ── 프레임 검사 (기준선·드리프트·잘림) ─────────────────────────────────

        private static void CheckFrames(Color32[] a, int w, int cw, int ch, int frames, Options o,
                                        Item baseline, Item drift, Item clip)
        {
            var minY = new int[frames]; var maxY = new int[frames];
            var minX = new int[frames]; var maxX = new int[frames];
            var empty = new bool[frames];
            for (int f = 0; f < frames; f++)
            {
                minX[f] = int.MaxValue; minY[f] = int.MaxValue; maxX[f] = -1; maxY[f] = -1;
                for (int y = 0; y < ch; y++)
                    for (int x = 0; x < cw; x++)
                    {
                        if (a[y * w + f * cw + x].a == 0) continue;
                        if (x < minX[f]) minX[f] = x; if (x > maxX[f]) maxX[f] = x;
                        if (y < minY[f]) minY[f] = y; if (y > maxY[f]) maxY[f] = y;
                    }
                empty[f] = maxX[f] < 0;
                if (empty[f]) baseline.Fail($"프레임 {f} 비어 있음");
            }

            // 기준선: 프레임마다 최하단 불투명 행이 같아야 함 (공중 애니 예외)
            if (!o.airborne)
            {
                int refRow = -1;
                for (int f = 0; f < frames; f++)
                {
                    if (empty[f]) continue;
                    if (refRow < 0) refRow = minY[f];
                    else if (minY[f] != refRow) baseline.Fail($"프레임 {f} 기준선 행 {minY[f]} ≠ {refRow}");
                }
            }

            // 수평·높이 드리프트 (Death 예외)
            if (!o.death)
            {
                float sumC = 0, sumH = 0; int n = 0;
                for (int f = 0; f < frames; f++)
                {
                    if (empty[f]) continue;
                    sumC += (minX[f] + maxX[f] + 1) * 0.5f;
                    sumH += maxY[f] - minY[f] + 1;
                    n++;
                }
                if (n > 0)
                {
                    float meanC = sumC / n, meanH = sumH / n;
                    for (int f = 0; f < frames; f++)
                    {
                        if (empty[f]) continue;
                        float c = (minX[f] + maxX[f] + 1) * 0.5f;
                        float hh = maxY[f] - minY[f] + 1;
                        if (Math.Abs(c - meanC) > DriftTolerance)
                            drift.Fail($"프레임 {f} 가로 중심 {c:F1} 이 평균 {meanC:F1} 에서 ±{DriftTolerance}px 초과");
                        if (Math.Abs(hh - meanH) > DriftTolerance)
                            drift.Fail($"프레임 {f} 높이 {hh} 가 평균 {meanH:F1} 에서 ±{DriftTolerance}px 초과");
                    }
                }
            }

            // 잘림: 셀 상·좌·우 1px 테두리 (바닥 행 예외)
            for (int f = 0; f < frames; f++)
            {
                if (empty[f]) continue;
                int top = 0, left = 0, right = 0;
                for (int x = 0; x < cw; x++) if (a[(ch - 1) * w + f * cw + x].a != 0) top++;
                for (int y = 0; y < ch; y++)
                {
                    if (a[y * w + f * cw].a != 0) left++;
                    if (a[y * w + f * cw + cw - 1].a != 0) right++;
                }
                if (top + left + right > 0)
                    clip.Fail($"프레임 {f} 셀 테두리 침범 (상 {top} · 좌 {left} · 우 {right})");
            }
        }
    }
}
