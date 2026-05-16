using UnityEditor;
using UnityEngine;

namespace EssSystem.Core.Application.SingleManagers.InventoryManager
{
    [CustomEditor(typeof(InventoryManager))]
    public class InventoryManagerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var inventoryManager = target as InventoryManager;
            if (inventoryManager == null) return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("数据工具", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            if (GUILayout.Button("重新加载数据", GUILayout.Height(30)))
            {
                inventoryManager.SendMessage("EditorReloadData");
            }

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(!UnityEngine.Application.isPlaying))
            {
                if (GUILayout.Button("添加随机物品到目标容器", GUILayout.Height(30)))
                {
                    inventoryManager.SendMessage("EditorAddRandomItem");
                }
            }
            if (!UnityEngine.Application.isPlaying)
                EditorGUILayout.HelpBox("「添加随机物品」需 Play 模式（依赖 Service.Instance）", MessageType.Info);

            EditorGUILayout.Space();
        }
    }
}
