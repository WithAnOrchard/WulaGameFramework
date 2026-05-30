#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace EssSystem.Core.Foundation.BuildSystem
{
    /// <summary>
    /// 通用构建助手 - 支持 Addressable 资源隔离
    /// </summary>
    public static class OneClickBuildHelper
    {
        private const string LogTag = "[Build]";

        /// <summary>
        /// 快速构建
        /// </summary>
        public static bool QuickBuild(
            string projectName,
            string scenePath,
            BuildTarget target = BuildTarget.StandaloneWindows64,
            string frameworkGroup = "Framework-Resources")
        {
            var outputPath = Path.Combine(
                Directory.GetParent(UnityEngine.Application.dataPath)?.FullName ?? UnityEngine.Application.dataPath,
                "Builds", projectName, projectName + ".exe");

            var sceneFullPath = Path.Combine(UnityEngine.Application.dataPath, "..", scenePath).Replace('\\', '/');
            
            if (!File.Exists(sceneFullPath))
            {
                Debug.LogError(LogTag + " 场景文件不存在: " + scenePath);
                return false;
            }

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            Debug.Log(LogTag + " === 构建: " + projectName + " ===");

            bool prevIncludeInBuild = true;
            var addrSettings = AddressableAssetSettingsDefaultObject.Settings;
            
            if (!string.IsNullOrEmpty(frameworkGroup))
            {
                prevIncludeInBuild = SetAddressableGroupIncluded(addrSettings, frameworkGroup, false);
            }

            BuildAddressableContent();

            var prevBuildOption = addrSettings?.BuildAddressablesWithPlayerBuild 
                                 ?? AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;
            if (addrSettings != null)
                addrSettings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;

            BuildReport report = null;
            bool buildSuccess = false;
            
            try
            {
                Debug.Log(LogTag + " ▶ 构建 Player...");
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes           = new[] { scenePath },
                    locationPathName = outputPath,
                    target           = target,
                    options          = BuildOptions.None,
                });
                
                buildSuccess = report?.summary.result == BuildResult.Succeeded;
            }
            finally
            {
                if (addrSettings != null)
                    addrSettings.BuildAddressablesWithPlayerBuild = prevBuildOption;
                
                if (!string.IsNullOrEmpty(frameworkGroup))
                {
                    SetAddressableGroupIncluded(addrSettings, frameworkGroup, prevIncludeInBuild);
                }
                
                LogBuildResult(report, outputPath);
            }

            return buildSuccess;
        }

        private static bool SetAddressableGroupIncluded(AddressableAssetSettings settings, string groupName, bool included)
        {
            if (settings == null) return true;
            
            var group = settings.FindGroup(groupName);
            if (group == null) return true;
            
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            if (schema == null) return true;
            
            bool prevValue = schema.IncludeInBuild;
            schema.IncludeInBuild = included;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            
            return prevValue;
        }

        private static void BuildAddressableContent()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.Log(LogTag + " Addressable Settings 未配置，跳过");
                return;
            }
            
            Debug.Log(LogTag + " 构建 Addressable...");
            AddressableAssetSettings.BuildPlayerContent(out var result);
            
            if (!string.IsNullOrEmpty(result?.Error))
            {
                Debug.LogError(LogTag + " Addressable 错误: " + result.Error);
            }
        }

        private static void LogBuildResult(BuildReport report, string outputPath)
        {
            if (report == null) return;
            
            var summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log(LogTag + " ✓ 成功 " + (summary.totalSize / 1048576f).ToString("F1") + " MB → " + outputPath);
            }
            else
            {
                Debug.LogError(LogTag + " ✗ 失败: " + summary.result);
            }
        }

        /// <summary>
        /// 显示输出文件夹
        /// </summary>
        public static void ShowOutputFolder(string outputPath)
        {
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                EditorUtility.RevealInFinder(dir);
            }
        }
    }
}
#endif
