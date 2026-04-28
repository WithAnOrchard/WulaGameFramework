using UnityEditor;
using UnityEngine;

namespace EssSystem.EssManager.InventoryManager
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
        }
    }
}
