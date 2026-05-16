# 反模式黑名单（Anti-Patterns）

> **写代码前必读。** 看到 AI 或人写出以下任何一条 → 立刻拒绝 / 重写，无需讨论。
>
> 本文是"硬性禁令"清单，配合 `Agent.md` 的"必须做"形成完整规则。条目按危害程度从高到低排列。

---

## 🔴 P0 — 框架契约级（破坏后果：运行期崩溃 / 数据丢失 / 启动失败）

### A1. ❌ `[Event("...")]` 用字符串字面量
```csharp
// ❌ 禁止
[Event("OpenInventoryUI")]
public List<object> Open(...) { ... }

// ✅ 必须
public const string EVT_OPEN_UI = "OpenInventoryUI";
[Event(EVT_OPEN_UI)]
public List<object> Open(...) { ... }
```
**理由**：根 `Agent.md` §4.1 强制规则，违反即丢失 IDE 跳转 / 重命名安全 / 全局可搜索。

### A2. ❌ 跨模块调用为读常量而 `using` 其他业务模块
事件名遵循**非对称协议**（`Agent.md` §4.1）：定义方常量、消费方字符串。

```csharp
// ❌ 跨模块仅为读 EVT_X 常量而 using 其他业务 Manager ——等同于运行时引用，破坏 Event 解耦
using EssSystem.Core.Application.SingleManagers.InventoryManager;
EventProcessor.Instance.TriggerEventMethod(InventoryManager.EVT_OPEN_UI, data);

// ✅ 跨模块消费方：直接 bare-string
EventProcessor.Instance.TriggerEventMethod("OpenInventoryUI", data);

// ✅ 同模块（本就在 using 范围内）：继续用常量
EventProcessor.Instance.TriggerEventMethod(EVT_OPEN_UI, data);
```

**理由**：`agent_lint.ps1 [6]` 扫描所有 bare-string、cross-ref 全工程 `EVT_XXX` 常量池——拼错会打不过 lint。查 Event 不要猜：先看根 `Agent.md` 的【全局 Event 索引】表，再跳转到模块 `Agent.md` 的 `## Event API` 节。

### A3. ❌ Service 里存 `GameObject` / `MonoBehaviour` / `Transform`
```csharp
// ❌ Unity 对象不可序列化，持久化会炸
public class FooService : Service<FooService>
{
    private GameObject _player;  // ← 错
    public void Save() { SetData("cat", "player", _player); }  // ← 必崩
}
```
**理由**：`Service<T>` 数据走 MiniJson + `AssemblyQualifiedName` 持久化，Unity 对象无法序列化。**Service 只存纯 C# 数据**；运行时实例放 `Manager<T>`（MonoBehaviour）。

### A4. ❌ DAO / 持久化数据类不加 `[Serializable]`
```csharp
// ❌
public class QuestData { public string id; public int progress; }

// ✅
[Serializable]
public class QuestData { public string id; public int progress; }
```
**理由**：MiniJson 反序列化依赖。漏加 → 加载时字段全空。

### A5. ❌ 改了 `[Event]` 不同步改 `Agent.md`
**理由**：`Agent.md` 的 `## Event API` 章节 + 根 `Agent.md` 全局索引是 AI 唯一可信的 Event 文档。腐烂一次，后续 AI 全部走错路。**修改 Event 后必做自查**：「我新增/改了哪个 EVT_XXX？对应 Agent.md 改了吗？根 Agent.md 索引改了吗？」

### A6. ❌ 业务 Manager 之间直接 `using`
```csharp
// ❌ InventoryManager 里
using EssSystem.Manager.QuestManager;
QuestManager.Instance.AcceptQuest(...);

// ✅
EventProcessor.Instance.TriggerEventMethod("AcceptQuest", data);
```
**理由**：跨模块解耦的核心契约。直 `using` 会让模块图变成网状，重构地狱。**例外**：业务 Manager 调框架 Manager（`UIManager` / `ResourceManager`）走事件即可，但**同模块**的 Manager → Service 必须直调（性能 + 类型安全）。

### A7. ❌ 跨模块 Event 参数 / 返回值泄露模块私有类型
```csharp
// ❌ 返回 UIEntity —— 调用方被迫 using UIManager.Entity 命名空间
[Event(EVT_GET_ENTITY)]
public List<object> GetUIEntity(List<object> data) => ResultCode.Ok(_entity);  // UIEntity

// ✅ 只返 Unity 中立类型
[Event(EVT_GET_UI_GAMEOBJECT)]
public List<object> GetUIGameObject(List<object> data) => ResultCode.Ok(_entity.gameObject);
```
**理由**：跨模块协议只能用 `GameObject` / `Transform` / `Vector3` / `string id` 等 Unity 中立类型。泄露 `UIEntity` / `Character` / `Entity` / `UIComponent` 子类等模块私有类型 = 购者必须 `using` 发布者，与 A6 同质。参考：`UIManager.EVT_GET_UI_GAMEOBJECT` 返 `GameObject`；`CharacterManager.EVT_CREATE_CHARACTER` 返 `Transform`。

---

## 🟠 P1 — 架构纪律级（破坏后果：可维护性塌陷）

### B1. ❌ 在 `Awake` / 构造函数里调用更低优先级的 Manager
```csharp
// ❌ InventoryManager(10) 的 Awake 里调 UIManager(5) 时它在但 ResourceManager(0) 已注册了
// 但你拿不到尚未 Initialize 的 Manager 的 Service 实例
[Manager(10)]
public class InventoryManager : Manager<InventoryManager>
{
    protected override void Initialize() { UIManager.Instance.RegisterX(...); }  // ← 还行
    private void Awake() { ResourceManager.Instance.GetX(...); }  // ← 危险，绕过 [Manager(N)] 排序
}
```
**理由**：`AbstractGameManager` 通过 `[Manager(N)]` 排序后**依次** `Initialize`。Awake 顺序由 Unity 决定（仍走 `[DefaultExecutionOrder]` 但更脆弱）。**业务初始化一律放 `Initialize` override，不要用 `Awake`**。

### B2. ❌ `Manager<T>` 写 `private void Update()` 而非 `protected override void Update()`
```csharp
// ❌ 静默隐藏框架 Update（无 base 调用 → 框架钩子失效）
private void Update() { ... }

// ✅
protected override void Update()
{
    base.Update();
    // your logic
}
```
**理由**：`Manager<T>` 的虚生命周期方法（Update/Initialize 等）有框架职责。覆盖时**必须** `protected override` + `base.Xxx()`。

### B3. ❌ 给 `Manager<T>` / `Service<T>` 加构造函数
```csharp
// ❌ 单例由框架构造，写构造函数会迷惑实例化路径
public class FooService : Service<FooService>
{
    public FooService() { /* ... */ }  // ← 不要写
}
```
**理由**：`Service<T>` 用 `SingletonNormal<T>`（懒加载）；`Manager<T>` 是 `MonoBehaviour`（不能用构造函数初始化）。**初始化逻辑放 `Initialize()` override**。

### B4. ❌ Service 里直接 `Instantiate` / `Resources.Load`
```csharp
// ❌ 绕过缓存 + 与 ResourceManager 冲突
var prefab = Resources.Load<GameObject>("Prefabs/X");
GameObject.Instantiate(prefab);
```
**理由**：所有资源走 `ResourceManager.EVT_*` 才会进缓存 / 触发预加载 / 走外部文件路径。直接 `Resources.Load` 是双轨，导致内存泄漏排查困难。

### B5. ❌ 命令事件 vs 广播事件命名混用
约定（**强制**）：
- **命令**（让别人做某事）：`EVT_<VERB>_<NOUN>`，方法名动词开头 → `EVT_OPEN_UI` → `"OpenInventoryUI"`
- **广播**（已经发生）：广播事件常量值用 `On` 前缀字符串，方法名 `On` 开头 → `EVT_OPEN_UI` 在 Service 上对应 `"OnOpenInventoryUI"`（同名常量但语义相反，**靠模块归属区分**）

**理由**：`Inventory` 模块就有 `InventoryManager.EVT_OPEN_UI`（命令）vs `InventoryService.EVT_OPEN_UI`（广播）的命名碰撞案例。**新模块务必在 `Agent.md` 的 Event API 标注"命令/广播"**。

### B6. ❌ 同字符串多处 `[Event(...)]` 注册
```csharp
// ❌ 两个类都 [Event("Foo")] → _eventMethods 字典 key 冲突，后者覆盖前者
public class A { [Event("Foo")] public List<object> X(...) { ... } }
public class B { [Event("Foo")] public List<object> Y(...) { ... } }
```
**理由**：`EventProcessor._eventMethods` 是 `Dictionary<string, ...>`，**单一 handler**。需要多播请用 `[EventListener]`，不要用 `[Event]`。已存在的同名（如 `ResourceManager`/`ResourceService` 的 `UnloadResource`）属历史 façade dead-code，新代码禁止复制。

### B7. ❌ DAO 类放在业务模块根目录
```
Manager/QuestManager/
├── QuestManager.cs
├── QuestService.cs
└── QuestData.cs   ← ❌ 错位置

Manager/QuestManager/
├── QuestManager.cs
├── QuestService.cs
└── Dao/
    └── QuestData.cs  ← ✅
```
**理由**：根 `Agent.md` §3 文件组织规则。

### B8. ❌ UI Entity 类放业务模块
所有继承 `UIEntity` 的 MonoBehaviour **只能** 放在 `Scripts/EssSystem/Core/EssManagers/Presentation/UIManager/Entity/`。业务模块只产 DAO（`UIComponent` 子类）。
**理由**：业务模块不依赖 uGUI，方便独立测试。

### B9. ❌ 业务代码自建 Canvas / uGUI 组件
```csharp
// ❌ 绕过 UIManager 自建界面
var go = new GameObject("MyPanel");
go.AddComponent<Canvas>();
go.AddComponent<Image>();
go.AddComponent<Button>();

// ✅ 走 UIManager DAO 树
var panel = new UIPanelComponent("my_panel", "面板")
    .SetPosition(960, 540).SetSize(400, 300);
panel.AddChild(new UIButtonComponent("btn_ok", "确定"));
EventProcessor.Instance.TriggerEventMethod("RegisterUIEntity",
    new List<object> { panel.Id, panel });
```
**理由**：`Agent.md` §5 强制规则。运行时 UI 必须走 `UIManager` DAO 树。禁止 `AddComponent<Canvas/Image/Button/Text/CanvasScaler/VerticalLayoutGroup/...>`，禁止在业务层 `using UnityEngine.UI`。按钮交互用 `btnDao.OnClick += handler`，不是 `btn.onClick.AddListener`。**例外**：纯非交互 `SpriteRenderer`（如 Character 贴图）不属于 UI。参考实现：`InventoryUIBuilder.cs`。

### B10. ❌ 需高 DPI 文字就引入 TextMeshPro
框架仅用 uGUI `Text` (LegacyRuntime.ttf)，**未引入 TMP**。需要清晰文字时用**超采样**：`dao.FontSize ×= 2; dao.Size ×= 2; dao.SetScale(0.5f, 0.5f);`。倍率用整数（2×/3×），避免与 Canvas pixelPerfect 冲突。要真正矢量文字是架构级改动，禁止业务单方面加依赖。

### B11. ❌ 静态注册表用 `RuntimeInitializeLoadType.SubsystemRegistration`
```csharp
// ❌ PlayModeResetGuard 也在这阶段跑，顺序未定义 → 你的注册可能被抹掉
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
private static void AutoRegister() { MyRegistry.Register(new Template()); }

// ✅
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
private static void AutoRegister() {
    if (!MyRegistry.Contains(Id)) MyRegistry.Register(new Template());
}
```
**理由**：`Agent.md` 【项目特定规则 §1】。`PlayModeResetGuard` 也跑在 `SubsystemRegistration`，会清空已知静态注册表；同阶段多个 `RuntimeInitializeOnLoadMethod` 顺序未定义。用 `BeforeSceneLoad`（严格晚于清理，早于所有 Awake）。新增静态注册表同步在 `PlayModeResetGuard.ResetStaticRegistries()` 添加清理。

### B12. ❌ `AddComponent<XxxManager>()` 后立即改 Inspector 字段
```csharp
// ❌ AddComponent 同步触发 Awake，SetTemplateId 来不及
var mgr = new GameObject().AddComponent<MapManager>();
mgr.SetTemplateId("forest");   // Awake 已跑完，迟了

// ✅ 先 Inactive，AddComponent 不触发 Awake
var holder = new GameObject(nameof(MapManager));
holder.SetActive(false);
holder.transform.SetParent(transform);
var mgr = holder.AddComponent<MapManager>();
mgr.SetTemplateId("forest");
holder.SetActive(true);   // 此刻才同步触发 Awake/Initialize
```
**理由**：`Agent.md` 【项目特定规则 §2】。参考 `Demo/DayNight/DayNightGameManager.cs`。

---

## 🟡 P2 — 风格 / 可读性级

### C1. ❌ 用 `"成功"` / `"错误"` / `"失败"` 中文字符串当返回码
```csharp
// ❌
return new List<object> { "成功", data };

// ✅
return ResultCode.Ok(data);
```
**理由**：历史已统一到 `ResultCode.OK`/`ERROR` 常量 + `Ok()`/`Fail()`/`IsOk()`。

### C2. ❌ `TriggerEvent` vs `TriggerEventMethod` 混用
- `TriggerEventMethod(name, data)` — 调一个 `[Event]` 标注的方法（**单点 RPC**，有返回值）
- `TriggerEvent(name, data)` — 触发**广播**（所有 `[EventListener]` 监听者都跑，按优先级）

**理由**：用错会"事件没人接"或"返回值丢失"。命令型用 Method，广播型用 Event。

### C3. ❌ `[Event]` 方法名乱起
- `[Event]` 方法：动词开头（`OpenX` / `GetX` / `RegisterX`）
- `[EventListener]` 方法：`On` 开头（`OnPlayerDamage` / `OnInventoryChanged`）

### C4. ❌ 文件中间 / 函数内部写 `using`
```csharp
public void Foo()
{
    using EssSystem.Core;  // ← C# 不允许，但 AI 偶尔会幻觉出来
}
```
所有 `using` **必须**在文件顶部。

### C5. ❌ 直接读写 `EnableLogging` 字段而不是属性
`EnableLogging` 已是自动属性（历史决策表），用属性。

### C6. ❌ 给 Manager 加 `[DefaultExecutionOrder]`
**用 `[Manager(N)]` 即可**，框架内部会处理。两个一起加是冗余且可能冲突。

---

## 🚫 不要复活的"老路"（历史决策表）

读到 AI 想做以下任一，立刻打住：

| ❌ 复活的老路 | 现状 |
|---|---|
| 新建 `EventManager` 类 | 已合并入 `EventProcessor`（namespace `EssSystem.Core.Event`） |
| 用 `Event` 类名 | 已重命名为 `EventBase`（避免与 namespace 同名冲突） |
| `Manager<T>` 加空 `FixedUpdate`/`LateUpdate`/`OnEnable`/`OnDisable`/`OnApplicationFocus`/`OnApplicationPause`/`Cleanup` 占位 | 已删除，子类需要直接声明 |
| `UIService` 内部 `ServiceXxx` 事件 thin wrapper | 已删除 |
| `DataService` 用反射保存 Service | 改用 `IServicePersistence` 接口，**零反射** |
| `UIService` 用反射拿 Canvas | 改用 `UIManager.EVT_GET_CANVAS_TRANSFORM` 事件 |
| `InventoryManager` 直接 `using UIManager` | 已改为通过 Event 调，完全解耦 |
| `ResultCode.cs` 在 `Core/` 根目录 | 已移到 `Core/Util/`（namespace 仍 `EssSystem.Core`） |
| `EssManagers/` 平铺业务 Manager（极早期布局） | 已拆除 `EssManagers/`，按 `Foundation/` `Presentation/` `Application/` 三层直接平铺在 `Core/` 下（与 `Base/` 同级） |
| 业务 Manager 放在 `EssSystem/Manager/` | 仅限可选第三方模块（如 `DanmuManager`）；框架原生业务全部迁入 `Core/Foundation/` / `Core/Presentation/` / `Core/Application/` |
| `UIEntity` 命名空间为 `EssSystem.Core.UI.Entity.*` | 已收回 `EssSystem.Core.Presentation.UIManager.Entity.*`（UIManager 私有实现） |
| `EventProcessor.TriggerEventMethod` 吞后续异常 | 已解 `TargetInvocationException` 暴露真实 `InnerException`，不要加 try/catch 遮盖 |
| 在 `Manager/` 下手写业务脚手架 | 用 `tools/new-module.ps1 -Name Xxx -Priority N`，自动遵守常量化/目录结构/Agent.md 模板 |
| `AudioManager` 放 `Manager/` 基类文件夹 | 已迁到 `Core/Presentation/AudioManager/`（namespace `EssSystem.Core.Presentation.AudioManager`），优先级 `[Manager(3)]`；旧文件 `#if false` 待删 |
| `AudioManager` 手动 `AddListener` 注册事件 | 全部改 `[Event(EVT_XXX)]`，去掉 `RegisterEvents()` / `OnDestroy` 手动注销 |
| `AudioManager` 直接 `Resources.Load<AudioClip>` | 改走 ResourceManager bare-string `"GetAudioClip"` |
| 提交前不跑 `agent_lint` | `tools/install-hooks.ps1` 一次性安装 pre-commit hook，自动 `agent_lint -Strict` |

---

## 自查清单（提交前 30 秒）

- [ ] `[Event(...)]` 定义方用了 `EVT_XXX` 常量吗？（A1）
- [ ] 跨模块 `TriggerEventMethod` / `[EventListener]` 走了 bare-string 而不是 `using` 其他业务模块拿常量吗？（A2）
- [ ] 我新增/改了 `[Event]` 吗？对应模块 `Agent.md` 的 `## Event API` + 根 `Agent.md` 【全局 Event 索引】改了吗？（A5）
- [ ] 业务 Manager 之间有 `using` 吗？事件返回值有泄露 `UIEntity` / `Character` / `Entity` 吗？（A6/A7）
- [ ] Service 里有 `GameObject` / `MonoBehaviour` 字段吗？（A3）
- [ ] DAO 类加 `[Serializable]` 了吗？（A4）
- [ ] 覆盖 `Manager<T>` 生命周期方法时用了 `protected override` + `base.Xxx()` 吗？（B2）
- [ ] 资源加载走 `ResourceManager.EVT_*` 还是 `Resources.Load`？（B4）
- [ ] 新事件标了"命令"还是"广播"吗？（B5）
- [ ] UI 走了 `UIManager` DAO 树，没有自建 Canvas / `using UnityEngine.UI` 吗？（B9）
- [ ] 静态注册表用了 `BeforeSceneLoad` 而不是 `SubsystemRegistration` 吗？（B11）
- [ ] 跑了 `tools\agent_lint.ps1 -Strict` 吗（或 pre-commit hook 已安装）？

不全 ✅ 不要提交。
