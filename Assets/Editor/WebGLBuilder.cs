using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Xunyi.EditorTools
{
    /// <summary>
    /// WebGL 构建入口。既可以在编辑器菜单里点，也可以命令行批处理：
    ///
    ///   "E:\UNITY\Editor\Unity.exe" -batchmode -quit ^
    ///     -projectPath "E:\UNITY\寻遗" ^
    ///     -executeMethod Xunyi.EditorTools.WebGLBuilder.BuildWebGL ^
    ///     -logFile "E:\UNITY\寻遗\Builds\webgl-build.log"
    ///
    /// 产物：E:\UNITY\寻遗\Builds\WebGL（index.html 在根，适合直接做 GitHub Pages 源）
    /// </summary>
    public static class WebGLBuilder
    {
        private const string OutputDir = "Builds/WebGL";

        [MenuItem("Tools/寻遗/构建 WebGL")]
        public static void BuildWebGL()
        {
            // Unity 2022.3 的 API：PlayerSettings.WebGL.compressionFormat
            // （Unity 6 才是 EditorUserBuildSettings.webGLCompressionFormat）
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            AssetDatabase.SaveAssets();

            // 2) 切平台
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            {
                bool switched = EditorUserBuildSettings.SwitchActiveBuildTarget(
                    BuildTargetGroup.WebGL, BuildTarget.WebGL);
                if (!switched)
                {
                    Fail("切换到 WebGL 平台失败（可能未安装 WebGL Build Support 模块）。");
                    return;
                }
            }

            // 3) 收集 Build Settings 里的启用场景
            string[] scenes = Array.ConvertAll(
                Array.FindAll(EditorBuildSettings.scenes, s => s.enabled),
                s => s.path);

            if (scenes.Length == 0)
            {
                Fail("Build Settings 里没有启用任何场景。");
                return;
            }

            string fullOut = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), OutputDir));
            Directory.CreateDirectory(fullOut);

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = fullOut,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None,
            };

            Debug.Log("[WebGLBuilder] 开始构建 → " + fullOut + "，场景：" + string.Join(", ", scenes));

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log("[WebGLBuilder] 构建成功："
                    + (summary.totalSize / 1024f / 1024f).ToString("F1") + " MB，"
                    + summary.totalTime.TotalSeconds.ToString("F0") + " 秒");
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError("[WebGLBuilder] 构建失败：" + summary.result
                    + "，错误数 " + summary.totalErrors);
                EditorApplication.Exit(1);
            }
        }

        private static void Fail(string message)
        {
            Debug.LogError("[WebGLBuilder] " + message);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            else EditorUtility.DisplayDialog("WebGL 构建", message, "好");
        }
    }
}
