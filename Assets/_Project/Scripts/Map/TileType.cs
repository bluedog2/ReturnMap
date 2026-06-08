namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  TileType / TrapAnchor — 맵 데이터 열거형
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 맵 그리드의 기본 타일 종류.
    /// <para>정수값으로 <see cref="MapData.grid"/> 배열에 저장됩니다. (index = y * width + x)</para>
    /// <para><b>주의</b>: TrapSlot 은 타일이 아니라 오버레이입니다 → <see cref="TrapAnchor"/> 참조.</para>
    /// </summary>
    public enum TileType
    {
        Empty   = 0,  // 빈 공간 — 플레이어 통행 가능
        Floor   = 1,  // 바닥   — 밟고 설 수 있음
        Wall    = 2,  // 좌우 벽
        Ceiling = 3,  // 천장
    }

    /// <summary>
    /// 함정 설치 슬롯이 어느 면에 부착되는지를 나타냅니다.
    /// <para>
    /// Build UI 가 이 값으로 (1) 슬롯에 놓을 수 있는 함정 필터링,
    /// (2) 함정 방향(<c>isFlipped</c> / <c>facingRight</c>) 자동 설정을 수행합니다.
    /// </para>
    /// </summary>
    public enum TrapAnchor
    {
        Floor,      // 바닥 위쪽 면   — SpikeTrap(정방향)
        Ceiling,    // 천장 아래쪽 면 — SpikeTrap(반전), DropHammer
        LeftWall,   // 좌벽 오른쪽 면 — ArrowShooter(오른쪽 발사)
        RightWall,  // 우벽 왼쪽 면   — ArrowShooter(왼쪽 발사)
    }
}
