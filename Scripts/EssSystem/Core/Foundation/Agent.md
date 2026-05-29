# Foundation 模块总体指南

> **Foundation 模块提供框架的基础设施服务**，包括数据持久化、资源加载、网络通讯等核心功能。
>
> 所有业务模块都依赖 Foundation 模块的服务。

## 📋 模块结构

```
Core/Foundation/
├── DataManager/              → 数据持久化和 Service 自动注册
│   ├── DataManager.cs        - Manager（[Manager(-20)]，最早启动）
│   ├── DataService.cs        - Service（Service 自动注册 + 统一持久化）
│   └── Agent.md              - 详细文档
│
├── ResourceManager/          → 资源加载、缓存、预加载
│   ├── ResourceManager.cs    - Manager（[Manager(0)]，Façade）
│   ├── ResourceService.cs    - Service（缓存 + 索引 + FBX manifest）
│   ├── ResourceRefCounter.cs - 引用计数管理
│   ├── Editor/               - 编辑器工具
│   └── Agent.md              - 详细文档
│
└── NetworkManager/           → 多人联机网络通讯（基于 Mirror）
    ├── NetworkManager.cs     - Manager（[Manager(2)]）
    ├── NetworkService.cs     - Service（网络状态 + 消息编解码）
    ├── Editor/               - Mirror 自动安装器
    ├── Runtime/              - Mirror 桥接代码
    ├── Agent.md              - 详细文档
    └── README.md             - 快速开始
```

---

## 🏗️ 核心功能

### 1. DataManager — 数据持久化和 Service 自动注册

**设计理念**：
- 集中管理所有 Service 的持久化
- 自动注册新 Service（通过事件监听）
- 应用退出时统一保存

**优先级**：`[Manager(-20)]`（最早启动）

**工作流程**：
```
1. Service<T>.Initialize()
   ↓
2. 触发 EVT_INITIALIZED = "OnServiceInitialized" 事件
   ↓
3. DataService 监听器 → 将 Service 加入 _serviceInstances
   ↓
4. Application.quitting
   ↓
5. 遍历 _serviceInstances，逐个调 SaveAllCategories()
```

**关键特性**：
- ✅ 自动注册（无需手动添加）
- ✅ 统一持久化（一次性保存所有 Service）
- ✅ 零反射（通过 IServicePersistence 接口）
- ✅ 增量保存（每个 Service 独立文件）

**优化**（Phase 1.1 - DataManager 优化）：
- ✅ Service 去重检查优化（O(n) → O(1)）
- ✅ 延迟初始化（启动时无磁盘 I/O，-5~10ms）
- ✅ 批量保存统计（性能监控）

**性能指标**：
- 启动时间：-5~10ms
- Service 注册时间：O(n) → O(1)
- 内存占用：无增加

**数据结构**：
```
{persistentDataPath}/ServiceData/
├── DataService/Settings.json
├── UIService/UIComponents.json
├── InventoryService/Items.json, Configs.json, …
└── ResourceService/Prefab.json, Sprite.json, …
```

**使用场景**：
- 游戏存档
- 用户设置
- 应用配置

**示例**：
```csharp
// Service 自动注册（无需手动操作）
public class PlayerService : Service<PlayerService>
{
    public const string CAT_DATA = "PlayerData";
    
    protected override void Initialize()
    {
        base.Initialize();  // 自动触发 EVT_INITIALIZED
        // 初始化逻辑
    }
}

// 数据自动保存（应用退出时）
PlayerService.Instance.SetData(CAT_DATA, "Name", "Player1");
```

---

### 2. ResourceManager — 资源加载、缓存、预加载

**设计理念**：
- 统一的资源加载入口
- 自动缓存和索引
- 支持 Editor 和 Build 双路径
- FBX 模型和动画特殊处理

**优先级**：`[Manager(0)]`

**支持的资源类型**：
```csharp
public enum ResourceType 
{ 
    Prefab,         // GameObject 预制体
    Sprite,         // 2D 精灵
    AudioClip,      // 音频
    Texture,        // 纹理
    RuleTile,       // 规则瓷砖
    AnimationClip   // 动画片段
}
```

**工作流程**：
```
1. ResourceManager.Start()
   ↓
2. ResourceService.OnDataLoaded
   ├─ PreloadConfiguredResources()  按配置异步预加载
   ├─ IndexAllResources()           全量索引 Resources/
   │  ├─ [Editor] AssetDatabase 索引
   │  ├─ [Editor] FBX 内 clip 入缓存
   │  ├─ LoadFBXManifestIfPresent
   │  └─ Resources.LoadAll 兜底
   └─ 广播 EVT_RESOURCES_LOADED
   ↓
3. 业务模块等待资源就绪
```

**关键 API**：
```csharp
// 同步加载
var prefab = EventProcessor.Instance.TriggerEventMethod(
    "GetPrefab", new List<object> { "Prefabs/Player" });

var sprite = EventProcessor.Instance.TriggerEventMethod(
    "GetSprite", new List<object> { "Sprites/UI/Button" });

var audioClip = EventProcessor.Instance.TriggerEventMethod(
    "GetAudioClip", new List<object> { "Audio/BGM/MainTheme" });

// 异步加载
EventProcessor.Instance.TriggerEventMethod(
    "LoadPrefabAsync", new List<object> { "Prefabs/Enemy", callback });

// 卸载资源
EventProcessor.Instance.TriggerEventMethod(
    "UnloadAsset", new List<object> { "Prefabs/Player" });
```

**特殊处理**：
- **FBX 模型**：`Resources/` 下 `.fbx` 根资产是 GameObject
- **FBX 动画**：内部 AnimationClip 子资产按 `clip.name` 索引到全局缓存
- **子图**：Sprite 文件中的子图按 `sprite.name` 查找

**优化**（Phase 1.1）：
- 资源引用计数管理
- 自动清理超时未使用资源（300 秒）
- 每 60 秒自动检查一次

**性能**：
- 缓存命中：O(1)
- 首次加载：自动 fallback 到候选子目录
- 预期效果：-2~5MB（引用计数优化）

**使用场景**：
- 游戏资源加载
- UI 资源管理
- 音频加载
- 预加载优化

---

### 3. NetworkManager — 多人联机网络通讯

**设计理念**：
- 基于 Mirror 框架
- 事件驱动（无需直接引用 Mirror 类型）
- 自动安装 Mirror（OpenUPM）
- 支持 Host / Server / Client 模式

**优先级**：`[Manager(2)]`

**架构**：
```
业务侧 TriggerEvent(EVT_HOST_START / EVT_CLIENT_CONNECT / EVT_SEND_*)
   ↓
NetworkManager [Event] handler
   ↓
WulaNetworkManagerBehaviour (Mirror.NetworkManager 子类)
   ↓
KCP / WebSocket Transport ↔ 远端
   ↓
Mirror 回调 → NetworkService.NotifyXxx
   ↓
EventProcessor.TriggerEvent(EVT_NET_STATUS_CHANGED / EVT_NET_MESSAGE ...)
   ↓
业务订阅方
```

**自动安装 Mirror**：
- 第一次挂载 NetworkManager 时自动触发
- 检测 Packages/manifest.json
- 通过 OpenUPM 安装 Mirror
- 设置 MIRROR_INSTALLED 编译宏

**菜单**：`Tools/WulaFramework/Network/`
- `Install Mirror Now` — 手动触发
- `Uninstall Mirror`
- `Toggle Auto-Install` — 关闭自动安装提示
- `Check Mirror Status` — 检查安装状态

**配置**（Inspector）：
- `_autoStart` — Initialize 后自动启动
- `_autoMode` — Host / ServerOnly / Client
- `_port` — 监听/连接端口（默认 7777）
- `_serverAddress` — 服务器地址（默认 localhost）
- `_mirrorHostObjectName` — 桥接子物体名

**命令事件**：
```csharp
// 启动主机（Server + 本地 Client）
EventProcessor.Instance.TriggerEventMethod(
    "NetHostStart", new List<object> { 7777 });

// 启动纯服务器
EventProcessor.Instance.TriggerEventMethod(
    "NetServerStart", new List<object> { 7777 });

// 连接到服务器
EventProcessor.Instance.TriggerEventMethod(
    "NetClientConnect", new List<object> { "192.168.1.1", 7777 });

// 断开连接
EventProcessor.Instance.TriggerEventMethod(
    "NetDisconnect", new List<object> { });

// 发送消息到服务器
EventProcessor.Instance.TriggerEventMethod(
    "NetSendToServer", new List<object> { "PlayerMove", playerData });

// 服务器广播到所有客户端
EventProcessor.Instance.TriggerEventMethod(
    "NetSendToAll", new List<object> { "GameStateUpdate", gameState });

// 对等广播（所有节点都收到）
EventProcessor.Instance.TriggerEventMethod(
    "NetBroadcast", new List<object> { "ChatMessage", message });
```

**广播事件**：
- `EVT_NET_STATUS_CHANGED` — 网络状态变化
- `EVT_NET_MESSAGE` — 接收网络消息
- `EVT_NET_ERROR` — 网络错误

**消息编解码**：
- 自动 JSON 编码
- 支持类型：string / long / double / bool / List / Dictionary

**使用场景**：
- 多人游戏
- 实时协作
- 网络同步
- 聊天系统

---

## 🔄 启动顺序

Foundation 模块的启动顺序（由 Manager 优先级控制）：

```
EventProcessor(-30)
   ↓
DataManager(-20)
   ↓
ResourceManager(0)
   ↓
NetworkManager(2)
   ↓
其他 Manager(10+)
```

**关键点**：
1. DataManager 最早启动，准备好 Service 注册机制
2. ResourceManager 启动后加载资源
3. NetworkManager 启动后准备网络连接
4. 业务 Manager 最后启动，此时所有基础设施就绪

---

## ⚠️ 注意事项

### DataManager

- ✅ Service 必须 `public`（反射创建）
- ✅ DAO 类必须 `[Serializable]`
- ❌ 禁止存储 GameObject / MonoBehaviour / Transform
- ⚠️ DataService 不监听自己的 EVT_INITIALIZED（避免无限递归）

### ResourceManager

- ✅ 资源路径相对 `Resources/`，不带扩展名
- ✅ 支持自动 fallback 到候选子目录
- ⚠️ FBX 动画需要在 `Resources/CharacterFBXManifest.json` 中配置
- ⚠️ 引用计数：300 秒无使用自动卸载

### NetworkManager

- ✅ Mirror 未安装时命令返回 Fail（不会编译错误）
- ✅ 事件驱动（无需直接引用 Mirror）
- ⚠️ 消息 payload 必须可 JSON 序列化
- ⚠️ 多人游戏需要处理网络延迟和同步问题

---

## 📊 性能指标

| 模块 | 优化项 | 预期效果 |
|---|---|---|
| DataManager | 统一持久化 | 无额外开销 |
| ResourceManager | 引用计数 | -2~5MB |
| NetworkManager | 事件驱动 | 无额外开销 |

---

## 📌 总结

**Foundation 模块提供框架的基础设施服务**：
- ✅ DataManager — 数据持久化和 Service 自动注册
- ✅ ResourceManager — 资源加载、缓存、预加载
- ✅ NetworkManager — 多人联机网络通讯

**推荐使用**：
1. 所有 Service 自动注册到 DataManager
2. 所有资源加载通过 ResourceManager
3. 网络通讯通过 NetworkManager 事件驱动

**启动顺序**：
1. EventProcessor(-30)
2. DataManager(-20)
3. ResourceManager(0)
4. NetworkManager(2)
5. 业务 Manager(10+)

---

**Foundation 模块已分类完成！**
