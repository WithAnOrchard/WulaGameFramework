# DataManager 指南

## 概述

`DataManager`（`[Manager(-20)]`）+ `DataService` 负责 Service 自动注册与数据持久化。

## 自动注册流程

```
Service.Initialize() → 触发 Service<T>.EVT_INITIALIZED ("OnServiceInitialized") 事件
                              ↓
DataService 监听 → service 加入 _serviceInstances 列表
                              ↓
Application.quitting → 遍历列表调 service.SaveAllCategories()
```

DataService 自身在 `Initialize` 时把自己 `_serviceInstances.Add(this)`，避免循环。

## API

```csharp
// 获取所有已注册的 Service（IReadOnlyList）
DataService.Instance.GetServiceInstances();

// 手动持久化（一般不需要，应用退出时自动）
foreach (var s in DataService.Instance.GetServiceInstances()) s.SaveAllCategories();
```

## 持久化结构

```
{persistentDataPath}/ServiceData/
├── DataService/Settings.json
├── UIService/UIComponents.json
├── InventoryService/Items.json, Configs.json, ...
└── ResourceService/Prefab.json, Sprite.json, ...
```

每个 `{TypeName}/{Category}.json` 是独立文件，便于增量保存与调试。

## IServicePersistence 接口

```csharp
public interface IServicePersistence
{
    void SaveAllCategories();
}
```

`Service<T>` 已实现该接口，DataService 通过接口直接调用，零反射。

## DataManager Inspector 字段

- `_serviceCount` — 已注册 Service 数量
- `_serviceNames` — 各 Service 类名
- `_dataFolderPath` — 数据根目录

## 注意事项

- Service 必须 public，否则反射创建失败
- 自定义数据类需 `[Serializable]` 才能被 `JsonUtility` 序列化
- 不要存储 GameObject / MonoBehaviour（不可序列化）
- DataService 不触发自己的 `OnServiceInitialized`，避免无限递归
