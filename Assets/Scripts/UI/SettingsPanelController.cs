using UnityEngine;
using UnityEngine.UI;

namespace Xunyi.UI
{
    /// <summary>
    /// 设置面板：音量交给常驻 AudioManager（控制 BGM 源），
    /// 亮度用全屏黑色遮罩的 Alpha 模拟。两者都持久化到 PlayerPrefs。
    ///
    /// 亮度语义（v2）：滑块靠右 = 越亮。遮罩 Alpha = (1 - value) * maxDim，
    /// 所以 value=1 时完全不加暗，value=0 时压到最暗。
    /// </summary>
    public sealed class SettingsPanelController : MonoBehaviour
    {
        private const string BrightnessKey = "settings.brightness.v2";
        private const string LegacyBrightnessKey = "settings.brightness";
        private const string VolumeKeyFallback = "settings.volume";

        private const float DefaultVolume = 0.8f;
        private const float DefaultBrightness = 1f;   // 1 = 最亮

        [SerializeField] private Slider _volumeSlider;
        [SerializeField] private Slider _brightnessSlider;

        [Tooltip("层级最高的全屏黑色图，通过 Alpha 模拟屏幕变暗")]
        [SerializeField] private Image _brightnessOverlay;

        [Tooltip("亮度滑块拉到最左（value=0）时对应的最大遮罩 Alpha")]
        [SerializeField, Range(0f, 1f)] private float _maxDimAlpha = 0.8f;

        private void Awake()
        {
            if (_volumeSlider != null)
                _volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
            if (_brightnessSlider != null)
                _brightnessSlider.onValueChanged.AddListener(OnBrightnessChanged);

            ApplyFromPrefs();
        }

        private void OnDisable()
        {
            // 离开设置面板时统一落盘，避免拖动过程中每帧写盘。
            PlayerPrefs.Save();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) PlayerPrefs.Save();
        }

        /// <summary>从 PlayerPrefs 读取并应用（面板未激活时也可调用）。</summary>
        public void ApplyFromPrefs()
        {
            // 音量以 PlayerPrefs 为唯一真源：AudioManager 与 UI 各自读同一个键，
            // 不依赖两者 Awake 的先后顺序（读当前源音量会读到未初始化的默认值）。
            float volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKeyFallback, DefaultVolume));
            if (_volumeSlider != null) _volumeSlider.SetValueWithoutNotify(volume);

            if (Xunyi.Audio.AudioManager.Instance != null)
                Xunyi.Audio.AudioManager.Instance.SetVolume(volume);
            else
                AudioListener.volume = volume;

            float brightness = ReadBrightness();
            if (_brightnessSlider != null) _brightnessSlider.SetValueWithoutNotify(brightness);
            ApplyBrightness(brightness);
        }

        public void OnVolumeChanged(float value)
        {
            float v = Mathf.Clamp01(value);

            if (Xunyi.Audio.AudioManager.Instance != null)
            {
                Xunyi.Audio.AudioManager.Instance.SetVolume(v);   // 内部写 PlayerPrefs
            }
            else
            {
                // 极端情况下的兜底：管理器还没就绪
                AudioListener.volume = v;
                PlayerPrefs.SetFloat(VolumeKeyFallback, v);
            }
        }

        public void OnBrightnessChanged(float value)
        {
            float v = Mathf.Clamp01(value);
            ApplyBrightness(v);
            PlayerPrefs.SetFloat(BrightnessKey, v);
        }

        private void ApplyBrightness(float value)
        {
            if (_brightnessOverlay == null) return;

            float v = Mathf.Clamp01(value);

            // 滑块越靠右 -> 越亮 -> 黑色遮罩越淡
            Color c = _brightnessOverlay.color;
            c.a = (1f - v) * _maxDimAlpha;

            _brightnessOverlay.raycastTarget = false;   // 永不挡输入
            _brightnessOverlay.color = c;
            _brightnessOverlay.enabled = c.a > 0.001f;

            // 遮罩必须始终置顶，否则只压暗了部分 UI
            _brightnessOverlay.transform.SetAsLastSibling();
        }

        /// <summary>
        /// 读取亮度并按需迁移旧键：旧版语义 0=最亮，新版 1=最亮，所以取反。
        /// </summary>
        private float ReadBrightness()
        {
            if (PlayerPrefs.HasKey(BrightnessKey))
                return Mathf.Clamp01(PlayerPrefs.GetFloat(BrightnessKey));

            if (PlayerPrefs.HasKey(LegacyBrightnessKey))
            {
                float legacy = Mathf.Clamp01(PlayerPrefs.GetFloat(LegacyBrightnessKey));
                float migrated = 1f - legacy;
                PlayerPrefs.SetFloat(BrightnessKey, migrated);
                PlayerPrefs.Save();
                return migrated;
            }

            return DefaultBrightness;
        }
    }
}
