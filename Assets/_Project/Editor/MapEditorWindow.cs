using System.IO;
using UnityEditor;
using UnityEngine;

namespace ReTrap.EditorTools
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  MapEditorWindow — 커스텀 맵 에디터 창
    //  3-1 : 창 골격 + 팔레트 영속화
    //  3-2 : 그리드 렌더링 (색·마커·선)
    //  3-3 : 펜 + Undo
    //  3-4 : 슬롯·마커·채움·지우개
    //  3-5 : 저장 · 불러오기 · 새 맵 · 크기 변경
    //  3-6 : 마우스 휠 줌 · 중간버튼 패닝 · 스프라이트 오버레이  ← 이번 단계
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// JSON 기반 맵을 그리드로 편집하는 에디터 창.
    /// <para>메뉴 <b>ReTrap → Map Editor</b> 로 엽니다.</para>
    /// </summary>
    public class MapEditorWindow : EditorWindow
    {
        // ── EditorPrefs 키 ────────────────────────────────────────────────────

        private const string PrefKeyPaletteGuid = "ReTrap.MapEditor.PaletteGuid";

        // ── 편집 상태 ─────────────────────────────────────────────────────────

        private MapDocument       _doc;      // Undo 대상 래퍼
        private TilePaletteConfig _palette;  // 스프라이트/색 참조

        private bool _isDirty;

        private const float SidebarWidth = 190f;

        // ── 캔버스 / 줌 / 패닝 ───────────────────────────────────────────────

        private float   _cellSize    = 24f;          // 셀 한 변 픽셀
        private Vector2 _canvasScroll;               // 스크롤 위치

        private const float GridLinePadding = 8f;
        private const float MinCellSize     = 8f;
        private const float MaxCellSize     = 80f;
        private const float ZoomSpeed       = 2.0f;  // 휠 1틱당 픽셀 증감

        private bool  _isPanning;                    // 중간버튼 드래그 중
        private Rect  _viewRect;                     // 현재 프레임의 캔버스 뷰 영역

        // ── 색상 팔레트 ───────────────────────────────────────────────────────

        private static readonly Color ColGridLine = new Color(0f,   0f,   0f,   0.25f);
        private static readonly Color ColCanvasBg = new Color(0.15f,0.15f,0.17f,1f);
        private static readonly Color ColSpawn    = new Color(0.2f, 0.9f, 0.3f, 1f);
        private static readonly Color ColGoal     = new Color(1f,   0.85f,0.2f, 1f);
        private static readonly Color ColHover    = new Color(1f,   1f,   1f,   0.25f);

        // ── 브러시 / 입력 상태 ────────────────────────────────────────────────

        private enum BrushMode  { Tile, Slot, Marker }
        private enum ActiveTool { Pen, Fill, Eraser }

        private BrushMode  _brushMode    = BrushMode.Tile;
        private TileType   _brushTile    = TileType.Floor;
        private TrapAnchor _brushAnchor  = TrapAnchor.Floor;
        private ActiveTool _activeTool   = ActiveTool.Pen;
        private bool       _markerIsGoal;

        private bool _isPainting;
        private int  _hoverX = -1, _hoverY = -1;

        // ── 크기 변경 입력 ────────────────────────────────────────────────────

        private int _resizeW = 24;
        private int _resizeH = 16;

        // ── 진입점 ────────────────────────────────────────────────────────────

        [MenuItem("ReTrap/Map Editor")]
        public static void Open()
        {
            var win = GetWindow<MapEditorWindow>("Map Editor");
            win.minSize = new Vector2(580f, 360f);
            win.Show();
        }

        // ── 생명주기 ──────────────────────────────────────────────────────────

        private void OnEnable()
        {
            EnsureDocument();
            LoadPaletteFromPrefs();
            Undo.undoRedoPerformed += OnUndoRedo;
            wantsMouseMove = true;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            if (_doc != null) DestroyImmediate(_doc);
        }

        private void OnUndoRedo() => Repaint();

        private void EnsureDocument()
        {
            if (_doc != null) return;
            _doc            = CreateInstance<MapDocument>();
            _doc.hideFlags  = HideFlags.DontSave;
            _resizeW        = _doc.map.width;
            _resizeH        = _doc.map.height;
        }

        // ── GUI ───────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            EnsureDocument();
            DrawTopBar();
            EditorGUILayout.BeginHorizontal();
            {
                DrawSidebar();
                DrawCanvas();
            }
            EditorGUILayout.EndHorizontal();
            DrawStatusBar();
        }

        // ── 상단 바 ───────────────────────────────────────────────────────────

        private void DrawTopBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                EditorGUI.BeginChangeCheck();
                var newPalette = (TilePaletteConfig)EditorGUILayout.ObjectField(
                    _palette, typeof(TilePaletteConfig), false, GUILayout.Width(200f));
                if (EditorGUI.EndChangeCheck())
                {
                    _palette = newPalette;
                    SavePaletteToPrefs();
                }

                GUILayout.Space(6f);
                GUILayout.Label("Map ID", GUILayout.Width(46f));
                EditorGUI.BeginChangeCheck();
                string newId = EditorGUILayout.TextField(_doc.map.mapId, GUILayout.Width(140f));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RegisterCompleteObjectUndo(_doc, "Rename Map ID");
                    _doc.map.mapId = newId;
                    MarkDirty();
                }

                GUILayout.Space(10f);

                if (GUILayout.Button("New",  EditorStyles.toolbarButton, GUILayout.Width(42f))) NewMap();
                if (GUILayout.Button("Load", EditorStyles.toolbarButton, GUILayout.Width(42f))) LoadMap();

                var prevBg = GUI.backgroundColor;
                if (_isDirty) GUI.backgroundColor = new Color(1f, 0.72f, 0.15f);
                if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(42f))) SaveMap();
                GUI.backgroundColor = prevBg;

                GUILayout.FlexibleSpace();

                if (_isDirty)
                {
                    var prev = GUI.color;
                    GUI.color = new Color(1f, 0.7f, 0.2f);
                    GUILayout.Label("● 미저장", EditorStyles.toolbarButton);
                    GUI.color = prev;
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        // ── 좌측 사이드바 ─────────────────────────────────────────────────────

        private void DrawSidebar()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(SidebarWidth));
            {
                // 타일 브러시
                EditorGUILayout.LabelField("타일", EditorStyles.boldLabel);
                DrawBrushButton("Empty",   () => SetTileBrush(TileType.Empty),
                    _brushMode == BrushMode.Tile && _brushTile == TileType.Empty,
                    PaletteColor(TileType.Empty));
                DrawBrushButton("Floor",   () => SetTileBrush(TileType.Floor),
                    _brushMode == BrushMode.Tile && _brushTile == TileType.Floor,
                    PaletteColor(TileType.Floor));
                DrawBrushButton("Wall",    () => SetTileBrush(TileType.Wall),
                    _brushMode == BrushMode.Tile && _brushTile == TileType.Wall,
                    PaletteColor(TileType.Wall));
                DrawBrushButton("Ceiling", () => SetTileBrush(TileType.Ceiling),
                    _brushMode == BrushMode.Tile && _brushTile == TileType.Ceiling,
                    PaletteColor(TileType.Ceiling));

                GUILayout.Space(6f);

                // 슬롯 브러시
                EditorGUILayout.LabelField("슬롯 (TrapAnchor)", EditorStyles.boldLabel);
                DrawBrushButton("▲ Floor",     () => SetSlotBrush(TrapAnchor.Floor),
                    _brushMode == BrushMode.Slot && _brushAnchor == TrapAnchor.Floor,    new Color(1f,0.5f,0f,0.8f));
                DrawBrushButton("▼ Ceiling",   () => SetSlotBrush(TrapAnchor.Ceiling),
                    _brushMode == BrushMode.Slot && _brushAnchor == TrapAnchor.Ceiling,  new Color(1f,0.5f,0f,0.8f));
                DrawBrushButton("◀ LeftWall",  () => SetSlotBrush(TrapAnchor.LeftWall),
                    _brushMode == BrushMode.Slot && _brushAnchor == TrapAnchor.LeftWall, new Color(1f,0.5f,0f,0.8f));
                DrawBrushButton("▶ RightWall", () => SetSlotBrush(TrapAnchor.RightWall),
                    _brushMode == BrushMode.Slot && _brushAnchor == TrapAnchor.RightWall,new Color(1f,0.5f,0f,0.8f));

                GUILayout.Space(6f);

                // 마커 브러시
                EditorGUILayout.LabelField("마커", EditorStyles.boldLabel);
                DrawBrushButton("S  Spawn", () => SetMarkerBrush(false),
                    _brushMode == BrushMode.Marker && !_markerIsGoal, ColSpawn);
                DrawBrushButton("G  Goal",  () => SetMarkerBrush(true),
                    _brushMode == BrushMode.Marker &&  _markerIsGoal, ColGoal);

                GUILayout.Space(8f);

                // 도구
                EditorGUILayout.LabelField("도구", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                {
                    DrawToolButton("✏ 펜",    ActiveTool.Pen);
                    DrawToolButton("⬛ 채움",  ActiveTool.Fill);
                    DrawToolButton("✕ 지우개", ActiveTool.Eraser);
                }
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(6f);

                // 전체 지우기
                var prevBg2 = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.35f, 0.35f);
                if (GUILayout.Button("🗑 전체 지우기", EditorStyles.miniButton))
                    ClearAll();
                GUI.backgroundColor = prevBg2;

                GUILayout.Space(10f);

                // 크기 변경
                EditorGUILayout.LabelField("크기 변경", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"현재  {_doc.map.width} × {_doc.map.height}",
                    EditorStyles.miniLabel);
                EditorGUILayout.BeginHorizontal();
                {
                    EditorGUILayout.LabelField("W", GUILayout.Width(14f));
                    _resizeW = EditorGUILayout.IntField(_resizeW, GUILayout.Width(38f));
                    GUILayout.Space(4f);
                    EditorGUILayout.LabelField("H", GUILayout.Width(14f));
                    _resizeH = EditorGUILayout.IntField(_resizeH, GUILayout.Width(38f));
                }
                EditorGUILayout.EndHorizontal();
                _resizeW = Mathf.Clamp(_resizeW, 4, 256);
                _resizeH = Mathf.Clamp(_resizeH, 4, 256);

                if (GUILayout.Button("크기 적용", EditorStyles.miniButton))
                    ApplyResize();

                GUILayout.Space(10f);

                // 줌 표시 + 리셋
                EditorGUILayout.LabelField("줌", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"{_cellSize:F0} px/cell", EditorStyles.miniLabel);
                if (GUILayout.Button("줌 리셋 (24px)", EditorStyles.miniButton))
                {
                    _cellSize = 24f;
                    Repaint();
                }
            }
            EditorGUILayout.EndVertical();
        }

        // ── 사이드바 헬퍼 ────────────────────────────────────────────────────

        private Color PaletteColor(TileType type)
            => _palette != null ? _palette.GetColor(type) : FallbackColor(type);

        private void SetTileBrush(TileType type)    { _brushMode = BrushMode.Tile;   _brushTile   = type; }
        private void SetSlotBrush(TrapAnchor anch)  { _brushMode = BrushMode.Slot;   _brushAnchor = anch; }
        private void SetMarkerBrush(bool isGoal)    { _brushMode = BrushMode.Marker; _markerIsGoal = isGoal; }

        private void DrawBrushButton(string label, System.Action onSelect, bool selected, Color swatch)
        {
            var prevBg = GUI.backgroundColor;
            if (selected) GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
            EditorGUILayout.BeginHorizontal();
            {
                var r = GUILayoutUtility.GetRect(14f, 14f, GUILayout.Width(14f));
                r.y += 2f;
                EditorGUI.DrawRect(r, swatch);
                if (GUILayout.Button((selected ? "● " : "○ ") + label, EditorStyles.miniButton))
                    onSelect();
            }
            EditorGUILayout.EndHorizontal();
            GUI.backgroundColor = prevBg;
        }

        private void DrawToolButton(string label, ActiveTool tool)
        {
            var prevBg = GUI.backgroundColor;
            if (_activeTool == tool) GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
            if (GUILayout.Button(label, EditorStyles.miniButton)) _activeTool = tool;
            GUI.backgroundColor = prevBg;
        }

        // ── 캔버스 ───────────────────────────────────────────────────────────

        private void DrawCanvas()
        {
            EditorGUILayout.BeginVertical();
            {
                _viewRect = GUILayoutUtility.GetRect(
                    100f, 100f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                EditorGUI.DrawRect(_viewRect, ColCanvasBg);

                var map = _doc.map;
                if (map == null || map.grid == null) { EditorGUILayout.EndVertical(); return; }

                // ── 줌 / 패닝: ScrollView 바깥에서 처리 (윈도우 좌표 사용) ──
                HandleZoomPan(map);

                float gridW      = map.width  * _cellSize;
                float gridH      = map.height * _cellSize;
                var contentRect  = new Rect(0f, 0f,
                    gridW + GridLinePadding * 2f,
                    gridH + GridLinePadding * 2f);

                // 스크롤 범위 클램프 (그리드 밖으로 패닝 방지)
                _canvasScroll.x = Mathf.Clamp(_canvasScroll.x, 0f,
                    Mathf.Max(0f, contentRect.width  - _viewRect.width));
                _canvasScroll.y = Mathf.Clamp(_canvasScroll.y, 0f,
                    Mathf.Max(0f, contentRect.height - _viewRect.height));

                _canvasScroll = GUI.BeginScrollView(_viewRect, _canvasScroll, contentRect,
                    false, false, GUIStyle.none, GUIStyle.none);
                {
                    var origin = new Vector2(GridLinePadding, GridLinePadding);
                    DrawGridCells(map, origin);
                    DrawMarkers(map, origin);
                    DrawGridLines(map, origin, gridW, gridH);
                    DrawHover(map, origin);
                    HandleCanvasInput(map, origin);
                }
                GUI.EndScrollView();

                // 줌 힌트 (우하단)
                DrawZoomHint();
            }
            EditorGUILayout.EndVertical();
        }

        // ── 줌 / 패닝 (ScrollView 바깥) ──────────────────────────────────────

        /// <summary>
        /// 마우스 휠 줌 (커서 기준) + 중간버튼 패닝.
        /// ScrollView 바깥에서 호출하므로 <c>e.mousePosition</c> 은 윈도우 좌표입니다.
        /// </summary>
        private void HandleZoomPan(MapData map)
        {
            Event e = Event.current;

            // 뷰 영역 밖이면 무시
            if (!_viewRect.Contains(e.mousePosition)) return;

            // ── 마우스 휠 줌 ──────────────────────────────────────────────────
            if (e.type == EventType.ScrollWheel)
            {
                float oldSize = _cellSize;

                // delta.y > 0 = 아래로 스크롤 = 축소
                float delta = -e.delta.y * ZoomSpeed;
                _cellSize = Mathf.Clamp(_cellSize + delta, MinCellSize, MaxCellSize);

                if (!Mathf.Approximately(_cellSize, oldSize))
                {
                    // 커서 위치가 고정되도록 스크롤 보정
                    // mouseInView: 뷰 좌상단 기준 마우스 위치
                    Vector2 mouseInView    = e.mousePosition - new Vector2(_viewRect.x, _viewRect.y);
                    // mouseInContent: 스크롤 콘텐츠 기준 마우스 위치
                    Vector2 mouseInContent = mouseInView + _canvasScroll;

                    float ratio       = _cellSize / oldSize;
                    _canvasScroll     = mouseInContent * ratio - mouseInView;

                    e.Use();
                    Repaint();
                }
            }

            // ── 중간버튼 패닝 ─────────────────────────────────────────────────
            if (e.type == EventType.MouseDown && e.button == 2)
            {
                _isPanning = true;
                e.Use();
            }
            if (e.type == EventType.MouseDrag && e.button == 2 && _isPanning)
            {
                _canvasScroll -= e.delta;
                e.Use();
                Repaint();
            }
            if (e.type == EventType.MouseUp && e.button == 2 && _isPanning)
            {
                _isPanning = false;
                e.Use();
            }
        }

        // ── 줌 힌트 오버레이 ─────────────────────────────────────────────────

        private void DrawZoomHint()
        {
            // 뷰 우하단 힌트 (Ctrl+휠, 중간버튼 조작법 안내)
            string hint = $"🔍 {_cellSize:F0}px  |  휠: 줌  |  중간버튼: 패닝";
            var style = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight,
                normal    = { textColor = new Color(0.8f, 0.8f, 0.8f, 0.6f) }
            };
            var r = new Rect(_viewRect.x, _viewRect.yMax - 18f, _viewRect.width - 6f, 16f);
            GUI.Label(r, hint, style);
        }

        // ── 그리드 셀 렌더 ───────────────────────────────────────────────────

        private void DrawGridCells(MapData map, Vector2 origin)
        {
            for (int y = 0; y < map.height; y++)
            {
                for (int x = 0; x < map.width; x++)
                {
                    TileType type = map.GetTile(x, y);
                    Rect     rect = CellRect(map, origin, x, y);

                    // ① 폴백 색상 블록 (스프라이트 없을 때도 색으로 구분)
                    EditorGUI.DrawRect(rect, PaletteColor(type));

                    // ② 스프라이트 오버레이
                    if (_palette != null)
                    {
                        Sprite sp = _palette.GetSprite(type);
                        if (sp != null) DrawSpriteCell(rect, sp);
                    }
                }
            }

            // ③ 슬롯 오버레이
            foreach (var slot in map.trapSlots)
            {
                if (!map.InBounds(slot.x, slot.y)) continue;
                Rect  rect = CellRect(map, origin, slot.x, slot.y);
                Color tint = _palette != null
                    ? _palette.GetSlotTint(slot.Anchor)
                    : new Color(1f, 0.5f, 0f, 0.5f);

                // 반투명 색 오버레이
                EditorGUI.DrawRect(rect, tint);
                DrawRectOutline(rect, tint * 1.4f, 1f);

                // 팔레트 슬롯 스프라이트 → 없으면 방향 기호 텍스트
                bool drewSprite = false;
                if (_palette != null)
                {
                    Sprite slotSp = _palette.GetSlotSprite(slot.Anchor);
                    if (slotSp != null)
                    {
                        DrawSpriteCell(rect, slotSp);
                        drewSprite = true;
                    }
                }

                if (!drewSprite)
                {
                    string arrow = slot.Anchor switch
                    {
                        TrapAnchor.Floor     => "▲",
                        TrapAnchor.Ceiling   => "▼",
                        TrapAnchor.LeftWall  => "◀",
                        TrapAnchor.RightWall => "▶",
                        _                    => "?"
                    };
                    var labelStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize  = Mathf.Max(8, (int)(_cellSize * 0.45f)),
                        normal    = { textColor = Color.white }
                    };
                    GUI.Label(rect, arrow, labelStyle);
                }
            }
        }

        /// <summary>
        /// Sprite 를 지정 Rect 에 UV 좌표를 계산해 그립니다.
        /// 스프라이트 아틀라스에서 잘라낸 경우에도 올바른 영역만 표시됩니다.
        /// </summary>
        private static void DrawSpriteCell(Rect rect, Sprite sprite)
        {
            if (sprite == null || sprite.texture == null) return;

            Texture2D tex = sprite.texture;
            Rect      tr  = sprite.textureRect;

            // 아틀라스 내 정규화 UV
            var uv = new Rect(
                tr.x      / tex.width,
                tr.y      / tex.height,
                tr.width  / tex.width,
                tr.height / tex.height);

            // Unity IMGUI는 Y 아래쪽이므로 UV를 뒤집어야 스프라이트가 정방향으로 보임
            GUI.DrawTextureWithTexCoords(rect, tex, uv, true);
        }

        // ── 마커 렌더 ─────────────────────────────────────────────────────────

        private void DrawMarkers(MapData map, Vector2 origin)
        {
            DrawMarkerCell(map, origin, map.spawnPoint.x, map.spawnPoint.y, "S", ColSpawn);
            DrawMarkerCell(map, origin, map.goalPoint.x,  map.goalPoint.y,  "G", ColGoal);
        }

        private void DrawMarkerCell(MapData map, Vector2 origin, int x, int y, string label, Color col)
        {
            if (!map.InBounds(x, y)) return;
            Rect rect  = CellRect(map, origin, x, y);
            Color fill = col; fill.a = 0.35f;
            EditorGUI.DrawRect(rect, fill);
            DrawRectOutline(rect, col, 2f);

            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = Color.white },
                fontSize  = Mathf.Max(8, (int)(_cellSize * 0.4f))
            };
            GUI.Label(rect, label, style);
        }

        // ── 그리드선 ─────────────────────────────────────────────────────────

        private void DrawGridLines(MapData map, Vector2 origin, float gridW, float gridH)
        {
            // 셀이 너무 작으면 그리드선 생략 (8px 미만 = 렌더 노이즈)
            if (_cellSize < 8f) return;

            for (int x = 0; x <= map.width; x++)
                EditorGUI.DrawRect(
                    new Rect(origin.x + x * _cellSize, origin.y, 1f, gridH), ColGridLine);
            for (int y = 0; y <= map.height; y++)
                EditorGUI.DrawRect(
                    new Rect(origin.x, origin.y + y * _cellSize, gridW, 1f), ColGridLine);
        }

        // ── 호버 ─────────────────────────────────────────────────────────────

        private void DrawHover(MapData map, Vector2 origin)
        {
            if (!map.InBounds(_hoverX, _hoverY)) return;
            Rect rect = CellRect(map, origin, _hoverX, _hoverY);
            EditorGUI.DrawRect(rect, ColHover);
            DrawRectOutline(rect, Color.white, 1f);
        }

        // ── 마우스 입력 / 편집 (ScrollView 안쪽) ──────────────────────────────

        private bool TryGetCell(MapData map, Vector2 origin, Vector2 mouse,
                                out int cx, out int cy)
        {
            cx     = Mathf.FloorToInt((mouse.x - origin.x) / _cellSize);
            int rr = Mathf.FloorToInt((mouse.y - origin.y) / _cellSize);
            cy     = (map.height - 1) - rr;          // y 뒤집기 역변환
            return map.InBounds(cx, cy);
        }

        private void HandleCanvasInput(MapData map, Vector2 origin)
        {
            Event e       = Event.current;
            bool overCell = TryGetCell(map, origin, e.mousePosition, out int cx, out int cy);

            switch (e.type)
            {
                case EventType.MouseMove:
                    UpdateHover(overCell, cx, cy);
                    break;

                case EventType.MouseDown:
                    if (e.button == 0 && overCell)
                    {
                        BeginStroke();
                        ApplyBrush(map, cx, cy);
                        _isPainting = (_activeTool != ActiveTool.Fill);
                        UpdateHover(true, cx, cy);
                        e.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (e.button == 0 && _isPainting)
                    {
                        if (overCell) ApplyBrush(map, cx, cy);
                        UpdateHover(overCell, cx, cy);
                        e.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (e.button == 0 && _isPainting)
                    {
                        _isPainting = false;
                        e.Use();
                    }
                    break;
            }
        }

        private void UpdateHover(bool valid, int cx, int cy)
        {
            int nx = valid ? cx : -1, ny = valid ? cy : -1;
            if (nx != _hoverX || ny != _hoverY)
            {
                _hoverX = nx; _hoverY = ny;
                Repaint();
            }
        }

        // ── 브러시 적용 ───────────────────────────────────────────────────────

        private void BeginStroke()
        {
            string label = _activeTool == ActiveTool.Eraser ? "Erase"
                         : _activeTool == ActiveTool.Fill   ? $"Fill {_brushTile}"
                         : _brushMode  == BrushMode.Slot    ? $"Slot {_brushAnchor}"
                         : _brushMode  == BrushMode.Marker  ? (_markerIsGoal ? "Move Goal" : "Move Spawn")
                         : $"Paint {_brushTile}";
            Undo.RegisterCompleteObjectUndo(_doc, label);
        }

        private void ApplyBrush(MapData map, int x, int y)
        {
            switch (_activeTool)
            {
                case ActiveTool.Eraser: ApplyEraser(map, x, y); break;
                case ActiveTool.Fill:   ApplyFill(map, x, y);   break;
                default:                ApplyPen(map, x, y);    break;
            }
        }

        private void ApplyPen(MapData map, int x, int y)
        {
            switch (_brushMode)
            {
                case BrushMode.Tile:
                    if (map.GetTile(x, y) == _brushTile) return;
                    map.SetTile(x, y, _brushTile);
                    MarkDirty();
                    break;

                case BrushMode.Slot:
                    var existing = map.GetSlot(x, y);
                    if (existing == null || existing.Anchor != _brushAnchor)
                    {
                        map.ToggleSlot(x, y, _brushAnchor);
                        MarkDirty();
                    }
                    break;

                case BrushMode.Marker:
                    if (_markerIsGoal)
                    {
                        if (map.goalPoint.x == x && map.goalPoint.y == y) return;
                        map.goalPoint = new GridCoord(x, y);
                    }
                    else
                    {
                        if (map.spawnPoint.x == x && map.spawnPoint.y == y) return;
                        map.spawnPoint = new GridCoord(x, y);
                    }
                    MarkDirty();
                    break;
            }
        }

        private void ApplyEraser(MapData map, int x, int y)
        {
            bool changed = false;
            if (map.GetTile(x, y) != TileType.Empty)
            {
                map.SetTile(x, y, TileType.Empty);
                changed = true;
            }
            if (map.GetSlot(x, y) != null)
            {
                map.trapSlots.RemoveAll(s => s.x == x && s.y == y);
                changed = true;
            }
            if (changed) MarkDirty();
        }

        private void ApplyFill(MapData map, int startX, int startY)
        {
            if (_brushMode != BrushMode.Tile) return;

            TileType target = map.GetTile(startX, startY);
            if (target == _brushTile) return;

            var queue   = new System.Collections.Generic.Queue<(int, int)>();
            var visited = new System.Collections.Generic.HashSet<int>();
            queue.Enqueue((startX, startY));
            visited.Add(map.Index(startX, startY));

            int[] dx = {  0,  0, -1, 1 };
            int[] dy = {  1, -1,  0, 0 };

            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();
                map.SetTile(x, y, _brushTile);
                for (int i = 0; i < 4; i++)
                {
                    int nx = x + dx[i], ny = y + dy[i];
                    if (!map.InBounds(nx, ny)) continue;
                    int idx = map.Index(nx, ny);
                    if (visited.Contains(idx)) continue;
                    if (map.GetTile(nx, ny) != target) continue;
                    visited.Add(idx);
                    queue.Enqueue((nx, ny));
                }
            }
            MarkDirty();
        }

        // ── 저장 ─────────────────────────────────────────────────────────────

        private void SaveMap()
        {
            var map = _doc.map;
            if (!map.Validate(out string err))
            {
                EditorUtility.DisplayDialog("저장 실패", $"맵 검증 실패:\n\n{err}", "확인");
                return;
            }
            string dir = Path.Combine(Application.streamingAssetsPath, "Maps");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string path = Path.Combine(dir, $"{map.mapId}.json");
            File.WriteAllText(path, map.ToJson(), System.Text.Encoding.UTF8);
            AssetDatabase.Refresh();
            _isDirty = false;
            Repaint();
            Debug.Log($"[MapEditor] 저장 완료 → {path}");
        }

        // ── 불러오기 ─────────────────────────────────────────────────────────

        private void LoadMap()
        {
            if (_isDirty && !ConfirmDiscard("미저장 변경사항",
                "저장하지 않은 변경사항이 있습니다.\n계속 진행하면 변경사항이 사라집니다.",
                "계속")) return;

            string defaultDir = Path.Combine(Application.streamingAssetsPath, "Maps");
            if (!Directory.Exists(defaultDir)) defaultDir = Application.streamingAssetsPath;

            string path = EditorUtility.OpenFilePanel("맵 불러오기", defaultDir, "json");
            if (string.IsNullOrEmpty(path)) return;

            MapData loaded = MapData.FromJson(File.ReadAllText(path));
            if (loaded == null)
            {
                EditorUtility.DisplayDialog("불러오기 실패", "JSON 파싱에 실패했습니다.", "확인");
                return;
            }
            if (!loaded.Validate(out string err))
            {
                EditorUtility.DisplayDialog("불러오기 실패", $"맵 검증 실패:\n\n{err}", "확인");
                return;
            }

            Undo.RegisterCompleteObjectUndo(_doc, "Load Map");
            _doc.map = loaded;
            _resizeW = loaded.width;
            _resizeH = loaded.height;
            _isDirty = false;
            Repaint();
            Debug.Log($"[MapEditor] 불러오기 완료 ← {path}");
        }

        // ── 새 맵 ─────────────────────────────────────────────────────────────

        private void NewMap()
        {
            if (_isDirty && !ConfirmDiscard("새 맵 만들기",
                "저장하지 않은 변경사항이 있습니다.\n새 맵을 만들면 변경사항이 사라집니다.",
                "새 맵 생성")) return;

            int w = Mathf.Clamp(_resizeW, 4, 256);
            int h = Mathf.Clamp(_resizeH, 4, 256);
            Undo.RegisterCompleteObjectUndo(_doc, "New Map");
            _doc.map = MapData.CreateEmpty(w, h);
            _resizeW = w; _resizeH = h;
            _isDirty = false;
            Repaint();
            Debug.Log($"[MapEditor] 새 맵 생성 {w}×{h}");
        }

        // ── 크기 변경 ─────────────────────────────────────────────────────────

        private void ApplyResize()
        {
            int w = Mathf.Clamp(_resizeW, 4, 256);
            int h = Mathf.Clamp(_resizeH, 4, 256);
            if (w == _doc.map.width && h == _doc.map.height)
            {
                Debug.Log("[MapEditor] 크기가 동일합니다 — 건너뜀");
                return;
            }
            Undo.RegisterCompleteObjectUndo(_doc, $"Resize {w}×{h}");
            _doc.map.Resize(w, h);
            _resizeW = w; _resizeH = h;
            MarkDirty();
            Debug.Log($"[MapEditor] 크기 변경 → {w}×{h}");
        }

        // ── 전체 지우기 ───────────────────────────────────────────────────────

        /// <summary>모든 타일을 Empty 로, 모든 슬롯을 제거합니다. Undo 가능.</summary>
        private void ClearAll()
        {
            if (!EditorUtility.DisplayDialog(
                    "전체 지우기",
                    "타일과 슬롯을 전부 지웁니다.\n(Ctrl+Z 로 되돌릴 수 있습니다)",
                    "지우기", "취소")) return;

            Undo.RegisterCompleteObjectUndo(_doc, "Clear All");

            var map = _doc.map;
            System.Array.Clear(map.grid, 0, map.grid.Length); // 전부 0 = Empty
            map.trapSlots.Clear();
            MarkDirty();
        }

        // ── 공통 확인 다이얼로그 ─────────────────────────────────────────────

        private static bool ConfirmDiscard(string title, string message, string ok)
            => EditorUtility.DisplayDialog(title, message, ok, "취소");

        // ── 상태바 ────────────────────────────────────────────────────────────

        private void DrawStatusBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                if (_doc.map.InBounds(_hoverX, _hoverY))
                {
                    var t    = _doc.map.GetTile(_hoverX, _hoverY);
                    var slot = _doc.map.GetSlot(_hoverX, _hoverY);
                    string slotInfo = slot != null ? slot.Anchor.ToString() : "없음";
                    GUILayout.Label($"셀({_hoverX},{_hoverY}) · {t} · 슬롯:{slotInfo}");
                }
                else GUILayout.Label("셀: —");

                GUILayout.Space(12f);
                GUILayout.Label($"맵 {_doc.map.width}×{_doc.map.height}");
                GUILayout.Space(12f);
                GUILayout.Label($"슬롯 {_doc.map.trapSlots.Count}개");
                GUILayout.Space(12f);
                GUILayout.Label($"줌 {_cellSize:F0}px");
                GUILayout.FlexibleSpace();
                GUILayout.Label(_palette == null ? "팔레트: 없음" : $"팔레트: {_palette.name}");
            }
            EditorGUILayout.EndHorizontal();
        }

        // ── Dirty 추적 ────────────────────────────────────────────────────────

        private void MarkDirty() { _isDirty = true; Repaint(); }

        // ── 유틸 ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 셀(x,y) → 스크롤 콘텐츠 내 Rect.
        /// originBottomLeft 규약: 데이터 y=0 이 화면 아래쪽에 오도록 y 뒤집기.
        /// </summary>
        private Rect CellRect(MapData map, Vector2 origin, int x, int y)
        {
            float sx = origin.x + x * _cellSize;
            float sy = origin.y + (map.height - 1 - y) * _cellSize;
            return new Rect(sx, sy, _cellSize, _cellSize);
        }

        private static void DrawRectOutline(Rect r, Color col, float t)
        {
            EditorGUI.DrawRect(new Rect(r.x,        r.y,        r.width, t),        col);
            EditorGUI.DrawRect(new Rect(r.x,        r.yMax - t, r.width, t),        col);
            EditorGUI.DrawRect(new Rect(r.x,        r.y,        t,       r.height), col);
            EditorGUI.DrawRect(new Rect(r.xMax - t, r.y,        t,       r.height), col);
        }

        private static Color FallbackColor(TileType type)
        {
            switch (type)
            {
                case TileType.Floor:   return new Color(0.55f, 0.38f, 0.24f);
                case TileType.Wall:    return new Color(0.45f, 0.45f, 0.48f);
                case TileType.Ceiling: return new Color(0.28f, 0.28f, 0.32f);
                default:               return new Color(0.18f, 0.18f, 0.20f);
            }
        }

        // ── 팔레트 EditorPrefs 영속화 ────────────────────────────────────────

        private void SavePaletteToPrefs()
        {
            if (_palette == null) { EditorPrefs.DeleteKey(PrefKeyPaletteGuid); return; }
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(_palette));
            EditorPrefs.SetString(PrefKeyPaletteGuid, guid);
        }

        private void LoadPaletteFromPrefs()
        {
            string guid = EditorPrefs.GetString(PrefKeyPaletteGuid, string.Empty);
            if (string.IsNullOrEmpty(guid)) return;
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return;
            _palette = AssetDatabase.LoadAssetAtPath<TilePaletteConfig>(path);
        }
    }
}
