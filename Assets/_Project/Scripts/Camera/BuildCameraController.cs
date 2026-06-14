using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using Unity.Cinemachine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  BuildCameraController — 빌드 페이즈 전지적 설계 뷰 카메라
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>Main Camera</b>(CinemachineBrain 동거)에 부착합니다.
    ///
    /// <para><b>빌드 페이즈</b>: CinemachineBrain 을 끄고 카메라를 직접 제어 —
    /// <see cref="CameraConfinerSync"/> 의 경계 박스(맵 영역)를 기준으로
    /// 맵 밖이 보이지 않는 단일 고정 뷰. 휠 줌(커서 기준), 중간버튼 드래그 패닝,
    /// 화면 가장자리 엣지 패닝.</para>
    ///
    /// <para><b>플레이 / 검증 페이즈</b>: CinemachineBrain 을 다시 켜서
    /// 기존 PlayerFollowCamera(플레이어 추적) 로 복원합니다.</para>
    ///
    /// <para><b>경계 정책</b>: 줌아웃 상한을 "맵 밖이 보이지 않는 최대 size"(크롭)로
    /// 동적 cap 하므로 어떤 줌·패닝에서도 맵 밖은 화면에 들어오지 않습니다.</para>
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class BuildCameraController : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Zoom")]
        [Tooltip("휠 1틱당 Orthographic Size 증감.")]
        [SerializeField] private float zoomSpeed    = 1.5f;
        [Tooltip("최대 확대 한계 (가장 작은 size). 줌아웃 상한은 맵 크기로 자동 계산.")]
        [SerializeField] private float minOrthoSize = 3f;

        [Header("Pan — 중간버튼 드래그")]
        [SerializeField] private float panSpeed = 1f;

        [Header("Pan — 엣지 스크롤")]
        [Tooltip("커서를 화면 가장자리에 대면 그 방향으로 카메라가 이동합니다.")]
        [SerializeField] private bool  edgePan       = true;
        [Tooltip("가장자리로 인식할 화면 픽셀 두께.")]
        [SerializeField] private float edgeMargin    = 24f;
        [Tooltip("엣지 패닝 속도 (현재 줌 size 에 비례).")]
        [SerializeField] private float edgePanSpeed  = 3f;

        // ── 런타임 ────────────────────────────────────────────────────────────

        private Camera           _cam;
        private CinemachineBrain _brain;

        private bool    _active;
        private bool    _panning;
        private Vector2 _lastPanScreenPos;

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            _cam   = GetComponent<Camera>();
            _brain = GetComponent<CinemachineBrain>();
        }

        private void OnEnable()
        {
            GamePhaseManager.OnPhaseChanged += HandlePhase;
            MapLoader.OnMapLoaded           += HandleMapLoaded;
        }

        private void OnDisable()
        {
            GamePhaseManager.OnPhaseChanged -= HandlePhase;
            MapLoader.OnMapLoaded           -= HandleMapLoaded;
        }

        private void Start()
        {
            if (GamePhaseManager.Instance != null)
                HandlePhase(GamePhaseManager.Instance.currentPhase);
        }

        private void Update()
        {
            if (!_active) return;
            if (!TryGetBounds(out Bounds b)) return;

            // UI(HUD 핫바 등) 위에서는 줌·엣지팬을 막아 오조작 방지
            bool overUI = EventSystem.current != null &&
                          EventSystem.current.IsPointerOverGameObject();

            if (!overUI) HandleZoom(b);
            HandlePan(b);                 // 중간버튼 드래그는 시작 후 UI 무관하게 지속
            if (!overUI && edgePan) HandleEdgePan(b);
        }

        // ── 페이즈 연동 ───────────────────────────────────────────────────────

        private void HandlePhase(GamePhase phase)
        {
            if (phase == GamePhase.Build) EnterBuildView();
            else                          ExitBuildView();
        }

        private void HandleMapLoaded(MapData map)
        {
            if (_active) FitToBounds();
        }

        private void EnterBuildView()
        {
            _active = true;
            if (_brain != null) _brain.enabled = false; // Cinemachine 제어 중단 → 직접 제어
            _cam.orthographic = true;
            FitToBounds();
        }

        private void ExitBuildView()
        {
            _active  = false;
            _panning = false;
            if (_brain != null) _brain.enabled = true;  // 플레이어 추적 카메라 복원
        }

        // ── 뷰 맞춤 ───────────────────────────────────────────────────────────

        /// <summary>줌아웃 상한(크롭): 가로·세로 모두 경계 안에 들어가는 최대 size.</summary>
        private float MaxCropSize(Bounds b)
            => Mathf.Min(b.extents.y, b.extents.x / _cam.aspect);

        /// <summary>경계 중심으로 이동하고 줌아웃 상한(맵이 화면을 꽉 채우는 크롭)으로 맞춥니다.</summary>
        private void FitToBounds()
        {
            if (!TryGetBounds(out Bounds b)) return;

            float maxSize = Mathf.Max(MaxCropSize(b), minOrthoSize);
            _cam.orthographicSize = maxSize;
            transform.position = new Vector3(b.center.x, b.center.y, transform.position.z);
        }

        // ── 입력 ──────────────────────────────────────────────────────────────

        /// <summary>휠 줌 — 커서가 가리키는 월드 지점을 고정한 채 확대/축소.</summary>
        private void HandleZoom(Bounds b)
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) < 0.01f) return;

            Vector3 screenPos       = mouse.position.ReadValue();
            Vector3 worldBeforeZoom = _cam.ScreenToWorldPoint(screenPos);

            float maxSize = Mathf.Max(MaxCropSize(b), minOrthoSize);
            float target  = _cam.orthographicSize - Mathf.Sign(scroll) * zoomSpeed;
            _cam.orthographicSize = Mathf.Clamp(target, minOrthoSize, maxSize);

            // 줌 후 커서 지점이 어긋난 만큼 카메라를 보정 → 커서 기준 줌
            Vector3 worldAfterZoom = _cam.ScreenToWorldPoint(screenPos);
            transform.position += (worldBeforeZoom - worldAfterZoom);

            ClampToBounds(b);
        }

        /// <summary>중간버튼 클릭 드래그 패닝.</summary>
        private void HandlePan(Bounds b)
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.middleButton.wasPressedThisFrame)
            {
                _panning          = true;
                _lastPanScreenPos = mouse.position.ReadValue();
            }
            else if (mouse.middleButton.wasReleasedThisFrame)
            {
                _panning = false;
            }

            if (!_panning || !mouse.middleButton.isPressed) return;

            Vector2 cur         = mouse.position.ReadValue();
            Vector2 deltaScreen = cur - _lastPanScreenPos;
            _lastPanScreenPos   = cur;

            float worldPerPixel = (_cam.orthographicSize * 2f) / Screen.height;
            transform.position += new Vector3(-deltaScreen.x, -deltaScreen.y, 0f) * worldPerPixel * panSpeed;

            ClampToBounds(b);
        }

        /// <summary>커서를 화면 가장자리에 대면 그 방향으로 카메라 이동 (RTS 식).</summary>
        private void HandleEdgePan(Bounds b)
        {
            var mouse = Mouse.current;
            if (mouse == null || _panning) return; // 드래그 패닝 중엔 엣지팬 비활성

            Vector2 p = mouse.position.ReadValue();
            // 커서가 게임뷰 밖이면 무시
            if (p.x < 0f || p.y < 0f || p.x > Screen.width || p.y > Screen.height) return;

            Vector2 dir = Vector2.zero;
            if (p.x < edgeMargin)               dir.x = -1f;
            else if (p.x > Screen.width  - edgeMargin) dir.x = 1f;
            if (p.y < edgeMargin)               dir.y = -1f;
            else if (p.y > Screen.height - edgeMargin) dir.y = 1f;

            if (dir == Vector2.zero) return;

            float worldPerSec = edgePanSpeed * _cam.orthographicSize;
            transform.position += (Vector3)(dir.normalized * worldPerSec * Time.deltaTime);

            ClampToBounds(b);
        }

        /// <summary>카메라 뷰를 경계 안으로 가둡니다. 한 축이 경계에 꽉 차면 그 축은 중앙 고정.</summary>
        private void ClampToBounds(Bounds b)
        {
            float halfH = _cam.orthographicSize;
            float halfW = halfH * _cam.aspect;

            float minX = b.min.x + halfW, maxX = b.max.x - halfW;
            float minY = b.min.y + halfH, maxY = b.max.y - halfH;

            float x = (minX > maxX) ? b.center.x : Mathf.Clamp(transform.position.x, minX, maxX);
            float y = (minY > maxY) ? b.center.y : Mathf.Clamp(transform.position.y, minY, maxY);
            transform.position = new Vector3(x, y, transform.position.z);
        }

        // ── 경계 소스 ─────────────────────────────────────────────────────────

        /// <summary>
        /// 경계 우선순위: CameraConfinerSync(플레이 카메라와 공유) → 맵 데이터 폴백.
        /// </summary>
        private bool TryGetBounds(out Bounds b)
        {
            b = default;

            var sync = CameraConfinerSync.Instance;
            if (sync != null && sync.HasBounds) { b = sync.WorldBounds; return true; }

            var loader = MapLoader.Instance;
            if (loader != null && loader.CurrentMap != null)
            {
                var map = loader.CurrentMap;
                Vector2 o = loader.MapOrigin;
                float w = map.width * map.tileUnit, h = map.height * map.tileUnit;
                b = new Bounds(new Vector3(o.x + w * 0.5f, o.y + h * 0.5f, 0f), new Vector3(w, h, 1f));
                return true;
            }
            return false;
        }
    }
}
