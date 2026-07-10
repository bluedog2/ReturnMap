using System.Collections.Generic;
using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  ComponentPool — 범용 컴포넌트 오브젝트 풀 (재사용 유틸)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 풀에서 나가고 들어올 때 상태를 초기화해야 하는 컴포넌트가 구현하는 계약.
    /// Awake/OnEnable 은 풀 재사용 시 호출 시점이 불안정하므로,
    /// 풀 대상의 상태 리셋은 반드시 이 훅에서 처리합니다.
    /// </summary>
    public interface IPoolable
    {
        /// <summary>풀에서 꺼내져 활성화된 직후. 상태를 새 개체처럼 초기화할 것.</summary>
        void OnSpawned();

        /// <summary>풀로 반납되어 비활성화되기 직전. 코루틴/참조 정리.</summary>
        void OnDespawned();
    }

    /// <summary>
    /// Instantiate/Destroy 를 대체하는 범용 컴포넌트 풀.
    /// 검증 AI 에이전트가 첫 사용처이며, 화살(ArrowShooter 투사체)·보스 등
    /// 반복 스폰되는 모든 캐릭터/발사체에 재사용합니다.
    ///
    /// <para><b>사용 규약</b></para>
    /// <list type="bullet">
    ///   <item>꺼내기: <see cref="Get"/> — 위치 지정 후 활성화, IPoolable.OnSpawned 호출</item>
    ///   <item>반납: <see cref="Release"/> — OnDespawned 호출 후 비활성화. Destroy 금지</item>
    ///   <item>소유자 파괴 시: <see cref="Clear"/> 로 보관 인스턴스 정리</item>
    /// </list>
    /// </summary>
    public class ComponentPool<T> where T : Component
    {
        private readonly Stack<T>  _inactive = new Stack<T>();
        private readonly T         _prefab;
        private readonly Transform _parent;
        private readonly int       _maxSize;

        /// <param name="prefab">복제 원본. IPoolable 구현 시 스폰/반납 훅이 호출됩니다.</param>
        /// <param name="parent">인스턴스를 붙일 부모 (스케일 1 권장). null 이면 씬 루트.</param>
        /// <param name="maxSize">보관 상한. 초과 반납분은 파괴 (스파이크 후 메모리 회수).</param>
        public ComponentPool(T prefab, Transform parent = null, int maxSize = 16)
        {
            _prefab  = prefab;
            _parent  = parent;
            _maxSize = Mathf.Max(1, maxSize);
        }

        /// <summary>현재 풀에 잠들어 있는 개수 (디버그용).</summary>
        public int CountInactive => _inactive.Count;

        /// <summary>미리 지정 개수만큼 생성해 재워둡니다. 로딩 타이밍에 호출해 첫 스폰 히치 방지.</summary>
        public void Prewarm(int count)
        {
            for (int i = _inactive.Count; i < count; i++)
            {
                var item = Object.Instantiate(_prefab, _parent);
                item.gameObject.SetActive(false);
                _inactive.Push(item);
            }
        }

        /// <summary>인스턴스를 꺼내 지정 위치에서 활성화합니다.</summary>
        public T Get(Vector3 position, Quaternion rotation)
        {
            // 씬 언로드 등으로 파괴된 잔여 항목은 건너뜀
            T item = null;
            while (_inactive.Count > 0 && item == null)
                item = _inactive.Pop();

            if (item == null)
                item = Object.Instantiate(_prefab, _parent);

            // 위치를 먼저 잡고 활성화 (활성화 프레임에 옛 위치가 보이는 것 방지)
            item.transform.SetPositionAndRotation(position, rotation);
            item.gameObject.SetActive(true);
            (item as IPoolable)?.OnSpawned();
            return item;
        }

        /// <summary>인스턴스를 풀로 반납합니다. null/파괴된 참조는 무시.</summary>
        public void Release(T item)
        {
            if (item == null) return;

#if UNITY_EDITOR
            if (_inactive.Contains(item))
            {
                Debug.LogError($"[ComponentPool] 이중 반납 감지: {item.name}");
                return;
            }
#endif
            (item as IPoolable)?.OnDespawned();
            item.gameObject.SetActive(false);

            if (_inactive.Count < _maxSize)
                _inactive.Push(item);
            else
                Object.Destroy(item.gameObject);
        }

        /// <summary>보관 중인 인스턴스를 전부 파괴합니다. 소유자 OnDestroy 에서 호출.</summary>
        public void Clear()
        {
            while (_inactive.Count > 0)
            {
                var item = _inactive.Pop();
                if (item != null) Object.Destroy(item.gameObject);
            }
        }
    }
}
