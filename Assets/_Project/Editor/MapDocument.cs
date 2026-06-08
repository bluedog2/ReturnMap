using UnityEngine;

namespace ReTrap.EditorTools
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  MapDocument — Undo 지원용 편집 래퍼 (에디터 전용)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Unity 의 Undo 시스템은 <see cref="UnityEngine.Object"/> 만 기록할 수 있습니다.
    /// 순수 C# 클래스인 <see cref="MapData"/> 를 직접 기록할 수 없으므로,
    /// ScriptableObject 로 감싸 <c>Undo.RegisterCompleteObjectUndo</c> 대상이 되게 합니다.
    ///
    /// <para>이 객체는 디스크에 저장하지 않고 메모리에서만 존재합니다
    /// (창이 <c>CreateInstance</c> 로 생성, <c>hideFlags = DontSave</c>).</para>
    /// </summary>
    public class MapDocument : ScriptableObject
    {
        /// <summary>현재 편집 중인 맵 데이터.</summary>
        public MapData map = MapData.CreateEmpty(24, 16);
    }
}
