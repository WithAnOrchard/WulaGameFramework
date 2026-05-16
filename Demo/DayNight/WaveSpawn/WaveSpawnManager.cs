using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Base.Manager;
using EssSystem.Core.Base.Event;

namespace Demo.DayNight.WaveSpawn
{
    /// <summary>波次刷怪 Manager —— 监听 <see cref="DayNightGameManager.EVT_PHASE_CHANGED"/>，
    /// 在夜晚开始时通过 <see cref="WaveSpawnService"/> 启动一波；白天暂停 tick。</summary>
    [Manager(20)]
    public class WaveSpawnManager : Manager<WaveSpawnManager>
    {
        [Header("Spawn 区域")]
        [Tooltip("敌人挂载的父 Transform（默认本组件所在节点）")]
        [SerializeField] private Transform _spawnRoot;

        [Tooltip("敌人刷新中心（世界坐标）；null 时取本节点 position")]
        [SerializeField] private Transform _spawnCenter;

        [Tooltip("围绕中心的刷新半径（米）")]
        [SerializeField, Min(0f)] private float _spawnRadius = 8f;

        public WaveSpawnService Service => WaveSpawnService.Instance;

        protected override void Initialize()
        {
            base.Initialize();
            if (_spawnRoot == null) _spawnRoot = transform;
            if (Service != null) _serviceEnableLogging = Service.EnableLogging;
            Log("WaveSpawnManager 初始化完成", Color.green);
        }

        protected override void UpdateServiceInspectorInfo()
        {
            if (Service == null) return;
            Service.UpdateInspectorInfo();
            _serviceInspectorInfo = Service.InspectorInfo;
        }

        protected override void SyncServiceLoggingSettings()
        {
            if (Service != null) Service.EnableLogging = _serviceEnableLogging;
        }

        protected override void Update()
        {
            base.Update();
            if (Service == null || !Service.HasActiveWave) return;
            var center = _spawnCenter != null ? _spawnCenter.position : transform.position;
            Service.Tick(Time.deltaTime, _spawnRoot, center, _spawnRadius);
        }

        // ─── 订阅昼夜切换 ───────────────────────────────────────
        [EventListener(DayNightGameManager.EVT_PHASE_CHANGED)]
        public List<object> OnPhaseChanged(string eventName, List<object> data)
        {
            if (data == null || data.Count < 3) return ResultCode.Ok();
            var isNight = data[0] is bool b ? b : false;
            var round = data[1] is int r ? r : 1;
            var isBossNight = data[2] is bool bb && bb;

            if (isNight)
            {
                if (Service != null) Service.StartWaveForRound(round, isBossNight);
            }
            else
            {
                if (Service != null) Service.CancelActiveWave();
            }
            return ResultCode.Ok();
        }
    }
}
