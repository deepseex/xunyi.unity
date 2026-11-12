using UnityEngine;
using UnityEngine.UI;

namespace Xunyi.Level
{
    /// <summary>
    /// 把主菜单设置里的"亮度"带到关卡场景：同一条公式，
    /// 遮罩 Alpha = (1 - 亮度) * 0.8。GameObject 失活时也会先读回一次。
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class BrightnessApplier : MonoBehaviour
    {
        private const string BrightnessKey = "settings.brightness.v2";
        private const string LegacyBrightnessKey = "settings.brightness";
        private const float MaxDimAlpha = 0.8f;

        [SerializeField] private Image _overlay;

        private void Awake()
        {
            if (_overlay == null) _overlay = GetComponent<Image>();
            Apply();
        }

        private void OnEnable()
        {
            Apply();
        }

        public void Apply()
        {
            if (_overlay == null) return;

            float brightness = ReadBrightness();
            Color c = _overlay.color;
            c.a = (1f - brightness) * MaxDimAlpha;

            _overlay.raycastTarget = false;
            _overlay.color = c;
            _overlay.enabled = c.a > 0.001f;
        }

        private static float ReadBrightness()
        {
            if (PlayerPrefs.HasKey(BrightnessKey))
                return Mathf.Clamp01(PlayerPrefs.GetFloat(BrightnessKey));

            if (PlayerPrefs.HasKey(LegacyBrightnessKey))
                return 1f - Mathf.Clamp01(PlayerPrefs.GetFloat(LegacyBrightnessKey));

            return 1f;
        }
    }
}
