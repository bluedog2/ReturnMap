using UnityEditor;
using UnityEngine;

namespace ReTrap.EditorTools
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  MapEditorWindow.Canvas — 캔버스 렌더 · 줌 · 패닝
    // ═══════════════════════════════════════════════════════════════════════════

    public partial class MapEditorWindow
    {
        // ── 캔버스 본체 ───────────────────────────────────────────────────────

        private void DrawCanvas()
        {
            EditorGUILayout.BeginVertical();
            {
                _viewRect = GUILayoutUtility.GetRect(
                    100f, 100f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                EditorGUI.DrawRect(_viewRect, ColCanvasBg);

                var map = _doc.map;
                if (map == null || map.grid == null) { EditorGUILayout.EndVertical(); return; }

                // 줌/패닝은 ScrollView 바깥(윈도우 좌표)에서 처리
                HandleZoomPan(map);

                float gridW     = map.width  * _cellSize;
                float gridH     = map.height * _cellSize;
                var contentRect = new Rect(0f, 0f,
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

                DrawZoomHint();
            }
            EditorGUILayout.EndVertical();
        }

        // ── 줌 / 패닝 ─────────────────────────────────────────────────────────

        /// <summary>
        /// 마우스 휠 줌 (커서 기준) + 중간버튼 패닝.
        /// ScrollView 바깥에서 호출하므로 <c>e.mousePosition</c> 은 윈도우 좌표입니다.
        /// </summary>
        private void HandleZoomPan(MapData map)
        {
            Event e = Event.current;
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
                    // 커서 아래 셀이 고정되도록 스크롤 보정
                    Vector2 mouseInView    = e.mousePosition - new Vector2(_viewRect.x, _viewRect.y);
                    Vector2 mouseInContent = mouseInView + _canvasScroll;

                    float ratio   = _cellSize / oldSize;
                    _canvasScroll = mouseInContent * ratio - mouseInView;

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

        private void DrawZoomHint()
        {
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

                    // ① 폴백 색상 블록
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
                    : TilePaletteConfig.DefaultSlotTint;

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

            var uv = new Rect(
                tr.x      / tex.width,
                tr.y      / tex.height,
                tr.width  / tex.width,
                tr.height / tex.height);

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

        // ── 그리드선 / 호버 ──────────────────────────────────────────────────

        private void DrawGridLines(MapData map, Vector2 origin, float gridW, float gridH)
        {
            // 셀이 너무 작으면 그리드선 생략 (렌더 노이즈 방지)
            if (_cellSize < 8f) return;

            for (int x = 0; x <= map.width; x++)
                EditorGUI.DrawRect(
                    new Rect(origin.x + x * _cellSize, origin.y, 1f, gridH), ColGridLine);
            for (int y = 0; y <= map.height; y++)
                EditorGUI.DrawRect(
                    new Rect(origin.x, origin.y + y * _cellSize, gridW, 1f), ColGridLine);
        }

        private void DrawHover(MapData map, Vector2 origin)
        {
            if (!map.InBounds(_hoverX, _hoverY)) return;
            Rect rect = CellRect(map, origin, _hoverX, _hoverY);
            EditorGUI.DrawRect(rect, ColHover);
            DrawRectOutline(rect, Color.white, 1f);
        }

        // ── 좌표 유틸 ─────────────────────────────────────────────────────────

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
    }
}
