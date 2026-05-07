using System;
using UnityEngine;

namespace EssSystem.Core.EssManagers.Gameplay.MapManager.Voxel3D.Dao
{
    /// <summary>
    /// 3D 体素方块类型定义。<b>纯色版</b>：顶面 / 侧面各一个 <see cref="Color32"/>，
    /// 后续接入贴图时改为 atlas UV 偏移即可。
    /// </summary>
    [Serializable]
    public class VoxelBlockType
    {
        /// <summary>方块 ID（1-based；0 保留给 Air）。</summary>
        public byte Id;
        public string Name;
        public Color32 TopColor;
        public Color32 SideColor;
        /// <summary>是否实心（参与碰撞 / 实心面剔除）。水之类透明体设 false。</summary>
        public bool Solid;

        public VoxelBlockType() { }
        public VoxelBlockType(byte id, string name, Color32 top, Color32 side, bool solid = true)
        {
            Id = id; Name = name; TopColor = top; SideColor = side; Solid = solid;
        }
    }
}
