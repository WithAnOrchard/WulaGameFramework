using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace EssSystem.Core.Foundation.ResourceManager.Editor
{
    /// <summary>
    /// 一键将 Assets/FrameworkResources 下的所有资源注册为 Addressable（Framework-Resources 组）。
    /// 在执行 migrate_framework_resources.ps1 并重新打开 Unity 后运行此菜单项。
    /// </summary>
    public static class AddressablesMigrationTool
    {
        private const string MENU_PREFIX = "Tools/WulaSystem/Foundation/Resource/Addressables/";
        private const string FrameworkGroup    = "Framework-Resources";
        private const string DobeCatGroup      = "DobeCat-Resources";
        private const string FrameworkRootPath = "Assets/FrameworkResources";
        private const string DobeCatRootPath   = "Assets/Demo/DobeCat/Resources";

        [MenuItem(MENU_PREFIX + "Migrate Framework Resources", priority = 1)]
        public static void MigrateFrameworkResources()
        {
            if (!AssetDatabase.IsValidFolder(FrameworkRootPath))
            {
                EditorUtility.DisplayDialog("迁移", $"找不到 {FrameworkRootPath}，请先运行 migrate_framework_resources.ps1", "OK");
                return;
            }
            var settings = EnsureAddressableSettings();
            var group    = EnsureGroup(settings, FrameworkGroup, isFramework: true);
            RegisterFolderAsAddressable(settings, group, FrameworkRootPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[AddressablesMigration] Framework-Resources 迁移完成（组: {FrameworkGroup}）");
            EditorUtility.DisplayDialog("迁移完成", $"已将 {FrameworkRootPath} 下所有资源注册到 Addressable 组 [{FrameworkGroup}]。\n\n后续 DobeCat 构建会自动跳过此组。", "OK");
        }

        [MenuItem(MENU_PREFIX + "Register DobeCat Resources", priority = 2)]
        public static void RegisterDobeCatResources()
        {
            if (!AssetDatabase.IsValidFolder(DobeCatRootPath))
            {
                EditorUtility.DisplayDialog("注册", $"找不到 {DobeCatRootPath}", "OK");
                return;
            }
            var settings = EnsureAddressableSettings();
            var group    = EnsureGroup(settings, DobeCatGroup, isFramework: false);
            RegisterFolderAsAddressable(settings, group, DobeCatRootPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[AddressablesMigration] DobeCat-Resources 注册完成（组: {DobeCatGroup}）");
        }

        // ─── 内部工具 ─────────────────────────────────────────────

        private static AddressableAssetSettings EnsureAddressableSettings()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                settings = AddressableAssetSettings.Create(
                    AddressableAssetSettingsDefaultObject.kDefaultConfigFolder,
                    AddressableAssetSettingsDefaultObject.kDefaultConfigAssetName,
                    createDefaultGroups: true,
                    isPersisted: true);
                AddressableAssetSettingsDefaultObject.Settings = settings;
                Debug.Log("[AddressablesMigration] Addressable Settings 已自动创建");
            }
            return settings;
        }

        private static AddressableAssetGroup EnsureGroup(AddressableAssetSettings settings, string groupName, bool isFramework)
        {
            var group = settings.FindGroup(groupName);
            if (group != null) return group;

            group = settings.CreateGroup(groupName, setAsDefaultGroup: false, readOnly: false, postEvent: false, schemasToCopy: null);
            var schema = group.AddSchema<BundledAssetGroupSchema>();
            schema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
            schema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
            schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            schema.IncludeInBuild = !isFramework; // Framework 组默认不打进 DobeCat 包
            Debug.Log($"[AddressablesMigration] 已创建 Addressable 组: {groupName} (IncludeInBuild={!isFramework})");
            return group;
        }

        private static void RegisterFolderAsAddressable(AddressableAssetSettings settings, AddressableAssetGroup group, string folderPath)
        {
            var guids = AssetDatabase.FindAssets("", new[] { folderPath });
            var count = 0;
            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.IsValidFolder(assetPath)) continue; // 跳过子文件夹本身

                // 地址 = 去掉根路径前缀，保持与原 Resources.Load 路径兼容
                var address = assetPath
                    .Replace(folderPath + "/", string.Empty)
                    .Replace('\\', '/');
                // 去掉扩展名（Resources.Load 不带扩展名）
                var dotIdx = address.LastIndexOf('.');
                if (dotIdx >= 0) address = address.Substring(0, dotIdx);

                var entry = settings.CreateOrMoveEntry(guid, group, readOnly: false, postEvent: false);
                entry.address = address;
                count++;
            }
            Debug.Log($"[AddressablesMigration] 注册了 {count} 个资源到组 [{group.Name}]");
        }
    }
}
