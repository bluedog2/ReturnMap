using UnityEditor;
using UnityEngine;

namespace ReTrap.EditorTools
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  MapEditorWindow — 커스텀 맵 에디터 창 (partial 루트)
    //
    //  파일 구성:
    //    MapEditorWindow.cs         — 필드 · 생명주기 · OnGUI  (이 파일)
    //    MapEditorWindow.Sidebar.cs — 상단 바 · 사이드바 · 상태바
    //    MapEditorWindow.Canvas.cs  — 캔버스 렌더 · 줌 · 패닝
    //    MapEditorWindow.Tools.cs   — 마우스 입력 · 브러시 적용
    //    MapEditorWindow.IO.cs      — 저장 · 불러오기 · 새 맵 · 크기 · 전체 지우기
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// JSON 기반 맵을 그리드로 편집하는 에디터 창.
    /// <para>메뉴 <b>ReTrap → Map Editor</b> 로 엽니다.</para>
    /// </summary>
    public partial class MapEditorWindow : EditorWindow
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

        // ── 색상 (창 UI 전용 — 타일 폴백색은 TilePaletteConfig.FallbackColor) ─

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

        /// <summary>문서가 없으면 빈 맵으로 생성. hideFlags 로 디스크 저장 방지.</summary>
        private void EnsureDocument()
        {
            if (_doc != null) return;
            _doc           = CreateInstance<MapDocument>();
            _doc.hideFlags = HideFlags.DontSave;
            _resizeW       = _doc.map.width;
            _resizeH       = _doc.map.height;
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

        // ── 공용 헬퍼 ─────────────────────────────────────────────────────────

        /// <summary>팔레트가 있으면 팔레트 색, 없으면 공용 폴백색.</summary>
        private Color PaletteColor(TileType type)
            => _palette != null ? _palette.GetColor(type)
                                : TilePaletteConfig.FallbackColor(type);

        private void MarkDirty() { _isDirty = true; Repaint(); }

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
