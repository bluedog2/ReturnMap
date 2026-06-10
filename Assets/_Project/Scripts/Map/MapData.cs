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
        /// 데이터 스키마 버전. 필드 추가·의미 변경 시 올리고
        /// <see cref="FromJson"/> 쪽에서 버전 분기로 구형 맵을 마이그레이션합니다.
        /// </summary>
        public int    version     = 1;

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
                width      = width,
                height     = height,
                grid       = new int[width * height],          // 전부 Empty(0)
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
            int copyW   = Mathf.Min(width,  newW);
            int copyH   = Mathf.Min(height, newH);

            for (int y = 0; y < copyH; y++)
                for (int x = 0; x < copyW; x++)
                    newGrid[y * newW + x] = grid[y * width + x];

            // 범위 밖 슬롯 제거
            trapSlots.RemoveAll(s => s.x >= newW || s.y >= newH);

            // spawn/goal 클램프
            spawnPoint = new GridCoord(Mathf.Clamp(spawnPoint.x, 0, newW - 1),
                                       Mathf.Clamp(spawnPoint.y, 0, newH - 1));
            goalPoint  = new GridCoord(Mathf.Clamp(goalPoint.x,  0, newW - 1),
                                       Mathf.Clamp(goalPoint.y,  0, newH - 1));

            grid   = newGrid;
            width  = newW;
            height = newH;
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

        public static MapData FromJson(string json) => JsonUtility.FromJson<MapData>(json);
    }
}
