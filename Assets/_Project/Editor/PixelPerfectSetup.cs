using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Unity.Cinemachine;

namespace ReTrap.EditorTools
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  PixelPerfectSetup — Pixel Perfect Camera + CinemachinePixelPerfect 배선 (32px 전환 1단계)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 메뉴 <b>ReTrap → Setup → 픽셀 퍼펙트 카메라 세팅</b> (멱등, 대화상자 없음 → 배치모드 가능):
    /// <list type="bullet">
    ///   <item>SampleScene 의 Main Camera 에 <see cref="PixelPerfectCamera"/>(PPU 32 · ref 640×360 ·
    ///         gridSnapping None · cropFrame None)를 추가하고 <b>꺼진 상태</b>로 둔다
    ///         (Play 페이즈에서 <see cref="BuildCameraController"/> 가 켠다).</item>
    ///   <item>vcam PlayerFollowCamera 에 <see cref="CinemachinePixelPerfect"/> 확장을 추가하고
    ///         렌즈 size 를 5.625(= 360 / 2 / 32)로 맞춘다.</item>
    /// </list>
    /// <c>-executeMethod ReTrap.EditorTools.PixelPerfectSetup.Run</c>
    /// </summary>
    public static class PixelPerfectSetup
    {
        private const string ScenePath = "Assets/_Project/ResourcceEX/Scenes/SampleScene.unity";
        private const string VcamName  = "PlayerFollowCamera";
        private const int    Ppu       = 32;
        private const int    RefX      = 640;
        private const int    RefY      = 360;

        [MenuItem("ReTrap/Setup/픽셀 퍼펙트 카메라 세팅", false, 111)]
        public static void Run()
        {
            // 메뉴 실행 시 저장 안 된 씬 변경을 묻지 않고 버리지 않도록
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // Main Camera
            var ctrl = Object.FindFirstObjectByType<BuildCameraController>();
            var cam  = ctrl != null ? ctrl.GetComponent<Camera>() : Camera.main;
            if (cam == null)
            {
                Debug.LogError("[PixelPerfectSetup] Main Camera 를 찾지 못함");
                return;
            }

            bool addedPpc = false;
            if (!cam.TryGetComponent(out PixelPerfectCamera ppc))
            {
                ppc = cam.gameObject.AddComponent<PixelPerfectCamera>();
                addedPpc = true;
            }
            ppc.assetsPPU      = Ppu;
            ppc.refResolutionX = RefX;
            ppc.refResolutionY = RefY;
            ppc.gridSnapping   = PixelPerfectCamera.GridSnapping.None;
            ppc.cropFrame      = PixelPerfectCamera.CropFrame.None;
            ppc.enabled        = false;
            EditorUtility.SetDirty(ppc);

            // vcam
            bool addedExt = false;
            float lensSize = -1f;
            CinemachineCamera vcam = null;
            foreach (var v in Object.FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (v.name == VcamName) { vcam = v; break; }

            if (vcam == null)
            {
                Debug.LogWarning($"[PixelPerfectSetup] vcam '{VcamName}' 없음 — 카메라만 세팅");
            }
            else
            {
                if (!vcam.TryGetComponent(out CinemachinePixelPerfect _))
                {
                    vcam.gameObject.AddComponent<CinemachinePixelPerfect>();
                    addedExt = true;
                }
                lensSize = RefY / (2f * Ppu);
                var lens = vcam.Lens;
                lens.OrthographicSize = lensSize;
                vcam.Lens = lens;
                EditorUtility.SetDirty(vcam);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log($"[PixelPerfectSetup] PPC {(addedPpc ? "추가" : "기존")}(PPU {Ppu}, {RefX}x{RefY}, 꺼짐) · " +
                      $"CinemachinePixelPerfect {(addedExt ? "추가" : "기존/없음")} · vcam size {lensSize}. 씬 저장 완료.");
        }
    }
}
