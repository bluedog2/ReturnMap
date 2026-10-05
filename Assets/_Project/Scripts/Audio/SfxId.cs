namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  SfxId — SFX 종류 식별자
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// SFX 종류. 0부터 연속된 값이며 <see cref="AudioConfig"/> 엔트리 표의 인덱스로 직접 사용된다.
    /// 새 SFX 는 끝에 추가할 것(기존 값 순서 변경 금지).
    /// </summary>
    public enum SfxId
    {
        /// <summary>함정 발동(가시 돌출·화살 발사·해머 낙하).</summary>
        TrapActivate = 0,
        /// <summary>플레이어 피격.</summary>
        PlayerHit = 1,
    }
}
