using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Xunyi.Level
{
    /// <summary>
    /// 第一关节拍器：把"点击"翻译成沿当前连通路径前进，并判定通关。
    /// 路径是固定节点链，某一段是否可走由对应 RotatingRamp 是否对齐决定。
    /// </summary>
    public sealed class Level1Director : MonoBehaviour
    {
        [Header("角色")]
        [SerializeField] private CharacterWalker _walker;

        [Header("路径")]
        [SerializeField] private Transform[] _nodes;
        [Tooltip("长度 = 节点数-1；-1 表示该段永远可走，>=0 表示需要对应旋转件对齐")]
        [SerializeField] private int[] _segmentGate;

        [Header("旋转件")]
        [SerializeField] private RotatingRamp[] _ramps;

        [Header("流程")]
        [SerializeField] private LevelIntro _intro;
        [SerializeField] private VictoryPanel _victory;

        private int _current;
        private bool _moving;
        private bool _won;
        private Camera _camera;

        /// <summary>角色正在走路上时，禁止旋转任何飞檐层（否则路径节点会在脚下移动）。</summary>
        public static bool IsCharacterMoving { get; private set; }

        [Header("输入")]
        [Tooltip("按下到抬起的位移超过该像素数即视为拖拽（转相机/转桥），不算点击")]
        [SerializeField, Range(2f, 40f)] private float _clickThresholdPixels = 12f;

        private bool _pressValid;
        private Vector2 _pressPosition;

        public void TeleportToStart()
        {
            _current = 0;
            if (_walker != null && _nodes != null && _nodes.Length > 0)
                _walker.transform.position = _nodes[0].position;
        }

        private void Start()
        {
            _camera = Camera.main;
            TeleportToStart();
        }

        private void Update()
        {
            // 站着不动时锁在当前路径点上：旋转层转动会把角色一起带走，
            // 这样角色永远踩在采样出来的踏面上，不会悬空。
            StickToCurrentNode();

            if (_won || _moving) return;
            if (_intro != null && _intro.IsPlaying) return;
            if (_victory != null && _victory.IsOpen) return;

            Pointer pointer = Pointer.current;
            if (pointer == null) return;

            // 用"按下有效性 + 抬起时位移"判定点击：
            // 这样拖拽转相机、拖拽转桥都不会误触发走路。
            if (pointer.press.wasPressedThisFrame)
            {
                _pressValid = IsPressOnPlayfield(pointer);
                _pressPosition = pointer.position.ReadValue();
            }
            else if (pointer.press.wasReleasedThisFrame)
            {
                bool valid = _pressValid;
                _pressValid = false;
                if (!valid) return;
                if ((pointer.position.ReadValue() - _pressPosition).magnitude > _clickThresholdPixels) return;
                Advance();
            }
        }

        private void StickToCurrentNode()
        {
            if (_moving || _walker == null || _nodes == null) return;
            if (_current < 0 || _current >= _nodes.Length) return;
            if (_nodes[_current] == null) return;

            Vector3 target = _nodes[_current].position;
            float t = 1f - Mathf.Exp(-25f * Time.deltaTime);
            _walker.transform.position = Vector3.Lerp(_walker.transform.position, target, t);
        }

        private bool IsPressOnPlayfield(Pointer pointer)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return false;

            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return true;

            Ray ray = _camera.ScreenPointToRay(pointer.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, 10000f) &&
                hit.collider.GetComponentInParent<RotatingRamp>() != null)
                return false;   // 按在廊桥上：交给 RotatingRamp

            return true;
        }

        /// <summary>沿当前连通路径能走多远走多远。</summary>
        public void Advance()
        {
            if (_nodes == null || _nodes.Length < 2 || _walker == null) return;

            List<Vector3> path = new List<Vector3>();
            int index = _current;

            while (index < _nodes.Length - 1)
            {
                int gate = (index < _segmentGate.Length) ? _segmentGate[index] : -1;
                if (gate >= 0 && (_ramps == null || gate >= _ramps.Length || !_ramps[gate].IsAligned))
                    break;   // 这一段被关卡机关挡住了

                index++;
                path.Add(_nodes[index].position);
            }

            if (path.Count == 0) return;

            _moving = true;
            IsCharacterMoving = true;
            int arrived = index;
            _walker.Walk(path, () =>
            {
                _moving = false;
                IsCharacterMoving = false;
                _current = arrived;
                if (_current >= _nodes.Length - 1) Win();
            });
        }

        private void Win()
        {
            if (_won) return;
            _won = true;
            if (_victory != null) _victory.Show();
        }

        /// <summary>调试用：当前所有段是否全部连通。</summary>
        public bool IsRouteComplete()
        {
            for (int i = 0; i < _nodes.Length - 1; i++)
            {
                int gate = (i < _segmentGate.Length) ? _segmentGate[i] : -1;
                if (gate >= 0 && !_ramps[gate].IsAligned) return false;
            }
            return true;
        }
    }
}
