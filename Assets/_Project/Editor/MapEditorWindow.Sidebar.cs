using UnityEditor;
using UnityEngine;

namespace ReTrap.EditorTools
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  MapEditorWindow.Sidebar — 상단 바 · 사이드바 · 상태바
    // ═══════════════════════════════════════════════════════════════════════════

    public partial class MapEditorWindow
    {
        // ── 상단 바 ───────────────────────────────────────────────────────────

        private void DrawTopBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                // 팔레트 설정 ObjectField
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
                    _doc.map.mapId = SanitizeMapId(newId);
                    MarkDirty();
                }

                GUILayout.Space(10f);

                if (GUILayout.Button("New",  EditorStyles.toolbarButton, GUILayout.Width(42f))) NewMap();
                if (GUILayout.Button("Load", EditorStyles.toolbarButton, GUILayout.Width(42f))) LoadMap();

                var prevBg = GUI.backgroundColor;
                if (_isDirty) GUI.backgroundColor = new Color(1f, 0.72f, 0.15f);
                if (GUILayout.Button("Save",    EditorStyles.toolbarButton, GUILayout.Width(42f))) SaveMap();
                GUI.backgroundColor = prevBg;
                if (GUILayout.Button("Save As", EditorStyles.toolbarButton, GUILayout.Width(56f))) SaveMapAs();

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
                // ── 편집 레이어 토글 (지형 / 배경) ────────────────────────────
                EditorGUILayout.LabelField("편집 레이어", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                {
                    DrawLayerButton("지형", EditLayer.Foreground);
                    DrawLayerButton("배경", EditLayer.Background);
                }
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(8f);

                if (_editLayer == EditLayer.Background)
                {
                    DrawBackgroundPalette();
                }
                else
                {
                // ── WHAT: 타일 ────────────────────────────────────────────────
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

                // ── WHAT: 슬롯 ────────────────────────────────────────────────
                EditorGUILayout.LabelField("슬롯 (TrapAnchor)", EditorStyles.boldLabel);
                Color slotSwatch = TilePaletteConfig.DefaultSlotTint;
                DrawBrushButton("▲ Floor",     () => SetSlotBrush(TrapAnchor.Floor),
                    _brushMode == BrushMode.Slot && _brushAnchor == TrapAnchor.Floor,     slotSwatch);
                DrawBrushButton("▼ Ceiling",   () => SetSlotBrush(TrapAnchor.Ceiling),
                    _brushMode == BrushMode.Slot && _brushAnchor == TrapAnchor.Ceiling,   slotSwatch);
                DrawBrushButton("◀ LeftWall",  () => SetSlotBrush(TrapAnchor.LeftWall),
                    _brushMode == BrushMode.Slot && _brushAnchor == TrapAnchor.LeftWall,  slotSwatch);
                DrawBrushButton("▶ RightWall", () => SetSlotBrush(TrapAnchor.RightWall),
                    _brushMode == BrushMode.Slot && _brushAnchor == TrapAnchor.RightWall, slotSwatch);

                GUILayout.Space(6f);

                // ── WHAT: 마커 ────────────────────────────────────────────────
                EditorGUILayout.LabelField("마커", EditorStyles.boldLabel);
                DrawBrushButton("S  Spawn", () => SetMarkerBrush(false),
                    _brushMode == BrushMode.Marker && !_markerIsGoal, ColSpawn);
                DrawBrushButton("G  Goal",  () => SetMarkerBrush(true),
                    _brushMode == BrushMode.Marker &&  _markerIsGoal, ColGoal);
                }

                GUILayout.Space(8f);

                // ── HOW: 도구 ─────────────────────────────────────────────────
                EditorGUILayout.LabelField("도구", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                {
                    DrawToolButton("✏ 펜",    ActiveTool.Pen);
                    DrawToolButton("⬛ 채움",  ActiveTool.Fill);
                    DrawToolButton("✕ 지우개", ActiveTool.Eraser);
                }
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(6f);

                // ── 전체 지우기 ───────────────────────────────────────────────
                var prevBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.35f, 0.35f);
                if (GUILayout.Button("🗑 전체 지우기", EditorStyles.miniButton))
                    ClearAll();
                GUI.backgroundColor = prevBg;

                GUILayout.Space(10f);

                // ── 크기 변경 ─────────────────────────────────────────────────
                EditorGUILayout.LabelField("크기 변경", EditorStyles.boldLabel);

                // 현재 맵 크기 + 비율 표시 (목표 비율과 일치하면 ✓)
                int curW = _doc.map.width, curH = _doc.map.height;
                EditorGUILayout.LabelField(
                    $"현재  {curW} × {curH}  ({RatioText(curW, curH)}){AspectMatchMark(curW, curH)}",
                    EditorStyles.miniLabel);

                // 화면 비율 프리셋 — 빌드 카메라가 맵에 딱 맞으려면 화면과 같은 비율로 제작
                EditorGUI.BeginChangeCheck();
                _aspect = (AspectPreset)EditorGUILayout.Popup("비율", (int)_aspect, AspectLabels);
                bool aspectChanged = EditorGUI.EndChangeCheck();

                float ratio = AspectValue(_aspect);

                EditorGUILayout.BeginHorizontal();
                {
                    EditorGUILayout.LabelField("W", GUILayout.Width(14f));
                    EditorGUI.BeginChangeCheck();
                    _resizeW = EditorGUILayout.IntField(_resizeW, GUILayout.Width(38f));
                    bool wEdited = EditorGUI.EndChangeCheck();

                    GUILayout.Space(4f);

                    // 비율 잠금(Free 아님) 시 H 는 자동 계산되므로 비활성 표시
                    EditorGUILayout.LabelField("H", GUILayout.Width(14f));
                    using (new EditorGUI.DisabledScope(ratio > 0f))
                    {
                        EditorGUI.BeginChangeCheck();
                        _resizeH = EditorGUILayout.IntField(_resizeH, GUILayout.Width(38f));
                        if (EditorGUI.EndChangeCheck() && ratio <= 0f) { /* Free: 수동 H */ }
                    }

                    // 비율 잠금: W 또는 비율이 바뀌면 H = round(W ÷ ratio)
                    if (ratio > 0f && (aspectChanged || wEdited))
                        _resizeH = Mathf.RoundToInt(_resizeW / ratio);
                }
                EditorGUILayout.EndHorizontal();

                _resizeW = Mathf.Clamp(_resizeW, 4, 256);
                _resizeH = Mathf.Clamp(_resizeH, 4, 256);

                if (ratio > 0f)
                    EditorGUILayout.LabelField($"→ {_resizeW} × {_resizeH} ({AspectLabels[(int)_aspect]})",
                        EditorStyles.miniLabel);

                if (GUILayout.Button("크기 적용", EditorStyles.miniButton))
                    ApplyResize();

                GUILayout.Space(10f);

                // ── 줌 ────────────────────────────────────────────────────────
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

        // ── 편집 레이어 / 배경 팔레트 ────────────────────────────────────────

        private void DrawLayerButton(string label, EditLayer layer)
        {
            var prevBg = GUI.backgroundColor;
            if (_editLayer == layer) GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
            if (GUILayout.Button(label, EditorStyles.miniButton))
            {
                _editLayer = layer;
                Repaint();
            }
            GUI.backgroundColor = prevBg;
        }

        /// <summary>배경 타일 썸네일 팔레트. 팔레트의 Background Tiles 를 그리드로 표시·선택.</summary>
        private void DrawBackgroundPalette()
        {
            EditorGUILayout.LabelField("배경 타일", EditorStyles.boldLabel);

            int count = _palette != null ? _palette.BackgroundTileCount : 0;
            if (count == 0)
            {
                EditorGUILayout.HelpBox(
                    "배경 타일이 없습니다.\nTilePaletteConfig 의 Background Tiles 에 스프라이트를 추가하세요.",
                    MessageType.Info);
                return;
            }

            const int   cols = 4;
            const float cell = 38f;

            // 타일이 많으면 세로 스크롤 (고정 높이 영역 안에서 스크롤)
            _bgPaletteScroll = EditorGUILayout.BeginScrollView(
                _bgPaletteScroll, GUILayout.Height(220f));
            {
                for (int i = 1; i <= count; i++) // 1-base (맵 background 인덱스)
                {
                    if ((i - 1) % cols == 0) EditorGUILayout.BeginHorizontal();

                    var r = GUILayoutUtility.GetRect(cell, cell, GUILayout.Width(cell), GUILayout.Height(cell));

                    // 선택 하이라이트 배경
                    EditorGUI.DrawRect(r, _brushBackground == i
                        ? new Color(0.4f, 0.7f, 1f, 0.6f)
                        : new Color(0f, 0f, 0f, 0.25f));

                    // 썸네일 (스프라이트 UV 그리기 — DrawSpriteCell 재사용)
                    var sp = _palette.GetBackgroundSprite(i);
                    if (sp != null)
                    {
                        var inner = new Rect(r.x + 3, r.y + 3, r.width - 6, r.height - 6);
                        DrawSpriteCell(inner, sp);
                    }

                    if (GUI.Button(r, GUIContent.none, GUIStyle.none))
                    {
                        _brushBackground = i;
                        Repaint();
                    }

                    if ((i - 1) % cols == cols - 1 || i == count) EditorGUILayout.EndHorizontal();
                }
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.LabelField($"선택: #{_brushBackground} / {count}", EditorStyles.miniLabel);
        }

        // ── 비율 표시 헬퍼 ────────────────────────────────────────────────────

        /// <summary>W:H 를 기약분수로 표시 (예: 24×16 → "3:2").</summary>
        private static string RatioText(int w, int h)
        {
            if (w <= 0 || h <= 0) return "-";
            int g = Gcd(w, h);
            return $"{w / g}:{h / g}";
        }

        /// <summary>현재 크기가 선택한 목표 비율과 일치하면 " ✓", 아니면 빈 문자열.</summary>
        private string AspectMatchMark(int w, int h)
        {
            float ratio = AspectValue(_aspect);
            if (ratio <= 0f || h <= 0) return string.Empty;
            return Mathf.Abs((float)w / h - ratio) < 0.01f ? "  ✓" : string.Empty;
        }

        private static int Gcd(int a, int b)
        {
            while (b != 0) { (a, b) = (b, a % b); }
            return Mathf.Abs(a);
        }

        /// <summary>
        /// mapId 는 곧 저장 파일명이므로 파일명에 쓸 수 없는 문자를 제거한다.
        /// </summary>
        private static string SanitizeMapId(string id)
        {
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                id = id.Replace(c.ToString(), string.Empty);
            return id;
        }

        // ── 브러시 선택 헬퍼 ─────────────────────────────────────────────────

        private void SetTileBrush(TileType type)   { _brushMode = BrushMode.Tile;   _brushTile    = type;   }
        private void SetSlotBrush(TrapAnchor anch) { _brushMode = BrushMode.Slot;   _brushAnchor  = anch;   }
        private void SetMarkerBrush(bool isGoal)   { _brushMode = BrushMode.Marker; _markerIsGoal = isGoal; }

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
    }
}
