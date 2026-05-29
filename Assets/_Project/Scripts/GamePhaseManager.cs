using UnityEngine;
using UnityEngine.Rendering;
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

        [Header("Volumes")]
        public Volume buildVolume;
        public Volume playVolume;
        public float transitionDuration = 1.0f;

        [Header("Current State")]
        public GamePhase currentPhase = GamePhase.Build;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            SetPhase(GamePhase.Build, true);
        }

        public void SetPhase(GamePhase newPhase, bool immediate = false)
        {
            currentPhase = newPhase;
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
