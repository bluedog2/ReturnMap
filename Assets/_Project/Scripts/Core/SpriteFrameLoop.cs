using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  SpriteFrameLoop — 스프라이트 프레임 반복 재생 (골 표식 등 장식)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 같은 오브젝트의 <see cref="SpriteRenderer"/> 에 프레임 배열을 일정 속도로 반복 표시합니다.
    /// Animator 없이 쓰는 가벼운 장식용 (예: 골 깃발 펄럭임).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteFrameLoop : MonoBehaviour
    {
        [Header("프레임")]
        [Tooltip("반복 재생할 프레임 (순서대로).")]
        [SerializeField] private Sprite[] frames;

        [Tooltip("재생 속도(프레임/초).")]
        [SerializeField, Min(0.1f)] private float fps = 6f;

        private SpriteRenderer _sr;

        private void Awake() => _sr = GetComponent<SpriteRenderer>();

        private void Update()
        {
            if (frames == null || frames.Length == 0) return;
            int i = (int)(Time.time * fps) % frames.Length;
            if (_sr.sprite != frames[i]) _sr.sprite = frames[i];
        }
    }
}
