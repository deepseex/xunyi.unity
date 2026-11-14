using System;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Xunyi.EditorTools
{
    /// <summary>
    /// iOS 适配：核对并打印 WebGL 相关设置，然后构建。
    /// 用反射读取，避免不同 Unity 小版本 API 差异导致编译失败。
    /// </summary>
    public static class WebGLiOSProfile
    {
        [MenuItem("Tools/寻遗/核对 WebGL 设置并构建")]
        public static void DiagnoseAndBuild()
        {
            DumpWebGLApi();
            DumpEffectiveSettings();
            WebGLBuilder.BuildWebGL();
        }

        public static void Diagnose()
        {
            DumpWebGLApi();
            DumpEffectiveSettings();
        }

        private static void DumpWebGLApi()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[iOSProfile] ===== PlayerSettings.WebGL 全部公开属性 =====");

            PropertyInfo[] props = typeof(PlayerSettings.WebGL).GetProperties(
                BindingFlags.Public | BindingFlags.Static);

            foreach (PropertyInfo p in props)
            {
                string value = "(读取失败)";
                try { value = Convert.ToString(p.GetValue(null)); }
                catch (Exception e) { value = "(" + e.GetType().Name + ")"; }
                sb.AppendLine("[iOSProfile]   " + p.Name + " : " + p.PropertyType.Name + " = " + value);
            }

            // 是否存在任何 WebGL 1.0 相关开关
            bool hasWebGL1 = false;
            foreach (PropertyInfo p in props)
            {
                string n = p.Name.ToLowerInvariant();
                if (n.Contains("webgl1") || n.Contains("version") && n.Contains("webgl"))
                { hasWebGL1 = true; sb.AppendLine("[iOSProfile]   >>> 疑似 WebGL 版本开关: " + p.Name); }
            }
            sb.AppendLine("[iOSProfile] 存在 WebGL 1.0 回退开关 = " + hasWebGL1);

            Debug.Log(sb.ToString());
        }

        private static void DumpEffectiveSettings()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[iOSProfile] ===== 实际生效值 =====");

            sb.AppendLine("[iOSProfile] compressionFormat = " + PlayerSettings.WebGL.compressionFormat);
            sb.AppendLine("[iOSProfile] exceptionSupport  = " + PlayerSettings.WebGL.exceptionSupport);
            sb.AppendLine("[iOSProfile] memoryGrowthMode  = " + PlayerSettings.WebGL.memoryGrowthMode);
            sb.AppendLine("[iOSProfile] initialMemorySize = " + PlayerSettings.WebGL.initialMemorySize + " MB");
            sb.AppendLine("[iOSProfile] maximumMemorySize = " + PlayerSettings.WebGL.maximumMemorySize + " MB");
            sb.AppendLine("[iOSProfile] template          = " + PlayerSettings.WebGL.template);
            sb.AppendLine("[iOSProfile] strippingLevel(WebGL) = "
                + PlayerSettings.GetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.WebGL));

            try
            {
                sb.AppendLine("[iOSProfile] il2cppCodeGeneration(WebGL) = "
                    + PlayerSettings.GetIl2CppCodeGeneration(UnityEditor.Build.NamedBuildTarget.WebGL));
            }
            catch (Exception e)
            {
                sb.AppendLine("[iOSProfile] il2cppCodeGeneration 读取失败: " + e.GetType().Name);
            }

            sb.AppendLine("[iOSProfile] stripEngineCode = " + PlayerSettings.stripEngineCode);

            Debug.Log(sb.ToString());
        }
    }
}
