using Abbey.Core;
using UnityEngine;

namespace Abbey.CameraRig
{
    /// <summary>
    /// The locked isometric camera (ART_BIBLE.md camera contract — never break):
    /// orthographic, pitch 30 / yaw 45, zoom changes orthographic size ONLY, no
    /// rotation API of any kind. WASD/arrow keys pan on the ground plane, the
    /// scroll wheel zooms, an optional follow target overrides panning, and the
    /// focus point can be clamped to world bounds.
    /// Tunables (pan speed, zoom speed/limits) live in <see cref="PrototypeConfig"/>.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public class IsoCameraController : MonoBehaviour
    {
        public const float Pitch = 30f;
        public const float Yaw = 45f;

        [Tooltip("Optional transform to follow. When set, WASD panning is ignored.")]
        [SerializeField] Transform followTarget;

        [Tooltip("Clamp the focus point to worldBoundsMin/Max on the XZ plane.")]
        [SerializeField] bool useWorldBounds;
        [SerializeField] Vector2 worldBoundsMin = new Vector2(-50f, -50f);
        [SerializeField] Vector2 worldBoundsMax = new Vector2(50f, 50f);

        [Tooltip("Distance the camera sits back from the focus point along its view axis.")]
        [SerializeField, Min(1f)] float rigDistance = 40f;

        Camera _camera;
        PrototypeConfig _config;
        Vector3 _focusPoint;

        public Camera TargetCamera
        {
            get
            {
                if (_camera == null)
                {
                    _camera = GetComponent<Camera>();
                }
                return _camera;
            }
        }

        public PrototypeConfig Config
        {
            get
            {
                if (_config == null)
                {
                    _config = PrototypeConfig.LoadOrDefault();
                }
                return _config;
            }
            set { _config = value; }
        }

        /// <summary>The ground point the camera looks at (y = 0 plane).</summary>
        public Vector3 FocusPoint => _focusPoint;

        public Transform FollowTarget => followTarget;

        void Awake()
        {
            EnforceProjection();
            _camera.orthographicSize = Config.cameraDefaultOrthoSize;
            _focusPoint = ComputeFocusFromPosition();
            ApplyFocus();
        }

        void OnValidate()
        {
            EnforceProjection();
        }

        void LateUpdate()
        {
            EnforceProjection();

            if (Application.isPlaying)
            {
                ReadPanInput();
                ReadZoomInput();
            }

            if (followTarget != null)
            {
                _focusPoint = new Vector3(followTarget.position.x, 0f, followTarget.position.z);
            }

            ApplyFocus();
        }

        /// <summary>Locks projection and rotation. There is no way to rotate this camera.</summary>
        void EnforceProjection()
        {
            if (_camera == null)
            {
                _camera = GetComponent<Camera>();
            }
            if (_camera != null)
            {
                _camera.orthographic = true;
            }
            transform.rotation = Quaternion.Euler(Pitch, Yaw, 0f);
        }

        void ReadPanInput()
        {
            if (followTarget != null)
            {
                return;
            }

            float x = Input.GetAxisRaw("Horizontal");
            float z = Input.GetAxisRaw("Vertical");
            if (Mathf.Approximately(x, 0f) && Mathf.Approximately(z, 0f))
            {
                return;
            }

            // Camera-relative ground directions under the fixed 45° yaw.
            Vector3 forward = Quaternion.Euler(0f, Yaw, 0f) * Vector3.forward;
            Vector3 right = Quaternion.Euler(0f, Yaw, 0f) * Vector3.right;
            Vector3 delta = (right * x + forward * z).normalized
                            * Config.cameraPanSpeed * Time.deltaTime;
            _focusPoint += delta;
        }

        void ReadZoomInput()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Approximately(scroll, 0f))
            {
                return;
            }
            SetZoom(TargetCamera.orthographicSize - scroll * Config.cameraZoomSpeed);
        }

        /// <summary>Zoom changes orthographic size only, clamped to config limits.</summary>
        public void SetZoom(float orthographicSize)
        {
            TargetCamera.orthographicSize = Mathf.Clamp(
                orthographicSize, Config.cameraMinOrthoSize, Config.cameraMaxOrthoSize);
        }

        public void SetFollowTarget(Transform target)
        {
            followTarget = target;
        }

        /// <summary>Snaps the focus point to a world position (XZ).</summary>
        public void FocusOn(Vector3 worldPosition)
        {
            followTarget = null;
            _focusPoint = new Vector3(worldPosition.x, 0f, worldPosition.z);
            ApplyFocus();
        }

        public void SetWorldBounds(Vector2 min, Vector2 max)
        {
            worldBoundsMin = Vector2.Min(min, max);
            worldBoundsMax = Vector2.Max(min, max);
            useWorldBounds = true;
        }

        public void ClearWorldBounds()
        {
            useWorldBounds = false;
        }

        void ApplyFocus()
        {
            if (useWorldBounds)
            {
                _focusPoint.x = Mathf.Clamp(_focusPoint.x, worldBoundsMin.x, worldBoundsMax.x);
                _focusPoint.z = Mathf.Clamp(_focusPoint.z, worldBoundsMin.y, worldBoundsMax.y);
            }
            _focusPoint.y = 0f;
            transform.position = _focusPoint - transform.forward * rigDistance;
        }

        Vector3 ComputeFocusFromPosition()
        {
            // Project the current camera position forward onto the y = 0 plane.
            Vector3 origin = transform.position;
            Vector3 dir = transform.forward;
            if (Mathf.Abs(dir.y) < 0.0001f)
            {
                return new Vector3(origin.x, 0f, origin.z);
            }
            float t = -origin.y / dir.y;
            Vector3 hit = origin + dir * Mathf.Max(t, 0f);
            return new Vector3(hit.x, 0f, hit.z);
        }
    }
}
