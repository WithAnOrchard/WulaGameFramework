#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Debug = UnityEngine.Debug;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Demo.Tribe.Editor
{
    /// <summary>
    /// Tribe build entry. It registers Assets/FrameworkResources/Tribe as Tribe-Resources
    /// and only includes that Addressable group for the Tribe player build.
    /// </summary>
    public class TribeBuildMenu : IPostprocessBuildWithReport
    {
        private const string MENU_PREFIX = "Build/WulaSystem/Demo/Tribe/";
        private const string ScenePath = "Assets/Demo/Tribe/Tribe.unity";
        private const string TribeGroup = "Tribe-Resources";
        private const string TribeRootPath = "Assets/FrameworkResources/Tribe";
        private const string AddressRootPath = "Assets/FrameworkResources/";

        private static readonly Dictionary<string, bool> PreviousIncludeStates = new Dictionary<string, bool>();

        private static string AutoOutputPath =>
            Path.Combine(
                Directory.GetParent(Application.dataPath)!.FullName,
                "Builds",
                "Tribe",
                "Tribe.exe");

        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            RestoreAddressableGroupStates();
        }

        [MenuItem(MENU_PREFIX + "Build Windows x64", priority = 1)]
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64);

        [MenuItem(MENU_PREFIX + "Register Tribe Resources", priority = 10)]
        public static void RegisterTribeResourcesMenu()
        {
            var settings = EnsureAddressableSettings();
            var count = RegisterTribeResources(settings);
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Tribe Resources",
                $"Registered {count} assets into Addressable group [{TribeGroup}].",
                "OK");
        }

        [MenuItem(MENU_PREFIX + "Show Output Folder", priority = 20)]
        public static void ShowOutputFolder()
        {
            var dir = Path.GetDirectoryName(AutoOutputPath)!;
            if (Directory.Exists(dir))
                EditorUtility.RevealInFinder(dir);
            else
                EditorUtility.DisplayDialog("Tribe Build", $"Output folder does not exist yet:\n{dir}", "OK");
        }

        [MenuItem(MENU_PREFIX + "Show Log Folder", priority = 21)]
        public static void ShowLogFolder()
        {
            var dir = Path.Combine(
                Directory.GetParent(Application.dataPath)!.FullName,
                "Logs",
                "Tribe");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            EditorUtility.RevealInFinder(dir);
        }

        public static void BuildFromCommandLine()
        {
            Build(BuildTarget.StandaloneWindows64);
        }

        private static void Build(BuildTarget target)
        {
            if (!File.Exists(Path.Combine(Application.dataPath, "..", ScenePath)))
            {
                var msg = $"Scene file does not exist:\n{ScenePath}";
                Debug.LogError($"[TribeBuild] {msg}");
                if (!Application.isBatchMode)
                    EditorUtility.DisplayDialog("Tribe Build Failed", msg, "OK");
                return;
            }

            if (!AssetDatabase.IsValidFolder(TribeRootPath))
            {
                var msg = $"Resource folder does not exist:\n{TribeRootPath}";
                Debug.LogError($"[TribeBuild] {msg}");
                if (!Application.isBatchMode)
                    EditorUtility.DisplayDialog("Tribe Build Failed", msg, "OK");
                return;
            }

            var outputPath = AutoOutputPath;
            var dir = Path.GetDirectoryName(outputPath)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var settings = EnsureAddressableSettings();
            var registeredCount = RegisterTribeResources(settings);
            SetOnlyTribeGroupIncluded(settings);

            var previousBuildOption = settings.BuildAddressablesWithPlayerBuild;
            settings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;

            BuildReport report = null;
            try
            {
                Debug.Log($"[TribeBuild] Registered {registeredCount} Tribe assets. Building Addressables...");
                AddressableAssetSettings.BuildPlayerContent(out var result);
                if (!string.IsNullOrEmpty(result?.Error))
                {
                    Debug.LogError($"[TribeBuild] Addressables build failed: {result.Error}");
                    return;
                }

                Debug.Log($"[TribeBuild] Building player -> {outputPath}");
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = outputPath,
                    target = target,
                    options = BuildOptions.None,
                });
            }
            finally
            {
                settings.BuildAddressablesWithPlayerBuild = previousBuildOption;
                RestoreAddressableGroupStates();
                LogBuildResult(report, outputPath);
            }
        }

        private static AddressableAssetSettings EnsureAddressableSettings()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings != null) return settings;

            settings = AddressableAssetSettings.Create(
                AddressableAssetSettingsDefaultObject.kDefaultConfigFolder,
                AddressableAssetSettingsDefaultObject.kDefaultConfigAssetName,
                createDefaultGroups: true,
                isPersisted: true);
            AddressableAssetSettingsDefaultObject.Settings = settings;
            return settings;
        }

        private static AddressableAssetGroup EnsureTribeGroup(AddressableAssetSettings settings)
        {
            var group = settings.FindGroup(TribeGroup);
            if (group == null)
            {
                group = settings.CreateGroup(TribeGroup, false, false, false, null);
            }

            var schema = group.GetSchema<BundledAssetGroupSchema>() ?? group.AddSchema<BundledAssetGroupSchema>();
            schema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
            schema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
            schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            schema.IncludeInBuild = true;
            return group;
        }

        private static int RegisterTribeResources(AddressableAssetSettings settings)
        {
            var group = EnsureTribeGroup(settings);
            var guids = AssetDatabase.FindAssets("", new[] { TribeRootPath });
            var count = 0;

            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.IsValidFolder(assetPath)) continue;
                if (assetPath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;

                var address = ToAddress(assetPath);
                if (string.IsNullOrEmpty(address)) continue;

                var entry = settings.CreateOrMoveEntry(guid, group, false, false);
                entry.address = address;
                AddLabels(settings, entry, address);
                count++;
            }

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            return count;
        }

        private static string ToAddress(string assetPath)
        {
            var path = assetPath.Replace('\\', '/');
            if (!path.StartsWith(AddressRootPath, StringComparison.OrdinalIgnoreCase)) return null;

            var address = path.Substring(AddressRootPath.Length);
            var dot = address.LastIndexOf('.');
            if (dot >= 0) address = address.Substring(0, dot);
            return address;
        }

        private static void AddLabels(AddressableAssetSettings settings, AddressableAssetEntry entry, string address)
        {
            AddLabel(settings, entry, "Tribe");

            var parts = address.Split('/');
            var current = "";
            for (int i = 0; i < parts.Length - 1; i++)
            {
                current = string.IsNullOrEmpty(current) ? parts[i] : $"{current}/{parts[i]}";
                AddLabel(settings, entry, current);
            }

            AddLabel(settings, entry, address);
        }

        private static void AddLabel(AddressableAssetSettings settings, AddressableAssetEntry entry, string label)
        {
            if (string.IsNullOrEmpty(label)) return;
            settings.AddLabel(label, false);
            entry.SetLabel(label, true, true, false);
        }

        private static void SetOnlyTribeGroupIncluded(AddressableAssetSettings settings)
        {
            PreviousIncludeStates.Clear();
            foreach (var group in settings.groups)
            {
                if (group == null) continue;
                var schema = group.GetSchema<BundledAssetGroupSchema>();
                if (schema == null) continue;

                PreviousIncludeStates[group.Name] = schema.IncludeInBuild;
                schema.IncludeInBuild = group.Name == TribeGroup;
                EditorUtility.SetDirty(group);
            }

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        private static void RestoreAddressableGroupStates()
        {
            if (PreviousIncludeStates.Count == 0) return;

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) return;

            foreach (var pair in PreviousIncludeStates)
            {
                var group = settings.FindGroup(pair.Key);
                var schema = group?.GetSchema<BundledAssetGroupSchema>();
                if (schema == null) continue;

                schema.IncludeInBuild = pair.Value;
                EditorUtility.SetDirty(group);
            }

            PreviousIncludeStates.Clear();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        private static void LogBuildResult(BuildReport report, string outputPath)
        {
            if (report == null) return;

            var summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
                Debug.Log($"[TribeBuild] Build succeeded {summary.totalSize / 1048576f:F1} MB -> {outputPath}");
            else
                Debug.LogError($"[TribeBuild] Build failed: {summary.result}, errors={summary.totalErrors}, warnings={summary.totalWarnings}");
        }
    }
}
#endif
