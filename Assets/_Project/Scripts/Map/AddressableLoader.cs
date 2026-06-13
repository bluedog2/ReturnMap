using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  AddressableLoader — 어드레서블 에셋 로드 전담 구조
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 여러 어드레서블 에셋을 모아 로드하고, 핸들을 한곳에서 추적·해제하는 로더입니다.
    /// 소유자(MapLoader 등)가 인스턴스를 하나 만들어 사용합니다.
    ///
    /// <para><b>사용 패턴</b></para>
    /// <code>
    /// var loader = new AddressableLoader();
    /// loader.Load(spriteRef, s => cache[type] = s);   // 요청만 등록 (논블로킹)
    /// loader.Load(prefabRef, p => prefabCache[type] = p);
    /// yield return loader.WaitAll();                   // 전부 완료까지 코루틴 대기
    /// // ... 캐시 사용 ...
    /// loader.ReleaseAll();                             // 씬 종료 시 핸들 해제
    /// </code>
    ///
    /// <para><b>왜 별도 구조인가</b>: AsyncOperationHandle 을 코루틴에서 직접
    /// <c>yield return</c> 하면 대기가 어긋날 수 있어, 여기서 <see cref="AsyncOperationHandle.IsDone"/>
    /// 폴링으로 안전하게 기다립니다. 로드·대기·해제 책임을 한 클래스로 모읍니다.</para>
    /// </summary>
    public class AddressableLoader
    {
        private readonly List<AsyncOperationHandle> _handles = new List<AsyncOperationHandle>();

        /// <summary>등록된(로드 중/완료) 핸들 수.</summary>
        public int Count => _handles.Count;

        /// <summary>
        /// AssetReference 를 비동기 로드하고 핸들을 추적합니다.
        /// 성공 시 <paramref name="onLoaded"/> 로 결과를 전달합니다 (실패는 콜백 호출 안 함).
        /// </summary>
        public AsyncOperationHandle<T> Load<T>(AssetReferenceT<T> aref, Action<T> onLoaded)
            where T : UnityEngine.Object
        {
            var handle = aref.LoadAssetAsync();
            _handles.Add(handle);
            handle.Completed += op =>
            {
                if (op.Status == AsyncOperationStatus.Succeeded)
                    onLoaded?.Invoke(op.Result);
                else
                    Debug.LogError($"[AddressableLoader] 로드 실패: {aref.RuntimeKey}");
            };
            return handle;
        }

        /// <summary>등록된 모든 핸들이 완료될 때까지 대기하는 코루틴.</summary>
        public IEnumerator WaitAll()
        {
            for (int i = 0; i < _handles.Count; i++)
            {
                var h = _handles[i];
                while (h.IsValid() && !h.IsDone)
                    yield return null;
            }
        }

        /// <summary>추적 중인 모든 핸들을 해제합니다.</summary>
        public void ReleaseAll()
        {
            foreach (var h in _handles)
                if (h.IsValid()) Addressables.Release(h);
            _handles.Clear();
        }
    }
}
