using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ReTrap.EditorTools
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  MetaUiSetup — 스테이지 미리보기 + 연구소 UI 자동 생성/배선
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 메뉴 <b>ReTrap → Setup → 메타 UI 세팅 (미리보기 + 연구소)</b>:
    /// <list type="number">
    ///   <item>씬 공유 UICanvas 확보(없으면 <see cref="TrapPrefabBuilder"/> 와 동일 규격으로 생성)</item>
    ///   <item><see cref="StagePreviewPanel"/> 오브젝트 생성/배선 (좌상단, Build 페이즈에만 표시)</item>
    ///   <item><see cref="ResearchBoardPanel"/> 오브젝트 생성/배선 (화면 중앙, R 키로 토글)</item>
    /// </list>
    /// 이미 생성돼 있으면 참조만 재배선하므로 여러 번 실행해도 안전합니다(멱등).
    /// 씬 저장은 하지 않습니다 — 호출자가 직접 저장할 것.
    /// </summary>
    public static class MetaUiSetup
    {
        private const string ResearchDir = "Assets/_Project/Settings/Meta/Research";
        private const string SpawnTablePath = "Assets/_Project/Settings/AI/SpawnTables/SpawnTable_Stage01.asset";

        private static readonly Color PanelBackground = new Color(0.06f, 0.06f, 0.09f, 0.85f);
        private static readonly Color RowBackground   = new Color(0.12f, 0.12f, 0.16f, 0.9f);

        [MenuItem("ReTrap/Setup/메타 UI 세팅 (미리보기 + 연구소)")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("메타 UI 세팅",
                    "플레이 모드에서는 실행할 수 없습니다.", "확인");
                return;
            }

            GameObject uiCanvas = EnsureUiCanvas();
            EnsureEventSystem();

            ResearchNodeDefinition[] nodes  = LoadResearchNodes();
            StageSpawnTable          table  = AssetDatabase.LoadAssetAtPath<StageSpawnTable>(SpawnTablePath);
            if (table == null)
                Debug.LogWarning($"[MetaUiSetup] 스폰 테이블을 찾지 못했습니다: {SpawnTablePath}");
            if (nodes.Length == 0)
                Debug.LogWarning($"[MetaUiSetup] 연구 노드를 찾지 못했습니다: {ResearchDir} — " +
                                  "ReTrap/Setup/연구소 노드 에셋 생성 을 먼저 실행하세요.");

            BuildStagePreviewPanel(uiCanvas.transform, table, nodes);
            BuildResearchBoardPanel(uiCanvas.transform, nodes);

            Debug.Log("[MetaUiSetup] ✅ 메타 UI 세팅 완료 (미리보기 + 연구소)");
        }

        // ── 데이터 로드 ──────────────────────────────────────────────────────

        /// <summary>Research 폴더의 노드 에셋 전부를 경로순으로 로드(두 패널이 같은 순서를 쓰도록).</summary>
        private static ResearchNodeDefinition[] LoadResearchNodes()
        {
            string[] guids = AssetDatabase.FindAssets("t:ResearchNodeDefinition", new[] { ResearchDir });
            return guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => p)
                .Select(AssetDatabase.LoadAssetAtPath<ResearchNodeDefinition>)
                .Where(n => n != null)
                .ToArray();
        }

        // ── 씬 공유 UICanvas / EventSystem ───────────────────────────────────
        // TrapPrefabBuilder.EnsureUiCanvas 와 동일 규격 (오브젝트 하나만 공유하도록 이름 고정).

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
            Debug.Log("[MetaUiSetup] 씬에 UICanvas 생성 (공유 UI 루트)");
            return go;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;

            var go = new GameObject("EventSystem",
                typeof(EventSystem), typeof(InputSystemUIInputModule));
            EditorSceneManager.MarkSceneDirty(go.scene);
            Debug.Log("[MetaUiSetup] EventSystem (InputSystemUIInputModule) 추가");
        }

        // ── StagePreviewPanel ────────────────────────────────────────────────

        private static void BuildStagePreviewPanel(Transform canvasTransform,
            StageSpawnTable table, ResearchNodeDefinition[] nodes)
        {
            Transform existing = canvasTransform.Find("StagePreviewPanel");
            bool isNew = existing == null;

            GameObject panelGo;
            if (isNew)
            {
                panelGo = new GameObject("StagePreviewPanel", typeof(RectTransform), typeof(Image));
                panelGo.transform.SetParent(canvasTransform, false);

                var rect = (RectTransform)panelGo.transform;
                rect.anchorMin        = new Vector2(0f, 1f);
                rect.anchorMax        = new Vector2(0f, 1f);
                rect.pivot            = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(12f, -12f);
                rect.sizeDelta        = new Vector2(420f, 220f);

                panelGo.GetComponent<Image>().color = PanelBackground;
            }
            else
            {
                panelGo = existing.gameObject;
            }

            Text previewText = FindOrCreateText(panelGo.transform, "PreviewText", "",
                13, FontStyle.Normal, Color.white, TextAnchor.UpperLeft);
            var textRect = (RectTransform)previewText.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 8f);
            textRect.offsetMax = new Vector2(-10f, -8f);
            previewText.horizontalOverflow = HorizontalWrapMode.Wrap;
            previewText.verticalOverflow   = VerticalWrapMode.Overflow;

            var panel = panelGo.GetComponent<StagePreviewPanel>();
            if (panel == null) panel = panelGo.AddComponent<StagePreviewPanel>();

            var so = new SerializedObject(panel);
            so.FindProperty("spawnTable").objectReferenceValue = table;

            var nodesProp = so.FindProperty("researchNodes");
            nodesProp.arraySize = nodes.Length;
            for (int i = 0; i < nodes.Length; i++)
                nodesProp.GetArrayElementAtIndex(i).objectReferenceValue = nodes[i];

            so.FindProperty("previewText").objectReferenceValue = previewText;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(panelGo.scene);
            Debug.Log(isNew
                ? "[MetaUiSetup] StagePreviewPanel 생성 및 배선 완료"
                : "[MetaUiSetup] StagePreviewPanel 참조 재배선 완료");
        }

        // ── ResearchBoardPanel ───────────────────────────────────────────────

        private static void BuildResearchBoardPanel(Transform canvasTransform, ResearchNodeDefinition[] nodes)
        {
            Transform existing = canvasTransform.Find("ResearchBoardPanel");
            bool isNew = existing == null;

            GameObject rootGo;
            if (isNew)
            {
                rootGo = new GameObject("ResearchBoardPanel", typeof(RectTransform), typeof(Image));
                rootGo.transform.SetParent(canvasTransform, false);

                var rect = (RectTransform)rootGo.transform;
                rect.anchorMin        = new Vector2(0.5f, 0.5f);
                rect.anchorMax        = new Vector2(0.5f, 0.5f);
                rect.pivot            = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta        = new Vector2(520f, 420f);

                rootGo.GetComponent<Image>().color = PanelBackground;
            }
            else
            {
                rootGo = existing.gameObject;
            }

            // ── 제목 / 잔액 ──────────────────────────────────────────────────
            Text title = FindOrCreateText(rootGo.transform, "Title",
                "지구 평평 협회 연구소  [R] 닫기", 22, FontStyle.Bold,
                new Color(1f, 0.9f, 0.4f), TextAnchor.MiddleCenter);
            AnchorTop(title.rectTransform, yOffset: -14f, height: 28f);

            Text currencyText = FindOrCreateText(rootGo.transform, "CurrencyText",
                "박살 난 지구본: 0", 16, FontStyle.Normal,
                Color.white, TextAnchor.MiddleCenter);
            AnchorTop(currencyText.rectTransform, yOffset: -46f, height: 22f);

            // ── 노드 6행 ─────────────────────────────────────────────────────
            const int rowCount = 6;
            Transform rows = rootGo.transform.Find("Rows");
            if (rows == null)
            {
                var rowsGo = new GameObject("Rows",
                    typeof(RectTransform), typeof(VerticalLayoutGroup));
                rowsGo.transform.SetParent(rootGo.transform, false);
                rows = rowsGo.transform;

                var rowsRect = (RectTransform)rows;
                rowsRect.anchorMin        = new Vector2(0.5f, 1f);
                rowsRect.anchorMax        = new Vector2(0.5f, 1f);
                rowsRect.pivot            = new Vector2(0.5f, 1f);
                rowsRect.anchoredPosition = new Vector2(0f, -76f);
                rowsRect.sizeDelta        = new Vector2(490f, 236f);

                var vlayout = rowsGo.GetComponent<VerticalLayoutGroup>();
                vlayout.spacing                = 4f;
                vlayout.childControlWidth      = true;
                vlayout.childControlHeight     = false;
                vlayout.childForceExpandWidth  = true;
                vlayout.childForceExpandHeight = false;
                vlayout.childAlignment         = TextAnchor.UpperCenter;
            }

            var nodeLabels     = new List<Text>(rowCount);
            var upgradeButtons = new List<Button>(rowCount);

            for (int i = 0; i < rowCount; i++)
            {
                string rowName = $"Row_{i}";
                Transform rowTf = rows.Find(rowName);
                GameObject rowGo;
                if (rowTf == null)
                {
                    rowGo = new GameObject(rowName,
                        typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup),
                        typeof(LayoutElement));
                    rowGo.transform.SetParent(rows, false);

                    rowGo.GetComponent<Image>().color = RowBackground;

                    var hlayout = rowGo.GetComponent<HorizontalLayoutGroup>();
                    hlayout.padding                = new RectOffset(8, 8, 4, 4);
                    hlayout.spacing                = 6f;
                    // childControlWidth = true 여야 자식의 LayoutElement.preferredWidth 가
                    // 실제 RectTransform 크기에 반영된다(false 면 위치만 잡고 크기는 그대로 둠).
                    hlayout.childControlWidth      = true;
                    hlayout.childControlHeight     = true;
                    hlayout.childForceExpandWidth  = false;
                    hlayout.childForceExpandHeight = true;
                    hlayout.childAlignment         = TextAnchor.MiddleLeft;

                    rowGo.GetComponent<LayoutElement>().preferredHeight = 36f;

                    // Rows 의 VerticalLayoutGroup 이 childControlHeight=false 라 높이를 직접
                    // 강제하지 않으므로, 기본 RectTransform 크기를 미리 맞춰둔다.
                    ((RectTransform)rowGo.transform).sizeDelta = new Vector2(490f, 36f);
                }
                else
                {
                    rowGo = rowTf.gameObject;
                }

                Text label = FindOrCreateText(rowGo.transform, "Label", "",
                    13, FontStyle.Normal, Color.white, TextAnchor.MiddleLeft);
                var labelLayout = label.GetComponent<LayoutElement>();
                if (labelLayout == null) labelLayout = label.gameObject.AddComponent<LayoutElement>();
                labelLayout.preferredWidth = 380f;
                // LayoutGroup 이 크기를 아직 재계산하지 않은 상태에서도(에디터 즉시 확인) 올바르게
                // 보이도록 RectTransform 크기를 직접 지정 — LayoutElement 값과 일치시킨다.
                ((RectTransform)label.transform).sizeDelta = new Vector2(380f, 28f);
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow   = VerticalWrapMode.Overflow;

                Button button = FindOrCreateButton(rowGo.transform, "UpgradeButton", "투자", 84f, 28f);

                nodeLabels.Add(label);
                upgradeButtons.Add(button);
            }

            // ── 테스트 재화 지급 버튼 ────────────────────────────────────────
            // 레이아웃 그룹 밖(rootGo 직속)이라 LayoutElement 크기가 자동 반영되지 않으므로
            // sizeDelta 를 직접 지정한다.
            Button debugButton = FindOrCreateButton(rootGo.transform, "DebugGrantButton",
                "테스트 재화 +100", 160f, 30f);
            var debugRect = (RectTransform)debugButton.transform;
            debugRect.anchorMin        = new Vector2(0.5f, 0f);
            debugRect.anchorMax        = new Vector2(0.5f, 0f);
            debugRect.pivot            = new Vector2(0.5f, 0f);
            debugRect.anchoredPosition = new Vector2(0f, 14f);
            debugRect.sizeDelta        = new Vector2(160f, 30f);

            // ── 컴포넌트 부착 + 배선 ─────────────────────────────────────────
            // ⚠️ 컴포넌트는 반드시 '항상 활성'인 별도 컨트롤러 오브젝트에 붙인다.
            // rootGo(=panelRoot)는 SetActive(false)로 껐다 켜지므로, 컴포넌트를 여기 붙이면
            // 비활성 상태에서 Update(R키 감지)가 돌지 않아 패널을 영영 열 수 없다.
            Transform controllerTf = canvasTransform.Find("ResearchBoardController");
            GameObject controllerGo = controllerTf != null
                ? controllerTf.gameObject
                : new GameObject("ResearchBoardController", typeof(RectTransform));
            if (controllerTf == null) controllerGo.transform.SetParent(canvasTransform, false);

            // 마이그레이션: 과거 버전이 rootGo(패널)에 직접 붙였던 낡은 컴포넌트를 제거해
            // 컨트롤러와 중복되지 않게 한다 (rootGo 는 비활성이라 낡은 것은 동작하지 않지만 정리).
            var staleOnRoot = rootGo.GetComponent<ResearchBoardPanel>();
            if (staleOnRoot != null) Object.DestroyImmediate(staleOnRoot);

            var panel = controllerGo.GetComponent<ResearchBoardPanel>();
            if (panel == null) panel = controllerGo.AddComponent<ResearchBoardPanel>();

            var so = new SerializedObject(panel);

            var nodesProp = so.FindProperty("nodes");
            nodesProp.arraySize = nodes.Length;
            for (int i = 0; i < nodes.Length; i++)
                nodesProp.GetArrayElementAtIndex(i).objectReferenceValue = nodes[i];

            so.FindProperty("currencyText").objectReferenceValue = currencyText;

            var labelsProp = so.FindProperty("nodeLabels");
            labelsProp.arraySize = nodeLabels.Count;
            for (int i = 0; i < nodeLabels.Count; i++)
                labelsProp.GetArrayElementAtIndex(i).objectReferenceValue = nodeLabels[i];

            var buttonsProp = so.FindProperty("upgradeButtons");
            buttonsProp.arraySize = upgradeButtons.Count;
            for (int i = 0; i < upgradeButtons.Count; i++)
                buttonsProp.GetArrayElementAtIndex(i).objectReferenceValue = upgradeButtons[i];

            so.FindProperty("panelRoot").objectReferenceValue = rootGo;
            so.ApplyModifiedPropertiesWithoutUndo();

            // DebugGrantCurrency 버튼은 런타임 AddListener 방식이 아니라(ResearchBoardPanel 은
            // 자기 버튼 배열만 런타임 배선) 별도 버튼이므로, 에디터에서 영구 리스너로 연결.
            // 재실행 시 중복 연결을 막기 위해 이미 배선돼 있으면 건너뛴다.
            EnsurePersistentListener(debugButton, panel, panel.DebugGrantCurrency);

            // 시작 시 비활성 (Build 페이즈에서 R 로 열림)
            rootGo.SetActive(false);

            EditorSceneManager.MarkSceneDirty(rootGo.scene);
            Debug.Log(isNew
                ? "[MetaUiSetup] ResearchBoardPanel 생성 및 배선 완료"
                : "[MetaUiSetup] ResearchBoardPanel 참조 재배선 완료");
        }

        // ── UI 생성 헬퍼 ─────────────────────────────────────────────────────

        private static Text FindOrCreateText(Transform parent, string name, string content,
            int size, FontStyle style, Color color, TextAnchor alignment)
        {
            Transform tf = parent.Find(name);
            GameObject go;
            if (tf == null)
            {
                go = new GameObject(name, typeof(RectTransform), typeof(Text));
                go.transform.SetParent(parent, false);
            }
            else
            {
                go = tf.gameObject;
            }

            var text = go.GetComponent<Text>();
            if (string.IsNullOrEmpty(text.text) && !string.IsNullOrEmpty(content))
                text.text = content;
            text.font          = BuildHudController.BuiltinFont();
            text.fontSize      = size;
            text.fontStyle     = style;
            text.color         = color;
            text.alignment     = alignment;
            text.raycastTarget = false;
            return text;
        }

        private static Button FindOrCreateButton(Transform parent, string name, string label,
            float width, float height)
        {
            Transform tf = parent.Find(name);
            GameObject go;
            if (tf == null)
            {
                go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button),
                    typeof(LayoutElement));
                go.transform.SetParent(parent, false);
            }
            else
            {
                go = tf.gameObject;
            }

            var image = go.GetComponent<Image>();
            image.color = new Color(0.2f, 0.45f, 0.25f, 1f);

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;

            var layout = go.GetComponent<LayoutElement>();
            if (layout == null) layout = go.AddComponent<LayoutElement>();
            layout.preferredWidth  = width;
            layout.preferredHeight = height;

            // LayoutGroup 자식이든 아니든(디버그 버튼처럼 레이아웃 그룹 밖에 있든) 항상 올바르게
            // 보이도록 RectTransform 크기도 직접 지정 — LayoutElement 값과 일치시킨다.
            ((RectTransform)go.transform).sizeDelta = new Vector2(width, height);

            var labelText = FindOrCreateText(go.transform, "Label", label,
                14, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            var labelRect = (RectTransform)labelText.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            return button;
        }

        /// <summary>
        /// 상단 기준 가로 스트레치 배치 (title/currency 텍스트용).
        /// anchorMin/Max 를 (0,1)-(1,1) 로 스트레치하고 offsetMin/Max 만으로 위치·높이를 정하므로
        /// anchoredPosition/sizeDelta 와 섞어 쓰지 않는다.
        /// </summary>
        private static void AnchorTop(RectTransform rect, float yOffset, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = new Vector2(10f, yOffset - height);
            rect.offsetMax = new Vector2(-10f, yOffset);
        }

        /// <summary>
        /// 버튼 onClick 에 영구 리스너를 멱등하게 연결한다 (같은 대상+메서드가 이미 있으면 건너뜀).
        /// </summary>
        private static void EnsurePersistentListener(Button button, UnityEngine.Object target,
            UnityEngine.Events.UnityAction call)
        {
            string methodName = call.Method.Name;
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            {
                if (button.onClick.GetPersistentTarget(i) == target &&
                    button.onClick.GetPersistentMethodName(i) == methodName)
                    return; // 이미 배선됨
            }
            UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, call);
        }
    }
}
