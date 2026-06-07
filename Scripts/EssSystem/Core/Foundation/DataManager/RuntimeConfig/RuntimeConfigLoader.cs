using System;
using System.IO;
using UnityEngine;

namespace EssSystem.Core.Foundation.DataManager.RuntimeConfig
{
    public static class RuntimeConfigLoader
    {
        public const string ContentRootFolderName = "FrameworkResources";
        public const string ConfigFolderName = "Config";

        public static bool TryLoadJson<T>(string relativePath, out T data, Action<string> log = null)
        {
            data = default;
            if (string.IsNullOrWhiteSpace(relativePath)) return false;

            var path = ResolvePath(relativePath);
            if (string.IsNullOrEmpty(path))
            {
                log?.Invoke($"Runtime config not found: {relativePath}");
                return false;
            }

            try
            {
                var json = File.ReadAllText(path);
                data = JsonUtility.FromJson<T>(json);
                log?.Invoke($"Loaded runtime config: {path}");
                return data != null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RuntimeConfigLoader] Failed to load {path}: {ex.Message}");
                return false;
            }
        }

        public static string ResolvePath(string relativePath)
        {
            relativePath = relativePath.Replace('\\', '/').TrimStart('/');

            var externalPath = Path.Combine(GetExternalConfigRootPath(), relativePath);
            if (File.Exists(externalPath)) return externalPath;

#if UNITY_EDITOR
            var editorPath = Path.Combine(GetEditorConfigRootPath(), relativePath);
            if (File.Exists(editorPath)) return editorPath;
#endif

            return null;
        }

        public static string GetExternalConfigRootPath()
        {
            var basePath = UnityEngine.Application.dataPath;
            var parent = Directory.GetParent(basePath);
            return Path.Combine(parent?.FullName ?? basePath, ContentRootFolderName, ConfigFolderName);
        }

        public static string GetEditorConfigRootPath()
        {
            var projectRoot = Directory.GetParent(UnityEngine.Application.dataPath);
            var root = projectRoot?.FullName ?? UnityEngine.Application.dataPath;
            return Path.Combine(root, "Assets", ContentRootFolderName, ConfigFolderName);
        }
    }
}
