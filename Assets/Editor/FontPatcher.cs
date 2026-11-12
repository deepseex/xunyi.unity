using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Xunyi.EditorTools
{
    /// <summary>
    /// 把场景里所有 Text / TextMesh 的字体替换为工程内置的中文字体。
    /// WebGL 没有系统字体可回退，内置 LegacyRuntime.ttf 不含中文字形，
    /// 不换字体就会出现"汉字全空白"。
    /// </summary>
    public static class FontPatcher
    {
        private const string FontPath = "Assets/Fonts/XunyiCJK.otf";

        private static readonly string[] ScenePaths =
        {
            "Assets/Scenes/SampleScene.unity",
            "Assets/Scenes/Level1.unity",
        };

        [MenuItem("Tools/寻遗/修复中文字体")]
        public static void FixAll()
        {
            Font cjk = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (cjk == null)
            {
                Debug.LogError("[FontPatcher] 找不到字体：" + FontPath);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            int total = 0;

            foreach (string path in ScenePaths)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                int changed = 0;

                foreach (Text text in Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (text.font == cjk) continue;
                    text.font = cjk;
                    EditorUtility.SetDirty(text);
                    changed++;
                }

                foreach (TextMesh mesh in Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (mesh.font == cjk) continue;
                    mesh.font = cjk;
                    EditorUtility.SetDirty(mesh);
                    changed++;
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[FontPatcher] " + path + " 替换字体 " + changed + " 处");
                total += changed;
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[FontPatcher] 完成，共替换 " + total + " 处，字体 = " + cjk.name
                + "，字符数 = " + cjk.characterInfo.Length);
        }

        /// <summary>批处理入口：换字体后直接构建 WebGL。</summary>
        public static void FixAndBuild()
        {
            FixAll();
            WebGLBuilder.BuildWebGL();
        }
    }
}
