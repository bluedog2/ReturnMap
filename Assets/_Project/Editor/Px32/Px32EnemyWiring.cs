using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ReTrap.EditorTools
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  Px32EnemyWiring — 적 5종 32px 연결 (32px 전환 5단계 배선)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 메뉴 <b>ReTrap → Setup → Px32 적 연결</b> (멱등, 대화상자 없음 → 배치모드 가능):
    /// <c>-executeMethod ReTrap.EditorTools.Px32EnemyWiring.Run</c>
    /// <list type="number">
    ///   <item>사전 검사: 시트 20장 규격 프레임 수 · 프리팹 5종의 Visual(SpriteRenderer)·Collider2D · 각 종 Idle_0. 하나라도 실패하면 아무것도 바꾸지 않고 중단.</item>
    ///   <item>적 클립의 빈 경로 m_Sprite 바인딩 → "Visual", 컨트롤러 state writeDefaultValues=false,
    ///         클립 재배선(<see cref="SpriteClipRebinder.RunApplyEnemies"/>). 재배선에서 건너뛴 게 있으면 프리팹은 건드리지 않고 중단.</item>
    ///   <item>프리팹: Visual 스케일 1 · 위치(지상=콜라이더 바닥, Imp_Flyer=콜라이더 중심) · 기본 sprite=Idle_0.
    ///         콜라이더 크기·오프셋은 변경하지 않고, 콜라이더 크기 vs 새 아트 불투명 영역(px)만 로그로 남긴다.</item>
    /// </list>
    /// 모든 이전 값은 로그에 남긴다.
    /// </summary>
    public static class Px32EnemyWiring
    {
        private const string SheetDir  = "Assets/_Project/ResourcceEX/Sprites/Px32/Enemies";
        private const string PrefabDir = "Assets/_Project/ResourcceEX/Prefabs/Enemies";
        private const string AnimDir   = "Assets/_Project/ResourcceEX/Animations/Enemies";
        private const float  Ppu       = 32f;

        private static readonly string[] Species =
            { "Goblin_Raider", "Imp_Flyer", "Skeleton_Archer", "Elite_Knight", "Orc_Brute" };

        private static readonly (string state, int frames)[] States =
            { ("Idle", 4), ("Run", 6), ("Hit", 2), ("Death", 6) };

        /// <summary>중심 피벗(공중 개체) 종. 나머지는 하단 중앙 피벗(발바닥).</summary>
        private const string FlyerSpecies = "Imp_Flyer";
        /// <summary>서 있는 칸 중심 기준 바닥면(루트 로컬 y, tileUnit 1).</summary>
        private const float  GroundY      = -0.5f;

        [MenuItem("ReTrap/Setup/Px32 적 연결", false, 113)]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[Px32EnemyWiring] 플레이 모드에서는 실행할 수 없습니다.");
                return;
            }

            // 1) 사전 검사 — 아무것도 바꾸기 전에
            AssetDatabase.Refresh();
            string root = Directory.GetParent(Application.dataPath).FullName;
            bool ok = true;
            foreach (string sp in Species)
            {
                foreach (var (state, frames) in States)
                {
                    string path = $"{SheetDir}/{sp}_{state}.png";
                    int count = File.Exists(Path.Combine(root, path))
                        ? AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Count()
                        : 0;
                    if (count != frames)
                    {
                        Debug.LogError($"[Px32EnemyWiring] 아트 미입고 — {path} 스프라이트 {count}개 (규격 {frames}).");
                        ok = false;
                    }
                }

                string prefabPath = PrefabPath(sp);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                var visual = prefab != null ? prefab.transform.Find("Visual") : null;
                if (visual == null || visual.GetComponent<SpriteRenderer>() == null || prefab.GetComponent<Collider2D>() == null)
                {
                    Debug.LogError($"[Px32EnemyWiring] 프리팹 구성 누락 — {prefabPath} (프리팹={prefab != null}, " +
                                   $"Visual={visual != null}, Visual SpriteRenderer={(visual != null && visual.GetComponent<SpriteRenderer>() != null)}, " +
                                   $"루트 Collider2D={(prefab != null && prefab.GetComponent<Collider2D>() != null)})");
                    ok = false;
                }
                if (FindIdle0(sp) == null)
                {
                    Debug.LogError($"[Px32EnemyWiring] {sp}_Idle_0 스프라이트 없음");
                    ok = false;
                }
            }
            if (!ok)
            {
                Debug.LogError("[Px32EnemyWiring] 사전 검사 실패 — 아무것도 변경하지 않고 중단.");
                return;
            }

            // 2) 클립 경로 보정 + 컨트롤러 writeDefaultValues=false + 클립 재배선
            FixEmptyPathSpriteBindings();
            FixControllers();
            if (!SpriteClipRebinder.RunApplyEnemies())
            {
                Debug.LogError("[Px32EnemyWiring] 적 클립 재배선에서 건너뛴 항목이 있음 — Logs/px32-rebind.txt 확인. 프리팹은 변경하지 않고 중단.");
                AssetDatabase.SaveAssets();
                return;
            }

            // 3) 프리팹
            foreach (string sp in Species) WirePrefab(sp);

            // 4) 저장
            AssetDatabase.SaveAssets();
            Debug.Log("[Px32EnemyWiring] 완료 — 클립/컨트롤러/프리팹 저장.");
        }

        private static string PrefabPath(string sp) => $"{PrefabDir}/Enemy_{sp}.prefab";

        private static Sprite FindIdle0(string sp) =>
            AssetDatabase.LoadAllAssetsAtPath($"{SheetDir}/{sp}_Idle.png")
                .OfType<Sprite>().FirstOrDefault(s => s.name == $"{sp}_Idle_0");

        // ── 프리팹 ───────────────────────────────────────────────────────────

        private static void WirePrefab(string sp)
        {
            string path = PrefabPath(sp);
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var visual = contents.transform.Find("Visual");
                var sr     = visual.GetComponent<SpriteRenderer>();
                var col    = contents.GetComponent<Collider2D>();
                var idle0  = FindIdle0(sp);

                // 콜라이더(루트 로컬) 바닥·중심 — 크기/오프셋은 변경하지 않고 비교 로그에만 쓴다
                GetColliderExtents(col, out Vector2 center, out Vector2 size);
                float bottom = center.y - size.y * 0.5f;
                // 루트는 서 있는 칸 중심(MapData.CellToWorld)에 놓이므로 바닥면 = 루트 로컬 -0.5칸.
                // 지상 종은 발바닥 피벗을 바닥면에, Imp(중앙 피벗)는 칸 중심에 둔다.
                float targetY = sp == FlyerSpecies ? 0f : GroundY;

                Vector3 oldScale = visual.localScale, oldPos = visual.localPosition;
                var oldSprite = sr.sprite;

                visual.localScale    = new Vector3(1f, 1f, oldScale.z);
                visual.localPosition = new Vector3(0f, targetY, oldPos.z);
                sr.sprite = idle0;

                PrefabUtility.SaveAsPrefabAsset(contents, path);

                string bbox = OpaqueBoundsPx(sp, idle0, out Vector2Int bboxPx) ? $"{bboxPx.x}x{bboxPx.y}px" : "측정 실패";
                Debug.Log($"[Px32EnemyWiring] {sp}: {col.GetType().Name} 크기 {size.x * Ppu:F1}x{size.y * Ppu:F1}px(콜라이더 유지) vs 새 아트 Idle_0 불투명 {bbox}. " +
                          $"Visual scale {oldScale} → {visual.localScale}, pos {oldPos} → {visual.localPosition} " +
                          $"({(sp == FlyerSpecies ? "칸 중심" : "바닥면")} y={targetY:F5}, 참고: 콜라이더 바닥 {bottom:F5} · 중심 {center.y:F5}). " +
                          $"sprite {(oldSprite != null ? oldSprite.name : "null")} → {idle0.name}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>콜라이더의 루트 로컬 중심·크기. Box/Capsule/Circle 지원, 그 외는 bounds 근사.</summary>
        private static void GetColliderExtents(Collider2D col, out Vector2 center, out Vector2 size)
        {
            switch (col)
            {
                case BoxCollider2D b:
                    center = b.offset; size = b.size; return;
                case CapsuleCollider2D c:
                    center = c.offset; size = c.size; return;
                case CircleCollider2D ci:
                    center = ci.offset; size = Vector2.one * (ci.radius * 2f); return;
                default:
                    // 프리팹 콘텐츠는 비활성 상태라 bounds 가 0일 수 있음 — 사전 검사에서 지원 타입만 쓴다고 가정
                    center = col.offset; size = col.bounds.size; return;
            }
        }

        /// <summary>Idle_0 스프라이트 사각형 안의 알파>0 픽셀 bbox 크기(px). PNG 를 직접 디코딩(Read/Write 설정 무관).</summary>
        private static bool OpaqueBoundsPx(string sp, Sprite sprite, out Vector2Int size)
        {
            size = default;
            string full = Path.Combine(Directory.GetParent(Application.dataPath).FullName, $"{SheetDir}/{sp}_Idle.png");
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!tex.LoadImage(File.ReadAllBytes(full))) return false;
                Rect r = sprite.rect;
                int x0 = Mathf.RoundToInt(r.x), y0 = Mathf.RoundToInt(r.y);
                int w = Mathf.RoundToInt(r.width), h = Mathf.RoundToInt(r.height);
                var px = tex.GetPixels32();
                int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        if (px[(y0 + y) * tex.width + x0 + x].a == 0) continue;
                        if (x < minX) minX = x; if (x > maxX) maxX = x;
                        if (y < minY) minY = y; if (y > maxY) maxY = y;
                    }
                if (maxX < 0) return false;
                size = new Vector2Int(maxX - minX + 1, maxY - minY + 1);
                return true;
            }
            finally { Object.DestroyImmediate(tex); }
        }

        // ── 클립 / 컨트롤러 ──────────────────────────────────────────────────

        /// <summary>적 클립의 SpriteRenderer.m_Sprite 바인딩 경로가 ""(루트)이면 "Visual" 로 옮긴다 (커브 유지). 이미 고쳐졌으면 no-op.</summary>
        private static void FixEmptyPathSpriteBindings()
        {
            foreach (string sp in Species)
                foreach (var (state, _) in States)
                {
                    string name = $"Enemy_{sp}_{state}";
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{AnimDir}/{name}.anim");
                    if (clip == null) continue;
                    foreach (var b in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                    {
                        if (b.type != typeof(SpriteRenderer) || b.propertyName != "m_Sprite" || b.path != "") continue;
                        var keys = AnimationUtility.GetObjectReferenceCurve(clip, b);
                        var nb = b;
                        nb.path = "Visual";
                        AnimationUtility.SetObjectReferenceCurve(clip, b, null);
                        AnimationUtility.SetObjectReferenceCurve(clip, nb, keys);
                        EditorUtility.SetDirty(clip);
                        Debug.Log($"[Px32EnemyWiring] {name} m_Sprite 바인딩 경로 \"\" → \"Visual\" ({keys?.Length ?? 0}키)");
                    }
                }
        }

        private static void FixControllers()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:AnimatorController", new[] { AnimDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                if (ctrl == null) continue;
                foreach (var layer in ctrl.layers)
                    FixStateMachine(layer.stateMachine, ctrl.name);
                EditorUtility.SetDirty(ctrl);
            }
        }

        private static void FixStateMachine(AnimatorStateMachine sm, string ctrlName)
        {
            foreach (var cs in sm.states)
            {
                if (cs.state.writeDefaultValues)
                    Debug.Log($"[Px32EnemyWiring] {ctrlName} {cs.state.name} writeDefaultValues True → False");
                cs.state.writeDefaultValues = false;
            }
            foreach (var sub in sm.stateMachines)
                FixStateMachine(sub.stateMachine, ctrlName);
        }
    }
}
