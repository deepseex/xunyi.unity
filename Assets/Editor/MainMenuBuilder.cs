using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Xunyi.UI;

namespace Xunyi.EditorTools
{
    /// <summary>
    /// 一键生成《寻遗》主菜单场景结构：9:16 竖屏 Canvas + 背景 + 四按钮 + 三个子面板 + 两层遮罩。
    /// 只做搭建，不进入 Play 模式。
    /// </summary>
    public static class MainMenuBuilder
    {
        private const string BackgroundSource = @"E:\UNITY\寻遗\脸谱\菜单.jpg";
        private const string ArtFolder = "Assets/Art/UI";
        private const string SpritePath = ArtFolder + "/菜单.jpg";

        private const string BgmSource = @"E:\UNITY\寻遗\音乐\背景音乐.mp3";
        private const string AudioFolder = "Assets/Audio";
        private const string BgmPath = AudioFolder + "/背景音乐.mp3";

        // 1080 x 1920 参考分辨率下的版面常数
        private const int RefWidth = 1080;
        private const int RefHeight = 1920;
        private const float BottomThirdHeight = RefHeight / 3f;   // 640

        private static readonly Color TextColor = new Color(0.98f, 0.96f, 0.90f, 1f);
        private static readonly Color PlateColor = new Color(0.055f, 0.05f, 0.09f, 0.62f);
        private static readonly Color PanelPlateColor = new Color(0.14f, 0.13f, 0.19f, 0.92f);
        // Linear 色彩空间下 alpha 的感知衰减更慢，子面板底色必须接近不透明才压得住底图
        private static readonly Color PanelDimColor = new Color(0.035f, 0.03f, 0.055f, 0.965f);
        private static readonly Color CardColor = new Color(0.10f, 0.095f, 0.14f, 0.92f);

        [MenuItem("Tools/寻遗/构建主菜单")]
        public static void Build()
        {
            // 播放模式下的改动只是运行时临时对象，退出即丢失，且 MarkSceneDirty 会直接抛异常
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[MainMenuBuilder] 正在 Play 模式（或切换中），请先停止运行再构建。");
                return;
            }

            EnsureFolders();
            Sprite background = ImportBackground();
            AudioClip bgm = ImportBgm();

            DestroyIfExists("UI Canvas");
            DestroyIfExists("EventSystem");
            DestroyIfExists("AudioManager");

            CreateEventSystem();
            CreateAudioManager(bgm);

            Canvas canvas = CreateCanvas();
            Transform canvasRoot = canvas.transform;

            CreateBackground(canvasRoot, background);

            MainMenuController controller = canvas.gameObject.AddComponent<MainMenuController>();

            // --- 主菜单（四个入口） ---
            GameObject mainMenuRoot = CreateRect("MainMenu", canvasRoot).gameObject;
            Stretch(mainMenuRoot.GetComponent<RectTransform>());

            RectTransform buttonArea = CreateRect("ButtonArea", mainMenuRoot.transform);
            buttonArea.anchorMin = new Vector2(0f, 0f);
            buttonArea.anchorMax = new Vector2(1f, 1f / 3f);
            buttonArea.offsetMin = Vector2.zero;
            buttonArea.offsetMax = Vector2.zero;

            Button startButton = CreateButton(buttonArea, "BtnStart", "开始游戏", new Vector2(310f, 430f), 48);
            Button settingsButton = CreateButton(buttonArea, "BtnSettings", "设置", new Vector2(770f, 430f), 48);
            Button achievementsButton = CreateButton(buttonArea, "BtnAchievements", "成就", new Vector2(310f, 230f), 48);
            Button levelsButton = CreateButton(buttonArea, "BtnLevels", "关卡", new Vector2(770f, 230f), 48);

            // --- 设置面板 ---
            GameObject settingsPanel = CreatePanel(canvasRoot, "SettingsPanel", "设置", out Button settingsBack);
            SettingsPanelController settingsController = settingsPanel.AddComponent<SettingsPanelController>();
            CreatePlate(settingsPanel.transform, "Card", new Vector2(0f, -20f), new Vector2(1000f, 620f));

            Slider volumeSlider = CreateSliderRow(settingsPanel.transform, "音量", 140f, 0.8f);
            Slider brightnessSlider = CreateSliderRow(settingsPanel.transform, "亮度", 0f, 1f);   // 1 = 最亮

            Text brightnessHint = CreateText(settingsPanel.transform, "BrightnessHint",
                "（亮度通过画面遮罩模拟，非系统亮度）", 32, TextAnchor.MiddleCenter,
                new Color(0.75f, 0.75f, 0.8f, 0.85f));
            RectTransform hintRect = brightnessHint.rectTransform;
            hintRect.anchorMin = hintRect.anchorMax = new Vector2(0.5f, 0.5f);
            hintRect.pivot = new Vector2(0.5f, 0.5f);
            hintRect.sizeDelta = new Vector2(900f, 60f);
            hintRect.anchoredPosition = new Vector2(0f, -230f);

            // --- 成就面板 ---
            GameObject achievementsPanel = CreatePanel(canvasRoot, "AchievementsPanel", "成就", out Button achievementsBack);
            CreatePlate(achievementsPanel.transform, "Card", new Vector2(0f, 40f), new Vector2(900f, 280f));

            Text achievementsText = CreateText(achievementsPanel.transform, "Placeholder", "还没有任何成就", 52,
                TextAnchor.MiddleCenter, TextColor);
            RectTransform achRect = achievementsText.rectTransform;
            achRect.anchorMin = achRect.anchorMax = new Vector2(0.5f, 0.5f);
            achRect.pivot = new Vector2(0.5f, 0.5f);
            achRect.sizeDelta = new Vector2(900f, 200f);
            achRect.anchoredPosition = new Vector2(0f, 40f);

            // --- 关卡面板 ---
            GameObject levelsPanel = CreatePanel(canvasRoot, "LevelsPanel", "关卡", out Button levelsBack);
            // 注意：CreateButton 用左下角锚点，坐标相对父物体左下角，不是父物体中心。
            RectTransform levelArea = CreateRect("LevelGrid", levelsPanel.transform);
            levelArea.anchorMin = levelArea.anchorMax = new Vector2(0.5f, 0.5f);
            levelArea.pivot = new Vector2(0.5f, 0.5f);
            levelArea.sizeDelta = new Vector2(900f, 480f);
            levelArea.anchoredPosition = new Vector2(0f, 40f);

            Button[] levelButtons =
            {
                CreateButton(levelArea, "BtnLevel1", "关卡 1", new Vector2(210f, 360f), 46, PanelPlateColor),
                CreateButton(levelArea, "BtnLevel2", "关卡 2", new Vector2(690f, 360f), 46, PanelPlateColor),
                CreateButton(levelArea, "BtnLevel3", "关卡 3", new Vector2(210f, 150f), 46, PanelPlateColor),
                CreateButton(levelArea, "BtnLevel4", "关卡 4", new Vector2(690f, 150f), 46, PanelPlateColor),
            };

            // --- 遮罩层（白场转场 + 亮度压暗） ---
            ScreenFader fader = CreateFadeOverlay(canvasRoot);
            Image brightnessOverlay = CreateBrightnessOverlay(canvasRoot);

            // --- 事件绑定（持久化到场景，不是运行时委托） ---
            UnityEventTools.AddPersistentListener(startButton.onClick, controller.OnStartGame);
            UnityEventTools.AddPersistentListener(settingsButton.onClick, controller.OpenSettings);
            UnityEventTools.AddPersistentListener(achievementsButton.onClick, controller.OpenAchievements);
            UnityEventTools.AddPersistentListener(levelsButton.onClick, controller.OpenLevels);

            UnityEventTools.AddPersistentListener(settingsBack.onClick, controller.BackToMainMenu);
            UnityEventTools.AddPersistentListener(achievementsBack.onClick, controller.BackToMainMenu);
            UnityEventTools.AddPersistentListener(levelsBack.onClick, controller.BackToMainMenu);

            for (int i = 0; i < levelButtons.Length; i++)
                UnityEventTools.AddIntPersistentListener(levelButtons[i].onClick, controller.EnterLevel, i + 1);

            // --- 引用赋值 ---
            SetRef(controller, "_mainMenuRoot", mainMenuRoot);
            SetRef(controller, "_settingsPanel", settingsPanel);
            SetRef(controller, "_achievementsPanel", achievementsPanel);
            SetRef(controller, "_levelsPanel", levelsPanel);
            SetRef(controller, "_fader", fader);
            SetRef(controller, "_settings", settingsController);

            SetRef(settingsController, "_volumeSlider", volumeSlider);
            SetRef(settingsController, "_brightnessSlider", brightnessSlider);
            SetRef(settingsController, "_brightnessOverlay", brightnessOverlay);

            // 面板默认关闭，主菜单可见
            settingsPanel.SetActive(false);
            achievementsPanel.SetActive(false);
            levelsPanel.SetActive(false);
            mainMenuRoot.SetActive(true);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            Debug.Log("[MainMenuBuilder] 主菜单构建完成：Canvas 1080x1920 match=1，4 个入口按钮，3 个子面板，2 层遮罩，常驻 AudioManager（BGM 循环）。");
        }

        // ------------------------------------------------------------------
        // 资源
        // ------------------------------------------------------------------

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Art"))
                AssetDatabase.CreateFolder("Assets", "Art");
            if (!AssetDatabase.IsValidFolder(ArtFolder))
                AssetDatabase.CreateFolder("Assets/Art", "UI");
        }

        private static Sprite ImportBackground()
        {
            if (!File.Exists(BackgroundSource))
            {
                // 源目录可能已被清理（例如旧工程删除）：只要工程内已有这张贴图就直接复用
                TextureImporter fallback = AssetImporter.GetAtPath(SpritePath) as TextureImporter;
                if (fallback != null)
                {
                    ApplySpriteImportSettings(fallback);
                    Debug.Log("[MainMenuBuilder] 源图缺失，复用工程内已有底图：" + SpritePath);
                    return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
                }

                Debug.LogError("[MainMenuBuilder] 找不到底图：" + BackgroundSource + "，工程内也没有 " + SpritePath);
                return null;
            }

            string destination = BackgroundSource.Replace('\\', '/');
            int assetsIndex = destination.IndexOf("/Assets/", System.StringComparison.OrdinalIgnoreCase);
            if (assetsIndex >= 0)
            {
                // 源文件若已在工程内，直接用当前路径
                string existing = destination.Substring(assetsIndex + 1);
                TextureImporter existingImporter = AssetImporter.GetAtPath(existing) as TextureImporter;
                if (existingImporter != null)
                {
                    ApplySpriteImportSettings(existingImporter);
                    return AssetDatabase.LoadAssetAtPath<Sprite>(existing);
                }
            }

            File.Copy(BackgroundSource, SpritePath, true);
            AssetDatabase.ImportAsset(SpritePath, ImportAssetOptions.ForceUpdate);

            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
            if (importer == null)
            {
                Debug.LogError("[MainMenuBuilder] 导入器获取失败：" + SpritePath);
                return null;
            }

            ApplySpriteImportSettings(importer);
            return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        }

        private static void ApplySpriteImportSettings(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;              // Sprite (2D and UI)
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 4096;                                  // 源图 1600x2848，不裁切
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        // ------------------------------------------------------------------
        // 骨架
        // ------------------------------------------------------------------

        private static void DestroyIfExists(string name)
        {
            GameObject existing = GameObject.Find(name);
            if (existing != null) Object.DestroyImmediate(existing);
        }

        private static void CreateEventSystem()
        {
            GameObject go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            InputSystemUIInputModule module = go.GetComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();   // 项目是 Input System only，必须用这个模块
        }

        /// <summary>导入 BGM 并创建常驻 AudioManager（Play On Awake + Loop）。</summary>
        private static void CreateAudioManager(AudioClip clip)
        {
            GameObject go = new GameObject("AudioManager", typeof(AudioSource), typeof(Xunyi.Audio.AudioManager));

            AudioSource source = go.GetComponent<AudioSource>();
            source.clip = clip;
            source.playOnAwake = true;
            source.loop = true;
            source.spatialBlend = 0f;        // 2D 音频
            source.volume = 0.8f;

            SetRef(go.GetComponent<Xunyi.Audio.AudioManager>(), "_bgmSource", source);
        }

        private static AudioClip ImportBgm()
        {
            if (!AssetDatabase.IsValidFolder(AudioFolder))
                AssetDatabase.CreateFolder("Assets", "Audio");

            if (File.Exists(BgmSource))
            {
                File.Copy(BgmSource, BgmPath, true);
                AssetDatabase.ImportAsset(BgmPath, ImportAssetOptions.ForceUpdate);
            }

            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(BgmPath);
            if (clip == null)
                Debug.LogError("[MainMenuBuilder] BGM 载入失败：" + BgmPath);
            return clip;
        }

        private static Canvas CreateCanvas()
        {
            GameObject go = new GameObject("UI Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefWidth, RefHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;      // 高度优先：竖屏适配
            return canvas;
        }

        private static void CreateBackground(Transform parent, Sprite sprite)
        {
            GameObject go = new GameObject("Background", typeof(RectTransform), typeof(Image));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            Stretch(rect);

            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;        // 铺满全屏（源图本就是 9:16）
            image.raycastTarget = false;
            image.color = Color.white;
        }

        private static ScreenFader CreateFadeOverlay(Transform parent)
        {
            GameObject go = new GameObject("WhiteFadeOverlay", typeof(RectTransform), typeof(Image), typeof(ScreenFader));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            Stretch(rect);

            Image image = go.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = false;

            ScreenFader fader = go.GetComponent<ScreenFader>();
            fader.SetAlphaImmediate(0f);
            return fader;
        }

        private static Image CreateBrightnessOverlay(Transform parent)
        {
            GameObject go = new GameObject("BrightnessOverlay", typeof(RectTransform), typeof(Image));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            Stretch(rect);
            rect.SetAsLastSibling();             // Canvas 最顶层

            Image image = go.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = false;         // 不能挡住输入
            image.enabled = false;
            return image;
        }

        // ------------------------------------------------------------------
        // 控件
        // ------------------------------------------------------------------

        private static GameObject CreatePanel(Transform parent, string name, string title, out Button backButton)
        {
            GameObject panel = CreateRect(name, parent).gameObject;
            Stretch(panel.GetComponent<RectTransform>());

            GameObject dim = new GameObject("Dim", typeof(RectTransform), typeof(Image));
            RectTransform dimRect = (RectTransform)dim.transform;
            dimRect.SetParent(panel.transform, false);
            Stretch(dimRect);
            Image dimImage = dim.GetComponent<Image>();
            dimImage.color = PanelDimColor;
            dimImage.raycastTarget = true;       // 吃掉底层点击

            Text titleText = CreateText(panel.transform, "Title", title, 72, TextAnchor.MiddleCenter, TextColor);
            RectTransform titleRect = titleText.rectTransform;
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.sizeDelta = new Vector2(900f, 120f);
            titleRect.anchoredPosition = new Vector2(0f, -140f);

            backButton = CreateButton(panel.transform, "BtnBack", "返回", new Vector2(0f, 0f), 46, PanelPlateColor);
            RectTransform backRect = (RectTransform)backButton.transform;
            backRect.anchorMin = new Vector2(0.5f, 0f);
            backRect.anchorMax = new Vector2(0.5f, 0f);
            backRect.pivot = new Vector2(0.5f, 0f);
            backRect.sizeDelta = new Vector2(340f, 130f);
            backRect.anchoredPosition = new Vector2(0f, 170f);

            return panel;
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPosition,
            int fontSize, Color? plateColor = null)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(MenuButtonJuice));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(380f, 150f);
            rect.anchoredPosition = anchoredPosition;

            Image image = go.GetComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");  // Unity 默认 UI 材质
            image.type = Image.Type.Sliced;
            image.color = plateColor ?? PlateColor;

            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.86f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            Text text = CreateText(rect, "Label", label, fontSize, TextAnchor.MiddleCenter, TextColor);
            Stretch(text.rectTransform, 12f);

            Shadow shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
            shadow.effectDistance = new Vector2(2f, -2f);

            return button;
        }

        /// <summary>子面板内容底板：压暗背景、给文字和滑块一个稳定的对比面。</summary>
        private static Image CreatePlate(Transform parent, string name, Vector2 center, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = center;

            Image image = go.GetComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;
            image.color = CardColor;
            image.raycastTarget = false;

            // 排到 Dim 之上、标题与返回按钮之下
            rect.SetSiblingIndex(1);
            return image;
        }

        private static Slider CreateSliderRow(Transform parent, string label, float anchoredY, float initialValue)
        {
            Text caption = CreateText(parent, label + "Label", label, 46, TextAnchor.MiddleLeft, TextColor);
            RectTransform captionRect = caption.rectTransform;
            captionRect.anchorMin = captionRect.anchorMax = new Vector2(0.5f, 0.5f);
            captionRect.pivot = new Vector2(0f, 0.5f);
            captionRect.sizeDelta = new Vector2(300f, 80f);
            captionRect.anchoredPosition = new Vector2(-470f, anchoredY);

            Slider slider = CreateSlider(parent, label + "Slider", new Vector2(520f, 64f));
            RectTransform sliderRect = (RectTransform)slider.transform;
            sliderRect.anchorMin = sliderRect.anchorMax = new Vector2(0.5f, 0.5f);
            sliderRect.pivot = new Vector2(0f, 0.5f);
            sliderRect.sizeDelta = new Vector2(520f, 64f);
            sliderRect.anchoredPosition = new Vector2(-150f, anchoredY);

            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.value = initialValue;

            return slider;
        }

        private static Slider CreateSlider(Transform parent, string name, Vector2 size)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(Slider));
            RectTransform rootRect = (RectTransform)root.transform;
            rootRect.SetParent(parent, false);
            rootRect.sizeDelta = size;

            GameObject background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            RectTransform bgRect = (RectTransform)background.transform;
            bgRect.SetParent(rootRect, false);
            bgRect.anchorMin = new Vector2(0f, 0.32f);
            bgRect.anchorMax = new Vector2(1f, 0.68f);
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            Image bgImage = background.GetComponent<Image>();
            bgImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            bgImage.type = Image.Type.Sliced;
            bgImage.color = new Color(1f, 1f, 1f, 0.18f);

            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            RectTransform fillAreaRect = (RectTransform)fillArea.transform;
            fillAreaRect.SetParent(rootRect, false);
            fillAreaRect.anchorMin = new Vector2(0f, 0.32f);
            fillAreaRect.anchorMax = new Vector2(1f, 0.68f);
            fillAreaRect.offsetMin = new Vector2(0f, 0f);
            fillAreaRect.offsetMax = new Vector2(0f, 0f);

            GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            RectTransform fillRect = (RectTransform)fill.transform;
            fillRect.SetParent(fillAreaRect, false);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            Image fillImage = fill.GetComponent<Image>();
            fillImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            fillImage.type = Image.Type.Sliced;
            fillImage.color = new Color(0.98f, 0.86f, 0.55f, 0.95f);

            GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            RectTransform handleAreaRect = (RectTransform)handleArea.transform;
            handleAreaRect.SetParent(rootRect, false);
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = new Vector2(20f, 0f);
            handleAreaRect.offsetMax = new Vector2(-20f, 0f);

            GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            RectTransform handleRect = (RectTransform)handle.transform;
            handleRect.SetParent(handleAreaRect, false);
            handleRect.sizeDelta = new Vector2(48f, 68f);
            Image handleImage = handle.GetComponent<Image>();
            handleImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            handleImage.color = Color.white;

            Slider slider = root.GetComponent<Slider>();
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
            slider.transition = Selectable.Transition.ColorTint;

            return slider;
        }

        private static Text CreateText(Transform parent, string name, string content, int fontSize,
            TextAnchor anchor, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);

            Text text = go.GetComponent<Text>();
            text.font = GetDefaultFont();
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = false;
            return text;
        }

        private static Font GetDefaultFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return font;
        }

        // ------------------------------------------------------------------
        // 工具
        // ------------------------------------------------------------------

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect, float padding = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        private static void SetRef(Object target, string fieldName, Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError("[MainMenuBuilder] 找不到字段：" + fieldName + " @ " + target.name);
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
