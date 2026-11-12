using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Xunyi.Level
{
    /// <summary>
    /// 可旋转的飞檐廊桥。拖动改变朝向，松手吸附到 45° 网格；
    /// 只有接近目标角度（对齐）时，它才把两端连成可通行的路。
    /// 对齐后桥面会换成土黄色，作为不依赖相机的明确视觉反馈。
    /// </summary>
    public sealed class RotatingRamp : MonoBehaviour
    {
        // 必须序列化：否则存档后角度沿用缺省值 0，目标角度丢失，谜题直接失效
        [SerializeField] private float _targetAngle;
        [SerializeField] private float _angle;
        [SerializeField, Range(1f, 30f)] private float _tolerance = 12f;
        [SerializeField, Range(15f, 90f)] private float _snapStep = 45f;
        [SerializeField, Range(0.05f, 1.5f)] private float _dragSensitivity = 0.35f;
        [SerializeField, Range(0.05f, 0.6f)] private float _snapDuration = 0.16f;

        [Header("对齐反馈")]
        [SerializeField] private Color _alignedTint = new Color(0.643f, 0.506f, 0.365f, 1f);   // #A4815D 土黄

        private float _dragStartAngle;
        private float _dragStartX;
        private float _snapFrom;
        private float _snapTo;
        private float _snapTime = -1f;
        private bool _dragging;
        private bool _moved;
        private Camera _camera;

        private Renderer[] _renderers;
        private MaterialPropertyBlock _mpb;
        private Color _baseTint = Color.white;
        private bool _tintInitialised;
        private bool? _lastAligned;

        public bool IsAligned => Mathf.Abs(Mathf.DeltaAngle(_angle, _targetAngle)) <= _tolerance;
        public bool IsDragging => _dragging;

        public void Configure(float startAngle, float targetAngle)
        {
            _targetAngle = targetAngle;
            _angle = startAngle;
            transform.localRotation = Quaternion.Euler(0f, _angle, 0f);
        }

        private void Awake()
        {
            if (_camera == null) _camera = Camera.main;
            _renderers = GetComponentsInChildren<Renderer>();
            _mpb = new MaterialPropertyBlock();

            if (_renderers.Length > 0 && _renderers[0].sharedMaterial != null &&
                _renderers[0].sharedMaterial.HasProperty("_BaseColor"))
            {
                _baseTint = _renderers[0].sharedMaterial.GetColor("_BaseColor");
                _tintInitialised = true;
            }
        }

        private void Update()
        {
            UpdateSnap();
            UpdateTint();

            Pointer pointer = Pointer.current;
            if (pointer == null) return;

            if (pointer.press.wasPressedThisFrame) TryBeginDrag(pointer);
            else if (pointer.press.isPressed && _dragging) ContinueDrag(pointer);
            else if (pointer.press.wasReleasedThisFrame && _dragging) EndDrag();
        }

        private void UpdateSnap()
        {
            if (_snapTime < 0f) return;

            _snapTime += Time.deltaTime;
            float t = Mathf.Clamp01(_snapTime / _snapDuration);
            _angle = Mathf.Lerp(_snapFrom, _snapTo, Mathf.SmoothStep(0f, 1f, t));
            transform.localRotation = Quaternion.Euler(0f, _angle, 0f);

            if (t >= 1f)
            {
                _angle = _snapTo;
                _snapTime = -1f;
            }
        }

        private void UpdateTint()
        {
            if (!_tintInitialised || _renderers == null || _renderers.Length == 0) return;

            bool aligned = IsAligned;
            if (_lastAligned.HasValue && _lastAligned.Value == aligned) return;
            _lastAligned = aligned;

            Color tint = aligned ? _alignedTint : _baseTint;
            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer r = _renderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor("_BaseColor", tint);
                r.SetPropertyBlock(_mpb);
            }
        }

        private void TryBeginDrag(Pointer pointer)
        {
            if (Level1Director.IsCharacterMoving) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            Vector2 screen = pointer.position.ReadValue();
            Ray ray = _camera.ScreenPointToRay(screen);
            if (!Physics.Raycast(ray, out RaycastHit hit, 10000f)) return;
            if (hit.collider.transform != transform && !hit.collider.transform.IsChildOf(transform)) return;

            _dragging = true;
            _moved = false;
            _snapTime = -1f;
            _dragStartAngle = _angle;
            _dragStartX = screen.x;
        }

        private void ContinueDrag(Pointer pointer)
        {
            float dx = pointer.position.ReadValue().x - _dragStartX;
            if (Mathf.Abs(dx) > 6f) _moved = true;

            _angle = _dragStartAngle + dx * _dragSensitivity;
            transform.localRotation = Quaternion.Euler(0f, _angle, 0f);
        }

        private void EndDrag()
        {
            _dragging = false;

            // 轻点（没有明显拖动）等价于"转 45°"，方便触屏单手操作
            if (!_moved) _angle += _snapStep;

            _snapFrom = _angle;
            _snapTo = Mathf.Round(_angle / _snapStep) * _snapStep;
            _snapTime = 0f;
        }
    }
}
