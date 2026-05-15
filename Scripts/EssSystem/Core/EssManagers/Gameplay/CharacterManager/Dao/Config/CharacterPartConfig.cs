using System;
using System.Collections.Generic;
using UnityEngine;

namespace EssSystem.Core.EssManagers.Gameplay.CharacterManager.Dao
{
    /// <summary>
    /// 单个部件配置 —— 描述部件类型、初始 Sprite、本地变换以及（动态部件的）动作列表。
    /// </summary>
    [Serializable]
    public class CharacterPartConfig
    {
        #region Identity / Type

        /// <summary>部件 ID，在所属 Character 内唯一（例如 "Body" / "Head" / "Weapon"）。</summary>
        public string PartId = string.Empty;

        /// <summary>部件类型：静态或动态。</summary>
        public CharacterPartType PartType = CharacterPartType.Static;

        #endregion

        #region Transform / Render

        /// <summary>相对 Character 根节点的本地坐标。</summary>
        public Vector3 LocalPosition = Vector3.zero;

        /// <summary>本地缩放。</summary>
        public Vector3 LocalScale = Vector3.one;

        /// <summary>本地欧拉角（仅 3D Prefab 模式生效；2D Sprite 模式忽略）。</summary>
        public Vector3 LocalEulerAngles = Vector3.zero;

        /// <summary>SpriteRenderer 的 sortingOrder（同层内的前后顺序，越大越靠前）。仅 2D 生效。</summary>
        public int SortingOrder = 0;

        /// <summary>SpriteRenderer 的 color（白色 = 不染色）。仅 2D 生效。</summary>
        public Color Color = Color.white;

        /// <summary>初始是否可见。</summary>
        public bool IsVisible = true;

        /// <summary>
        /// 运动角色：决定该部件在 <see cref="Runtime.CharacterView.PlayLocomotion"/> 与
        /// <see cref="Runtime.CharacterView.TriggerAttack"/> 中如何分派动作。
        /// <list type="bullet">
        /// <item><b>Movement</b>（默认）：只播 Walk / Idle，攻击和跳跃期间也保持移动动作。</item>
        /// <item><b>Body</b>：在空中播 Jump，地面播 Walk / Idle（身体躯干）。</item>
        /// <item><b>Attack</b>：攻击窗口内播 Attack；非攻击期间退化为 Walk / Idle。</item>
        /// </list>
        /// </summary>
        public CharacterLocomotionRole LocomotionRole = CharacterLocomotionRole.Movement;

        #endregion

        #region Static Mode

        /// <summary>仅静态部件使用：固定 Sprite Id（2D）。</summary>
        public string StaticSpriteId = string.Empty;

        #endregion

        #region 3D Prefab Mode

        /// <summary>
        /// 仅 3D 模式（<see cref="CharacterRenderMode.Prefab3D"/>）使用：Prefab 资源 ID（路径）。
        /// 通过 ResourceManager 的 <c>GetResource</c> Event 以 <c>"Prefab"</c> 类型加载。
        /// </summary>
        public string PrefabPath = string.Empty;

        #endregion

        #region Dynamic Mode

        /// <summary>仅动态部件使用：所有动作配置。</summary>
        public List<CharacterActionConfig> Animations = new List<CharacterActionConfig>();

        /// <summary>仅动态部件使用：默认起播动作名（为空则不自动播放）。</summary>
        public string DefaultActionName = string.Empty;

        #endregion

        public CharacterPartConfig() { }

        public CharacterPartConfig(string partId, CharacterPartType type)
        {
            PartId = partId;
            PartType = type;
        }

        public CharacterPartConfig WithStatic(string spriteId)
        {
            PartType = CharacterPartType.Static;
            StaticSpriteId = spriteId ?? string.Empty;
            return this;
        }

        public CharacterPartConfig WithDynamic(string defaultActionName, params CharacterActionConfig[] actions)
        {
            PartType = CharacterPartType.Dynamic;
            DefaultActionName = defaultActionName ?? string.Empty;
            Animations = new List<CharacterActionConfig>(actions ?? Array.Empty<CharacterActionConfig>());
            return this;
        }

        public CharacterPartConfig WithLocalPosition(Vector3 pos)        { LocalPosition = pos; return this; }
        public CharacterPartConfig WithLocalScale(Vector3 scale)         { LocalScale = scale; return this; }
        public CharacterPartConfig WithLocalRotation(Vector3 eulerAngles){ LocalEulerAngles = eulerAngles; return this; }
        public CharacterPartConfig WithSortingOrder(int order)           { SortingOrder = order; return this; }
        public CharacterPartConfig WithColor(Color c)                    { Color = c; return this; }
        public CharacterPartConfig WithVisible(bool v)                   { IsVisible = v; return this; }
        public CharacterPartConfig WithLocomotionRole(CharacterLocomotionRole role) { LocomotionRole = role; return this; }

        /// <summary>
        /// 3D 模式专用：指定 Prefab 资源路径，并把 PartType 标记为 <see cref="CharacterPartType.Dynamic"/>
        /// （3D 部件天然有 Animator，不区分 Static/Dynamic）。
        /// 后续 <c>WithDynamic</c> / <c>Animations</c> 中的 ActionConfig 应填充 <c>AnimatorStateName</c>。
        /// </summary>
        public CharacterPartConfig WithPrefab(string prefabPath)
        {
            PartType   = CharacterPartType.Dynamic;
            PrefabPath = prefabPath ?? string.Empty;
            return this;
        }

        /// <summary>按动作名查找 <see cref="CharacterActionConfig"/>，找不到返回 null。</summary>
        public CharacterActionConfig GetAction(string actionName)
        {
            if (Animations == null || string.IsNullOrEmpty(actionName)) return null;
            for (var i = 0; i < Animations.Count; i++)
                if (Animations[i] != null && Animations[i].ActionName == actionName)
                    return Animations[i];
            return null;
        }
    }
}
