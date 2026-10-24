using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ReTrap.EditorTools
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  ScaleTestSceneBuilder — 640×360 · 32px 규격 시안 씬 생성
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 메뉴 <b>ReTrap → Dev → 32px 규격 시안 씬 생성</b>:
    /// <c>Sprites/ScaleTest</c> 의 시안 스프라이트(PPU 32, Point)로 <c>Scenes/ScaleTest_32px.unity</c> 를 만들고
    /// Pixel Perfect Camera(기준 640×360) 화면을 <c>Logs/scale-test/mockup.png</c> 로 렌더한다. 게임 로직 없음(비교용).
    /// </summary>
    public static class ScaleTestSceneBuilder
    {
        const string SpriteDir = "Assets/_Project/ResourcceEX/Sprites/ScaleTest";
        const string ScenePath = "Assets/_Project/ResourcceEX/Scenes/ScaleTest_32px.unity";
        const int Ppu = 32;

        [MenuItem("ReTrap/Dev/32px 규격 시안 씬 생성", false, 200)]
        public static void Run()
        {
            ConfigureImports();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 카메라: 640×360 기준, 세로 11.25칸
            var camGo = new GameObject("Main Camera", typeof(Camera));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 360f / 2f / Ppu;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            camGo.transform.position = new Vector3(0f, 4.6f, -10f);
            var ppc = camGo.AddComponent<PixelPerfectCamera>();
            ppc.assetsPPU = Ppu;
            ppc.refResolutionX = 640;
            ppc.refResolutionY = 360;

            // 배경 2층(가로 반복)
            Layer("BG_Far", "bg_play_far", -30, new Vector2(0f, 4.6f), tiledWidth: 30f);
            Layer("BG_Near", "bg_play_near", -20, new Vector2(0f, 4.6f), tiledWidth: 30f);

            // 지형: 바닥 2줄 + 계단 + 떠 있는 발판
            var floor = Load("tile_play_floor");
            for (int x = -11; x <= 11; x++) { Tile(floor, x, 0); Tile(floor, x, -1); }
            for (int x = 4; x <= 11; x++) Tile(floor, x, 1);
            for (int x = 7; x <= 11; x++) Tile(floor, x, 2);
            for (int x = -6; x <= -3; x++) Tile(floor, x, 4);

            // 주인공(1.5칸) · 고블린(1.25칸) — 발이 바닥 윗면(y=0.5)에 닿게
            Actor("Hero", "hero_48", new Vector2(-1.5f, 0.5f));
            Actor("Goblin", "goblin_40", new Vector2(2.0f, 0.5f));

            EditorSceneManager.SaveScene(scene, ScenePath);
            Render(cam, Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "scale-test", "mockup.png"));
            Debug.Log("[ScaleTestSceneBuilder] 시안 씬 생성: " + ScenePath);
        }

        static void ConfigureImports()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { SpriteDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var ti = (TextureImporter)AssetImporter.GetAtPath(path);
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.spritePixelsPerUnit = Ppu;
                ti.filterMode = FilterMode.Point;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.mipmapEnabled = false;
                var s = new TextureImporterSettings(); ti.ReadTextureSettings(s);
                s.spriteMeshType = SpriteMeshType.FullRect;
                bool actor = path.Contains("hero") || path.Contains("goblin");
                s.spriteAlignment = (int)(actor ? SpriteAlignment.BottomCenter : SpriteAlignment.Center);
                ti.SetTextureSettings(s);
                ti.SaveAndReimport();
            }
        }

        static Sprite Load(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteDir}/{name}.png");

        static SpriteRenderer Make(string name, Sprite sp, int order, Vector2 pos)
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.position = pos;
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = sp; sr.sortingLayerName = "Map"; sr.sortingOrder = order;
            return sr;
        }

        static void Tile(Sprite sp, int x, int y) => Make($"Tile_{x}_{y}", sp, 0, new Vector2(x, y));
        static void Actor(string name, string sprite, Vector2 feet) => Make(name, Load(sprite), 20, feet);

        static void Layer(string name, string sprite, int order, Vector2 pos, float tiledWidth)
        {
            var sr = Make(name, Load(sprite), order, pos);
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = new Vector2(tiledWidth, sr.sprite.bounds.size.y);
        }

        static void Render(Camera cam, string file)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            var rt = new RenderTexture(1920, 1080, 24);
            cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
            RenderTexture.active = rt;
            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); tex.Apply();
            RenderTexture.active = null; rt.Release();
            File.WriteAllBytes(file, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
    }
}
