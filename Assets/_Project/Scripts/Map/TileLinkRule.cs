namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  TileLinkRule — 이음 낙서 타일(가장자리 맞춤) 선택 규칙
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 이웃 칸으로 낙서가 이어지는 지형 타일의 조각 선택 규칙 (화풍 v2, NEWIMAG/GraffitiTiles3 README 와 동일).
    /// <para>칸 경계마다 종류(0=낙서 없음, 1=크림슨 띠, 2=흰 선)를 셀 좌표 해시로 정하고,
    /// 칸은 양쪽 경계 종류 조합의 조각을 씁니다. 이웃이 같은 경계를 공유하므로 항상 이어집니다.</para>
    /// <para>Floor·Ceiling 은 가로(왼쪽=first, 오른쪽=second), Wall 은 세로(위=first, 아래=second).
    /// 이웃이 같은 타입이 아니거나 함정 슬롯이면 그 경계는 0 (낙서 끝처리).</para>
    /// </summary>
    public static class TileLinkRule
    {
        /// <summary>조각 목록 길이: [0]=미사용(0-0 은 빈 콘크리트 변형), 1..8 = first*3+second, 9 = 1-1 b, 10 = 2-2 b.</summary>
        public const int SpriteCount = 11;

        /// <summary>셀 좌표 결정적 해시 (TilePaletteConfig 변형 선택과 같은 식).</summary>
        public static uint Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663);
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return h;
            }
        }

        /// <summary>경계 종류: 없음 50% · 크림슨 25% · 흰 선 25%.</summary>
        public static int Kind(int x, int y)
        {
            uint k = Hash(x, y) % 4;
            return k < 2 ? 0 : (int)k - 1;
        }

        /// <summary>
        /// 조각 인덱스. 양쪽 경계가 모두 0 이면 -1 (빈 콘크리트 변형 사용).
        /// </summary>
        /// <param name="vertical">Wall 이면 true (세로 이음).</param>
        /// <param name="linkPrev">Floor·Ceiling: 왼쪽 이웃과 이어짐 / Wall: 아래 이웃과 이어짐.</param>
        /// <param name="linkNext">Floor·Ceiling: 오른쪽 이웃과 이어짐 / Wall: 위 이웃과 이어짐.</param>
        public static int SpriteIndex(bool vertical, int x, int y, bool linkPrev, bool linkNext)
        {
            int first, second;
            if (vertical)
            {
                first  = linkNext ? Kind(x, y)     : 0; // 위 경계 (y, y+1)
                second = linkPrev ? Kind(x, y - 1) : 0; // 아래 경계 (y-1, y)
            }
            else
            {
                first  = linkPrev ? Kind(x - 1, y) : 0; // 왼쪽 경계 (x-1, x)
                second = linkNext ? Kind(x, y)     : 0; // 오른쪽 경계 (x, x+1)
            }
            if (first == 0 && second == 0) return -1;
            if (first == second && ((Hash(x, y) >> 8) & 1) != 0) return first == 1 ? 9 : 10;
            return first * 3 + second;
        }
    }
}
