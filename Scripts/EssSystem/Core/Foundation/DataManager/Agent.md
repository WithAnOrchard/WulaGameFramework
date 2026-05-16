# DataManager 指南

## 概述

`Foundation/DataManager`（`[Manager(-20)]`，最早启动的业务 Manager）+ `DataService` 负责 **Service 自动注册** + **JSON 数据持久化**。

| | 类 | 角色 |
|---|---|---|
| Manager | `DataManager` | MonoBehaviour 单例，Inspector 展示已注册 Service 名单 |
| Service | `DataService` | 纯 C# 单例，收集所有 Service、退出时统一调 `SaveAllCategories()` |

> `DataManager` 自身**不定义任何 `[Event]`**，因此无 `## Event API` 章节。它通过监听 `Service<T>.EVT_INITIALIZED = "OnServiceInitialized"`（根 Agent.md §全局 Event 索引登记）完成 Service 自动注册。

## 文件结构

```
Foundation/DataManager/
├── DataManager.cs   Manager（MonoBehaviour 单例 + Inspector）
├── DataService.cs   Service（持久化调度 + IServicePersistence 调用）
└── Agent.md         本文档
```

## 启动 / 数据流

```
1. Service<T>.Initialize()
       │
       ▼
2. 触发 EVT_INITIALIZED = "OnServiceInitialized" 广播
       │
       ▼
3. DataService 监听器 → 把 service 加入 _serviceInstances
       │
       ▼
4. Application.quitting
       │
       ▼
5. 遍历 _serviceInstances，逐个调 IServicePersistence.SaveAllCategories()
```

> `DataService` 自身在 `Initialize` 里把自己 `_serviceInstances.Add(this)`，避免监听自己产生循环。

## IServicePersistence 接口

```csharp
public interface IServicePersistence
{
    void SaveAllCategories();
}
```

`Service<T>` 已实现该接口，`DataService` 通过接口直接调用，**零反射**。

## API（同模块直调）

```csharp
// 取所有已注册的 Service（IReadOnlyList）
var services = DataService.Instance.GetServiceInstances();

// 手动持久化（一般不需要，应用退出时自动）
foreach (var s in services) s.SaveAllCategories();
```

## 持久化结构

```
{persistentDataPath}/ServiceData/
├── DataService/Settings.json
├── UIService/UIComponents.json
├── InventoryService/Items.json, Configs.json, …
└── ResourceService/Prefab.json, Sprite.json, …
```

每个 `{TypeName}/{Category}.json` 是独立文件，便于增量保存与调试。

## Inspector 字段

| 字段 | 说明 |
|---|---|
| `_serviceCount` | 已注册 Service 数量 |
| `_serviceNames` | 各 Service 类名 |
| `_dataFolderPath` | 数据根目录（`{persistentDataPath}/ServiceData`） |

## 注意事项

- Service 必须 `public`，否则反射创建失败
- 自定义 DAO / 持久化数据类必须 `[Serializable]`（Anti-Patterns §A4）
- 禁止存储 `GameObject` / `MonoBehaviour` / `Transform`（不可序列化；Anti-Patterns §A3）
- `DataService` 不监听自己的 `OnServiceInitialized`，避免无限递归
