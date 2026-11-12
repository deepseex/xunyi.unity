using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Xunyi.EditorTools
{
    /// <summary>
    /// Unity 2022.3 专用配置：
    /// 1) 确保工程挂了 URP 渲染管线（工程是 Built-in 3D 模板，但材质用的是 URP/Lit）
    /// 2) 提供命令行入口，便于批处理构建
    /// </summary>
    public static class Setup2022
    {
        private const string CopiedRpPath = "Assets/Settings/Mobile_RPAsset.asset";
        private const string NewRpPath = "Assets/Settings/URP-Asset-2022.asset";
        private const string NewRendererPath = "Assets/Settings/URP-Renderer-2022.asset";

        [MenuItem("Tools/寻遗/配置 URP 渲染管线")]
        public static void EnsureUrp()
        {            UniversalRenderPipelineAsset rp =
                AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(CopiedRpPath);

            if (rp != null)
            {
                Debug.Log("[Setup2022] 复用工程内的 URP 资产: " + CopiedRpPath);
            }
            else
            {
                rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(NewRpPath);
            }

            if (rp == null)
            {
                // 没有可用资产时新建一套（Renderer + PipelineAsset）
                UniversalRendererData renderer =
                    ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, NewRendererPath);

                rp = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(rp, NewRpPath);
                Debug.Log("[Setup2022] 新建 URP 资产: " + NewRpPath);
            }

            GraphicsSettings.renderPipelineAsset = rp;

            int original = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = rp;
            }
            QualitySettings.SetQualityLevel(original, false);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[Setup2022] URP 已挂载: " + rp.name
                + "；当前管线 = " + GraphicsSettings.renderPipelineAsset);
        }

        /// <summary>把主菜单与关卡场景写入 Build Settings（新工程默认是空的）。</summary>
        [MenuItem("Tools/寻遗/配置 Build Settings 场景")]
        public static void EnsureBuildScenes()
        {
            string[] wanted =
            {
                "Assets/Scenes/SampleScene.unity",
                "Assets/Scenes/Level1.unity",
            };

            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>();
            foreach (string path in wanted)
            {
                if (System.IO.File.Exists(path))
                    list.Add(new EditorBuildSettingsScene(path, true));
                else
                    Debug.LogWarning("[Setup2022] 场景不存在，已跳过: " + path);
            }

            EditorBuildSettings.scenes = list.ToArray();
            Debug.Log("[Setup2022] Build Settings 场景数 = " + list.Count);
        }

        /// <summary>批处理入口：配置 URP + 场景，然后构建 WebGL。</summary>
        public static void PrepareAndBuild()
        {
            EnsureUrp();
            EnsureBuildScenes();
            WebGLBuilder.BuildWebGL();
        }
    }
}
