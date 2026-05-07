using UnityEditor;
using UnityEngine;

namespace EssSystem.Core.EssManagers.Gameplay.MapManager.TopDown2D
{
    /// <summary>
    /// MapManager 自定义 Inspector：在默认 Inspector 下方追加「重新生成地图」按钮，
    /// 用 Inspector 当前 Perlin 参数覆盖默认配置，并重建运行时 Map + MapView。
    /// </summary>
    [CustomEditor(typeof(MapManager))]
    public class MapManagerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var manager = target as MapManager;
            if (manager == null) return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("地图调试工具", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("用当前参数重新生成地图", GUILayout.Height(32)))
                {
                    manager.RegenerateDefaultMap();
                }
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "「重新生成地图」需 Play 模式（依赖 MapService 单例与已创建的 Map 实例）。\n" +
                    "Editor 模式下修改上方参数将作为下次启动 Play 时的默认值。",
                    MessageType.Info);
            }
        }
    }
}
