using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ReTrap.EditorTools
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  Px32HeroWiring — 주인공 32px 연결 (32px 전환 4단계 배선)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 메뉴 <b>ReTrap → Setup → Px32 주인공 연결</b> (멱등, 대화상자 없음 → 배치모드 가능):
    /// <c>-executeMethod ReTrap.EditorTools.Px32HeroWiring.Run</c>
    /// <list type="number">
    ///   <item>주인공 시트 8종(Hero_Run/JumpStart/Fall/Land/Dash/Hit/Death + 기존 Idle)이 모두 규격 프레임 수로 있을 때만 진행
    ///         (없으면 "아트 미입고"로 중단 — 반쪽 연결 방지). 클립은 <see cref="SpriteClipRebinder.RunApplyHero"/> 로 재배선.</item>
    ///   <item>SampleScene 의 Player: Visual 스케일 1 · 위치 = 콜라이더 바닥, 캡슐 콜라이더 (0.75, 1.875)
    ///         (바닥 높이 유지, 화풍 v2), Rigidbody2D 보간 Interpolate, vcam TargetOffset.y 1.0, DashGaugeUI offset (1.0, 1.7).
    ///         <c>PlayerEffects.effectScale</c> 은 건드리지 않는다. 시트 셀이 <see cref="Px32Rules.HeroCell"/> 가 아니면 중단.</item>
    ///   <item>Lord_Controller 의 Land state speed 를 1 로 되돌린다.</item>
    /// </list>
    /// 모든 이전 값은 로그에 남긴다.
    /// </summary>
    public static class Px32HeroWiring
    {
        private const string ScenePath      = "Assets/_Project/ResourcceEX/Scenes/SampleScene.unity";
        private const string ControllerPath = "Assets/_Project/ResourcceEX/Animations/Lord_Controller.controller";
        private const string SheetDir       = "Assets/_Project/ResourcceEX/Sprites/Px32/Player";

        private static readonly (string sheet, int frames)[] Sheets =
        {
            ("Hero_Idle", 6), ("Hero_Run", 8), ("Hero_JumpStart", 2), ("Hero_Fall", 2),
            ("Hero_Land", 3), ("Hero_Dash", 4), ("Hero_Hit", 2), ("Hero_Death", 6),
        };

        // 화풍 v2 (docs/hero-b-scale-impact.md D1 ②): 아트 키 80px, 판정은 머리 꼭대기·포니테일 제외 60px
        private static readonly Vector2 NewColliderSize = new Vector2(0.75f, 1.875f);
        private const float   CameraTargetOffsetY = 1.0f;                     // 몸 중간이 화면 중심 근처에 오게
        private static readonly Vector2 DashGaugeOffset = new Vector2(1.0f, 1.7f); // 새 키의 머리 옆

        [MenuItem("ReTrap/Setup/Px32 주인공 연결", false, 112)]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[Px32HeroWiring] 플레이 모드에서는 실행할 수 없습니다.");
                return;
            }
            // 메뉴 실행 시 저장 안 된 씬 변경을 묻지 않고 버리지 않도록
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            // 1) 아트 입고 확인 — 8종 모두 새 규격 셀(Px32Rules.HeroCell)로 있을 때만 진행
            //    (구 64 셀 시트에 새 콜라이더를 붙이는 반쪽 연결 방지)
            AssetDatabase.Refresh();
            foreach (var (sheet, frames) in Sheets)
            {
                string path = $"{SheetDir}/{sheet}.png";
                var sprites = File.Exists(Path.Combine(Directory.GetParent(Application.dataPath).FullName, path))
                    ? AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray()
                    : new Sprite[0];
                if (sprites.Length != frames)
                {
                    Debug.LogError($"[Px32HeroWiring] 아트 미입고 — {path} 스프라이트 {sprites.Length}개 (규격 {frames}). 아무것도 변경하지 않고 중단.");
                    return;
                }
                int cellH = Mathf.RoundToInt(sprites[0].rect.height);
                if (cellH != Px32Rules.HeroCell)
                {
                    Debug.LogError($"[Px32HeroWiring] 구 규격 시트 — {path} 셀 높이 {cellH} (규격 {Px32Rules.HeroCell}). 화풍 v2 시트 입고 전이므로 아무것도 변경하지 않고 중단.");
                    return;
                }
            }

            // 2) 씬의 Player 검사 (아무것도 바꾸기 전에)
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var player = Object.FindFirstObjectByType<PlayerController>();
            if (player == null)
            {
                Debug.LogError("[Px32HeroWiring] SampleScene 에서 PlayerController 를 찾지 못함 — 중단");
                return;
            }
            var visual = player.transform.Find("Visual");
            var cap    = player.GetComponent<CapsuleCollider2D>();
            var rb     = player.GetComponent<Rigidbody2D>();
            if (visual == null || cap == null || rb == null)
            {
                Debug.LogError($"[Px32HeroWiring] Player 구성 누락 (Visual={visual != null}, Capsule={cap != null}, Rigidbody2D={rb != null}) — 중단");
                return;
            }
            var heroIdle = AssetDatabase.LoadAllAssetsAtPath($"{SheetDir}/Hero_Idle.png")
                .OfType<Sprite>().FirstOrDefault(sp => sp.name == "Hero_Idle_0");
            var visualSr = visual.GetComponent<SpriteRenderer>();
            if (heroIdle == null || visualSr == null)
            {
                Debug.LogError($"[Px32HeroWiring] Hero_Idle_0={(heroIdle != null)}, Visual SpriteRenderer={(visualSr != null)} — 중단");
                return;
            }

            // 3) 주인공 클립: 빈 경로 m_Sprite 바인딩 → "Visual", 이어서 재배선
            FixEmptyPathSpriteBindings();
            if (!SpriteClipRebinder.RunApplyHero())
            {
                Debug.LogError("[Px32HeroWiring] 주인공 클립 재배선에서 건너뛴 항목이 있음 — Logs/px32-rebind.txt 확인. 씬/컨트롤러는 변경하지 않고 중단.");
                return;
            }

            // 콜라이더 바닥 (로컬) — 이전 값 기준. 재실행 시에도 새 값 기준으로 같은 결과(멱등)
            float bottom = cap.offset.y - cap.size.y * 0.5f;
            Vector2 oldSize = cap.size, oldOffset = cap.offset;
            Vector3 oldScale = visual.localScale, oldPos = visual.localPosition;
            var oldInterp = rb.interpolation;

            cap.size   = NewColliderSize;
            cap.offset = new Vector2(oldOffset.x, bottom + NewColliderSize.y * 0.5f);
            visual.localScale    = new Vector3(1f, 1f, oldScale.z);
            visual.localPosition = new Vector3(0f, bottom, oldPos.z);
            rb.interpolation     = RigidbodyInterpolation2D.Interpolate;

            var oldSprite = visualSr.sprite;
            visualSr.sprite = heroIdle;

            EditorUtility.SetDirty(visualSr);
            EditorUtility.SetDirty(cap);
            EditorUtility.SetDirty(visual);
            EditorUtility.SetDirty(rb);
            ApplyCameraAndGauge(player);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[Px32HeroWiring] 콜라이더 바닥(로컬) y={bottom:F5} 유지. " +
                      $"Capsule size {oldSize} → {cap.size}, offset {oldOffset} → {cap.offset}. " +
                      $"Visual scale {oldScale} → {visual.localScale}, pos {oldPos} → {visual.localPosition}. " +
                      $"Rigidbody2D interpolation {oldInterp} → {rb.interpolation}. effectScale 미변경. " +
                      $"Visual sprite {(oldSprite != null ? oldSprite.name : "null")} → {heroIdle.name}.");

            // 4) 컨트롤러: Land state speed + 모든 state writeDefaultValues=false
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (ctrl == null)
            {
                Debug.LogError($"[Px32HeroWiring] 컨트롤러 없음: {ControllerPath}");
                return;
            }
            bool found = false;
            foreach (var layer in ctrl.layers)
                foreach (var cs in layer.stateMachine.states)
                {
                    if (cs.state.writeDefaultValues)
                        Debug.Log($"[Px32HeroWiring] Lord_Controller {cs.state.name} writeDefaultValues True → False");
                    cs.state.writeDefaultValues = false;
                    if (cs.state.name == "Land")
                    {
                        Debug.Log($"[Px32HeroWiring] Lord_Controller Land speed {cs.state.speed} → 1");
                        cs.state.speed = 1f;
                        found = true;
                    }
                }
            if (!found) Debug.LogWarning("[Px32HeroWiring] Lord_Controller 에서 Land state 를 찾지 못함 (최상위 state machine 만 검색)");
            EditorUtility.SetDirty(ctrl);

            // 5) 저장
            AssetDatabase.SaveAssets();
            Debug.Log("[Px32HeroWiring] 완료 — 씬/컨트롤러/클립 저장.");
        }

        /// <summary>vcam PositionComposer TargetOffset.y 와 DashGaugeUI offset 을 새 키에 맞춘다 (이전 값 로그). 없으면 경고만.</summary>
        private static void ApplyCameraAndGauge(PlayerController player)
        {
            var composer = Object.FindFirstObjectByType<Unity.Cinemachine.CinemachinePositionComposer>();
            if (composer != null)
            {
                var old = composer.TargetOffset;
                composer.TargetOffset = new Vector3(old.x, CameraTargetOffsetY, old.z);
                EditorUtility.SetDirty(composer);
                Debug.Log($"[Px32HeroWiring] vcam TargetOffset {old} → {composer.TargetOffset}");
            }
            else Debug.LogWarning("[Px32HeroWiring] CinemachinePositionComposer 없음 — 카메라 오프셋 미변경");

            var gauge = player.GetComponentInChildren<DashGaugeUI>(true);
            if (gauge == null) gauge = Object.FindFirstObjectByType<DashGaugeUI>();
            if (gauge != null)
            {
                var so   = new SerializedObject(gauge);
                var prop = so.FindProperty("offset");
                if (prop != null)
                {
                    Debug.Log($"[Px32HeroWiring] DashGaugeUI offset {prop.vector2Value} → {DashGaugeOffset}");
                    prop.vector2Value = DashGaugeOffset;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            else Debug.LogWarning("[Px32HeroWiring] DashGaugeUI 없음 — 게이지 오프셋 미변경");
        }

        private static readonly string[] HeroClips =
            { "Idle", "Run", "Jump_Start", "Jump_Loop", "Fall_Loop", "Land", "Dash", "Lord_Hit", "Death" };

        /// <summary>주인공 클립의 SpriteRenderer.m_Sprite 바인딩 경로가 ""(루트)이면 "Visual" 로 옮긴다 (커브 유지). 이미 고쳐졌으면 no-op.</summary>
        private static void FixEmptyPathSpriteBindings()
        {
            foreach (var name in HeroClips)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"Assets/_Project/ResourcceEX/Animations/{name}.anim");
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
                    Debug.Log($"[Px32HeroWiring] {name} m_Sprite 바인딩 경로 \"\" → \"Visual\" ({keys?.Length ?? 0}키)");
                }
            }
        }
    }
}
