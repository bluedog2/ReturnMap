using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace ReTrap.EditorTools
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  Px32ImportPostprocessor — Sprites/Px32/ 하위 텍스처 임포트 규격 자동 적용
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <c>Assets/_Project/ResourcceEX/Sprites/Px32/</c> 하위 텍스처에만 32px 규격(계획서 2.1)을 적용합니다:
    /// PPU 32 · Point · 무압축 · FullRect · 밉맵 끔 · Clamp · alphaIsTransparency.
    /// 시트 파일(<c>&lt;대상&gt;_&lt;애니&gt;.png</c>, 카테고리 Player/Enemies/Effects)은 Multiple + 셀(=텍스처 높이 정사각) 그리드 슬라이스.
    /// 설정이 이미 같으면 아무것도 바꾸지 않아 재임포트 루프가 생기지 않습니다. 그 외 경로의 기존 에셋은 건드리지 않습니다.
    /// </summary>
    public class Px32ImportPostprocessor : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!Px32Rules.IsPx32Path(assetPath)) return;
            var ti = assetImporter as TextureImporter;
            if (ti == null) return;

            string category = Px32Rules.CategoryOf(assetPath);
            string baseName = Path.GetFileNameWithoutExtension(assetPath);

            if (ti.textureType != TextureImporterType.Sprite)               ti.textureType = TextureImporterType.Sprite;
            if (!Mathf.Approximately(ti.spritePixelsPerUnit, Px32Rules.Ppu)) ti.spritePixelsPerUnit = Px32Rules.Ppu;
            if (ti.filterMode != FilterMode.Point)                          ti.filterMode = FilterMode.Point;
            if (ti.textureCompression != TextureImporterCompression.Uncompressed)
                ti.textureCompression = TextureImporterCompression.Uncompressed;
            if (ti.mipmapEnabled)                                           ti.mipmapEnabled = false;
            if (ti.wrapMode != TextureWrapMode.Clamp)                       ti.wrapMode = TextureWrapMode.Clamp;
            if (!ti.alphaIsTransparency)                                    ti.alphaIsTransparency = true;

            // 슬라이스 모드 · 피벗 · 메시 타입
            int cell = SheetCell(category, baseName, out int frames);
            var wantMode = cell > 0 ? SpriteImportMode.Multiple : SpriteImportMode.Single;
            if (ti.spriteImportMode != wantMode) ti.spriteImportMode = wantMode;

            Vector2 pivot = Px32Rules.PivotFor(category, baseName);
            var s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            if (s.spriteMeshType != SpriteMeshType.FullRect ||
                s.spriteAlignment != (int)SpriteAlignment.Custom || s.spritePivot != pivot)
            {
                s.spriteMeshType  = SpriteMeshType.FullRect;
                s.spriteAlignment = (int)SpriteAlignment.Custom;
                s.spritePivot     = pivot;
                ti.SetTextureSettings(s);
            }

            if (cell > 0) ApplyGrid(ti, baseName, cell, frames, pivot);
        }

        /// <summary>시트면 셀 크기(=높이), 아니면 0.</summary>
        private int SheetCell(string category, string baseName, out int frames)
        {
            frames = 0;
            if (!Px32Rules.IsSheetCategory(category) || baseName.IndexOf('_') < 0) return 0;
            if (!TryReadPngSize(out int w, out int h) || h <= 0) return 0;
            if (w % h != 0)
            {
                Debug.LogWarning($"[Px32] {assetPath}: 가로 {w} 가 높이 {h} 의 배수가 아님 — 슬라이스 생략(Single)");
                return 0;
            }
            frames = w / h;
            return h;
        }

        /// <summary>PNG IHDR 에서 크기를 읽습니다(임포트 중 텍스처 로드 없이).</summary>
        private bool TryReadPngSize(out int w, out int h)
        {
            w = h = 0;
            try
            {
                string abs = Path.Combine(Px32Rules.ProjectRoot(), assetPath);
                using (var fs = File.OpenRead(abs))
                {
                    var b = new byte[24];
                    if (fs.Read(b, 0, 24) < 24 || b[1] != (byte)'P' || b[2] != (byte)'N' || b[3] != (byte)'G') return false;
                    w = (b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19];
                    h = (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23];
                    return true;
                }
            }
            catch (Exception) { return false; }
        }

        /// <summary>
        /// com.unity.2d.sprite 데이터 프로바이더로 그리드 슬라이스. 같은 이름의 기존 SpriteRect 는 spriteID 를 재사용하고
        /// 새 이름만 GUID 를 발급합니다. name/rect/pivot 이 모두 같으면 쓰지 않습니다.
        /// </summary>
        private static void ApplyGrid(TextureImporter ti, string baseName, int cell, int frames, Vector2 pivot)
        {
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(ti);
            if (provider == null) return;
            provider.InitSpriteEditorDataProvider();

            var old = provider.GetSpriteRects();
            var oldByName = new Dictionary<string, SpriteRect>();
            foreach (var r in old) oldByName[r.name] = r;

            bool same = old.Length == frames;
            var want = new SpriteRect[frames];
            for (int i = 0; i < frames; i++)
            {
                string name = $"{baseName}_{i}";
                var rect = new Rect(i * cell, 0, cell, cell);
                bool exists = oldByName.TryGetValue(name, out var prev);
                want[i] = new SpriteRect
                {
                    name      = name,
                    rect      = rect,
                    alignment = SpriteAlignment.Custom,
                    pivot     = pivot,
                    spriteID  = exists ? prev.spriteID : GUID.Generate(),
                };
                if (exists) want[i].border = prev.border;
                if (!exists || prev.rect != rect || prev.pivot != pivot || prev.alignment != SpriteAlignment.Custom)
                    same = false;
            }
            if (same) return;

            provider.SetSpriteRects(want);

            var nameProvider = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (nameProvider != null)
            {
                var pairs = new List<SpriteNameFileIdPair>(frames);
                foreach (var r in want) pairs.Add(new SpriteNameFileIdPair(r.name, r.spriteID));
                nameProvider.SetNameFileIdPairs(pairs);
            }
            provider.Apply();
        }
    }
}
