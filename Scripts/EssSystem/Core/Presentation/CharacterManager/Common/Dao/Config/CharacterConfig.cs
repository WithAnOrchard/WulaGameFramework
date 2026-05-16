using System;
using System.Collections.Generic;
using UnityEngine;

namespace EssSystem.Core.Presentation.CharacterManager.Dao
{
    /// <summary>
    /// Character 顶层配置 —— 一个 Character 由多个 <see cref="CharacterPartConfig"/> 组成，
    /// 注册到 <see cref="CharacterManager.CharacterService"/> 后可按 <see cref="ConfigId"/> 实例化。
    /// </summary>
    [Serializable]
    public class CharacterConfig
    {
        /// <summary>配置 ID（唯一），实例化时通过此键索引。</summary>
        public string ConfigId = string.Empty;

        /// <summary>显示名称（调试 / Inspector 用）。</summary>
        public string DisplayName = string.Empty;

        /// <summary>所有部件配置（按列表顺序创建 GameObject，<see cref="CharacterPartConfig.SortingOrder"/> 决定渲染顺序）。</summary>
        public List<CharacterPartConfig> Parts = new List<CharacterPartConfig>();

        /// <summary>根节点初始缩放（统一缩放整个 Character）。</summary>
        public Vector3 RootScale = Vector3.one;

        /// <summary>
        /// 渲染模式：<see cref="CharacterRenderMode.Sprite2D"/>（默认）或 <see cref="CharacterRenderMode.Prefab3D"/>。
        /// 决定 Build 时为每个 Part 实例化哪种 PartView 组件。
        /// </summary>
        public CharacterRenderMode RenderMode = CharacterRenderMode.Sprite2D;

        public CharacterConfig() { }

        public CharacterConfig(string id, string displayName)
        {
            ConfigId = id ?? string.Empty;
            DisplayName = displayName ?? id ?? string.Empty;
        }

        public CharacterConfig WithPart(CharacterPartConfig part)
        {
            if (part != null) Parts.Add(part);
            return this;
        }

        public CharacterConfig WithRootScale(Vector3 scale) { RootScale = scale; return this; }

        /// <summary>设置渲染模式（链式调用）。</summary>
        public CharacterConfig WithRenderMode(CharacterRenderMode mode) { RenderMode = mode; return this; }

        /// <summary>按 PartId 查找部件配置，找不到返回 null。</summary>
        public CharacterPartConfig GetPart(string partId)
        {
            if (Parts == null || string.IsNullOrEmpty(partId)) return null;
            for (var i = 0; i < Parts.Count; i++)
                if (Parts[i] != null && Parts[i].PartId == partId)
                    return Parts[i];
            return null;
        }
    }
}
