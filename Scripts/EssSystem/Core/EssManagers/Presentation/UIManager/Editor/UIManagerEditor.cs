using UnityEditor;
using UnityEngine;

namespace EssSystem.Core.EssManagers.Presentation.UIManager
{
    [CustomEditor(typeof(UIManager))]
    public class UIManagerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var uiManager = target as UIManager;
            if (uiManager == null) return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("热重载工具", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            if (GUILayout.Button("热重载UI配置", GUILayout.Height(30)))
            {
                uiManager.SendMessage("EditorHotReloadUIConfigs");
            }

            EditorGUILayout.Space();
        }
    }
}
