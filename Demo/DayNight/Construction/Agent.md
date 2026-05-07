# Construction（昼夜求生 Demo · 建造）

玩家在场景中放置/移除工事（墙、炮塔等）的逻辑。命令入口在 `ConstructionManager`，状态在 `ConstructionService`。

## 关键文件

- `ConstructionManager.cs` — façade；命令事件 + 玩法限制（如仅白天可建）
- `ConstructionService.cs` — Service 单例；维护 placement 列表 + 广播
- `Dao/ConstructionPlacement.cs` — 单条放置数据（typeId / instanceId / position / rotation）

## Event API

| 常量 | 字符串 | 类型 | 参数 / 返回 | 说明 |
|---|---|---|---|---|
| `ConstructionManager.EVT_PLACE` | `PlaceConstruction` | 命令 | `[string typeId, Vector3 position, float rotation?]` → `Ok(string instanceId)` | 在指定位置放置工事 |
| `ConstructionManager.EVT_REMOVE` | `RemoveConstruction` | 命令 | `[string instanceId]` → `ResultCode` | 移除工事 |
| `ConstructionService.EVT_PLACED` | `OnConstructionPlaced` | 广播 | `[string instanceId, string typeId, Vector3 position]` | 放置成功后广播 |
| `ConstructionService.EVT_REMOVED` | `OnConstructionRemoved` | 广播 | `[string instanceId]` | 移除成功后广播 |
