using UnityEditor;
using UnityEngine;

namespace ReTrap.EditorTools
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  MapEditorWindow.Tools — 마우스 입력 · 브러시 적용
    // ═══════════════════════════════════════════════════════════════════════════

    public partial class MapEditorWindow
    {
        // ── 마우스 → 셀 좌표 ─────────────────────────────────────────────────

        /// <summary>스크롤 콘텐츠 좌표의 마우스 → 셀 좌표 역변환. 범위 밖이면 false.</summary>
        private bool TryGetCell(MapData map, Vector2 origin, Vector2 mouse,
                                out int cx, out int cy)
        {
            cx     = Mathf.FloorToInt((mouse.x - origin.x) / _cellSize);
            int rr = Mathf.FloorToInt((mouse.y - origin.y) / _cellSize);
            cy     = (map.height - 1) - rr;          // y 뒤집기 역변환
            return map.InBounds(cx, cy);
        }

        // ── 입력 핸들링 (ScrollView 안쪽) ────────────────────────────────────

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
                        // 채움 도구는 1회성 — 드래그 불필요
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

        // ── 스트로크 / 브러시 디스패치 ───────────────────────────────────────

        /// <summary>스트로크 시작 시 1회 전체 스냅샷 → 드래그 1회 = Undo 1회.</summary>
        private void BeginStroke()
        {
            string label = _activeTool == ActiveTool.Eraser ? "Erase"
                         : _activeTool == ActiveTool.Fill   ? $"Fill {_brushTile}"
                         : _brushMode  == BrushMode.Slot    ? $"Slot {_brushAnchor}"
                         : _brushMode  == BrushMode.Marker  ? (_markerIsGoal ? "Move Goal" : "Move Spawn")
                         : $"Paint {_brushTile}";
            Undo.RegisterCompleteObjectUndo(_doc, label);
        }

        /// <summary>현재 브러시+도구 조합을 셀에 적용.</summary>
        private void ApplyBrush(MapData map, int x, int y)
        {
            switch (_activeTool)
            {
                case ActiveTool.Eraser: ApplyEraser(map, x, y); break;
                case ActiveTool.Fill:   ApplyFill(map, x, y);   break;
                default:                ApplyPen(map, x, y);    break;
            }
        }

        // ── 도구별 적용 ───────────────────────────────────────────────────────

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
                    // 드래그 중에는 추가/갱신만 (드래그 중 제거는 혼란)
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

        /// <summary>지우개: 타일 Empty 화 + 해당 셀 슬롯 제거.</summary>
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

        /// <summary>
        /// 채움(Flood Fill): 클릭 지점과 같은 타일 타입으로 연결된 영역을
        /// 현재 선택된 타일로 교체. 타일 모드에서만 동작.
        /// </summary>
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
    }
}
