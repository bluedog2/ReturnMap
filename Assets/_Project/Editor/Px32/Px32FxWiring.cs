using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ReTrap.EditorTools
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  Px32FxWiring — 화풍 v2 이펙트·월드 UI·HUD 아이콘 입고 + 연결
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 메뉴 <b>ReTrap → Setup → Px32 이펙트·UI 연결</b> (배치모드는 <see cref="Run"/>, 멱등):
    /// <list type="number">
    ///   <item>NEWIMAG/Px32FxV2/Effects(시트) → Sprites/Px32/Effects, UI(단일) → Sprites/Px32/UI 복사
    ///         (GoalMarker 는 시트 슬라이스를 위해 Fx_GoalMarker 로). 규격은 Px32Rules.EffectSpec.</item>
    ///   <item>SampleScene: PlayerEffects 프레임(대시·피격·사망·리스폰·착지 먼지), DashGaugeUI 픽셀 링,
    ///         AgentKillFx(처치 연기) 생성, MapLoader 골 표식 프리팹, BuildHudController 철거 아이콘.</item>
    ///   <item>함정 프리팹: ArrowShooter 쿨다운 픽셀 링, Critical/Dud 파티클 픽셀 텍스처 재질.</item>
    ///   <item>TrapDefinition 아이콘 3종.</item>
    /// </list>
    /// 덮어쓰기 전 값은 로그에 남긴다.
    /// </summary>
    public static class Px32FxWiring
    {
        private const string InboxEffects = "NEWIMAG/Px32FxV2/Effects";
        private const string InboxUI      = "NEWIMAG/Px32FxV2/UI";
        private const string EffectsDir   = "Assets/_Project/ResourcceEX/Sprites/Px32/Effects";
        private const string UIDir        = "Assets/_Project/ResourcceEX/Sprites/Px32/UI";
        private const string ScenePath    = "Assets/_Project/ResourcceEX/Scenes/SampleScene.unity";
        private const string TrapPrefabs  = "Assets/_Project/ResourcceEX/Prefabs/Traps";
        private const string GoalPrefab   = "Assets/_Project/ResourcceEX/Prefabs/GoalMarker.prefab";
        private const string MaterialsDir = "Assets/_Project/ResourcceEX/Materials";
        private const string DefsDir      = "Assets/_Project/Settings/TrapDefinitions";
        private const string HudPrefab    = "Assets/_Project/ResourcceEX/Prefabs/UI/BuildHud.prefab";

        [MenuItem("ReTrap/Setup/Px32 이펙트·UI 연결", false, 115)]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[Px32FxWiring] 플레이 모드에서는 실행할 수 없습니다.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine($"입고 {CopyInbox()}장");
            AssetDatabase.Refresh();

            WireTrapPrefabs(log);
            WireIcons(log);
            var goal = BuildGoalPrefab(log);
            WireScene(log, goal);

            AssetDatabase.SaveAssets();
            Debug.Log("[Px32FxWiring] " + log.ToString().Replace("\n", " | "));
        }

        // ── 1. 입고 ───────────────────────────────────────────────────────────

        private static int CopyInbox()
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            int n = 0;
            foreach (var (src, dst) in new[] { (InboxEffects, EffectsDir), (InboxUI, UIDir) })
            {
                string from = Path.Combine(root, src);
                if (!Directory.Exists(from)) { Debug.LogWarning("[Px32FxWiring] 수신함 없음: " + src); continue; }
                Directory.CreateDirectory(Path.Combine(root, dst));
                foreach (string f in Directory.GetFiles(from, "*.png"))
                {
                    string name = Path.GetFileName(f);
                    if (name == "GoalMarker.png") name = "Fx_GoalMarker.png"; // '_' 가 있어야 시트로 슬라이스
                    File.Copy(f, Path.Combine(root, dst, name), true);
                    n++;
                }
            }
            return n;
        }

        // ── 2. 함정 프리팹 (쿨다운 링 · 상태 파티클) ──────────────────────────

        private static void WireTrapPrefabs(StringBuilder log)
        {
            var critMat = EnsureParticleMaterial("TrapParticle_Critical", "Particle_Critical");
            var dudMat  = EnsureParticleMaterial("TrapParticle_Dud",      "Particle_Dud");

            foreach (string trap in new[] { "SpikeTrap", "ArrowShooter", "DropHammer" })
            {
                string path = $"{TrapPrefabs}/{trap}.prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    SetParticles(root, "CriticalParticles", critMat, log, trap);
                    SetParticles(root, "DudParticles",      dudMat,  log, trap);

                    // ArrowShooter 는 게이지가 없으면 런타임에 기본값으로 붙인다 → 아트를 넣으려면 프리팹에 미리 둔다
                    var gauge = root.GetComponent<TrapCooldownGauge>();
                    if (gauge == null && root.GetComponent<ArrowShooter>() != null)
                        gauge = root.AddComponent<TrapCooldownGauge>();
                    if (gauge != null)
                    {
                        var so = new SerializedObject(gauge);
                        log.AppendLine($"[{trap}.CooldownGauge] diameter {so.FindProperty("diameter").floatValue} → 0.75");
                        SetSprites(so.FindProperty("fillFrames"), LoadFrames($"{EffectsDir}/Gauge_Ring24.png"));
                        so.FindProperty("backgroundSprite").objectReferenceValue = LoadSprite($"{UIDir}/Gauge_Ring24_Background.png");
                        so.FindProperty("diameter").floatValue        = 0.75f; // 24px
                        // 링 그림이 이미 크림슨 — 상태 색은 원본 위 약한 틴트
                        so.FindProperty("normalColor").colorValue     = Color.white;
                        so.FindProperty("criticalColor").colorValue   = new Color(1f, 0.75f, 0.75f, 1f);
                        so.FindProperty("beneficialColor").colorValue = new Color(1f, 0.95f, 0.6f, 1f);
                        so.FindProperty("backgroundColor").colorValue = Color.white;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }

        private static Material EnsureParticleMaterial(string matName, string texName)
        {
            string path = $"{MaterialsDir}/{matName}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var src = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsDir}/TrapParticle.mat");
                mat = new Material(src);
                AssetDatabase.CreateAsset(mat, path);
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{UIDir}/{texName}.png");
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void SetParticles(GameObject root, string child, Material mat, StringBuilder log, string trap)
        {
            var t = root.transform.Find(child);
            var ps = t != null ? t.GetComponent<ParticleSystem>() : null;
            if (ps == null) return;
            var main = ps.main;
            log.AppendLine($"[{trap}.{child}] size {main.startSize.constant} → 0.25, color → 흰(텍스처 원색)");
            main.startColor = Color.white;
            main.startSize  = 0.25f; // 8px
            var r = t.GetComponent<ParticleSystemRenderer>();
            if (r != null) r.sharedMaterial = mat;
        }

        // ── 3. HUD 아이콘 ─────────────────────────────────────────────────────

        private static void WireIcons(StringBuilder log)
        {
            foreach (var (def, icon) in new[] { ("Spike", "Icon_Spike"), ("ArrowShooter", "Icon_ArrowShooter"), ("DropHammer", "Icon_DropHammer") })
            {
                var asset = AssetDatabase.LoadAssetAtPath<TrapDefinition>($"{DefsDir}/TrapDefinition_{def}.asset");
                if (asset == null) { log.AppendLine("정의 없음: " + def); continue; }
                var so = new SerializedObject(asset);
                so.FindProperty("icon").objectReferenceValue = LoadSprite($"{UIDir}/{icon}.png");
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
            }
        }

        // ── 4. 골 표식 프리팹 ─────────────────────────────────────────────────

        private static GameObject BuildGoalPrefab(StringBuilder log)
        {
            var frames = LoadFrames($"{EffectsDir}/Fx_GoalMarker.png");
            if (frames.Length == 0) { log.AppendLine("골 표식 프레임 없음 — 프리팹 생략"); return null; }

            var root = new GameObject("GoalMarker");
            try
            {
                // StageGoalTrigger 는 Collider2D 를 요구 — 콜라이더 먼저
                var col = root.AddComponent<BoxCollider2D>();
                col.isTrigger = true;
                col.size      = Vector2.one;
                root.AddComponent<StageGoalTrigger>();

                var visual = new GameObject("Visual");
                visual.transform.SetParent(root.transform, false);
                visual.transform.localPosition = new Vector3(0f, -0.5f, 0f); // 하단 피벗 = 칸 바닥
                var sr = visual.AddComponent<SpriteRenderer>();
                sr.sprite           = frames[0];
                sr.sortingLayerName = "Map";
                sr.sortingOrder     = 15; // 타일·함정 위, 캐릭터 아래 (기존 골 표식과 동일)
                var loop = new SerializedObject(visual.AddComponent<SpriteFrameLoop>());
                SetSprites(loop.FindProperty("frames"), frames);
                loop.FindProperty("fps").floatValue = 6f;
                loop.ApplyModifiedPropertiesWithoutUndo();

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, GoalPrefab);
                log.AppendLine("골 표식 프리팹: " + GoalPrefab);
                return prefab;
            }
            finally { Object.DestroyImmediate(root); }
        }

        // ── 5. 씬 ─────────────────────────────────────────────────────────────

        private static void WireScene(StringBuilder log, GameObject goalPrefab)
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var player = Object.FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                var fx = player.GetComponent<PlayerEffects>();
                if (fx != null)
                {
                    var so = new SerializedObject(fx);
                    SetSprites(so.FindProperty("dashFrames"),    LoadFrames($"{EffectsDir}/Fx_Dash.png"));
                    SetSprites(so.FindProperty("hitFrames"),     LoadFrames($"{EffectsDir}/Fx_Hit.png"));
                    SetSprites(so.FindProperty("deathFrames"),   LoadFrames($"{EffectsDir}/Fx_Death.png"));
                    SetSprites(so.FindProperty("respawnFrames"), LoadFrames($"{EffectsDir}/Fx_Respawn.png"));
                    SetSprites(so.FindProperty("dustFrames"),    LoadFrames($"{EffectsDir}/Fx_Dust.png"));
                    log.AppendLine($"[PlayerEffects] fps {so.FindProperty("fps").floatValue} → 15, effectScale {so.FindProperty("effectScale").floatValue} → 1");
                    so.FindProperty("fps").floatValue         = 15f;
                    so.FindProperty("effectScale").floatValue = 1f; // 32px 규격 그대로 (PPU 32)
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                var gauge = player.GetComponent<DashGaugeUI>();
                if (gauge != null)
                {
                    var so = new SerializedObject(gauge);
                    log.AppendLine($"[DashGaugeUI] size {so.FindProperty("size").floatValue} → 1");
                    SetSprites(so.FindProperty("fillFrames"), LoadFrames($"{EffectsDir}/Gauge_Ring32.png"));
                    so.FindProperty("backgroundSprite").objectReferenceValue = LoadSprite($"{UIDir}/Gauge_Ring32_Background.png");
                    so.FindProperty("size").floatValue              = 1f; // 32px
                    so.FindProperty("fillColor").colorValue         = Color.white;
                    so.FindProperty("emptyColor").colorValue        = Color.white;
                    so.FindProperty("rechargeColor").colorValue     = new Color(0.7f, 0.7f, 0.7f, 1f); // 충전 중 = 어둡게
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            else log.AppendLine("경고: PlayerController 없음");

            var killFx = Object.FindFirstObjectByType<AgentKillFx>();
            if (killFx == null) killFx = new GameObject("AgentKillFx").AddComponent<AgentKillFx>();
            var kso = new SerializedObject(killFx);
            SetSprites(kso.FindProperty("frames"), LoadFrames($"{EffectsDir}/Fx_EnemyPoof.png"));
            kso.ApplyModifiedPropertiesWithoutUndo();

            var loader = Object.FindFirstObjectByType<MapLoader>();
            if (loader != null && goalPrefab != null)
            {
                var so = new SerializedObject(loader);
                so.FindProperty("_goalMarkerPrefab").objectReferenceValue = goalPrefab;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            WireHudPrefab(log);
        }

        /// <summary>BuildHud 프리팹(TrapPrefabBuilder 가 만듦)의 철거 아이콘.</summary>
        private static void WireHudPrefab(StringBuilder log)
        {
            if (!File.Exists(Path.Combine(Directory.GetParent(Application.dataPath).FullName, HudPrefab)))
            {
                log.AppendLine("경고: BuildHud 프리팹 없음 — 철거 아이콘 생략");
                return;
            }
            var root = PrefabUtility.LoadPrefabContents(HudPrefab);
            try
            {
                var hud = root.GetComponentInChildren<BuildHudController>(true);
                if (hud == null) { log.AppendLine("경고: BuildHudController 없음 — 철거 아이콘 생략"); return; }
                var so = new SerializedObject(hud);
                so.FindProperty("removeIcon").objectReferenceValue = LoadSprite($"{UIDir}/Icon_Remove.png");
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, HudPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // ── 헬퍼 ──────────────────────────────────────────────────────────────

        private static Sprite LoadSprite(string path)
            => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();

        /// <summary>시트 슬라이스 스프라이트를 이름 끝 번호 순으로.</summary>
        private static Sprite[] LoadFrames(string path)
            => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
                .OrderBy(s => int.TryParse(s.name.Substring(s.name.LastIndexOf('_') + 1), out int i) ? i : 0)
                .ToArray();

        private static void SetSprites(SerializedProperty arr, Sprite[] sprites)
        {
            arr.arraySize = sprites.Length;
            for (int i = 0; i < sprites.Length; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
        }
    }
}
