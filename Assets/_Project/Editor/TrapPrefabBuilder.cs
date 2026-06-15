using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ReTrap.EditorTools
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  TrapPrefabBuilder — 플레이스홀더 함정 프리팹 생성 + Build UI 씬 배선
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 메뉴 <b>ReTrap → Setup → 함정 프리팹 + Build UI 세팅</b>:
    /// <list type="number">
    ///   <item>SpikeTrap / ArrowShooter(+Arrow) / DropHammer 플레이스홀더 프리팹 생성</item>
    ///   <item>Managers 에 BuildPhaseController + TrapMutationManager 추가</item>
    ///   <item>프리팹 리스트 자동 할당 + 씬 저장</item>
    /// </list>
    /// 이미 있는 항목은 건너뛰므로 여러 번 실행해도 안전합니다.
    /// 아트 확정 후 프리팹의 Visual 스프라이트만 교체하면 됩니다.
    /// </summary>
    public static class TrapPrefabBuilder
    {
        private const string PrefabDir   = "Assets/_Project/ResourcceEX/Prefabs/Traps";
        private const string TileSprite  = "Assets/_Project/ResourcceEX/Sprites/Environment/CorruptedCastleTile.png";

        [MenuItem("ReTrap/Setup/함정 프리팹 + Build UI 세팅")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("함정 세팅",
                    "플레이 모드에서는 실행할 수 없습니다.", "확인");
                return;
            }

            EnsureFolder();
            Sprite sprite = LoadPlaceholderSprite();

            var arrow  = BuildArrowProjectile(sprite);
            var spike  = BuildSpikeTrap(sprite);
            var shooter= BuildArrowShooter(sprite, arrow);
            var hammer = BuildDropHammer(sprite);

            var hud      = BuildHudPrefab();
            var uiCanvas = EnsureUiCanvas();
            EnsureEventSystem();

            WireScene(new List<GameObject> { spike, shooter, hammer }, hud, uiCanvas);

            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[TrapPrefabBuilder] ✅ 함정 프리팹 + Build UI 세팅 완료");
        }

        // ── 폴더 / 스프라이트 ────────────────────────────────────────────────

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/ResourcceEX/Prefabs"))
                AssetDatabase.CreateFolder("Assets/_Project/ResourcceEX", "Prefabs");
            if (!AssetDatabase.IsValidFolder(PrefabDir))
                AssetDatabase.CreateFolder("Assets/_Project/ResourcceEX/Prefabs", "Traps");
        }

        private static Sprite LoadPlaceholderSprite()
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(TileSprite);
            if (s == null)
                Debug.LogWarning("[TrapPrefabBuilder] 타일 스프라이트를 찾지 못해 " +
                                 "스프라이트 없이 생성합니다 (색상 블록도 안 보일 수 있음).");
            return s;
        }

        // ── 프리팹 빌더 공통 ─────────────────────────────────────────────────

        private static GameObject SavePrefab(GameObject temp, string name)
        {
            string path = $"{PrefabDir}/{name}.prefab";
            var prefab  = PrefabUtility.SaveAsPrefabAsset(temp, path);
            Object.DestroyImmediate(temp);
            Debug.Log($"[TrapPrefabBuilder] 프리팹 생성 → {path}");
            return prefab;
        }

        private static GameObject ExistingPrefab(string name)
            => AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/{name}.prefab");

        private static SpriteRenderer AddVisual(GameObject root, Sprite sprite, Color tint,
                                                Vector2 scale, Vector2 localPos)
        {
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = localPos;
            visual.transform.localScale    = new Vector3(scale.x, scale.y, 1f);

            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite           = sprite;
            sr.color            = tint;
            sr.sortingLayerName = "Map"; // 타일과 같은 레이어라야 가려지지 않음
            sr.sortingOrder     = 10;    // 배경(-10)·타일(0) 위, 캐릭터(20) 아래
            return sr;
        }

        private static Rigidbody2D AddKinematicRb(GameObject root)
        {
            var rb = root.GetComponent<Rigidbody2D>();
            if (rb == null) rb = root.AddComponent<Rigidbody2D>();
            rb.bodyType     = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            return rb;
        }

        // ── 개별 프리팹 ───────────────────────────────────────────────────────

        /// <summary>Arrow 발사체 — Trigger + Rigidbody2D(중력 0).</summary>
        private static GameObject BuildArrowProjectile(Sprite sprite)
        {
            var existing = ExistingPrefab("Arrow");
            if (existing != null) return existing;

            var go = new GameObject("Arrow");
            AddVisual(go, sprite, new Color(0.9f, 0.9f, 0.3f), new Vector2(0.5f, 0.15f), Vector2.zero);

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size      = new Vector2(0.5f, 0.15f);

            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType     = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;

            go.AddComponent<Arrow>();
            return SavePrefab(go, "Arrow");
        }

        /// <summary>SpikeTrap — 셀 하단 가시. 루트 콜라이더가 damageArea 폴백.</summary>
        private static GameObject BuildSpikeTrap(Sprite sprite)
        {
            var existing = ExistingPrefab("SpikeTrap");
            if (existing != null) return existing;

            var go = new GameObject("SpikeTrap");
            AddKinematicRb(go);

            var sr = AddVisual(go, sprite, new Color(0.85f, 0.2f, 0.2f),
                               new Vector2(0.8f, 0.4f), new Vector2(0f, -0.3f));

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size      = new Vector2(0.8f, 0.4f);
            col.offset    = new Vector2(0f, -0.3f);

            var trap = go.AddComponent<SpikeTrap>();
            var so   = new SerializedObject(trap);
            so.FindProperty("spikeVisual").objectReferenceValue = sr.transform;
            so.FindProperty("baseCost").intValue                = 10;
            so.FindProperty("dangerLevel").intValue             = 3;
            so.ApplyModifiedPropertiesWithoutUndo();

            return SavePrefab(go, "SpikeTrap");
        }

        /// <summary>ArrowShooter — FirePoint 자식 + Arrow 프리팹 연결.</summary>
        private static GameObject BuildArrowShooter(Sprite sprite, GameObject arrowPrefab)
        {
            var existing = ExistingPrefab("ArrowShooter");
            if (existing != null) return existing;

            var go = new GameObject("ArrowShooter");
            AddKinematicRb(go);
            AddVisual(go, sprite, new Color(0.3f, 0.5f, 0.9f),
                      new Vector2(0.6f, 0.6f), Vector2.zero);

            // 발사 지점 (벽 바깥쪽으로 facingRight 에 따라 좌우 — 기본 오른쪽)
            var firePoint = new GameObject("FirePoint");
            firePoint.transform.SetParent(go.transform, false);
            firePoint.transform.localPosition = new Vector3(0.45f, 0f, 0f);

            // 루트 트리거 (플레이어 직접 접촉 감지용 — 슈터 본체는 데미지 없음이라 작게)
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size      = new Vector2(0.6f, 0.6f);

            var trap = go.AddComponent<ArrowShooter>();
            var so   = new SerializedObject(trap);
            so.FindProperty("arrowPrefab").objectReferenceValue = arrowPrefab;
            so.FindProperty("firePoint").objectReferenceValue   = firePoint.transform;
            so.FindProperty("baseCost").intValue                = 25;
            so.FindProperty("dangerLevel").intValue             = 5;
            so.ApplyModifiedPropertiesWithoutUndo();

            return SavePrefab(go, "ArrowShooter");
        }

        /// <summary>DropHammer — Ground/Player 레이어 마스크 자동 설정.</summary>
        private static GameObject BuildDropHammer(Sprite sprite)
        {
            var existing = ExistingPrefab("DropHammer");
            if (existing != null) return existing;

            var go = new GameObject("DropHammer");
            AddKinematicRb(go); // DropHammer 는 RequireComponent(Rigidbody2D)

            AddVisual(go, sprite, new Color(0.4f, 0.35f, 0.45f),
                      new Vector2(0.9f, 0.7f), Vector2.zero);

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size      = new Vector2(0.9f, 0.7f);

            int groundLayer = LayerMask.NameToLayer("Ground");
            int playerLayer = LayerMask.NameToLayer("Player");

            var trap = go.AddComponent<DropHammer>();
            var so   = new SerializedObject(trap);
            so.FindProperty("groundLayer").intValue   = groundLayer >= 0 ? (1 << groundLayer) : 0;
            so.FindProperty("detectionMask").intValue = playerLayer >= 0 ? (1 << playerLayer) : ~0;
            so.FindProperty("baseCost").intValue      = 50;
            so.FindProperty("dangerLevel").intValue   = 8;
            so.ApplyModifiedPropertiesWithoutUndo();

            return SavePrefab(go, "DropHammer");
        }

        // ── Build HUD 프리팹 (uGUI Canvas) ───────────────────────────────────

        private const string HudPrefabPath = "Assets/_Project/ResourcceEX/Prefabs/UI/BuildHud.prefab";

        /// <summary>
        /// Build HUD <b>핫바</b> 프리팹 생성 (Canvas 없음 — 씬의 UICanvas 아래에 로드됨).
        /// 하단 중앙 MMO 핫바 스타일. 슬롯 자체는 BuildHudController 가 런타임에 동적 생성합니다.
        /// </summary>
        private static GameObject BuildHudPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            if (existing != null)
            {
                // 구버전(Canvas 포함 루트 / 핫바 없는 좌상단 패널형)이면 재생성
                bool isLegacy = existing.GetComponent<Canvas>() != null
                             || existing.transform.Find("BuildPanel/Hotbar") == null;
                if (!isLegacy) return existing;
                AssetDatabase.DeleteAsset(HudPrefabPath);
                Debug.Log("[TrapPrefabBuilder] 구버전 HUD 삭제 — 하단 핫바형으로 재생성");
            }

            if (!AssetDatabase.IsValidFolder("Assets/_Project/ResourcceEX/Prefabs/UI"))
                AssetDatabase.CreateFolder("Assets/_Project/ResourcceEX/Prefabs", "UI");

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // ── 패널 루트 (RectTransform 전체 스트레치 — 부모 Canvas 에 맞춤) ──
            var root = new GameObject("BuildHud", typeof(RectTransform));
            var rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            // ── Build 패널 (페이즈 토글 단위 — 전체 스트레치 투명 컨테이너) ──
            var panel = new GameObject("BuildPanel", typeof(RectTransform));
            panel.transform.SetParent(root.transform, false);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            // ── 핫바 (하단 중앙 — 슬롯 수에 맞춰 가로 자동 확장) ─────────────
            var buttons = new GameObject("Hotbar",
                typeof(RectTransform), typeof(Image),
                typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            buttons.transform.SetParent(panel.transform, false);

            var hotbarRect = (RectTransform)buttons.transform;
            hotbarRect.anchorMin        = new Vector2(0.5f, 0f);
            hotbarRect.anchorMax        = new Vector2(0.5f, 0f);
            hotbarRect.pivot            = new Vector2(0.5f, 0f);
            hotbarRect.anchoredPosition = new Vector2(0f, 14f);

            buttons.GetComponent<Image>().color = new Color(0.04f, 0.04f, 0.06f, 0.85f);

            var hlayout = buttons.GetComponent<HorizontalLayoutGroup>();
            hlayout.padding                = new RectOffset(5, 5, 5, 5);
            hlayout.spacing                = 4f;
            hlayout.childControlWidth      = false;
            hlayout.childControlHeight     = false;
            hlayout.childForceExpandWidth  = false;
            hlayout.childForceExpandHeight = false;
            hlayout.childAlignment         = TextAnchor.MiddleCenter;

            var fitter = buttons.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

            // ── 예산 텍스트 (핫바 바로 위 중앙) ──────────────────────────────
            var budgetText = MakeText(panel.transform, "BudgetText", "예산  100",
                font, 20, FontStyle.Bold, new Color(1f, 0.9f, 0.4f), 24f);
            var budgetRect = (RectTransform)budgetText.transform;
            budgetRect.anchorMin        = new Vector2(0.5f, 0f);
            budgetRect.anchorMax        = new Vector2(0.5f, 0f);
            budgetRect.pivot            = new Vector2(0.5f, 0f);
            budgetRect.anchoredPosition = new Vector2(0f, 88f);
            budgetRect.sizeDelta        = new Vector2(300f, 24f);
            budgetText.alignment        = TextAnchor.MiddleCenter;

            // ── 힌트 텍스트 (예산 위 — 흐릿하게) ─────────────────────────────
            var hintText = MakeText(panel.transform, "HintText",
                "버튼/1~3: 선택 · 슬롯 클릭: 설치", font, 12, FontStyle.Normal,
                new Color(0.85f, 0.85f, 0.85f, 0.75f), 18f);
            var hintRect = (RectTransform)hintText.transform;
            hintRect.anchorMin        = new Vector2(0.5f, 0f);
            hintRect.anchorMax        = new Vector2(0.5f, 0f);
            hintRect.pivot            = new Vector2(0.5f, 0f);
            hintRect.anchoredPosition = new Vector2(0f, 114f);
            hintRect.sizeDelta        = new Vector2(760f, 18f);
            hintText.alignment        = TextAnchor.MiddleCenter;

            // ── Play 중 복귀 힌트 (패널 밖, 기본 비활성) ─────────────────────
            var playHint = MakeText(root.transform, "PlayHint",
                "[B] 빌드 페이즈로 돌아가기", font, 16, FontStyle.Bold,
                new Color(1f, 1f, 1f, 0.7f), 24f);
            var playRect = (RectTransform)playHint.transform;
            playRect.anchorMin        = new Vector2(0f, 1f);
            playRect.anchorMax        = new Vector2(0f, 1f);
            playRect.pivot            = new Vector2(0f, 1f);
            playRect.anchoredPosition = new Vector2(12f, -12f);
            playRect.sizeDelta        = new Vector2(360f, 26f);
            playHint.gameObject.SetActive(false);

            // ── 컨트롤러 연결 ────────────────────────────────────────────────
            var hudCtrl = root.AddComponent<BuildHudController>();
            var so = new SerializedObject(hudCtrl);
            so.FindProperty("panel").objectReferenceValue           = panel;
            so.FindProperty("budgetText").objectReferenceValue      = budgetText;
            so.FindProperty("buttonContainer").objectReferenceValue = buttons.transform;
            so.FindProperty("hintText").objectReferenceValue        = hintText;
            so.FindProperty("playHint").objectReferenceValue        = playHint.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, HudPrefabPath);
            Object.DestroyImmediate(root);
            Debug.Log($"[TrapPrefabBuilder] HUD 프리팹 생성 → {HudPrefabPath}");
            return prefab;
        }

        private static Text MakeText(Transform parent, string name, string content,
                                     Font font, int size, FontStyle style,
                                     Color color, float preferredHeight)
        {
            var go = new GameObject(name,
                typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<Text>();
            text.text          = content;
            text.font          = font;
            text.fontSize      = size;
            text.fontStyle     = style;
            text.color         = color;
            text.alignment     = TextAnchor.MiddleLeft;
            text.raycastTarget = false;

            go.GetComponent<LayoutElement>().preferredHeight = preferredHeight;
            return text;
        }

        // ── 씬 공유 UICanvas ─────────────────────────────────────────────────

        /// <summary>
        /// 씬에 영구 UI 루트 'UICanvas' 를 보장합니다.
        /// 모든 UI 패널 프리팹(BuildHud, 추후 HP바 등)이 이 아래에 로드됩니다.
        /// </summary>
        private static GameObject EnsureUiCanvas()
        {
            var existing = GameObject.Find("UICanvas");
            if (existing != null && existing.GetComponent<Canvas>() != null)
                return existing;

            var go = new GameObject("UICanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight  = 0.5f;

            EditorSceneManager.MarkSceneDirty(go.scene);
            Debug.Log("[TrapPrefabBuilder] 씬에 UICanvas 생성 (공유 UI 루트)");
            return go;
        }

        // ── EventSystem (uGUI 버튼 클릭 필수) ────────────────────────────────

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;

            var go = new GameObject("EventSystem",
                typeof(EventSystem), typeof(InputSystemUIInputModule));
            EditorSceneManager.MarkSceneDirty(go.scene);
            Debug.Log("[TrapPrefabBuilder] EventSystem (InputSystemUIInputModule) 추가");
        }

        // ── 씬 배선 ───────────────────────────────────────────────────────────

        private static void WireScene(List<GameObject> prefabs, GameObject hudPrefab,
                                      GameObject uiCanvas)
        {
            var managers = GameObject.Find("Managers");
            if (managers == null)
            {
                Debug.LogWarning("[TrapPrefabBuilder] 'Managers' 오브젝트가 없어 새로 만듭니다.");
                managers = new GameObject("Managers");
            }

            // TrapMutationManager (누락 시 추가)
            if (managers.GetComponent<TrapMutationManager>() == null)
            {
                managers.AddComponent<TrapMutationManager>();
                Debug.Log("[TrapPrefabBuilder] TrapMutationManager 추가");
            }

            // BuildPhaseController + 프리팹 리스트
            var ctrl = managers.GetComponent<BuildPhaseController>();
            if (ctrl == null) ctrl = managers.AddComponent<BuildPhaseController>();

            var so = new SerializedObject(ctrl);

            // 함정 프리팹 → 어드레서블 등록(Traps 그룹) 후 trapRefs[i] 에 GUID 배선
            var trapArr = so.FindProperty("trapRefs");
            trapArr.arraySize = prefabs.Count;
            for (int i = 0; i < prefabs.Count; i++)
            {
                string path = AssetDatabase.GetAssetPath(prefabs[i]);
                string guid = RegisterAddressable(path, "Traps", $"Trap/{prefabs[i].name}");
                SetAssetReferenceGuid(trapArr.GetArrayElementAtIndex(i), guid);
            }

            // HUD 프리팹 → 어드레서블 등록(UI 그룹) 후 hudRef 에 GUID 배선
            string hudPath = AssetDatabase.GetAssetPath(hudPrefab);
            string hudGuid = RegisterAddressable(hudPath, "UI", "UI/BuildHud");
            SetAssetReferenceGuid(so.FindProperty("hudRef"), hudGuid);

            so.FindProperty("uiRoot").objectReferenceValue = uiCanvas != null ? uiCanvas.transform : null;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(managers.scene);
            Debug.Log($"[TrapPrefabBuilder] BuildPhaseController 배선 완료 — 함정 {prefabs.Count}종 (어드레서블)");
        }

        // ── 어드레서블 헬퍼 ──────────────────────────────────────────────────

        /// <summary>
        /// 에셋을 지정 그룹에 어드레서블 등록하고 주소를 설정합니다. 그룹이 없으면 생성.
        /// 이미 등록돼 있으면 그룹·주소만 갱신(멱등). 등록한 에셋의 GUID 반환.
        /// </summary>
        private static string RegisterAddressable(string assetPath, string groupName, string address)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError("[TrapPrefabBuilder] AddressableAssetSettings 가 없습니다 — " +
                               "Window → Asset Management → Addressables → Groups 에서 초기화하세요.");
                return string.Empty;
            }

            var group = settings.FindGroup(groupName);
            if (group == null)
                group = settings.CreateGroup(groupName, false, false, false, null,
                                             typeof(UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema),
                                             typeof(UnityEditor.AddressableAssets.Settings.GroupSchemas.ContentUpdateGroupSchema));

            string guid  = AssetDatabase.AssetPathToGUID(assetPath);
            var    entry = settings.CreateOrMoveEntry(guid, group);
            entry.address = address;
            return guid;
        }

        /// <summary>AssetReference SerializedProperty 의 m_AssetGUID 를 설정.</summary>
        private static void SetAssetReferenceGuid(SerializedProperty refProp, string guid)
        {
            var guidProp = refProp.FindPropertyRelative("m_AssetGUID");
            if (guidProp != null) guidProp.stringValue = guid;
        }
    }
}
