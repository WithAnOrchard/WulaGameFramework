#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace EssSystem.Core.Presentation.CharacterManager.EditorTools
{
    /// <summary>
    /// Editor 工具：**一次性**生成 `Sprite2DAnimator` 模式所需的 base AnimatorController 资产。
    /// <para><b>菜单</b>：Tools/Character/Build Sprite Animator Base Controller</para>
    /// <para><b>产物</b>：<c>Assets/Resources/Generated/CharacterAnimBase.controller</c>，
    /// 包含 8 个标准 state（Walk/Idle/Jump/Attack/Defend/Damage/Death/Special），
    /// 每个 state 一个占位 AnimationClip（作为运行时
    /// <see cref="AnimatorOverrideController"/> 的 override key）。</para>
    /// <para>占位 clip 的 <c>loopTime = true</c>；运行时用 wrapMode 实际控制是否循环
    /// （非循环动作由 <c>CharacterPartView2DAnimator</c> 监听 normalizedTime ≥ 1 手动 clamp）。</para>
    /// </summary>
    public static class CharacterAnimatorBaseControllerBuilder
    {
        public const string OutputAssetPath = "Assets/Resources/Generated/CharacterAnimBase.controller";

        /// <summary>Runtime 用 <c>Resources.Load&lt;RuntimeAnimatorController&gt;(BaseControllerResourcePath)</c> 取它。</summary>
        public const string BaseControllerResourcePath = "Generated/CharacterAnimBase";

        public static readonly string[] StandardStates =
            { "Walk", "Idle", "Jump", "Attack", "Defend", "Damage", "Death", "Special" };

        [MenuItem("Tools/Character/Build Sprite Animator Base Controller")]
        public static void Build()
        {
            EnsureFolder("Assets/Resources");
            EnsureFolder("Assets/Resources/Generated");

            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(OutputAssetPath) != null)
                AssetDatabase.DeleteAsset(OutputAssetPath);

            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(OutputAssetPath);
            var sm = ctrl.layers[0].stateMachine;
            for (int i = sm.states.Length - 1; i >= 0; i--)
                sm.RemoveState(sm.states[i].state);

            for (int i = 0; i < StandardStates.Length; i++)
            {
                var stateName = StandardStates[i];
                var state = sm.AddState(stateName);

                // 占位 clip —— 名字 = state 名（便于 AnimatorOverrideController 调试可读）
                var clip = new AnimationClip
                {
                    name = stateName,
                    frameRate = 12f,
                    legacy = false,
                };
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);

                // 子资产嵌入 controller，便于一起被 AssetDatabase 管理
                AssetDatabase.AddObjectToAsset(clip, ctrl);

                state.motion = clip;
                if (i == 0) sm.defaultState = state;
            }

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[CharacterAnimatorBaseControllerBuilder] base controller 生成: {OutputAssetPath}（{StandardStates.Length} 个 state）");
            EditorUtility.DisplayDialog("Build Sprite Animator Base Controller",
                $"已生成：\n{OutputAssetPath}\n\n含 {StandardStates.Length} 个 state，运行时 AnimatorOverrideController 会按 state 名替换 clip。",
                "OK");
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;
            var parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            var leaf = Path.GetFileName(assetPath);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
#endif
