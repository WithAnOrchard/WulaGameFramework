#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Demo.DayNight;
using Demo.DayNight.WaveSpawn;
using Demo.DayNight.BaseDefense;
using Demo.DayNight.Construction;
using Demo.DayNight.Hud;
using EssSystem.EssManager.MapManager;

namespace Demo.DayNight.EditorTools
{
    /// <summary>
    /// 一键把昼夜求生 Demo 的全部 Manager（WaveSpawn / BaseDefense / Construction / Hud）
    /// 挂到当前选中的 <see cref="DayNightGameManager"/> 节点上。
    /// <para>菜单：<c>Tools → DayNight Demo → Setup Managers On Selection</c></para>
    /// </summary>
    public static class DayNightSceneSetup
    {
        private const string MenuRoot = "Tools/DayNight Demo/";

        [MenuItem(MenuRoot + "Setup Managers On Selection", priority = 100)]
        public static void SetupManagersOnSelection()
        {
            var go = Selection.activeGameObject;
            DayNightGameManager root;

            if (go != null)
            {
                root = go.GetComponent<DayNightGameManager>()
                    ?? go.GetComponentInParent<DayNightGameManager>();
            }
            else
            {
                root = Object.FindFirstObjectByType<DayNightGameManager>(FindObjectsInactive.Include);
            }

            if (root == null)
            {
                EditorUtility.DisplayDialog(
                    "DayNight Demo Setup",
                    "未找到 DayNightGameManager。\n请先在场景中创建一个 GameObject 并添加 DayNightGameManager 组件，或在层级面板里选中已有的节点。",
                    "OK");
                return;
            }

            var target = root.gameObject;
            Undo.RegisterFullObjectHierarchyUndo(target, "Setup DayNight Demo Managers");

            var added = 0;
            added += AddIfMissing<WaveSpawnManager>(target);
            added += AddIfMissing<BaseDefenseManager>(target);
            added += AddIfMissing<ConstructionManager>(target);
            added += AddIfMissing<DayNightHudManager>(target);
            added += EnsureMapManagerChild(target);

            EditorUtility.SetDirty(target);
            EditorSceneManager.MarkSceneDirty(target.scene);

            Debug.Log($"[DayNightSceneSetup] 在 '{target.name}' 上完成 setup —— 新增 {added} 个组件" +
                      (added == 0 ? "（已全部存在）" : string.Empty));
            Selection.activeGameObject = target;
            EditorGUIUtility.PingObject(target);
        }

        [MenuItem(MenuRoot + "Create DayNight Root + Managers", priority = 101)]
        public static void CreateDayNightRoot()
        {
            var existing = Object.FindFirstObjectByType<DayNightGameManager>(FindObjectsInactive.Include);
            if (existing != null)
            {
                EditorUtility.DisplayDialog(
                    "DayNight Demo Setup",
                    $"场景中已有 DayNightGameManager（{existing.name}）。\n直接对它使用 'Setup Managers On Selection' 即可。",
                    "OK");
                Selection.activeGameObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing);
                return;
            }

            var go = new GameObject("DayNightGameRoot");
            Undo.RegisterCreatedObjectUndo(go, "Create DayNight Root");
            go.AddComponent<DayNightGameManager>();
            AddIfMissing<WaveSpawnManager>(go);
            AddIfMissing<BaseDefenseManager>(go);
            AddIfMissing<ConstructionManager>(go);
            AddIfMissing<DayNightHudManager>(go);
            EnsureMapManagerChild(go);

            EditorSceneManager.MarkSceneDirty(go.scene);
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
            Debug.Log("[DayNightSceneSetup] 已创建 DayNightGameRoot 并挂载全部 Manager。");
        }

        private static int AddIfMissing<T>(GameObject host) where T : Component
        {
            if (host.GetComponent<T>() != null) return 0;
            Undo.AddComponent<T>(host);
            Debug.Log($"[DayNightSceneSetup] 添加组件 {typeof(T).Name} 到 '{host.name}'");
            return 1;
        }

        /// <summary>
        /// 在 root 下找/建一个 MapManager 子节点（按惯例放子节点而不是根节点，与运行时 SyncMapTemplateBeforeInit 行为一致）。
        /// </summary>
        private static int EnsureMapManagerChild(GameObject root)
        {
            // 已经有任何 MapManager（root 或其子节点）就跳过
            if (root.GetComponentInChildren<MapManager>(true) != null) return 0;

            var holder = new GameObject(nameof(MapManager));
            Undo.RegisterCreatedObjectUndo(holder, "Create MapManager");
            Undo.SetTransformParent(holder.transform, root.transform, "Parent MapManager");
            holder.transform.SetParent(root.transform, false);
            Undo.AddComponent<MapManager>(holder);
            Debug.Log($"[DayNightSceneSetup] 创建 MapManager 子节点于 '{root.name}'");
            return 1;
        }
    }
}
#endif
