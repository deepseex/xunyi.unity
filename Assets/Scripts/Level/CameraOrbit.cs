using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Xunyi.Level
{
    /// <summary>
    /// 绕 Y 轴环绕相机（正交）。拖拽自由旋转，松手吸附到 90° 一档的四个等距视角。
    ///
    /// 解谜保护：
    /// 1) 连通判定完全在世界空间做（桥相对塔的方位角），相机怎么转都不影响判定；
    /// 2) 松手只吸附到 {45°,135°,225°,315°}——这四个视角对 X/Z 两轴的桥都是 45° 斜看，
    ///    读数质量一致，不会出现"某视角看过去像接上了"的歧义；
    /// 3) 旋转中禁止角色移动（IsOrbiting 由 指挥者 查询）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class CameraOrbit : MonoBehaviour
    {
        [Header("环绕参数")]
        [SerializeField] private Vector3 _pivot = Vector3.zero;
        [SerializeField] private float _pitch = 32f;
        [SerializeField] private float _yaw = 45f;
        [SerializeField] private float _distance = 225f;

        [Header("操作")]
        [SerializeField, Range(0.02f, 1.5f)] private float _dragSensitivity = 0.22f;
        [Tooltip("吸附基准角：45° 起始")]
        [SerializeField] private float _snapOrigin = 45f;
        [Tooltip("吸附步长：90° 一档，共 4 个等距视角")]
        [SerializeField] private float _snapStep = 90f;
        [SerializeField, Range(0.05f, 0.8f)] private float _snapDuration = 0.22f;
        [SerializeField, Range(2f, 40f)] private float _dragThresholdPixels = 10f;

        private float _snapFrom;
        private float _snapTo;
        private float _snapTime = -1f;

        private float _dragStartYaw;
        private float _dragStartX;
        private bool _dragging;
        private bool _moved;
        private Camera _camera;

        public bool IsOrbiting => _dragging || _snapTime >= 0f;
        public float Yaw => _yaw;

        public void Configure(Vector3 pivot, float distance, float pitch, float yaw)
        {
            _pivot = pivot;
            _distance = distance;
            _pitch = pitch;
            _yaw = yaw;
            Apply();
        }

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            Apply();
        }

        private void Update()
        {
            // 吸附动画
            if (_snapTime >= 0f)
            {
                _snapTime += Time.deltaTime;
                float t = Mathf.Clamp01(_snapTime / _snapDuration);
                _yaw = Mathf.Lerp(_snapFrom, _snapTo, Mathf.SmoothStep(0f, 1f, t));
                Apply();
                if (t >= 1f)
                {
                    _yaw = _snapTo;
                    _snapTime = -1f;
                }
            }

            Pointer pointer = Pointer.current;
            if (pointer == null) return;

            if (pointer.press.wasPressedThisFrame) TryBegin(pointer);
            else if (pointer.press.isPressed && _dragging) ContinueDrag(pointer);
            else if (pointer.press.wasReleasedThisFrame && _dragging) EndDrag(pointer);
        }

        private void TryBegin(Pointer pointer)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            if (_camera == null) _camera = GetComponent<Camera>();

            Vector2 screen = pointer.position.ReadValue();
            Ray ray = _camera.ScreenPointToRay(screen);
            if (Physics.Raycast(ray, out RaycastHit hit, 10000f) &&
                hit.collider.GetComponentInParent<RotatingRamp>() != null)
                return;   // 拖的是廊桥，不接管

            _dragging = true;
            _moved = false;
            _snapTime = -1f;
            _dragStartYaw = _yaw;
            _dragStartX = screen.x;
        }

        private void ContinueDrag(Pointer pointer)
        {
            float dx = pointer.position.ReadValue().x - _dragStartX;
            if (Mathf.Abs(dx) > _dragThresholdPixels) _moved = true;
            if (!_moved) return;

            _yaw = _dragStartYaw - dx * _dragSensitivity;
            Apply();
        }

        private void EndDrag(Pointer pointer)
        {
            _dragging = false;
            if (!_moved) return;   // 只是点了一下，不吸附

            float snapped = _snapOrigin + Mathf.Round((_yaw - _snapOrigin) / _snapStep) * _snapStep;
            _snapFrom = _yaw;
            _snapTo = snapped;
            _snapTime = 0f;
        }

        private void Apply()
        {
            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            transform.rotation = rotation;
            transform.position = _pivot - rotation * Vector3.forward * _distance;
        }
    }
}
