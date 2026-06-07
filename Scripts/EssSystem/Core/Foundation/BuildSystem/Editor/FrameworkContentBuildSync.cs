#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace EssSystem.Core.Foundation.BuildSystem.Editor
{
    public static class FrameworkContentBuildSync
    {
        private const string MenuPath = "Tools/WulaSystem/Foundation/Resources/Export FrameworkResources To Last Build";
        private const string SourceContentRoot = "Assets/FrameworkResources";
        private const string OutputFolderName = "FrameworkResources";

        [MenuItem(MenuPath, priority = 1)]
        public static void ExportToLastBuildOutput()
        {
            var lastBuildLocation = EditorUserBuildSettings.GetBuildLocation(EditorUserBuildSettings.activeBuildTarget);
            if (string.IsNullOrEmpty(lastBuildLocation))
            {
                Debug.LogWarning("[FrameworkContentBuildSync] Last build output path is empty.");
                return;
            }

            CopyToOutputDirectory(Path.GetDirectoryName(lastBuildLocation));
        }

        public static void CopyToBuildOutput(UnityEditor.Build.Reporting.BuildReport report)
        {
            var outputPath = report?.summary.outputPath;
            if (string.IsNullOrEmpty(outputPath))
            {
                Debug.LogWarning("[FrameworkContentBuildSync] Build output path is empty, skip FrameworkResources export.");
                return;
            }

            CopyToOutputDirectory(Path.GetDirectoryName(outputPath));
        }

        private static void CopyToOutputDirectory(string outputDirectory)
        {
            if (!AssetDatabase.IsValidFolder(SourceContentRoot))
            {
                Debug.LogWarning($"[FrameworkContentBuildSync] Source missing: {SourceContentRoot}");
                return;
            }

            if (string.IsNullOrEmpty(outputDirectory))
            {
                Debug.LogWarning("[FrameworkContentBuildSync] Output directory is empty.");
                return;
            }

            var sourceFullPath = ToFullPath(SourceContentRoot);
            var targetFullPath = Path.Combine(outputDirectory, OutputFolderName);

            if (Directory.Exists(targetFullPath))
                Directory.Delete(targetFullPath, recursive: true);

            CopyDirectory(sourceFullPath, targetFullPath);
            AssetDatabase.Refresh();
            Debug.Log($"[FrameworkContentBuildSync] Exported: {SourceContentRoot} -> {targetFullPath}");
        }

        private static string ToFullPath(string assetPath)
        {
            var projectRoot = Directory.GetParent(UnityEngine.Application.dataPath)?.FullName ?? UnityEngine.Application.dataPath;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                if (file.EndsWith(".meta")) continue;
                if (file.EndsWith(".cs")) continue;
                var target = Path.Combine(targetDir, Path.GetFileName(file));
                File.Copy(file, target, overwrite: true);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var target = Path.Combine(targetDir, Path.GetFileName(dir));
                CopyDirectory(dir, target);
            }
        }
    }
}
#endif
