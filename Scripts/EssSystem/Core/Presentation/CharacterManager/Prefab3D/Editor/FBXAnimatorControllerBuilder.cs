#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace EssSystem.Core.Presentation.CharacterManager.EditorTools
{
    /// <summary>
    /// Builds an AnimatorController beside selected FBX/model assets.
    /// Menu: Tools/WulaSystem/Presentation/Character/3D/FBX/Build AnimatorController From Selected FBX
    /// </summary>
    public static class FBXAnimatorControllerBuilder
    {
        private const string MENU_PREFIX = "Tools/WulaSystem/Presentation/Character/3D/FBX/";

        [MenuItem(MENU_PREFIX + "Build AnimatorController From Selected FBX")]
        public static void BuildFromSelection()
        {
            var selected = Selection.GetFiltered<GameObject>(SelectionMode.DeepAssets);
            if (selected == null || selected.Length == 0)
            {
                EditorUtility.DisplayDialog("FBX -> AnimatorController",
                    "Please select one or more FBX model assets in the Project view.", "OK");
                return;
            }

            int generated = 0;
            int skipped = 0;
            foreach (var go in selected)
            {
                if (go == null) continue;

                var path = AssetDatabase.GetAssetPath(go);
                if (string.IsNullOrEmpty(path)) continue;

                if (!IsFbxOrModel(path))
                {
                    skipped++;
                    continue;
                }

                if (BuildOneAtPath(path)) generated++;
                else skipped++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[FBXAnimatorControllerBuilder] Generated {generated} controllers; skipped {skipped} selections.");
        }

        public static bool BuildOneAtPath(string fbxAssetPath)
        {
            var clips = LoadClipsAtPath(fbxAssetPath);
            if (clips.Count == 0)
            {
                Debug.LogWarning($"[FBXAnimatorControllerBuilder] No AnimationClip found in {fbxAssetPath}; skipped.");
                return false;
            }

            var dir = Path.GetDirectoryName(fbxAssetPath)?.Replace('\\', '/') ?? "";
            var name = Path.GetFileNameWithoutExtension(fbxAssetPath);
            var controllerPath = $"{dir}/{name}.controller";

            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) != null)
                AssetDatabase.DeleteAsset(controllerPath);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var stateMachine = controller.layers[0].stateMachine;

            for (int i = stateMachine.states.Length - 1; i >= 0; i--)
                stateMachine.RemoveState(stateMachine.states[i].state);

            for (int i = 0; i < clips.Count; i++)
            {
                var clip = clips[i];
                var state = stateMachine.AddState(clip.name);
                state.motion = clip;
                if (i == 0) stateMachine.defaultState = state;
            }

            EditorUtility.SetDirty(controller);
            Debug.Log($"[FBXAnimatorControllerBuilder] Generated controller: {controllerPath} ({clips.Count} states).");
            return true;
        }

        private static bool IsFbxOrModel(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext == ".fbx" || ext == ".obj" || ext == ".dae" || ext == ".blend";
        }

        private static List<AnimationClip> LoadClipsAtPath(string path)
        {
            var clips = new List<AnimationClip>();
            var subAssets = AssetDatabase.LoadAllAssetsAtPath(path);
            if (subAssets == null) return clips;

            foreach (var asset in subAssets)
            {
                if (asset is AnimationClip clip
                    && !clip.name.StartsWith("__preview__", System.StringComparison.Ordinal))
                    clips.Add(clip);
            }

            return clips;
        }
    }
}
#endif
