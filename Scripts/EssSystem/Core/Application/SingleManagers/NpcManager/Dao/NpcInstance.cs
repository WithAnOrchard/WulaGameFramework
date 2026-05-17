using System;
using System.Collections.Generic;
using UnityEngine;

namespace EssSystem.Core.Application.SingleManagers.NpcManager.Dao
{
    /// <summary>
    /// NPC 运行时实例 —— 一个 <see cref="NpcConfig"/> 在场景中的具体出现。
    /// </summary>
    [Serializable]
    public class NpcInstance
    {
        /// <summary>运行时唯一 Id（生成时分配，多人多副本互不冲突）。</summary>
        public string InstanceId;

        /// <summary>引用的配置 Id。</summary>
        public string ConfigId;

        /// <summary>世界坐标。</summary>
        public Vector3 WorldPosition;

        /// <summary>所属子场景 Id（接 SceneInstanceManager；空 = OverWorld）。</summary>
        public string SceneInstanceId;

        /// <summary>是否存活（false = 被消灭后等待 respawn / 清理）。</summary>
        public bool IsAlive = true;

        /// <summary>运行时业务标志（任务进度、好感度等）。不参与序列化（业务侧自定义）。</summary>
        [NonSerialized] public Dictionary<string, object> RuntimeFlags;
    }
}
