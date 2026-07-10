using UnityEngine;
using UnityEngine.Rendering;
using System;
using System.Collections;

namespace ReTrap
{
    public enum GamePhase
    {
        Build,
        Verification,
        Play
    }

    public class GamePhaseManager : MonoBehaviour
    {
        public static GamePhaseManager Instance { get; private set; }

        /// <summary>페이즈 전환 시 발행. TrapMutationManager 등이 구독.</summary>
        public static event Action<GamePhase> OnPhaseChanged;

        [Header("Volumes")]
        public Volume buildVolume;
        public Volume playVolume;
        public float transitionDuration = 1.0f;

        [Header("Current State")]
        public GamePhase currentPhase = GamePhase.Build;

        [Header("페이즈 흐름")]
        [SerializeField]
        [Tooltip("true면 빌드 완료 시 검증 페이즈를 거친다. AI 미구현 상태에서는 스텁이 즉시 통과시킨다.")]
        private bool useVerificationPhase = false;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            SetPhase(GamePhase.Build, true);
        }

        /// <summary>
        /// 빌드 페이즈 완료 시 호출. <see cref="useVerificationPhase"/> 설정에 따라
        /// 검증 페이즈를 거칠지 곧바로 플레이로 갈지 결정합니다.
        /// 페이즈 흐름의 결정권을 GamePhaseManager 로 중앙화하기 위한 진입점이며,
        /// BuildPhaseController 등 호출부는 이 메서드만 사용하고 직접 SetPhase(Play) 를 호출하지 않습니다.
        /// </summary>
        public void AdvanceFromBuild()
        {
            SetPhase(useVerificationPhase ? GamePhase.Verification : GamePhase.Play);
        }

        /// <summary>
        /// 페이즈를 전환합니다.
        /// <para><b>실행 순서 정책</b>: (a) 순서가 보장되어야 하는 게임플레이 계층은
        /// 이벤트가 아니라 <b>직접 호출</b>로 처리합니다 (예: TrapMutationManager 의 변이 적용/리셋 —
        /// Verification 진입 시 "변이 상태 결정"이 이후 로직보다 먼저 끝나야 함).
        /// (b) 그 외 순서 무관한 표현 계층(카메라, HUD, 볼륨 블렌드, 슬롯 비주얼)은
        /// <see cref="OnPhaseChanged"/> 이벤트 구독으로 반응합니다.
        /// 새 순서 의존 로직이 필요하면 이벤트 발행 이전에 직접 호출을 추가할 것.</para>
        /// </summary>
        public void SetPhase(GamePhase newPhase, bool immediate = false)
        {
            currentPhase = newPhase;

            // (a) 순서 보장이 필요한 직접 호출 — 이벤트 발행보다 먼저 실행되어야 함
            TrapMutationManager.Instance?.ApplyPhase(newPhase);

            // (b) 순서 무관한 표현 계층 — 이벤트로 통지
            OnPhaseChanged?.Invoke(newPhase);
            StopAllCoroutines();

            if (immediate)
            {
                UpdateVolumes(newPhase == GamePhase.Build ? 1 : 0);
            }
            else
            {
                StartCoroutine(TransitionVolume(newPhase));
            }
        }

        private IEnumerator TransitionVolume(GamePhase targetPhase)
        {
            float elapsed = 0;
            float startBuildWeight = buildVolume.weight;
            float startPlayWeight = playVolume.weight;
            
            float targetBuildWeight = (targetPhase == GamePhase.Build) ? 1f : 0f;
            float targetPlayWeight = (targetPhase == GamePhase.Play || targetPhase == GamePhase.Verification) ? 1f : 0f;

            while (elapsed < transitionDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / transitionDuration;
                
                buildVolume.weight = Mathf.Lerp(startBuildWeight, targetBuildWeight, t);
                playVolume.weight = Mathf.Lerp(startPlayWeight, targetPlayWeight, t);
                
                yield return null;
            }
            
            buildVolume.weight = targetBuildWeight;
            playVolume.weight = targetPlayWeight;
        }

        private void UpdateVolumes(float buildWeight)
        {
            buildVolume.weight = buildWeight;
            playVolume.weight = 1f - buildWeight;
        }
        
        [ContextMenu("Switch to Play Phase")]
        public void TestPlayPhase() => SetPhase(GamePhase.Play);

        [ContextMenu("Switch to Build Phase")]
        public void TestBuildPhase() => SetPhase(GamePhase.Build);
    }
}
