using System;
using System.Collections.Generic;
using UnityEngine;

namespace EssSystem.EssManager.CharacterManager.Dao
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

        /// <summary>SpriteRenderer 的 sortingOrder（同层内的前后顺序，越大越靠前）。</summary>
        public int SortingOrder = 0;

        /// <summary>SpriteRenderer 的 color（白色 = 不染色）。</summary>
        public Color Color = Color.white;

        /// <summary>初始是否可见。</summary>
        public bool IsVisible = true;

        #endregion

        #region Static Mode

        /// <summary>仅静态部件使用：固定 Sprite Id。</summary>
        public string StaticSpriteId = string.Empty;

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

        public CharacterPartConfig WithLocalPosition(Vector3 pos) { LocalPosition = pos; return this; }
        public CharacterPartConfig WithLocalScale(Vector3 scale)  { LocalScale = scale; return this; }
        public CharacterPartConfig WithSortingOrder(int order)    { SortingOrder = order; return this; }
        public CharacterPartConfig WithColor(Color c)             { Color = c; return this; }
        public CharacterPartConfig WithVisible(bool v)            { IsVisible = v; return this; }

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
