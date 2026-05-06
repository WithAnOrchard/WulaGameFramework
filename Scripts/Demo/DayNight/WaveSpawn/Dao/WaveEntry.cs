using System;

namespace Demo.DayNight.WaveSpawn.Dao
{
    /// <summary>波次中单种敌人的刷怪条目。</summary>
    [Serializable]
    public class WaveEntry
    {
        /// <summary>EntityManager 中已注册的 EntityConfig ID（决定外观/属性）。</summary>
        public string EntityConfigId;

        /// <summary>本波刷出的数量。</summary>
        public int Count = 5;

        /// <summary>每只敌人之间的间隔（秒）；0 表示同帧批量刷。</summary>
        public float SpawnInterval = 0.5f;

        /// <summary>开始刷新该条目前等待的秒数（相对于本波开始时间）。</summary>
        public float StartDelay = 0f;
    }
}
