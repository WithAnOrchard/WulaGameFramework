#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace EssSystem.Core.Foundation.BuildSystem.Editor
{
    /// <summary>
    /// EssSystem 构建菜单
    /// 
    /// 核心构建逻辑请使用 OneClickBuildHelper
    /// </summary>
    public static class EssSystemBuildMenu
    {
        [MenuItem("Build/WulaSystem/Foundation/Build Player %&b", priority = 1)]
        public static void BuildPlayer()
        {
            EditorApplication.ExecuteMenuItem("File/Build Settings...");
        }
    }
}
#endif
