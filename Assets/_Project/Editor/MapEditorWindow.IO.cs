using System.IO;
using UnityEditor;
using UnityEngine;

namespace ReTrap.EditorTools
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  MapEditorWindow.IO — 저장 · 불러오기 · 새 맵 · 크기 변경 · 전체 지우기
    // ═══════════════════════════════════════════════════════════════════════════

    public partial class MapEditorWindow
    {
        // ── 저장 ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 맵을 StreamingAssets/Maps/{mapId}.json 에 저장합니다.
        /// 검증 실패 시 다이얼로그로 오류를 표시하고 중단합니다.
        /// </summary>
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

        /// <summary>
        /// 파일 열기 패널로 JSON 을 선택해 맵을 불러옵니다.
        /// 검증 실패 시 불러오기를 취소합니다.
        /// </summary>
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

        /// <summary>
        /// 현재 리사이즈 입력값(W×H)으로 빈 맵을 새로 만듭니다.
        /// 미저장 변경사항이 있으면 확인을 요청합니다.
        /// </summary>
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

        /// <summary>
        /// 입력한 W×H 로 현재 맵을 리사이즈합니다.
        /// 기존 타일은 겹치는 범위만 보존되고, 범위 밖 슬롯은 삭제됩니다.
        /// </summary>
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
    }
}
