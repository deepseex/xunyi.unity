using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Xunyi.Level
{
    /// <summary>
    /// 沿预设节点序列行走的角色控制器。逐点插值，移动时朝向行进方向。
    /// 不做物理推挤：本关是"路径连通"解谜，路径由 Level1Director 决定。
    /// </summary>
    public sealed class CharacterWalker : MonoBehaviour
    {
        [SerializeField, Range(0.5f, 12f)] private float _speed = 3.4f;
        [SerializeField, Range(90f, 1080f)] private float _turnSpeed = 540f;

        private Coroutine _routine;

        public bool IsWalking => _routine != null;

        public void Stop()
        {
            if (_routine == null) return;
            StopCoroutine(_routine);
            _routine = null;
        }

        public void Walk(IList<Vector3> path, Action onComplete)
        {
            Stop();
            if (path == null || path.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }
            _routine = StartCoroutine(WalkRoutine(path, onComplete));
        }

        private IEnumerator WalkRoutine(IList<Vector3> path, Action onComplete)
        {
            for (int i = 0; i < path.Count; i++)
            {
                Vector3 target = path[i];

                while ((transform.position - target).sqrMagnitude > 0.0016f)
                {
                    Vector3 to = target - transform.position;
                    Vector3 flat = new Vector3(to.x, 0f, to.z);

                    if (flat.sqrMagnitude > 0.0004f)
                    {
                        Quaternion look = Quaternion.LookRotation(flat.normalized, Vector3.up);
                        transform.rotation = Quaternion.RotateTowards(
                            transform.rotation, look, _turnSpeed * Time.deltaTime);
                    }

                    transform.position = Vector3.MoveTowards(
                        transform.position, target, _speed * Time.deltaTime);

                    yield return null;
                }

                transform.position = target;
            }

            _routine = null;
            onComplete?.Invoke();
        }
    }
}
