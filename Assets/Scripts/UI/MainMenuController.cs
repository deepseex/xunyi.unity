using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Xunyi.UI
{
    /// <summary>
    /// 主菜单总控：四个入口按钮、三个子面板的显隐、"开始游戏"的白场转场与关卡加载。
    /// 面板切换不用布尔堆叠，统一走 ShowMainMenu / ShowPanel 两个入口。
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        [Header("根节点")]
        [SerializeField] private GameObject _mainMenuRoot;
        [SerializeField] private GameObject _settingsPanel;
        [SerializeField] private GameObject _achievementsPanel;
        [SerializeField] private GameObject _levelsPanel;

        [Header("转场")]
        [SerializeField] private ScreenFader _fader;
        [SerializeField, Range(0.05f, 2f)] private float _fadeToWhiteDuration = 0.35f;
        [SerializeField, Range(0f, 3f)] private float _loadingHoldDuration = 0.45f;
        [SerializeField, Range(0.05f, 2f)] private float _fadeFromWhiteDuration = 0.40f;

        [Header("场景")]
        [Tooltip("必须与 Build Settings 里的关卡场景名一致")]
        [SerializeField] private string _firstLevelScene = "Level1";

        [Header("设置")]
        [SerializeField] private SettingsPanelController _settings;

        private bool _transitioning;

        private void Awake()
        {
            // 设置项要在进主菜单前就生效（面板默认隐藏，其 Awake 不会跑）。
            if (_settings != null) _settings.ApplyFromPrefs();

            if (_fader != null) _fader.SetAlphaImmediate(0f);
            ShowMainMenu();
        }

        // ---------- 主菜单入口 ----------

        public void OnStartGame()
        {
            if (_transitioning) return;
            StartCoroutine(StartGameRoutine());
        }

        private IEnumerator StartGameRoutine()
        {
            _transitioning = true;
            SetMenuInteractable(false);

            // 渐隐：透明 -> 全白
            yield return _fader.FadeTo(1f, _fadeToWhiteDuration);

            Debug.Log("加载占位：正在准备关卡 1…");
            yield return new WaitForSecondsRealtime(_loadingHoldDuration);

            Debug.Log("进入关卡1");
            LoadScene(_firstLevelScene);
        }

        public void OpenSettings() => ShowPanel(_settingsPanel);
        public void OpenAchievements() => ShowPanel(_achievementsPanel);
        public void OpenLevels() => ShowPanel(_levelsPanel);

        public void BackToMainMenu() => ShowMainMenu();

        /// <summary>关卡面板的 4 个按钮共用，参数由 Inspector 传入。</summary>
        public void EnterLevel(int level)
        {
            if (level == 1)
            {
                if (_transitioning) return;
                StartCoroutine(EnterLevelRoutine(_firstLevelScene));
                return;
            }

            Debug.Log("进入关卡" + level);
        }

        private IEnumerator EnterLevelRoutine(string sceneName)
        {
            _transitioning = true;
            SetMenuInteractable(false);

            yield return _fader.FadeTo(1f, _fadeToWhiteDuration);

            Debug.Log("进入关卡1");
            LoadScene(sceneName);
        }

        private void LoadScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError("[MainMenuController] 场景名称为空。");
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError("[MainMenuController] 场景未加入 Build Settings：" + sceneName);
                StartCoroutine(RecoverFromFailedLoad());
                return;
            }

            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }

        private IEnumerator RecoverFromFailedLoad()
        {
            yield return _fader.FadeTo(0f, _fadeFromWhiteDuration);
            _transitioning = false;
            SetMenuInteractable(true);
        }

        // ---------- 面板显隐 ----------

        public void ShowMainMenu()
        {
            SetActive(_mainMenuRoot, true);
            SetActive(_settingsPanel, false);
            SetActive(_achievementsPanel, false);
            SetActive(_levelsPanel, false);
        }

        public void ShowPanel(GameObject panel)
        {
            SetActive(_mainMenuRoot, false);
            SetActive(_settingsPanel, false);
            SetActive(_achievementsPanel, false);
            SetActive(_levelsPanel, false);
            SetActive(panel, true);
        }

        private void SetMenuInteractable(bool value)
        {
            if (_mainMenuRoot == null) return;
            foreach (Button button in _mainMenuRoot.GetComponentsInChildren<Button>(true))
                button.interactable = value;
        }

        private static void SetActive(GameObject target, bool value)
        {
            if (target != null && target.activeSelf != value) target.SetActive(value);
        }
    }
}
