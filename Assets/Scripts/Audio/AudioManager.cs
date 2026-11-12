using UnityEngine;

namespace Xunyi.Audio
{
    /// <summary>
    /// 常驻音频管理：持有背景音乐 AudioSource，跨场景不销毁。
    /// 音量只作用于 BGM 这个 AudioSource；AudioListener.volume 固定为 1，
    /// 避免"全局音量 × 源音量"双重衰减（滑块 0.5 变成实际 0.25）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AudioManager : MonoBehaviour
    {
        private const string VolumeKey = "settings.volume";
        private const float DefaultVolume = 0.8f;

        public static AudioManager Instance { get; private set; }

        [SerializeField] private AudioSource _bgmSource;

        public AudioSource BgmSource => _bgmSource;
        public float Volume => _bgmSource != null ? _bgmSource.volume : 0f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);          // 场景里重复放置时只保留第一个
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);     // 常驻：切场景不中断音乐

            if (_bgmSource == null) _bgmSource = GetComponent<AudioSource>();
            if (_bgmSource == null)
            {
                Debug.LogError("[AudioManager] 缺少 AudioSource，背景音乐无法播放。");
                return;
            }

            _bgmSource.playOnAwake = true;
            _bgmSource.loop = true;
            _bgmSource.spatialBlend = 0f;      // 纯 2D，不会被相机距离影响

            // 全局音量保持 1，音量大小全部交给 BGM 源，避免双重乘算
            AudioListener.volume = 1f;

            ApplyVolume(PlayerPrefs.GetFloat(VolumeKey, DefaultVolume));

            // playOnAwake 已经会起播；这里只兜底 clip 手动赋值却没自动播的情况
            if (!_bgmSource.isPlaying && _bgmSource.clip != null) _bgmSource.Play();
        }

        /// <summary>设置背景音乐音量（0~1）并立即持久化。</summary>
        public void SetVolume(float value)
        {
            ApplyVolume(value);
            PlayerPrefs.SetFloat(VolumeKey, Volume);
            PlayerPrefs.Save();
        }

        private void ApplyVolume(float value)
        {
            if (_bgmSource == null) return;
            _bgmSource.volume = Mathf.Clamp01(value);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) PlayerPrefs.Save();
        }

        private void OnApplicationQuit()
        {
            PlayerPrefs.Save();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
