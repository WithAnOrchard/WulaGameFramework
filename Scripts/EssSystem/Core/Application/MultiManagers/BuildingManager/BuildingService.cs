using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Manager;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Application.MultiManagers.BuildingManager.Dao;
using EssSystem.Core.Application.MultiManagers.BuildingManager.Dao.Config;
using EssSystem.Core.Application.MultiManagers.BuildingManager.Runtime;

namespace EssSystem.Core.Application.MultiManagers.BuildingManager
{
    public class BuildingService : Service<BuildingService>
    {
        public const string CAT_CONFIGS = "BuildingConfigs";
        public const string CAT_INSTANCES = "Buildings";
        public const string CAT_SAVE = "BuildingSave";

        public const string EVT_COMPLETED = "OnBuildingCompleted";
        public const string EVT_DESTROYED = "OnBuildingDestroyed";
        public const string EVT_SUPPLY_PROGRESS = "OnBuildingSupplyProgress";

        protected override bool IsTransientCategory(string category)
            => category == CAT_CONFIGS || category == CAT_INSTANCES;

        protected override void Initialize()
        {
            base.Initialize();
            Log("BuildingService initialized", Color.green);
        }

        public void RegisterConfig(BuildingConfig config)
        {
            if (config == null || string.IsNullOrEmpty(config.ConfigId))
            {
                LogWarning("Ignore empty BuildingConfig");
                return;
            }
            SetData(CAT_CONFIGS, config.ConfigId, config);
            Log($"Register BuildingConfig: {config.ConfigId} ({config.DisplayName})", Color.blue);
        }

        public BuildingConfig GetConfig(string configId)
            => string.IsNullOrEmpty(configId) ? null : GetData<BuildingConfig>(CAT_CONFIGS, configId);

        public Building GetBuilding(string instanceId)
            => string.IsNullOrEmpty(instanceId) ? null : GetData<Building>(CAT_INSTANCES, instanceId);

        public Building PlaceBuilding(string configId, string instanceId, Vector3 position, bool startCompleted = false)
        {
            if (string.IsNullOrEmpty(configId) || string.IsNullOrEmpty(instanceId))
            {
                LogWarning("PlaceBuilding invalid args");
                return null;
            }
            if (HasData(CAT_INSTANCES, instanceId))
            {
                LogWarning($"Building already exists: {instanceId}");
                return GetBuilding(instanceId);
            }

            var config = GetConfig(configId);
            if (config == null)
            {
                LogWarning($"BuildingConfig not found: {configId}");
                return null;
            }

            var completed = startCompleted || IsAlreadyComplete(config);
            var entityConfigId = $"_building:{configId}";
            EnsureEntityConfig(entityConfigId, config, completed);

            var createResult = EventProcessor.Instance.TriggerEventMethod(
                "CreateEntity", new List<object> { entityConfigId, instanceId, null, position });
            if (!ResultCode.IsOk(createResult))
            {
                LogWarning($"Create building entity failed: {instanceId}");
                return null;
            }

            var root = createResult.Count >= 2 ? createResult[1] as Transform : null;
            ConfigureDurability(instanceId, config.MaxHp);

            var building = new Building
            {
                InstanceId = instanceId,
                ConfigId = configId,
                Config = config,
                EntityInstanceId = instanceId,
                CharacterRoot = root,
                State = BuildingState.Constructing,
                Remaining = new Dictionary<string, int>(),
            };
            foreach (var c in config.Costs)
                building.Remaining[c.ItemId] = c.Amount;

            SetData(CAT_INSTANCES, instanceId, building);

            if (completed) CompleteBuildingInternal(building, silent: !startCompleted);
            else AttachCostHud(building);

            Log($"Place building: {instanceId} ({configId}) at {position} state={building.State}", Color.green);
            return building;
        }

        public int SupplyMaterial(string instanceId, string itemId, int amount)
        {
            var b = GetBuilding(instanceId);
            if (b == null || b.State != BuildingState.Constructing) return -1;
            if (string.IsNullOrEmpty(itemId) || amount <= 0) return -1;
            if (!b.Remaining.TryGetValue(itemId, out var need) || need <= 0) return 0;

            var consumed = Mathf.Min(need, amount);
            b.Remaining[itemId] = need - consumed;

            if (b.CostHudHost != null)
                b.CostHudHost.GetComponent<BuildingCostHud>()?.RefreshTexts();

            if (EventProcessor.HasInstance)
                EventProcessor.Instance.TriggerEvent(EVT_SUPPLY_PROGRESS,
                    new List<object> { instanceId, itemId, b.Remaining[itemId] });

            var allDone = true;
            foreach (var kv in b.Remaining)
            {
                if (kv.Value > 0) { allDone = false; break; }
            }
            if (allDone) CompleteBuildingInternal(b, silent: false);

            return b.Remaining[itemId];
        }

        public bool DestroyBuilding(string instanceId)
        {
            var b = GetBuilding(instanceId);
            if (b == null) return false;

            if (b.CostHudHost != null)
            {
                b.CostHudHost.GetComponent<BuildingCostHud>()?.Dispose();
                b.CostHudHost = null;
            }

            if (EventProcessor.HasInstance)
                EventProcessor.Instance.TriggerEventMethod("DestroyEntity", new List<object> { instanceId });

            RemoveData(CAT_INSTANCES, instanceId);
            if (EventProcessor.HasInstance)
                EventProcessor.Instance.TriggerEvent(EVT_DESTROYED, new List<object> { instanceId });
            Log($"Destroy building: {instanceId}", Color.yellow);
            return true;
        }

        public IEnumerable<Building> GetAllBuildings()
        {
            if (!_dataStorage.TryGetValue(CAT_INSTANCES, out var dict)) yield break;
            foreach (var kv in dict)
                if (kv.Value is Building b) yield return b;
        }

        public override void SaveAllCategories()
        {
            if (_dataStorage.ContainsKey(CAT_SAVE))
                _dataStorage[CAT_SAVE].Clear();

            foreach (var b in GetAllBuildings())
            {
                var save = new BuildingSaveData
                {
                    InstanceId = b.InstanceId,
                    ConfigId = b.ConfigId,
                    State = b.State,
                    Position = QueryEntityPosition(b.EntityInstanceId),
                };
                if (b.State == BuildingState.Constructing && b.Remaining != null)
                {
                    foreach (var kv in b.Remaining)
                        save.RemainingCosts.Add(new BuildingSaveData.CostEntry { ItemId = kv.Key, Amount = kv.Value });
                }
                SetData(CAT_SAVE, b.InstanceId, save);
            }

            base.SaveAllCategories();
            Log($"Save buildings: {_dataStorage.GetValueOrDefault(CAT_SAVE)?.Count ?? 0}", Color.green);
        }

        public void RestoreBuildings()
        {
            if (!_dataStorage.TryGetValue(CAT_SAVE, out var saveDict) || saveDict.Count == 0)
            {
                Log("No building save data", Color.gray);
                return;
            }

            var count = 0;
            foreach (var kv in new Dictionary<string, object>(saveDict))
            {
                if (kv.Value is not BuildingSaveData save) continue;
                if (GetConfig(save.ConfigId) == null)
                {
                    LogWarning($"Skip building restore, config missing: {save.ConfigId}");
                    continue;
                }

                var isCompleted = save.State == BuildingState.Completed;
                var building = PlaceBuilding(save.ConfigId, save.InstanceId, save.Position, isCompleted);
                if (building == null) continue;

                if (!isCompleted && save.RemainingCosts != null)
                {
                    building.Remaining.Clear();
                    foreach (var entry in save.RemainingCosts)
                        building.Remaining[entry.ItemId] = entry.Amount;

                    if (building.CostHudHost != null)
                        building.CostHudHost.GetComponent<BuildingCostHud>()?.RefreshTexts();
                }
                count++;
            }

            Log($"Restore buildings: {count}", Color.green);
        }

        private static bool IsAlreadyComplete(BuildingConfig cfg) => cfg.Costs == null || cfg.Costs.Count == 0;

        private static void EnsureEntityConfig(string entityConfigId, BuildingConfig config, bool useCompletedVisual)
        {
            var visualId = useCompletedVisual
                ? config.CharacterConfigId
                : (string.IsNullOrEmpty(config.PendingCharacterConfigId)
                    ? config.CharacterConfigId
                    : config.PendingCharacterConfigId);

            if (!EventProcessor.HasInstance) return;
            EventProcessor.Instance.TriggerEventMethod(
                "RegisterSimpleEntityConfig",
                new List<object>
                {
                    entityConfigId,
                    config.DisplayName,
                    visualId,
                    config.Collider,
                    config.SpawnOffset,
                    "Static"
                });
        }

        private void ConfigureDurability(string entityId, float maxHp)
        {
            if (string.IsNullOrEmpty(entityId) || maxHp <= 0f || !EventProcessor.HasInstance) return;
            EventProcessor.Instance.TriggerEventMethod("SetEntityMaxHp", new List<object> { entityId, maxHp, true });
            EventProcessor.Instance.TriggerEventMethod(
                "RegisterDeathCallback",
                new List<object>
                {
                    entityId,
                    (System.Action<string, string>)((_, __) => DestroyBuilding(entityId))
                });
        }

        private static Vector3 QueryEntityPosition(string entityId)
        {
            if (string.IsNullOrEmpty(entityId) || !EventProcessor.HasInstance) return Vector3.zero;
            var result = EventProcessor.Instance.TriggerEventMethod("GetEntityPosition", new List<object> { entityId });
            return ResultCode.IsOk(result) && result.Count >= 2 && result[1] is Vector3 pos ? pos : Vector3.zero;
        }

        private void CompleteBuildingInternal(Building b, bool silent)
        {
            b.State = BuildingState.Completed;

            if (b.CostHudHost != null)
            {
                b.CostHudHost.GetComponent<BuildingCostHud>()?.Dispose();
                b.CostHudHost = null;
            }

            ReplaceCharacterIfNeeded(b);

            try { b.Config.ApplyCapabilities?.Invoke(b.EntityInstanceId); }
            catch (System.Exception e) { LogWarning($"ApplyCapabilities for {b.InstanceId} failed: {e.Message}"); }

            if (!silent && EventProcessor.HasInstance)
                EventProcessor.Instance.TriggerEvent(EVT_COMPLETED, new List<object> { b.InstanceId, b.ConfigId });

            Log($"Building completed: {b.InstanceId}", Color.cyan);
        }

        private void ReplaceCharacterIfNeeded(Building b)
        {
            var cfg = b.Config;
            if (string.IsNullOrEmpty(cfg.PendingCharacterConfigId)) return;
            if (cfg.PendingCharacterConfigId == cfg.CharacterConfigId) return;
            if (!EventProcessor.HasInstance) return;

            var position = QueryEntityPosition(b.EntityInstanceId);
            EventProcessor.Instance.TriggerEventMethod("DestroyEntity", new List<object> { b.EntityInstanceId });

            var entityConfigId = $"_building:{b.ConfigId}";
            EnsureEntityConfig(entityConfigId, cfg, useCompletedVisual: true);
            var createResult = EventProcessor.Instance.TriggerEventMethod(
                "CreateEntity", new List<object> { entityConfigId, b.EntityInstanceId, null, position });
            b.CharacterRoot = ResultCode.IsOk(createResult) && createResult.Count >= 2
                ? createResult[1] as Transform
                : null;
            ConfigureDurability(b.EntityInstanceId, cfg.MaxHp);

            Log($"Building visual replaced: {b.InstanceId}", Color.cyan);
        }

        private static void AttachCostHud(Building b)
        {
            if (b.CharacterRoot == null) return;
            if (b.Config.Costs == null || b.Config.Costs.Count == 0) return;
            var go = b.CharacterRoot.gameObject;
            var hud = go.GetComponent<BuildingCostHud>();
            if (hud == null) hud = go.AddComponent<BuildingCostHud>();
            hud.Bind(b);
            b.CostHudHost = go;
        }
    }
}
