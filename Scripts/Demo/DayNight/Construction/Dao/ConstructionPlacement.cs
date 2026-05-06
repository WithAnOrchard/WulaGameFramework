using System;
using UnityEngine;

namespace Demo.DayNight.Construction.Dao
{
    /// <summary>玩家放置的工事记录（持久化）。</summary>
    [Serializable]
    public class ConstructionPlacement
    {
        /// <summary>实例 ID（持久化主键）。</summary>
        public string InstanceId;

        /// <summary>工事类型（如 "wall" / "turret" / "wire"）。业务自定义。</summary>
        public string TypeId;

        /// <summary>世界坐标。</summary>
        public Vector3 Position;

        /// <summary>朝向（绕 Z 轴度数；2D 一般用这个就够）。</summary>
        public float Rotation;

        /// <summary>当前 HP（工事自身可被破坏，0 自动 Remove）。</summary>
        public int Hp = 100;
    }
}
