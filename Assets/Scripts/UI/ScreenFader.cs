using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Xunyi.UI
{
    /// <summary>
    /// 全屏纯白遮罩的渐隐/渐显（这里是"由透明渐白"与"由白渐透明"）。
    /// 用 Mathf.SmoothStep 做缓动，转场首尾更柔和；时间走 unscaledDeltaTime。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public sealed class ScreenFader : MonoBehaviour
    {
        [SerializeField] private Image _image;
        [SerializeField, Range(0f, 1f)] private float _alpha;

        public float Alpha => _alpha;

        private void Awake()
        {
            if (_image == null) _image = GetComponent<Image>();
            _image.raycastTarget = false;   // 遮罩不吃点击，避免挡住下层按钮
            SetAlphaImmediate(_alpha);
        }

        public void SetAlphaImmediate(float alpha)
        {
            _alpha = Mathf.Clamp01(alpha);
            if (_image == null) _image = GetComponent<Image>();
            if (_image == null) return;

            Color c = _image.color;
            c.a = _alpha;
            _image.color = c;

            // 完全透明时不参与渲染，省一次全屏填充。
            _image.enabled = _alpha > 0.001f;
        }

        /// <summary>渐变到目标透明度，可直接 yield return 等待完成。</summary>
        public IEnumerator FadeTo(float targetAlpha, float duration)
        {
            if (_image == null) _image = GetComponent<Image>();

            float from = _alpha;
            float to = Mathf.Clamp01(targetAlpha);

            if (duration <= 0f || Mathf.Approximately(from, to))
            {
                SetAlphaImmediate(to);
                yield break;
            }

            _image.enabled = true;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                SetAlphaImmediate(Mathf.LerpUnclamped(from, to, t));
                yield return null;
            }

            SetAlphaImmediate(to);
        }
    }
}
