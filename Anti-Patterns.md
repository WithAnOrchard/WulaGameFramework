# 反模式黑名单（Anti-Patterns）

> 本文件记录项目中明确禁止或需要强约束的写法。`Agent.md` 负责说明“应该怎么做”，本文件负责说明“哪些做法不要再出现”。

使用方式：

- 写代码前先看相关模块的 `Agent.md`，再看本文件中对应禁区。
- 如果发现代码已经存在反模式，先判断是不是历史遗留；新增代码不允许继续复制。
- 如果确实需要例外，必须写在模块 `Agent.md` 中，说明适用范围、理由和替代检查方式。

## P0：框架契约级禁区

这类问题会造成模块耦合、事件失效、数据损坏、启动失败或构建资源失控。新增代码必须避免。

### A1. 禁止在 `[Event(...)]` 定义侧直接写字符串字面量

错误写法：

```csharp
[Event("OpenInventoryUI")]
public List<object> Open(List<object> data) { ... }
```

正确写法：

```csharp
public const string EVT_OPEN_UI = "OpenInventoryUI";

[Event(EVT_OPEN_UI)]
public List<object> Open(List<object> data) { ... }
```

原因：定义侧必须可搜索、可重命名、可被 `agent_lint` 追踪。字符串字面量会让事件协议失去来源。

### A2. 禁止为了读取事件常量而跨业务模块 `using` 对方 Manager

错误写法：

```csharp
using EssSystem.Core.Application.SingleManagers.InventoryManager;

EventProcessor.Instance.TriggerEventMethod(InventoryManager.EVT_OPEN_UI, data);
```

正确写法：

```csharp
EventProcessor.Instance.TriggerEventMethod("OpenInventoryUI", data);
```

规则：

- 事件定义方使用 `EVT_XXX` 常量。
- 同模块内部可以直接使用本模块常量。
- 跨模块消费方优先使用字符串，并依赖 lint 反查常量池。
- 不要为了“少写字符串”引入业务模块引用。

原因：事件名是协议，不是运行时依赖。消费方如果 `using` 对方业务 Manager，只是把事件解耦又绕回直接耦合。

### A3. 禁止同一个事件字符串注册多个 `[Event]` 处理器

错误写法：

```csharp
public class A
{
    [Event("Foo")]
    public List<object> HandleA(List<object> data) { ... }
}

public class B
{
    [Event("Foo")]
    public List<object> HandleB(List<object> data) { ... }
}
```

正确做法：

- 命令型事件只保留一个 `[Event]` 处理器。
- 多播通知使用 `[EventListener]`。
- Facade 与 Service 共用字符串时，要确认是否为历史兼容；新增代码不要复制这种结构。

原因：命令事件通常由字典按字符串注册，重复 key 会覆盖或产生不可预期结果。

### A4. 禁止新增或修改事件后不更新文档

凡是新增、删除、重命名 `[Event]` / `[EventListener]` / `EVT_XXX`，必须同步检查：

- 对应模块 `Agent.md` 的 `## Event API` 是否需要更新。
- 全局事件索引或相关文档是否需要同步。
- `tools/agent_lint.ps1 -Strict` 是否通过。

原因：事件是 AI 和人工协作时最容易误用的协议层。文档过期会导致后续修改持续走错方向。

### A5. 禁止业务 Manager 之间直接互相持有或直接驱动

错误写法：

```csharp
public class InventoryManager : Manager<InventoryManager>
{
    private SkillManager _skillManager;

    protected override void Initialize()
    {
        _skillManager = SkillManager.Instance;
        _skillManager.Cast(...);
    }
}
```

正确做法：

- 优先拆小接口或上下文对象。
- 低频跨模块命令走事件。
- 数据查询优先走稳定只读 API。
- EntityManager 与 SkillManager 尤其不能重新形成双向强引用。

原因：业务 Manager 互相持有会让模块图变成网状，后续 Demo 拆分、构建裁剪和单模块测试都会变困难。

### A6. 禁止跨模块事件参数暴露模块私有类型

错误写法：

```csharp
[Event(EVT_GET_UI_ENTITY)]
public List<object> GetUIEntity(List<object> data)
{
    return ResultCode.Ok(_entity); // UIEntity 属于 UIManager 内部实现
}
```

正确做法：

- 跨模块事件返回 `string id`、`GameObject`、`Transform`、`Vector3`、纯数据结构等稳定类型。
- 模块私有类型只在模块内部 API 中流动。

原因：事件参数一旦暴露私有类型，消费方就必须 `using` 发布模块命名空间，事件解耦会失效。

### A7. 禁止 Service 持久化 Unity 对象

错误写法：

```csharp
public class FooService : Service<FooService>
{
    private GameObject _player;
    private Transform _target;
}
```

正确做法：

- Service 存纯 C# 数据、配置、id、快照、状态机数据。
- GameObject / MonoBehaviour / Transform 等运行时对象放在 Manager 或场景组件里。
- 必要时 Service 只保存对象 id，由 Manager 负责绑定运行时实例。

原因：Unity 对象无法稳定序列化，也不应该跨场景长期持久化。

### A8. 禁止持久化数据类缺少 `[Serializable]`

错误写法：

```csharp
public class QuestData
{
    public string Id;
    public int Progress;
}
```

正确写法：

```csharp
[Serializable]
public class QuestData
{
    public string Id;
    public int Progress;
}
```

原因：持久化和反序列化依赖可序列化数据结构。漏掉后容易出现加载字段为空的问题。

### A9. 禁止无范围资源扫描和全量加载

错误写法：

```csharp
Resources.LoadAll<GameObject>("");
Resources.LoadAll<Sprite>("");
```

正确做法：

- ResourceManager / ResourceService 必须按 Demo、模块或资源清单加载。
- Tribe 构建只纳入 Tribe 资源和声明共享资源。
- Editor 调试用全量加载必须有明确开关，不能影响构建包。

原因：无范围扫描会拖慢启动，污染 Demo 构建资源，也会让资源问题难以定位。

## P1：架构纪律级禁区

这类问题通常不会立刻炸，但会持续增加维护成本。

### B1. 禁止给 Manager 手写 `[DefaultExecutionOrder]`

正确做法：

```csharp
[Manager(10)]
public class InventoryManager : Manager<InventoryManager>
{
}
```

规则：

- Manager 加载顺序统一通过 `[Manager(N)]` 表达。
- 调整顺序时同步更新根 `Agent.md` 中的 Manager 顺序表。
- 同一优先级内不要依赖固定先后。

原因：`ManagerAttribute` 已继承 Unity 执行顺序能力。再额外写 `[DefaultExecutionOrder]` 会产生重复规则。

### B2. 禁止在 Manager 子类里用 `private void Update()` 隐藏框架生命周期

错误写法：

```csharp
private void Update()
{
    // 会隐藏基类 Update，导致框架钩子不稳定
}
```

正确写法：

```csharp
protected override void Update()
{
    base.Update();
    // 自己的逻辑
}
```

原因：Manager 基类已经承担 Inspector 同步等框架职责，覆盖生命周期时必须保留基类行为。

### B3. 禁止给 Manager / Service 写构造函数承载初始化逻辑

错误写法：

```csharp
public class FooService : Service<FooService>
{
    public FooService()
    {
        LoadConfig();
    }
}
```

正确做法：

- Service 初始化写在 `Initialize()`。
- Manager 初始化写在 `Initialize()`。
- MonoBehaviour 运行时引用绑定放在 Awake / Start，但要尊重 `[Manager(N)]` 顺序。

原因：Manager 是 MonoBehaviour，Service 是框架单例。构造函数不是可靠的 Unity / 框架生命周期入口。

### B4. 禁止 AddComponent 后再设置会影响 Awake 的字段

错误写法：

```csharp
var holder = new GameObject(nameof(MapManager));
var map = holder.AddComponent<MapManager>();
map.SetTemplateId("forest"); // Awake 可能已经跑完
```

正确写法：

```csharp
var holder = new GameObject(nameof(MapManager));
holder.SetActive(false);
holder.transform.SetParent(transform);
var map = holder.AddComponent<MapManager>();
map.SetTemplateId("forest");
holder.SetActive(true);
```

原因：`AddComponent` 会同步触发 Awake。需要先配置再初始化时，应先创建 inactive 对象。

### B5. 禁止 Service 直接 Instantiate 或直接 Resources.Load

错误写法：

```csharp
var prefab = Resources.Load<GameObject>("Prefabs/X");
var go = GameObject.Instantiate(prefab);
```

正确做法：

- 资源定位走 ResourceManager / ResourceService 的统一入口。
- 实例化运行时对象由 Manager、工厂或场景组件负责。
- 高频 VFX 等可使用对象池，但资源来源仍需统一。

原因：Service 直接加载和实例化会绕过缓存、资源清单、构建裁剪和释放策略。

### B6. 禁止业务代码自建 Canvas 或绕过 UIManager 管 UI

错误写法：

```csharp
var go = new GameObject("Panel");
go.AddComponent<Canvas>();
go.AddComponent<UnityEngine.UI.Button>();
```

正确做法：

- 窗口打开、关闭、层级、输入阻挡通过 UIManager。
- Demo UI 可以有自己的样式构建器，但入口和生命周期要回到 UIManager。
- UI 对齐、缩放、超采样策略要在统一 UI 构建逻辑中处理。

原因：UI 一旦分散创建，窗口层级、输入遮挡、适配和释放都会失控。

### B7. 禁止把 Demo 专属逻辑塞进框架公共层

错误写法：

```csharp
// ResourceManager 中硬编码 Tribe 路径和玩法规则
if (demo == "Tribe") LoadCampfireOnly();
```

正确做法：

- 框架层提供通用资源范围、清单、构建参数能力。
- Demo 层声明自己需要的路径、资源组、启动配置。
- 通用能力被第二个 Demo 复用后，再考虑上移框架层。

原因：Demo 逻辑污染框架后，其他 Demo 构建和运行会被迫承担无关规则。

### B8. 禁止在启动阶段做全项目反射、全资源加载或同步重 IO

正确做法：

- 能构建期生成的清单不要运行时扫描。
- 必须运行时扫描时，按 Demo / 模块 / Manager 明确范围。
- 排查耗时时补充分段计时日志，不盲目猜瓶颈。

原因：Tribe 启动卡顿已经暴露过这类问题，启动流程需要持续收敛。

### B9. 禁止静态注册表只依赖 `SubsystemRegistration` 自动注册

正确做法：

- 清理静态状态可以放在 `SubsystemRegistration`。
- 自动注册更适合 `BeforeSceneLoad` 或显式初始化入口。
- 新增静态注册表时，同步考虑 Play Mode 重置逻辑。

原因：同阶段多个 RuntimeInitialize 方法顺序不稳定，可能出现刚注册又被清理的情况。

## P2：可维护性与风格禁区

这类问题会降低可读性和协作效率，新增代码也应避免。

### C1. 禁止用中文字符串当 ResultCode

错误写法：

```csharp
return new List<object> { "成功", data };
return new List<object> { "失败", error };
```

正确写法：

```csharp
return ResultCode.Ok(data);
return ResultCode.Fail(error);
```

原因：返回码必须稳定、可判断、可被工具和调用方统一处理。

### C2. 禁止混用 `TriggerEvent` 与 `TriggerEventMethod`

规则：

- `TriggerEventMethod`：调用单个 `[Event]` 命令，有返回值。
- `TriggerEvent`：广播给多个 `[EventListener]`，通常表达已发生事实。

原因：用错会导致“没有返回值”“没人处理”“监听器被当命令”等隐蔽问题。

### C3. 禁止 `[Event]` / `[EventListener]` 命名语义混乱

规则：

- `[Event]` 方法使用动词开头，例如 `OpenInventory`、`GetSprite`、`RegisterUI`。
- `[EventListener]` 方法使用 `On` 开头，例如 `OnInventoryChanged`。
- 命令事件常量用 `EVT_VERB_NOUN`。
- 广播事件字符串通常表达已发生事实。

原因：命令和广播混在一起会让调用方不知道该等返回值还是监听通知。

### C4. 禁止在函数中间写 `using`

错误写法：

```csharp
public void Foo()
{
    using EssSystem.Core;
}
```

正确做法：

- C# using 放在文件顶部。
- 如果只用一次类型，优先完整限定名或整理文件顶部 using。

### C5. 禁止留下临时日志、临时菜单和临时测试入口

规则：

- 排查问题可以加临时日志，但完成后要移除或收敛到可开关日志。
- Demo 专属日志落盘必须可关闭，发布包中不能默认刷屏。
- Editor 菜单新增前先归类到清晰层级。

原因：工具和日志入口散乱后，后续构建和排查会变得很难信任。

### C6. 禁止中文文档写入乱码或混用错误编码

规则：

- 所有 `Agent.md`、`Anti-Patterns.md`、`TODO.md` 保持中文 UTF-8。
- 修改中文文档后必须跑乱码扫描。
- PowerShell 控制台显示乱码不等于文件内容乱码，必要时用 UTF-8 方式读取确认。

原因：文档是后续 AI 和人工判断架构的入口，乱码会直接破坏协作质量。

## 数据密集型直接 API 例外

事件系统不是所有调用的唯一答案。满足以下条件时，可以保留直接 C# API：

- 高频访问，事件分派和装拆箱成本不可接受。
- 参数或返回值包含强领域类型，事件化会明显破坏类型安全。
- 调用关系属于同一内聚子系统内部，而不是跨业务 Manager 随意互调。
- 模块 `Agent.md` 明确写出 public API 清单、适用范围和为什么不事件化。

当前可参考的例外方向：MapManager / Voxel3DMapManager 这类地形、chunk、持久化强内聚子系统。例外不自动扩散到 Foundation、Presentation 或普通业务 Manager。

## 不要复活的旧路

以下做法属于历史决策中已经放弃的方向，不要重新引入：

| 旧路 | 当前规则 |
|---|---|
| 新建独立 EventManager | 使用 EventProcessor |
| 给 Manager 同时写 `[DefaultExecutionOrder]` 和 `[Manager(N)]` | 只写 `[Manager(N)]` |
| Service 反射保存所有内部状态 | 通过明确持久化数据和接口处理 |
| UIService 反射查 Canvas | 通过 UIManager 公开入口 |
| 业务 Manager 放进无边界的公共 Manager 目录 | 按 Foundation / Presentation / Application / Demo 边界归位 |
| 运行时全量 Resources 扫描 | 按 Demo / 模块 / 清单收敛加载 |
| 构建前手动跑零散配置工具 | 统一收敛到 BuildSystem |
| 新增工具随意挂菜单 | 先归类，再加入清晰菜单层级 |

## 提交前自查

提交前至少确认：

- `[Event(...)]` 定义侧使用了 `EVT_XXX` 常量。
- 跨模块消费事件没有为了读常量而 `using` 对方业务模块。
- 新增或修改 Event 后，对应模块 `Agent.md` 已同步。
- Service 没有持有需要持久化的 Unity 对象。
- DAO / 持久化数据类有 `[Serializable]`。
- Manager 生命周期覆盖使用 `protected override` 并调用 `base`。
- 没有新增无范围资源扫描或启动期全量加载。
- UI 生命周期仍由 UIManager 统一管理。
- 没有把 Demo 专属逻辑塞进框架公共层。
- 中文文档无乱码，`agent_lint` 和相关检查通过。
