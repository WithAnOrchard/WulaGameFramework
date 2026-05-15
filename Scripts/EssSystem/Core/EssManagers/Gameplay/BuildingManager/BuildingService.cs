using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Base.Event;
using EssSystem.Core.EssManagers.Manager;
using EssSystem.Core.EssManagers.Gameplay.BuildingManager.Dao;
using EssSystem.Core.EssManagers.Gameplay.BuildingManager.Dao.Config;
using EssSystem.Core.EssManagers.Gameplay.BuildingManager.Runtime;
using EssSystem.Core.EssManagers.Gameplay.EntityManager;            // CharacterViewBridge 仅作为 typed event wrapper 引入
using EssSystem.Core.EssManagers.Gameplay.EntityManager.Dao;        // Entity / EntityKind 作为 DTO
using EssSystem.Core.EssManagers.Gameplay.EntityManager.Dao.Config; // EntityConfig / EntityColliderConfig / EntityColliderShape 作为 DTO
using EssSystem.Core.EssManagers.Gameplay.EntityManager.Runtime;    // EntityHandle 仅用于 Unbind 调用
// 跨模块业务调用一律走 EventProcessor bare-string（4.1）；Dao 类型仅作为 DTO 允许直接引用。

namespace EssSystem.Core.EssManagers.Gameplay.BuildingManager
{
    /// <summary>
    /// 建筑业务服务。
    /// <list type="bullet">
    /// <item>持有所有 <see cref="BuildingConfig"/>（仅内存 —— 含 <c>Action&lt;Entity&gt;</c> 不可序列化）</item>
    /// <item>持有所有运行时 <see cref="Building"/> 实例（仅内存）</item>
    /// <item>下层把建筑当 <see cref="EntityKind.Static"/> 的 Entity 走 <c>EntityService</c> 创建，
    /// 上层在 Entity 之上叠加：HP 死亡级联、建造材料账本、HUD、完成回调</item>
    /// </list>
    /// </summary>
    public class BuildingService : Service<BuildingService>
    {
        public const string CAT_CONFIGS   = "BuildingConfigs";
        public const string CAT_INSTANCES = "Buildings";
        /// <summary>持久化存档分类 —— 存 <see cref="BuildingSaveData"/> 快照。</summary>
        public const string CAT_SAVE      = "BuildingSave";

        /// <summary>广播：建造完成。args: [string instanceId, string configId]</summary>
        public const string EVT_COMPLETED = "OnBuildingCompleted";
        /// <summary>广播：建筑销毁（HP 归 0 或主动销毁）。args: [string instanceId]</summary>
        public const string EVT_DESTROYED = "OnBuildingDestroyed";
        /// <summary>广播：材料补给后剩余更新。args: [string instanceId, string itemId, int remaining]</summary>
        public const string EVT_SUPPLY_PROGRESS = "OnBuildingSupplyProgress";

        // BuildingConfig 含 Action（不可序列化）+ Building 含 Unity 引用，这两个分类不持久化；
        // CAT_SAVE 保存 BuildingSaveData 快照，可持久化。
        protected override bool IsTransientCategory(string category)
            => category == CAT_CONFIGS || category == CAT_INSTANCES;

        protected override void Initialize()
        {
            base.Initialize();
            Log("BuildingService 初始化完成", Color.green);
        }

        // ─── Config ───────────────────────────────────────────────

        public void RegisterConfig(BuildingConfig config)
        {
            if (config == null || string.IsNullOrEmpty(config.ConfigId))
            {
                LogWarning("忽略空 BuildingConfig 或缺 ConfigId");
                return;
            }
            SetData(CAT_CONFIGS, config.ConfigId, config);
            Log($"注册 BuildingConfig: {config.ConfigId} ({config.DisplayName})", Color.blue);
        }

        public BuildingConfig GetConfig(string configId) =>
            string.IsNullOrEmpty(configId) ? null : GetData<BuildingConfig>(CAT_CONFIGS, configId);

        // ─── Instance ─────────────────────────────────────────────

        public Building GetBuilding(string instanceId) =>
            string.IsNullOrEmpty(instanceId) ? null : GetData<Building>(CAT_INSTANCES, instanceId);

        /// <summary>
        /// 放置一座建筑。<paramref name="startCompleted"/>=true 跳过建造期直接进入完成态。
        /// 当 <see cref="BuildingConfig.Costs"/> 为空时也会直接完成态（无需材料）。
        /// </summary>
        public Building PlaceBuilding(string configId, string instanceId, Vector3 position, bool startCompleted = false)
        {
            if (string.IsNullOrEmpty(configId) || string.IsNullOrEmpty(instanceId))
            {
                LogWarning("PlaceBuilding 参数无效");
                return null;
            }
            if (HasData(CAT_INSTANCES, instanceId))
            {
                LogWarning($"Building {instanceId} 已存在");
                return GetBuilding(instanceId);
            }
            var config = GetConfig(configId);
            if (config == null) { LogWarning($"BuildingConfig 不存在: {configId}"); return null; }

            // 1) 底层 Entity（走 EntityManager bare-string）
            var entityCfgId = $"_building:{configId}";
            EnsureEntityConfig(entityCfgId, config, useCompletedVisual: startCompleted || IsAlreadyComplete(config));

            var createR = EventProcessor.Instance.TriggerEventMethod(
                "CreateEntity",
                new List<object> { entityCfgId, instanceId, null, position });
            if (!ResultCode.IsOk(createR)) { LogWarning($"创建底层 Entity 失败: {instanceId}"); return null; }

            var entity = QueryEntity(instanceId);
            if (entity == null) { LogWarning($"创建后查不到 Entity: {instanceId}"); return null; }

            // 2) HP / 死亡级联
            if (config.MaxHp > 0f)
            {
                entity.CanBeAttacked(config.MaxHp)
                      .OnDied((_, __) => DestroyBuilding(instanceId));
            }

            // 3) Building wrapper
            var building = new Building
            {
                InstanceId = instanceId,
                ConfigId   = configId,
                Config     = config,
                Entity     = entity,
                State      = BuildingState.Constructing,
                Remaining  = new Dictionary<string, int>(),
            };
            foreach (var c in config.Costs) building.Remaining[c.ItemId] = c.Amount;

            SetData(CAT_INSTANCES, instanceId, building);

            // 4) 路由：直接完成 / 进入建造期
            if (startCompleted || IsAlreadyComplete(config))
            {
                CompleteBuildingInternal(building, silent: !startCompleted);
            }
            else
            {
                AttachCostHud(building);
            }

            Log($"放置建筑: {instanceId} ({configId}) at {position} state={building.State}", Color.green);
            return building;
        }

        /// <summary>对正在建造中的建筑送材料。返回该 itemId 的剩余需要数（已完成则返回 0；不在建造中返回 -1）。</summary>
        public int SupplyMaterial(string instanceId, string itemId, int amount)
        {
            var b = GetBuilding(instanceId);
            if (b == null || b.State != BuildingState.Constructing) return -1;
            if (string.IsNullOrEmpty(itemId) || amount <= 0) return -1;
            if (!b.Remaining.TryGetValue(itemId, out var need) || need <= 0) return 0;

            var consumed = Mathf.Min(need, amount);
            b.Remaining[itemId] = need - consumed;

            // HUD 更新
            if (b.CostHudHost != null)
            {
                var hud = b.CostHudHost.GetComponent<BuildingCostHud>();
                if (hud != null) hud.RefreshTexts();
            }

            // 广播
            if (EventProcessor.HasInstance)
                EventProcessor.Instance.TriggerEvent(EVT_SUPPLY_PROGRESS,
                    new List<object> { instanceId, itemId, b.Remaining[itemId] });

            // 检查是否全部清零
            var allDone = true;
            foreach (var kv in b.Remaining) if (kv.Value > 0) { allDone = false; break; }
            if (allDone) CompleteBuildingInternal(b, silent: false);

            return b.Remaining[itemId];
        }

        /// <summary>销毁建筑（含 HUD + 底层 Entity）。</summary>
        public bool DestroyBuilding(string instanceId)
        {
            var b = GetBuilding(instanceId);
            if (b == null) return false;

            // HUD
            if (b.CostHudHost != null)
            {
                var hud = b.CostHudHost.GetComponent<BuildingCostHud>();
                hud?.Dispose();
                b.CostHudHost = null;
            }

            // 底层 Entity（走 EntityManager bare-string）
            if (EventProcessor.HasInstance)
                EventProcessor.Instance.TriggerEventMethod("DestroyEntity", new List<object> { instanceId });

            RemoveData(CAT_INSTANCES, instanceId);
            if (EventProcessor.HasInstance)
                EventProcessor.Instance.TriggerEvent(EVT_DESTROYED, new List<object> { instanceId });
            Log($"销毁建筑: {instanceId}", Color.yellow);
            return true;
        }

        public IEnumerable<Building> GetAllBuildings()
        {
            if (!_dataStorage.TryGetValue(CAT_INSTANCES, out var dict)) yield break;
            foreach (var kv in dict)
                if (kv.Value is Building b) yield return b;
        }

        // ─── 持久化 ──────────────────────────────────────────────

        /// <summary>
        /// 保存全部建筑到 <see cref="CAT_SAVE"/>（<see cref="BuildingSaveData"/> 快照）。
        /// Configs / Instances 保持 transient，仅 CAT_SAVE 写入磁盘。
        /// </summary>
        public override void SaveAllCategories()
        {
            // 先清空旧的 save 数据
            if (_dataStorage.ContainsKey(CAT_SAVE))
                _dataStorage[CAT_SAVE].Clear();

            foreach (var b in GetAllBuildings())
            {
                var save = new BuildingSaveData
                {
                    InstanceId = b.InstanceId,
                    ConfigId   = b.ConfigId,
                    State      = b.State,
                    Position   = b.Entity?.WorldPosition ?? Vector3.zero,
                };
                if (b.State == BuildingState.Constructing && b.Remaining != null)
                {
                    foreach (var kv in b.Remaining)
                        save.RemainingCosts.Add(new BuildingSaveData.CostEntry { ItemId = kv.Key, Amount = kv.Value });
                }
                SetData(CAT_SAVE, b.InstanceId, save);
            }

            base.SaveAllCategories();
            Log($"保存 {_dataStorage.GetValueOrDefault(CAT_SAVE)?.Count ?? 0} 座建筑", Color.green);
        }

        /// <summary>
        /// 从 <see cref="CAT_SAVE"/> 存档重建所有建筑。
        /// <para>前置条件：所有 <see cref="BuildingConfig"/> 已通过 <see cref="RegisterConfig"/> 注册。</para>
        /// <para>典型调用时机：GameManager 启动后、Config 注册完毕后调一次。</para>
        /// </summary>
        public void RestoreBuildings()
        {
            if (!_dataStorage.TryGetValue(CAT_SAVE, out var saveDict) || saveDict.Count == 0)
            {
                Log("无建筑存档可恢复", Color.gray);
                return;
            }

            var count = 0;
            foreach (var kv in new Dictionary<string, object>(saveDict))
            {
                if (kv.Value is not BuildingSaveData save) continue;
                var config = GetConfig(save.ConfigId);
                if (config == null)
                {
                    LogWarning($"恢复建筑跳过: ConfigId={save.ConfigId} 未注册");
                    continue;
                }

                var isCompleted = save.State == BuildingState.Completed;
                var building = PlaceBuilding(save.ConfigId, save.InstanceId, save.Position, startCompleted: isCompleted);
                if (building == null) continue;

                // 恢复 Constructing 态的剩余材料
                if (!isCompleted && save.RemainingCosts != null)
                {
                    building.Remaining.Clear();
                    foreach (var entry in save.RemainingCosts)
                        building.Remaining[entry.ItemId] = entry.Amount;

                    // 刷新 HUD
                    if (building.CostHudHost != null)
                    {
                        var hud = building.CostHudHost.GetComponent<BuildingCostHud>();
                        if (hud != null) hud.RefreshTexts();
                    }
                }
                count++;
            }

            Log($"恢复 {count} 座建筑", Color.green);
        }

        // ─── 内部 ────────────────────────────────────────────────

        private static bool IsAlreadyComplete(BuildingConfig cfg) => cfg.Costs == null || cfg.Costs.Count == 0;

        /// <summary>
        /// 在 EntityService 中确保有一份 EntityConfig 与本 BuildingConfig 对齐。
        /// 重复调用是 idempotent —— 后调用会覆盖前一份（外观可能从 pending 切到 completed）。
        /// </summary>
        private static void EnsureEntityConfig(string entityCfgId, BuildingConfig bc, bool useCompletedVisual)
        {
            var visualId = useCompletedVisual
                ? bc.CharacterConfigId
                : (string.IsNullOrEmpty(bc.PendingCharacterConfigId) ? bc.CharacterConfigId : bc.PendingCharacterConfigId);
            var ec = new EntityConfig
            {
                ConfigId = entityCfgId,
                DisplayName = bc.DisplayName,
                CharacterConfigId = visualId,
                Kind = EntityKind.Static,
                Collider = bc.Collider,
                SpawnOffset = bc.SpawnOffset,
            };
            if (EventProcessor.HasInstance)
                EventProcessor.Instance.TriggerEventMethod("RegisterEntityConfig", new List<object> { ec });
        }

        /// <summary>查询 EntityManager 指定 instanceId 的 Entity。</summary>
        private static Entity QueryEntity(string instanceId)
        {
            if (!EventProcessor.HasInstance) return null;
            var r = EventProcessor.Instance.TriggerEventMethod("GetEntity", new List<object> { instanceId });
            return ResultCode.IsOk(r) && r.Count >= 2 ? r[1] as Entity : null;
        }

        /// <summary>建造完成 —— 换皮（若有 pending→completed 视觉差异）、跑能力链、广播。</summary>
        private void CompleteBuildingInternal(Building b, bool silent)
        {
            b.State = BuildingState.Completed;

            // HUD
            if (b.CostHudHost != null)
            {
                var hud = b.CostHudHost.GetComponent<BuildingCostHud>();
                hud?.Dispose();
                b.CostHudHost = null;
            }

            // 视觉换皮：PendingCharacterConfigId → CharacterConfigId（完成态外观）
            ReplaceCharacterIfNeeded(b);

            // 注入能力链
            try { b.Config.ApplyCapabilities?.Invoke(b.Entity); }
            catch (System.Exception e) { LogWarning($"ApplyCapabilities for {b.InstanceId} 抛异常: {e.Message}"); }

            if (!silent && EventProcessor.HasInstance)
                EventProcessor.Instance.TriggerEvent(EVT_COMPLETED, new List<object> { b.InstanceId, b.ConfigId });

            Log($"建筑完成: {b.InstanceId}", Color.cyan);
        }

        /// <summary>
        /// 若建造期使用了不同的 <see cref="BuildingConfig.PendingCharacterConfigId"/>，
        /// 完成时销毁旧 Character 并以 <see cref="BuildingConfig.CharacterConfigId"/> 创建新 Character。
        /// Entity 的 CharacterRoot / CharacterInstanceId / Collider / EntityHandle 会被更新。
        /// </summary>
        private void ReplaceCharacterIfNeeded(Building b)
        {
            var cfg = b.Config;
            if (string.IsNullOrEmpty(cfg.PendingCharacterConfigId)) return;
            if (cfg.PendingCharacterConfigId == cfg.CharacterConfigId) return;
            if (b.Entity == null) return;

            var entity = b.Entity;
            var instanceId = b.InstanceId;

            // 1) 销毁旧 Character（保留 Entity 自身）
            if (entity.CharacterRoot != null)
            {
                var handle = entity.CharacterRoot.GetComponent<EntityHandle>();
                if (handle != null) handle.Unbind();
            }
            CharacterViewBridge.DestroyCharacter(entity.CharacterInstanceId);
            entity.CharacterInstanceId = null;
            entity.CharacterRoot = null;

            // 2) 创建新 Character（完成态外观）
            var root = CharacterViewBridge.CreateCharacter(
                cfg.CharacterConfigId, instanceId, null, entity.WorldPosition);

            if (root != null)
            {
                entity.CharacterInstanceId = instanceId;
                entity.CharacterRoot = root;

                // 3) 重挂碰撞体（走 EntityManager bare-string）
                if (cfg.Collider != null && cfg.Collider.Shape != EntityColliderShape.None &&
                    EventProcessor.HasInstance)
                    EventProcessor.Instance.TriggerEventMethod(
                        "ApplyCollider", new List<object> { root.gameObject, cfg.Collider });

                // 4) 重挂 EntityHandle（走 EntityManager bare-string）
                if (EventProcessor.HasInstance)
                    EventProcessor.Instance.TriggerEventMethod(
                        "AttachEntityHandle", new List<object> { root.gameObject, entity });

                // 5) SpawnOffset
                if (cfg.SpawnOffset != Vector3.zero)
                    CharacterViewBridge.Move(instanceId, cfg.SpawnOffset);

                Log($"建筑 {instanceId} 换皮: {cfg.PendingCharacterConfigId} → {cfg.CharacterConfigId}", Color.cyan);
            }
            else
            {
                LogWarning($"建筑 {instanceId} 换皮失败: CharacterConfigId={cfg.CharacterConfigId}");
            }
        }

        private static void AttachCostHud(Building b)
        {
            if (b.Entity == null || b.Entity.CharacterRoot == null) return;
            if (b.Config.Costs == null || b.Config.Costs.Count == 0) return;
            var go = b.Entity.CharacterRoot.gameObject;
            var hud = go.GetComponent<BuildingCostHud>();
            if (hud == null) hud = go.AddComponent<BuildingCostHud>();
            hud.Bind(b);
            b.CostHudHost = go;
        }
    }
}
