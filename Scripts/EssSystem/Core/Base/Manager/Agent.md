# Manager & Service Base Classes
**Base Classes** (`Manager<T>`, `Service<T>`)
## 职责
- **Manager<T>**：框架基类，所有 Manager 继承自此。提供生命周期管理、事件分派、日志同步等基础功能。
- **Service<T>**：框架基类，所有 Service 继承自此。提供数据存储、初始化事件、日志管理等基础功能。
## Event API

> Full Event definitions (params / return / side effects / usage) live in root Events.md -> section: **Manager Event**.

- `EVT_INITIALIZED`

## 文件结构
```
Core/Base/Manager/
├── Manager.cs           Manager<T> 基类
├── Service.cs           Service<T> 基类
├── Agent.md             本文档
```
## 生命周期
```
1. Manager.Awake()
   ├─ 创建 Service 实例
   └─ 调用 Service.Initialize()
       ├─ 广播 EVT_INITIALIZED
       └─ 业务初始化
2. Manager.Update()
   └─ 调用 Service.Tick()
3. Manager.OnDestroy()
   └─ 调用 Service.Dispose()
```
