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

        /// <summary>StreamingAssets/Maps/{mapId}.json 에 즉시 저장합니다.</summary>
        private void SaveMap()
        {
            if (!ValidateBeforeSave()) return;

            string dir  = Path.Combine(Application.streamingAssetsPath, "Maps");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string path = Path.Combine(dir, $"{_doc.map.mapId}.json");
            WriteAndRefresh(path);
        }

        /// <summary>
        /// 파일 저장 패널을 열어 이름·위치를 선택 후 저장합니다.
        /// 기본 폴더는 StreamingAssets/Maps, 기본 이름은 현재 mapId.
        /// 선택한 파일명으로 mapId 도 자동 갱신됩니다.
        /// </summary>
        private void SaveMapAs()
        {
            if (!ValidateBeforeSave()) return;

            string defaultDir = Path.Combine(Application.streamingAssetsPath, "Maps");
            if (!Directory.Exists(defaultDir)) Directory.CreateDirectory(defaultDir);

            string path = EditorUtility.SaveFilePanel(
                "다른 이름으로 저장", defaultDir, _doc.map.mapId, "json");
            if (string.IsNullOrEmpty(path)) return;

            // StreamingAssets 밖에 저장하면 MapLoader 가 찾을 수 없다 — 확인 후 진행
            bool insideStreaming = Path.GetFullPath(path).StartsWith(
                Path.GetFullPath(Application.streamingAssetsPath),
                System.StringComparison.OrdinalIgnoreCase);
            if (!insideStreaming && !EditorUtility.DisplayDialog(
                    "저장 위치 경고",
                    "StreamingAssets 밖에 저장하면 게임에서 이 맵을 로드할 수 없습니다.\n" +
                    "(백업·공유 용도라면 계속 진행해도 됩니다)",
                    "저장", "취소"))
                return;

            // 파일명 → mapId 동기화
            string newId = Path.GetFileNameWithoutExtension(path);
            if (newId != _doc.map.mapId)
            {
                Undo.RegisterCompleteObjectUndo(_doc, "Save As – Rename Map ID");
                _doc.map.mapId = newId;
            }

            WriteAndRefresh(path);
        }

        // ── 저장 공통 ─────────────────────────────────────────────────────────

        /// <summary>
        /// 저장 전 유효성 + 콘텐츠 경고 검사.
        /// 경고는 저장을 막지 않고 확인만 받는다 (특수 맵 허용).
        /// </summary>
        private bool ValidateBeforeSave()
        {
            var map = _doc.map;
            if (!map.Validate(out string err))
            {
                EditorUtility.DisplayDialog("저장 실패", $"맵 검증 실패:\n\n{err}", "확인");
                return false;
            }

            var warnings = MapAuthoringValidator.Validate(map);
            if (warnings.Count > 0)
            {
                const int MaxShow = 8;
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"콘텐츠 경고 {warnings.Count}건:");
                sb.AppendLine();
                for (int i = 0; i < warnings.Count && i < MaxShow; i++)
                    sb.AppendLine($"• {warnings[i]}");
                if (warnings.Count > MaxShow)
                    sb.AppendLine($"… 외 {warnings.Count - MaxShow}건 (콘솔 참조)");
                sb.AppendLine();
                sb.Append("그래도 저장할까요?");

                foreach (var w in warnings)
                    Debug.LogWarning($"[MapEditor] {w}");

                if (!EditorUtility.DisplayDialog("맵 콘텐츠 경고", sb.ToString(), "저장", "취소"))
                    return false;
            }
            return true;
        }

        /// <summary>JSON 파일 기록 + AssetDatabase 갱신 + 더티 클리어.</summary>
        private void WriteAndRefresh(string path)
        {
            File.WriteAllText(path, _doc.map.ToJson(), System.Text.Encoding.UTF8);
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
