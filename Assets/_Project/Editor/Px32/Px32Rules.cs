using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace ReTrap.EditorTools
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  Px32Rules — 32px 아트 규격 상수·규격표·팔레트 로더 (계획서 2절)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 32px 규격(PPU 32)의 단일 출처. 셀 규격표(계획서 2.2), 경로 규칙, 마스터 팔레트 로더를 제공합니다.
    /// 에디터 전용이며 런타임 동작과 무관합니다.
    /// </summary>
    public static class Px32Rules
    {
        public const int    Ppu        = 32;
        public const string RootFolder = "Assets/_Project/ResourcceEX/Sprites/Px32";
        /// <summary>마스터 팔레트 (화풍 v2 — 주인공·적·함정·타일·배경 추가색 포함).</summary>
        public const string PaletteRel = "NEWIMAG/palette_v2.txt";

        /// <summary>허용 카테고리 폴더 이름.</summary>
        public static readonly string[] Categories = { "Tiles", "Player", "Enemies", "Traps", "Effects", "Backgrounds", "UI" };

        /// <summary>시트(가로 1줄 프레임)로 슬라이스하는 카테고리.</summary>
        public static bool IsSheetCategory(string category)
            => category == "Player" || category == "Enemies" || category == "Effects";

        /// <summary>시트 1종의 규격(셀 크기·프레임 수·fps·피벗).</summary>
        public sealed class SheetSpec
        {
            public int     cellW, cellH, frames;
            public float   fps;
            public bool    loop;
            public Vector2 pivot;
        }

        /// <summary>경로가 Px32 폴더 하위인지.</summary>
        public static bool IsPx32Path(string assetPath)
            => assetPath != null && assetPath.Replace('\\', '/').StartsWith(RootFolder + "/", StringComparison.OrdinalIgnoreCase);

        /// <summary>Px32 하위 경로에서 카테고리(첫 하위 폴더)를 추출. 없으면 null.</summary>
        public static string CategoryOf(string assetPath)
        {
            if (!IsPx32Path(assetPath)) return null;
            string rest = assetPath.Replace('\\', '/').Substring(RootFolder.Length + 1);
            int slash = rest.IndexOf('/');
            return slash < 0 ? null : rest.Substring(0, slash);
        }

        /// <summary>파일명(확장자 제외) "&lt;대상&gt;_&lt;애니&gt;" 에서 애니 이름. '_' 가 없으면 빈 문자열.</summary>
        public static string AnimOf(string baseName)
        {
            int i = baseName.LastIndexOf('_');
            return i < 0 ? "" : baseName.Substring(i + 1);
        }

        /// <summary>공중 애니(기준선 드리프트 예외): Jump*, Fall*, Imp 계열 전부.</summary>
        public static bool IsAirborne(string baseName)
        {
            string anim = AnimOf(baseName);
            return anim.StartsWith("Jump", StringComparison.OrdinalIgnoreCase)
                || anim.StartsWith("Fall", StringComparison.OrdinalIgnoreCase)
                || baseName.StartsWith("Imp", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Death 애니(수평·높이 드리프트 예외).</summary>
        public static bool IsDeath(string baseName)
            => AnimOf(baseName).Equals("Death", StringComparison.OrdinalIgnoreCase);

        /// <summary>웅크림·착지 찌그러짐·움찔 애니(높이 드리프트 예외 — 자세 높이가 바뀌는 게 정상): JumpStart·Land·Hit.</summary>
        public static bool IsSquash(string baseName)
        {
            string anim = AnimOf(baseName);
            return anim.Equals("JumpStart", StringComparison.OrdinalIgnoreCase)
                || anim.Equals("Land", StringComparison.OrdinalIgnoreCase)
                || anim.Equals("Hit", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 카테고리·파일명(확장자 제외)에 해당하는 규격. 규격표에 없으면 null
        /// (Validator 가 셀=높이 정사각·프레임=가로/세로 로 추정).
        /// </summary>
        public static SheetSpec GetSheetSpec(string category, string baseName)
        {
            if (category == "Tiles")
                return new SheetSpec { cellW = 32, cellH = 32, frames = 1, pivot = new Vector2(0.5f, 0.5f) };

            // 배경: 원경 960×540, 근경 960×384 (이름 끝 _Near) — 3칸 기준 화면(docs/scale-3tile.md) — 규격으로 업스케일 배율을 확정해야
            // 2×2 덩어리로 그린 그림을 4배로 잘못 추정하지 않는다
            if (category == "Backgrounds")
            {
                bool near = baseName.EndsWith("_Near", StringComparison.OrdinalIgnoreCase);
                return new SheetSpec { cellW = 960, cellH = near ? 384 : 540, frames = 1, pivot = new Vector2(0.5f, near ? 0f : 0.5f) };
            }

            if (category == "Effects")
                return EffectSpec(baseName);

            string anim = AnimOf(baseName);
            if (anim.Length == 0) return null;

            return CharacterSpec(category, baseName, anim, HeroCell, EnemyCell(baseName));
        }

        /// <summary>
        /// 이펙트·월드 UI 시트 규격 (화풍 v2, NEWIMAG/Px32FxV2). 하단 피벗 = 발밑·바닥에 놓는 것.
        /// </summary>
        private static SheetSpec EffectSpec(string baseName)
        {
            var center = new Vector2(0.5f, 0.5f);
            var bottom = new Vector2(0.5f, 0f);
            switch (baseName)
            {
                case "Fx_Dash":      return Sheet(32, 5, 20f, false, bottom);
                case "Fx_Hit":       return Sheet(32, 4, 20f, false, center);
                case "Fx_Death":     return Sheet(48, 6, 15f, false, center);
                case "Fx_Respawn":   return Sheet(48, 6, 15f, false, bottom);
                case "Fx_Dust":      return Sheet(16, 4, 15f, false, bottom);
                case "Fx_EnemyPoof": return Sheet(48, 6, 15f, false, center);
                case "Fx_GoalMarker": return Sheet(64, 4, 6f, true,  bottom); // 3칸 기준 2칸 (NEWIMAG/Px32FxV3)
                case "Gauge_Ring24": return Sheet(24, 9,  0f, false, center);
                case "Gauge_Ring32": return Sheet(32, 9,  0f, false, center);
            }
            return null;
        }

        /// <summary>주인공 셀 (3칸 기준: 키 96px — docs/scale-3tile.md).</summary>
        public const int HeroCell = 128;

        private static SheetSpec CharacterSpec(string category, string baseName, string anim, int heroCell, int enemyCell)
        {
            if (category == "Player" && baseName.StartsWith("Hero_", StringComparison.OrdinalIgnoreCase))
            {
                var bottom = new Vector2(0.5f, 0f);
                switch (anim)
                {
                    case "Idle":      return Sheet(heroCell, 6,  8f, true,  bottom);
                    case "Run":       return Sheet(heroCell, 8, 12f, true,  bottom);
                    case "JumpStart": return Sheet(heroCell, 2, 12f, false, bottom);
                    case "Fall":      return Sheet(heroCell, 2,  8f, true,  bottom);
                    case "Land":      return Sheet(heroCell, 3, 12f, false, bottom);
                    case "Dash":      return Sheet(heroCell, 4, 20f, false, bottom);  // 대시 0.2s(PlayerController.dashDuration)와 길이 일치
                    case "Hit":       return Sheet(heroCell, 2, 10f, false, bottom);
                    case "Death":     return Sheet(heroCell, 6, 10f, false, bottom);
                }
                return null;
            }

            if (category == "Enemies")
            {
                int cell = enemyCell;
                if (cell == 0) return null;
                var pivot = baseName.StartsWith("Imp", StringComparison.OrdinalIgnoreCase)
                    ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 0f);
                switch (anim)
                {
                    case "Idle":  return Sheet(cell, 4,  6f, true,  pivot);
                    case "Run":   return Sheet(cell, 6, 10f, true,  pivot);
                    case "Hit":   return Sheet(cell, 2, 10f, false, pivot);
                    case "Death": return Sheet(cell, 6, 10f, false, pivot);
                }
            }
            return null;
        }

        /// <summary>적 셀 (3칸 기준: 고블린·임프 키 64 → 셀 96, 스켈레톤·기사 키 96 → 128, 오크 키 128 → 160).</summary>
        private static int EnemyCell(string baseName)
        {
            if (baseName.StartsWith("Goblin", StringComparison.OrdinalIgnoreCase) ||
                baseName.StartsWith("Imp",    StringComparison.OrdinalIgnoreCase)) return 96;
            if (baseName.StartsWith("Skeleton", StringComparison.OrdinalIgnoreCase) ||
                baseName.StartsWith("Elite",    StringComparison.OrdinalIgnoreCase)) return 128;
            if (baseName.StartsWith("Orc", StringComparison.OrdinalIgnoreCase)) return 160;
            return 0;
        }

        private static SheetSpec Sheet(int cell, int frames, float fps, bool loop, Vector2 pivot)
            => new SheetSpec { cellW = cell, cellH = cell, frames = frames, fps = fps, loop = loop, pivot = pivot };

        /// <summary>시트 슬라이스 피벗: 캐릭터(Player/Enemies)는 규격 피벗, 그 외 중앙.</summary>
        public static Vector2 PivotFor(string category, string baseName)
        {
            var spec = GetSheetSpec(category, baseName);
            if (spec != null) return spec.pivot;
            if (category == "Player" || category == "Enemies")
                return baseName.StartsWith("Imp", StringComparison.OrdinalIgnoreCase)
                    ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 0f);
            return new Vector2(0.5f, 0.5f);
        }

        // ── 팔레트 ─────────────────────────────────────────────────────────────

        private static Color32[] _palette;

        public static string ProjectRoot() => Path.GetDirectoryName(Application.dataPath);

        /// <summary>
        /// 마스터 팔레트 파일의 #RRGGBB 줄(# 로 시작해도 6자리 hex 면 색, 그 외 주석 무시)을 읽습니다.
        /// 파일이 없으면 빈 배열(CI 가 "팔레트 없음"으로 실패).
        /// </summary>
        public static Color32[] LoadPalette(bool forceReload = false)
        {
            if (_palette != null && !forceReload) return _palette;
            var list = new List<Color32>();
            ReadPaletteFile(PaletteRel, list);
            _palette = list.ToArray();
            return _palette;
        }

        private static void ReadPaletteFile(string rel, List<Color32> list)
        {
            string path = Path.Combine(ProjectRoot(), rel);
            if (!File.Exists(path)) return;
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length != 7 || line[0] != '#' || line.IndexOf(' ') >= 0) continue; // 주석·빈 줄
                if (!int.TryParse(line.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int v)) continue;
                var c = new Color32((byte)(v >> 16), (byte)(v >> 8), (byte)v, 255);
                if (!list.Exists(p => p.r == c.r && p.g == c.g && p.b == c.b)) list.Add(c);
            }
        }
    }
}
