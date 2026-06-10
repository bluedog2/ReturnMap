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
