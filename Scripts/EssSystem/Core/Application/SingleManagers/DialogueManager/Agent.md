# DialogueManager 指南
## 概述
`DialogueManager`（`[Manager(15)]`，薄门面）+ `DialogueService`（业务/状态机 + 持久化）提供完整对话系统：
- 一段 `Dialogue` 由若干 `DialogueLine` 组成；每行可显式 `NextLineId` 或顺序推进。
- 行内可挂 `DialogueOption[]`，选中后可：广播事件 / 触发回调 / 跳到指定行 / 切换到另一段对话。
- UI 可配置：主面板尺寸/位置/背景、说话者文本、正文文本、立绘、"下一句"按钮、选项按钮列表、关闭按钮。
- 通过 Event 调用 UIManager（`RegisterUIEntity` / `UnregisterUIEntity` / `GetUIGameObject` 等），与 UI 模块**完全解耦**。
- 单实例会话设计：同时只允许一段活动对话；启动新对话会覆盖旧的。
## 文件结构
```
DialogueManager/
├── DialogueManager.cs          薄门面：默认注册、EVT_OPEN_UI/EVT_CLOSE_UI、按钮回调、广播订阅
├── DialogueService.cs          状态机 + 持久化 + 6 个 [Event] handler + 3 个广播
├── Agent.md                    本文档
├── UI/
│   ├── DialogueUIBuilder.cs    纯静态：BuildPanelTree / ApplyLine；使用共享 Specs
│   └── DialogueUIRefs.cs       UI 子组件引用集合
└── Dao/
    ├── Dialogue.cs             一段对话（含 List<DialogueLine>）
    ├── DialogueLine.cs         一行（说话者+文本+选项+背景/立绘覆盖）
    ├── DialogueOption.cs       选项（事件名/回调/跳转）
    └── Specs/
        └── DialogueConfig.cs   UI 配置：使用 UIManager 共享 `UIPanelSpec` / `UITextSpec` / `UIButtonSpec`；嵌套 `OptionsLayout` 描述选项按钮列表堆叠
```
## 数据分类（持久化）
| 常量 | 用途 |
|---|---|
| `DialogueService.CAT_DIALOGUES` = `"Dialogues"` | 全部已注册 `Dialogue` |
| `DialogueService.CAT_CONFIGS`   = `"Configs"`   | 全部已注册 `DialogueConfig` |
会话状态（`ActiveDialogueId` / `ActiveLineId` / `ActiveConfigId`）属运行时数据，**不写盘**。
## 推进规则速查
| 当前行状态 | UI 显示 | 用户操作 |
|---|---|---|
| `Options` 非空 | 选项按钮列表 | 点击某选项 → `SelectOption(index)` |
| `Options` 为空 + `NextLineId` 有值 | "下一句" 按钮 | 点击 → 跳到 `NextLineId` |
| `Options` 为空 + 末尾行 | "下一句" 按钮 | 点击 → 结束对话 |
`DialogueOption` 三类副作用按顺序触发：① 广播 `EventName(EventArgs...)` → ② `OnSelected` 回调 → ③ 跳转。
## Event API

> Full Event definitions (params / return / side effects / usage) live in root Events.md -> section: **DialogueManager Event**.

- `CharacterManager.EVT_GET_PART_SPRITE_ID`
- `DialogueManager.EVT_CLOSE_UI`
- `DialogueManager.EVT_OPEN_UI`
- `DialogueManager.EVT_SET_PORTRAIT_SPRITE`
- `DialogueService.EVT_ADVANCE`
- `DialogueService.EVT_END`
- `DialogueService.EVT_ENDED`
- `DialogueService.EVT_LINE_CHANGED`
- `DialogueService.EVT_QUERY_CURRENT`
- `DialogueService.EVT_REGISTER_CONFIG`
- `DialogueService.EVT_REGISTER_DIALOGUE`
- `DialogueService.EVT_SELECT_OPTION`
- `DialogueService.EVT_STARTED`
- `UIManager.EVT_REGISTER_ENTITY`

## 使用范例
### 1. 在游戏内代码动态构造对话并打开 UI
### 2. 监听选项广播事件（业务模块）
### 3. 自定义对话框 UI（背景/位置）
> **Specs 共享**：面板 / 文本 / 按钮 子组件都使用 UIManager 提供的 `UIPanelSpec` / `UITextSpec` / `UIButtonSpec`，与 Inventory 模块保持一致。仅 `OptionsLayout`（选项按钮堆叠列表）为 Dialogue 独有。
## 与其他模块的协作
| 场景 | 路径 |
|---|---|
| 启动对话 | 业务 → `EVT_OPEN_UI`（命令） |
| 接收选项触发的事件 | 业务用 `[EventListener("XXX")]` 订阅 `option.EventName` |
| UI 刷新（DialogueManager 内部） | 订阅 `EVT_LINE_CHANGED` → `RefreshCurrentLine` |
| 对话结束清理 | 订阅 `EVT_ENDED`（外部业务清场景状态等） |
## 默认行为
`DialogueManager` Inspector 提供两个 toggle：
- `_registerDefaultConfig` — 启动注册一份 `Default` 配置（id=`DialogueManager.DefaultConfigId`）
- `_registerDebugDialogue` — 启动注册一段 `DebugDialogue` 用以开箱即用调试，可通过右键菜单"打开调试对话"立刻看到效果
线上发版前请把这两个开关关闭。
