using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Xunyi.Level
{
    /// <summary>
    /// 通关界面。版面严格按屏幕高度分三段：
    ///   50% 上段 —— 居中显示"第一关"插画
    ///   30% 中段 —— 显示题记文字
    ///   20% 下段 —— 最底部的"下一关"按钮
    /// </summary>
    public sealed class VictoryPanel : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _group;
        [SerializeField, Range(0.1f, 1.5f)] private float _showSeconds = 0.45f;

        public bool IsOpen => gameObject.activeSelf;

        // 注意：不要在这里 SetActive(false)。面板默认在场景里就是关闭的，
        // 一旦本组件随激活被唤醒又把自己关掉，Show() 的协程会一起被打断。
        public void Show()
        {
            if (IsOpen) return;
            gameObject.SetActive(true);
            StopAllCoroutines();
            StartCoroutine(ShowRoutine());
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private IEnumerator ShowRoutine()
        {
            if (_group == null) yield break;

            _group.alpha = 0f;
            _group.blocksRaycasts = true;
            _group.interactable = true;

            float elapsed = 0f;
            while (elapsed < _showSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / _showSeconds));
                _group.alpha = t;
                yield return null;
            }

            _group.alpha = 1f;
        }

        /// <summary>“下一关”按钮——暂时只做占位。</summary>
        public void OnNextLevel()
        {
            Debug.Log("下一关（占位）：第二关尚未搭建");
        }
    }
}
