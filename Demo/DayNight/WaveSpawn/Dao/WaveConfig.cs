using System;
using System.Collections.Generic;

namespace Demo.DayNight.WaveSpawn.Dao
{
    /// <summary>单个夜晚波次的刷怪配置。</summary>
    [Serializable]
    public class WaveConfig
    {
        /// <summary>配置 ID（持久化主键）。</summary>
        public string ConfigId;

        /// <summary>该配置生效的回合范围 [<see cref="MinRound"/>, <see cref="MaxRound"/>]；MaxRound &lt;= 0 表示无上限。</summary>
        public int MinRound = 1;
        public int MaxRound = 0;

        /// <summary>是否仅用于 BOSS 夜。</summary>
        public bool IsBossWave = false;

        /// <summary>本波包含的敌人条目。</summary>
        public List<WaveEntry> Entries = new();
    }
}
