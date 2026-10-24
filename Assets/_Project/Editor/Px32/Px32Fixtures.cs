using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ReTrap.EditorTools
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  Px32Fixtures — PixelArtSheetValidator 자기 검사용 결함 샘플 생성
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 메뉴 <b>ReTrap → Dev → Px32 결함 샘플 생성</b>: <c>tools/ci/fixtures/px32/</c> 에 48×48 셀 4프레임 캐릭터 시트를
    /// 8배 업스케일한 PNG 4장(good / bad_drift / bad_clip / bad_fakepixel)을 만듭니다. 팔레트 색만 사용.
    /// CIRunner <c>sprites32</c> 검사가 이 4장으로 검사기 자체를 확인합니다.
    /// </summary>
    public static class Px32Fixtures
    {
        public const string FixtureFolder = "tools/ci/fixtures/px32";
        private const int Cell = 48, Frames = 4, Scale = 8;

        [MenuItem("ReTrap/Dev/Px32 결함 샘플 생성")]
        public static string GenerateFixtures()
        {
            var pal = Px32Rules.LoadPalette(true);
            if (pal.Length < 4) { string m = "팔레트 부족: " + Px32Rules.PaletteRel; Debug.LogError("[Px32Fixtures] " + m); return m; }

            // 밝기순 정렬 후 외곽선(가장 어두움)·몸통·머리 색 선택
            var sorted = (Color32[])pal.Clone();
            Array.Sort(sorted, (x, y) => Lum(x).CompareTo(Lum(y)));
            Color32 outline = sorted[0];
            Color32 body    = sorted[sorted.Length / 2];
            Color32 head    = sorted[sorted.Length * 3 / 4];

            string dir = Path.Combine(Px32Rules.ProjectRoot(), FixtureFolder);
            Directory.CreateDirectory(dir);

            Write(dir, "good_Idle.png",           Upscale(Draw(outline, body, head, null, false)));
            Write(dir, "bad_drift_Idle.png",      Upscale(Draw(outline, body, head, new[] { 0, 3, 6, 9 }, false)));
            Write(dir, "bad_clip_Idle.png",       Upscale(Draw(outline, body, head, null, true)));
            Write(dir, "bad_fakepixel_Idle.png", AddFakeNoise(Upscale(Draw(outline, body, head, null, false)), outline));

            string msg = "Px32 결함 샘플 4장 생성: " + dir;
            Debug.Log("[Px32Fixtures] " + msg);
            return msg;
        }

        private static float Lum(Color32 c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;

        /// <summary>1배 아트(192×48, 아래→위)를 그립니다. xOffsets: 프레임별 몸통 x 이동, tallHead: 머리를 셀 상단 테두리까지 올림.</summary>
        private static Color32[] Draw(Color32 outline, Color32 body, Color32 head, int[] xOffsets, bool tallHead)
        {
            int w = Cell * Frames;
            var a = new Color32[w * Cell];
            for (int f = 0; f < Frames; f++)
            {
                int bx = f * Cell + 18 + (xOffsets != null ? xOffsets[f] : 0);
                Box(a, w, bx, 0, bx + 11, 29, outline, body);                       // 몸통 12×30 (발 = 행 0)
                int hy = tallHead ? 40 : 30;                                        // 머리 8×8
                if (tallHead) Box(a, w, bx + 5, 30, bx + 6, 39, outline, body);     // 목
                Box(a, w, bx + 2, hy, bx + 9, hy + 7, outline, head);
            }
            return a;
        }

        private static void Box(Color32[] a, int w, int x0, int y0, int x1, int y1, Color32 line, Color32 fill)
        {
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    a[y * w + x] = (x == x0 || x == x1 || y == y0 || y == y1) ? line : fill;
        }

        private static Color32[] Upscale(Color32[] a)
        {
            int w = Cell * Frames, W = w * Scale, H = Cell * Scale;
            var o = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    o[y * W + x] = a[(y / Scale) * w + x / Scale];
            return o;
        }

        /// <summary>불투명 블록마다 모서리 1px 을 반투명으로, 가장자리 중간 1px 을 외곽선색으로 바꿔 격자 불일치를 만듭니다.</summary>
        private static Color32[] AddFakeNoise(Color32[] o, Color32 outline)
        {
            int W = Cell * Frames * Scale, H = Cell * Scale;
            for (int by = 0; by < H / Scale; by++)
                for (int bx = 0; bx < W / Scale; bx++)
                {
                    if (o[(by * Scale) * W + bx * Scale].a == 0) continue;
                    var c = o[(by * Scale) * W + bx * Scale];
                    o[(by * Scale) * W + bx * Scale] = new Color32(c.r, c.g, c.b, 128);
                    o[(by * Scale + 3) * W + bx * Scale + Scale - 1] = outline;
                    o[(by * Scale + Scale - 1) * W + bx * Scale] = new Color32(c.r, c.g, c.b, 64);
                }
            return o;
        }

        private static void Write(string dir, string name, Color32[] px)
        {
            var tex = new Texture2D(Cell * Frames * Scale, Cell * Scale, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            File.WriteAllBytes(Path.Combine(dir, name), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }
    }
}
