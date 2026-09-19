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
    /// 메뉴 <b>ReTrap → Setup → 6. 메타 UI 세팅 (미리보기 + 연구소)</b>:
    /// <list type="number">
    ///   <item>씬 공유 UICanvas 확보(없으면 <see cref="TrapPrefabBuilder"/> 와 동일 규격으로 생성)</item>
    ///   <item><see cref="StagePreviewPanel"/> 오브젝트 생성/배선 (좌상단, Build 페이즈에만 표시)</item>
    ///   <item><see cref="ResearchBoardPanel"/> 오브젝트 생성/배선 (화면 중앙, R 키로 토글)</item>
    ///   <item><see cref="PhaseControlsGuide"/> 오브젝트 생성/배선 (화면 하단, 전 페이즈 상시 표시 +
    ///         Build 전용 "시작" 버튼)</item>
    ///   <item><see cref="StageClearPanel"/> 오브젝트 생성/배선 (화면 중앙, 스테이지 클리어 시
    ///         <see cref="StageFlowController"/> 이벤트로 표시) + <see cref="StageFlowController"/>
    ///         로직 오브젝트 보장</item>
    /// </list>
    /// 이미 생성돼 있으면 참조만 재배선하므로 여러 번 실행해도 안전합니다(멱등).
    /// 씬 저장은 하지 않습니다 — 호출자가 직접 저장할 것.
    /// </summary>
    public static class MetaUiSetup
    {
        private const string ResearchDir = "Assets/_Project/Settings/Meta/Research";
        private const string SpawnTablePath = "Assets/_Project/Settings/AI/SpawnTables/SpawnTable_Stage01.asset";

        // ── 색상 (튜닝용 — 값은 여기 한곳에 모아둠) ──────────────────────────
        private static readonly Color PanelBackground   = new Color(0.06f, 0.06f, 0.09f, 0.62f);
        private static readonly Color RowBackground     = new Color(0.14f, 0.14f, 0.19f, 0.92f);
        private static readonly Color AccentColor       = new Color(0.85f, 0.65f, 0.18f, 1f);   // 강조 띠/제목
        private static readonly Color TextTitleColor    = new Color(1f, 0.92f, 0.55f, 1f);
        private static readonly Color TextPrimaryColor  = Color.white;
        private static readonly Color TextMutedColor    = new Color(0.82f, 0.82f, 0.88f, 0.85f);
        private static readonly Color InvestButtonColor = new Color(0.2f, 0.45f, 0.25f, 1f);     // 초록 — 투자
        private static readonly Color DebugButtonColor  = new Color(0.35f, 0.32f, 0.16f, 1f);    // 어두운 금색 — 디버그

        // ── 크기 (튜닝용 — 값은 여기 한곳에 모아둠) ──────────────────────────
        private const float ScreenMargin        = 16f;

        private const float PreviewPanelWidth   = 360f;
        private const float PreviewPanelHeight  = 250f;
        private const int   PreviewTitleSize    = 19;
        private const int   PreviewBodySize     = 14;
        private const float PreviewTitleHeight  = 26f;
        private const float PreviewPadding      = 12f;

        private const float BoardWidth          = 560f;
        private const float BoardHeight         = 460f;
        private const float BoardTitleBarHeight = 44f;
        private const int   BoardTitleSize      = 20;
        private const int   BoardCurrencySize   = 16;
        private const float RowHeight           = 40f;
        private const float RowSpacing          = 6f;
        // rowWidth(528) - 행 좌우 패딩(16) - 라벨/버튼 간격(6) - 버튼 폭(InvestButtonWidth) = 416
        private const float RowLabelWidth       = 416f;
        private const float InvestButtonWidth   = 90f;
        private const float InvestButtonHeight  = 30f;
        private const float DebugButtonWidth    = 170f;
        private const float DebugButtonHeight   = 32f;

        private const float GuideBarHeight       = 44f;
        private const float GuideBarSideMargin   = 220f; // 좌우 여백 (시작 버튼과 겹치지 않도록)
        private const int   GuideTextSize        = 14;
        private const float StartButtonWidth     = 160f;
        private const float StartButtonHeight    = 40f;
        private static readonly Color GuideBarBackground = new Color(0.05f, 0.05f, 0.07f, 0.75f);
        private static readonly Color StartButtonColor   = new Color(0.2f, 0.5f, 0.28f, 1f); // 초록 — 진행

        private const float ClearPanelWidth      = 460f;
        private const float ClearPanelHeight     = 300f;
        private const float ClearTitleBarHeight  = 44f;
        private const int   ClearTitleSize       = 22;
        private const int   ClearBodySize        = 16;
        private const int   ClearErrorSize       = 14;
        private const float ClearButtonWidth     = 170f;
        private const float ClearButtonHeight    = 40f;
        private const float ClearButtonSpacing   = 16f;
        private static readonly Color ClearErrorColor       = new Color(0.95f, 0.35f, 0.3f, 1f);
        private static readonly Color ClearCloseButtonColor = new Color(0.3f, 0.3f, 0.34f, 1f); // 회색 — 중립(닫기)

        [MenuItem("ReTrap/Setup/6. 메타 UI 세팅 (미리보기 + 연구소)", false, 6)]
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
                                  "ReTrap/Setup/2. 연구소 노드 에셋 생성 (6종) 을 먼저 실행하세요.");

            BuildStagePreviewPanel(uiCanvas.transform, table, nodes);
            BuildResearchBoardPanel(uiCanvas.transform, nodes);
            BuildPhaseControlsGuide(uiCanvas.transform);
            BuildStageClearPanel(uiCanvas.transform);

            Debug.Log("[MetaUiSetup] ✅ 메타 UI 세팅 완료 (미리보기 + 연구소 + 조작 안내 + 스테이지 클리어)");
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
                panelGo = new GameObject("StagePreviewPanel", typeof(RectTransform));
                panelGo.transform.SetParent(canvasTransform, false);
            }
            else
            {
                panelGo = existing.gameObject;
            }

            // 좌상단 앵커, 화면 가장자리에서 여백을 두고 배치 (재실행 시에도 값 갱신).
            var rect = (RectTransform)panelGo.transform;
            rect.anchorMin        = new Vector2(0f, 1f);
            rect.anchorMax        = new Vector2(0f, 1f);
            rect.pivot            = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(ScreenMargin, -ScreenMargin);
            rect.sizeDelta        = new Vector2(PreviewPanelWidth, PreviewPanelHeight);

            // ── 표시 콘텐츠 컨테이너 ─────────────────────────────────────────
            // ⚠️ ResearchBoardPanel 과 같은 이유로, 배경/제목/본문은 별도 자식(PanelContent)에
            // 담아 그 자식만 SetActive 로 껐다 켠다. StagePreviewPanel 스크립트를 panelGo 자신에
            // 붙인 채 panelGo 를 직접 껐다 켜면, 비활성 전환 시 OnDisable 이 즉시 실행돼
            // GamePhaseManager.OnPhaseChanged 구독이 끊기고, 이후 Build 로 돌아와도 아무도
            // 다시 켜주지 않아 패널이 영영 숨은 채로 남는다 — 항상 활성 상태여야 하는 panelGo 에는
            // 스크립트만 두고, 실제 표시 여부는 이 자식 컨테이너로 제어한다.
            Transform contentTf = panelGo.transform.Find("PanelContent");
            GameObject contentGo;
            if (contentTf == null)
            {
                contentGo = new GameObject("PanelContent", typeof(RectTransform), typeof(Image));
                contentGo.transform.SetParent(panelGo.transform, false);
            }
            else
            {
                contentGo = contentTf.gameObject;
            }

            var contentRect = (RectTransform)contentGo.transform;
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;
            contentGo.GetComponent<Image>().color = PanelBackground;

            // 마이그레이션: 과거 버전이 panelGo 직속에 뒀던 배경 Image/Title/PreviewText 를
            // 새 컨테이너로 정리한다 (재실행 시 한 번만 동작, 이후엔 대상이 없어 no-op).
            var staleBackground = panelGo.GetComponent<Image>();
            if (staleBackground != null) Object.DestroyImmediate(staleBackground);
            MigrateChildIfPresent(panelGo.transform, contentGo.transform, "Title");
            MigrateChildIfPresent(panelGo.transform, contentGo.transform, "PreviewText");

            // ── 제목 (장식용 — 데이터 미배선, StagePreviewPanel 은 본문 텍스트 하나만 사용) ──
            Text title = FindOrCreateText(contentGo.transform, "Title", "스테이지 미리보기",
                PreviewTitleSize, FontStyle.Bold, TextTitleColor, TextAnchor.UpperLeft);
            var titleRect = (RectTransform)title.transform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot     = new Vector2(0.5f, 1f);
            titleRect.offsetMin = new Vector2(PreviewPadding, -(PreviewPadding + PreviewTitleHeight));
            titleRect.offsetMax = new Vector2(-PreviewPadding, -PreviewPadding);

            Text previewText = FindOrCreateText(contentGo.transform, "PreviewText", "",
                PreviewBodySize, FontStyle.Normal, TextPrimaryColor, TextAnchor.UpperLeft);
            var textRect = (RectTransform)previewText.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(PreviewPadding, PreviewPadding);
            textRect.offsetMax = new Vector2(-PreviewPadding, -(PreviewPadding + PreviewTitleHeight));
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
            so.FindProperty("panelRoot").objectReferenceValue = contentGo;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(panelGo.scene);
            Debug.Log(isNew
                ? "[MetaUiSetup] StagePreviewPanel 생성 및 배선 완료"
                : "[MetaUiSetup] StagePreviewPanel 참조 재배선 완료");
        }

        /// <summary>
        /// <paramref name="oldParent"/> 직속에 <paramref name="name"/> 자식이 있으면
        /// <paramref name="newParent"/> 밑으로 재배치한다(과거 버전 씬 구조 마이그레이션용,
        /// 이미 옮겨졌다면 대상이 없어 자동으로 no-op).
        /// </summary>
        private static void MigrateChildIfPresent(Transform oldParent, Transform newParent, string name)
        {
            Transform child = oldParent.Find(name);
            if (child != null)
                child.SetParent(newParent, false);
        }

        /// <summary>
        /// <paramref name="parent"/> 직속에 <paramref name="name"/> 자식이 있으면 파괴한다
        /// (과거 버전 씬 구조 잔재 정리용, 대상이 없으면 no-op).
        /// </summary>
        private static void RemoveStaleDirectChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null)
                Object.DestroyImmediate(child.gameObject);
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

                rootGo.GetComponent<Image>().color = PanelBackground;
            }
            else
            {
                rootGo = existing.gameObject;
            }

            // 화면 중앙 정렬 (재실행 시에도 값 갱신).
            var rect = (RectTransform)rootGo.transform;
            rect.anchorMin        = new Vector2(0.5f, 0.5f);
            rect.anchorMax        = new Vector2(0.5f, 0.5f);
            rect.pivot            = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta        = new Vector2(BoardWidth, BoardHeight);
            rootGo.GetComponent<Image>().color = PanelBackground;

            // 마이그레이션: TitleBar 도입 이전 버전은 패널 직속에 "Title"/"CurrencyText" 를
            // 만들었다. 지금 코드는 TitleBar 하위에만 같은 이름으로 만들기 때문에(아래 참조),
            // 과거 씬에 남은 패널 직속 잔재가 있으면 TitleBar 밖이라 그대로 겹쳐 보인다 —
            // 이름으로 패널 직속 자식만 찾아 제거한다(TitleBar 하위는 Find 가 직계만 보므로
            // 대상이 아니라 안전, 이미 정리됐다면 대상이 없어 no-op).
            RemoveStaleDirectChild(rootGo.transform, "Title");
            RemoveStaleDirectChild(rootGo.transform, "CurrencyText");

            // ── 제목 바 (상단 강조색 띠) ───────────────────────────────────────
            Transform titleBarTf = rootGo.transform.Find("TitleBar");
            GameObject titleBarGo = titleBarTf != null
                ? titleBarTf.gameObject
                : new GameObject("TitleBar", typeof(RectTransform), typeof(Image));
            if (titleBarTf == null) titleBarGo.transform.SetParent(rootGo.transform, false);

            var titleBarRect = (RectTransform)titleBarGo.transform;
            titleBarRect.anchorMin = new Vector2(0f, 1f);
            titleBarRect.anchorMax = new Vector2(1f, 1f);
            titleBarRect.pivot     = new Vector2(0.5f, 1f);
            titleBarRect.offsetMin = new Vector2(0f, -BoardTitleBarHeight);
            titleBarRect.offsetMax = Vector2.zero;
            titleBarGo.GetComponent<Image>().color = AccentColor;

            // 제목(좌측 정렬) — 우측에 잔액 표시 공간을 남겨둔다.
            Text title = FindOrCreateText(titleBarGo.transform, "Title",
                "지구 평평 협회 연구소", BoardTitleSize, FontStyle.Bold,
                new Color(0.1f, 0.08f, 0.02f, 1f), TextAnchor.MiddleLeft);
            var titleRect = (RectTransform)title.rectTransform;
            titleRect.anchorMin = Vector2.zero;
            titleRect.anchorMax = Vector2.one;
            titleRect.offsetMin = new Vector2(16f, 0f);
            titleRect.offsetMax = new Vector2(-190f, 0f);

            // 잔액 — 우상단(제목 바 우측) 눈에 띄게.
            Text currencyText = FindOrCreateText(titleBarGo.transform, "CurrencyText",
                "박살 난 지구본: 0", BoardCurrencySize, FontStyle.Bold,
                new Color(0.15f, 0.1f, 0.02f, 1f), TextAnchor.MiddleRight);
            var currencyRect = (RectTransform)currencyText.rectTransform;
            currencyRect.anchorMin        = new Vector2(1f, 0f);
            currencyRect.anchorMax        = new Vector2(1f, 1f);
            currencyRect.pivot            = new Vector2(1f, 0.5f);
            currencyRect.anchoredPosition = new Vector2(-14f, 0f);
            currencyRect.sizeDelta        = new Vector2(180f, 0f);

            // ── 노드 6행 ─────────────────────────────────────────────────────
            const int rowCount = 6;
            const float rowWidth = BoardWidth - ScreenMargin * 2f; // 좌우 여백 확보
            Transform rows = rootGo.transform.Find("Rows");
            if (rows == null)
            {
                var rowsGo = new GameObject("Rows",
                    typeof(RectTransform), typeof(VerticalLayoutGroup));
                rowsGo.transform.SetParent(rootGo.transform, false);
                rows = rowsGo.transform;

                var vlayout = rowsGo.GetComponent<VerticalLayoutGroup>();
                vlayout.spacing                = RowSpacing;
                vlayout.childControlWidth      = true;
                vlayout.childControlHeight     = false;
                vlayout.childForceExpandWidth  = true;
                vlayout.childForceExpandHeight = false;
                vlayout.childAlignment         = TextAnchor.UpperCenter;
            }

            var rowsRect = (RectTransform)rows;
            rowsRect.anchorMin        = new Vector2(0.5f, 1f);
            rowsRect.anchorMax        = new Vector2(0.5f, 1f);
            rowsRect.pivot            = new Vector2(0.5f, 1f);
            rowsRect.anchoredPosition = new Vector2(0f, -(BoardTitleBarHeight + 14f));
            rowsRect.sizeDelta        = new Vector2(rowWidth, rowCount * RowHeight + (rowCount - 1) * RowSpacing);

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
                }
                else
                {
                    rowGo = rowTf.gameObject;
                }

                rowGo.GetComponent<LayoutElement>().preferredHeight = RowHeight;
                // Rows 의 VerticalLayoutGroup 이 childControlHeight=false 라 높이를 직접
                // 강제하지 않으므로, 기본 RectTransform 크기를 미리 맞춰둔다.
                ((RectTransform)rowGo.transform).sizeDelta = new Vector2(rowWidth, RowHeight);

                Text label = FindOrCreateText(rowGo.transform, "Label", "",
                    13, FontStyle.Normal, TextPrimaryColor, TextAnchor.MiddleLeft);
                var labelLayout = label.GetComponent<LayoutElement>();
                if (labelLayout == null) labelLayout = label.gameObject.AddComponent<LayoutElement>();
                labelLayout.preferredWidth = RowLabelWidth;
                // LayoutGroup 이 크기를 아직 재계산하지 않은 상태에서도(에디터 즉시 확인) 올바르게
                // 보이도록 RectTransform 크기를 직접 지정 — LayoutElement 값과 일치시킨다.
                ((RectTransform)label.transform).sizeDelta = new Vector2(RowLabelWidth, RowHeight - 8f);
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow   = VerticalWrapMode.Overflow;

                Button button = FindOrCreateButton(rowGo.transform, "UpgradeButton", "투자",
                    InvestButtonWidth, InvestButtonHeight, InvestButtonColor);

                nodeLabels.Add(label);
                upgradeButtons.Add(button);
            }

            // ── 하단: 테스트 재화 지급 버튼 + 닫기 안내 ───────────────────────
            // 레이아웃 그룹 밖(rootGo 직속)이라 LayoutElement 크기가 자동 반영되지 않으므로
            // sizeDelta 를 직접 지정한다.
            Button debugButton = FindOrCreateButton(rootGo.transform, "DebugGrantButton",
                "테스트 재화 +100", DebugButtonWidth, DebugButtonHeight, DebugButtonColor);
            var debugRect = (RectTransform)debugButton.transform;
            debugRect.anchorMin        = new Vector2(0.5f, 0f);
            debugRect.anchorMax        = new Vector2(0.5f, 0f);
            debugRect.pivot            = new Vector2(0.5f, 0f);
            debugRect.anchoredPosition = new Vector2(0f, 44f);
            debugRect.sizeDelta        = new Vector2(DebugButtonWidth, DebugButtonHeight);

            Text closeHint = FindOrCreateText(rootGo.transform, "CloseHint", "[R] 닫기",
                13, FontStyle.Normal, TextMutedColor, TextAnchor.MiddleCenter);
            var closeHintRect = (RectTransform)closeHint.rectTransform;
            closeHintRect.anchorMin        = new Vector2(0.5f, 0f);
            closeHintRect.anchorMax        = new Vector2(0.5f, 0f);
            closeHintRect.pivot            = new Vector2(0.5f, 0f);
            closeHintRect.anchoredPosition = new Vector2(0f, 14f);
            closeHintRect.sizeDelta        = new Vector2(200f, 20f);

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

        // ── PhaseControlsGuide ───────────────────────────────────────────────

        /// <summary>
        /// 화면 하단 조작 안내 바 + Build 전용 "시작" 버튼을 생성/배선한다.
        /// StagePreviewPanel/ResearchBoardPanel 과 달리 이 바는 전 페이즈에서 항상 표시되므로
        /// (표시 여부는 <see cref="PhaseControlsGuide"/> 가 내부적으로 텍스트/버튼만 갈아끼움),
        /// rootGo 자체를 SetActive 로 끄지 않고 컴포넌트를 직접 붙인다.
        /// </summary>
        private static void BuildPhaseControlsGuide(Transform canvasTransform)
        {
            Transform existing = canvasTransform.Find("PhaseControlsGuide");
            bool isNew = existing == null;

            GameObject rootGo;
            if (isNew)
            {
                rootGo = new GameObject("PhaseControlsGuide", typeof(RectTransform));
                rootGo.transform.SetParent(canvasTransform, false);
            }
            else
            {
                rootGo = existing.gameObject;
            }

            // 하단 중앙, 좌우로 넓게 배치 (재실행 시에도 값 갱신).
            var rect = (RectTransform)rootGo.transform;
            rect.anchorMin        = new Vector2(0f, 0f);
            rect.anchorMax        = new Vector2(1f, 0f);
            rect.pivot            = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, ScreenMargin);
            rect.sizeDelta        = new Vector2(-ScreenMargin * 2f, GuideBarHeight);

            // ── 반투명 배경 ────────────────────────────────────────────────
            Transform bgTf = rootGo.transform.Find("Background");
            GameObject bgGo = bgTf != null
                ? bgTf.gameObject
                : new GameObject("Background", typeof(RectTransform), typeof(Image));
            if (bgTf == null) bgGo.transform.SetParent(rootGo.transform, false);

            var bgRect = (RectTransform)bgGo.transform;
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            var bgImage = bgGo.GetComponent<Image>();
            bgImage.color         = GuideBarBackground;
            bgImage.raycastTarget = false;

            // ── 조작 안내 텍스트 (중앙 정렬, 우측에 시작 버튼 자리 확보) ────
            Text guideText = FindOrCreateText(rootGo.transform, "GuideText", "",
                GuideTextSize, FontStyle.Normal, TextPrimaryColor, TextAnchor.MiddleCenter);
            var guideRect = (RectTransform)guideText.transform;
            guideRect.anchorMin = Vector2.zero;
            guideRect.anchorMax = Vector2.one;
            guideRect.offsetMin = new Vector2(20f, 0f);
            guideRect.offsetMax = new Vector2(-GuideBarSideMargin, 0f);
            guideText.horizontalOverflow = HorizontalWrapMode.Overflow;
            guideText.verticalOverflow   = VerticalWrapMode.Overflow;

            // ── 시작 버튼 (Build 페이즈에만 표시 — startButtonRoot 로 감쌈) ──
            Transform startRootTf = rootGo.transform.Find("StartButtonRoot");
            GameObject startRootGo = startRootTf != null
                ? startRootTf.gameObject
                : new GameObject("StartButtonRoot", typeof(RectTransform));
            if (startRootTf == null) startRootGo.transform.SetParent(rootGo.transform, false);

            var startRootRect = (RectTransform)startRootGo.transform;
            startRootRect.anchorMin        = new Vector2(1f, 0.5f);
            startRootRect.anchorMax        = new Vector2(1f, 0.5f);
            startRootRect.pivot            = new Vector2(1f, 0.5f);
            startRootRect.anchoredPosition = new Vector2(-16f, 0f);
            startRootRect.sizeDelta        = new Vector2(StartButtonWidth, StartButtonHeight);

            Button startButton = FindOrCreateButton(startRootGo.transform, "StartButton",
                "▶ 검증 시작", StartButtonWidth, StartButtonHeight, StartButtonColor);
            var startButtonRect = (RectTransform)startButton.transform;
            startButtonRect.anchorMin = Vector2.zero;
            startButtonRect.anchorMax = Vector2.one;
            startButtonRect.offsetMin = Vector2.zero;
            startButtonRect.offsetMax = Vector2.zero;

            // ── 컴포넌트 부착 + 배선 ─────────────────────────────────────────
            var guide = rootGo.GetComponent<PhaseControlsGuide>();
            if (guide == null) guide = rootGo.AddComponent<PhaseControlsGuide>();

            var so = new SerializedObject(guide);
            so.FindProperty("guideText").objectReferenceValue       = guideText;
            so.FindProperty("startButton").objectReferenceValue     = startButton;
            so.FindProperty("startButtonRoot").objectReferenceValue = startRootGo;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(rootGo.scene);
            Debug.Log(isNew
                ? "[MetaUiSetup] PhaseControlsGuide 생성 및 배선 완료"
                : "[MetaUiSetup] PhaseControlsGuide 참조 재배선 완료");
        }

        // ── StageClearPanel ──────────────────────────────────────────────────

        /// <summary>
        /// 화면 중앙 스테이지 클리어 결과 패널을 생성/배선하고, 씬에
        /// <see cref="StageFlowController"/>(로직 전담, UI 참조 없음) 오브젝트를 보장한다.
        /// ⚠️ 직전 세션에 구 레이아웃 잔재로 중복 위젯이 생긴 사고가 있었으므로, 컴포넌트는
        /// 반드시 항상 활성 상태인 rootGo 에 붙이고 실제 표시/숨김은 자식 "PanelContent"
        /// 로만 제어한다(ResearchBoardPanel/StagePreviewPanel 과 동일한 구조 규약).
        /// </summary>
        private static void BuildStageClearPanel(Transform canvasTransform)
        {
            Transform existing = canvasTransform.Find("StageClearPanel");
            bool isNew = existing == null;

            GameObject rootGo;
            if (isNew)
            {
                rootGo = new GameObject("StageClearPanel", typeof(RectTransform));
                rootGo.transform.SetParent(canvasTransform, false);
            }
            else
            {
                rootGo = existing.gameObject;
            }

            // 루트 자신은 캔버스 전체를 덮는 앵커만 담당 — 실제 배경/내용은 PanelContent 자식.
            var rootRect = (RectTransform)rootGo.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            // ── 콘텐츠 컨테이너 (표시/숨김 대상, panelRoot 로 배선됨) ────────
            Transform contentTf = rootGo.transform.Find("PanelContent");
            GameObject contentGo;
            if (contentTf == null)
            {
                contentGo = new GameObject("PanelContent", typeof(RectTransform), typeof(Image));
                contentGo.transform.SetParent(rootGo.transform, false);
            }
            else
            {
                contentGo = contentTf.gameObject;
            }

            var contentRect = (RectTransform)contentGo.transform;
            contentRect.anchorMin        = new Vector2(0.5f, 0.5f);
            contentRect.anchorMax        = new Vector2(0.5f, 0.5f);
            contentRect.pivot            = new Vector2(0.5f, 0.5f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta        = new Vector2(ClearPanelWidth, ClearPanelHeight);
            contentGo.GetComponent<Image>().color = PanelBackground;

            // ── 제목 바 ────────────────────────────────────────────────────
            Transform titleBarTf = contentGo.transform.Find("TitleBar");
            GameObject titleBarGo = titleBarTf != null
                ? titleBarTf.gameObject
                : new GameObject("TitleBar", typeof(RectTransform), typeof(Image));
            if (titleBarTf == null) titleBarGo.transform.SetParent(contentGo.transform, false);

            var titleBarRect = (RectTransform)titleBarGo.transform;
            titleBarRect.anchorMin = new Vector2(0f, 1f);
            titleBarRect.anchorMax = new Vector2(1f, 1f);
            titleBarRect.pivot     = new Vector2(0.5f, 1f);
            titleBarRect.offsetMin = new Vector2(0f, -ClearTitleBarHeight);
            titleBarRect.offsetMax = Vector2.zero;
            titleBarGo.GetComponent<Image>().color = AccentColor;

            Text titleText = FindOrCreateText(titleBarGo.transform, "TitleText", "스테이지 클리어",
                ClearTitleSize, FontStyle.Bold, new Color(0.1f, 0.08f, 0.02f, 1f), TextAnchor.MiddleCenter);
            var titleRect = (RectTransform)titleText.rectTransform;
            titleRect.anchorMin = Vector2.zero;
            titleRect.anchorMax = Vector2.one;
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            // ── 본문 텍스트 (보상 / 상태(전 스테이지 완주) / 오류) ───────────
            Text rewardText = FindOrCreateText(contentGo.transform, "RewardText", "",
                ClearBodySize, FontStyle.Bold, TextTitleColor, TextAnchor.MiddleCenter);
            var rewardRect = (RectTransform)rewardText.rectTransform;
            rewardRect.anchorMin        = new Vector2(0f, 1f);
            rewardRect.anchorMax        = new Vector2(1f, 1f);
            rewardRect.pivot            = new Vector2(0.5f, 1f);
            rewardRect.anchoredPosition = new Vector2(0f, -(ClearTitleBarHeight + 26f));
            rewardRect.sizeDelta        = new Vector2(-40f, 28f);

            Text statusText = FindOrCreateText(contentGo.transform, "StatusText", "",
                ClearBodySize, FontStyle.Normal, TextMutedColor, TextAnchor.MiddleCenter);
            var statusRect = (RectTransform)statusText.rectTransform;
            statusRect.anchorMin        = new Vector2(0f, 1f);
            statusRect.anchorMax        = new Vector2(1f, 1f);
            statusRect.pivot            = new Vector2(0.5f, 1f);
            statusRect.anchoredPosition = new Vector2(0f, -(ClearTitleBarHeight + 62f));
            statusRect.sizeDelta        = new Vector2(-40f, 26f);

            Text errorText = FindOrCreateText(contentGo.transform, "ErrorText", "",
                ClearErrorSize, FontStyle.Normal, ClearErrorColor, TextAnchor.MiddleCenter);
            var errorRect = (RectTransform)errorText.rectTransform;
            errorRect.anchorMin        = new Vector2(0f, 1f);
            errorRect.anchorMax        = new Vector2(1f, 1f);
            errorRect.pivot            = new Vector2(0.5f, 1f);
            errorRect.anchoredPosition = new Vector2(0f, -(ClearTitleBarHeight + 98f));
            errorRect.sizeDelta        = new Vector2(-40f, 44f);
            errorText.horizontalOverflow = HorizontalWrapMode.Wrap;
            errorText.verticalOverflow   = VerticalWrapMode.Overflow;

            // ── 버튼 행 (하단 중앙, 가로 정렬) ────────────────────────────
            Transform buttonRowTf = contentGo.transform.Find("ButtonRow");
            GameObject buttonRowGo;
            if (buttonRowTf == null)
            {
                buttonRowGo = new GameObject("ButtonRow",
                    typeof(RectTransform), typeof(HorizontalLayoutGroup));
                buttonRowGo.transform.SetParent(contentGo.transform, false);

                var hlayout = buttonRowGo.GetComponent<HorizontalLayoutGroup>();
                hlayout.spacing                = ClearButtonSpacing;
                hlayout.childControlWidth      = false;
                hlayout.childControlHeight     = false;
                hlayout.childForceExpandWidth  = false;
                hlayout.childForceExpandHeight = false;
                hlayout.childAlignment         = TextAnchor.MiddleCenter;
            }
            else
            {
                buttonRowGo = buttonRowTf.gameObject;
            }

            var buttonRowRect = (RectTransform)buttonRowGo.transform;
            buttonRowRect.anchorMin        = new Vector2(0.5f, 0f);
            buttonRowRect.anchorMax        = new Vector2(0.5f, 0f);
            buttonRowRect.pivot            = new Vector2(0.5f, 0f);
            buttonRowRect.anchoredPosition = new Vector2(0f, 20f);
            buttonRowRect.sizeDelta        = new Vector2(ClearPanelWidth - 40f, ClearButtonHeight);

            Button nextButton    = FindOrCreateButton(buttonRowGo.transform, "NextButton", "다음 스테이지",
                ClearButtonWidth, ClearButtonHeight, StartButtonColor);
            Button restartButton = FindOrCreateButton(buttonRowGo.transform, "RestartButton", "처음부터",
                ClearButtonWidth, ClearButtonHeight, StartButtonColor);
            Button closeButton   = FindOrCreateButton(buttonRowGo.transform, "CloseButton", "닫기",
                ClearButtonWidth, ClearButtonHeight, ClearCloseButtonColor);

            // ── 컴포넌트 부착 + 배선 (항상 활성인 rootGo 에 부착 — panelRoot 만 토글) ──
            var panel = rootGo.GetComponent<StageClearPanel>();
            if (panel == null) panel = rootGo.AddComponent<StageClearPanel>();

            var so = new SerializedObject(panel);
            so.FindProperty("panelRoot").objectReferenceValue     = contentGo;
            so.FindProperty("titleText").objectReferenceValue     = titleText;
            so.FindProperty("rewardText").objectReferenceValue    = rewardText;
            so.FindProperty("statusText").objectReferenceValue    = statusText;
            so.FindProperty("errorText").objectReferenceValue     = errorText;
            so.FindProperty("nextButton").objectReferenceValue    = nextButton;
            so.FindProperty("restartButton").objectReferenceValue = restartButton;
            so.FindProperty("closeButton").objectReferenceValue   = closeButton;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 시작 시 비활성 (StageClearPanel.Start 가 방어적으로도 강제하지만 에디터에서도 반영).
            contentGo.SetActive(false);

            EditorSceneManager.MarkSceneDirty(rootGo.scene);
            Debug.Log(isNew
                ? "[MetaUiSetup] StageClearPanel 생성 및 배선 완료"
                : "[MetaUiSetup] StageClearPanel 참조 재배선 완료");

            EnsureStageFlowController();
        }

        /// <summary>
        /// <see cref="StageFlowController"/> 는 UI 참조가 없는 순수 로직 싱글턴이므로
        /// 별도 오브젝트로 존재하면 충분하다 — 없으면 하나 생성한다(멱등, 이미 있으면 no-op).
        /// </summary>
        private static void EnsureStageFlowController()
        {
            if (Object.FindFirstObjectByType<StageFlowController>() != null) return;

            var go = new GameObject("StageFlowController", typeof(StageFlowController));
            EditorSceneManager.MarkSceneDirty(go.scene);
            Debug.Log("[MetaUiSetup] StageFlowController 오브젝트 생성");
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

        /// <summary>
        /// 버튼 하나를 멱등 생성/재사용. <paramref name="baseColor"/> 를 normal 색으로 삼아
        /// hover(밝게)/pressed(어둡게)/disabled(회색조) 상태 색상을 명시적으로 설정한다
        /// (눌리는 느낌을 위해 uGUI 기본 ColorBlock 을 그대로 쓰지 않고 직접 지정).
        /// </summary>
        private static Button FindOrCreateButton(Transform parent, string name, string label,
            float width, float height, Color baseColor)
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
            image.color = baseColor;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition    = Selectable.Transition.ColorTint;

            ColorBlock colors = button.colors;
            colors.normalColor      = baseColor;
            colors.highlightedColor = Color.Lerp(baseColor, Color.white, 0.25f);
            colors.pressedColor     = Color.Lerp(baseColor, Color.black, 0.35f);
            colors.selectedColor    = colors.highlightedColor;
            colors.disabledColor    = new Color(baseColor.grayscale, baseColor.grayscale, baseColor.grayscale, 0.5f);
            colors.colorMultiplier  = 1f;
            button.colors = colors;

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
