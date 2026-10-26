using System;
// Runtime integration reference; place into the game only during a separately authorized integration.
public static class GraffitiTiles3Placement {
    public static uint Hash(int x, int y) {
        unchecked {
            uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663);
            h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
            return h;
        }
    }
    public static int Kind(int x, int y) {
        uint k = Hash(x, y) % 4;
        return k < 2 ? 0 : (int)k - 1;
    }
    public static string TileName(bool wall, int first, int second, int x, int y) {
        bool b = first == second && first > 0 && ((Hash(x, y) >> 8) & 1) != 0;
        return (wall ? "Wall_T" : "Floor_L") + first + (wall ? "_B" : "_R") + second + (b ? "_b" : "");
    }
}
