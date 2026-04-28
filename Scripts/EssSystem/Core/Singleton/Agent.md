# Singleton 模块指南

## 概述

`EssSystem.Core.Singleton` 提供两种单例基类：

| 类型 | 用途 | 父类 |
|---|---|---|
| `SingletonNormal<T>` | 普通 C# 单例（线程安全） | — |
| `SingletonMono<T>` | Unity MonoBehaviour 单例 | `MonoBehaviour` |

`Service<T>` 继承 `SingletonNormal<T>`；`Manager<T>` 继承 `SingletonMono<T>`。

## SingletonNormal<T>

```csharp
public class MyService : SingletonNormal<MyService>
{
    protected virtual void Initialize() { /* 首次访问 Instance 时自动调用 */ }
}

MyService.Instance.DoSomething();
```

**API**
- `T Instance` — 懒加载实例（线程安全）
- `bool HasInstance` — 是否已创建
- `T TryGetInstance()` — 不创建，仅尝试获取
- `void DestroyInstance()` — 销毁实例（若实现 `IDisposable` 会调 `Dispose`）
- `protected virtual void Initialize()` — 子类初始化钩子
- `protected void Log/LogWarning/LogError(...)`

## SingletonMono<T>

```csharp
public class MyManager : SingletonMono<MyManager>
{
    protected override void Awake() { base.Awake(); /* 单例就绪 */ }
    protected override void Initialize() { /* Awake 后调用 */ }
    protected override void OnDestroy() { base.OnDestroy(); /* 必须调 base */ }
}
```

**API**
- `T Instance` — 自动创建/查找场景中的 GameObject
- `bool HasInstance`
- `T TryGetInstance()`
- 自动 `DontDestroyOnLoad`
- 应用退出后访问返回 null

## 注意事项

- `SingletonNormal<T>` 在首次访问 `Instance` 时才初始化（懒加载）
- `SingletonMono<T>` 在 `Awake` 时初始化
- 子类重写 `OnDestroy` 必须调 `base.OnDestroy()`，否则静态实例不会被清理
- 不要在多线程访问 `SingletonMono`（Unity 主线程独占）
