using System;
using System.Collections.Generic;
using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  MapData — JSON 직렬화 맵 데이터 모델
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>그리드 좌표 (정수). JsonUtility 직렬화 가능.</summary>
    [Serializable]
    public struct GridCoord
    {
        public int x;
        public int y;

        public GridCoord(int x, int y) { this.x = x; this.y = y; }

        public override string ToString() => $"({x},{y})";
    }

    /// <summary>
    /// 함정 설치 슬롯 1개. 기본 타일 위에 얹히는 오버레이 데이터입니다.
    /// <para>anchor 는 JSON 가독성을 위해 <b>문자열</b>로 직렬화하고,
    /// 코드에서는 <see cref="Anchor"/> 프로퍼티로 열거형 변환해 사용합니다.</para>
    /// </summary>
    [Serializable]
    public class TrapSlotData
    {
        public int    x;
        public int    y;
        public string anchor = nameof(TrapAnchor.Floor);

        public TrapSlotData() { }

        public TrapSlotData(int x, int y, TrapAnchor anchor)
        {
            this.x      = x;
            this.y      = y;
            this.anchor = anchor.ToString();
        }

        /// <summary>문자열 anchor ↔ 열거형 변환. 파싱 실패 시 Floor 폴백.</summary>
        public TrapAnchor Anchor
        {
            get => Enum.TryParse(anchor, out TrapAnchor a) ? a : TrapAnchor.Floor;
            set => anchor = value.ToString();
        }
    }

    /// <summary>
    /// 한 스테이지의 맵 지형 데이터. JSON 으로 저장/로드됩니다.
    ///
    /// <para><b>좌표 규약</b></para>
    /// <list type="bullet">
    ///   <item>1차원 grid 배열, <c>index = y * width + x</c></item>
    ///   <item><see cref="originBottomLeft"/> = true → grid 0행이 월드 <b>최하단</b>
    ///         (Unity Y축 위 방향과 일치, 상하 반전 방지)</item>
    /// </list>
    ///
    /// <para><b>함정 배치 데이터는 여기 포함되지 않습니다.</b>
    /// 지형(개발자 제작)과 함정 배치(플레이어 런타임)는 분리됩니다.</para>
    /// </summary>
    [Serializable]
    public class MapData
    {
        // ── 메타 ──────────────────────────────────────────────────────────────

        /// <summary>
        /// 현재 데이터 스키마 버전.
        /// <para><b>스키마 변경 규약</b>: 필드 추가·의미 변경 시 이 값을 올리고,
        /// <see cref="Migrate"/> 의 switch 에 이전 버전 → 다음 버전 단계 변환
        /// case 를 추가합니다. (한 버전씩 순차 마이그레이션)</para>
        /// </summary>
        public const int CurrentVersion = 1;

        /// <summary>
        /// 데이터 스키마 버전. 필드 추가·의미 변경 시 올리고
        /// <see cref="FromJson"/> 쪽에서 버전 분기로 구형 맵을 마이그레이션합니다.
        /// </summary>
        public int    version     = CurrentVersion;

        public string mapId       = "stage_new";
        public string displayName = "";

        /// <summary>Build Phase 에서 플레이어가 함정 설치에 쓸 수 있는 예산.</summary>
        public int    buildBudget = 100;

        // ── 그리드 크기 ──────────────────────────────────────────────────────
        public int   width    = 24;
        public int   height   = 16;
        public float tileUnit = 1f;

        /// <summary>
        /// 좌표 원점 규약. 항상 true(좌하단 원점)로 고정하며, 명시적으로 직렬화해
        /// 로더가 상하 반전 없이 일관되게 해석하도록 합니다.
        /// </summary>
        public bool originBottomLeft = true;

        // ── 진행 지점 ────────────────────────────────────────────────────────
        public GridCoord spawnPoint = new GridCoord(1, 1);
        public GridCoord goalPoint  = new GridCoord(22, 14);

        // ── 타일 데이터 (length == width*height) ─────────────────────────────
        public int[] grid;

        /// <summary>
        /// 배경 레이어 — 충돌 없는 장식 타일의 <b>인덱스</b> 그리드 (0=없음, 1~N=배경 타일).
        /// 지형 <see cref="grid"/> 와 독립이며 같은 셀에 공존합니다.
        /// <para>인덱스 기반이라 배경 타일 종류를 코드 수정 없이 늘릴 수 있고,
        /// 향후 다중 레이어(패럴랙스)로 확장 시 이 필드를 레이어 배열로 마이그레이션합니다.</para>
        /// </summary>
        public int[] background;

        // ── 함정 슬롯 (오버레이) ─────────────────────────────────────────────
        public List<TrapSlotData> trapSlots = new List<TrapSlotData>();

        // ── 팩토리 ────────────────────────────────────────────────────────────

        /// <summary>지정 크기의 빈(Empty) 맵을 생성합니다.</summary>
        public static MapData CreateEmpty(int width, int height)
        {
            width  = Mathf.Max(1, width);
            height = Mathf.Max(1, height);

            return new MapData
            {
                version    = CurrentVersion,
                width      = width,
                height     = height,
                grid       = new int[width * height],          // 전부 Empty(0)
                background = new int[width * height],          // 배경 전부 없음(0)
                spawnPoint = new GridCoord(1, 1),
                goalPoint  = new GridCoord(width - 2, height - 2),
                trapSlots  = new List<TrapSlotData>(),
            };
        }

        // ── 인덱스 / 타일 접근 ───────────────────────────────────────────────

        public int  Index(int x, int y)    => y * width + x;
        public bool InBounds(int x, int y)  => x >= 0 && x < width && y >= 0 && y < height;

        /// <summary>범위 밖이면 Empty 반환 (이웃 검사 안전).</summary>
        public TileType GetTile(int x, int y)
            => InBounds(x, y) ? (TileType)grid[Index(x, y)] : TileType.Empty;

        /// <summary>범위 밖이면 무시.</summary>
        public void SetTile(int x, int y, TileType type)
        {
            if (InBounds(x, y))
                grid[Index(x, y)] = (int)type;
        }

        // ── 배경 레이어 접근 ──────────────────────────────────────────────────

        /// <summary>배경 타일 인덱스 (0=없음). 범위 밖/미초기화면 0.</summary>
        public int GetBackground(int x, int y)
            => (background != null && InBounds(x, y)) ? background[Index(x, y)] : 0;

        /// <summary>배경 타일 인덱스 설정 (0=지움). 범위 밖이면 무시.</summary>
        public void SetBackground(int x, int y, int tileIndex)
        {
            EnsureBackground();
            if (InBounds(x, y))
                background[Index(x, y)] = tileIndex;
        }

        /// <summary>배경 배열이 grid 와 같은 길이를 갖도록 보장 (구버전 맵·null 대비).</summary>
        public void EnsureBackground()
        {
            if (background == null || background.Length != width * height)
            {
                var old = background;
                background = new int[width * height];
                if (old != null)
                {
                    int n = Mathf.Min(old.Length, background.Length);
                    System.Array.Copy(old, background, n);
                }
            }
        }

        // ── 함정 슬롯 ─────────────────────────────────────────────────────────

        /// <summary>해당 좌표의 슬롯 반환 (없으면 null).</summary>
        public TrapSlotData GetSlot(int x, int y)
            => trapSlots.Find(s => s.x == x && s.y == y);

        /// <summary>
        /// 슬롯 토글. 에디터 클릭 동작용.
        /// <list type="bullet">
        ///   <item>슬롯 없음 → 새 슬롯 추가</item>
        ///   <item>슬롯 있음 + 같은 anchor → 제거</item>
        ///   <item>슬롯 있음 + 다른 anchor → anchor 갱신</item>
        /// </list>
        /// </summary>
        public void ToggleSlot(int x, int y, TrapAnchor anchor)
        {
            if (!InBounds(x, y)) return;

            var existing = GetSlot(x, y);
            if (existing == null)
            {
                trapSlots.Add(new TrapSlotData(x, y, anchor));
            }
            else if (existing.Anchor == anchor)
            {
                trapSlots.Remove(existing);
            }
            else
            {
                existing.Anchor = anchor;
            }
        }

        // ── 리사이즈 (유동 크기) ──────────────────────────────────────────────

        /// <summary>
        /// 그리드 크기를 변경하면서 겹치는 영역의 타일을 보존합니다.
        /// 커지면 빈칸(Empty)으로 채우고, 작아지면 범위 밖 데이터를 잘라냅니다.
        /// spawn/goal 은 새 범위로 클램프, 범위 밖 슬롯은 제거합니다.
        /// </summary>
        public void Resize(int newW, int newH)
        {
            newW = Mathf.Max(1, newW);
            newH = Mathf.Max(1, newH);

            var newGrid = new int[newW * newH];
            var newBg   = new int[newW * newH];
            int copyW   = Mathf.Min(width,  newW);
            int copyH   = Mathf.Min(height, newH);

            EnsureBackground();
            for (int y = 0; y < copyH; y++)
                for (int x = 0; x < copyW; x++)
                {
                    newGrid[y * newW + x] = grid[y * width + x];
                    newBg[y * newW + x]   = background[y * width + x];
                }

            // 범위 밖 슬롯 제거
            trapSlots.RemoveAll(s => s.x >= newW || s.y >= newH);

            // spawn/goal 클램프
            spawnPoint = new GridCoord(Mathf.Clamp(spawnPoint.x, 0, newW - 1),
                                       Mathf.Clamp(spawnPoint.y, 0, newH - 1));
            goalPoint  = new GridCoord(Mathf.Clamp(goalPoint.x,  0, newW - 1),
                                       Mathf.Clamp(goalPoint.y,  0, newH - 1));

            grid       = newGrid;
            background = newBg;
            width      = newW;
            height     = newH;
        }

        // ── 검증 ──────────────────────────────────────────────────────────────

        /// <summary>로드 직후 데이터 무결성 검사. 실패 시 error 에 사유를 담습니다.</summary>
        public bool Validate(out string error)
        {
            if (width <= 0 || height <= 0)
            {
                error = $"잘못된 그리드 크기: {width}x{height}";
                return false;
            }

            if (grid == null || grid.Length != width * height)
            {
                error = $"grid 길이 불일치: {(grid == null ? 0 : grid.Length)} != {width * height}";
                return false;
            }

            if (!InBounds(spawnPoint.x, spawnPoint.y))
            {
                error = $"spawnPoint 범위 초과: {spawnPoint}";
                return false;
            }

            if (!InBounds(goalPoint.x, goalPoint.y))
            {
                error = $"goalPoint 범위 초과: {goalPoint}";
                return false;
            }

            if (trapSlots != null)
            {
                var seenCoords = new HashSet<(int, int)>();
                foreach (var s in trapSlots)
                {
                    if (!InBounds(s.x, s.y))
                    {
                        error = $"trapSlot 범위 초과: ({s.x},{s.y})";
                        return false;
                    }

                    // anchor 오타가 조용히 Floor 로 폴백되는 것을 차단
                    if (!Enum.TryParse(s.anchor, out TrapAnchor _))
                    {
                        error = $"trapSlot anchor 파싱 불가: '{s.anchor}' at ({s.x},{s.y})";
                        return false;
                    }

                    // 같은 좌표에 슬롯이 2개 이상이면 제작 실수 — 조용히 뒤 항목이 무시되는 것 방지
                    if (!seenCoords.Add((s.x, s.y)))
                    {
                        error = $"trapSlot 좌표 중복: ({s.x},{s.y})";
                        return false;
                    }
                }
            }

            error = null;
            return true;
        }

        // ── 좌표 변환 (그리드 ↔ 월드) ────────────────────────────────────────

        /// <summary>
        /// 그리드 셀의 <b>중심</b> 월드 좌표를 반환합니다.
        /// originBottomLeft 규약에 따라 y를 그대로 위 방향으로 매핑합니다.
        /// </summary>
        public Vector2 CellToWorld(int x, int y, Vector2 origin = default)
            => origin + new Vector2((x + 0.5f) * tileUnit, (y + 0.5f) * tileUnit);

        /// <summary>
        /// 맵 전체의 월드 공간 <see cref="Bounds"/>.
        /// <para>카메라 경계 설정 시 <c>CameraController.SetBoundsFromMap(map, origin)</c> 에서 사용합니다.</para>
        /// </summary>
        public Bounds GetWorldBounds(Vector2 origin = default)
        {
            float w      = width  * tileUnit;
            float h      = height * tileUnit;
            var   center = (Vector3)(origin + new Vector2(w * 0.5f, h * 0.5f));
            return new Bounds(center, new Vector3(w, h, 0f));
        }

        // ── JSON ──────────────────────────────────────────────────────────────

        public string ToJson(bool pretty = true) => JsonUtility.ToJson(this, pretty);

        public static MapData FromJson(string json)
        {
            var data = JsonUtility.FromJson<MapData>(json);
            if (data != null)
                Migrate(data);
            return data;
        }

        // ── 버전 마이그레이션 ─────────────────────────────────────────────────

        /// <summary>
        /// 구버전 JSON 을 <see cref="CurrentVersion"/> 까지 한 단계씩 끌어올립니다.
        /// <para><b>스키마 변경 시</b>: <see cref="CurrentVersion"/> 을 올리고,
        /// 아래 switch 에 "이전 버전 값" 을 case 로 추가해 다음 버전으로 변환하는
        /// 로직을 넣습니다 (버전이 한 번에 여러 단계 뛰어도 순차 적용됨).</para>
        /// <para>버전 무관하게 항상 안전한 보정(예: 배경 배열 크기 정합)은
        /// 루프 안에서 매 단계 공통으로 수행합니다.</para>
        /// </summary>
        private static void Migrate(MapData data)
        {
            if (data.version > CurrentVersion)
            {
                Debug.LogWarning($"[MapData] '{data.mapId}' 의 버전({data.version})이 " +
                                  $"현재 지원 버전({CurrentVersion})보다 높습니다 — " +
                                  "최신 에디터/빌드로 만들어진 맵일 수 있습니다.");
            }

            while (data.version < CurrentVersion)
            {
                int fromVersion = data.version;

                switch (fromVersion)
                {
                    // 현재는 버전 1 뿐이라 분기 없음.
                    // 예) case 1: /* v1 → v2 변환 */ data.version = 2; break;

                    default:
                        // 알 수 없는 과거 버전 — 더 진행할 수 없으므로 강제로 최신 취급하고 루프 탈출
                        Debug.LogWarning($"[MapData] '{data.mapId}' 버전 {fromVersion} → " +
                                         $"{fromVersion + 1} 마이그레이션 규칙 없음 — 중단");
                        data.version = CurrentVersion;
                        break;
                }

                // 각 case 가 자기 버전을 갱신하지 않은 경우를 대비한 안전망
                if (data.version == fromVersion)
                    data.version = fromVersion + 1;
            }

            // 버전 무관 안전 보정 (구버전 맵·null 대비)
            data.EnsureBackground();
        }
    }
}
