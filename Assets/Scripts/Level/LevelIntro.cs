using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Xunyi.Level
{
    /// <summary>
    /// 进场：黑屏 + 关卡名"西安大雁塔"，停留 2 秒后淡出。
    /// 淡出期间不接受输入（由 Level1Director 查询 IsPlaying）。
    /// </summary>
    public sealed class LevelIntro : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private Text _title;
        [SerializeField] private string _titleText = "西安大雁塔";
        [SerializeField, Range(0.5f, 6f)] private float _holdSeconds = 2f;
        [SerializeField, Range(0.1f, 3f)] private float _fadeSeconds = 0.8f;

        public bool IsPlaying { get; private set; }

        private IEnumerator Start()
        {
            IsPlaying = true;

            if (_title != null) _title.text = _titleText;
            if (_group != null)
            {
                _group.alpha = 1f;
                _group.blocksRaycasts = true;
            }

            yield return new WaitForSecondsRealtime(_holdSeconds);

            float elapsed = 0f;
            while (elapsed < _fadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / _fadeSeconds));
                if (_group != null) _group.alpha = 1f - t;
                yield return null;
            }

            if (_group != null)
            {
                _group.alpha = 0f;
                _group.blocksRaycasts = false;
            }

            IsPlaying = false;
            gameObject.SetActive(false);
        }
    }
}
