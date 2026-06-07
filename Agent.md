# WulaGameFramework 项目级 Agent 规范

> 本文件是整个 `Assets/` 工作区的顶层协作规范。进入具体模块前，先读本文件；进入模块后，再读取对应目录下的 `Agent.md`。如果根目录规范与模块规范冲突，以更严格、更靠近代码所有权边界的规则为准，并在修改前先确认设计意图。

> Git 仓库根目录位于 `Assets/`，不是 Unity 项目根目录。执行 `status`、`commit`、`push` 等 Git 命令时使用 `git -C Assets ...`，避免在项目根目录误判为非仓库。

## 1. 项目定位

WulaGameFramework 是一个 Unity 游戏框架项目，目标是沉淀可复用的游戏基础设施，并通过多个 Demo 验证框架能力。

项目开发时要同时关注三件事：

- 框架层保持通用、稳定、低耦合。
- Demo 层只表达玩法验证，不反向污染框架。
- 工具链要能持续检查、构建、生成资源索引和暴露问题。

所有修改都应尽量沿着已有模块边界推进，不要为了快速修复把 Demo 逻辑塞进 Foundation、Core 或通用 Manager 中。

## 2. 文档查询顺序

处理任何任务前，按下面顺序读取文档：

1. 根目录 `Agent.md`：确认全局规则和工程约束。
2. 相关模块目录下的 `Agent.md`：确认模块职责、禁区和常用入口。
3. `TODO.md` 或模块内 TODO：确认已记录的解耦项、技术债和未完成事项。
4. 相关源码：以代码现状为准，文档只提供方向，不能替代代码检查。

如果发现文档过期，要优先小范围修正文档；如果发现 TODO 已完成，要同步移除或改成已完成说明，避免后续误导。

## 3. 目录与模块边界

主要目录职责如下：

- `Scripts/EssSystem/Foundation`：基础设施层，放资源、事件、日志、配置、构建辅助等底层能力。
- `Scripts/EssSystem/Core`：框架核心层，放实体、表现、技能、UI、数据流、生命周期等通用系统。
- `Scripts/EssSystem/Managers`：通用 Manager 聚合与生命周期协调，禁止堆积 Demo 专属逻辑。
- `Scripts/EssSystem/Services`：跨场景或长期存在的服务，重点关注初始化顺序和释放边界。
- `Demo/`：玩法 Demo。Demo 可以依赖框架能力，但框架不得依赖 Demo。
- `Editor/`、`tools/`：编辑器菜单、构建、检查、生成与维护工具。
- `FrameworkResources/`：框架和 Demo 可控资源目录。需要按 Demo 或模块收敛资源加载与构建范围。

跨目录移动代码前，先确认该能力属于“通用框架能力”还是“当前 Demo 验证逻辑”。无法确定时，优先留在更靠近业务的一侧。

## 4. Manager 清单与职责

本文档只记录项目中已经存在、或代码中明确规划的 Manager，不凭常见框架模板补名字。当前代码里主要 Manager 分布如下：

- Foundation：ResourceManager、DataManager、NetworkManager。
- Presentation：UIManager、AudioManager、InputManager、CameraManager、EffectsManager、LightManager、CharacterManager。
- Application / SingleManagers：EntityManager、InventoryManager、DialogueManager、SceneInstanceManager、AutoUpdateManager。
- Application / MultiManagers：SkillManager、MapManager、Voxel3DMapManager、VoxelLightingManager、FarmManager、CraftingManager、BuildingManager、NpcManager、ShopManager。
- EssSystem / Manager：LiveStatusManager、BilibiliDanmuManager。
- Demo：DobeCatGameManager、TribeGameManager、CubicGameManager 等 Demo 自身入口。

当前 `[Manager(N)]` 加载优先级如下，数值越小越先进入 Unity 的执行顺序：

1. `-30`：EventProcessor
2. `-25`：AutoUpdateManager
3. `-20`：DataManager
4. `0`：ResourceManager
5. `2`：InputManager、NetworkManager
6. `3`：AudioManager
7. `4`：CameraManager
8. `5`：UIManager
9. `6`：EffectsManager
10. `7`：LightManager
11. `10`：InventoryManager
12. `11`：CharacterManager
13. `12`：MapManager
14. `13`：EntityManager、Voxel3DMapManager
15. `14`：BuildingManager、VoxelLightingManager
16. `15`：DialogueManager、SkillManager
17. `16`：SceneInstanceManager
18. `17`：NpcManager
19. `18`：CraftingManager、FarmManager
20. `19`：ShopManager
21. `50`：LiveStatusManager、BilibiliDanmuManager

同一优先级内不依赖固定先后；如果两个 Manager 之间有真实依赖，需要拆出接口、事件或显式初始化上下文，不要依赖同优先级的偶然执行顺序。`AbstractGameManager` 会发现并按优先级登记子节点上的 Manager，但 Manager 自身的 `Initialize` 仍由各自 `Awake` 触发。

职责边界：

- ResourceManager 只负责资源定位、加载、缓存和释放策略，不承载玩法逻辑。
- DataManager 负责数据读取与访问入口，不替代具体业务 Manager。
- UIManager 统一负责 UI 打开、关闭、层级、输入阻挡、窗口生命周期。
- EntityManager 负责实体生命周期、注册、查询、回收，不直接驱动技能业务。
- SkillManager 负责技能定义、释放、冷却、目标校验，不直接持有具体 Demo 场景逻辑。
- SceneInstanceManager 负责场景实例相关能力，不等同于 Unity 的 SceneManager，也不承担资源清单构建职责。

Manager 启动顺序必须以代码中的实际初始化链路、组件挂载和 `ManagerAttribute` / Unity 执行顺序为准。调整顺序时必须同步修改对应类上的 `[Manager(N)]`，并同步更新本节。

EntityManager 与 SkillManager 是当前重点解耦区域：实体系统提供抽象能力，技能系统通过接口、事件或上下文访问实体，不应形成双向强引用。

## 5. 通信与解耦规则

模块间通信优先级：

1. 明确接口或上下文对象。
2. 事件系统。
3. 只读配置或数据表。
4. Manager 查询。
5. 直接引用具体实现。

原则：

- Demo 不要直接修改框架内部状态，应通过公开 API 或事件表达意图。
- Framework 不要引用 Demo 命名空间。
- Manager 之间禁止随意互相 new、互相初始化或循环持有。
- 需要跨模块通信时，优先定义小接口，不要把完整 Manager 暴露出去。
- 一次性脚本可以留在 Demo，但如果第二个 Demo 也要用，就考虑抽到框架层。

## 6. 输入规则

所有运行时输入读取和按键绑定都必须经过 `InputManager` 的统一输入管道。
规则：
- 业务、Demo、UI、交互、角色控制、快捷栏、调试热键等代码不得自行定义 `KeyCode` 字段或直接调用 `Input.GetKey`、`Input.GetKeyDown`、`Input.GetKeyUp`、`Input.GetAxis`、`Input.GetAxisRaw`、`Input.GetMouseButton*`、`Input.mousePosition`、`Input.mouseScrollDelta` 等 Unity 输入 API。
- 新增按键时，先在 `InputManager` 中声明稳定的 Action 名和默认绑定，再由业务代码通过 `InputManager.IsPressed`、`InputManager.IsDown`、`InputManager.IsUp`、`InputManager.GetMoveAxis`、鼠标查询 API 或对应事件使用。
- 数字快捷键、背包开关、交互、跳跃、攻击、冲刺、取消、调试热键等都应表达为 Action，不应在业务侧写死具体键位。
- 需要广播输入变化时使用 `InputManager.EVT_INPUT_DOWN` / `InputManager.EVT_INPUT_UP`；高频或无人监听场景不得产生刷屏日志。
- 唯一允许直接访问 Unity `Input` 的位置是 `InputManager` 内部实现，或经过明确文档说明的底层平台适配/窗口穿透等特殊代码；特殊例外必须在对应模块 `Agent.md` 或代码注释中说明原因。
- 发现旧代码直接读取输入时，应优先迁移到 `InputManager`，不要继续复制旧写法。

## 7. Event 规则

事件系统要支持按 Demo、模块和 Manager 收敛扫描范围，避免启动时全量扫描所有无关事件。

规则：

- 事件定义应放在所属模块或 Demo 的 `Event` / `Events` 目录下。
- Demo 启动时只扫描自己的事件，以及显式依赖的 Manager / 模块事件。
- 框架公共事件可以放在 Foundation 或 Core 的公共事件目录，但要保持稳定、少量、语义清晰。
- 事件名应表达业务事实，而不是调用动作，例如 `InventoryItemAdded` 优于 `CallRefreshInventory`。
- 事件载荷使用小型不可变数据或只读结构，避免塞入大型 Manager、GameObject 或可变集合。
- 缺失事件目标时应有清晰日志，不能静默失败，也不能在正常缺失场景刷屏。

## 8. 配置规则

系统默认配置必须优先抽离为 JSON 数据文件，代码只负责加载、校验、注册和少量运行时转换。
规则：
- 默认物品、容器、UI 布局、建筑模板、地图模板、角色模板、输入默认绑定、调试模板等不得继续散落在 Manager 的 `RegisterDefault*` / `BuildDefault*` 代码中。
- 默认配置文件统一放在 `FrameworkResources/Config` 目录体系下；编辑期源文件放 `Assets/FrameworkResources/Config/...`。
- 构建后由 BuildSystem 将 `Assets/FrameworkResources` 导出到可执行文件同级 `FrameworkResources`；发布后配置和素材都从该目录读取和修改。
- 运行时读取顺序为可执行文件同级 `FrameworkResources/Config` 优先，其次编辑器源目录 `Assets/FrameworkResources/Config`，最后才允许极小范围代码兜底。
- 配置 JSON 字段名应尽量与 Dao 公共字段一致，避免写重复翻译层；确需运行时转换时应封装在所属 Manager 或专门 loader 中。
- `ServiceData` / `persistentDataPath` 仍用于玩家存档、运行时状态和用户修改后的持久化数据，不作为框架默认配置源头。
- 新增系统默认值时，先新增或扩展 JSON，再让系统从配置文件读取；不要把可调数值、默认模板或演示数据写死在业务代码里。

## 9. UI 规则

所有 UI 都应通过 UIManager 或其下级抽象统一管理。

规则：

- 禁止 Demo 随处直接创建、销毁或查找 UI 根节点。
- UI 窗口要有明确层级、打开方式、关闭方式和输入阻挡策略。
- UI 风格应尽量沿用当前 Demo 的既有视觉语言。Tribe 当前 UI 参考 DobeCat 的窗口结构，但允许保留 Tribe 的像素风主题。
- 热键栏、背包、制作台、对话框等通用交互 UI 要稳定对齐，避免运行时随文字、图标或缩放产生偏移。
- 关闭按钮、制作按钮、继续按钮等交互元素必须有足够清晰的状态和点击区域。
- UI 资源优先来自 `FrameworkResources` 中对应 Demo 或通用 UI 目录。

## 10. 资源与构建规则

ResourceManager / ResourceService 是当前重点优化区域。资源加载必须支持按 Demo 和模块收敛，避免全项目扫描和全量打包。

规则：

- 禁止在通用启动流程中使用无范围的 `Resources.LoadAll` 或等价全量扫描。
- Demo 构建时只打包该 Demo 需要的资源，以及显式声明的共享资源。
- Tribe 默认只使用 `FrameworkResources/Tribe` 及其声明依赖的共享资源。
- Editor 模式可以为调试默认加载某个 Demo 的完整资源目录，但该行为必须可控，不能影响构建包。
- Addressable / Resources / FrameworkResources 的选择要通过统一入口表达，不要在 Demo 内散落多套路径规则。
- 构建前应生成或刷新资源清单，构建后应能从日志确认实际纳入的资源范围。

## 11. BuildSystem 与版本规则

BuildSystem 是构建期自动化的统一入口。

规则：

- AutoUpdateManager 的构建期能力应合并到 BuildSystem，不再要求单独手动执行配置更新。
- 每个 Demo 都应有独立构建入口，例如 DobeCat Build 与 Tribe Build 分开维护。
- 每次 Demo 版本构建时必须更新版本号，便于服务器区分部署包。
- 构建时应自动刷新必要生成物，例如资源清单、角色清单、配置索引、版本信息。
- 构建菜单层级要清晰，避免工具入口散落在多个无意义菜单下。
- 构建日志要能定位构建目标、资源范围、版本号和失败原因。

## 12. Service 持久化规则

Service 适合承载跨场景或长期生命周期能力，但必须有清晰边界。

规则：

- Service 初始化顺序要明确，不能依赖 Unity 随机执行顺序。
- Service 不应直接持有 Demo 场景对象，必要时使用弱引用、注册表或事件连接。
- Service 应提供释放、重置或重新绑定能力，方便切换 Demo、重载场景和测试。
- 持久化状态要区分运行时缓存、玩家存档和编辑器临时数据。

## 13. 运行时性能规则

启动卡顿和运行时 spikes 要优先从扫描、反射、资源加载和日志刷屏排查。

规则：

- 启动阶段要避免全项目反射扫描、全资源加载和同步 IO。
- 必须扫描时，应按 Demo / 模块 / Manager 显式范围收敛。
- 可缓存的生成物应放到构建期或编辑器工具生成，运行时只读取结果。
- 需要排查耗时时，优先补充分段日志和简单计时器，确认真实瓶颈后再优化。
- Demo 专属日志可以额外输出到项目根目录，便于本地排查，但发布包中要可关闭。

## 14. 工具与检查

常用检查入口：

- `tools/agent_lint.ps1 -Strict`：检查 Agent 文档规范。
- `tools/check_compile_errors.ps1 -TopN 999`：检查 Unity 编译错误和警告。
- 乱码扫描：搜索常见 UTF-8 被错误解码后的异常字符组合，确认所有 `Agent.md` 都是正常中文。

修改后至少执行：

1. 与任务相关的专项检查。
2. Agent 文档改动后执行 Agent lint。
3. C# 或 Unity 相关改动后执行编译检查工具。
4. 中文文档改动后执行乱码扫描。

如果检查工具本身输出乱码，但文件内容确认是 UTF-8 正常中文，应记录这一点，不要误判为文件损坏。

## 15. Agent.md 模块文档规范

每个模块的 `Agent.md` 应保持短而明确，建议包含：

- 模块定位：这个模块解决什么问题。
- 目录职责：关键子目录分别放什么。
- 主要入口：常见 Manager、Service、Editor 工具或运行时入口。
- 修改规则：哪些可以改，哪些需要谨慎。
- 常见检查：改完要跑哪些工具或手动验证什么。
- 当前关注点：正在解耦、优化或容易踩坑的地方。

不要在模块 Agent 中堆大量历史聊天记录、临时截图结论或已经完成的 TODO。完成项应移除或压缩为当前规则。

## 16. AI 协作流程

处理任务时遵循：

1. 先读文档和相关代码，不凭印象改。
2. 先定位模块边界，再决定改哪里。
3. 小步修改，避免顺手重构无关区域。
4. 改代码后跑工具，改文档后查乱码。
5. 发现用户已有改动时，不回滚；如果影响当前任务，顺着现状继续处理。
6. 完成后说明修改内容、验证结果和剩余风险。

对于 UI 调整，要结合截图和运行表现反复确认位置、间距、层级和视觉一致性；不要只改数值后假设已经解决。

## 17. 当前重点约束

当前项目中需要持续注意：

- EntityManager 与 SkillManager 继续保持解耦，避免新增双向依赖。
- ResourceService / ResourceManager 继续从全量扫描改为按 Demo / 模块加载。
- Tribe 构建只纳入 Tribe 资源和声明共享资源。
- Tribe 的启动耗时、日志落盘、UI 对齐和资源路径仍是重点维护区域。
- 工具菜单需要保持统一层级，新增工具先归类再加入菜单。
- 所有 Agent.md 必须保持中文 UTF-8，不允许再次写入乱码内容。

## 18. 提交前检查

提交前确认：

- 没有无关格式化或大范围重排。
- 没有把 Demo 专属逻辑塞进框架公共层。
- 没有新增无范围资源扫描。
- 没有新增 Unity 编译 warning / error。
- 没有留下临时日志、临时菜单、临时测试入口。
- 文档、TODO 与实际代码状态一致。
