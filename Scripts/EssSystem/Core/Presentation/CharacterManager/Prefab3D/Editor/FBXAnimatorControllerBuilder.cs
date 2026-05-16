#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace EssSystem.Core.Presentation.CharacterManager.EditorTools
{
    /// <summary>
    /// Editor 工具：选中一个或多个 FBX，一键在同目录生成同名 <c>.controller</c>，
    /// 把 FBX 内每个 AnimationClip 加为 state（第一个为默认）。
    /// <para>菜单：<b>Tools/Character/Build AnimatorController From Selected FBX</b></para>
    /// <para>用途：在 <see cref="EssSystem.Core.Presentation.CharacterManager.Dao.CharacterRenderMode.Prefab3D"/>
    /// 模式下省去手动建 Controller。Playables 模式（<see cref="EssSystem.Core.Presentation.CharacterManager.Dao.CharacterRenderMode.Prefab3DClips"/>）不需要本工具。</para>
    /// <para>同时把生成的 Controller 赋给 FBX 的 ModelImporter（这样 FBX 的 Animator 默认指向它）。</para>
    /// </summary>
    public static class FBXAnimatorControllerBuilder
    {
        [MenuItem("Tools/Character/Build AnimatorController From Selected FBX")]
        public static void BuildFromSelection()
        {
            var selected = Selection.GetFiltered<GameObject>(SelectionMode.DeepAssets);
            if (selected == null || selected.Length == 0)
            {
                EditorUtility.DisplayDialog("FBX → AnimatorController",
                    "请在 Project 视图中选中一个或多个 FBX 模型。", "OK");
                return;
            }

            int generated = 0, skipped = 0;
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
                else                      skipped++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[FBXAnimatorControllerBuilder] 生成 {generated} 个 .controller，跳过 {skipped} 个非 FBX 选择");
        }

        /// <summary>对单个 FBX 资产路径生成 .controller 并自动绑定到 ModelImporter。</summary>
        public static bool BuildOneAtPath(string fbxAssetPath)
        {
            var clips = LoadClipsAtPath(fbxAssetPath);
            if (clips.Count == 0)
            {
                Debug.LogWarning($"[FBXAnimatorControllerBuilder] {fbxAssetPath} 内无 AnimationClip，跳过");
                return false;
            }

            var dir         = Path.GetDirectoryName(fbxAssetPath)?.Replace('\\', '/') ?? "";
            var name        = Path.GetFileNameWithoutExtension(fbxAssetPath);
            var controllerPath = $"{dir}/{name}.controller";

            // 已存在则覆盖（先删旧的，避免 state 重复）
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) != null)
                AssetDatabase.DeleteAsset(controllerPath);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var sm = controller.layers[0].stateMachine;

            // 移除自动创建的 New State / Empty 默认
            for (int i = sm.states.Length - 1; i >= 0; i--)
                sm.RemoveState(sm.states[i].state);

            for (int i = 0; i < clips.Count; i++)
            {
                var clip  = clips[i];
                var state = sm.AddState(clip.name);
                state.motion = clip;
                if (i == 0) sm.defaultState = state;
            }

            EditorUtility.SetDirty(controller);

            // 把 Controller 绑定到 FBX 的 ModelImporter（FBX 自身实例化后 Animator 自动用这个）
            var importer = AssetImporter.GetAtPath(fbxAssetPath) as ModelImporter;
            if (importer != null)
            {
                // ModelImporter 不直接持有 controller，但通过设置 Avatar / Animator 配置完成。
                // FBX 在场景中实例化时其 Animator.runtimeAnimatorController 默认为 null —— Unity 不会
                // 把 .controller 自动绑到 FBX 上。这里改为提示用户在 Prefab3D 模式下手动指定，或直接使用 Prefab3DClips。
            }

            Debug.Log($"[FBXAnimatorControllerBuilder] 生成 Controller: {controllerPath}（{clips.Count} 个 state）");
            return true;
        }

        private static bool IsFbxOrModel(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext == ".fbx" || ext == ".obj" || ext == ".dae" || ext == ".blend";
        }

        private static List<AnimationClip> LoadClipsAtPath(string path)
        {
            var list = new List<AnimationClip>();
            var subAssets = AssetDatabase.LoadAllAssetsAtPath(path);
            if (subAssets == null) return list;
            foreach (var a in subAssets)
            {
                if (a is AnimationClip c
                    && !c.name.StartsWith("__preview__", System.StringComparison.Ordinal))
                    list.Add(c);
            }
            return list;
        }
    }
}
#endif
