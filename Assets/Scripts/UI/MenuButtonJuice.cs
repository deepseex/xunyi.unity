using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Xunyi.UI
{
    /// <summary>
    /// 按钮"按下缩小 -> 松手弹回"的灵动反馈。
    /// 曲线全部走 Mathf.SmoothStep，避免线性缩放带来的生硬感；
    /// 松手时先轻微过冲（overshoot）再平滑落回，形成"弹"的手感。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MenuButtonJuice : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [Header("按下")]
        [SerializeField, Range(0.80f, 1.00f)] private float _pressScale = 0.92f;
        [SerializeField, Range(0.03f, 0.25f)] private float _pressDuration = 0.09f;

        [Header("松手回弹")]
        [SerializeField, Range(0.08f, 0.40f)] private float _releaseDuration = 0.16f;
        [SerializeField, Range(1.00f, 1.20f)] private float _overshoot = 1.05f;
        [SerializeField, Range(0.05f, 0.40f)] private float _settleDuration = 0.13f;

        private Vector3 _baseScale = Vector3.one;
        private Coroutine _routine;
        private bool _isPressed;

        private void Awake()
        {
            _baseScale = transform.localScale;
        }

        private void OnDisable()
        {
            // 面板被隐藏时把缩放复位，避免下次显示时停在缩小状态。
            StopAnim();
            transform.localScale = _baseScale;
            _isPressed = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _isPressed = true;
            Play(_baseScale * _pressScale, _pressDuration, false);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _isPressed = false;
            Play(_baseScale, _releaseDuration, true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // 按住后拖出按钮范围：同样回弹，避免卡在压缩状态。
            if (!_isPressed) return;
            _isPressed = false;
            Play(_baseScale, _releaseDuration, true);
        }

        private void Play(Vector3 target, float duration, bool withBounce)
        {
            StopAnim();
            _routine = StartCoroutine(AnimateRoutine(target, duration, withBounce));
        }

        private void StopAnim()
        {
            if (_routine == null) return;
            StopCoroutine(_routine);
            _routine = null;
        }

        private IEnumerator AnimateRoutine(Vector3 target, float duration, bool withBounce)
        {
            yield return Tween(transform.localScale, target, duration);

            if (withBounce)
            {
                // 过冲：先弹过 1.0，再平滑收回，形成"弹"而不"跳"的观感。
                Vector3 peak = _baseScale * _overshoot;
                yield return Tween(_baseScale, peak, _releaseDuration * 0.45f);
                yield return Tween(peak, _baseScale, _settleDuration);
            }

            transform.localScale = target;
            _routine = null;
        }

        private IEnumerator Tween(Vector3 from, Vector3 to, float duration)
        {
            if (duration <= 0f)
            {
                transform.localScale = to;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;      // 转场期间 timeScale 可能为 0，用真实时间更稳。
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                transform.localScale = Vector3.LerpUnclamped(from, to, t);
                yield return null;
            }

            transform.localScale = to;
        }
    }
}
